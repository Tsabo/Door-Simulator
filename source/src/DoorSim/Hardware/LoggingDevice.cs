using OSDP.Net;
using OSDP.Net.Model;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;
using CommunicationConfiguration = OSDP.Net.Model.CommandData.CommunicationConfiguration;
using DeviceCapabilities = OSDP.Net.Model.ReplyData.DeviceCapabilities;
using ManufacturerSpecific = OSDP.Net.Model.CommandData.ManufacturerSpecific;

namespace DoorSim.Hardware;

/// <summary>
/// Extends <see cref="Device"/> with structured logging of every CP command received,
/// and provides OSDP-compliant responses for the handshake and polling commands.
/// The base <see cref="Device"/> class returns NAK for every <c>Handle*</c> method by
/// default — all overrides here must return a real reply rather than calling base.
/// </summary>
internal sealed class LoggingDevice : Device
{
    private readonly ILogger _logger;
    private readonly int _doorId;
    private readonly string _label;
    private readonly byte _osdpAddress;
    private readonly int _baudRate;
    private readonly bool _ackManufacturerCommand;

    // Volatile reference ensures the latest state is visible across threads without locking.
    // ReaderLedState is an immutable record so partial reads are impossible.
    private volatile ReaderLedState _ledState = new(OsdpLedColor.Off, false, false);

    // Cancels any pending revert-to-permanent task when a new LED command arrives.
    private CancellationTokenSource _ledRevertCts = new();

    /// <summary>The last LED and buzzer state commanded by the panel.</summary>
    public ReaderLedState CurrentLedState => _ledState;

    private readonly Func<bool> _getDoorOpen;
    private readonly Func<bool> _getRexActive;

    private static readonly byte[] _vendorCode = [0x00, 0x00, 0x01];

    // Capabilities advertised to the CP during the osdp_CAP handshake.
    // Must match what the simulator actually supports.
    private static readonly DeviceCapabilities _pdCapabilities = new([
        new DeviceCapability(CapabilityFunction.CardDataFormat,        1, 0), // raw bits up to 1024
        new DeviceCapability(CapabilityFunction.ReaderLEDControl,      1, 1), // 1 LED, on/off only
        new DeviceCapability(CapabilityFunction.CheckCharacterSupport, 1, 0), // CRC-16
        new DeviceCapability(CapabilityFunction.CommunicationSecurity, 0, 0), // no AES
    ]);

    public LoggingDevice(
        DeviceConfiguration config,
        ILoggerFactory loggerFactory,
        int doorId,
        string label,
        int baudRate,
        Func<bool> getDoorOpen,
        Func<bool> getRexActive,
        bool ackManufacturerCommand = false)
        : base(config, loggerFactory)
    {
        _logger      = loggerFactory.CreateLogger<LoggingDevice>();
        _doorId      = doorId;
        _label       = label;
        _getDoorOpen  = getDoorOpen;
        _getRexActive = getRexActive;
        _osdpAddress  = config.Address;
        _baudRate     = baudRate;
        _ackManufacturerCommand = ackManufacturerCommand;
    }

    // -------------------------------------------------------------------------
    // Connection setup — logged at Debug; fire once per session
    // -------------------------------------------------------------------------

    protected override PayloadData HandleIdReport()
    {
        _logger.LogDebug("Door {Id} ({Label}): CP requested osdp_ID (connection setup)", _doorId, _label);
        return new DeviceIdentification(_vendorCode,
            modelNumber: 1, version: 1, serialNumber: _doorId,
            firmwareMajor: 1, firmwareMinor: 0, firmwareBuild: 0);
    }

    protected override PayloadData HandleDeviceCapabilities()
    {
        _logger.LogDebug("Door {Id} ({Label}): CP requested osdp_CAP (connection setup)", _doorId, _label);
        return _pdCapabilities;
    }

    // -------------------------------------------------------------------------
    // Panel response to card reads — logged at Information.
    // If these fire after a card send, the panel saw the card.
    // -------------------------------------------------------------------------

    protected override PayloadData HandleReaderLEDControl(ReaderLedControls controls)
    {
        var led0 = controls.Controls.FirstOrDefault(p => p.LedNumber == 0)
                   ?? controls.Controls.FirstOrDefault();
        if (led0 is null)
            return new Ack();

        // Cancel any previously scheduled revert.
        var oldCts = Interlocked.Exchange(ref _ledRevertCts, new CancellationTokenSource());
        oldCts.Cancel();
        oldCts.Dispose();

        var isTemp = led0 is
        {
            TemporaryMode: TemporaryReaderControlCode.SetTemporaryAndStartTimer,
            TemporaryTimer: > 0,
        };
        var tempVisibleBlink = ShouldVisiblyBlink(led0.TemporaryOnColor, led0.TemporaryOffColor, led0.TemporaryOffTime);
        var permVisibleBlink = led0.PermanentMode == PermanentReaderControlCode.SetPermanentState
                               && ShouldVisiblyBlink(led0.PermanentOnColor, led0.PermanentOffColor, led0.PermanentOffTime);

        _logger.LogInformation(
            "Door {Id} ({Label}): osdp_LED led={Led} tmp(mode={TmpMode},timer={TmpTimer},on={TmpOn},off={TmpOff},offTime={TmpOffTime},blink={TmpBlink}) perm(mode={PermMode},on={PermOn},off={PermOff},offTime={PermOffTime},blink={PermBlink})",
            _doorId,
            _label,
            led0.LedNumber,
            led0.TemporaryMode,
            led0.TemporaryTimer,
            led0.TemporaryOnColor,
            led0.TemporaryOffColor,
            led0.TemporaryOffTime,
            tempVisibleBlink,
            led0.PermanentMode,
            led0.PermanentOnColor,
            led0.PermanentOffColor,
            led0.PermanentOffTime,
            permVisibleBlink);

        // Permanent state — render as steady color in the mock UI.
        // Panels often use permanent blink patterns as heartbeat/idle signals, which causes
        // "always blinking" readers in the simulator and masks temporary card feedback.
        var permColor = MapLedColor(led0.PermanentOnColor);
        var permanentState = new ReaderLedState(permColor, false, _ledState.BuzzerActive);

        if (isTemp)
        {
            // Show the temporary (flash) color immediately …
            var tempColor = MapLedColor(led0.TemporaryOnColor);
            _ledState = new ReaderLedState(tempColor, tempVisibleBlink, _ledState.BuzzerActive);

            // … then revert to the permanent state after TemporaryTimer × 100 ms.
            var revertMs  = led0.TemporaryTimer * 100;
            var revertCts = _ledRevertCts;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(revertMs, revertCts.Token).ConfigureAwait(false);
                    _ledState = permanentState;
                }
                catch (OperationCanceledException) { }
            }, revertCts.Token);
        }
        else if (led0.TemporaryMode == TemporaryReaderControlCode.CancelAnyTemporaryAndDisplayPermanent
                 || led0.PermanentMode == PermanentReaderControlCode.SetPermanentState)
        {
            _ledState = permanentState;
        }

        return new Ack();
    }

    protected override PayloadData HandleBuzzerControl(ReaderBuzzerControl control)
    {
        _logger.LogInformation(
            "Door {Id} ({Label}): CP sent osdp_BUZ — panel is responding to a card event: {Control}",
            _doorId, _label, control);
        _ledState = new ReaderLedState(_ledState.Color, _ledState.Blink,
            control.ToneCode == ToneCode.Default);
        return new Ack();
    }

    protected override PayloadData HandleTextOutput(ReaderTextOutput textOutput)
    {
        _logger.LogInformation(
            "Door {Id} ({Label}): CP sent osdp_TEXT — {Text}",
            _doorId, _label, textOutput);
        return new Ack();
    }

    // -------------------------------------------------------------------------
    // Output / access control responses
    // -------------------------------------------------------------------------

    protected override PayloadData HandleOutputControl(OutputControls controls)
    {
        _logger.LogInformation(
            "Door {Id} ({Label}): CP sent osdp_OUT (relay/output control): {Controls}",
            _doorId, _label, controls);
        return new Ack();
    }

    protected override PayloadData HandleManufacturerCommand(ManufacturerSpecific commandPayload)
    {
        _logger.LogInformation(
            "Door {Id} ({Label}): CP sent osdp_MFG vendor={Vendor} data={Data}",
            _doorId, _label,
            BitConverter.ToString(commandPayload.VendorCode),
            BitConverter.ToString(commandPayload.Data));
        return _ackManufacturerCommand ? new Ack() : new Nak(ErrorCode.UnknownCommandCode);
    }

    // -------------------------------------------------------------------------
    // Ongoing status polls — logged at Trace to avoid noise at Debug
    // -------------------------------------------------------------------------

    protected override PayloadData HandleLocalStatusReport()
    {
        _logger.LogTrace("Door {Id} ({Label}): CP sent osdp_LSTAT", _doorId, _label);
        return new LocalStatus(tamper: false, powerFailure: false);
    }

    protected override PayloadData HandleReaderStatusReport()
    {
        _logger.LogTrace("Door {Id} ({Label}): CP sent osdp_RSTAT", _doorId, _label);
        return new ReaderStatus([ReaderTamperStatus.Normal]);
    }

    protected override PayloadData HandleInputStatusReport()
    {
        _logger.LogTrace("Door {Id} ({Label}): CP sent osdp_ISTAT", _doorId, _label);
        return new InputStatus([
            _getDoorOpen()  ? InputStatusValue.Active : InputStatusValue.Inactive,  // DPS
            _getRexActive() ? InputStatusValue.Active : InputStatusValue.Inactive,  // REX
        ]);
    }

    protected override PayloadData HandleCommunicationSet(CommunicationConfiguration config)
    {
        _logger.LogDebug(
            "Door {Id} ({Label}): CP sent osdp_COMSET (address={Addr}, baud={Baud}) — ignoring",
            _doorId, _label, config.Address, config.BaudRate);
        // Reply with our *actual* current settings, not the requested ones.
        // Must return the ReplyData type (pdCOMMSET reply), not the CommandData type.
        return new OSDP.Net.Model.ReplyData.CommunicationConfiguration(_osdpAddress, _baudRate);
    }


    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    // The simulator UI can render a single color + optional blink; it cannot display
    // alternating two-color patterns. Treat blink as a visible pulse only when the
    // panel cycles from a lit color to black.
    private static bool ShouldVisiblyBlink(LedColor onColor, LedColor offColor, byte offTime) =>
        offTime > 0 && onColor != LedColor.Black && offColor == LedColor.Black;

    private static OsdpLedColor MapLedColor(LedColor c) => c switch
    {
        LedColor.Red     => OsdpLedColor.Red,
        LedColor.Green   => OsdpLedColor.Green,
        LedColor.Amber   => OsdpLedColor.Amber,
        LedColor.Blue    => OsdpLedColor.Blue,
        LedColor.Magenta => OsdpLedColor.Magenta,
        LedColor.Cyan    => OsdpLedColor.Cyan,
        LedColor.White   => OsdpLedColor.White,
        _                => OsdpLedColor.Off,   // Black / None
    };
}

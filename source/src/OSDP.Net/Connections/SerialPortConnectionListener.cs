using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OSDP.Net.Connections;

/// <summary>
/// Implements a serial port connection listener for OSDP Peripheral Devices.
/// </summary>
/// <remarks>
/// Unlike TCP listeners that wait for incoming connections, serial communication doesn't have a 
/// connection establishment phase. This listener immediately opens the serial port and creates 
/// an IOsdpConnection for OSDP communication. When the connection is closed (e.g., due to errors 
/// or device disconnection), it automatically reopens the port to maintain availability. This behavior 
/// is essential for serial-based OSDP devices that need to remain accessible to ACUs over RS-485 
/// or similar serial interfaces.
/// </remarks>
public class SerialPortConnectionListener : OsdpConnectionListener
{
    private readonly string _portName;
    private CancellationTokenSource _cancellationTokenSource;
    private Task _listenTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="SerialPortConnectionListener"/> class.
    /// </summary>
    /// <param name="portName">The name of the serial port (e.g., "COM1", "/dev/ttyS0").</param>
    /// <param name="baudRate">The baud rate for serial communication.</param>
    /// <param name="loggerFactory">Optional logger factory for diagnostic logging.</param>
    public SerialPortConnectionListener(
        string portName, int baudRate, ILoggerFactory loggerFactory = null) : base(baudRate, loggerFactory)
    {
        _portName = portName;
    }

    /// <summary>
    /// How long to wait for the remaining bytes of an in-progress frame once its length is known
    /// (forwarded to the connection's <see cref="OsdpConnection.ReplyTimeout"/>). Governs how long
    /// Bus.WaitForMessageLength/WaitForRestOfMessage will wait for inter-byte gaps within a single
    /// frame before throwing a TimeoutException. Defaults to 200ms, same as OsdpConnection.
    /// </summary>
    public TimeSpan ReplyTimeout { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <inheritdoc/>
    public override Task Start(Func<IOsdpConnection, Task> newConnectionHandler)
    {
        if (IsRunning) return Task.CompletedTask;

        IsRunning = true;
        _cancellationTokenSource = new CancellationTokenSource();

        Logger?.LogInformation("Starting serial port listener on {Port} @ {BaudRate} baud", _portName, BaudRate);

        var token = _cancellationTokenSource.Token;
        _listenTask = Task.Run(() => ListenLoop(newConnectionHandler, token), token);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task Stop()
    {
        IsRunning = false;
        _cancellationTokenSource?.Cancel();

        if (_listenTask != null)
        {
            try
            {
                await _listenTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                /* normal on shutdown */
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Error stopping serial listener task on {Port}", _portName);
            }
        }

        await base.Stop().ConfigureAwait(false);

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }

    /// <summary>
    /// Opens the serial port and creates a connection, automatically reopening if the connection closes.
    /// </summary>
    /// <param name="newConnectionHandler">The handler to process the new connection.</param>
    /// <param name="cancellationToken">Cancellation token to signal listener shutdown.</param>
    private async Task ListenLoop(Func<IOsdpConnection, Task> newConnectionHandler, CancellationToken cancellationToken)
    {
        while (IsRunning && !cancellationToken.IsCancellationRequested)
        {
            if (!SerialPortUtils.PortExists(_portName))
            {
                Logger?.LogWarning(
                    "Serial port {Port} not found on the system; retrying in 5s",
                    _portName);

                try
                {
                    await Task.Delay(5000, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                continue;
            }

            SerialPortOsdpConnection connection = null;
            try
            {
                connection = new SerialPortOsdpConnection(_portName, BaudRate)
                {
                    ReplyTimeout = ReplyTimeout,
                };
                await connection.Open().ConfigureAwait(false);

                Logger?.LogDebug("Serial port {Port} opened successfully", _portName);

                var activeConn = connection;
                var handlerTask = Task.Run(async () =>
                {
                    try
                    {
                        await newConnectionHandler(activeConn).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        /* normal on shutdown */
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex, "Error in serial connection handler on {Port}", _portName);
                    }
                    finally
                    {
                        try
                        {
                            await activeConn.Close().ConfigureAwait(false);
                        }
                        catch
                        {
                            /* best-effort */
                        }
                    }
                }, cancellationToken);

                RegisterConnection(connection, handlerTask);

                await handlerTask.ConfigureAwait(false);

                if (IsRunning && !cancellationToken.IsCancellationRequested)
                {
                    Logger?.LogDebug("Serial connection closed, reopening port {Port}", _portName);
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                if (connection != null)
                {
                    try { await connection.Close().ConfigureAwait(false); } catch (Exception closeEx) { Logger?.LogDebug(closeEx, "Error closing connection on cancel"); }
                }
                break;
            }
            catch (Exception ex)
            {
                if (connection != null)
                {
                    try { await connection.Close().ConfigureAwait(false); } catch (Exception closeEx) { Logger?.LogDebug(closeEx, "Error closing connection on error"); }
                }

                Logger?.LogWarning(
                    "Failed to open serial port {Port} ({ExType}: {Msg}); retrying in 5s",
                    _portName, ex.GetType().Name, ex.Message);

                if (IsRunning && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(5000, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }
    }
}
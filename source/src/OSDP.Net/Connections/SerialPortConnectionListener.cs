using System;
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
    public override async Task Start(Func<IOsdpConnection, Task> newConnectionHandler)
    {
        IsRunning = true;

        Logger?.LogInformation("Starting serial port listener on {Port} @ {BaudRate} baud", _portName, BaudRate);

        await OpenSerialPort(newConnectionHandler);
    }

    /// <summary>
    /// Opens the serial port and creates a connection, automatically reopening if the connection closes.
    /// </summary>
    /// <param name="newConnectionHandler">The handler to process the new connection.</param>
    private async Task OpenSerialPort(Func<IOsdpConnection, Task> newConnectionHandler)
    {
        try
        {
            var connection = new SerialPortOsdpConnection(_portName, BaudRate)
            {
                ReplyTimeout = ReplyTimeout,
            };
            await connection.Open();
            
            Logger?.LogDebug("Serial port {Port} opened successfully", _portName);

            var task = Task.Run(async () =>
            {
                try
                {
                    await newConnectionHandler(connection);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Error in serial connection handler");
                }
                finally
                {
                    // If still running, reopen the serial port after a brief delay
                    if (IsRunning)
                    {
                        Logger?.LogDebug("Serial connection closed, reopening port {Port}", _portName);
                        await Task.Delay(1000); // Brief delay before reopening
                        await OpenSerialPort(newConnectionHandler);
                    }
                }
            });
            
            RegisterConnection(connection, task);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to open serial port {Port}", _portName);
            
            // Retry after delay if still running
            if (IsRunning)
            {
                await Task.Delay(5000); // Longer delay on error
                await OpenSerialPort(newConnectionHandler);
            }
        }
    }
}
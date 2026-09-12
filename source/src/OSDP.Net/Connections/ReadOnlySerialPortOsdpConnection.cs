using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace OSDP.Net.Connections
{
    /// <summary>
    /// Read-only serial port connection for passive monitoring of OSDP traffic.
    /// This connection only supports reading data and will throw NotSupportedException for write operations.
    /// </summary>
    public class ReadOnlySerialPortOsdpConnection : OsdpConnection
    {
        private readonly string _portName;
        private SerialPort _serialPort;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReadOnlySerialPortOsdpConnection"/> class.
        /// </summary>
        /// <param name="portName">Name of the serial port (e.g., "COM3").</param>
        /// <param name="baudRate">The baud rate for the serial connection.</param>
        /// <exception cref="ArgumentNullException">Thrown when portName is null.</exception>
        public ReadOnlySerialPortOsdpConnection(string portName, int baudRate) : base(baudRate)
        {
            _portName = portName ?? throw new ArgumentNullException(nameof(portName));
        }

        /// <inheritdoc />
        public override Task Open()
        {
            if (_serialPort == null)
            {
                _serialPort = new SerialPort(_portName, BaudRate)
                {
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    ReadTimeout = 1000,
                    Handshake = Handshake.None
                };

                try
                {
                    _serialPort.Open();
                    IsOpen = true;
                }
                catch
                {
                    try
                    {
                        _serialPort.Dispose();
                    }
                    catch
                    {
                        /* best-effort */
                    }
                    _serialPort = null;
                    IsOpen = false;
                    throw;
                }
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override Task Close()
        {
            if (_serialPort != null)
            {
                try
                {
                    if (_serialPort.IsOpen)
                        _serialPort.Close();
                }
                catch
                {
                    /* best-effort */
                }

                try
                {
                    _serialPort.Dispose();
                }
                catch
                {
                    /* best-effort */
                }

                _serialPort = null;
            }

            IsOpen = false;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        /// <exception cref="NotSupportedException">
        /// This connection is read-only and does not support write operations.
        /// </exception>
        public override Task WriteAsync(byte[] buffer)
        {
            throw new NotSupportedException(
                "This is a read-only connection for passive monitoring. Write operations are not supported.");
        }

        /// <inheritdoc />
        public override async Task<int> ReadAsync(byte[] buffer, CancellationToken token)
        {
            var serialPort = _serialPort;
            if (serialPort == null || !serialPort.IsOpen)
            {
                throw new InvalidOperationException("Connection is not open.");
            }

            try
            {
                return await serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new IOException($"Error reading from serial port '{_portName}': {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{_portName} (Read-Only)";
        }
    }
}

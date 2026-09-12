using System;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;

namespace OSDP.Net.Connections;

/// <summary>
/// Utility methods for serial port discovery and validation across platforms.
/// </summary>
public static class SerialPortUtils
{
    /// <summary>
    /// Checks whether the specified serial port name or path currently exists on the operating system.
    /// Prevents throwing unhandled/first-chance <see cref="FileNotFoundException"/> from <see cref="SerialPort.Open"/>.
    /// </summary>
    /// <param name="portName">Port name (e.g., "COM1", "/dev/ttyUSB0", "/dev/ttyRS485_1_1").</param>
    /// <returns>True if the port exists on the system; otherwise false.</returns>
    public static bool PortExists(string portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
            return false;

        var normalized = portName.Trim();

        // 1. Check direct file/device existence (primary on Linux, e.g. /dev/tty*)
        try
        {
            if (File.Exists(normalized))
                return true;
        }
        catch
        {
            /* best-effort */
        }

        // 2. Check system serial port names via SerialPort.GetPortNames() (primary on Windows, e.g. COM1..COM256)
        try
        {
            var ports = SerialPort.GetPortNames();
            if (ports.Any(p => string.Equals(p.Trim(), normalized, StringComparison.OrdinalIgnoreCase)))
                return true;

            // Handle \\.\COMx syntax if passed
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var cleanName = normalized.StartsWith(@"\\.\", StringComparison.OrdinalIgnoreCase)
                    ? normalized[4..]
                    : normalized;

                if (ports.Any(p => string.Equals(p.Trim(), cleanName, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
        }
        catch
        {
            /* best-effort */
        }

        return false;
    }
}

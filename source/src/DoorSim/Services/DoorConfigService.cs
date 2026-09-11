namespace DoorSim.Services;

/// <summary>
/// CRUD service for <see cref="DoorConfigEntity" />.
/// Validates GPIO pin and Modbus channel uniqueness, and OSDP baud rate values.
/// </summary>
public class DoorConfigService(DoorSimDbContext db)
{
    public async Task<DoorConfiguration[]> GetAllAsync() =>
        await db.Doors
            .OrderBy(p => p.Label)
            .Select(p => p.ToDto())
            .ToArrayAsync()
            .ConfigureAwait(false);

    public async Task<DoorConfiguration?> GetAsync(int id)
    {
        var entity = await db.Doors.FindAsync(id).ConfigureAwait(false);
        return entity?.ToDto();
    }

    public async Task<string[]> GetAvailableSerialPortsAsync(int? excludeDoorId = null)
    {
        var usedPorts = (await db.Doors
                .Where(p => excludeDoorId == null || p.Id != excludeDoorId)
                .Where(p => p.Protocol == nameof(ProtocolType.Osdp))
                .Select(p => p.OsdpSerialPort)
                .ToListAsync()
                .ConfigureAwait(false))
            .Where(port => !string.IsNullOrWhiteSpace(port))
            .Select(port => port!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return
        [
            .. DiscoverSerialPorts()
                .Where(port => !usedPorts.Contains(port)),
        ];
    }

    public async Task<(DoorConfiguration? Result, string? Error)> CreateAsync(DoorConfiguration dto)
    {
        dto = SanitizeStrings(dto);

        var error = await ValidateAsync(dto, null);
        if (error is not null)
            return (null, error);

        var entity = DoorConfigEntity.FromDto(dto with { Id = 0 });
        db.Doors.Add(entity);
        await db.SaveChangesAsync().ConfigureAwait(false);
        return (entity.ToDto(), null);
    }

    public async Task<(DoorConfiguration? Result, string? Error)> UpdateAsync(int id, DoorConfiguration dto)
    {
        dto = SanitizeStrings(dto);

        var entity = await db.Doors.FindAsync(id).ConfigureAwait(false);
        if (entity is null)
            return (null, null);

        var error = await ValidateAsync(dto, id).ConfigureAwait(false);
        if (error is not null)
            return (null, error);

        entity.Label = dto.Label;
        entity.Protocol = dto.Protocol.ToString();
        entity.D0Pin = dto.D0Pin;
        entity.D1Pin = dto.D1Pin;
        entity.OsdpAddress = dto.OsdpAddress;
        entity.OsdpSerialPort = dto.OsdpSerialPort;
        entity.OsdpBaudRate = dto.OsdpBaudRate;
        entity.DpsPin = dto.DpsPin;
        entity.RexPin = dto.RexPin;
        entity.ModbusSerialPort = dto.ModbusSerialPort;
        entity.ModbusUnitId = dto.ModbusUnitId;
        entity.DpsModbusChannel = dto.DpsModbusChannel;
        entity.RexModbusChannel = dto.RexModbusChannel;
        entity.ModbusTcpHost = dto.ModbusTcpHost;
        entity.ModbusTcpPort = dto.ModbusTcpPort;
        entity.DpsNormallyOpen = dto.DpsNormallyOpen;
        entity.RexNormallyOpen = dto.RexNormallyOpen;

        await db.SaveChangesAsync().ConfigureAwait(false);
        return (entity.ToDto(), null);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.Doors.FindAsync(id).ConfigureAwait(false);
        if (entity is null)
            return false;

        db.Doors.Remove(entity);
        await db.SaveChangesAsync().ConfigureAwait(false);
        return true;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Strips control characters (including CR/LF) from the free-text fields that get
    /// interpolated into structured log templates downstream, so a malicious value can't
    /// forge fake log entries (CWE-117 / CodeQL cs/log-forging).
    /// </summary>
    private static DoorConfiguration SanitizeStrings(DoorConfiguration dto) => dto with
    {
        Label = Sanitize(dto.Label)!,
        OsdpSerialPort = Sanitize(dto.OsdpSerialPort),
        ModbusSerialPort = Sanitize(dto.ModbusSerialPort),
        ModbusTcpHost = Sanitize(dto.ModbusTcpHost),
    };

    private static string? Sanitize(string? value) =>
        value?.Replace("\r", "").Replace("\n", "");

    private async Task<string?> ValidateAsync(DoorConfiguration dto, int? excludeId)
    {
        if (dto is { Protocol: ProtocolType.Osdp, OsdpBaudRate: not null } &&
            !OsdpBaudRates.IsSupported(dto.OsdpBaudRate.Value))
        {
            return $"OSDP baud rate {dto.OsdpBaudRate} is not supported. " +
                   $"Valid rates: {string.Join(", ", OsdpBaudRates.Supported)}.";
        }

        var others = await db.Doors
            .Where(p => excludeId == null || p.Id != excludeId)
            .ToListAsync()
            .ConfigureAwait(false);

        var usedPins = others
            .SelectMany(p => new[] { p.D0Pin, p.D1Pin, p.DpsPin, p.RexPin })
            .OfType<int>()
            .ToHashSet();

        var proposed = new[] { dto.D0Pin, dto.D1Pin, dto.DpsPin, dto.RexPin }
            .OfType<int>();

        foreach (var pin in proposed)
        {
            if (usedPins.Contains(pin))
                return $"GPIO pin {pin} is already assigned to another door.";
        }

        // Validate Modbus RTU channel uniqueness per (serialPort, unitId) board
        if (dto.ModbusTcpHost is null && dto.ModbusSerialPort is not null && dto.ModbusUnitId.HasValue)
        {
            var boardPeers = others
                .Where(p =>
                    p.ModbusTcpHost == null &&
                    p.ModbusSerialPort == dto.ModbusSerialPort &&
                    p.ModbusUnitId == dto.ModbusUnitId)
                .ToList();

            var usedChannels = boardPeers
                .SelectMany(p => new[] { p.DpsModbusChannel, p.RexModbusChannel })
                .OfType<int>()
                .ToHashSet();

            foreach (var ch in new[] { dto.DpsModbusChannel, dto.RexModbusChannel }.OfType<int>())
            {
                if (usedChannels.Contains(ch))
                    return $"Modbus channel {ch} on {dto.ModbusSerialPort} unit {dto.ModbusUnitId} is already assigned to another door.";
            }
        }

        // Validate Modbus TCP channel uniqueness per (host, port, unitId) device
        if (dto.ModbusTcpHost is not null && dto.ModbusUnitId.HasValue)
        {
            int tcpPort = dto.ModbusTcpPort ?? 502;
            var boardPeers = others
                .Where(p =>
                    string.Equals(p.ModbusTcpHost, dto.ModbusTcpHost, StringComparison.OrdinalIgnoreCase) &&
                    (p.ModbusTcpPort ?? 502) == tcpPort &&
                    p.ModbusUnitId == dto.ModbusUnitId)
                .ToList();

            var usedChannels = boardPeers
                .SelectMany(p => new[] { p.DpsModbusChannel, p.RexModbusChannel })
                .OfType<int>()
                .ToHashSet();

            foreach (var ch in new[] { dto.DpsModbusChannel, dto.RexModbusChannel }.OfType<int>())
            {
                if (usedChannels.Contains(ch))
                    return $"Modbus channel {ch} on {dto.ModbusTcpHost}:{tcpPort} unit {dto.ModbusUnitId} is already assigned to another door.";
            }
        }

        return null;
    }

    private static string[] DiscoverSerialPorts()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || !Directory.Exists("/dev"))
            return [];

        try
        {
            return
            [
                .. Directory.EnumerateFiles("/dev", "tty*")
                    .Where(path =>
                    {
                        var fileName = Path.GetFileName(path);
                        return fileName.StartsWith("ttyRS485", StringComparison.OrdinalIgnoreCase)
                               || fileName.StartsWith("ttyACM", StringComparison.OrdinalIgnoreCase)
                               || fileName.StartsWith("ttyAMA", StringComparison.OrdinalIgnoreCase)
                               || fileName.StartsWith("ttyUSB", StringComparison.OrdinalIgnoreCase)
                               || fileName.StartsWith("ttyS", StringComparison.OrdinalIgnoreCase);
                    })
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase),
            ];
        }
        catch
        {
            return [];
        }
    }
}

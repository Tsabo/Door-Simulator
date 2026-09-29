namespace DoorSim.Hosting;

/// <summary>
/// Resolves the directory that holds DoorSim's SQLite database and rolling log files, so a
/// downloaded build runs on any OS without hand-edited paths.
/// </summary>
/// <remarks>
/// Resolution order:
/// <list type="number">
/// <item><c>DataDirectory</c> configuration value (appsettings / command line).</item>
/// <item><c>DOORSIM_DATA_DIR</c> environment variable (set by the Docker image and the .deb unit).</item>
/// <item>
/// Linux: <c>/var/lib/doorsim</c> — the Pi install location — when it can be created and written;
/// otherwise the per-user data folder (e.g. a desktop Linux user without root).
/// </item>
/// <item>
/// Windows / macOS: the per-user data folder
/// (<c>%LOCALAPPDATA%\DoorSim</c>, <c>~/Library/Application Support/DoorSim</c>).
/// </item>
/// </list>
/// Explicit <c>ConnectionStrings:DoorSim</c> and <c>Logs:Directory</c> settings still take precedence
/// over paths derived from this root.
/// </remarks>
public static class DataPaths
{
    public const string ConfigKey = "DataDirectory";
    public const string EnvironmentVariable = "DOORSIM_DATA_DIR";
    public const string LinuxSystemDirectory = "/var/lib/doorsim";

    /// <summary>Resolve the data root and make sure it exists.</summary>
    public static string EnsureRoot(IConfiguration configuration) =>
        EnsureRoot(
            configuration[ConfigKey],
            Environment.GetEnvironmentVariable(EnvironmentVariable),
            OperatingSystem.IsLinux(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.Create), "DoorSim"),
            TryPrepare);

    internal static string EnsureRoot(string? configured,
        string? environment,
        bool isLinux,
        string userDirectory,
        Func<string, bool> tryPrepare)
    {
        var explicitRoot = !string.IsNullOrWhiteSpace(configured) ? configured
            : !string.IsNullOrWhiteSpace(environment) ? environment
            : null;

        if (explicitRoot is not null)
        {
            // An explicit location is never silently swapped for another one — fail loudly instead.
            Directory.CreateDirectory(explicitRoot);

            return explicitRoot;
        }

        if (isLinux && tryPrepare(LinuxSystemDirectory))
            return LinuxSystemDirectory;

        Directory.CreateDirectory(userDirectory);

        return userDirectory;
    }

    /// <summary>Create <paramref name="directory" /> if needed and confirm the process can write to it.</summary>
    private static bool TryPrepare(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);

            var probe = Path.Combine(directory, $".write-test-{Environment.ProcessId}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);

            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}

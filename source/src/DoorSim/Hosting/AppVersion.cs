using System.Reflection;

namespace DoorSim.Hosting;

/// <summary>The running build's version, as stamped by Directory.Build.props / the release workflow.</summary>
public static class AppVersion
{
    /// <summary>Full informational version, e.g. <c>1.2.3+7881449c0ffee...</c>.</summary>
    public static string Informational { get; } =
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0-dev";

    /// <summary>Human-friendly version with the commit SHA shortened, e.g. <c>1.2.3+7881449</c>.</summary>
    public static string Display { get; } = Shorten(Informational);

    internal static string Shorten(string informational)
    {
        var plus = informational.IndexOf('+');

        if (plus < 0)
            return informational;

        var sha = informational[(plus + 1)..];

        return sha.Length > 7
            ? $"{informational[..plus]}+{sha[..7]}"
            : informational;
    }
}

using System.Reflection;

namespace RigCheck;

/// <summary>
/// Channel, build date, and expiry for the running binary.
///
/// Both values are stamped into the assembly at compile time from MSBuild
/// properties (see RigCheck.csproj) because deterministic builds strip
/// file timestamps, so nothing else about the binary says when it was built.
/// CI passes Channel and BuildDateUtc explicitly; a local build defaults
/// to channel "dev" with today's date and never expires.
/// </summary>
public static class BuildInfo
{
    public static string    Channel    { get; }
    public static DateOnly? BuildDate  { get; }
    /// <summary>Tag suffix of a tagged pre-release, e.g. "beta.1"; empty for alpha, stable, and local builds.</summary>
    public static string    PreRelease { get; }

    static BuildInfo()
    {
        var meta = typeof(BuildInfo).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(a => a.Key, a => a.Value ?? string.Empty);

        Channel = meta.GetValueOrDefault("Channel", "dev").ToLowerInvariant();
        PreRelease = meta.GetValueOrDefault("PreRelease", string.Empty).Trim();

        BuildDate = DateOnly.TryParseExact(
            meta.GetValueOrDefault("BuildDateUtc", string.Empty),
            "yyyy-MM-dd", out var date) ? date : null;
    }

    public static bool IsAlpha  => Channel == BrandingInfo.ChannelAlpha;
    public static bool IsBeta   => Channel == BrandingInfo.ChannelBeta;
    public static bool IsStable => Channel == BrandingInfo.ChannelStable;

    /// <summary>
    /// Version for display: "0.7.0" on stable, "0.7.0-beta.1" on a tagged
    /// beta, "0.7.0-alpha" on a develop build (the date and hash are in
    /// the filename and About, not here).
    /// </summary>
    public static string VersionLabel =>
        IsStable                              ? BrandingInfo.Version
        : PreRelease.Length > 0               ? $"{BrandingInfo.Version}-{PreRelease}"
        :                                       $"{BrandingInfo.Version}-{Channel}";

    /// <summary>Last day this build runs, or null for channels that never expire.</summary>
    public static DateOnly? ExpiryDate => BuildDate is not { } built ? null : Channel switch
    {
        BrandingInfo.ChannelAlpha => built.AddDays(BrandingInfo.AlphaExpiryDays),
        BrandingInfo.ChannelBeta  => built.AddDays(BrandingInfo.BetaExpiryDays),
        _                         => null,
    };

    public static int? DaysUntilExpiry =>
        ExpiryDate is { } exp ? exp.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber : null;

    public static bool IsExpired => DaysUntilExpiry is < 0;

    /// <summary>True in the final days before expiry, when the UI should start warning.</summary>
    public static bool IsExpiringSoon => DaysUntilExpiry is >= 0 and <= BrandingInfo.ExpiryWarnDays;

    /// <summary>Alpha builds refuse to run once expired; beta builds only warn.</summary>
    public static bool BlocksWhenExpired => IsAlpha;
}

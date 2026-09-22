using Newtonsoft.Json.Linq;
using Serilog;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace RigCheck.Services;

/// <summary>A release on GitHub that is newer than the running build.</summary>
public record AvailableUpdate(string Tag, string Label, string Channel, Version Version, string PageUrl);

/// <summary>
/// Asks GitHub Releases whether something newer exists for this build's
/// channel, and nothing more: no download, no install, no prompt loop.
///
/// Channel rule — what a user is told about:
///   stable → stable releases only
///   beta   → betas and stables
///   alpha  → everything
/// so nobody on a beta is nagged about a nightly, and an alpha user learns
/// when the beta they should move to exists.
///
/// Tags: alpha/{yyyyMMdd}-{HHmm}-{sha7} carry no version, so an alpha's
/// version comes from the release name ("RigCheck 0.7.2-alpha (…)"); beta
/// and stable tags are v{version}[-beta].
/// </summary>
public class UpdateCheckService
{
    private static readonly TimeSpan MinInterval = TimeSpan.FromHours(20);   // "once a day" with slack for launch time

    private readonly HttpClient      _http;
    private readonly SettingsService _settings;

    public UpdateCheckService(HttpClient http, SettingsService settings)
    {
        _http     = http;
        _settings = settings;
    }

    /// <summary>Effective setting: the saved choice, else on for alpha/beta and off for stable.</summary>
    public bool IsEnabled =>
        _settings.Current.UpdateCheckEnabled ?? !BuildInfo.IsStable;

    /// <summary>
    /// Check if enabled and due. Returns the newest release the channel rule
    /// allows that is newer than the running build, or null. Never throws;
    /// a PC with no internet gets null and a debug log line.
    /// </summary>
    public async Task<AvailableUpdate?> CheckAsync(bool force = false)
    {
        if (!force && !IsEnabled) return null;
        if (!force && DateTime.UtcNow - _settings.Current.UpdateLastChecked < MinInterval) return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BrandingInfo.ReleasesApiUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("RigCheck", BrandingInfo.Version));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Log.Debug("Update check: GitHub answered {Status}", response.StatusCode);
                return null;
            }

            var releases = JArray.Parse(await response.Content.ReadAsStringAsync());
            var newest   = releases
                .Select(Parse)
                .Where(r => r is not null && AllowedForThisChannel(r.Channel))
                .Cast<AvailableUpdate>()
                .OrderByDescending(r => r.Version)
                .ThenByDescending(r => ChannelRank(r.Channel))
                .FirstOrDefault();

            _settings.Update(s => s.UpdateLastChecked = DateTime.UtcNow);

            if (newest is null || !IsNewerThanRunning(newest)) return null;

            _settings.Update(s => s.UpdateLastSeenTag = newest.Tag);
            Log.Information("Update available: {Label} ({Tag})", newest.Label, newest.Tag);
            return newest;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Update check failed (offline is normal)");
            return null;
        }
    }

    // ── Parsing ───────────────────────────────────────────────────────────

    private static readonly Regex StableOrBetaTag = new(@"^v(\d+\.\d+\.\d+)(-beta)?$", RegexOptions.Compiled);
    private static readonly Regex AlphaName       = new(@"RigCheck\s+(\d+\.\d+\.\d+)-alpha", RegexOptions.Compiled);

    private static AvailableUpdate? Parse(JToken release)
    {
        if (release["draft"]?.Value<bool>() == true) return null;

        var tag  = release["tag_name"]?.Value<string>() ?? string.Empty;
        var name = release["name"]?.Value<string>()     ?? string.Empty;
        var url  = release["html_url"]?.Value<string>() ?? BrandingInfo.ReleasesUrl;

        var m = StableOrBetaTag.Match(tag);
        if (m.Success && Version.TryParse(m.Groups[1].Value, out var v))
        {
            var channel = m.Groups[2].Success ? BrandingInfo.ChannelBeta : BrandingInfo.ChannelStable;
            var label   = channel == BrandingInfo.ChannelBeta ? $"{v}-beta" : v.ToString();
            return new(tag, label, channel, v, url);
        }

        if (tag.StartsWith("alpha/", StringComparison.Ordinal))
        {
            var a = AlphaName.Match(name);
            if (a.Success && Version.TryParse(a.Groups[1].Value, out var av))
                return new(tag, $"{av}-alpha", BrandingInfo.ChannelAlpha, av, url);
        }

        return null;
    }

    // ── Channel rule ──────────────────────────────────────────────────────

    private static bool AllowedForThisChannel(string releaseChannel) => BuildInfo.Channel switch
    {
        BrandingInfo.ChannelStable => releaseChannel == BrandingInfo.ChannelStable,
        BrandingInfo.ChannelBeta   => releaseChannel is BrandingInfo.ChannelStable or BrandingInfo.ChannelBeta,
        _                          => true,   // alpha and local dev builds hear about everything
    };

    private static int ChannelRank(string channel) => channel switch
    {
        BrandingInfo.ChannelStable => 3,
        BrandingInfo.ChannelBeta   => 2,
        _                          => 1,
    };

    // Same version number, more trusted channel counts as newer (0.7.2 beta
    // → 0.7.2 stable is an update); same number and channel is not.
    private static bool IsNewerThanRunning(AvailableUpdate candidate)
    {
        if (!Version.TryParse(BrandingInfo.Version, out var running)) return false;
        var cmp = candidate.Version.CompareTo(running);
        if (cmp != 0) return cmp > 0;
        return ChannelRank(candidate.Channel) > ChannelRank(BuildInfo.Channel);
    }
}

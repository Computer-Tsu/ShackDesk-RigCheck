using System.Globalization;
using System.Resources;

namespace RigCheck.Localization;

/// <summary>
/// Access to user-visible text in Resources/Strings.resx and its per-language
/// variants (Strings.de.resx, Strings.ja.resx, …).
///
/// A missing key returns "!Key!" rather than throwing, so a typo is visible
/// in the UI instead of crashing the app. Brand names are never stored here —
/// they are injected through format placeholders from BrandingInfo so
/// translators cannot alter them.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Manager =
        new("RigCheck.Resources.Strings", typeof(Strings).Assembly);

    /// <summary>Text for <paramref name="key"/> in the current UI language.</summary>
    public static string Get(string key) =>
        Manager.GetString(key, CultureInfo.CurrentUICulture) ?? $"!{key}!";

    /// <summary>Text for <paramref name="key"/> with {0}, {1}… placeholders filled.</summary>
    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    /// <summary>
    /// Override the UI language for this session. Called at startup from the
    /// saved setting; null or empty restores the Windows UI language.
    /// </summary>
    public static void SetUiCulture(string? cultureName)
    {
        var culture = string.IsNullOrWhiteSpace(cultureName)
            ? CultureInfo.InstalledUICulture
            : CultureInfo.GetCultureInfo(cultureName);

        CultureInfo.CurrentUICulture               = culture;
        CultureInfo.DefaultThreadCurrentUICulture  = culture;
    }
}

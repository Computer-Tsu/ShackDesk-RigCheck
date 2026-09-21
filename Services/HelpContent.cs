using RigCheck.Localization;
using RigCheck.Models;

namespace RigCheck.Services;

/// <summary>
/// Help articles shown alongside specific failure types, aimed at operators
/// who have never configured CAT control before. Text lives in Strings.resx.
/// </summary>
public static class HelpContent
{
    public static HelpTopic[] TopicsForError(RigctlError error) => error switch
    {
        RigctlError.Timeout    => [EnableCat(), BaudRateMatch()],
        RigctlError.NoResponse => [BaudRateMatch(), CivAddress()],
        _                      => [],
    };

    private static HelpTopic EnableCat() => new(
        Strings.Get("Help_EnableCat_Title"),
        Strings.Get("Help_EnableCat_Body"));

    private static HelpTopic BaudRateMatch() => new(
        Strings.Get("Help_Baud_Title"),
        Strings.Format("Help_Baud_Body", BrandingInfo.AppName));

    private static HelpTopic CivAddress() => new(
        Strings.Get("Help_CivAddress_Title"),
        Strings.Get("Help_CivAddress_Body"));
}

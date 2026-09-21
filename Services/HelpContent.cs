using RigCheck.Models;

namespace RigCheck.Services;

/// <summary>
/// Help articles shown alongside specific failure types, aimed at operators
/// who have never configured CAT control before.
/// </summary>
public static class HelpContent
{
    public static HelpTopic[] TopicsForError(RigctlError error) => error switch
    {
        RigctlError.Timeout    => [EnableCat, BaudRateMatch],
        RigctlError.NoResponse => [BaudRateMatch, CivAddress],
        _                      => [],
    };

    private static readonly HelpTopic EnableCat = new(
        "Enabling CAT control on your radio",
        "Most radios ship with CAT control disabled or set to a default speed. " +
        "Look in the radio's menu for a setting named CAT, CI-V, RS-232, or PC control " +
        "and make sure it is turned on. Icom radios call this CI-V; Yaesu and Kenwood " +
        "call it CAT. The exact menu number is in your radio's manual under \"remote control\".");

    private static readonly HelpTopic BaudRateMatch = new(
        "Matching the baud rate",
        "The baud rate set in RigCheck must match the rate set inside the radio. " +
        "Common defaults: Icom IC-7300 and IC-705 use 19200 or 115200; Yaesu FT-991A and " +
        "FT-DX10 use 38400; Kenwood TS-590SG uses 115200; Elecraft K3 uses 38400. " +
        "If unsure, try 9600 first — nearly every radio supports it.");

    private static readonly HelpTopic CivAddress = new(
        "Icom CI-V address",
        "Icom radios each answer to a CI-V address, shown in the radio's menu as a hex " +
        "number such as 94h. Hamlib uses the default address for the selected model. " +
        "If the address has been changed in the radio, either reset it to the default " +
        "or add the matching address in the advanced serial settings.");
}

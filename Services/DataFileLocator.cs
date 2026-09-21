using Serilog;
using System.IO;

namespace RigCheck.Services;

/// <summary>
/// Finds a data file such as usb_devices.json, external copy first,
/// embedded copy last. The embedded copy keeps the single portable exe
/// self-sufficient; an external copy beside the exe or in the shared
/// ShackDesk folder overrides it, so the data can be updated separately
/// from the program or shared with other ShackDesk apps later.
///
/// Search order:
///   1. next to the exe            — a tester drops a newer file in
///   2. Assets\ next to the exe    — a source-tree layout
///   3. %LOCALAPPDATA%\ShackDesk\Data\ — shared across suite apps
///   4. embedded resource          — always present
/// </summary>
public static class DataFileLocator
{
    private static readonly string SharedDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        BrandingInfo.SuiteName, "Data");

    /// <summary>Open the first copy found. The stream's origin is logged. Returns null only if the embedded copy is missing.</summary>
    public static Stream? Open(string fileName)
    {
        foreach (var path in ExternalCandidates(fileName))
        {
            if (!File.Exists(path)) continue;
            try
            {
                Log.Information("Data file {Name} loaded from {Path}", fileName, path);
                return File.OpenRead(path);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Data file {Path} exists but could not be opened; trying next location", path);
            }
        }

        var resource = $"{BrandingInfo.AppName}.Assets.{fileName}";
        var stream   = typeof(DataFileLocator).Assembly.GetManifestResourceStream(resource);
        if (stream is null)
            Log.Warning("Data file {Name} not found anywhere, including embedded resource {Resource}", fileName, resource);
        else
            Log.Debug("Data file {Name} loaded from embedded resource", fileName);
        return stream;
    }

    /// <summary>Read the first copy found as text, or null.</summary>
    public static string? ReadAllText(string fileName)
    {
        using var stream = Open(fileName);
        if (stream is null) return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static IEnumerable<string> ExternalCandidates(string fileName)
    {
        yield return Path.Combine(AppContext.BaseDirectory, fileName);
        yield return Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
        yield return Path.Combine(SharedDir, fileName);
    }
}

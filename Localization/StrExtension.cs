using System.Windows.Markup;

namespace RigCheck.Localization;

/// <summary>
/// XAML markup extension that resolves a Strings.resx key at load time.
/// Usage: <c>Header="{loc:Str Menu_Help}"</c> with
/// <c>xmlns:loc="clr-namespace:RigCheck.Localization"</c>.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class StrExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public StrExtension() { }
    public StrExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        Strings.Get(Key);
}

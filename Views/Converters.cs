using RigCheck.ViewModels;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace RigCheck.Views;

// ── Value converters for XAML bindings ────────────────────────────────────
// Instantiated once in Resources/Styles.xaml and referenced by key from
// MainWindow.xaml. Color converters look brushes up from the application
// resources at runtime so the theme can be swapped without touching code.

/// <summary>Negates a boolean. Used for "enabled when not running" bindings.</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => value is false;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => value is false;
}

/// <summary>Collapses an element when the bound boolean is true.</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
        value is Visibility.Collapsed;
}

/// <summary>Collapses an element when the bound string is null or empty.</summary>
public sealed class NullOrEmptyToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
        throw new NotSupportedException();
}

/// <summary>Pass/fail brush for the Hamlib status indicator dot.</summary>
public sealed class BoolToStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        Application.Current.Resources[value is true ? "BrushPass" : "BrushFail"];
    public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
        throw new NotSupportedException();
}

/// <summary>Colors raw console lines: commands in accent, errors in red, output in normal text.</summary>
public sealed class ConsoleKindToColorConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var key = value switch
        {
            ConsoleEntryKind.Command => "BrushAccent",
            ConsoleEntryKind.Error   => "BrushFail",
            _                        => "BrushForeground",
        };
        return Application.Current.Resources[key];
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
        throw new NotSupportedException();
}

using RigCheck.Models;
using RigCheck.ViewModels;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace RigCheck.Views;

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => value is false;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => value is false;
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
        value is Visibility.Collapsed;
}

public sealed class NullOrEmptyToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
        throw new NotSupportedException();
}

public sealed class ExpandIconConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        value is true ? "▾" : "▸";
    public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
        throw new NotSupportedException();
}

public sealed class BoolToStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        Application.Current.Resources[value is true ? "BrushPass" : "BrushFail"];
    public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
        throw new NotSupportedException();
}

public sealed class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var key = value switch
        {
            TestStatus.Pass    => "BrushPass",
            TestStatus.Fail    => "BrushFail",
            TestStatus.Warning => "BrushWarn",
            TestStatus.Running => "BrushAccent",
            _                  => "BrushMuted",
        };
        return Application.Current.Resources[key];
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
        throw new NotSupportedException();
}

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

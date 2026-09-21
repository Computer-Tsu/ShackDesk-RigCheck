using RigCheck.Localization;
using RigCheck.Services;
using RigCheck.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace RigCheck.Views;

/// <summary>
/// Code-behind for the main window. Kept to view-only concerns:
/// window placement persistence and keyboard routing for the raw console.
/// All application logic lives in MainViewModel.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel    _vm;
    private readonly SettingsService  _settings;
    private readonly TelemetryService _telemetry;

    public MainWindow(MainViewModel vm, SettingsService settings, TelemetryService telemetry)
    {
        _vm        = vm;
        _settings  = settings;
        _telemetry = telemetry;
        // Set DataContext before InitializeComponent so bindings resolve on first layout
        DataContext = vm;
        InitializeComponent();

        // Brand name is injected here so it can never end up in a translation file.
        AboutMenuItem.Header = Strings.Format("Menu_HelpAbout", BrandingInfo.AppName);
    }

    // ── Menu ──────────────────────────────────────────────────────────────
    // Dialogs are opened from code-behind because owning and showing a
    // window is a view concern; the ViewModel never references a Window.

    private void Settings_Click(object sender, RoutedEventArgs e) =>
        new SettingsDialog(_settings, _telemetry) { Owner = this }.ShowDialog();

    private void ViewData_Click(object sender, RoutedEventArgs e) =>
        new TelemetryDataViewer(_telemetry) { Owner = this }.ShowDialog();

    private void About_Click(object sender, RoutedEventArgs e) =>
        new AboutDialog(_settings.Current.InstallId) { Owner = this }.ShowDialog();

    // ── Window placement ──────────────────────────────────────────────────
    // Width and height are bound directly to settings in XAML. Left/Top are
    // restored here because a window position is view state, not ViewModel state.

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var s = _settings.Current;
        if (!double.IsNaN(s.WindowLeft) && !double.IsNaN(s.WindowTop))
        {
            Left = s.WindowLeft;
            Top  = s.WindowTop;
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e) =>
        _vm.OnWindowClosing(Left, Top);

    // ── Raw console keyboard handling ─────────────────────────────────────
    // Enter sends the command; Up/Down walk the history like a shell prompt.

    private void ConsoleInput_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                _vm.SendRawCommandCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Up:
                _vm.RawConsole.HistoryUp();
                ConsoleInput.CaretIndex = ConsoleInput.Text.Length;
                e.Handled = true;
                break;
            case Key.Down:
                _vm.RawConsole.HistoryDown();
                ConsoleInput.CaretIndex = ConsoleInput.Text.Length;
                e.Handled = true;
                break;
        }
    }
}

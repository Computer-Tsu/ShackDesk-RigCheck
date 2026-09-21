using RigCheck.Localization;
using RigCheck.Services;
using RigCheck.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace RigCheck.Views;

/// <summary>
/// Code-behind for the main window. Kept to view-only concerns:
/// window placement persistence, rendering results into the document
/// viewer, and keyboard routing for the raw console.
/// All application logic lives in MainViewModel.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel          _vm;
    private readonly SettingsService        _settings;
    private readonly TelemetryService       _telemetry;
    private readonly DiscoveryDataService   _discoveryData;
    private readonly ResultsDocumentBuilder _resultsDoc = new();

    public MainWindow(MainViewModel vm, SettingsService settings, TelemetryService telemetry,
                      DiscoveryDataService discoveryData)
    {
        _vm            = vm;
        _settings      = settings;
        _telemetry     = telemetry;
        _discoveryData = discoveryData;
        // Set DataContext before InitializeComponent so bindings resolve on first layout
        DataContext = vm;
        InitializeComponent();

        // Brand name is injected here so it can never end up in a translation file.
        AboutMenuItem.Header = Strings.Format("Menu_HelpAbout", BrandingInfo.AppName);

        // Results are rendered as a FlowDocument (selectable, copyable text).
        // The ViewModel only knows about result items; the document is a
        // view concern, so it is rebuilt here whenever the items change.
        ResultsViewer.Document = _resultsDoc.Document;
        _vm.Results.Items.CollectionChanged += (_, _) => _resultsDoc.Rebuild(_vm.Results.Items);
        _resultsDoc.Rebuild(_vm.Results.Items);

        _vm.TaskCompleted += success => CompletionNotifier.Notify(this, _settings.Current, success);
    }

    // ── Results copy ──────────────────────────────────────────────────────
    // The viewer's built-in Copy puts RTF and XAML on the clipboard alongside
    // the text, which drags fonts and colors into email clients. Operators
    // paste results into forum posts and emails, so plain text only.

    private void ResultsCopy_CanExecute(object sender, CanExecuteRoutedEventArgs e) =>
        e.CanExecute = ResultsViewer.Selection is { IsEmpty: false };

    private void ResultsCopy_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var text = ResultsViewer.Selection?.Text;
        if (!string.IsNullOrEmpty(text))
        {
            // The inline Copy buttons come through as a space at the end of
            // their line; trim so pasted command lines are exact.
            var lines = text.Split('\n').Select(l => l.TrimEnd(' ', '\r'));
            Clipboard.SetText(string.Join(Environment.NewLine, lines));
        }
        e.Handled = true;
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

    // Find my radio: the operator ticks the ports it may open, then the
    // ViewModel runs the sweep. Refreshing first catches a cable plugged in
    // since the window opened.
    private void FindRadio_Click(object sender, RoutedEventArgs e)
    {
        _vm.Connection.RefreshPorts();
        var dlg = new DiscoveryDialog(_vm.Connection.AvailablePorts.ToList(), _discoveryData) { Owner = this };
        if (dlg.ShowDialog() == true)
            _ = _vm.FindRadioCommand.ExecuteAsync(dlg.SelectedPorts);
    }

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

using RigCheck.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace RigCheck.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var s = ((Services.SettingsService)Application.Current.Resources["Settings"]).Current;
        if (!double.IsNaN(s.WindowLeft) && !double.IsNaN(s.WindowTop))
        {
            Left = s.WindowLeft;
            Top  = s.WindowTop;
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e) =>
        _vm.OnWindowClosing(Left, Top);

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

using CommunityToolkit.Mvvm.ComponentModel;
using RigCheck.Localization;
using RigCheck.Models;
using RigCheck.Services;
using System.Windows;

namespace RigCheck.Views;

/// <summary>
/// Port checklist shown before Find my radio runs. Returns the ticked
/// ports through <see cref="SelectedPorts"/> when the operator clicks Start.
/// </summary>
public partial class DiscoveryDialog : Window
{
    private readonly List<PortChoice> _choices;

    public IReadOnlyList<ComPortInfo> SelectedPorts =>
        _choices.Where(c => c.IsSelected).Select(c => c.Port).ToList();

    public DiscoveryDialog(IReadOnlyList<ComPortInfo> ports, DiscoveryDataService data)
    {
        InitializeComponent();

        _choices = ports.Select(p =>
        {
            var skipClass = data.SkipClass(p.FriendlyName);
            var detail = skipClass is not null
                ? Strings.Format("Discovery_SkipReason", Strings.TryGet($"DeviceClass_{skipClass}", out var cls) ? cls : skipClass)
                : p.CableHint ?? p.DeviceType;
            return new PortChoice(p, skipClass is null, detail);
        }).ToList();

        PortList.ItemsSource = _choices;
        if (_choices.Count == 0)
        {
            NoPortsText.Visibility = Visibility.Visible;
            StartButton.IsEnabled  = false;
        }
    }

    private void Start_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}

/// <summary>One row of the checklist.</summary>
public partial class PortChoice : ObservableObject
{
    public ComPortInfo Port   { get; }
    public string      Title  => Port.DisplayName;
    public string      Detail { get; }

    [ObservableProperty] private bool _isSelected;

    public PortChoice(ComPortInfo port, bool selected, string detail)
    {
        Port       = port;
        Detail     = detail;
        IsSelected = selected;
    }
}

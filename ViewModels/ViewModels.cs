using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RigCheck.Models;
using RigCheck.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace RigCheck.ViewModels;

// ═══════════════════════════════════════════════════════════════════════════
// ConnectionViewModel
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Exposes all connection configuration fields to the UI.
/// Populates COM port list, radio presets, and baud rate options.
/// </summary>
public partial class ConnectionViewModel : ObservableObject
{
    private readonly ComPortService     _ports;
    private readonly RadioPresetsService _presets;

    // ── Dropdowns ─────────────────────────────────────────────────────────

    public ObservableCollection<ComPortInfo> AvailablePorts   { get; } = [];
    public ObservableCollection<RadioPreset> AvailablePresets { get; } = [];

    public IReadOnlyList<int> BaudRates { get; } =
        [1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200];

    public IReadOnlyList<string> DataBitsOptions  { get; } = ["7", "8"];
    public IReadOnlyList<string> ParityOptions    { get; } = ["None", "Even", "Odd", "Mark", "Space"];
    public IReadOnlyList<string> StopBitsOptions  { get; } = ["1", "1.5", "2"];
    public IReadOnlyList<string> FlowCtrlOptions  { get; } = ["None", "Hardware", "Software"];
    public IReadOnlyList<string> PttMethods       { get; } = ["CAT", "RTS", "DTR", "VOX", "None"];

    // ── Connection fields ─────────────────────────────────────────────────

    [ObservableProperty] private ComPortInfo? _selectedPort;
    [ObservableProperty] private int          _baudRate    = 9600;
    [ObservableProperty] private string       _dataBits    = "8";
    [ObservableProperty] private string       _parity      = "None";
    [ObservableProperty] private string       _stopBits    = "1";
    [ObservableProperty] private string       _flowControl = "None";
    [ObservableProperty] private string       _pttMethod   = "CAT";

    // Hamlib model fields
    [ObservableProperty] private int    _modelId   = 1;
    [ObservableProperty] private string _modelName = string.Empty;
    [ObservableProperty] private string _modelSearch = string.Empty;

    // Network / rigctld
    [ObservableProperty] private bool   _useRigctld   = false;
    [ObservableProperty] private string _rigctldHost  = "localhost";
    [ObservableProperty] private int    _rigctldPort  = 4532;

    // Selected preset
    [ObservableProperty] private RadioPreset? _selectedPreset;
    partial void OnSelectedPresetChanged(RadioPreset? value)
    {
        if (value is null) return;
        ApplyPreset(value);
    }

    // Cable hint — shown in UI when a known radio USB cable is detected
    [ObservableProperty] private string _cableHint    = string.Empty;
    [ObservableProperty] private bool   _hasCableHint;

    partial void OnSelectedPortChanged(ComPortInfo? value)
    {
        if (value?.HasRadioHint == true)
        {
            CableHint    = $"Detected: {value.CableHint}";
            HasCableHint = true;
        }
        else
        {
            CableHint    = string.Empty;
            HasCableHint = false;
        }
    }

    public ConnectionViewModel(ComPortService ports, RadioPresetsService presets)
    {
        _ports   = ports;
        _presets = presets;
        RefreshPorts();
        LoadPresets();
    }

    [RelayCommand]
    public void RefreshPorts()
    {
        AvailablePorts.Clear();
        foreach (var p in _ports.GetAvailablePorts())
            AvailablePorts.Add(p);

        if (SelectedPort is null && AvailablePorts.Count > 0)
            SelectedPort = AvailablePorts[0];
    }

    private void LoadPresets()
    {
        AvailablePresets.Clear();
        foreach (var p in _presets.Presets)
            AvailablePresets.Add(p);
    }

    private void ApplyPreset(RadioPreset preset)
    {
        ModelId   = preset.HamlibModelId;
        ModelName = preset.Name;
        BaudRate  = preset.BaudRate;
        DataBits  = preset.DataBits;
        Parity    = preset.Parity;
        StopBits  = preset.StopBits;
        FlowControl = preset.FlowControl;
        PttMethod = preset.PttMethod;
    }

    public ConnectionConfig BuildConfig() => new()
    {
        ModelId        = ModelId,
        RadioModelName = ModelName,
        ComPort        = SelectedPort?.PortName ?? string.Empty,
        BaudRate       = BaudRate,
        DataBits       = int.Parse(DataBits),
        Parity         = Parity,
        StopBits       = StopBits,
        FlowControl    = FlowControl,
        PttMethod      = PttMethod,
        UseRigctld     = UseRigctld,
        RigctldHost    = RigctldHost,
        RigctldPort    = RigctldPort,
    };

    public void LoadFrom(RigCheckSettings s)
    {
        ModelId    = s.RadioModelId;
        ModelName  = s.RadioModelName;
        BaudRate   = s.BaudRate;
        DataBits   = s.DataBits;
        Parity     = s.Parity;
        StopBits   = s.StopBits;
        FlowControl = s.FlowControl;
        PttMethod  = s.PttMethod;
        UseRigctld = s.UseRigctld;
        RigctldHost = s.RigctldHost;
        RigctldPort = s.RigctldPort;

        var saved = AvailablePorts.FirstOrDefault(p => p.PortName == s.ComPort);
        if (saved is not null) SelectedPort = saved;
    }

    public void SaveTo(RigCheckSettings s)
    {
        s.RadioModelId   = ModelId;
        s.RadioModelName = ModelName;
        s.ComPort        = SelectedPort?.PortName ?? string.Empty;
        s.BaudRate       = BaudRate;
        s.DataBits       = DataBits;
        s.Parity         = Parity;
        s.StopBits       = StopBits;
        s.FlowControl    = FlowControl;
        s.PttMethod      = PttMethod;
        s.UseRigctld     = UseRigctld;
        s.RigctldHost    = RigctldHost;
        s.RigctldPort    = RigctldPort;
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// TestResultsViewModel
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Exposes test results to the UI as they stream in during the test run.
/// </summary>
public partial class TestResultsViewModel : ObservableObject
{
    public ObservableCollection<TestResultItemViewModel> Items { get; } = [];

    [ObservableProperty] private string _summaryText = string.Empty;
    [ObservableProperty] private bool   _hasResults;

    public TestSuiteResult? SuiteResult { get; private set; }

    public void Clear()
    {
        Items.Clear();
        SummaryText = string.Empty;
        HasResults  = false;
        SuiteResult = null;

        // Add pending placeholders for all tests
        foreach (TestId id in Enum.GetValues<TestId>())
            Items.Add(new TestResultItemViewModel(TestResult.Pending(id)));
    }

    public void AddResult(TestResult result)
    {
        // Replace the pending placeholder for this test
        var existing = Items.FirstOrDefault(i => i.TestId == result.Id);
        if (existing is not null)
        {
            var idx = Items.IndexOf(existing);
            Items[idx] = new TestResultItemViewModel(result);
        }
        else
        {
            Items.Add(new TestResultItemViewModel(result));
        }

        HasResults = true;
    }

    public void SetSuiteResult(TestSuiteResult suite)
    {
        SuiteResult = suite;
        SummaryText = suite.AllPassed
            ? $"✓ All {suite.PassCount} tests passed"
            : $"{suite.FailCount} failed · {suite.PassCount} passed · {suite.WarningCount} warnings";
    }
}

/// <summary>Single test result row in the results list.</summary>
public partial class TestResultItemViewModel : ObservableObject
{
    private readonly TestResult _result;

    public TestResultItemViewModel(TestResult result)
    {
        _result = result;
    }

    public TestId    TestId      => _result.Id;
    public string    Name        => _result.FriendlyName;
    public string    Message     => _result.Message;
    public TestStatus Status     => _result.Status;
    public string    DisplayCommand => _result.DisplayCommand;
    public bool      HasDiagnosis  => _result.HasDiagnosis;
    public DiagnosticResult? Diagnosis => _result.Diagnosis;

    public string StatusIcon => Status switch
    {
        TestStatus.Pass    => "✓",
        TestStatus.Fail    => "✗",
        TestStatus.Warning => "⚠",
        TestStatus.Skipped => "–",
        TestStatus.Running => "…",
        _                  => "○",
    };

    [ObservableProperty] private bool _isExpanded;

    [RelayCommand]
    private void CopyCommand()
    {
        if (!string.IsNullOrEmpty(DisplayCommand))
            Clipboard.SetText(DisplayCommand);
    }

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;
}

// ═══════════════════════════════════════════════════════════════════════════
// RawConsoleViewModel
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Advanced raw Hamlib command console.
/// Users can type rigctl subcommands and see the output.
/// Command history is supported (up-arrow to recall).
/// </summary>
public partial class RawConsoleViewModel : ObservableObject
{
    private readonly HamlibRunnerService _runner;

    public ObservableCollection<ConsoleEntry> Entries { get; } = [];

    [ObservableProperty] private string _inputText = string.Empty;
    [ObservableProperty] private bool   _isRunning;

    private readonly List<string> _history = [];
    private int _historyIndex = -1;

    public RawConsoleViewModel(HamlibRunnerService runner)
    {
        _runner = runner;
    }

    [RelayCommand]
    private async Task SendCommandAsync(ConnectionConfig cfg)
    {
        var input = InputText.Trim();
        if (string.IsNullOrEmpty(input)) return;

        _history.Insert(0, input);
        _historyIndex = -1;
        InputText = string.Empty;

        var cmd = RigctlCommandBuilder.RawCommand(cfg, input);
        AddEntry(ConsoleEntryKind.Command, cmd.DisplayCommand);

        IsRunning = true;
        try
        {
            var result = await _runner.RunAsync(cmd);
            if (result.IsSuccess)
                AddEntry(ConsoleEntryKind.Output, result.RawOutput);
            else
                AddEntry(ConsoleEntryKind.Error, result.ErrorMessage);
        }
        finally
        {
            IsRunning = false;
        }
    }

    public void HistoryUp()
    {
        if (_history.Count == 0) return;
        _historyIndex = Math.Min(_historyIndex + 1, _history.Count - 1);
        InputText = _history[_historyIndex];
    }

    public void HistoryDown()
    {
        _historyIndex = Math.Max(_historyIndex - 1, -1);
        InputText = _historyIndex >= 0 ? _history[_historyIndex] : string.Empty;
    }

    [RelayCommand]
    private void ClearConsole() => Entries.Clear();

    private void AddEntry(ConsoleEntryKind kind, string text) =>
        Entries.Add(new ConsoleEntry(kind, text, DateTime.Now));

    public List<string> GetHistory() => [.._history];
    public void LoadHistory(List<string> history)
    {
        _history.Clear();
        _history.AddRange(history);
    }
}

public record ConsoleEntry(ConsoleEntryKind Kind, string Text, DateTime Timestamp);

public enum ConsoleEntryKind { Command, Output, Error }

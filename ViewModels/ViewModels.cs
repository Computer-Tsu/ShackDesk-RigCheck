using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RigCheck.Localization;
using RigCheck.Models;
using RigCheck.Services;
using System.Collections.ObjectModel;

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

    // Every optional serial setting offers "Radio default" first: the operator
    // usually doesn't know these, and Hamlib's model database does. Choice
    // values are what gets stored and passed to rigctl; labels are localized.
    public IReadOnlyList<BaudOption> BaudRates { get; } =
    [
        new(0, Strings.Get("Option_RadioDefault")),
        new(1200, "1200"), new(2400, "2400"), new(4800, "4800"), new(9600, "9600"),
        new(19200, "19200"), new(38400, "38400"), new(57600, "57600"), new(115200, "115200"),
    ];

    public IReadOnlyList<Choice> DataBitsOptions { get; } =
        [Choice.RadioDefault, Choice.Literal("7"), Choice.Literal("8")];

    public IReadOnlyList<Choice> ParityOptions { get; } =
        [Choice.RadioDefault, Choice.Localized("None"), Choice.Localized("Even"),
         Choice.Localized("Odd"), Choice.Localized("Mark"), Choice.Localized("Space")];

    public IReadOnlyList<Choice> StopBitsOptions { get; } =
        [Choice.RadioDefault, Choice.Literal("1"), Choice.Literal("1.5"), Choice.Literal("2")];

    public IReadOnlyList<Choice> FlowCtrlOptions { get; } =
        [Choice.RadioDefault, Choice.Localized("None"), Choice.Localized("Hardware"), Choice.Localized("Software")];

    public IReadOnlyList<Choice> PttMethods { get; } =
        [Choice.RadioDefault, Choice.Localized("CAT"), Choice.Localized("RTS"),
         Choice.Localized("DTR"), Choice.Localized("VOX"), Choice.Localized("None")];

    // ── Connection fields ─────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady), nameof(ReadinessHint))]
    private ComPortInfo? _selectedPort;

    [ObservableProperty] private int          _baudRate    = BrandingInfo.DefaultBaudRate;
    [ObservableProperty] private string       _dataBits    = BrandingInfo.DefaultDataBits;
    [ObservableProperty] private string       _parity      = BrandingInfo.DefaultParity;
    [ObservableProperty] private string       _stopBits    = BrandingInfo.DefaultStopBits;
    [ObservableProperty] private string       _flowControl = BrandingInfo.DefaultFlowCtrl;
    [ObservableProperty] private string       _pttMethod   = BrandingInfo.DefaultPttMethod;

    // Hamlib model fields. 0 = no radio chosen. Model 1 is Hamlib's dummy
    // rig, which would make every test pass against a simulation — so it is
    // never treated as a valid selection.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady), nameof(ReadinessHint))]
    private int    _modelId   = 0;

    [ObservableProperty] private string _modelName = string.Empty;
    [ObservableProperty] private string _modelSearch = string.Empty;

    // ── Readiness ─────────────────────────────────────────────────────────

    /// <summary>True when enough is configured to run the test suite.</summary>
    public bool IsReady => UseRigctld || (ModelId > 1 && SelectedPort is not null);

    /// <summary>What the operator still needs to choose, or empty when ready.</summary>
    public string ReadinessHint =>
        UseRigctld              ? string.Empty
        : ModelId <= 1          ? Strings.Get("Ready_ChooseRadio")
        : SelectedPort is null  ? Strings.Get("Ready_SelectPort")
        : string.Empty;

    // Network / rigctld
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady), nameof(ReadinessHint))]
    private bool   _useRigctld   = false;
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
            CableHint    = Strings.Format("Cable_Detected", value.CableHint ?? string.Empty);
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

    // Ports are never auto-selected: many shacks have several COM ports and
    // guessing wrong sends the operator down the wrong diagnosis path.
    [RelayCommand]
    public void RefreshPorts()
    {
        var previous = SelectedPort?.PortName;
        AvailablePorts.Clear();
        foreach (var p in _ports.GetAvailablePorts())
            AvailablePorts.Add(p);

        SelectedPort = AvailablePorts.FirstOrDefault(p => p.PortName == previous);
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
        DataBits       = int.TryParse(DataBits, out var bits) ? bits : 0,
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

/// <summary>A baud rate choice; Value 0 means omit the flag and let Hamlib decide.</summary>
public record BaudOption(int Value, string Label);

/// <summary>
/// A dropdown choice for a serial setting. Value is the stable, language-
/// independent string that is saved to settings and passed to rigctl;
/// Label is what the operator sees.
/// </summary>
public record Choice(string Value, string Label)
{
    public static readonly Choice RadioDefault =
        new(BrandingInfo.RadioDefault, Strings.Get("Option_RadioDefault"));

    /// <summary>A choice whose label needs no translation (numbers).</summary>
    public static Choice Literal(string value) => new(value, value);

    /// <summary>A choice whose label comes from Strings.resx as Option_{value}.</summary>
    public static Choice Localized(string value) => new(value, Strings.Get($"Option_{value}"));
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
            ? Strings.Format("Summary_AllPassed", suite.PassCount)
            : Strings.Format("Summary_Mixed", suite.FailCount, suite.PassCount, suite.WarningCount);
    }
}

/// <summary>Single test result as rendered by ResultsDocumentBuilder.</summary>
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

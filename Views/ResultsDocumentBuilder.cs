using RigCheck.Localization;
using RigCheck.Models;
using RigCheck.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace RigCheck.Views;

// ── Results as a FlowDocument ─────────────────────────────────────────────
// The results panel is a read-only, selectable document rather than a list
// of controls so the operator can drag-select across several tests, press
// Ctrl+A / Ctrl+C, and paste the text into an email or forum post.
//
// The same document carries two kinds of content:
//   • test results   — one Section per test (status, message, command,
//                      and the diagnosis when the test failed)
//   • transcript     — free-form lines from the discovery engine: the
//                      rigctl command lines and the raw bytes sent to and
//                      received from the serial port, so a technical user
//                      can replicate the query in PuTTY or a terminal.
//
// Colors are resource references (not captured brushes) so a theme swap
// re-colors the document without rebuilding it.

/// <summary>Kind of a transcript line; decides its color and prefix.</summary>
public enum TranscriptKind
{
    /// <summary>A rigctl / Hamlib command line as it would be typed.</summary>
    Command,
    /// <summary>Bytes written to the serial port (shown as hex + ASCII).</summary>
    SerialTx,
    /// <summary>Bytes read back from the serial port.</summary>
    SerialRx,
    /// <summary>A reply from rigctl / the radio, already decoded.</summary>
    Response,
    /// <summary>Progress line while probing ("Trying COM3 at 9600…").</summary>
    Trying,
    /// <summary>Anything else worth showing in muted text.</summary>
    Note,
}

/// <summary>Builds and owns the FlowDocument shown in the results panel.</summary>
public sealed class ResultsDocumentBuilder
{
    private const string MonoFont = "Consolas";

    public FlowDocument Document { get; }

    public ResultsDocumentBuilder()
    {
        Document = new FlowDocument
        {
            // Infinity = wrap to the viewer's width; the default column
            // width would cap the text at roughly 20 ems.
            ColumnWidth  = double.PositiveInfinity,
            PagePadding  = new Thickness(0),
            FontSize     = 13,
            // FlowDocument defaults to a serif face; use the same UI font as the window
            FontFamily   = SystemFonts.MessageFontFamily,
            IsHyphenationEnabled = false,
        };
        Document.SetResourceReference(FlowDocument.ForegroundProperty, "BrushForeground");
        Document.Background = Brushes.Transparent;
    }

    // ── Test results ─────────────────────────────────────────────────────

    /// <summary>Replace the whole document with the given results, in order.</summary>
    public void Rebuild(IEnumerable<TestResultItemViewModel> items)
    {
        Document.Blocks.Clear();
        foreach (var item in items)
            Document.Blocks.Add(BuildResultSection(item));
    }

    private static Section BuildResultSection(TestResultItemViewModel item)
    {
        var section = new Section
        {
            Margin  = new Thickness(0),
            Padding = new Thickness(8, 6, 8, 6),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        section.SetResourceReference(Block.BorderBrushProperty, "BrushBorder");

        // Status glyph + test name + message on one wrapping line
        var head = new Paragraph { Margin = new Thickness(0) };
        head.Inlines.Add(Colored(new Run(item.StatusIcon + "  ") { FontWeight = FontWeights.Bold }, StatusBrushKey(item.Status)));
        head.Inlines.Add(new Run(item.Name) { FontWeight = FontWeights.SemiBold });
        if (!string.IsNullOrEmpty(item.Message))
            head.Inlines.Add(Colored(new Run("   " + item.Message), "BrushMuted"));
        section.Blocks.Add(head);

        // The rigctl command, so the operator can reproduce the test by hand
        if (!string.IsNullOrEmpty(item.DisplayCommand))
            section.Blocks.Add(CommandParagraph(item.DisplayCommand, indent: 24));

        if (item.HasDiagnosis && item.Diagnosis is { } diag)
            section.Blocks.Add(BuildDiagnosis(diag));

        return section;
    }

    // Diagnosis is always shown for a failed test — it is the whole point
    // of the app, and the tests after a failure are skipped, so it never
    // pushes other results far down.
    private static Section BuildDiagnosis(DiagnosticResult diag)
    {
        var box = new Section
        {
            Margin  = new Thickness(24, 4, 0, 2),
            Padding = new Thickness(10, 6, 10, 6),
            BorderThickness = new Thickness(2, 0, 0, 0),
        };
        box.SetResourceReference(Block.BorderBrushProperty, "BrushBorder");
        box.SetResourceReference(Block.BackgroundProperty,  "BrushBackground");

        box.Blocks.Add(new Paragraph(new Run(diag.Summary) { FontWeight = FontWeights.SemiBold })
        {
            Margin = new Thickness(0),
        });

        if (diag.Checks.Length > 0)
        {
            var list = new List
            {
                MarkerStyle  = TextMarkerStyle.Disc,
                Margin       = new Thickness(0, 4, 0, 0),
                Padding      = new Thickness(18, 0, 0, 0),
                FontSize     = 12,
            };
            foreach (var check in diag.Checks)
                list.ListItems.Add(new ListItem(new Paragraph(new Run(check)) { Margin = new Thickness(0, 0, 0, 2) }));
            box.Blocks.Add(list);
        }

        if (diag.HasFixCommand)
        {
            var p = CommandParagraph(diag.FixCommand!, indent: 0);
            p.Inlines.InsertBefore(p.Inlines.FirstInline, new Run(Strings.Get("Results_Try") + " "));
            box.Blocks.Add(p);
        }

        if (!string.IsNullOrEmpty(diag.LearnMoreUrl))
        {
            var link = new Hyperlink(new Run(Strings.Get("Results_LearnMore") + " ↗"))
            {
                NavigateUri = new Uri(diag.LearnMoreUrl),
                ToolTip     = diag.LearnMoreUrl,
            };
            link.SetResourceReference(TextElement.ForegroundProperty, "BrushAccent");
            link.RequestNavigate += (_, e) =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName        = e.Uri.AbsoluteUri,
                    UseShellExecute = true,
                });
                e.Handled = true;
            };
            box.Blocks.Add(new Paragraph(link) { Margin = new Thickness(0, 4, 0, 0), FontSize = 12 });
        }

        return box;
    }

    // ── Transcript lines (discovery engine) ───────────────────────────────

    /// <summary>
    /// Append one transcript line. Command and serial lines get a Copy
    /// button so they can be pasted into a terminal unchanged.
    /// </summary>
    public void AddTranscriptLine(TranscriptKind kind, string text)
    {
        var (prefix, brushKey, mono, italic) = kind switch
        {
            TranscriptKind.Command  => ("$ ", "BrushAccent", true,  false),
            TranscriptKind.SerialTx => ("→ ", "BrushAccent", true,  false),
            TranscriptKind.SerialRx => ("← ", "BrushPass",   true,  false),
            TranscriptKind.Response => ("  ", "BrushPass",   true,  false),
            TranscriptKind.Trying   => ("  ", "BrushMuted",  false, true),
            _                       => ("  ", "BrushMuted",  false, false),
        };

        var p = new Paragraph { Margin = new Thickness(8, 1, 8, 1) };
        var run = new Run(prefix + text);
        if (mono)   { run.FontFamily = new FontFamily(MonoFont); run.FontSize = 12; }
        if (italic) run.FontStyle = FontStyles.Italic;
        p.Inlines.Add(Colored(run, brushKey));

        if (mono)
            p.Inlines.Add(CopyButton(text));

        Document.Blocks.Add(p);
    }

    /// <summary>Remove everything (used when a new run or scan starts).</summary>
    public void Clear() => Document.Blocks.Clear();

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>A monospace command line followed by an inline Copy button.</summary>
    private static Paragraph CommandParagraph(string command, double indent)
    {
        var p = new Paragraph { Margin = new Thickness(indent, 2, 0, 0) };
        var run = new Run(command) { FontFamily = new FontFamily(MonoFont), FontSize = 12 };
        p.Inlines.Add(Colored(run, "BrushAccent"));
        p.Inlines.Add(CopyButton(command));
        return p;
    }

    // The button is a UI element, not text, so drag-selecting the line and
    // copying gives the command alone — no stray "Copy" word in the paste.
    private static InlineUIContainer CopyButton(string text)
    {
        var button = new Button
        {
            Content = Strings.Get("Main_CopyCommand"),
            ToolTip = Strings.Get("Main_CopyCommandTip"),
            Tag     = text,
            Focusable = false,
        };
        button.SetResourceReference(FrameworkElement.StyleProperty, "CopyButtonStyle");
        button.Click += (s, _) => Clipboard.SetText((string)((Button)s).Tag);
        return new InlineUIContainer(button) { BaselineAlignment = BaselineAlignment.Center };
    }

    private static Run Colored(Run run, string brushKey)
    {
        run.SetResourceReference(TextElement.ForegroundProperty, brushKey);
        return run;
    }

    private static string StatusBrushKey(TestStatus status) => status switch
    {
        TestStatus.Pass    => "BrushPass",
        TestStatus.Fail    => "BrushFail",
        TestStatus.Warning => "BrushWarn",
        _                  => "BrushMuted",
    };
}

#if SIEMENS
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace TiaAgent.AddIn.Ui;

/// <summary>
/// Creates plain-text WPF FlowDocuments for display in the response window.
/// This class has ZERO dependencies on Markdig or any external Markdown library,
/// so it can never be poisoned by a TypeInitializationException from a failed
/// Markdig assembly load. Used as the reliable fallback when Markdown rendering
/// is unavailable.
/// </summary>
public static class PlainTextFlowDocumentHelper
{
    /// <summary>
    /// Creates a plain-text FlowDocument with Consolas font for monospace display.
    /// </summary>
    public static FlowDocument Create(string text)
    {
        var flowDoc = new FlowDocument
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            PagePadding = new Thickness(8)
        };

        var paragraph = new Paragraph(new Run(text));
        flowDoc.Blocks.Add(paragraph);
        return flowDoc;
    }

    /// <summary>
    /// Creates an empty-state FlowDocument for the loading/placeholder state.
    /// </summary>
    public static FlowDocument CreateEmpty()
    {
        var flowDoc = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            PagePadding = new Thickness(8)
        };

        var paragraph = new Paragraph
        {
            Foreground = Brushes.Gray,
            FontStyle = FontStyles.Italic,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 40, 0, 0)
        };
        paragraph.Inlines.Add(new Run("Waiting for response…"));
        flowDoc.Blocks.Add(paragraph);

        return flowDoc;
    }
}
#endif

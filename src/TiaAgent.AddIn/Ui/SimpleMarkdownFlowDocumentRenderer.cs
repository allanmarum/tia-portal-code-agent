#if SIEMENS
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using TiaAgent.AddIn.Diagnostics;
using WpfDocs = System.Windows.Documents;

namespace TiaAgent.AddIn.Ui;

/// <summary>
/// Dependency-free Markdown-to-FlowDocument renderer.
/// Uses only WPF primitives — no Markdig, no external NuGet packages.
/// Designed for TIA Portal's partial-trust sandbox where external assemblies
/// may trigger VerificationException.
///
/// Supported syntax:
///   - Headings (# through ######)
///   - **bold** and *italic*
///   - `inline code`
///   - Fenced code blocks (```)
///   - Bullet lists (- or *)
///   - Numbered lists (1.)
///   - Horizontal rules (---, ***, ___)
///   - Simple Markdown tables (| col | col |)
///
/// Unsupported syntax is rendered as normal readable text.
///
/// IMPORTANT: This class has NO static fields that require initialization.
/// Brushes and fonts are created inline in Render() to ensure that a failure
/// in this renderer can never poison the type or prevent the plain-text
/// fallback from working.
/// </summary>
public sealed class SimpleMarkdownFlowDocumentRenderer : IAgentResponseRenderer
{
    /// <summary>
    /// Renders Markdown content into a WPF FlowDocument.
    /// Returns null if the content is empty.
    /// Never throws — any parsing error falls back to plain-text rendering
    /// within the same FlowDocument.
    /// </summary>
    public WpfDocs.FlowDocument? Render(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        try
        {
            // Create brushes inline — no static initialization
            var headerBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));
            headerBrush.Freeze();

            var codeBackground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
            codeBackground.Freeze();

            var codeBorder = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD));
            codeBorder.Freeze();

            var inlineCodeBackground = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
            inlineCodeBackground.Freeze();

            var tableBorderBrush = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0));
            tableBorderBrush.Freeze();

            var tableHeaderBrush = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
            tableHeaderBrush.Freeze();

            var doc = new WpfDocs.FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                PagePadding = new Thickness(8)
            };

            var lines = content.Split('\n');
            var blockCount = 0;
            var i = 0;

            while (i < lines.Length)
            {
                var line = lines[i];
                var trimmed = line.TrimEnd('\r');

                // ── Fenced code block ──
                if (trimmed.StartsWith("```"))
                {
                    var lang = trimmed.Length > 3 ? trimmed.Substring(3).Trim() : "";
                    var codeLines = new List<string>();
                    i++;
                    while (i < lines.Length)
                    {
                        var codeLine = lines[i].TrimEnd('\r');
                        if (codeLine.StartsWith("```"))
                        {
                            i++;
                            break;
                        }
                        codeLines.Add(codeLine);
                        i++;
                    }
                    AddCodeBlock(doc, string.Join("\n", codeLines), lang, codeBackground, codeBorder);
                    blockCount++;
                    continue;
                }

                // ── Horizontal rule ──
                if (IsHorizontalRule(trimmed))
                {
                    AddHorizontalRule(doc);
                    blockCount++;
                    i++;
                    continue;
                }

                // ── Heading ──
                if (trimmed.Length > 0 && trimmed[0] == '#')
                {
                    var level = 0;
                    while (level < trimmed.Length && trimmed[level] == '#') level++;
                    if (level <= 6 && level < trimmed.Length && trimmed[level] == ' ')
                    {
                        var headingText = trimmed.Substring(level + 1);
                        AddHeading(doc, headingText, level, headerBrush);
                        blockCount++;
                        i++;
                        continue;
                    }
                }

                // ── Table ──
                if (trimmed.Contains("|") && trimmed.TrimStart().StartsWith("|"))
                {
                    var tableLines = new List<string>();
                    while (i < lines.Length)
                    {
                        var tl = lines[i].TrimEnd('\r');
                        if (!tl.Contains("|") || string.IsNullOrWhiteSpace(tl))
                            break;
                        tableLines.Add(tl);
                        i++;
                    }
                    if (tableLines.Count >= 2)
                    {
                        AddTable(doc, tableLines, tableBorderBrush, tableHeaderBrush);
                        blockCount++;
                    }
                    else
                    {
                        // Not a valid table — render as text
                        foreach (var tl in tableLines)
                        {
                            AddParagraph(doc, tl, null);
                            blockCount++;
                        }
                    }
                    continue;
                }

                // ── List item (bullet or numbered) ──
                if (IsBulletItem(trimmed) || IsNumberedItem(trimmed))
                {
                    while (i < lines.Length)
                    {
                        var li = lines[i].TrimEnd('\r');
                        if (string.IsNullOrWhiteSpace(li))
                            break;
                        if (!IsBulletItem(li) && !IsNumberedItem(li))
                            break;
                        AddListItem(doc, li);
                        blockCount++;
                        i++;
                    }
                    continue;
                }

                // ── Blank line ──
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    i++;
                    continue;
                }

                // ── Regular paragraph ──
                AddParagraph(doc, trimmed, null);
                blockCount++;
                i++;
            }

            AddInLogger.Info($"SimpleMarkdownFlowDocumentRenderer: rendered {blockCount} blocks from {lines.Length} lines.");
            return doc;
        }
        catch (Exception ex)
        {
            AddInLogger.Warn($"SimpleMarkdownFlowDocumentRenderer.Render failed: {ex.GetType().Name}: {ex.Message}");
            // Return a plain-text fallback rather than null
            return CreatePlainTextFallback(content);
        }
    }

    /// <summary>
    /// Creates a plain-text FlowDocument as last-resort fallback.
    /// </summary>
    private static WpfDocs.FlowDocument CreatePlainTextFallback(string text)
    {
        var doc = new WpfDocs.FlowDocument
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            PagePadding = new Thickness(8)
        };
        doc.Blocks.Add(new WpfDocs.Paragraph(new WpfDocs.Run(text)));
        return doc;
    }

    private static bool IsHorizontalRule(string line)
    {
        if (line.Length < 3) return false;
        char c = line[0];
        if (c != '-' && c != '*' && c != '_') return false;
        for (int j = 0; j < line.Length; j++)
        {
            if (line[j] != c && line[j] != ' ') return false;
        }
        // Must contain at least 3 of the marker character
        int count = 0;
        for (int j = 0; j < line.Length; j++)
        {
            if (line[j] == c) count++;
        }
        return count >= 3;
    }

    private static bool IsBulletItem(string line)
    {
        var t = line.TrimStart();
        return (t.StartsWith("- ") || t.StartsWith("* ")) && t.Length > 2;
    }

    private static bool IsNumberedItem(string line)
    {
        var t = line.TrimStart();
        var dot = t.IndexOf(". ");
        if (dot <= 0 || dot > 9) return false;
        for (int j = 0; j < dot; j++)
        {
            if (!char.IsDigit(t[j])) return false;
        }
        return true;
    }

    // ── Block rendering ──

    private static void AddHeading(WpfDocs.FlowDocument doc, string text, int level, Brush headerBrush)
    {
        var para = new WpfDocs.Paragraph
        {
            Foreground = headerBrush,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, level == 1 ? 12 : 8, 0, 4)
        };
        para.FontSize = level switch
        {
            1 => 20,
            2 => 17,
            3 => 15,
            _ => 14
        };
        AddInlineSpans(para, text);
        doc.Blocks.Add(para);
    }

    private static void AddParagraph(WpfDocs.FlowDocument doc, string text, Brush? foreground)
    {
        var para = new WpfDocs.Paragraph
        {
            Margin = new Thickness(0, 0, 0, 8)
        };
        if (foreground != null)
            para.Foreground = foreground;
        AddInlineSpans(para, text);
        doc.Blocks.Add(para);
    }

    private static void AddCodeBlock(WpfDocs.FlowDocument doc, string code, string lang,
        Brush background, Brush borderBrush)
    {
        if (string.IsNullOrEmpty(code))
            return;

        var section = new WpfDocs.Section
        {
            Background = background,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 4, 0, 8),
            Padding = new Thickness(8)
        };

        if (!string.IsNullOrEmpty(lang))
        {
            var langLabel = new WpfDocs.Paragraph
            {
                FontSize = 10,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 4)
            };
            langLabel.Inlines.Add(new WpfDocs.Run(lang));
            section.Blocks.Add(langLabel);
        }

        var codePara = new WpfDocs.Paragraph
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12
        };
        codePara.Inlines.Add(new WpfDocs.Run(code));
        section.Blocks.Add(codePara);

        doc.Blocks.Add(section);
    }

    private static void AddHorizontalRule(WpfDocs.FlowDocument doc)
    {
        var para = new WpfDocs.Paragraph
        {
            Margin = new Thickness(0, 8, 0, 8),
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 8, 0, 0)
        };
        doc.Blocks.Add(para);
    }

    private static void AddListItem(WpfDocs.FlowDocument doc, string line)
    {
        var trimmed = line.TrimStart();
        string marker;
        string text;

        if (IsBulletItem(trimmed))
        {
            marker = "• "; // bullet character
            text = trimmed.Substring(2);
        }
        else
        {
            // Numbered item
            var dot = trimmed.IndexOf(". ");
            marker = trimmed.Substring(0, dot + 2);
            text = trimmed.Substring(dot + 2);
        }

        var para = new WpfDocs.Paragraph
        {
            Margin = new Thickness(16, 0, 0, 4)
        };
        para.Inlines.Add(new WpfDocs.Run(marker) { Foreground = Brushes.Gray });
        AddInlineSpans(para, text);
        doc.Blocks.Add(para);
    }

    private static void AddTable(WpfDocs.FlowDocument doc, List<string> tableLines,
        Brush borderBrush, Brush headerBrush)
    {
        // Parse header row
        var headerCells = ParseTableRow(tableLines[0]);

        // Check if row 1 is a separator (|---|---|)
        int dataStart = 1;
        if (tableLines.Count > 1 && IsTableSeparator(tableLines[1]))
            dataStart = 2;

        var wpfTable = new WpfDocs.Table
        {
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1),
            CellSpacing = 0,
            Margin = new Thickness(0, 4, 0, 8)
        };

        for (int c = 0; c < headerCells.Count; c++)
            wpfTable.Columns.Add(new WpfDocs.TableColumn());

        var rowGroup = new WpfDocs.TableRowGroup();

        // Header row
        var headerRow = new WpfDocs.TableRow();
        foreach (var cellText in headerCells)
        {
            var cell = new WpfDocs.TableCell
            {
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(6),
                Background = headerBrush,
                FontWeight = FontWeights.SemiBold
            };
            var para = new WpfDocs.Paragraph { Margin = new Thickness(0) };
            AddInlineSpans(para, cellText.Trim());
            cell.Blocks.Add(para);
            headerRow.Cells.Add(cell);
        }
        rowGroup.Rows.Add(headerRow);

        // Data rows
        for (int r = dataStart; r < tableLines.Count; r++)
        {
            var cells = ParseTableRow(tableLines[r]);
            var row = new WpfDocs.TableRow();
            for (int c = 0; c < headerCells.Count; c++)
            {
                var cellText = c < cells.Count ? cells[c].Trim() : "";
                var cell = new WpfDocs.TableCell
                {
                    BorderBrush = borderBrush,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Padding = new Thickness(6)
                };
                var para = new WpfDocs.Paragraph { Margin = new Thickness(0) };
                AddInlineSpans(para, cellText);
                cell.Blocks.Add(para);
                row.Cells.Add(cell);
            }
            rowGroup.Rows.Add(row);
        }

        wpfTable.RowGroups.Add(rowGroup);
        doc.Blocks.Add(wpfTable);
    }

    private static List<string> ParseTableRow(string line)
    {
        var cells = new List<string>();
        var trimmed = line.Trim();
        if (trimmed.StartsWith("|")) trimmed = trimmed.Substring(1);
        if (trimmed.EndsWith("|")) trimmed = trimmed.Substring(0, trimmed.Length - 1);

        foreach (var cell in trimmed.Split('|'))
        {
            cells.Add(cell);
        }
        return cells;
    }

    private static bool IsTableSeparator(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith("|")) trimmed = trimmed.Substring(1);
        if (trimmed.EndsWith("|")) trimmed = trimmed.Substring(0, trimmed.Length - 1);

        foreach (var cell in trimmed.Split('|'))
        {
            var c = cell.Trim();
            if (c.Length == 0) return false;
            foreach (var ch in c)
            {
                if (ch != '-' && ch != ':' && ch != ' ') return false;
            }
        }
        return true;
    }

    // ── Inline rendering ──

    /// <summary>
    /// Parses inline Markdown spans (bold, italic, inline code) and adds them
    /// to the paragraph. Unsupported syntax is rendered as literal text.
    /// </summary>
    private static void AddInlineSpans(WpfDocs.Paragraph paragraph, string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        int i = 0;
        while (i < text.Length)
        {
            // ── Inline code ──
            if (text[i] == '`')
            {
                var end = text.IndexOf('`', i + 1);
                if (end > i + 1)
                {
                    var code = text.Substring(i + 1, end - i - 1);
                    var run = new WpfDocs.Run(code)
                    {
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 12,
                        Background = Brushes.WhiteSmoke,
                        BaselineAlignment = BaselineAlignment.Center
                    };
                    paragraph.Inlines.Add(run);
                    i = end + 1;
                    continue;
                }
            }

            // ── Bold (**text**) ──
            if (i + 1 < text.Length && text[i] == '*' && text[i + 1] == '*')
            {
                var end = text.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (end > i + 2)
                {
                    var bold = text.Substring(i + 2, end - i - 2);
                    var run = new WpfDocs.Run(bold) { FontWeight = FontWeights.Bold };
                    paragraph.Inlines.Add(run);
                    i = end + 2;
                    continue;
                }
            }

            // ── Italic (*text*) ──
            if (text[i] == '*' && (i + 1 >= text.Length || text[i + 1] != '*'))
            {
                var end = text.IndexOf('*', i + 1);
                if (end > i + 1)
                {
                    var italic = text.Substring(i + 1, end - i - 1);
                    var run = new WpfDocs.Run(italic) { FontStyle = FontStyles.Italic };
                    paragraph.Inlines.Add(run);
                    i = end + 1;
                    continue;
                }
            }

            // ── Literal text (collect until next special character) ──
            var start = i;
            while (i < text.Length && text[i] != '`' && text[i] != '*')
                i++;

            if (i > start)
            {
                paragraph.Inlines.Add(new WpfDocs.Run(text.Substring(start, i - start)));
            }

            // If we stopped at a special char that didn't form a valid span,
            // emit it as literal and move on
            if (i < text.Length && i == start)
            {
                paragraph.Inlines.Add(new WpfDocs.Run(text[i].ToString()));
                i++;
            }
        }
    }
}
#endif

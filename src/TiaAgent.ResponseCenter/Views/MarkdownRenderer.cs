using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Extensions.Tables;
using WpfDocuments = System.Windows.Documents;

namespace TiaAgent.ResponseCenter.Views;

/// <summary>
/// Renders Markdown content into a WPF FlowDocument.
/// Uses Markdig for parsing and a custom renderer for WPF elements.
/// Falls back to plain text if rendering fails.
/// </summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline s_pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    // Colors matching the existing AddIn palette
    private static readonly SolidColorBrush HeaderBrush = new(Color.FromRgb(0x00, 0x78, 0xD4));
    private static readonly SolidColorBrush CodeBackground = new(Color.FromRgb(0xF5, 0xF5, 0xF5));
    private static readonly SolidColorBrush CodeBorder = new(Color.FromRgb(0xDD, 0xDD, 0xDD));
    private static readonly SolidColorBrush InlineCodeBackground = new(Color.FromRgb(0xF0, 0xF0, 0xF0));
    private static readonly SolidColorBrush BlockquoteBrush = new(Color.FromRgb(0x6A, 0x73, 0x7D));
    private static readonly SolidColorBrush WarningBackground = new(Color.FromRgb(0xFF, 0xF8, 0xE1));
    private static readonly SolidColorBrush WarningBorder = new(Color.FromRgb(0xFF, 0xD7, 0x00));
    private static readonly SolidColorBrush TableBorderBrush = new(Color.FromRgb(0xD0, 0xD0, 0xD0));
    private static readonly SolidColorBrush TableHeaderBrush = new(Color.FromRgb(0xF0, 0xF0, 0xF0));

    static MarkdownRenderer()
    {
        // Freeze brushes for performance
        HeaderBrush.Freeze();
        CodeBackground.Freeze();
        CodeBorder.Freeze();
        InlineCodeBackground.Freeze();
        BlockquoteBrush.Freeze();
        WarningBackground.Freeze();
        WarningBorder.Freeze();
        TableBorderBrush.Freeze();
        TableHeaderBrush.Freeze();
    }

    /// <summary>
    /// Renders Markdown text into a FlowDocument.
    /// Returns null if rendering fails (caller should fall back to plain text).
    /// </summary>
    public static WpfDocuments.FlowDocument? Render(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return null;

        try
        {
            var document = Markdig.Markdown.Parse(markdown, s_pipeline);
            var flowDoc = new WpfDocuments.FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                PagePadding = new Thickness(8)
            };

            foreach (var block in document)
            {
                RenderBlock(flowDoc, block);
            }

            return flowDoc;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Creates a plain-text selectable fallback when Markdown rendering fails.
    /// </summary>
    public static WpfDocuments.FlowDocument CreatePlainTextFallback(string text)
    {
        var flowDoc = new WpfDocuments.FlowDocument
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            PagePadding = new Thickness(8)
        };

        var paragraph = new WpfDocuments.Paragraph(new WpfDocuments.Run(text));
        flowDoc.Blocks.Add(paragraph);
        return flowDoc;
    }

    private static void RenderBlock(WpfDocuments.FlowDocument doc, Markdig.Syntax.Block block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                RenderHeading(doc, heading);
                break;
            case ParagraphBlock paragraph:
                RenderParagraph(doc, paragraph);
                break;
            case ListBlock list:
                RenderList(doc, list, 0);
                break;
            case FencedCodeBlock fencedCode:
                RenderCodeBlock(doc, fencedCode);
                break;
            case CodeBlock codeBlock:
                RenderCodeBlock(doc, codeBlock);
                break;
            case QuoteBlock quote:
                RenderQuoteBlock(doc, quote);
                break;
            case ThematicBreakBlock:
                RenderHorizontalRule(doc);
                break;
            case Markdig.Extensions.Tables.Table table:
                RenderTable(doc, table);
                break;
            default:
                // Unknown block type — render as plain text
                var fallback = new WpfDocuments.Paragraph(new WpfDocuments.Run(block.ToString()));
                doc.Blocks.Add(fallback);
                break;
        }
    }

    private static void RenderHeading(WpfDocuments.FlowDocument doc, HeadingBlock heading)
    {
        var paragraph = new WpfDocuments.Paragraph
        {
            Foreground = HeaderBrush,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, heading.Level == 1 ? 12 : 8, 0, 4)
        };

        paragraph.FontSize = heading.Level switch
        {
            1 => 20,
            2 => 17,
            3 => 15,
            _ => 14
        };

        RenderInlineChildren(paragraph, heading.Inline);
        doc.Blocks.Add(paragraph);
    }

    private static void RenderParagraph(WpfDocuments.FlowDocument doc, ParagraphBlock paragraphBlock)
    {
        var paragraph = new WpfDocuments.Paragraph
        {
            Margin = new Thickness(0, 0, 0, 8)
        };

        RenderInlineChildren(paragraph, paragraphBlock.Inline);
        doc.Blocks.Add(paragraph);
    }

    private static void RenderList(WpfDocuments.FlowDocument doc, ListBlock list, int depth)
    {
        int index = 1;
        foreach (var item in list)
        {
            if (item is ListItemBlock listItem)
            {
                foreach (var subBlock in listItem)
                {
                    if (subBlock is ParagraphBlock para)
                    {
                        var paragraph = new WpfDocuments.Paragraph
                        {
                            Margin = new Thickness(16 + depth * 16, 0, 0, 4)
                        };

                        var marker = list.IsOrdered
                            ? $"{index}. "
                            : "• ";
                        paragraph.Inlines.Add(new WpfDocuments.Run(marker)
                        {
                            Foreground = Brushes.Gray
                        });

                        RenderInlineChildren(paragraph, para.Inline);
                        doc.Blocks.Add(paragraph);
                    }
                    else if (subBlock is ListBlock nestedList)
                    {
                        RenderList(doc, nestedList, depth + 1);
                    }
                }
                index++;
            }
        }
    }

    private static void RenderCodeBlock(WpfDocuments.FlowDocument doc, CodeBlock codeBlock)
    {
        var code = codeBlock is FencedCodeBlock fenced
            ? (fenced.Lines.ToString() ?? "").TrimEnd()
            : (codeBlock.ToString() ?? "").TrimEnd();

        if (string.IsNullOrEmpty(code))
            return;

        // Container with border and background
        var section = new WpfDocuments.Section
        {
            Background = CodeBackground,
            BorderBrush = CodeBorder,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 4, 0, 8),
            Padding = new Thickness(8)
        };

        // Language label if available
        if (codeBlock is FencedCodeBlock fencedBlock && !string.IsNullOrEmpty(fencedBlock.Info))
        {
            var langLabel = new WpfDocuments.Paragraph
            {
                FontSize = 10,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 4)
            };
            langLabel.Inlines.Add(new WpfDocuments.Run(fencedBlock.Info));
            section.Blocks.Add(langLabel);
        }

        var codeParagraph = new WpfDocuments.Paragraph
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12
        };
        codeParagraph.Inlines.Add(new WpfDocuments.Run(code));
        section.Blocks.Add(codeParagraph);

        // Copy button hint
        var copyHint = new WpfDocuments.Paragraph
        {
            FontSize = 10,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 4, 0, 0),
            TextAlignment = TextAlignment.Right
        };
        copyHint.Inlines.Add(new WpfDocuments.Run("📋 Copy"));
        section.Blocks.Add(copyHint);

        doc.Blocks.Add(section);
    }

    private static void RenderQuoteBlock(WpfDocuments.FlowDocument doc, QuoteBlock quote)
    {
        var section = new WpfDocuments.Section
        {
            BorderBrush = BlockquoteBrush,
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(8, 0, 0, 0),
            Margin = new Thickness(0, 4, 0, 8)
        };

        foreach (var block in quote)
        {
            if (block is ParagraphBlock para)
            {
                var paragraph = new WpfDocuments.Paragraph
                {
                    Foreground = BlockquoteBrush,
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                RenderInlineChildren(paragraph, para.Inline);
                section.Blocks.Add(paragraph);
            }
        }

        doc.Blocks.Add(section);
    }

    private static void RenderHorizontalRule(WpfDocuments.FlowDocument doc)
    {
        var paragraph = new WpfDocuments.Paragraph
        {
            Margin = new Thickness(0, 8, 0, 8),
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 8, 0, 0)
        };
        doc.Blocks.Add(paragraph);
    }

    private static void RenderTable(WpfDocuments.FlowDocument doc, Markdig.Extensions.Tables.Table table)
    {
        var wpfTable = new WpfDocuments.Table
        {
            BorderBrush = TableBorderBrush,
            BorderThickness = new Thickness(1),
            CellSpacing = 0,
            Margin = new Thickness(0, 4, 0, 8)
        };

        // Add columns
        var columnCount = table.ColumnDefinitions?.Count ?? 0;
        if (columnCount == 0)
        {
            // Count columns from first row
            foreach (var row in table)
            {
                if (row is Markdig.Extensions.Tables.TableRow tr)
                {
                    columnCount = tr.Count;
                    break;
                }
            }
        }

        for (int i = 0; i < Math.Max(columnCount, 1); i++)
        {
            wpfTable.Columns.Add(new WpfDocuments.TableColumn());
        }

        var rowGroup = new WpfDocuments.TableRowGroup();

        foreach (var row in table)
        {
            if (row is Markdig.Extensions.Tables.TableRow tr)
            {
                var wpfRow = new WpfDocuments.TableRow();
                bool isHeader = tr.IsHeader;

                foreach (var cell in tr)
                {
                    if (cell is Markdig.Extensions.Tables.TableCell tc)
                    {
                        var wpfCell = new WpfDocuments.TableCell
                        {
                            BorderBrush = TableBorderBrush,
                            BorderThickness = new Thickness(0, 0, 1, 1),
                            Padding = new Thickness(6),
                            Background = isHeader ? TableHeaderBrush : Brushes.Transparent
                        };

                        foreach (var block in tc)
                        {
                            if (block is ParagraphBlock para)
                            {
                                var paragraph = new WpfDocuments.Paragraph
                                {
                                    Margin = new Thickness(0),
                                    FontWeight = isHeader ? FontWeights.SemiBold : FontWeights.Normal
                                };
                                RenderInlineChildren(paragraph, para.Inline);
                                wpfCell.Blocks.Add(paragraph);
                            }
                        }

                        wpfRow.Cells.Add(wpfCell);
                    }
                }

                rowGroup.Rows.Add(wpfRow);
            }
        }

        wpfTable.RowGroups.Add(rowGroup);
        doc.Blocks.Add(wpfTable);
    }

    private static void RenderInlineChildren(WpfDocuments.Paragraph paragraph, ContainerInline? container)
    {
        if (container == null) return;

        foreach (var inline in container)
        {
            RenderInline(paragraph, inline);
        }
    }

    private static void RenderInline(WpfDocuments.Paragraph paragraph, Markdig.Syntax.Inlines.Inline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                paragraph.Inlines.Add(new WpfDocuments.Run(literal.Content.ToString()));
                break;

            case EmphasisInline emphasis:
                var run = new WpfDocuments.Run();
                RenderInlineChildren(run, emphasis);
                if (emphasis.DelimiterCount >= 2)
                    run.FontWeight = FontWeights.Bold;
                if (emphasis.DelimiterCount == 1)
                    run.FontStyle = FontStyles.Italic;
                paragraph.Inlines.Add(run);
                break;

            case CodeInline code:
                var codeRun = new WpfDocuments.Run(code.Content)
                {
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    Background = InlineCodeBackground,
                    BaselineAlignment = BaselineAlignment.Center
                };
                paragraph.Inlines.Add(codeRun);
                break;

            case LinkInline link:
                var linkRun = new WpfDocuments.Run();
                RenderInlineChildren(linkRun, link);
                linkRun.Foreground = HeaderBrush;
                linkRun.TextDecorations = TextDecorations.Underline;
                if (!string.IsNullOrEmpty(link.Url))
                    linkRun.ToolTip = link.Url;
                paragraph.Inlines.Add(linkRun);
                break;

            case LineBreakInline:
                paragraph.Inlines.Add(new WpfDocuments.LineBreak());
                break;

            case HtmlInline html:
                // Render HTML tags as literal text
                paragraph.Inlines.Add(new WpfDocuments.Run(html.Tag));
                break;

            default:
                // Unknown inline — try ToString
                var text = inline.ToString();
                if (!string.IsNullOrEmpty(text))
                    paragraph.Inlines.Add(new WpfDocuments.Run(text));
                break;
        }
    }

    private static void RenderInlineChildren(WpfDocuments.Run parentRun, ContainerInline? container)
    {
        // For emphasis, we can't add children to a Run directly in WPF.
        // This is handled by the Paragraph-level rendering.
        // This method exists for completeness but is typically not used.
    }
}

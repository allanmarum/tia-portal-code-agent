#if SIEMENS
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Extensions.Tables;
using TiaAgent.AddIn.Diagnostics;
using WpfDocs = System.Windows.Documents;

namespace TiaAgent.AddIn.Ui;

/// <summary>
/// Renders Markdown content into a WPF FlowDocument using Markdig.
/// Adapted from TiaAgent.ResponseCenter.Views.MarkdownRenderer for the
/// Add-In's net48 sandbox environment.
///
/// Falls back to a plain-text FlowDocument if rendering fails.
/// </summary>
public sealed class MarkdownFlowDocumentRenderer : IAgentResponseRenderer
{
    // Lazy-initialized: avoids TypeInitializationException poisoning the entire type.
    // If the pipeline or brushes fail to load, IsAvailable stays false and the
    // caller falls back to plain-text rendering.
    private static MarkdownPipeline? s_pipeline;
    private static SolidColorBrush? s_headerBrush;
    private static SolidColorBrush? s_codeBackground;
    private static SolidColorBrush? s_codeBorder;
    private static SolidColorBrush? s_inlineCodeBackground;
    private static SolidColorBrush? s_blockquoteBrush;
    private static SolidColorBrush? s_tableBorderBrush;
    private static SolidColorBrush? s_tableHeaderBrush;
    private static bool s_initialized;
    private static bool s_initFailed;

    /// <summary>
    /// Returns true if the renderer is fully initialized and ready to use.
    /// </summary>
    public static bool IsAvailable => s_initialized && !s_initFailed;

    // Track consecutive failures to allow retry but avoid spam
    private static int s_consecutiveFailures;
    private const int MaxConsecutiveFailures = 3;

    private static void EnsureInitialized()
    {
        if (s_initialized) return;

        try
        {
            AddInLogger.Info("MarkdownFlowDocumentRenderer: starting initialization...");

            s_pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();
            AddInLogger.Info("MarkdownFlowDocumentRenderer: Markdig pipeline built successfully.");

            s_headerBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));
            s_codeBackground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
            s_codeBorder = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD));
            s_inlineCodeBackground = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
            s_blockquoteBrush = new SolidColorBrush(Color.FromRgb(0x6A, 0x73, 0x7D));
            s_tableBorderBrush = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0));
            s_tableHeaderBrush = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));

            // Freeze brushes for performance — they must not be modified after freezing
            s_headerBrush.Freeze();
            s_codeBackground.Freeze();
            s_codeBorder.Freeze();
            s_inlineCodeBackground.Freeze();
            s_blockquoteBrush.Freeze();
            s_tableBorderBrush.Freeze();
            s_tableHeaderBrush.Freeze();

            // Only mark as initialized AFTER all resources are ready
            s_initialized = true;
            s_consecutiveFailures = 0;
            AddInLogger.Info("MarkdownFlowDocumentRenderer: initialization complete — renderer is available.");
        }
        catch (TypeInitializationException ex)
        {
            // TypeInitializationException is permanent for this AppDomain — don't retry
            s_initFailed = true;
            AddInLogger.Warn($"MarkdownFlowDocumentRenderer: initialization FAILED (permanent) — " +
                             $"Error: {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
                AddInLogger.Warn($"  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
        }
        catch (Exception ex)
        {
            // VerificationException and other transient failures — allow retry on next call.
            // In TIA Portal's partial-trust sandbox, the JIT verifier's behavior is
            // non-deterministic: the same Markdig code sometimes passes verification
            // and sometimes fails. Retrying on the next call (possibly a different thread)
            // gives the JIT another chance to succeed.
            s_consecutiveFailures++;
            if (s_consecutiveFailures >= MaxConsecutiveFailures)
            {
                s_initFailed = true;
                AddInLogger.Warn($"MarkdownFlowDocumentRenderer: initialization FAILED after " +
                                 $"{MaxConsecutiveFailures} attempts — giving up. " +
                                 $"Error: {ex.GetType().Name}: {ex.Message}");
            }
            else
            {
                AddInLogger.Warn($"MarkdownFlowDocumentRenderer: initialization attempt " +
                                 $"{s_consecutiveFailures}/{MaxConsecutiveFailures} failed " +
                                 $"(will retry on next call): {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                    AddInLogger.Warn($"  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            }
        }
    }

    /// <summary>
    /// Renders Markdown text into a FlowDocument.
    /// Returns null if the content is empty or rendering fails.
    /// </summary>
    public WpfDocs.FlowDocument? Render(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        EnsureInitialized();

        if (s_initFailed || s_pipeline == null)
        {
            AddInLogger.Warn("MarkdownFlowDocumentRenderer.Render: renderer not available, returning null.");
            return null;
        }

        try
        {
            AddInLogger.Info($"MarkdownFlowDocumentRenderer.Render: parsing {content.Length} chars...");
            var document = Markdig.Markdown.Parse(content, s_pipeline);
            AddInLogger.Info($"MarkdownFlowDocumentRenderer.Render: parsed {document.Count} top-level blocks.");

            var flowDoc = new WpfDocs.FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                PagePadding = new Thickness(8)
            };

            int blockCount = 0;
            foreach (var block in document)
            {
                RenderBlock(flowDoc, block);
                blockCount++;
            }

            AddInLogger.Info($"MarkdownFlowDocumentRenderer.Render: rendered {blockCount} blocks into FlowDocument.");
            return flowDoc;
        }
        catch (Exception ex)
        {
            AddInLogger.Warn($"MarkdownFlowDocumentRenderer.Render: Markdown parsing/rendering failed: " +
                             $"{ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
                AddInLogger.Warn($"  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            return null;
        }
    }

    /// <summary>
    /// Creates a plain-text fallback when Markdown rendering fails.
    /// </summary>
    public static WpfDocs.FlowDocument CreatePlainTextFallback(string text)
    {
        var flowDoc = new WpfDocs.FlowDocument
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            PagePadding = new Thickness(8)
        };

        var paragraph = new WpfDocs.Paragraph(new WpfDocs.Run(text));
        flowDoc.Blocks.Add(paragraph);
        return flowDoc;
    }

    private static void RenderBlock(WpfDocs.FlowDocument doc, Block block)
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
                var fallback = new WpfDocs.Paragraph(new WpfDocs.Run(block.ToString()));
                doc.Blocks.Add(fallback);
                break;
        }
    }

    private static void RenderHeading(WpfDocs.FlowDocument doc, HeadingBlock heading)
    {
        var paragraph = new WpfDocs.Paragraph
        {
            Foreground = s_headerBrush ?? Brushes.Black,
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

    private static void RenderParagraph(WpfDocs.FlowDocument doc, ParagraphBlock paragraphBlock)
    {
        var paragraph = new WpfDocs.Paragraph
        {
            Margin = new Thickness(0, 0, 0, 8)
        };

        RenderInlineChildren(paragraph, paragraphBlock.Inline);
        doc.Blocks.Add(paragraph);
    }

    private static void RenderList(WpfDocs.FlowDocument doc, ListBlock list, int depth)
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
                        var paragraph = new WpfDocs.Paragraph
                        {
                            Margin = new Thickness(16 + depth * 16, 0, 0, 4)
                        };

                        var marker = list.IsOrdered
                            ? $"{index}. "
                            : "• ";
                        paragraph.Inlines.Add(new WpfDocs.Run(marker)
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

    private static void RenderCodeBlock(WpfDocs.FlowDocument doc, CodeBlock codeBlock)
    {
        var code = codeBlock is FencedCodeBlock fenced
            ? (fenced.Lines.ToString() ?? "").TrimEnd()
            : (codeBlock.ToString() ?? "").TrimEnd();

        if (string.IsNullOrEmpty(code))
            return;

        // Container with border and background
        var section = new WpfDocs.Section
        {
            Background = s_codeBackground ?? Brushes.WhiteSmoke,
            BorderBrush = s_codeBorder ?? Brushes.LightGray,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 4, 0, 8),
            Padding = new Thickness(8)
        };

        // Language label if available
        if (codeBlock is FencedCodeBlock fencedBlock && !string.IsNullOrEmpty(fencedBlock.Info))
        {
            var langLabel = new WpfDocs.Paragraph
            {
                FontSize = 10,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 4)
            };
            langLabel.Inlines.Add(new WpfDocs.Run(fencedBlock.Info));
            section.Blocks.Add(langLabel);
        }

        var codeParagraph = new WpfDocs.Paragraph
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12
        };
        codeParagraph.Inlines.Add(new WpfDocs.Run(code));
        section.Blocks.Add(codeParagraph);

        doc.Blocks.Add(section);
    }

    private static void RenderQuoteBlock(WpfDocs.FlowDocument doc, QuoteBlock quote)
    {
        var section = new WpfDocs.Section
        {
            BorderBrush = s_blockquoteBrush ?? Brushes.Gray,
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(8, 0, 0, 0),
            Margin = new Thickness(0, 4, 0, 8)
        };

        foreach (var block in quote)
        {
            if (block is ParagraphBlock para)
            {
                var paragraph = new WpfDocs.Paragraph
                {
                    Foreground = s_blockquoteBrush ?? Brushes.Gray,
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                RenderInlineChildren(paragraph, para.Inline);
                section.Blocks.Add(paragraph);
            }
        }

        doc.Blocks.Add(section);
    }

    private static void RenderHorizontalRule(WpfDocs.FlowDocument doc)
    {
        var paragraph = new WpfDocs.Paragraph
        {
            Margin = new Thickness(0, 8, 0, 8),
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 8, 0, 0)
        };
        doc.Blocks.Add(paragraph);
    }

    private static void RenderTable(WpfDocs.FlowDocument doc, Markdig.Extensions.Tables.Table table)
    {
        var wpfTable = new WpfDocs.Table
        {
            BorderBrush = s_tableBorderBrush ?? Brushes.LightGray,
            BorderThickness = new Thickness(1),
            CellSpacing = 0,
            Margin = new Thickness(0, 4, 0, 8)
        };

        // Add columns
        var columnCount = table.ColumnDefinitions?.Count ?? 0;
        if (columnCount == 0)
        {
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
            wpfTable.Columns.Add(new WpfDocs.TableColumn());
        }

        var rowGroup = new WpfDocs.TableRowGroup();

        foreach (var row in table)
        {
            if (row is Markdig.Extensions.Tables.TableRow tr)
            {
                var wpfRow = new WpfDocs.TableRow();
                bool isHeader = tr.IsHeader;

                foreach (var cell in tr)
                {
                    if (cell is Markdig.Extensions.Tables.TableCell tc)
                    {
                        var wpfCell = new WpfDocs.TableCell
                        {
                            BorderBrush = s_tableBorderBrush ?? Brushes.LightGray,
                            BorderThickness = new Thickness(0, 0, 1, 1),
                            Padding = new Thickness(6),
                            Background = isHeader ? (s_tableHeaderBrush ?? Brushes.WhiteSmoke) : Brushes.Transparent
                        };

                        foreach (var block in tc)
                        {
                            if (block is ParagraphBlock para)
                            {
                                var paragraph = new WpfDocs.Paragraph
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

    private static void RenderInlineChildren(WpfDocs.Paragraph paragraph, ContainerInline? container)
    {
        if (container == null) return;

        foreach (var inline in container)
        {
            RenderInline(paragraph, inline);
        }
    }

    private static void RenderInline(WpfDocs.Paragraph paragraph, Inline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                paragraph.Inlines.Add(new WpfDocs.Run(literal.Content.ToString()));
                break;

            case EmphasisInline emphasis:
                // Build text content from all child inlines
                var emphasisText = CollectInlineText(emphasis);
                var run = new WpfDocs.Run(emphasisText);
                if (emphasis.DelimiterCount >= 2)
                    run.FontWeight = FontWeights.Bold;
                if (emphasis.DelimiterCount == 1)
                    run.FontStyle = FontStyles.Italic;
                paragraph.Inlines.Add(run);
                break;

            case CodeInline code:
                var codeRun = new WpfDocs.Run(code.Content)
                {
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    Background = s_inlineCodeBackground ?? Brushes.LightGray,
                    BaselineAlignment = BaselineAlignment.Center
                };
                paragraph.Inlines.Add(codeRun);
                break;

            case LinkInline link:
                var linkRun = new WpfDocs.Run();
                RenderInlineChildren(linkRun, link);
                linkRun.Foreground = s_headerBrush ?? Brushes.Blue;
                linkRun.TextDecorations = TextDecorations.Underline;
                if (!string.IsNullOrEmpty(link.Url))
                    linkRun.ToolTip = link.Url;
                paragraph.Inlines.Add(linkRun);
                break;

            case LineBreakInline:
                paragraph.Inlines.Add(new WpfDocs.LineBreak());
                break;

            case HtmlInline html:
                // Render HTML tags as literal text
                paragraph.Inlines.Add(new WpfDocs.Run(html.Tag));
                break;

            default:
                // Unknown inline — try ToString
                var text = inline.ToString();
                if (!string.IsNullOrEmpty(text))
                    paragraph.Inlines.Add(new WpfDocs.Run(text));
                break;
        }
    }

    private static void RenderInlineChildren(WpfDocs.Run parentRun, ContainerInline? container)
    {
        // For emphasis, WPF Run doesn't support nested inlines directly.
        // The emphasis rendering handles this at the Paragraph level.
    }

    /// <summary>
    /// Collects all text content from child inlines into a single string.
    /// Used for EmphasisInline where WPF Run cannot hold nested inlines.
    /// </summary>
    private static string CollectInlineText(ContainerInline container)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    sb.Append(literal.Content.ToString());
                    break;
                case CodeInline code:
                    sb.Append(code.Content);
                    break;
                case LineBreakInline:
                    sb.Append('\n');
                    break;
                case EmphasisInline nested:
                    sb.Append(CollectInlineText(nested));
                    break;
                case HtmlInline html:
                    sb.Append(html.Tag);
                    break;
                default:
                    var text = inline.ToString();
                    if (!string.IsNullOrEmpty(text))
                        sb.Append(text);
                    break;
            }
        }
        return sb.ToString();
    }
}
#endif

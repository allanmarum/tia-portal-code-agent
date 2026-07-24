using FluentAssertions;
using TiaAgent.AddIn.Ui;
using Xunit;

namespace TiaAgent.AddIn.Tests;

public class SimpleMarkdownFlowDocumentRendererTests
{
    private readonly SimpleMarkdownFlowDocumentRenderer _renderer = new();

    [Fact]
    public void Render_ReturnsNullForEmptyInput()
    {
        _renderer.Render("").Should().BeNull();
        _renderer.Render(null!).Should().BeNull();
        _renderer.Render("   ").Should().BeNull();
    }

    [Fact]
    public void Render_ProducesFlowDocumentForValidMarkdown()
    {
        var doc = _renderer.Render("# Hello\n\nSome text.");
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Render_HandlesHeadings()
    {
        var doc = _renderer.Render("# H1\n## H2\n### H3");
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Render_HandlesBoldAndItalic()
    {
        var doc = _renderer.Render("This is **bold** and *italic* text.");
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().Be(1);
    }

    [Fact]
    public void Render_HandlesInlineCode()
    {
        var doc = _renderer.Render("Use `var x = 1;` in your code.");
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().Be(1);
    }

    [Fact]
    public void Render_HandlesFencedCodeBlocks()
    {
        var md = "```csharp\nvar x = 1;\nvar y = 2;\n```";
        var doc = _renderer.Render(md);
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Render_HandlesUnorderedLists()
    {
        var md = "- Item 1\n- Item 2\n- Item 3";
        var doc = _renderer.Render(md);
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Render_HandlesOrderedList()
    {
        var md = "1. First\n2. Second\n3. Third";
        var doc = _renderer.Render(md);
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Render_HandlesHorizontalRule()
    {
        var md = "Before\n\n---\n\nAfter";
        var doc = _renderer.Render(md);
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Render_HandlesTable()
    {
        var md = "| Name | Value |\n|------|-------|\n| Foo  | 1     |\n| Bar  | 2     |";
        var doc = _renderer.Render(md);
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Render_HandlesMixedContent()
    {
        var md = @"# Title

Some paragraph with **bold** and *italic*.

- List item 1
- List item 2

```python
print('hello')
```

---

End.";
        var doc = _renderer.Render(md);
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().BeGreaterThan(5);
    }

    [Fact]
    public void Render_DoesNotThrowOnMalformedMarkdown()
    {
        var cases = new[]
        {
            "```\nunclosed code block",
            "**unclosed bold",
            "| incomplete | table",
            new string('x', 10000),
        };

        foreach (var md in cases)
        {
            var act = () => _renderer.Render(md);
            act.Should().NotThrow();
        }
    }

    [Fact]
    public void Render_LargeResponse_DoesNotThrow()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 1000; i++)
        {
            sb.AppendLine($"## Section {i}");
            sb.AppendLine();
            sb.AppendLine("Lorem ipsum dolor sit amet, consectetur adipiscing elit.");
            sb.AppendLine();
        }

        var act = () => _renderer.Render(sb.ToString());
        act.Should().NotThrow();
    }

    [Fact]
    public void PlainTextFlowDocumentHelper_Create_ReturnsDocumentForTextInput()
    {
        var doc = PlainTextFlowDocumentHelper.Create("Hello World");
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().Be(1);
    }

    [Fact]
    public void PlainTextFlowDocumentHelper_Create_HandlesEmptyString()
    {
        var doc = PlainTextFlowDocumentHelper.Create("");
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().Be(1);
    }

    [Fact]
    public void PlainTextFlowDocumentHelper_CreateEmpty_ReturnsEmptyStateDocument()
    {
        var doc = PlainTextFlowDocumentHelper.CreateEmpty();
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().Be(1);
    }

    // ── Deterministic runtime test ──

    [Fact]
    public void Render_DeterministicRuntimeTest()
    {
        // Fixed content from the requirements — verifies all major syntax elements
        // render correctly in a single document.
        var md = @"# Test title

This is **bold** and this is `inline code`.

- First item
- Second item";

        var doc = _renderer.Render(md);

        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().BeGreaterThanOrEqualTo(3,
            "heading + paragraph + 2 list items should produce at least 3 blocks");

        // Verify the heading block exists and has the right font size
        var heading = doc.Blocks.First() as System.Windows.Documents.Paragraph;
        heading.Should().NotBeNull();
        heading!.FontSize.Should().Be(20, "H1 headings should be 20pt");

        // Verify the paragraph contains bold and inline code runs
        var para = doc.Blocks.ElementAt(1) as System.Windows.Documents.Paragraph;
        para.Should().NotBeNull();
        var inlines = para!.Inlines.ToList();
        inlines.Count.Should().BeGreaterThanOrEqualTo(3,
            "paragraph should contain: literal 'This is ', bold 'bold', literal ' and this is ', code 'inline code', literal '.'");

        // Verify list items exist
        doc.Blocks.Count.Should().BeGreaterThanOrEqualTo(4,
            "document should have heading + paragraph + 2 list items");
    }
}

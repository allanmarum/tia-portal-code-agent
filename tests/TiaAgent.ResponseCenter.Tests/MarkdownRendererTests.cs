using FluentAssertions;
using TiaAgent.ResponseCenter.Views;
using Xunit;

namespace TiaAgent.ResponseCenter.Tests;

public class MarkdownRendererTests
{
    [Fact]
    public void Render_ReturnsNullForEmptyInput()
    {
        MarkdownRenderer.Render("").Should().BeNull();
        MarkdownRenderer.Render(null!).Should().BeNull();
        MarkdownRenderer.Render("   ").Should().BeNull();
    }

    [Fact]
    public void Render_ProducesFlowDocumentForValidMarkdown()
    {
        var doc = MarkdownRenderer.Render("# Hello\n\nSome text.");
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Render_HandlesHeadings()
    {
        var doc = MarkdownRenderer.Render("# H1\n## H2\n### H3");
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Render_HandlesCodeBlocks()
    {
        var md = "```csharp\nvar x = 1;\n```";
        var doc = MarkdownRenderer.Render(md);
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Render_HandlesBulletLists()
    {
        var md = "- Item 1\n- Item 2\n- Item 3";
        var doc = MarkdownRenderer.Render(md);
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Render_HandlesTables()
    {
        var md = "| Col1 | Col2 |\n|------|------|\n| A | B |";
        var doc = MarkdownRenderer.Render(md);
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Render_HandlesBlockquotes()
    {
        var md = "> This is a quote";
        var doc = MarkdownRenderer.Render(md);
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().Be(1);
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

> A blockquote

---

End.";
        var doc = MarkdownRenderer.Render(md);
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().BeGreaterThan(5);
    }

    [Fact]
    public void Render_DoesNotThrowOnMalformedMarkdown()
    {
        // Various edge cases that might break the renderer
        var cases = new[]
        {
            "```\nunclosed code block",
            "[broken link](",
            "![",
            "**unclosed bold",
            "| incomplete | table",
            new string('x', 10000),
        };

        foreach (var md in cases)
        {
            var doc = MarkdownRenderer.Render(md);
            // Should either return a valid doc or null (fallback), never throw
        }
    }

    [Fact]
    public void CreatePlainTextFallback_ProducesDocument()
    {
        var doc = MarkdownRenderer.CreatePlainTextFallback("Hello\nWorld");
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().Be(1);
    }
}

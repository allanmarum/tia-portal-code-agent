using FluentAssertions;
using TiaAgent.AddIn.Ui;
using Xunit;

namespace TiaAgent.AddIn.Tests;

public class MarkdownFlowDocumentRendererTests
{
    private readonly MarkdownFlowDocumentRenderer _renderer = new();

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
    public void Render_HandlesBlockquotes()
    {
        var md = "> This is a quote";
        var doc = _renderer.Render(md);
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().Be(1);
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
    public void Render_HandlesLinks()
    {
        var md = "Visit [GitHub](https://github.com) for more.";
        var doc = _renderer.Render(md);
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
        var doc = _renderer.Render(md);
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().BeGreaterThan(5);
    }

    [Fact]
    public void Render_HandlesMultilineResponse()
    {
        var lines = Enumerable.Range(1, 100)
            .Select(i => $"Line {i}: Some content here")
            .ToArray();
        var md = string.Join("\n\n", lines);
        var doc = _renderer.Render(md);
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().BeGreaterThanOrEqualTo(50);
    }

    [Fact]
    public void Render_HandlesLiteralBackslashInCode()
    {
        var md = @"```csharp
var path = ""C:\Users\test"";
var regex = ""\\d+\\."";
```";
        var doc = _renderer.Render(md);
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Render_DoesNotThrowOnMalformedMarkdown()
    {
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
    public void CreatePlainTextFallback_ProducesDocument()
    {
        var doc = MarkdownFlowDocumentRenderer.CreatePlainTextFallback("Hello\nWorld");
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().Be(1);
    }

    [Fact]
    public void CreatePlainTextFallback_HandlesEmptyString()
    {
        var doc = MarkdownFlowDocumentRenderer.CreatePlainTextFallback("");
        doc.Should().NotBeNull();
        doc!.Blocks.Count.Should().Be(1);
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
}

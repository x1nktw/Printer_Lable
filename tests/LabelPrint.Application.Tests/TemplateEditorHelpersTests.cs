using FluentAssertions;
using LabelPrint.Application.Templates;

namespace LabelPrint.Application.Tests;

public class TemplateAlignmentHelperTests
{
    [Fact]
    public void AlignToCanvasCenterHorizontal_Centers_Single_Element()
    {
        var items = new List<TemplateAlignmentHelper.MutableBounds>
        {
            new() { Id = "a", X = 0, Y = 5, Width = 20, Height = 10 }
        };

        TemplateAlignmentHelper.AlignToCanvasCenterHorizontal(items, canvasWidthMm: 58);

        items[0].X.Should().Be(19);
    }

    [Fact]
    public void AlignLeft_Aligns_Relative_To_Selection()
    {
        var items = new List<TemplateAlignmentHelper.MutableBounds>
        {
            new() { Id = "a", X = 10, Y = 0, Width = 5, Height = 5 },
            new() { Id = "b", X = 30, Y = 0, Width = 5, Height = 5 }
        };

        TemplateAlignmentHelper.AlignLeft(items);

        items.Should().OnlyContain(b => b.X == 10);
    }
}

public class EditorUndoStackTests
{
    [Fact]
    public void Undo_Redo_Restores_Snapshots()
    {
        var stack = new EditorUndoStack();
        stack.Push("state-a");
        stack.CanUndo.Should().BeTrue();

        var undone = stack.Undo("state-b");
        undone.Should().Be("state-a");
        stack.CanRedo.Should().BeTrue();

        var redone = stack.Redo("state-a");
        redone.Should().Be("state-b");
    }

    [Fact]
    public void Push_Clears_Redo_Branch()
    {
        var stack = new EditorUndoStack();
        stack.Push("a");
        stack.Undo("b");
        stack.CanRedo.Should().BeTrue();

        stack.Push("c");
        stack.CanRedo.Should().BeFalse();
    }
}

public class TemplateOverflowCheckerTests
{
    [Fact]
    public void Detects_Element_Outside_Canvas()
    {
        var elements = new[]
        {
            new TemplateOverflowChecker.ElementRect("inside", 2, 2, 10, 10),
            new TemplateOverflowChecker.ElementRect("outside", 50, 2, 20, 10)
        };

        TemplateOverflowChecker.GetOverflowElementIds(elements, 58, 40)
            .Should().ContainSingle()
            .Which.Should().Be("outside");

        TemplateOverflowChecker.BuildStatusMessage(elements, 58, 40)
            .Should().Contain("1 элемент");
    }

    [Fact]
    public void PreviewTextResolver_Replaces_Variable_Placeholders()
    {
        var variables = new Dictionary<string, string> { ["ProductName"] = "Молоко" };
        var text = TemplatePreviewTextResolver.Resolve(
            Domain.Enums.TemplateElementType.Text,
            Domain.Enums.TextBindingMode.Literal,
            "Товар: {{ProductName}}",
            null,
            variables);

        text.Should().Be("Товар: Молоко");
    }
}

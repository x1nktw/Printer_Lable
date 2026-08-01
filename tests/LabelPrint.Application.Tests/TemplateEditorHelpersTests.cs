using FluentAssertions;
using LabelPrint.Application.Templates;

namespace LabelPrint.Application.Tests;

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

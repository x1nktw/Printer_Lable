namespace LabelPrint.Domain.Enums;

/// <summary>How a text element content is resolved.</summary>
public enum TextBindingMode
{
    Literal = 0,
    Variable = 1,
    CurrentDate = 2,
    CurrentTime = 3
}

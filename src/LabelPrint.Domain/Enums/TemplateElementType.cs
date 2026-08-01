namespace LabelPrint.Domain.Enums;

/// <summary>
/// Visual element types in label template JSON schema.
/// Date/Time/Variable are Text bindings, not separate types.
/// </summary>
public enum TemplateElementType
{
    Text = 0,
    Image = 1,
    Barcode = 2,
    QrCode = 3,
    Rectangle = 4,
    Ellipse = 5,
    Line = 6,
    Table = 7
}

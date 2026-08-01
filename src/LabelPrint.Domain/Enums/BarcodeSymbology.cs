namespace LabelPrint.Domain.Enums;

/// <summary>Supported barcode / 2D symbologies.</summary>
public enum BarcodeSymbology
{
    Ean13 = 0,
    Ean8 = 1,
    Code128 = 2,
    Code39 = 3,
    QrCode = 4,
    DataMatrix = 5
}

using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LabelPrint.UI.Models;

public partial class AccentOption : ObservableObject
{
    public AccentOption(string name, string hex, bool light = false)
    {
        Name = name;
        Hex = hex;
        IsLight = light;
        Brush = new SolidColorBrush(Color.Parse(hex));
        OutlineBrush = light
            ? new SolidColorBrush(Color.Parse("#9CA3AF"))
            : Brushes.Transparent;
    }

    public string Name { get; }
    public string Hex { get; }
    public bool IsLight { get; }
    public IBrush Brush { get; }
    public IBrush OutlineBrush { get; }

    [ObservableProperty] private bool _isSelected;
}

using LabelPrint.Domain.Enums;

namespace LabelPrint.UI.Models;

public sealed class ThemeOption
{
    public ThemeOption(AppTheme theme, string name)
    {
        Theme = theme;
        Name = name;
    }

    public AppTheme Theme { get; }
    public string Name { get; }

    public override string ToString() => Name;
}

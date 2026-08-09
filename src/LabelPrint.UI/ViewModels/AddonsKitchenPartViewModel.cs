using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using LabelPrint.Application.Templates;
using LabelPrint.Domain.Enums;
using LabelPrint.Domain.Templates;

namespace LabelPrint.UI.ViewModels;

public enum AddonsKitchenPartKind
{
    Title,
    Underline,
    Icon,
    Text,
    Separator,
    EmptyText,
    EmptyImage
}

/// <summary>Editable sub-part while inside an AddonsKitchen isolation session.</summary>
public partial class AddonsKitchenPartViewModel : ObservableObject
{
    private readonly Func<double> _zoom;
    private readonly Func<CanvasElementViewModel?> _parent;
    private readonly Func<bool> _previewEmptyMode;

    public AddonsKitchenPartViewModel(
        AddonsKitchenPartKind kind,
        AddonsKitchenPart part,
        bool isRowRelative,
        Func<double> zoom,
        Func<CanvasElementViewModel?> parent,
        Func<bool> previewEmptyMode,
        string? displayName = null)
    {
        Kind = kind;
        _zoom = zoom;
        _parent = parent;
        _previewEmptyMode = previewEmptyMode;
        IsRowRelative = isRowRelative;
        DisplayName = displayName ?? kind switch
        {
            AddonsKitchenPartKind.Title => "Заголовок",
            AddonsKitchenPartKind.Underline => "Линия под заголовком",
            AddonsKitchenPartKind.Icon => "Иконка строки",
            AddonsKitchenPartKind.Text => "Текст строки",
            AddonsKitchenPartKind.Separator => "Разделитель",
            AddonsKitchenPartKind.EmptyText => "Текст (нет добавок)",
            AddonsKitchenPartKind.EmptyImage => "Картинка (нет добавок)",
            _ => kind.ToString()
        };

        _visible = part.Visible;
        _content = part.Content;
        _imagePath = part.ImagePath;
        _xMm = part.Bounds.X;
        _yMm = part.Bounds.Y;
        _widthMm = part.Bounds.Width;
        _heightMm = part.Bounds.Height;
        _fontFamily = part.Font?.Family ?? "Inter";
        _fontSizePt = part.Font?.SizePt > 0 ? part.Font.SizePt : 8;
        _isBold = part.Font?.Bold ?? true;
        _horizontalAlign = part.Font?.HorizontalAlign ?? TextHorizontalAlign.Left;
        _verticalAlign = part.Font?.VerticalAlign ?? TextVerticalAlign.Top;
        _invert = part.Invert;
        _strokeThickness = part.StrokeThickness > 0 ? part.StrokeThickness : 0.3;
        _dashed = part.Dashed;
    }

    public event Action? Changed;

    public AddonsKitchenPartKind Kind { get; }

    public string DisplayName { get; private set; }

    public bool IsRowRelative { get; }

    public bool IsEmptyContent => Kind is AddonsKitchenPartKind.EmptyText or AddonsKitchenPartKind.EmptyImage;

    public bool IsStructural => !IsEmptyContent;

    public bool CanDelete => IsEmptyContent;

    public bool IsTextPart => Kind is AddonsKitchenPartKind.Title or AddonsKitchenPartKind.Text or AddonsKitchenPartKind.EmptyText;

    public bool IsLinePart => Kind is AddonsKitchenPartKind.Underline or AddonsKitchenPartKind.Separator;

    public bool IsIconPart => Kind is AddonsKitchenPartKind.Icon or AddonsKitchenPartKind.EmptyImage;

    public bool ShowsEditableContent => Kind is AddonsKitchenPartKind.Title or AddonsKitchenPartKind.EmptyText;

    public bool ShowsImagePicker => Kind is AddonsKitchenPartKind.EmptyImage;

    public bool IsEmptyPart => IsEmptyContent;

    /// <summary>Accent for canvas chrome / layers list.</summary>
    public IBrush RoleBrush => Kind switch
    {
        AddonsKitchenPartKind.Title => Solid("FF2B6CB0"),
        AddonsKitchenPartKind.Underline => Solid("FF718096"),
        AddonsKitchenPartKind.Icon => Solid("FFDD6B20"),
        AddonsKitchenPartKind.Text => Solid("FF2F855A"),
        AddonsKitchenPartKind.Separator => Solid("FF805AD5"),
        AddonsKitchenPartKind.EmptyText => Solid("FFC53030"),
        AddonsKitchenPartKind.EmptyImage => Solid("FFD69E2E"),
        _ => Solid("FF4A5568")
    };

    public IBrush RoleFillBrush => Kind switch
    {
        AddonsKitchenPartKind.Title => Solid("332B6CB0"),
        AddonsKitchenPartKind.Underline => Solid("33718096"),
        AddonsKitchenPartKind.Icon => Solid("33DD6B20"),
        AddonsKitchenPartKind.Text => Solid("332F855A"),
        AddonsKitchenPartKind.Separator => Solid("33805AD5"),
        AddonsKitchenPartKind.EmptyText => Solid("33C53030"),
        AddonsKitchenPartKind.EmptyImage => Solid("33D69E2E"),
        _ => Solid("334A5568")
    };

    [ObservableProperty] private bool _visible = true;
    [ObservableProperty] private string? _content;
    [ObservableProperty] private string? _imagePath;
    [ObservableProperty] private double _xMm;
    [ObservableProperty] private double _yMm;
    [ObservableProperty] private double _widthMm;
    [ObservableProperty] private double _heightMm;
    [ObservableProperty] private string _fontFamily = "Inter";
    [ObservableProperty] private double _fontSizePt = 8;
    [ObservableProperty] private bool _isBold = true;
    [ObservableProperty] private TextHorizontalAlign _horizontalAlign = TextHorizontalAlign.Left;
    [ObservableProperty] private TextVerticalAlign _verticalAlign = TextVerticalAlign.Top;
    [ObservableProperty] private bool _invert;
    [ObservableProperty] private double _strokeThickness = 0.3;
    [ObservableProperty] private bool _dashed;
    [ObservableProperty] private bool _isSelected;

    public bool IsActiveInPreview =>
        Visible && (_previewEmptyMode() ? IsEmptyContent : IsStructural);

    public double LayerOpacity => IsActiveInPreview ? 1.0 : 0.38;

    public double LeftPx
    {
        get
        {
            var parent = _parent();
            if (parent is null)
            {
                return 0;
            }

            var scale = TemplateEditorViewModel.PxPerMm * _zoom();
            return (parent.XMm + XMm) * scale;
        }
    }

    public double TopPx
    {
        get
        {
            var parent = _parent();
            if (parent is null)
            {
                return 0;
            }

            var scale = TemplateEditorViewModel.PxPerMm * _zoom();
            var y = parent.YMm + (IsRowRelative ? parent.AddonsKitchenRowsOriginYMm : 0) + YMm;
            return y * scale;
        }
    }

    public double WidthPx => Math.Max(1, WidthMm * TemplateEditorViewModel.PxPerMm * _zoom());

    public double HeightPx => Math.Max(1, (IsLinePart ? Math.Max(HeightMm, StrokeThickness) : HeightMm) * TemplateEditorViewModel.PxPerMm * _zoom());

    public double FontSizePx => TemplateEditorViewModel.FontSizePtToPx(FontSizePt, _zoom());

    public string PreviewLabel => Kind switch
    {
        AddonsKitchenPartKind.Title => string.IsNullOrWhiteSpace(Content) ? AddonsKitchenLayoutDefaults.DefaultTitle : Content,
        AddonsKitchenPartKind.EmptyText => string.IsNullOrWhiteSpace(Content) ? AddonsKitchenLayoutDefaults.DefaultEmptyText : Content,
        AddonsKitchenPartKind.Text => "Добавить халапеньо",
        AddonsKitchenPartKind.Icon or AddonsKitchenPartKind.EmptyImage =>
            string.IsNullOrWhiteSpace(ImagePath) ? "⧉" : ImagePath,
        AddonsKitchenPartKind.Underline or AddonsKitchenPartKind.Separator => string.Empty,
        _ => DisplayName
    };

    public string LayerHint => IsEmptyContent ? "нет добавок" : "есть добавки";

    public Avalonia.Media.TextAlignment PreviewTextAlignment => HorizontalAlign switch
    {
        TextHorizontalAlign.Center => Avalonia.Media.TextAlignment.Center,
        TextHorizontalAlign.Right => Avalonia.Media.TextAlignment.Right,
        _ => Avalonia.Media.TextAlignment.Left
    };

    public Avalonia.Layout.HorizontalAlignment PreviewHorizontalAlignment => HorizontalAlign switch
    {
        TextHorizontalAlign.Center => Avalonia.Layout.HorizontalAlignment.Center,
        TextHorizontalAlign.Right => Avalonia.Layout.HorizontalAlignment.Right,
        _ => Avalonia.Layout.HorizontalAlignment.Left
    };

    public Avalonia.Layout.VerticalAlignment PreviewVerticalAlignment => VerticalAlign switch
    {
        TextVerticalAlign.Middle => Avalonia.Layout.VerticalAlignment.Center,
        TextVerticalAlign.Bottom => Avalonia.Layout.VerticalAlignment.Bottom,
        _ => Avalonia.Layout.VerticalAlignment.Top
    };

    public void SetDisplayName(string name) => DisplayName = name;

    public void NotifyPreviewModeChanged()
    {
        OnPropertyChanged(nameof(IsActiveInPreview));
        OnPropertyChanged(nameof(LayerOpacity));
    }

    public void NotifyScaleChanged()
    {
        OnPropertyChanged(nameof(LeftPx));
        OnPropertyChanged(nameof(TopPx));
        OnPropertyChanged(nameof(WidthPx));
        OnPropertyChanged(nameof(HeightPx));
        OnPropertyChanged(nameof(FontSizePx));
    }

    public void MoveByPixels(double dx, double dy, bool snap, double blockWidthMm, double blockHeightMm, double rowHeightMm)
    {
        var scale = TemplateEditorViewModel.PxPerMm * _zoom();
        var x = XMm + dx / scale;
        var y = YMm + dy / scale;
        if (snap)
        {
            x = Math.Round(x * 2) / 2;
            y = Math.Round(y * 2) / 2;
        }

        if (IsRowRelative)
        {
            XMm = Math.Clamp(x, 0, Math.Max(0, blockWidthMm - WidthMm));
            YMm = Math.Clamp(y, 0, Math.Max(0, rowHeightMm));
        }
        else
        {
            XMm = Math.Clamp(x, 0, Math.Max(0, blockWidthMm - WidthMm));
            YMm = Math.Clamp(y, 0, Math.Max(0, blockHeightMm - HeightMm));
        }
    }

    public AddonsKitchenPart ToPart() => new()
    {
        Visible = Visible,
        PartType = Kind is AddonsKitchenPartKind.EmptyImage
            ? AddonsKitchenLayoutDefaults.PartTypeImage
            : AddonsKitchenLayoutDefaults.PartTypeText,
        Content = Content,
        ImagePath = ImagePath,
        Bounds = new TemplateBounds { X = XMm, Y = YMm, Width = WidthMm, Height = HeightMm },
        Font = IsTextPart
            ? new TemplateFont
            {
                Family = FontFamily,
                SizePt = FontSizePt,
                Bold = IsBold,
                HorizontalAlign = HorizontalAlign,
                VerticalAlign = VerticalAlign
            }
            : null,
        Invert = Invert,
        StrokeThickness = StrokeThickness,
        Dashed = Dashed
    };

    partial void OnVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsActiveInPreview));
        OnPropertyChanged(nameof(LayerOpacity));
        RaiseChanged();
    }
    partial void OnContentChanged(string? value)
    {
        OnPropertyChanged(nameof(PreviewLabel));
        RaiseChanged();
    }
    partial void OnImagePathChanged(string? value)
    {
        OnPropertyChanged(nameof(PreviewLabel));
        RaiseChanged();
    }
    partial void OnXMmChanged(double value)
    {
        NotifyScaleChanged();
        RaiseChanged();
    }
    partial void OnYMmChanged(double value)
    {
        NotifyScaleChanged();
        RaiseChanged();
    }
    partial void OnWidthMmChanged(double value)
    {
        NotifyScaleChanged();
        RaiseChanged();
    }
    partial void OnHeightMmChanged(double value)
    {
        NotifyScaleChanged();
        RaiseChanged();
    }
    partial void OnFontFamilyChanged(string value) => RaiseChanged();
    partial void OnFontSizePtChanged(double value)
    {
        OnPropertyChanged(nameof(FontSizePx));
        RaiseChanged();
    }
    partial void OnIsBoldChanged(bool value) => RaiseChanged();
    partial void OnHorizontalAlignChanged(TextHorizontalAlign value)
    {
        OnPropertyChanged(nameof(PreviewTextAlignment));
        OnPropertyChanged(nameof(PreviewHorizontalAlignment));
        RaiseChanged();
    }

    partial void OnVerticalAlignChanged(TextVerticalAlign value)
    {
        OnPropertyChanged(nameof(PreviewVerticalAlignment));
        RaiseChanged();
    }
    partial void OnInvertChanged(bool value) => RaiseChanged();
    partial void OnStrokeThicknessChanged(double value)
    {
        NotifyScaleChanged();
        RaiseChanged();
    }
    partial void OnDashedChanged(bool value) => RaiseChanged();

    private void RaiseChanged() => Changed?.Invoke();

    private static IBrush Solid(string argb) =>
        new SolidColorBrush(Color.Parse("#" + argb));
}

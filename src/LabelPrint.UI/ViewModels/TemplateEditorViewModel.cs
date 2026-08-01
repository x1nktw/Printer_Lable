using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Templates;
using LabelPrint.Domain.Enums;
using LabelPrint.Domain.Templates;
using LabelPrint.Plugins.Abstractions.Variables;
using LabelPrint.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace LabelPrint.UI.ViewModels;

/// <summary>
/// Visual label template editor (Stage 4: undo, grid, guides, multi-select, align, preview).
/// </summary>
public partial class TemplateEditorViewModel : PageViewModelBase
{
    public const double PxPerMm = 8;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUiDialogService _dialogs;
    private readonly Func<Task> _navigateBackAsync;
    private readonly Guid _templateId;
    private readonly EditorUndoStack _undoStack = new();
    private string _cleanSnapshot = string.Empty;
    private bool _suppressUndo;
    private string _dragSnapshot = string.Empty;
    private IReadOnlyDictionary<string, string> _previewVariables = new Dictionary<string, string>();

    public TemplateEditorViewModel(
        IServiceScopeFactory scopeFactory,
        IUiDialogService dialogs,
        Guid templateId,
        Func<Task> navigateBackAsync)
    {
        _scopeFactory = scopeFactory;
        _dialogs = dialogs;
        _templateId = templateId;
        _navigateBackAsync = navigateBackAsync;
        Title = "Редактор шаблона";
        Elements.CollectionChanged += OnElementsCollectionChanged;
        SelectedElements.CollectionChanged += OnSelectedElementsChanged;

        foreach (var variable in TemplateVariablePalette.KnownVariables)
        {
            VariableDefinitions.Add(new TemplateVariableItemViewModel(variable, InsertVariable));
        }
    }

    public ObservableCollection<CanvasElementViewModel> Elements { get; } = new();

    public ObservableCollection<CanvasElementViewModel> SelectedElements { get; } = new();

    public ObservableCollection<TemplateVariableItemViewModel> VariableDefinitions { get; } = new();

    public ObservableCollection<SnapGuideViewModel> SnapGuides { get; } = new();

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private double _widthMm = 58;
    [ObservableProperty] private double _heightMm = 40;
    [ObservableProperty] private double _zoom = 1;
    [ObservableProperty] private CanvasElementViewModel? _selected;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _snapEnabled = true;
    [ObservableProperty] private bool _hasUnsavedChanges;
    [ObservableProperty] private bool _showGrid = true;
    [ObservableProperty] private bool _isPreviewMode;
    [ObservableProperty] private bool _canUndo;
    [ObservableProperty] private bool _canRedo;

    public double CanvasWidthPx => WidthMm * PxPerMm * Zoom;
    public double CanvasHeightPx => HeightMm * PxPerMm * Zoom;

    public bool HasMultiSelection => SelectedElements.Count > 1;

    partial void OnWidthMmChanged(double value)
    {
        OnPropertyChanged(nameof(CanvasWidthPx));
        RefreshOverflowState();
        RefreshDirtyState();
    }

    partial void OnHeightMmChanged(double value)
    {
        OnPropertyChanged(nameof(CanvasHeightPx));
        RefreshOverflowState();
        RefreshDirtyState();
    }

    partial void OnNameChanged(string value) => RefreshDirtyState();

    partial void OnZoomChanged(double value)
    {
        OnPropertyChanged(nameof(CanvasWidthPx));
        OnPropertyChanged(nameof(CanvasHeightPx));
        foreach (var el in Elements)
        {
            el.NotifyScaleChanged();
        }
    }

    partial void OnIsPreviewModeChanged(bool value)
    {
        if (value)
        {
            _ = LoadPreviewVariablesAsync();
        }
        else
        {
            RefreshAllPreviewText();
        }
    }

    partial void OnSelectedChanged(CanvasElementViewModel? value)
    {
        OnPropertyChanged(nameof(HasMultiSelection));
    }

    public async Task<bool> TryLeaveAsync()
    {
        RefreshDirtyState();
        if (!HasUnsavedChanges)
        {
            return true;
        }

        var choice = await _dialogs.ConfirmUnsavedChangesAsync(
            "Несохранённые изменения",
            "В шаблоне есть несохранённые изменения. Сохранить перед выходом?");

        switch (choice)
        {
            case UnsavedChangesResult.Cancel:
                return false;
            case UnsavedChangesResult.Discard:
                return true;
            case UnsavedChangesResult.Save:
                await SaveAsync();
                return !HasUnsavedChanges;
            default:
                return false;
        }
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        if (await TryLeaveAsync())
        {
            await _navigateBackAsync();
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITemplateService>();
        var result = await service.GetAsync(_templateId);
        if (result.IsFailure)
        {
            StatusMessage = result.Error;
            return;
        }

        var dto = result.Value;
        RestoreFromSnapshot(TemplateDocumentSerializer.Serialize(new TemplateDocument
        {
            SchemaVersion = 1,
            Name = dto.Name,
            Canvas = new TemplateCanvas { WidthMm = dto.WidthMm, HeightMm = dto.HeightMm, Dpi = 203 },
            Elements = dto.Document.Elements
        }), markClean: true);

        if (IsPreviewMode)
        {
            await LoadPreviewVariablesAsync();
        }

        StatusMessage = $"Элементов: {Elements.Count}";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var document = BuildDocument();
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITemplateService>();
        var result = await service.SaveDocumentAsync(_templateId, Name, document);
        if (result.IsFailure)
        {
            StatusMessage = result.Error;
            return;
        }

        MarkClean();
        StatusMessage = "Шаблон сохранён";
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        var snapshot = _undoStack.Undo(CaptureSnapshot());
        if (snapshot is null)
        {
            return;
        }

        RestoreFromSnapshot(snapshot);
        UpdateUndoRedoState();
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        var snapshot = _undoStack.Redo(CaptureSnapshot());
        if (snapshot is null)
        {
            return;
        }

        RestoreFromSnapshot(snapshot);
        UpdateUndoRedoState();
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedElements.Count == 0)
        {
            return;
        }

        RecordUndo();
        var toRemove = SelectedElements.ToList();
        ClearSelection();
        foreach (var item in toRemove)
        {
            DetachElement(item);
            Elements.Remove(item);
        }

        RefreshDirtyState();
        RefreshOverflowState();
    }

    [RelayCommand]
    private void DuplicateSelected()
    {
        if (SelectedElements.Count == 0)
        {
            return;
        }

        RecordUndo();
        var copies = new List<CanvasElementViewModel>();
        foreach (var item in SelectedElements.ToList())
        {
            var copy = item.Clone(2, 2);
            AttachElement(copy);
            Elements.Add(copy);
            copies.Add(copy);
        }

        SetSelection(copies);
        RefreshDirtyState();
        RefreshOverflowState();
    }

    [RelayCommand]
    private void GroupSelected()
    {
        if (SelectedElements.Count < 2)
        {
            return;
        }

        RecordUndo();
        var groupId = Guid.NewGuid().ToString("N");
        foreach (var item in SelectedElements)
        {
            item.GroupId = groupId;
        }

        RefreshDirtyState();
    }

    [RelayCommand]
    private void UngroupSelected()
    {
        if (SelectedElements.Count == 0)
        {
            return;
        }

        RecordUndo();
        foreach (var item in SelectedElements)
        {
            item.GroupId = null;
        }

        RefreshDirtyState();
    }

    [RelayCommand]
    private void AlignLeft() => Align(TemplateAlignmentHelper.AlignLeft);

    [RelayCommand]
    private void AlignCenterHorizontal() => Align(TemplateAlignmentHelper.AlignCenterHorizontal);

    [RelayCommand]
    private void AlignRight() => Align(TemplateAlignmentHelper.AlignRight);

    [RelayCommand]
    private void AlignTop() => Align(TemplateAlignmentHelper.AlignTop);

    [RelayCommand]
    private void AlignCenterVertical() => Align(TemplateAlignmentHelper.AlignCenterVertical);

    [RelayCommand]
    private void AlignBottom() => Align(TemplateAlignmentHelper.AlignBottom);

    public void SelectElement(CanvasElementViewModel element, bool addToSelection)
    {
        if (addToSelection)
        {
            if (SelectedElements.Contains(element))
            {
                SelectedElements.Remove(element);
                element.IsSelected = false;
                Selected = SelectedElements.LastOrDefault();
            }
            else
            {
                SelectedElements.Add(element);
                element.IsSelected = true;
                Selected = element;
            }
        }
        else
        {
            SetSelection([element]);
        }

        OnPropertyChanged(nameof(HasMultiSelection));
    }

    public void ClearSelection()
    {
        foreach (var item in SelectedElements.ToList())
        {
            item.IsSelected = false;
        }

        SelectedElements.Clear();
        Selected = null;
        OnPropertyChanged(nameof(HasMultiSelection));
    }

    public void BeginDrag(CanvasElementViewModel element)
    {
        _dragSnapshot = CaptureSnapshot();
        UpdateSnapGuides(element);
    }

    public void DragMove(CanvasElementViewModel element, double dx, double dy)
    {
        element.MoveByPixels(dx, dy, SnapEnabled);
        UpdateSnapGuides(element);
        RefreshOverflowState();
    }

    public void EndDrag()
    {
        SnapGuides.Clear();
        if (!_dragSnapshot.Equals(CaptureSnapshot(), StringComparison.Ordinal))
        {
            _undoStack.Push(_dragSnapshot);
            UpdateUndoRedoState();
            RefreshDirtyState();
        }

        _dragSnapshot = string.Empty;
    }

    public void SelectInRect(double x1Px, double y1Px, double x2Px, double y2Px, bool addToSelection)
    {
        var scale = PxPerMm * Zoom;
        var minX = Math.Min(x1Px, x2Px) / scale;
        var maxX = Math.Max(x1Px, x2Px) / scale;
        var minY = Math.Min(y1Px, y2Px) / scale;
        var maxY = Math.Max(y1Px, y2Px) / scale;

        var hits = Elements.Where(el =>
            el.XMm + el.WidthMm >= minX && el.XMm <= maxX &&
            el.YMm + el.HeightMm >= minY && el.YMm <= maxY).ToList();

        if (!addToSelection)
        {
            SetSelection(hits);
        }
        else
        {
            foreach (var hit in hits)
            {
                if (!SelectedElements.Contains(hit))
                {
                    SelectedElements.Add(hit);
                    hit.IsSelected = true;
                }
            }

            Selected = SelectedElements.LastOrDefault();
        }

        OnPropertyChanged(nameof(HasMultiSelection));
    }

    private void InsertVariable(TemplateVariablePalette.VariableDefinition variable) =>
        AddElementInternal(
            TemplateElementType.Text,
            variable.DisplayName,
            "{{" + variable.Key + "}}",
            TextBindingMode.Variable,
            variable.Key,
            recordUndo: true);

    [RelayCommand]
    private void AddText() => AddElementInternal(TemplateElementType.Text, "Текст", "{{ProductName}}");

    [RelayCommand]
    private void AddPrice() => AddElementInternal(TemplateElementType.Text, "Цена", "{{Price}}", TextBindingMode.Variable, "Price");

    [RelayCommand]
    private void AddBarcode() => AddElementInternal(TemplateElementType.Barcode, "Штрихкод", null, TextBindingMode.Variable, "Barcode");

    [RelayCommand]
    private void AddQrCode() => AddElementInternal(TemplateElementType.QrCode, "QR-код", null, TextBindingMode.Variable, "Barcode", BarcodeSymbology.QrCode);

    [RelayCommand]
    private void AddRectangle() => AddElementInternal(TemplateElementType.Rectangle, "Прямоугольник");

    [RelayCommand]
    private void AddEllipse() => AddElementInternal(TemplateElementType.Ellipse, "Эллипс");

    [RelayCommand]
    private void AddLine() => AddElementInternal(TemplateElementType.Line, "Линия");

    private void AddElementInternal(
        TemplateElementType type,
        string name,
        string? content = null,
        TextBindingMode binding = TextBindingMode.Literal,
        string? valueBinding = null,
        BarcodeSymbology? symbology = null,
        bool recordUndo = true)
    {
        if (recordUndo)
        {
            RecordUndo();
        }

        var doc = new TemplateElementDocument
        {
            Type = type,
            Name = name,
            Content = content,
            BindingMode = binding,
            ValueBinding = valueBinding,
            Bounds = new TemplateBounds
            {
                X = 2,
                Y = 2,
                Width = type is TemplateElementType.Line ? 40 : 30,
                Height = type is TemplateElementType.Barcode or TemplateElementType.QrCode ? 12 : 8
            },
            Font = new TemplateFont
            {
                Family = "Arial",
                SizePt = 10,
                Bold = type != TemplateElementType.Text || binding == TextBindingMode.Variable
            },
            Symbology = symbology ?? (type == TemplateElementType.Barcode ? BarcodeSymbology.Ean13 : type == TemplateElementType.QrCode ? BarcodeSymbology.QrCode : null),
            StrokeThickness = 0.4
        };

        var vm = CreateElementViewModel(doc);
        Elements.Add(vm);
        SetSelection([vm]);
        RefreshDirtyState();
        RefreshOverflowState();
    }

    private void Align(Action<IList<TemplateAlignmentHelper.MutableBounds>> align)
    {
        if (SelectedElements.Count == 0)
        {
            return;
        }

        RecordUndo();
        var bounds = SelectedElements
            .Where(e => !e.IsLocked)
            .Select(e => new TemplateAlignmentHelper.MutableBounds
            {
                Id = e.Id,
                X = e.XMm,
                Y = e.YMm,
                Width = e.WidthMm,
                Height = e.HeightMm
            })
            .ToList();

        if (bounds.Count == 0)
        {
            return;
        }

        align(bounds);
        foreach (var item in bounds)
        {
            var vm = SelectedElements.First(e => e.Id == item.Id);
            vm.XMm = item.X;
            vm.YMm = item.Y;
        }

        RefreshDirtyState();
        RefreshOverflowState();
    }

    private void SetSelection(IReadOnlyList<CanvasElementViewModel> items)
    {
        foreach (var item in SelectedElements.ToList())
        {
            item.IsSelected = false;
        }

        SelectedElements.Clear();
        foreach (var item in items)
        {
            SelectedElements.Add(item);
            item.IsSelected = true;
        }

        Selected = items.LastOrDefault();
    }

    private void UpdateSnapGuides(CanvasElementViewModel dragged)
    {
        SnapGuides.Clear();
        if (!SnapEnabled)
        {
            return;
        }

        var others = Elements
            .Where(e => e.Id != dragged.Id)
            .Select(e => new TemplateSnapGuides.ElementRect(e.XMm, e.YMm, e.WidthMm, e.HeightMm));

        var guides = TemplateSnapGuides.ComputeGuides(
            new TemplateSnapGuides.ElementRect(dragged.XMm, dragged.YMm, dragged.WidthMm, dragged.HeightMm),
            WidthMm,
            HeightMm,
            PxPerMm,
            Zoom,
            others);

        foreach (var guide in guides)
        {
            SnapGuides.Add(new SnapGuideViewModel(guide.PositionPx, guide.IsVertical));
        }
    }

    private async Task LoadPreviewVariablesAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductService>();
        var resolver = scope.ServiceProvider.GetRequiredService<IVariableResolver>();

        var search = await products.SearchAsync(null, null, includeArchived: false, skip: 0, take: 1);
        VariableContext context;
        if (search.IsSuccess && search.Value.Items.Count > 0)
        {
            var product = search.Value.Items[0];
            context = new VariableContext
            {
                ProductId = product.Id,
                Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };
        }
        else
        {
            context = new VariableContext
            {
                Values = TemplateVariablePalette.KnownVariables
                    .ToDictionary(v => v.Key, v => v.SampleValue, StringComparer.OrdinalIgnoreCase)
            };
        }

        _previewVariables = await resolver.ResolveAllAsync(context);
        RefreshAllPreviewText();
    }

    private void RefreshAllPreviewText()
    {
        foreach (var element in Elements)
        {
            element.UpdatePreviewText(_previewVariables, IsPreviewMode);
        }
    }

    private void OnElementsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshDirtyState();

    private void OnSelectedElementsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnPropertyChanged(nameof(HasMultiSelection));

    private TemplateDocument BuildDocument() => new()
    {
        SchemaVersion = 1,
        Name = Name,
        Canvas = new TemplateCanvas { WidthMm = WidthMm, HeightMm = HeightMm, Dpi = 203 },
        Elements = Elements.Select((e, i) => e.ToDocument(i)).ToList()
    };

    private void MarkClean()
    {
        _cleanSnapshot = CaptureSnapshot();
        HasUnsavedChanges = false;
        _undoStack.Clear();
        UpdateUndoRedoState();
    }

    private void RefreshDirtyState()
    {
        HasUnsavedChanges = !string.Equals(CaptureSnapshot(), _cleanSnapshot, StringComparison.Ordinal);
    }

    private void RefreshOverflowState()
    {
        var rects = Elements.Select(e => new TemplateOverflowChecker.ElementRect(e.Id, e.XMm, e.YMm, e.WidthMm, e.HeightMm));
        var overflowIds = TemplateOverflowChecker.GetOverflowElementIds(rects, WidthMm, HeightMm).ToHashSet();
        foreach (var element in Elements)
        {
            element.IsOverflow = overflowIds.Contains(element.Id);
        }

        var overflowMessage = TemplateOverflowChecker.BuildStatusMessage(rects, WidthMm, HeightMm);
        if (overflowMessage is not null && (StatusMessage is null || !StatusMessage.StartsWith("Шаблон сохранён")))
        {
            StatusMessage = overflowMessage;
        }
        else if (overflowMessage is not null)
        {
            StatusMessage = overflowMessage;
        }
    }

    private string CaptureSnapshot() => TemplateDocumentSerializer.Serialize(BuildDocument());

    private void RecordUndo()
    {
        if (_suppressUndo)
        {
            return;
        }

        _undoStack.Push(CaptureSnapshot());
        UpdateUndoRedoState();
    }

    private void UpdateUndoRedoState()
    {
        CanUndo = _undoStack.CanUndo;
        CanRedo = _undoStack.CanRedo;
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void RestoreFromSnapshot(string snapshot, bool markClean = false)
    {
        _suppressUndo = true;
        try
        {
            var document = TemplateDocumentSerializer.Deserialize(snapshot);
            Name = document.Name ?? string.Empty;
            WidthMm = document.Canvas.WidthMm;
            HeightMm = document.Canvas.HeightMm;

            foreach (var item in Elements.ToList())
            {
                DetachElement(item);
            }

            Elements.Clear();
            ClearSelection();

            foreach (var el in document.Elements.OrderBy(e => e.Z))
            {
                Elements.Add(CreateElementViewModel(el));
            }

            RefreshAllPreviewText();
            RefreshOverflowState();
            if (markClean)
            {
                MarkClean();
            }
            else
            {
                RefreshDirtyState();
            }
        }
        finally
        {
            _suppressUndo = false;
        }
    }

    private CanvasElementViewModel CreateElementViewModel(TemplateElementDocument document)
    {
        var vm = new CanvasElementViewModel(document, () => Zoom);
        AttachElement(vm);
        vm.UpdatePreviewText(_previewVariables, IsPreviewMode);
        return vm;
    }

    private void AttachElement(CanvasElementViewModel vm)
    {
        vm.Changed += OnElementChanged;
    }

    private void DetachElement(CanvasElementViewModel vm)
    {
        vm.Changed -= OnElementChanged;
    }

    private void OnElementChanged()
    {
        RefreshDirtyState();
        RefreshOverflowState();
        if (IsPreviewMode)
        {
            RefreshAllPreviewText();
        }
    }
}

public sealed class SnapGuideViewModel(double positionPx, bool isVertical)
{
    public double PositionPx { get; } = positionPx;

    public bool IsVertical { get; } = isVertical;
}

public sealed class TemplateVariableItemViewModel
{
    private readonly Action<TemplateVariablePalette.VariableDefinition> _insert;

    public TemplateVariableItemViewModel(
        TemplateVariablePalette.VariableDefinition definition,
        Action<TemplateVariablePalette.VariableDefinition> insert)
    {
        Definition = definition;
        _insert = insert;
        InsertCommand = new RelayCommand(() => _insert(definition));
    }

    public TemplateVariablePalette.VariableDefinition Definition { get; }

    public string DisplayName => Definition.DisplayName;

    public string Key => Definition.Key;

    public IRelayCommand InsertCommand { get; }
}

public partial class CanvasElementViewModel : ObservableObject
{
    private readonly Func<double> _zoom;

    public CanvasElementViewModel(TemplateElementDocument document, Func<double> zoom)
    {
        _zoom = zoom;
        Id = document.Id;
        Type = document.Type;
        Name = document.Name ?? document.Type.ToString();
        Content = document.Content;
        BindingMode = document.BindingMode;
        ValueBinding = document.ValueBinding;
        GroupId = document.GroupId;
        XMm = document.Bounds.X;
        YMm = document.Bounds.Y;
        WidthMm = document.Bounds.Width;
        HeightMm = document.Bounds.Height;
        Rotation = document.Rotation;
        FontFamily = document.Font?.Family ?? "Arial";
        FontSizePt = document.Font?.SizePt ?? 10;
        IsBold = document.Font?.Bold ?? false;
        Symbology = document.Symbology;
        StrokeThickness = document.StrokeThickness;
        Filled = document.Filled;
        Invert = document.Invert;
        Dashed = document.Dashed;
        CornerRadiusMm = document.CornerRadiusMm;
        ImagePath = document.ImagePath;
        IsLocked = document.IsLocked;
    }

    public event Action? Changed;

    public string Id { get; }

    public TemplateElementType Type { get; }

    [ObservableProperty] private string _name;
    [ObservableProperty] private string? _content;
    [ObservableProperty] private TextBindingMode _bindingMode;
    [ObservableProperty] private string? _valueBinding;
    [ObservableProperty] private string? _groupId;
    [ObservableProperty] private double _xMm;
    [ObservableProperty] private double _yMm;
    [ObservableProperty] private double _widthMm;
    [ObservableProperty] private double _heightMm;
    [ObservableProperty] private double _rotation;
    [ObservableProperty] private string _fontFamily;
    [ObservableProperty] private double _fontSizePt;
    [ObservableProperty] private bool _isBold;
    [ObservableProperty] private BarcodeSymbology? _symbology;
    [ObservableProperty] private double _strokeThickness;
    [ObservableProperty] private bool _filled;
    [ObservableProperty] private bool _invert;
    [ObservableProperty] private bool _dashed;
    [ObservableProperty] private double _cornerRadiusMm;
    [ObservableProperty] private string? _imagePath;
    [ObservableProperty] private bool _isLocked;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isOverflow;
    [ObservableProperty] private string _previewText = string.Empty;

    private bool _previewModeActive;

    public double LeftPx => XMm * TemplateEditorViewModel.PxPerMm * _zoom();
    public double TopPx => YMm * TemplateEditorViewModel.PxPerMm * _zoom();
    public double WidthPx => WidthMm * TemplateEditorViewModel.PxPerMm * _zoom();
    public double HeightPx => HeightMm * TemplateEditorViewModel.PxPerMm * _zoom();
    public double FontSizePx => FontSizePt * _zoom() * 1.2;

    public string DisplayText =>
        BindingMode == TextBindingMode.Variable && !string.IsNullOrWhiteSpace(ValueBinding)
            ? "{{" + ValueBinding + "}}"
            : (Content ?? Name);

    public bool IsText => Type is TemplateElementType.Text;
    public bool IsBarcode => Type is TemplateElementType.Barcode or TemplateElementType.QrCode;
    public bool IsRectangle => Type is TemplateElementType.Rectangle or TemplateElementType.Line;
    public bool IsEllipse => Type is TemplateElementType.Ellipse;
    public bool IsQrCode => Type is TemplateElementType.QrCode;

    partial void OnXMmChanged(double value) { NotifyScaleChanged(); Changed?.Invoke(); }
    partial void OnYMmChanged(double value) { NotifyScaleChanged(); Changed?.Invoke(); }
    partial void OnWidthMmChanged(double value) { NotifyScaleChanged(); Changed?.Invoke(); }
    partial void OnHeightMmChanged(double value) { NotifyScaleChanged(); Changed?.Invoke(); }
    partial void OnRotationChanged(double value) => Changed?.Invoke();
    partial void OnFontSizePtChanged(double value) { OnPropertyChanged(nameof(FontSizePx)); Changed?.Invoke(); }
    partial void OnContentChanged(string? value)
    {
        OnPropertyChanged(nameof(DisplayText));
        if (!_previewModeActive)
        {
            PreviewText = DisplayText;
        }

        Changed?.Invoke();
    }

    partial void OnValueBindingChanged(string? value)
    {
        OnPropertyChanged(nameof(DisplayText));
        if (!_previewModeActive)
        {
            PreviewText = DisplayText;
        }

        Changed?.Invoke();
    }

    partial void OnBindingModeChanged(TextBindingMode value)
    {
        OnPropertyChanged(nameof(DisplayText));
        if (!_previewModeActive)
        {
            PreviewText = DisplayText;
        }

        Changed?.Invoke();
    }
    partial void OnNameChanged(string value) => Changed?.Invoke();
    partial void OnIsBoldChanged(bool value) => Changed?.Invoke();
    partial void OnIsLockedChanged(bool value) => Changed?.Invoke();
    partial void OnGroupIdChanged(string? value) => Changed?.Invoke();

    public void UpdatePreviewText(IReadOnlyDictionary<string, string> variables, bool previewMode)
    {
        _previewModeActive = previewMode;
        PreviewText = previewMode
            ? TemplatePreviewTextResolver.Resolve(
                new TemplatePreviewTextResolver.CanvasElementSnapshot(Type, BindingMode, Content, ValueBinding),
                variables)
            : DisplayText;
    }

    public void NotifyScaleChanged()
    {
        OnPropertyChanged(nameof(LeftPx));
        OnPropertyChanged(nameof(TopPx));
        OnPropertyChanged(nameof(WidthPx));
        OnPropertyChanged(nameof(HeightPx));
        OnPropertyChanged(nameof(FontSizePx));
    }

    public void MoveByPixels(double dx, double dy, bool snap)
    {
        if (IsLocked)
        {
            return;
        }

        var scale = TemplateEditorViewModel.PxPerMm * _zoom();
        var x = XMm + dx / scale;
        var y = YMm + dy / scale;
        if (snap)
        {
            x = Math.Round(x);
            y = Math.Round(y);
        }

        XMm = Math.Max(0, x);
        YMm = Math.Max(0, y);
    }

    public TemplateElementDocument ToDocument(int z) => new()
    {
        Id = Id,
        Type = Type,
        Name = Name,
        Content = Content,
        BindingMode = BindingMode,
        ValueBinding = ValueBinding,
        GroupId = GroupId,
        Bounds = new TemplateBounds { X = XMm, Y = YMm, Width = WidthMm, Height = HeightMm },
        Rotation = Rotation,
        Z = z,
        IsLocked = IsLocked,
        Font = new TemplateFont { Family = FontFamily, SizePt = FontSizePt, Bold = IsBold },
        Symbology = Symbology,
        StrokeThickness = StrokeThickness,
        Filled = Filled,
        Invert = Invert,
        Dashed = Dashed,
        CornerRadiusMm = CornerRadiusMm,
        ImagePath = ImagePath
    };

    public CanvasElementViewModel Clone(double offsetX, double offsetY)
    {
        var doc = ToDocument(0);
        doc.Id = Guid.NewGuid().ToString("N");
        doc.Bounds.X += offsetX;
        doc.Bounds.Y += offsetY;
        return new CanvasElementViewModel(doc, _zoom);
    }
}

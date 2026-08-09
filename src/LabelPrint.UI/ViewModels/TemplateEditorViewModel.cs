using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Media.Imaging;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Icons;
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
    /// <summary>Editor canvas scale (~203 dpi: 203/25.4 ≈ 8).</summary>
    public const double PxPerMm = 8;

    /// <summary>
    /// Convert template font points to editor pixels so preview matches Skia print
    /// (<c>sizePt * dpi / 72</c> at the same mm scale as <see cref="PxPerMm"/>).
    /// </summary>
    public static double FontSizePtToPx(double sizePt, double zoom) =>
        sizePt * (25.4 / 72.0) * PxPerMm * zoom;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUiDialogService _dialogs;
    private readonly Func<Task> _navigateBackAsync;
    private readonly Guid _templateId;
    private readonly EditorUndoStack _undoStack = new();
    private string _cleanSnapshot = string.Empty;
    private bool _suppressUndo;
    private string _dragSnapshot = string.Empty;
    private IReadOnlyDictionary<string, string> _previewVariables = new Dictionary<string, string>();
    private CancellationTokenSource? _printPreviewCts;

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

        foreach (var family in BuildAvailableFontFamilies())
        {
            AvailableFontFamilies.Add(family);
        }
    }

    public ObservableCollection<CanvasElementViewModel> Elements { get; } = new();

    public ObservableCollection<CanvasElementViewModel> SelectedElements { get; } = new();

    public ObservableCollection<TemplateVariableItemViewModel> VariableDefinitions { get; } = new();

    public ObservableCollection<SnapGuideViewModel> SnapGuides { get; } = new();

    public ObservableCollection<string> AvailableFontFamilies { get; } = new();

    public ObservableCollection<string> IconKeys { get; } = new();

    public IReadOnlyList<string> IconColorOptions { get; } = ["Чёрный", "Белый"];

    public IReadOnlyList<string> BindingModeOptions { get; } = ["Текст", "Переменная"];

    public IReadOnlyList<string> VariableKeyOptions { get; } =
        TemplateVariablePalette.KnownVariables.Select(v => v.Key).ToArray();

    private static IEnumerable<string> BuildAvailableFontFamilies()
    {
        var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Inter",
            "Arial",
            "Segoe UI",
            "Times New Roman",
            "Courier New",
            "Consolas",
            "Verdana",
            "Tahoma",
            "Georgia",
            "Comic Sans MS",
            "Impact",
            "Roboto",
            "Calibri"
        };

        try
        {
            foreach (var font in Avalonia.Media.FontManager.Current.SystemFonts)
            {
                if (!string.IsNullOrWhiteSpace(font.Name))
                {
                    set.Add(font.Name);
                }
            }
        }
        catch
        {
            // System font enumeration is best-effort; curated list remains.
        }

        return set;
    }
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
    [ObservableProperty] private Bitmap? _printPreviewBitmap;
    [ObservableProperty] private CanvasElementViewModel? _editingAddonsKitchen;
    [ObservableProperty] private AddonsKitchenPartViewModel? _selectedInnerPart;
    [ObservableProperty] private bool _isolationPreviewEmpty;

    public ObservableCollection<AddonsKitchenPartViewModel> InnerParts { get; } = new();

    public bool IsEditingAddonsKitchen => EditingAddonsKitchen is not null;

    public bool ShowOuterToolbar => !IsEditingAddonsKitchen;

    public bool IsolationPreviewList => !IsolationPreviewEmpty;

    public string IsolationBreadcrumb =>
        EditingAddonsKitchen is null
            ? string.Empty
            : $"Шаблон › {EditingAddonsKitchen.Name}";

    public string IsolationModeHint =>
        IsolationPreviewEmpty
            ? "Режим «нет добавок» — текст и картинки пустого состояния"
            : "Режим «есть добавки» — заголовок и шаблон строки";

    public double CanvasWidthPx => WidthMm * PxPerMm * Zoom;
    public double CanvasHeightPx => HeightMm * PxPerMm * Zoom;

    /// <summary>True while editing vector chrome (not Skia print preview).</summary>
    public bool IsDesignMode => !IsPreviewMode;

    public bool HasMultiSelection => SelectedElements.Count > 1;

    public bool HasSelectedInnerPart => SelectedInnerPart is not null;

    public bool CanDeleteSelectedInnerPart => SelectedInnerPart?.CanDelete == true;

    public double OuterCanvasOpacity => IsEditingAddonsKitchen ? 0.12 : 1.0;

    public double IsolationFrameLeftPx => EditingAddonsKitchen?.LeftPx ?? 0;
    public double IsolationFrameTopPx => EditingAddonsKitchen?.TopPx ?? 0;
    public double IsolationFrameWidthPx => EditingAddonsKitchen?.WidthPx ?? 0;
    public double IsolationFrameHeightPx => EditingAddonsKitchen?.HeightPx ?? 0;

    partial void OnWidthMmChanged(double value)
    {
        OnPropertyChanged(nameof(CanvasWidthPx));
        RefreshOverflowState();
        RefreshDirtyState();
        SchedulePrintPreviewRefresh();
    }

    partial void OnHeightMmChanged(double value)
    {
        OnPropertyChanged(nameof(CanvasHeightPx));
        RefreshOverflowState();
        RefreshDirtyState();
        SchedulePrintPreviewRefresh();
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

        foreach (var part in InnerParts)
        {
            part.NotifyScaleChanged();
        }

        NotifyIsolationFrame();
    }

    partial void OnEditingAddonsKitchenChanged(CanvasElementViewModel? value)
    {
        OnPropertyChanged(nameof(IsEditingAddonsKitchen));
        OnPropertyChanged(nameof(ShowOuterToolbar));
        OnPropertyChanged(nameof(IsolationBreadcrumb));
        OnPropertyChanged(nameof(OuterCanvasOpacity));
        NotifyIsolationFrame();
    }

    partial void OnSelectedInnerPartChanged(AddonsKitchenPartViewModel? value)
    {
        if (value is not null && !value.IsActiveInPreview)
        {
            IsolationPreviewEmpty = value.IsEmptyContent;
        }

        foreach (var item in InnerParts)
        {
            item.IsSelected = ReferenceEquals(item, value);
        }

        OnPropertyChanged(nameof(HasSelectedInnerPart));
        OnPropertyChanged(nameof(CanDeleteSelectedInnerPart));
        DeleteSelectedInnerPartCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsolationPreviewEmptyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsolationPreviewList));
        OnPropertyChanged(nameof(IsolationModeHint));
        foreach (var part in InnerParts)
        {
            part.NotifyPreviewModeChanged();
        }

        if (SelectedInnerPart is { } selected && !selected.IsActiveInPreview)
        {
            var next = InnerParts.FirstOrDefault(p => p.IsActiveInPreview);
            if (next is not null)
            {
                SelectInnerPart(next);
            }
            else
            {
                SelectedInnerPart = null;
            }
        }
    }

    private void NotifyIsolationFrame()
    {
        OnPropertyChanged(nameof(IsolationFrameLeftPx));
        OnPropertyChanged(nameof(IsolationFrameTopPx));
        OnPropertyChanged(nameof(IsolationFrameWidthPx));
        OnPropertyChanged(nameof(IsolationFrameHeightPx));
    }

    partial void OnIsPreviewModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsDesignMode));
        if (value)
        {
            _ = LoadPreviewVariablesAsync();
        }
        else
        {
            CancelPrintPreview();
            ClearPrintPreviewBitmap();
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

        await LoadIconKeysAsync();
        await LoadPreviewVariablesAsync();

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
    private void AlignLeft()
    {
        if (TrySetTextHorizontalAlign(TextHorizontalAlign.Left))
        {
            return;
        }

        Align(bounds =>
        {
            if (bounds.Count == 1)
            {
                TemplateAlignmentHelper.AlignToCanvasLeft(bounds);
            }
            else
            {
                TemplateAlignmentHelper.AlignLeft(bounds);
            }
        });
    }

    [RelayCommand]
    private void AlignCenterHorizontal()
    {
        if (TrySetTextHorizontalAlign(TextHorizontalAlign.Center))
        {
            return;
        }

        Align(bounds =>
        {
            if (bounds.Count == 1)
            {
                TemplateAlignmentHelper.AlignToCanvasCenterHorizontal(bounds, WidthMm);
            }
            else
            {
                TemplateAlignmentHelper.AlignCenterHorizontal(bounds);
            }
        });
    }

    [RelayCommand]
    private void AlignRight()
    {
        if (TrySetTextHorizontalAlign(TextHorizontalAlign.Right))
        {
            return;
        }

        Align(bounds =>
        {
            if (bounds.Count == 1)
            {
                TemplateAlignmentHelper.AlignToCanvasRight(bounds, WidthMm);
            }
            else
            {
                TemplateAlignmentHelper.AlignRight(bounds);
            }
        });
    }

    [RelayCommand]
    private void AlignTop()
    {
        if (TrySetTextVerticalAlign(TextVerticalAlign.Top))
        {
            return;
        }

        Align(bounds =>
        {
            if (bounds.Count == 1)
            {
                TemplateAlignmentHelper.AlignToCanvasTop(bounds);
            }
            else
            {
                TemplateAlignmentHelper.AlignTop(bounds);
            }
        });
    }

    [RelayCommand]
    private void AlignCenterVertical()
    {
        if (TrySetTextVerticalAlign(TextVerticalAlign.Middle))
        {
            return;
        }

        Align(bounds =>
        {
            if (bounds.Count == 1)
            {
                TemplateAlignmentHelper.AlignToCanvasCenterVertical(bounds, HeightMm);
            }
            else
            {
                TemplateAlignmentHelper.AlignCenterVertical(bounds);
            }
        });
    }

    [RelayCommand]
    private void AlignBottom()
    {
        if (TrySetTextVerticalAlign(TextVerticalAlign.Bottom))
        {
            return;
        }

        Align(bounds =>
        {
            if (bounds.Count == 1)
            {
                TemplateAlignmentHelper.AlignToCanvasBottom(bounds, HeightMm);
            }
            else
            {
                TemplateAlignmentHelper.AlignBottom(bounds);
            }
        });
    }

    private bool TrySetTextHorizontalAlign(TextHorizontalAlign align)
    {
        if (IsEditingAddonsKitchen)
        {
            if (SelectedInnerPart is not { IsTextPart: true })
            {
                StatusMessage = "Выберите текстовую часть для выравнивания";
                return true;
            }

            if (SelectedInnerPart.HorizontalAlign == align)
            {
                return true;
            }

            RecordUndo();
            SelectedInnerPart.HorizontalAlign = align;
            PersistInnerPartsToElement();
            RefreshDirtyState();
            SchedulePrintPreviewRefresh();
            return true;
        }

        if (SelectedElements.Count != 1 || Selected is not { IsText: true, IsLocked: false })
        {
            return false;
        }

        RecordUndo();
        Selected.HorizontalAlign = align;
        RefreshDirtyState();
        return true;
    }

    private bool TrySetTextVerticalAlign(TextVerticalAlign align)
    {
        if (IsEditingAddonsKitchen)
        {
            if (SelectedInnerPart is not { IsTextPart: true })
            {
                StatusMessage = "Выберите текстовую часть для выравнивания";
                return true;
            }

            if (SelectedInnerPart.VerticalAlign == align)
            {
                return true;
            }

            RecordUndo();
            SelectedInnerPart.VerticalAlign = align;
            PersistInnerPartsToElement();
            RefreshDirtyState();
            SchedulePrintPreviewRefresh();
            return true;
        }

        if (SelectedElements.Count != 1 || Selected is not { IsText: true, IsLocked: false })
        {
            return false;
        }

        RecordUndo();
        Selected.VerticalAlign = align;
        RefreshDirtyState();
        return true;
    }

    public void SelectElement(CanvasElementViewModel element, bool addToSelection)
    {
        if (IsEditingAddonsKitchen)
        {
            return;
        }

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

    public void TryEnterAddonsKitchen(CanvasElementViewModel element)
    {
        if (!element.IsAddonsKitchen || IsPreviewMode)
        {
            return;
        }

        EnterAddonsKitchenEdit(element);
    }

    [RelayCommand]
    private void ExitAddonsKitchenEdit()
    {
        if (EditingAddonsKitchen is null)
        {
            return;
        }

        PersistInnerPartsToElement();
        ClearInnerParts();
        EditingAddonsKitchen = null;
        SelectedInnerPart = null;
        IsolationPreviewEmpty = false;
        RefreshDirtyState();
        SchedulePrintPreviewRefresh();
        StatusMessage = "Вышли из блока добавок";
    }

    private void EnterAddonsKitchenEdit(CanvasElementViewModel element)
    {
        if (EditingAddonsKitchen is not null)
        {
            PersistInnerPartsToElement();
            ClearInnerParts();
        }

        RecordUndo();
        ClearSelection();
        SetSelection([element]);
        IsolationPreviewEmpty = false;
        EditingAddonsKitchen = element;
        var layout = element.EnsureAddonsKitchenLayout();
        BuildInnerParts(layout);
        var first = InnerParts.FirstOrDefault(p => p.IsActiveInPreview);
        if (first is not null)
        {
            SelectInnerPart(first);
        }

        StatusMessage = "Редактирование блока добавок — Esc или «← К шаблону» для выхода";
    }

    private void BuildInnerParts(AddonsKitchenLayout layout)
    {
        ClearInnerParts();
        void Add(AddonsKitchenPartKind kind, AddonsKitchenPart? part, bool rowRelative, string? name = null)
        {
            if (part is null)
            {
                return;
            }

            var vm = new AddonsKitchenPartViewModel(
                kind,
                part,
                rowRelative,
                () => Zoom,
                () => EditingAddonsKitchen,
                () => IsolationPreviewEmpty,
                name);
            vm.Changed += OnInnerPartChanged;
            InnerParts.Add(vm);
        }

        Add(AddonsKitchenPartKind.Title, layout.Title, false);
        Add(AddonsKitchenPartKind.Underline, layout.Underline, false);
        Add(AddonsKitchenPartKind.Icon, layout.Icon, true);
        Add(AddonsKitchenPartKind.Text, layout.Text, true);
        Add(AddonsKitchenPartKind.Separator, layout.Separator, true);

        var emptyIndex = 1;
        foreach (var empty in layout.EmptyElements)
        {
            var isImage = AddonsKitchenLayoutDefaults.IsImagePart(empty);
            var kind = isImage ? AddonsKitchenPartKind.EmptyImage : AddonsKitchenPartKind.EmptyText;
            var label = isImage
                ? $"Картинка {emptyIndex}"
                : $"Текст {emptyIndex}";
            Add(kind, empty, false, $"{label} (нет добавок)");
            emptyIndex++;
        }
    }

    private void ClearInnerParts()
    {
        foreach (var part in InnerParts)
        {
            part.Changed -= OnInnerPartChanged;
            part.IsSelected = false;
        }

        InnerParts.Clear();
    }

    private void OnInnerPartChanged()
    {
        if (EditingAddonsKitchen is null)
        {
            return;
        }

        PersistInnerPartsToElement();
        foreach (var part in InnerParts)
        {
            part.NotifyScaleChanged();
        }

        NotifyIsolationFrame();
        RefreshDirtyState();
        SchedulePrintPreviewRefresh();
    }

    private void PersistInnerPartsToElement()
    {
        var parent = EditingAddonsKitchen;
        if (parent is null)
        {
            return;
        }

        var layout = parent.EnsureAddonsKitchenLayout();
        var emptyElements = new List<AddonsKitchenPart>();
        foreach (var part in InnerParts)
        {
            switch (part.Kind)
            {
                case AddonsKitchenPartKind.Title:
                    layout.Title = part.ToPart();
                    break;
                case AddonsKitchenPartKind.Underline:
                    layout.Underline = part.ToPart();
                    break;
                case AddonsKitchenPartKind.Icon:
                    layout.Icon = part.ToPart();
                    layout.RowHeightMm = Math.Max(layout.RowHeightMm, part.YMm + part.HeightMm);
                    break;
                case AddonsKitchenPartKind.Text:
                    layout.Text = part.ToPart();
                    layout.RowHeightMm = Math.Max(layout.RowHeightMm, part.YMm + part.HeightMm);
                    break;
                case AddonsKitchenPartKind.Separator:
                    layout.Separator = part.ToPart();
                    break;
                case AddonsKitchenPartKind.EmptyText:
                case AddonsKitchenPartKind.EmptyImage:
                    emptyElements.Add(part.ToPart());
                    break;
            }
        }

        layout.Empty = null;
        layout.EmptyElements = emptyElements;
        parent.AddonsKitchenLayout = layout;
    }

    public void SelectInnerPart(AddonsKitchenPartViewModel part)
    {
        foreach (var item in InnerParts)
        {
            item.IsSelected = false;
        }

        part.IsSelected = true;
        SelectedInnerPart = part;
    }

    [RelayCommand]
    private void SetIsolationPreviewList() => IsolationPreviewEmpty = false;

    [RelayCommand]
    private void SetIsolationPreviewEmpty() => IsolationPreviewEmpty = true;

    [RelayCommand]
    private void AddEmptyTextPart()
    {
        if (EditingAddonsKitchen is null)
        {
            return;
        }

        RecordUndo();
        IsolationPreviewEmpty = true;
        var parent = EditingAddonsKitchen;
        var n = InnerParts.Count(p => p.IsEmptyContent) + 1;
        var part = new AddonsKitchenPart
        {
            Visible = true,
            PartType = AddonsKitchenLayoutDefaults.PartTypeText,
            Content = AddonsKitchenLayoutDefaults.DefaultEmptyText,
            Bounds = new TemplateBounds { X = 1, Y = 1 + (n - 1) * 4, Width = Math.Max(10, parent.WidthMm - 2), Height = 3.5 },
            Font = new TemplateFont { Family = parent.FontFamily, SizePt = Math.Max(7, parent.FontSizePt - 0.5), Bold = parent.IsBold }
        };
        var vm = new AddonsKitchenPartViewModel(
            AddonsKitchenPartKind.EmptyText,
            part,
            false,
            () => Zoom,
            () => EditingAddonsKitchen,
            () => IsolationPreviewEmpty,
            $"Текст {n} (нет добавок)");
        vm.Changed += OnInnerPartChanged;
        InnerParts.Add(vm);
        SelectInnerPart(vm);
        PersistInnerPartsToElement();
        RefreshDirtyState();
    }

    [RelayCommand]
    private void AddEmptyImagePart()
    {
        if (EditingAddonsKitchen is null)
        {
            return;
        }

        RecordUndo();
        IsolationPreviewEmpty = true;
        var parent = EditingAddonsKitchen;
        var n = InnerParts.Count(p => p.IsEmptyContent) + 1;
        var size = Math.Min(8, Math.Max(3.2, parent.WidthMm / 4));
        var part = new AddonsKitchenPart
        {
            Visible = true,
            PartType = AddonsKitchenLayoutDefaults.PartTypeImage,
            ImagePath = IconKeys.FirstOrDefault(),
            Bounds = new TemplateBounds { X = 1, Y = 1 + (n - 1) * (size + 1), Width = size, Height = size }
        };
        var vm = new AddonsKitchenPartViewModel(
            AddonsKitchenPartKind.EmptyImage,
            part,
            false,
            () => Zoom,
            () => EditingAddonsKitchen,
            () => IsolationPreviewEmpty,
            $"Картинка {n} (нет добавок)");
        vm.Changed += OnInnerPartChanged;
        InnerParts.Add(vm);
        SelectInnerPart(vm);
        PersistInnerPartsToElement();
        RefreshDirtyState();
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelectedInnerPart))]
    private void DeleteSelectedInnerPart()
    {
        if (SelectedInnerPart is not { CanDelete: true } part)
        {
            return;
        }

        RecordUndo();
        part.Changed -= OnInnerPartChanged;
        InnerParts.Remove(part);
        SelectedInnerPart = InnerParts.FirstOrDefault(p => p.IsActiveInPreview);
        if (SelectedInnerPart is not null)
        {
            SelectedInnerPart.IsSelected = true;
        }

        PersistInnerPartsToElement();
        RefreshDirtyState();
        OnPropertyChanged(nameof(CanDeleteSelectedInnerPart));
    }

    public void BeginInnerDrag(AddonsKitchenPartViewModel part)
    {
        _dragSnapshot = CaptureSnapshot();
        SelectInnerPart(part);
    }

    public void DragMoveInner(AddonsKitchenPartViewModel part, double dx, double dy)
    {
        var parent = EditingAddonsKitchen;
        if (parent is null)
        {
            return;
        }

        part.MoveByPixels(dx, dy, SnapEnabled, parent.WidthMm, parent.HeightMm, parent.AddonsKitchenRowHeightMm);
        NotifyIsolationFrame();
    }

    public void EndInnerDrag()
    {
        if (!_dragSnapshot.Equals(CaptureSnapshot(), StringComparison.Ordinal))
        {
            PersistInnerPartsToElement();
            _undoStack.Push(_dragSnapshot);
            UpdateUndoRedoState();
            RefreshDirtyState();
            SchedulePrintPreviewRefresh();
        }

        _dragSnapshot = string.Empty;
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

    [RelayCommand]
    private void AddImage() => AddElementInternal(
        TemplateElementType.Image,
        "Иконка",
        binding: TextBindingMode.Variable,
        valueBinding: "ProductIconKey");

    [RelayCommand]
    private void AddAddonsKitchen()
    {
        RecordUndo();
        var doc = new TemplateElementDocument
        {
            Type = TemplateElementType.Text,
            Name = "Добавки (кухня)",
            BindingMode = TextBindingMode.Variable,
            ValueBinding = "AddonsKitchen",
            Content = "{{AddonsKitchen}}",
            Bounds = new TemplateBounds { X = 1.5, Y = 27, Width = 37, Height = 22 },
            Font = new TemplateFont { Family = "Inter", SizePt = 8, Bold = true },
            AddonsKitchen = AddonsKitchenLayoutDefaults.Create(
                new TemplateFont { Family = "Inter", SizePt = 8, Bold = true },
                37)
        };
        var vm = CreateElementViewModel(doc);
        Elements.Add(vm);
        SetSelection([vm]);
        RefreshDirtyState();
        RefreshOverflowState();
        SchedulePrintPreviewRefresh();
    }

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

        var isLine = type is TemplateElementType.Line;
        var isImage = type is TemplateElementType.Image;
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
                Width = isLine ? 40 : isImage ? 10 : 30,
                // Height 0 = horizontal line in Skia (diagonal uses height as rise).
                Height = isLine
                    ? 0
                    : type is TemplateElementType.Barcode or TemplateElementType.QrCode
                        ? 12
                        : isImage ? 10 : 8
            },
            Font = new TemplateFont
            {
                Family = "Arial",
                SizePt = 10,
                Bold = type != TemplateElementType.Text || binding == TextBindingMode.Variable
            },
            Symbology = symbology ?? (type == TemplateElementType.Barcode ? BarcodeSymbology.Ean13 : type == TemplateElementType.QrCode ? BarcodeSymbology.QrCode : null),
            StrokeThickness = isLine ? 0.28 : 0.4,
            Dashed = false
        };

        var vm = CreateElementViewModel(doc);
        Elements.Add(vm);
        SetSelection([vm]);
        RefreshDirtyState();
        RefreshOverflowState();
        SchedulePrintPreviewRefresh();
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

    private void EnsureFontFamilyListed(string? family)
    {
        if (string.IsNullOrWhiteSpace(family))
        {
            return;
        }

        if (AvailableFontFamilies.Any(f => f.Equals(family, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var insertAt = AvailableFontFamilies
            .TakeWhile(f => string.Compare(f, family, StringComparison.OrdinalIgnoreCase) < 0)
            .Count();
        AvailableFontFamilies.Insert(insertAt, family);
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

    private Task LoadIconKeysAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var addons = scope.ServiceProvider.GetRequiredService<IAddonService>();
        IconKeys.Clear();
        var hidden = HiddenIconStore.GetHidden();
        foreach (var key in addons.BuiltInIconKeys)
        {
            if (!hidden.Contains(key))
            {
                IconKeys.Add(key);
            }
        }

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelPrintPro",
            "addon-icons");
        if (Directory.Exists(dir))
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.png"))
            {
                var stem = Path.GetFileNameWithoutExtension(file);
                if (!string.IsNullOrWhiteSpace(stem)
                    && !hidden.Contains(stem)
                    && !IconKeys.Contains(stem, StringComparer.OrdinalIgnoreCase))
                {
                    IconKeys.Add(stem);
                }
            }
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ImportTemplateIconAsync()
    {
        var path = await _dialogs.PickPngFileAsync();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelPrintPro",
            "addon-icons");
        Directory.CreateDirectory(dir);

        var stem = Path.GetFileNameWithoutExtension(path);
        stem = string.Join("_", stem.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(stem))
        {
            stem = Guid.NewGuid().ToString("N")[..8];
        }

        File.Copy(path, Path.Combine(dir, $"{stem}.png"), overwrite: true);
        HiddenIconStore.Unhide(stem);
        if (!IconKeys.Contains(stem, StringComparer.OrdinalIgnoreCase))
        {
            IconKeys.Add(stem);
        }

        if (SelectedInnerPart is { ShowsImagePicker: true } innerImage)
        {
            RecordUndo();
            innerImage.ImagePath = stem;
        }
        else if (Selected is { IsImage: true } image)
        {
            RecordUndo();
            image.BindingMode = TextBindingMode.Literal;
            image.ValueBinding = null;
            image.ImagePath = stem;
        }

        StatusMessage = $"Иконка импортирована: {stem}.png";
        SchedulePrintPreviewRefresh();
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
        EnsureAddonPreviewIcons();
        RefreshAllPreviewText();
        if (IsPreviewMode)
        {
            await RenderPrintPreviewAsync();
        }
    }

    private void EnsureAddonPreviewIcons()
    {
        var map = new Dictionary<string, string>(_previewVariables, StringComparer.OrdinalIgnoreCase);
        if (!map.ContainsKey("AddonsKitchen") && map.TryGetValue("Addons", out var addons))
        {
            map["AddonsKitchen"] = addons;
        }

        if (!map.ContainsKey("AddonsKitchen"))
        {
            var sample = TemplateVariablePalette.KnownVariables
                .FirstOrDefault(v => v.Key == "AddonsKitchen")?.SampleValue
                ?? "Добавить халапеньо\nДвойной сыр\nБез лука";
            map["AddonsKitchen"] = sample;
            map["Addons"] = sample;
        }

        if (!map.ContainsKey("AddonIconKeys") || string.IsNullOrWhiteSpace(map["AddonIconKeys"]))
        {
            var lines = map["AddonsKitchen"]
                .Replace("\r\n", "\n")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var keys = IconKeys.Take(Math.Max(1, lines.Length)).ToList();
            while (keys.Count < lines.Length)
            {
                keys.Add(keys.Count > 0 ? keys[^1] : "sample");
            }

            map["AddonIconKeys"] = string.Join("\n", keys);
        }

        _previewVariables = map;
    }

    private void CancelPrintPreview()
    {
        _printPreviewCts?.Cancel();
        _printPreviewCts?.Dispose();
        _printPreviewCts = null;
    }

    private void ClearPrintPreviewBitmap()
    {
        var old = PrintPreviewBitmap;
        PrintPreviewBitmap = null;
        old?.Dispose();
    }

    private void SchedulePrintPreviewRefresh()
    {
        if (!IsPreviewMode)
        {
            return;
        }

        CancelPrintPreview();
        _printPreviewCts = new CancellationTokenSource();
        var token = _printPreviewCts.Token;
        _ = DebouncedRenderPrintPreviewAsync(token);
    }

    private async Task DebouncedRenderPrintPreviewAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(180, cancellationToken);
            await RenderPrintPreviewAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Newer edit superseded this render.
        }
    }

    private async Task RenderPrintPreviewAsync(CancellationToken cancellationToken = default)
    {
        if (!IsPreviewMode)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var render = scope.ServiceProvider.GetRequiredService<ILabelRenderService>();
            var document = BuildDocument();
            var result = await render.RenderAsync(document, _previewVariables, cancellationToken);
            if (cancellationToken.IsCancellationRequested || !IsPreviewMode)
            {
                return;
            }

            await using var stream = new MemoryStream(result.Payload);
            var bitmap = new Bitmap(stream);
            var previous = PrintPreviewBitmap;
            PrintPreviewBitmap = bitmap;
            previous?.Dispose();
        }
        catch (OperationCanceledException)
        {
            // Ignore cancelled preview.
        }
        catch (Exception ex)
        {
            StatusMessage = $"Превью печати: {ex.Message}";
        }
    }

    private void RefreshAllPreviewText()
    {
        foreach (var element in Elements)
        {
            element.UpdatePreviewText(_previewVariables, IsPreviewMode);
        }
    }

    private void OnElementsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshDirtyState();
        SchedulePrintPreviewRefresh();
    }

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
            ClearInnerParts();
            EditingAddonsKitchen = null;
            SelectedInnerPart = null;

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
                EnsureFontFamilyListed(el.Font?.Family);
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
            SchedulePrintPreviewRefresh();
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
        HorizontalAlign = document.Font?.HorizontalAlign ?? TextHorizontalAlign.Left;
        VerticalAlign = document.Font?.VerticalAlign ?? TextVerticalAlign.Top;
        Symbology = document.Symbology;
        StrokeThickness = document.StrokeThickness;
        Filled = document.Filled;
        Invert = document.Invert;
        Dashed = document.Dashed;
        CornerRadiusMm = document.CornerRadiusMm;
        ImagePath = document.ImagePath;
        IsLocked = document.IsLocked;
        if (document.AddonsKitchen is not null)
        {
            AddonsKitchenLayout = AddonsKitchenLayoutDefaults.Clone(document.AddonsKitchen);
        }
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
    [ObservableProperty] private TextHorizontalAlign _horizontalAlign = TextHorizontalAlign.Left;
    [ObservableProperty] private TextVerticalAlign _verticalAlign = TextVerticalAlign.Top;
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

    /// <summary>Persisted inner layout for AddonsKitchen blocks (null until edited / ensured).</summary>
    public AddonsKitchenLayout? AddonsKitchenLayout { get; set; }

    public double AddonsKitchenRowsOriginYMm =>
        AddonsKitchenLayout?.RowsOriginYMm
        ?? AddonsKitchenLayoutDefaults.Create(
            new TemplateFont { Family = FontFamily, SizePt = FontSizePt, Bold = IsBold },
            WidthMm).RowsOriginYMm;

    public double AddonsKitchenRowHeightMm =>
        AddonsKitchenLayout?.RowHeightMm
        ?? AddonsKitchenLayoutDefaults.Create(
            new TemplateFont { Family = FontFamily, SizePt = FontSizePt, Bold = IsBold },
            WidthMm).RowHeightMm;

    public AddonsKitchenLayout EnsureAddonsKitchenLayout()
    {
        AddonsKitchenLayout = AddonsKitchenLayoutDefaults.Resolve(
            AddonsKitchenLayout,
            new TemplateFont { Family = FontFamily, SizePt = FontSizePt, Bold = IsBold },
            WidthMm);
        return AddonsKitchenLayout;
    }

    private bool _previewModeActive;

    public double LeftPx => XMm * TemplateEditorViewModel.PxPerMm * _zoom();
    public double TopPx => YMm * TemplateEditorViewModel.PxPerMm * _zoom();

    /// <summary>
    /// Visual / rotate box. For lines this matches Skia segment bounds (zero axis → stroke thickness)
    /// so rotation around center stays on the segment midpoint.
    /// </summary>
    public double WidthPx
    {
        get
        {
            var scale = TemplateEditorViewModel.PxPerMm * _zoom();
            var w = Math.Abs(WidthMm * scale);
            if (Type is TemplateElementType.Line)
            {
                return w < 0.001 ? LineStrokeThicknessPx : w;
            }

            return Math.Max(w, 1);
        }
    }

    public double HeightPx
    {
        get
        {
            var scale = TemplateEditorViewModel.PxPerMm * _zoom();
            var h = Math.Abs(HeightMm * scale);
            if (Type is TemplateElementType.Line)
            {
                return h < 0.001 ? LineStrokeThicknessPx : h;
            }

            return Math.Max(h, 1);
        }
    }

    /// <summary>Larger transparent hit area so thin lines stay easy to select.</summary>
    public double HitWidthPx => Type is TemplateElementType.Line ? Math.Max(WidthPx, 8) : WidthPx;

    public double HitHeightPx => Type is TemplateElementType.Line ? Math.Max(HeightPx, 8) : HeightPx;

    public double FontSizePx => TemplateEditorViewModel.FontSizePtToPx(FontSizePt, _zoom());
    public double SizeMinimumMm => Type is TemplateElementType.Line ? 0 : 1;

    /// <summary>
    /// Line endpoints inside the geom box. Axis-aligned zero-height/width segments are centered
    /// on the stroke so half the stroke is not clipped when rotating.
    /// </summary>
    public Avalonia.Point LineStartPoint
    {
        get
        {
            var stroke = LineStrokeThicknessPx;
            if (Math.Abs(HeightMm) < 0.0001)
            {
                return new Avalonia.Point(0, stroke * 0.5);
            }

            if (Math.Abs(WidthMm) < 0.0001)
            {
                return new Avalonia.Point(stroke * 0.5, 0);
            }

            return default;
        }
    }

    public Avalonia.Point LineEndPoint
    {
        get
        {
            var scale = TemplateEditorViewModel.PxPerMm * _zoom();
            var w = WidthMm * scale;
            var h = HeightMm * scale;
            var stroke = LineStrokeThicknessPx;
            if (Math.Abs(HeightMm) < 0.0001)
            {
                return new Avalonia.Point(w, stroke * 0.5);
            }

            if (Math.Abs(WidthMm) < 0.0001)
            {
                return new Avalonia.Point(stroke * 0.5, h);
            }

            return new Avalonia.Point(w, h);
        }
    }

    public double LineStrokeThicknessPx =>
        Math.Max(1, StrokeThickness * TemplateEditorViewModel.PxPerMm * _zoom());

    public Avalonia.Collections.AvaloniaList<double>? LineDashArray =>
        Dashed ? [4, 3] : null;

    /// <summary>Always center — line geom box equals the segment, matching Skia midpoint pivot.</summary>
    public Avalonia.RelativePoint RotateOrigin => Avalonia.RelativePoint.Center;

    public string DisplayText =>
        BindingMode == TextBindingMode.Variable && !string.IsNullOrWhiteSpace(ValueBinding)
            ? "{{" + ValueBinding + "}}"
            : Type is TemplateElementType.Image
                ? (ImagePath ?? ValueBinding ?? "Иконка")
                : (Content ?? Name);

    public bool IsText => Type is TemplateElementType.Text;
    public bool IsBarcode => Type is TemplateElementType.Barcode or TemplateElementType.QrCode;
    public bool IsRectangleShape => Type is TemplateElementType.Rectangle;
    public bool IsLine => Type is TemplateElementType.Line;
    public bool IsEllipse => Type is TemplateElementType.Ellipse;
    public bool IsImage => Type is TemplateElementType.Image;
    public bool IsQrCode => Type is TemplateElementType.QrCode;
    public bool IsShape => Type is TemplateElementType.Rectangle or TemplateElementType.Ellipse or TemplateElementType.Line;
    public bool IsLineSolid => IsLine && !Dashed;
    public bool IsLineDashed => IsLine && Dashed;
    public bool IsAddonsKitchen =>
        IsText
        && BindingMode == TextBindingMode.Variable
        && string.Equals(ValueBinding, "AddonsKitchen", StringComparison.OrdinalIgnoreCase);

    public bool IsVariableBinding => BindingMode == TextBindingMode.Variable;
    public bool IsLiteralBinding => BindingMode != TextBindingMode.Variable;
    public bool ShowsLiteralContent => IsText && IsLiteralBinding;
    public bool ShowsVariablePicker => (IsText || IsBarcode) && IsVariableBinding;

    public string BindingModeLabel
    {
        get => BindingMode == TextBindingMode.Variable ? "Переменная" : "Текст";
        set
        {
            var next = string.Equals(value, "Переменная", StringComparison.OrdinalIgnoreCase)
                ? TextBindingMode.Variable
                : TextBindingMode.Literal;
            if (BindingMode == next)
            {
                return;
            }

            BindingMode = next;
            if (next == TextBindingMode.Variable && string.IsNullOrWhiteSpace(ValueBinding))
            {
                ValueBinding = "ProductName";
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsVariableBinding));
            OnPropertyChanged(nameof(IsLiteralBinding));
            OnPropertyChanged(nameof(ShowsLiteralContent));
            OnPropertyChanged(nameof(ShowsVariablePicker));
            OnPropertyChanged(nameof(IsAddonsKitchen));
        }
    }

    public Avalonia.Media.IBrush EditorChromeBrush =>
        IsLine
            ? Avalonia.Media.Brushes.Transparent
            : Invert
                ? Avalonia.Media.Brushes.Black
                : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(0x18, 0, 0, 0));

    public double EditorBorderThickness => IsLine ? 0 : 1;

    public Avalonia.Media.IBrush EditorTextBrush =>
        Invert ? Avalonia.Media.Brushes.White : Avalonia.Media.Brushes.Black;

    public Avalonia.Media.IBrush EditorShapeFill =>
        IsRectangleShape && Filled
            ? (Invert ? Avalonia.Media.Brushes.White : Avalonia.Media.Brushes.Black)
            : Avalonia.Media.Brushes.Transparent;

    public Avalonia.Media.IBrush EditorShapeStroke =>
        Invert ? Avalonia.Media.Brushes.White : Avalonia.Media.Brushes.Black;
    public bool UseProductIcon
    {
        get => IsImage && BindingMode == TextBindingMode.Variable
               && string.Equals(ValueBinding, "ProductIconKey", StringComparison.OrdinalIgnoreCase);
        set
        {
            if (!IsImage)
            {
                return;
            }

            if (value)
            {
                BindingMode = TextBindingMode.Variable;
                ValueBinding = "ProductIconKey";
            }
            else
            {
                BindingMode = TextBindingMode.Literal;
                ValueBinding = null;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayText));
            Changed?.Invoke();
        }
    }

    /// <summary>Editor label for <see cref="Invert"/> on image elements (black / white ink).</summary>
    public string IconColor
    {
        get => Invert ? "Белый" : "Чёрный";
        set
        {
            var white = string.Equals(value, "Белый", StringComparison.OrdinalIgnoreCase);
            if (Invert == white)
            {
                return;
            }

            Invert = white;
            OnPropertyChanged();
        }
    }

    public Avalonia.Media.TextAlignment TextAlignment => HorizontalAlign switch
    {
        TextHorizontalAlign.Center => Avalonia.Media.TextAlignment.Center,
        TextHorizontalAlign.Right => Avalonia.Media.TextAlignment.Right,
        _ => Avalonia.Media.TextAlignment.Left
    };

    public Avalonia.Layout.VerticalAlignment TextVerticalAlignment => VerticalAlign switch
    {
        TextVerticalAlign.Middle => Avalonia.Layout.VerticalAlignment.Center,
        TextVerticalAlign.Bottom => Avalonia.Layout.VerticalAlignment.Bottom,
        _ => Avalonia.Layout.VerticalAlignment.Top
    };

    public Avalonia.Layout.HorizontalAlignment BlockHorizontalAlignment => HorizontalAlign switch
    {
        TextHorizontalAlign.Center => Avalonia.Layout.HorizontalAlignment.Center,
        TextHorizontalAlign.Right => Avalonia.Layout.HorizontalAlignment.Right,
        _ => Avalonia.Layout.HorizontalAlignment.Left
    };

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
        OnPropertyChanged(nameof(IsAddonsKitchen));
        if (!string.IsNullOrWhiteSpace(value) && BindingMode != TextBindingMode.Variable)
        {
            BindingMode = TextBindingMode.Variable;
            OnPropertyChanged(nameof(BindingModeLabel));
            OnPropertyChanged(nameof(IsVariableBinding));
            OnPropertyChanged(nameof(IsLiteralBinding));
            OnPropertyChanged(nameof(ShowsLiteralContent));
            OnPropertyChanged(nameof(ShowsVariablePicker));
        }

        if (!_previewModeActive)
        {
            PreviewText = DisplayText;
        }

        Changed?.Invoke();
    }

    partial void OnBindingModeChanged(TextBindingMode value)
    {
        OnPropertyChanged(nameof(DisplayText));
        OnPropertyChanged(nameof(UseProductIcon));
        OnPropertyChanged(nameof(BindingModeLabel));
        OnPropertyChanged(nameof(IsVariableBinding));
        OnPropertyChanged(nameof(IsLiteralBinding));
        OnPropertyChanged(nameof(ShowsLiteralContent));
        OnPropertyChanged(nameof(ShowsVariablePicker));
        OnPropertyChanged(nameof(IsAddonsKitchen));
        if (!_previewModeActive)
        {
            PreviewText = DisplayText;
        }

        Changed?.Invoke();
    }
    partial void OnNameChanged(string value) => Changed?.Invoke();
    partial void OnIsBoldChanged(bool value) => Changed?.Invoke();
    partial void OnFontFamilyChanged(string value) => Changed?.Invoke();
    partial void OnHorizontalAlignChanged(TextHorizontalAlign value)
    {
        OnPropertyChanged(nameof(TextAlignment));
        OnPropertyChanged(nameof(BlockHorizontalAlignment));
        Changed?.Invoke();
    }

    partial void OnVerticalAlignChanged(TextVerticalAlign value)
    {
        OnPropertyChanged(nameof(TextVerticalAlignment));
        Changed?.Invoke();
    }

    partial void OnIsLockedChanged(bool value) => Changed?.Invoke();
    partial void OnGroupIdChanged(string? value) => Changed?.Invoke();
    partial void OnDashedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsLineSolid));
        OnPropertyChanged(nameof(IsLineDashed));
        OnPropertyChanged(nameof(LineDashArray));
        Changed?.Invoke();
    }

    partial void OnStrokeThicknessChanged(double value)
    {
        OnPropertyChanged(nameof(LineStrokeThicknessPx));
        Changed?.Invoke();
    }
    partial void OnFilledChanged(bool value)
    {
        OnPropertyChanged(nameof(EditorShapeFill));
        Changed?.Invoke();
    }

    partial void OnInvertChanged(bool value)
    {
        OnPropertyChanged(nameof(IconColor));
        OnPropertyChanged(nameof(EditorChromeBrush));
        OnPropertyChanged(nameof(EditorTextBrush));
        OnPropertyChanged(nameof(EditorShapeFill));
        OnPropertyChanged(nameof(EditorShapeStroke));
        Changed?.Invoke();
    }
    partial void OnCornerRadiusMmChanged(double value) => Changed?.Invoke();
    partial void OnImagePathChanged(string? value)
    {
        OnPropertyChanged(nameof(DisplayText));
        if (!_previewModeActive && IsImage)
        {
            PreviewText = DisplayText;
        }

        Changed?.Invoke();
    }

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
        OnPropertyChanged(nameof(HitWidthPx));
        OnPropertyChanged(nameof(HitHeightPx));
        OnPropertyChanged(nameof(FontSizePx));
        OnPropertyChanged(nameof(LineStartPoint));
        OnPropertyChanged(nameof(LineEndPoint));
        OnPropertyChanged(nameof(LineStrokeThicknessPx));
        OnPropertyChanged(nameof(RotateOrigin));
        OnPropertyChanged(nameof(SizeMinimumMm));
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
        Font = new TemplateFont
        {
            Family = FontFamily,
            SizePt = FontSizePt,
            Bold = IsBold,
            HorizontalAlign = HorizontalAlign,
            VerticalAlign = VerticalAlign
        },
        Symbology = Symbology,
        StrokeThickness = StrokeThickness,
        Filled = Filled,
        Invert = Invert,
        Dashed = Dashed,
        CornerRadiusMm = CornerRadiusMm,
        ImagePath = ImagePath,
        AddonsKitchen = IsAddonsKitchen && AddonsKitchenLayout is not null
            ? AddonsKitchenLayoutDefaults.Clone(AddonsKitchenLayout)
            : null
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

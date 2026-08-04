namespace LabelPrint.UI.Services;

/// <summary>
/// Result of an unsaved-changes confirmation dialog.
/// </summary>
public enum UnsavedChangesResult
{
    Save,
    Discard,
    Cancel
}

/// <summary>
/// UI dialogs that require a window owner.
/// </summary>
public interface IUiDialogService
{
    /// <summary>
    /// Asks the user what to do with unsaved changes.
    /// </summary>
    Task<UnsavedChangesResult> ConfirmUnsavedChangesAsync(string title, string message);

    /// <summary>
    /// Asks a yes/no question. Returns <c>true</c> when the user confirms.
    /// </summary>
    Task<bool> ConfirmAsync(string title, string message, string confirmText = "Да", string cancelText = "Отмена");

    /// <summary>
    /// Opens a file picker for PNG icons. Returns the selected path or <c>null</c>.
    /// </summary>
    Task<string?> PickPngFileAsync(string title = "Выберите PNG-иконку");

    /// <summary>
    /// Opens a file picker for template JSON. Returns the selected path or <c>null</c>.
    /// </summary>
    Task<string?> PickJsonFileAsync(string title = "Выберите JSON шаблона");
}

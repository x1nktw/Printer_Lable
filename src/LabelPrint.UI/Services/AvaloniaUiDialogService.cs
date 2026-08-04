using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace LabelPrint.UI.Services;

/// <summary>
/// Avalonia implementation of confirmation dialogs.
/// </summary>
public sealed class AvaloniaUiDialogService : IUiDialogService
{
    private const double DialogWidth = 420;

    /// <inheritdoc />
    public async Task<UnsavedChangesResult> ConfirmUnsavedChangesAsync(string title, string message)
    {
        var owner = GetMainWindow();
        if (owner is null)
        {
            return UnsavedChangesResult.Cancel;
        }

        var result = UnsavedChangesResult.Cancel;
        var dialog = CreateDialog(title);
        var saveButton = CreateButton("Сохранить");
        var discardButton = CreateButton("Не сохранять");
        var cancelButton = CreateButton("Отмена");

        saveButton.Click += (_, _) =>
        {
            result = UnsavedChangesResult.Save;
            dialog.Close();
        };
        discardButton.Click += (_, _) =>
        {
            result = UnsavedChangesResult.Discard;
            dialog.Close();
        };
        cancelButton.Click += (_, _) =>
        {
            result = UnsavedChangesResult.Cancel;
            dialog.Close();
        };

        dialog.Content = BuildContent(message, saveButton, discardButton, cancelButton);
        await dialog.ShowDialog(owner);
        return result;
    }

    /// <inheritdoc />
    public async Task<bool> ConfirmAsync(string title, string message, string confirmText = "Да", string cancelText = "Отмена")
    {
        var owner = GetMainWindow();
        if (owner is null)
        {
            return false;
        }

        var confirmed = false;
        var dialog = CreateDialog(title);
        var okButton = CreateButton(confirmText);
        var cancelButton = CreateButton(cancelText);

        okButton.Click += (_, _) =>
        {
            confirmed = true;
            dialog.Close();
        };
        cancelButton.Click += (_, _) => dialog.Close();

        dialog.Content = BuildContent(message, okButton, cancelButton);
        await dialog.ShowDialog(owner);
        return confirmed;
    }

    /// <inheritdoc />
    public async Task<string?> PickPngFileAsync(string title = "Выберите PNG-иконку")
    {
        var owner = GetMainWindow();
        if (owner is null)
        {
            return null;
        }

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("PNG")
                {
                    Patterns = ["*.png"],
                    MimeTypes = ["image/png"]
                }
            ]
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private static Window CreateDialog(string title) => new()
    {
        Title = title,
        Width = DialogWidth,
        MinWidth = DialogWidth,
        MaxWidth = DialogWidth,
        SizeToContent = SizeToContent.Height,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        CanResize = false,
        ShowInTaskbar = false,
        SystemDecorations = SystemDecorations.Full,
        UseLayoutRounding = true
    };

    private static Button CreateButton(string content) => new()
    {
        Content = content,
        MinWidth = 96,
        MinHeight = 32,
        Padding = new Thickness(14, 6),
        HorizontalContentAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(8, 0, 0, 0)
    };

    private static Control BuildContent(string message, params Control[] buttons)
    {
        var messageBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            MaxWidth = DialogWidth - 48
        };

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Spacing = 0,
            Margin = new Thickness(0, 20, 0, 0)
        };
        foreach (var button in buttons)
        {
            buttonRow.Children.Add(button);
        }

        return new Border
        {
            Padding = new Thickness(24, 20, 24, 16),
            Width = DialogWidth,
            Child = new StackPanel
            {
                Spacing = 0,
                Children = { messageBlock, buttonRow }
            }
        };
    }

    private static Window? GetMainWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }

        return null;
    }
}

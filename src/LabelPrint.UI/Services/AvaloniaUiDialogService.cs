using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;

namespace LabelPrint.UI.Services;

/// <summary>
/// Avalonia implementation of confirmation dialogs.
/// </summary>
public sealed class AvaloniaUiDialogService : IUiDialogService
{
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
        var saveButton = CreateButton("Сохранить", 120);
        var discardButton = CreateButton("Не сохранять", 140);
        var cancelButton = CreateButton("Отмена", 100);

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
        var okButton = CreateButton(confirmText, 120);
        var cancelButton = CreateButton(cancelText, 100);

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

    private static Window CreateDialog(string title) => new()
    {
        Title = title,
        Width = 460,
        SizeToContent = SizeToContent.Height,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        CanResize = false,
        ShowInTaskbar = false,
        MinHeight = 160
    };

    private static Button CreateButton(string content, double minWidth) => new()
    {
        Content = content,
        MinWidth = minWidth,
        Padding = new Thickness(12, 6)
    };

    private static Border BuildContent(string message, params Control[] buttons)
    {
        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 20, 0, 0)
        };
        foreach (var button in buttons)
        {
            buttonRow.Children.Add(button);
        }

        var root = new StackPanel { Spacing = 8 };
        root.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap
        });
        root.Children.Add(buttonRow);

        return new Border
        {
            Padding = new Thickness(20),
            Child = root
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

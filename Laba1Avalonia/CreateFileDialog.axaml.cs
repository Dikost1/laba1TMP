using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Laba1Avalonia;

// Диалог создания файлов данных и спецификаций.
/// Диалог создания нового файла спецификаций.
public partial class CreateFileDialog : Window
{
    public string FileName { get; private set; } = "";
    public int MaxNameLength { get; private set; }
    public string? SpecFileName { get; private set; }

    public CreateFileDialog()
    {
        InitializeComponent();
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        var fileName = FileNameBox?.Text?.Trim();
        if (string.IsNullOrEmpty(fileName))
        {
            return;
        }
        if (!int.TryParse(MaxLengthBox?.Text, out var maxLen) || maxLen <= 0)
        {
            return;
        }
        FileName = fileName;
        MaxNameLength = maxLen;
        var spec = SpecFileBox?.Text?.Trim();
        SpecFileName = string.IsNullOrEmpty(spec) ? null : spec;
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}

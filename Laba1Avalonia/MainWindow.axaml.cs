using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SpecificationData;

namespace Laba1Avalonia;

public partial class MainWindow : Window
{
    /// <summary>Общий менеджер спецификаций для всего приложения.</summary>
    public static SpecificationManager Manager { get; } = new SpecificationManager();

    public MainWindow()
    {
        InitializeComponent();
        UpdateStatus();
    }

    /// <summary>Обновляет строку статуса на главной форме.</summary>
    public void UpdateStatus()
    {
        StatusText!.Text = Manager.IsOpen
            ? $"Открыт файл: {Manager.OpenedFilePath}"
            : "Файл не открыт";
    }

    private async void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new CreateFileDialog();
        if (await dialog.ShowDialog<bool>(this) == true)
        {
            try
            {
                Manager.Create(dialog.FileName, dialog.MaxNameLength, dialog.SpecFileName, overwrite: true);
                UpdateStatus();
                await new InfoDialog("Успех", "Файлы созданы успешно.").ShowDialog(this);
            }
            catch (SpecificationException ex)
            {
                await new InfoDialog("Ошибка", ex.Message).ShowDialog(this);
            }
        }
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Открыть файл списка изделий",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Файлы .prd") { Patterns = new[] { "*.prd" } } }
        });
        if (files.Count == 0) return;
        try
        {
            Manager.Open(files[0].Path.LocalPath);
            UpdateStatus();
            await new InfoDialog("Успех", "Файл открыт успешно.").ShowDialog(this);
        }
        catch (SpecificationException ex)
        {
            await new InfoDialog("Ошибка", ex.Message).ShowDialog(this);
        }
    }

    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        Manager.Close();
        Close();
    }

    private async void OnShowComponentsClick(object? sender, RoutedEventArgs e)
    {
        if (!Manager.IsOpen)
        {
            await new InfoDialog("Ошибка", "Сначала откройте или создайте файл.").ShowDialog(this);
            return;
        }
        new ComponentsWindow().Show();
    }
}

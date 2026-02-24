using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SpecificationData;

namespace Laba1Avalonia;

/// Диалог добавления комплектующего в спецификацию.
public partial class SpecificationDialog : Window
{
    public bool Result { get; private set; }

    public SpecificationDialog()
    {
        InitializeComponent();
    }

    private async void OnOkClick(object? sender, RoutedEventArgs e)
    {
        var comp = ComponentBox?.Text?.Trim();
        var assem = AssemblyBox?.Text?.Trim();
        if (string.IsNullOrEmpty(comp) || string.IsNullOrEmpty(assem))
            return;
        try
        {
            MainWindow.Manager.InputAssembly(comp, assem);
            Result = true;
            Close(true);
        }
        catch (SpecificationException ex)
        {
            await new InfoDialog("Ошибка", ex.Message).ShowDialog(this);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Result = false;
        Close(false);
    }
}

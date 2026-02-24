using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Laba1Avalonia;

/// <summary>Простой диалог с сообщением.</summary>
public partial class InfoDialog : Window
{
    public new string Title { get; private set; } = "";
    public string Message { get; private set; } = "";

    public InfoDialog()
    {
        InitializeComponent();
    }

    public InfoDialog(string title, string message)
    {
        InitializeComponent();
        Title = title;
        Message = message;
        MessageText!.Text = message;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}

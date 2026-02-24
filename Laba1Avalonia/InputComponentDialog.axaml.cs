using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SpecificationData;

namespace Laba1Avalonia;

/// <summary>Диалог добавления компонента.</summary>
public partial class InputComponentDialog : Window
{
    public bool Result { get; private set; }
    public string? ComponentName { get; private set; }
    public ComponentType? ComponentType { get; private set; }

    public List<string> TypeItems { get; } = new() { "Изделие", "Узел", "Деталь" };

    public InputComponentDialog()
    {
        InitializeComponent();
        TypeCombo!.ItemsSource = TypeItems;
        TypeCombo.SelectedIndex = 0;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        var name = NameBox?.Text?.Trim();
        if (string.IsNullOrEmpty(name))
            return;
        var typeStr = TypeCombo?.SelectedItem as string;
        var type = typeStr switch
        {
            "Изделие" => SpecificationData.ComponentType.Product,
            "Узел" => SpecificationData.ComponentType.Unit,
            "Деталь" => SpecificationData.ComponentType.Part,
            _ => (SpecificationData.ComponentType?)null
        };
        if (type == null) return;
        ComponentName = name;
        ComponentType = type;
        Result = true;
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Result = false;
        Close(false);
    }
}

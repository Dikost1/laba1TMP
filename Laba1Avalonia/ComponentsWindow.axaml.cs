using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SpecificationData;

namespace Laba1Avalonia;

/// <summary>Форма со списком компонентов (рис. 4 методички).</summary>
public partial class ComponentsWindow : Window
{
    public ComponentsWindow()
    {
        InitializeComponent();
        LoadComponents();
    }

    /// <summary>Загружает и отображает список компонентов.</summary>
    public void LoadComponents()
    {
        var list = MainWindow.Manager.PrintAll();
        var items = new List<ComponentItem>();
        foreach (var (name, type) in list)
        {
            var typeName = type switch
            {
                ComponentType.Product => "Изделие",
                ComponentType.Unit => "Узел",
                _ => "Деталь"
            };
            items.Add(new ComponentItem { Name = name, TypeName = typeName });
        }
        ComponentsList!.ItemsSource = items;
    }

    private async void OnAddComponentClick(object? sender, RoutedEventArgs e)
    {
        var dlg = new InputComponentDialog();
        if (await dlg.ShowDialog<bool>(this) == true && dlg.ComponentName != null && dlg.ComponentType != null)
        {
            try
            {
                MainWindow.Manager.InputComponent(dlg.ComponentName, dlg.ComponentType.Value);
                LoadComponents();
            }
            catch (SpecificationException ex)
            {
                await new InfoDialog("Ошибка", ex.Message).ShowDialog(this);
            }
        }
    }

    private async void OnAddToSpecClick(object? sender, RoutedEventArgs e)
    {
        var dlg = new SpecificationDialog();
        if (await dlg.ShowDialog<bool>(this) == true)
            LoadComponents();
    }
}

/// <summary>Элемент для отображения в DataGrid.</summary>
public class ComponentItem
{
    public string Name { get; set; } = "";
    public string TypeName { get; set; } = "";
}

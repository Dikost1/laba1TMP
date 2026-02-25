// Точка входа для GUI-приложения на Avalonia. Настраивает AppBuilder и запускает главное окно.
using Avalonia;
using System;

namespace Laba1Avalonia;

class Program
{

    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);


    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

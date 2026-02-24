// Консольное приложение для работы с многосвязными структурами данных (Задание 1)
// Тестирующая программа выводит подсказку PS> и ожидает команды

using SpecificationData;

var manager = new SpecificationManager();

// Вывод справки по командам
void PrintHelp(string? outputFile = null)
{
    const string help = @"Список команд:
Create имя_файла(макс_длина_имени[, имя_файла_спецификаций]) — создать файлы
Open имя_файла — открыть файл
Input (имя_компонента, тип) — добавить компонент (тип: Изделие, Узел, Деталь)
Input (имя_компонента/имя_комплектующего) — добавить комплектующее в спецификацию
Delete (имя_компонента) — логически удалить компонент
Delete (имя_компонента/имя_комплектующего) — удалить комплектующее из спецификации
Restore (имя_компонента) — восстановить компонент
Restore (*) — восстановить все
Truncate — физически удалить помеченные записи
Print (имя_компонента) — вывести спецификацию
Print (*) — вывести список всех компонентов
Help [имя_файла] — вывести справку
Exit — выход";

    if (string.IsNullOrWhiteSpace(outputFile))
        Console.WriteLine(help);
    else
        File.WriteAllText(outputFile, help);
}

// Парсинг команды Create: Create filename( length [ , specfile ] )
bool TryParseCreate(string input, out string? fileName, out int maxLength, out string? specFile)
{
    fileName = null;
    maxLength = 0;
    specFile = null;
    var match = System.Text.RegularExpressions.Regex.Match(input.Trim(),
        @"Create\s+([^\s(]+)\s*\(\s*(\d+)\s*(?:,\s*([^\s)]+)\s*)?\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    if (!match.Success) return false;
    fileName = match.Groups[1].Value.Trim();
    maxLength = int.Parse(match.Groups[2].Value);
    if (match.Groups[3].Success && !string.IsNullOrWhiteSpace(match.Groups[3].Value))
        specFile = match.Groups[3].Value.Trim();
    return true;
}

// Парсинг Input (name, type)
bool TryParseInputComponent(string input, out string? name, out ComponentType? type)
{
    name = null;
    type = null;
    var match = System.Text.RegularExpressions.Regex.Match(input.Trim(),
        @"Input\s*\(\s*(.+?)\s*,\s*(Изделие|Узел|Деталь)\s*\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    if (!match.Success) return false;
    name = match.Groups[1].Value.Trim();
    type = match.Groups[2].Value.ToLowerInvariant() switch
    {
        "изделие" => ComponentType.Product,
        "узел" => ComponentType.Unit,
        "деталь" => ComponentType.Part,
        _ => (ComponentType?)null
    };
    return type != null;
}

// Парсинг Input (component/assembly)
bool TryParseInputAssembly(string input, out string? component, out string? assembly)
{
    component = null;
    assembly = null;
    var match = System.Text.RegularExpressions.Regex.Match(input.Trim(),
        @"Input\s*\(\s*(.+?)\s*/\s*(.+?)\s*\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    if (!match.Success) return false;
    component = match.Groups[1].Value.Trim();
    assembly = match.Groups[2].Value.Trim();
    return true;
}

// Парсинг Delete (name) или Delete (comp/assem)
bool TryParseDeleteOne(string input, out string? name)
{
    name = null;
    var match = System.Text.RegularExpressions.Regex.Match(input.Trim(),
        @"Delete\s*\(\s*(.+?)\s*\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    if (!match.Success) return false;
    var arg = match.Groups[1].Value.Trim();
    if (arg.Contains('/'))
    {
        var parts = arg.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2) { name = arg; return true; } // возвращаем как "comp/assem"
    }
    name = arg;
    return true;
}

bool TryParseDeleteAssembly(string input, out string? component, out string? assembly)
{
    component = null;
    assembly = null;
    var match = System.Text.RegularExpressions.Regex.Match(input.Trim(),
        @"Delete\s*\(\s*(.+?)\s*/\s*(.+?)\s*\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    if (!match.Success) return false;
    component = match.Groups[1].Value.Trim();
    assembly = match.Groups[2].Value.Trim();
    return true;
}

// Парсинг Restore (name) или Restore (*)
bool TryParseRestore(string input, out string? name, out bool all)
{
    name = null;
    all = false;
    var match = System.Text.RegularExpressions.Regex.Match(input.Trim(),
        @"Restore\s*\(\s*(.+?)\s*\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    if (!match.Success) return false;
    var arg = match.Groups[1].Value.Trim();
    if (arg == "*") { all = true; return true; }
    name = arg;
    return true;
}

// Парсинг Print (name) или Print (*)
bool TryParsePrint(string input, out string? name, out bool all)
{
    name = null;
    all = false;
    var match = System.Text.RegularExpressions.Regex.Match(input.Trim(),
        @"Print\s*\(\s*(.+?)\s*\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    if (!match.Success) return false;
    var arg = match.Groups[1].Value.Trim();
    if (arg == "*") { all = true; return true; }
    name = arg;
    return true;
}

// Парсинг Open
bool TryParseOpen(string input, out string? fileName)
{
    fileName = null;
    var parts = input.Trim().Split(' ', 2, StringSplitOptions.TrimEntries);
    if (parts.Length < 2 || !parts[0].Equals("Open", StringComparison.OrdinalIgnoreCase)) return false;
    fileName = parts[1].Trim();
    return !string.IsNullOrEmpty(fileName);
}

// Парсинг Help [file]
bool TryParseHelp(string input, out string? outputFile)
{
    outputFile = null;
    var parts = input.Trim().Split(' ', 2, StringSplitOptions.TrimEntries);
    if (parts.Length < 1 || !parts[0].Equals("Help", StringComparison.OrdinalIgnoreCase)) return false;
    if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]))
        outputFile = parts[1].Trim();
    return true;
}

void ProcessCommand(string line)
{
    var cmd = line.Trim();
    if (string.IsNullOrEmpty(cmd)) return;

    try
    {
        // Create
        if (TryParseCreate(cmd, out var createFile, out var createLen, out var createSpec))
        {
            var overwrite = false;
            if (File.Exists(createFile + (createFile.EndsWith(".prd") ? "" : ".prd")))
            {
                Console.Write("Файл существует. Перезаписать? (y/n): ");
                var input = Console.ReadLine()?.Trim() ?? "";
                overwrite = input.Equals("y", StringComparison.OrdinalIgnoreCase);
            }
            manager.Create(createFile, createLen, createSpec, overwrite);
            Console.WriteLine("Файлы созданы успешно.");
            return;
        }

        // Open
        if (TryParseOpen(cmd, out var openFile))
        {
            manager.Open(openFile!);
            Console.WriteLine("Файл открыт успешно.");
            return;
        }

        // Input (name, type)
        if (TryParseInputComponent(cmd, out var inName, out var inType))
        {
            manager.InputComponent(inName!, inType!.Value);
            Console.WriteLine("Компонент добавлен.");
            return;
        }

        // Input (comp/assem)
        if (TryParseInputAssembly(cmd, out var inComp, out var inAssem))
        {
            manager.InputAssembly(inComp!, inAssem!);
            Console.WriteLine("Комплектующее добавлено в спецификацию.");
            return;
        }

        // Delete (comp/assem)
        if (TryParseDeleteAssembly(cmd, out var delComp, out var delAssem))
        {
            manager.DeleteAssembly(delComp!, delAssem!);
            Console.WriteLine("Комплектующее удалено из спецификации.");
            return;
        }

        // Delete (name)
        if (TryParseDeleteOne(cmd, out var delName) && delName != null && !delName.Contains('/'))
        {
            manager.DeleteComponent(delName);
            Console.WriteLine("Компонент удалён.");
            return;
        }

        // Restore (*)
        if (TryParseRestore(cmd, out var restName, out var restAll) && restAll)
        {
            manager.RestoreAll();
            Console.WriteLine("Все записи восстановлены.");
            return;
        }

        // Restore (name)
        if (TryParseRestore(cmd, out restName, out _) && restName != null)
        {
            manager.RestoreComponent(restName);
            Console.WriteLine("Компонент восстановлен.");
            return;
        }

        // Truncate
        if (cmd.Trim().Equals("Truncate", StringComparison.OrdinalIgnoreCase))
        {
            manager.Truncate();
            Console.WriteLine("Усечение выполнено.");
            return;
        }

        // Print (*)
        if (TryParsePrint(cmd, out var printName, out var printAll) && printAll)
        {
            var list = manager.PrintAll();
            foreach (var (name, type) in list)
            {
                var typeStr = type switch { ComponentType.Product => "Изделие", ComponentType.Unit => "Узел", _ => "Деталь" };
                Console.WriteLine($"{name} {typeStr}");
            }
            return;
        }

        // Print (name)
        if (TryParsePrint(cmd, out printName, out _) && printName != null)
        {
            Console.WriteLine(manager.PrintComponent(printName));
            return;
        }

        // Help
        if (TryParseHelp(cmd, out var helpFile))
        {
            PrintHelp(helpFile);
            if (helpFile != null)
                Console.WriteLine($"Справка записана в файл {helpFile}");
            return;
        }

        // Exit
        if (cmd.Trim().Equals("Exit", StringComparison.OrdinalIgnoreCase))
        {
            manager.Close();
            Console.WriteLine("До свидания.");
            Environment.Exit(0);
        }

        Console.WriteLine("Неизвестная команда. Введите Help для справки.");
    }
    catch (SpecificationException ex)
    {
        Console.WriteLine($"Ошибка: {ex.Message}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка: {ex.Message}");
    }
}

// Основной цикл
Console.WriteLine("Спецификации — консольное приложение. Введите Help для справки.");
while (true)
{
    Console.Write("PS> ");
    var line = Console.ReadLine();
    if (line == null) break;
    ProcessCommand(line);
}

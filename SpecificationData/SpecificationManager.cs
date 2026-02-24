using System.Text;

namespace SpecificationData;

/// <summary>
/// Менеджер для работы с многосвязными структурами данных:
/// список изделий/узлов/деталей и список спецификаций.
/// </summary>
public class SpecificationManager
{
    private const int ProductsHeaderSize = 2 + 4 + 4 + Constants.SpecFileNameSize; // 26 байт (длина записи 2 + указатели 8 + имя 16)
    private const int ProductsRecordOverhead = 1 + 4 + 4; // бит удаления + ptr spec + ptr next
    private const int SpecHeaderSize = 4 + 4; // ptr first + ptr free
    private const int SpecRecordSize = 1 + 4 + 2 + 4; // delete + ptr product + multiplicity + ptr next

    private BinaryWriter? _productsWriter;
    private BinaryReader? _productsReader;
    private BinaryWriter? _specWriter;
    private BinaryReader? _specReader;
    private FileStream? _productsStream;
    private FileStream? _specStream;

    private int _recordLength;
    private int _firstRecordOffset;
    private int _freeAreaOffset;
    private string _specFileName = "";
    private string _basePath = "";
    private bool _isOpen;

    /// <summary>Путь к открытому файлу списка изделий.</summary>
    public string? OpenedFilePath { get; private set; }

    /// <summary>Открыты ли файлы для работы.</summary>
    public bool IsOpen => _isOpen;

    /// <summary>Максимальная длина имени компонента в текущем файле.</summary>
    public int RecordLength => _recordLength;

    /// <summary>
    /// Проверяет, соответствует ли файл формату (сигнатура PS).
    /// </summary>
    public static bool CheckSignature(string filePath)
    {
        if (!File.Exists(filePath)) return false;
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (fs.Length < Constants.SignatureSize) return false;
        var b1 = (byte)fs.ReadByte();
        var b2 = (byte)fs.ReadByte();
        return b1 == 'P' && b2 == 'S';
    }

    /// <summary>
    /// Создаёт новые файлы. Если файл существует и сигнатура верна — нужно подтверждение на перезапись.
    /// </summary>
    /// <param name="baseFileName">Базовое имя файла (без расширения).</param>
    /// <param name="maxComponentNameLength">Максимальная длина имени компонента.</param>
    /// <param name="specFileName">Имя файла спецификаций (опционально).</param>
    /// <param name="overwrite">Подтверждение перезаписи, если файл существует.</param>
    public void Create(string baseFileName, int maxComponentNameLength, string? specFileName = null, bool overwrite = false)
    {
        var basePath = EnsureExtension(baseFileName, Constants.ProductsExtension);
        var specPath = specFileName != null
            ? EnsureExtension(specFileName, Constants.SpecsExtension)
            : Path.ChangeExtension(basePath, Constants.SpecsExtension);

        if (File.Exists(basePath))
        {
            if (!CheckSignature(basePath))
                throw new SpecificationException("Сигнатура файла отсутствует или не соответствует заданию.");
            if (!overwrite)
                throw new SpecificationException("Файл существует. Требуется подтверждение на перезапись.");
        }

        _recordLength = maxComponentNameLength;
        _basePath = Path.GetFullPath(basePath);
        var specFullPath = Path.Combine(Path.GetDirectoryName(_basePath) ?? ".", Path.GetFileName(specPath));

        // Очищаем старые файлы
        Close();
        if (File.Exists(_basePath)) File.Delete(_basePath);
        if (File.Exists(specFullPath)) File.Delete(specFullPath);

        // Создаём файл изделий
        _productsStream = new FileStream(_basePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        _productsWriter = new BinaryWriter(_productsStream, Encoding.ASCII);
        _productsReader = new BinaryReader(_productsStream, Encoding.ASCII);

        // Сигнатура
        _productsWriter.Write((byte)'P');
        _productsWriter.Write((byte)'S');

        // Длина записи данных (2 байта)
        _productsWriter.Write((short)_recordLength);

        // Указатель на первую запись (-1 — пусто)
        _productsWriter.Write(Constants.NullPointer);

        // Указатель на свободную область — после заголовка и первой записи
        _firstRecordOffset = Constants.SignatureSize + ProductsHeaderSize;
        var firstRecordSize = ProductsRecordOverhead + _recordLength;
        _freeAreaOffset = _firstRecordOffset + firstRecordSize;
        _productsWriter.Write(_freeAreaOffset);

        // Имя файла спецификаций (16 байт)
        var specName = Path.GetFileName(specPath);
        if (specName.Length > Constants.SpecFileNameSize)
            specName = specName[..Constants.SpecFileNameSize];
        var specNameBytes = Encoding.ASCII.GetBytes(specName.PadRight(Constants.SpecFileNameSize));
        _productsWriter.Write(specNameBytes);

        _productsStream.Flush();

        // Создаём файл спецификаций
        _specStream = new FileStream(specFullPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        _specWriter = new BinaryWriter(_specStream, Encoding.ASCII);
        _specReader = new BinaryReader(_specStream, Encoding.ASCII);

        // Заголовок: ptr first, ptr free
        _specWriter.Write(Constants.NullPointer);
        _specWriter.Write(SpecHeaderSize); // свободная область начинается после заголовка
        _specStream.Flush();

        _specFileName = specFullPath;
        OpenedFilePath = _basePath;
        _isOpen = true;
    }

    /// <summary>
    /// Открывает существующие файлы.
    /// </summary>
    public void Open(string fileName)
    {
        var basePath = EnsureExtension(fileName, Constants.ProductsExtension);
        if (!File.Exists(basePath))
            throw new SpecificationException("Файл не найден.");

        if (!CheckSignature(basePath))
            throw new SpecificationException("Сигнатура файла отсутствует или не соответствует заданию.");

        Close();

        _basePath = Path.GetFullPath(basePath);
        _productsStream = new FileStream(_basePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        _productsReader = new BinaryReader(_productsStream, Encoding.ASCII);
        _productsWriter = new BinaryWriter(_productsStream, Encoding.ASCII);

        _productsStream.Seek(Constants.SignatureSize, SeekOrigin.Begin);
        _recordLength = _productsReader.ReadInt16();
        _firstRecordOffset = _productsReader.ReadInt32();
        _freeAreaOffset = _productsReader.ReadInt32();
        var specNameBytes = _productsReader.ReadBytes(Constants.SpecFileNameSize);
        _specFileName = Path.Combine(Path.GetDirectoryName(_basePath) ?? ".",
            Encoding.ASCII.GetString(specNameBytes).TrimEnd('\0', ' '));

        if (!File.Exists(_specFileName))
            _specFileName = Path.ChangeExtension(_basePath, Constants.SpecsExtension);

        _specStream = new FileStream(_specFileName, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        _specReader = new BinaryReader(_specStream, Encoding.ASCII);
        _specWriter = new BinaryWriter(_specStream, Encoding.ASCII);

        OpenedFilePath = _basePath;
        _isOpen = true;
    }

    /// <summary>
    /// Добавляет компонент в список. Тип: Изделие, Узел, Деталь.
    /// </summary>
    public void InputComponent(string name, ComponentType type)
    {
        EnsureOpen();
        if (string.IsNullOrWhiteSpace(name))
            throw new SpecificationException("Имя компонента не может быть пустым.");

        var trimmedName = name.Trim();
        if (trimmedName.Length > _recordLength)
            throw new SpecificationException($"Имя компонента превышает максимальную длину ({_recordLength}).");

        if (FindComponentOffset(trimmedName) != Constants.NullPointer)
            throw new SpecificationException("Компонент с таким именем уже существует.");

        // Добавляем в алфавитном порядке
        AddComponentInOrder(trimmedName, type);
    }

    /// <summary>
    /// Добавляет комплектующее в спецификацию компонента.
    /// </summary>
    public void InputAssembly(string componentName, string assemblyName)
    {
        EnsureOpen();
        var compOffset = FindComponentOffset(componentName);
        if (compOffset == Constants.NullPointer)
            throw new SpecificationException("Компонент не найден в списке.");

        var assemblyOffset = FindComponentOffset(assemblyName);
        if (assemblyOffset == Constants.NullPointer)
            throw new SpecificationException("Комплектующее не найдено в списке.");

        var compRecord = ReadProductRecord(compOffset);
        if (compRecord.FirstSpecOffset == Constants.NullPointer)
            throw new SpecificationException("Компонент является деталью и не может иметь спецификацию.");

        // Проверяем, является ли compRecord изделием/узлом — у детали FirstSpecOffset = -1
        if (compRecord.FirstSpecOffset == Constants.NullPointer)
            throw new SpecificationException("Деталь не может иметь комплектующие.");

        // Перечитываем — у изделия/узла FirstSpecOffset изначально -1 при создании
        // У детали при создании мы записываем -1. Значит, если это деталь, FirstSpecOffset = -1.
        if (IsPart(compOffset))
            throw new SpecificationException("Деталь не может иметь спецификацию.");

        AddToSpecification(compOffset, assemblyOffset);
    }

    /// <summary>
    /// Логически удаляет компонент из списка.
    /// </summary>
    public void DeleteComponent(string name)
    {
        EnsureOpen();
        var offset = FindComponentOffset(name);
        if (offset == Constants.NullPointer)
            throw new SpecificationException("Компонент не найден в списке.");

        if (HasReferences(offset))
            throw new SpecificationException("На компонент имеются ссылки в спецификациях других компонентов.");

        MarkProductDeleted(offset);
    }

    /// <summary>
    /// Удаляет комплектующее из спецификации компонента.
    /// </summary>
    public void DeleteAssembly(string componentName, string assemblyName)
    {
        EnsureOpen();
        var compOffset = FindComponentOffset(componentName);
        if (compOffset == Constants.NullPointer)
            throw new SpecificationException("Компонент не найден.");

        if (IsPart(compOffset))
            throw new SpecificationException("Для детали эта команда вызывает ошибку.");

        var assemblyOffset = FindComponentOffset(assemblyName);
        if (assemblyOffset == Constants.NullPointer)
            throw new SpecificationException("Комплектующее не найдено.");

        MarkSpecDeleted(compOffset, assemblyOffset);
    }

    /// <summary>
    /// Восстанавливает логически удалённый компонент и восстанавливает алфавитный порядок.
    /// </summary>
    public void RestoreComponent(string name)
    {
        EnsureOpen();
        var offset = FindComponentOffsetIncludingDeleted(name);
        if (offset == Constants.NullPointer)
            throw new SpecificationException("Компонент не найден.");

        UnmarkProductDeleted(offset);
        RebuildAlphabeticalOrder();
    }

    /// <summary>
    /// Восстанавливает все удалённые записи и алфавитный порядок.
    /// </summary>
    public void RestoreAll()
    {
        EnsureOpen();
        var current = _firstRecordOffset;
        while (current != Constants.NullPointer)
        {
            var record = ReadProductRecord(current);
            if (record.Deleted)
                UnmarkProductDeleted(current);
            current = record.NextOffset;
        }
        RebuildAlphabeticalOrder();
    }

    /// <summary>
    /// Физически удаляет помеченные записи и уплотняет файлы.
    /// </summary>
    public void Truncate()
    {
        EnsureOpen();

        // 1. Собираем активные записи изделий
        var activeProducts = new List<(int OldOffset, sbyte DeleteFlag, int FirstSpec, int Next, byte[] NameData)>();
        var recordSize = ProductsRecordOverhead + _recordLength;
        var current = _firstRecordOffset;

        while (current != Constants.NullPointer)
        {
            _productsStream!.Seek(current, SeekOrigin.Begin);
            var del = _productsReader!.ReadSByte();
            var firstSpec = _productsReader.ReadInt32();
            var next = _productsReader.ReadInt32();
            var nameData = _productsReader.ReadBytes(_recordLength);
            if (del != Constants.DeleteFlagDeleted)
                activeProducts.Add((current, del, firstSpec, next, nameData));
            current = next;
        }

        var offsetMap = new Dictionary<int, int>();
        var newOffset = Constants.SignatureSize + ProductsHeaderSize;
        foreach (var (oldOff, _, _, _, _) in activeProducts)
        {
            offsetMap[oldOff] = newOffset;
            newOffset += recordSize;
        }

        // 2. Собираем активные записи спецификаций с учётом маппинга
        var activeSpecs = new List<(int ProductOffset, int AssemblyOffset, short Mult)>();
        foreach (var (oldOff, _, firstSpec, _, _) in activeProducts)
        {
            var specOffset = firstSpec;
            while (specOffset != Constants.NullPointer)
            {
                _specStream!.Seek(specOffset, SeekOrigin.Begin);
                var del = _specReader!.ReadSByte();
                var ptrProduct = _specReader.ReadInt32();
                var mult = _specReader.ReadInt16();
                var next = _specReader.ReadInt32();
                if (del != Constants.DeleteFlagDeleted && offsetMap.ContainsKey(ptrProduct))
                    activeSpecs.Add((oldOff, ptrProduct, mult));
                specOffset = next;
            }
        }

        // 3. Группируем спецификации по родительскому продукту
        var specsByProduct = activeSpecs.GroupBy(s => s.ProductOffset).ToDictionary(g => g.Key, g => g.ToList());

        // 4. Записываем новый файл изделий
        var tempProductsPath = _basePath + ".tmp";
        using (var fs = new FileStream(tempProductsPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        using (var bw = new BinaryWriter(fs, Encoding.ASCII))
        using (var br = new BinaryReader(fs, Encoding.ASCII))
        {
            bw.Write((byte)'P');
            bw.Write((byte)'S');
            var firstProdOffset = activeProducts.Count > 0 ? offsetMap[activeProducts[0].OldOffset] : Constants.NullPointer;
            var freeArea = Constants.SignatureSize + ProductsHeaderSize + activeProducts.Count * recordSize;
            bw.Write((short)_recordLength);
            bw.Write(firstProdOffset);
            bw.Write(freeArea);
            var specName = Path.GetFileName(_specFileName);
            if (specName.Length > Constants.SpecFileNameSize) specName = specName[..Constants.SpecFileNameSize];
            bw.Write(Encoding.ASCII.GetBytes(specName.PadRight(Constants.SpecFileNameSize)));

            for (var i = 0; i < activeProducts.Count; i++)
            {
                var (_, del, _, _, nameData) = activeProducts[i];
                var prodOff = offsetMap[activeProducts[i].OldOffset];
                var nextOff = i + 1 < activeProducts.Count ? offsetMap[activeProducts[i + 1].OldOffset] : Constants.NullPointer;
                var firstSpecForProd = Constants.NullPointer;
                if (specsByProduct.TryGetValue(activeProducts[i].OldOffset, out var list) && list.Count > 0)
                {
                    var countBefore = 0;
                    for (var k = 0; k < i; k++)
                        if (specsByProduct.TryGetValue(activeProducts[k].OldOffset, out var l)) countBefore += l.Count;
                    firstSpecForProd = SpecHeaderSize + countBefore * SpecRecordSize;
                }

                bw.Write(del);
                bw.Write(firstSpecForProd);
                bw.Write(nextOff);
                bw.Write(nameData);
            }
        }

        // 5. Записываем новый файл спецификаций
        var tempSpecPath = _specFileName + ".tmp";
        using (var fs = new FileStream(tempSpecPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        using (var bw = new BinaryWriter(fs, Encoding.ASCII))
        {
            var specFree = SpecHeaderSize;
            var firstSpecOffset = Constants.NullPointer;
            foreach (var (oldProdOff, _, _, _, _) in activeProducts)
            {
                if (!specsByProduct.TryGetValue(oldProdOff, out var lst)) continue;
                for (var j = 0; j < lst.Count; j++)
                {
                    if (firstSpecOffset == Constants.NullPointer) firstSpecOffset = specFree;
                    var (_, assemblyOff, mult) = lst[j];
                    bw.Write(Constants.DeleteFlagActive);
                    bw.Write(offsetMap[assemblyOff]);
                    bw.Write(mult);
                    bw.Write(j + 1 < lst.Count ? specFree + SpecRecordSize : Constants.NullPointer);
                    specFree += SpecRecordSize;
                }
            }
            fs.Seek(0, SeekOrigin.Begin);
            bw.Write(firstSpecOffset);
            bw.Write(specFree);
        }

        // Закрываем и заменяем файлы
        _specWriter?.Dispose();
        _specReader?.Dispose();
        _specStream?.Dispose();
        _specWriter = null;
        _specReader = null;
        _specStream = null;

        if (File.Exists(_specFileName)) File.Delete(_specFileName);
        File.Move(tempSpecPath, _specFileName);

        _productsWriter?.Dispose();
        _productsReader?.Dispose();
        _productsStream?.Dispose();
        _productsWriter = null;
        _productsReader = null;
        _productsStream = null;

        if (File.Exists(_basePath)) File.Delete(_basePath);
        File.Move(tempProductsPath, _basePath);

        // Переоткрываем с обновлёнными данными
        Open(_basePath);
    }

    /// <summary>
    /// Выводит спецификацию компонента (дерево).
    /// </summary>
    public string PrintComponent(string name)
    {
        EnsureOpen();
        var offset = FindComponentOffset(name);
        if (offset == Constants.NullPointer)
            throw new SpecificationException("Компонент не найден.");

        if (IsPart(offset))
            throw new SpecificationException("Для детали эта команда вызывает ошибку.");

        return BuildSpecificationTree(offset, "");
    }

    /// <summary>
    /// Возвращает список всех компонентов с их типами.
    /// </summary>
    public IReadOnlyList<(string Name, ComponentType Type)> PrintAll()
    {
        EnsureOpen();
        var result = new List<(string, ComponentType)>();
        var current = _firstRecordOffset;
        while (current != Constants.NullPointer)
        {
            var record = ReadProductRecord(current);
            if (!record.Deleted)
            {
                var type = record.FirstSpecOffset == Constants.NullPointer ? ComponentType.Part : GetComponentType(current);
                result.Add((record.Name, type));
            }
            current = record.NextOffset;
        }
        return result;
    }

    /// <summary>
    /// Закрывает все файлы.
    /// </summary>
    public void Close()
    {
        _specWriter?.Dispose();
        _specReader?.Dispose();
        _specStream?.Dispose();
        _productsWriter?.Dispose();
        _productsReader?.Dispose();
        _productsStream?.Dispose();

        _specWriter = null;
        _specReader = null;
        _specStream = null;
        _productsWriter = null;
        _productsReader = null;
        _productsStream = null;
        _isOpen = false;
        OpenedFilePath = null;
    }

    private void EnsureOpen()
    {
        if (!_isOpen)
            throw new SpecificationException("Файлы не открыты. Используйте Create или Open.");
    }

    private static string EnsureExtension(string path, string ext)
    {
        if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            return path;
        return path + (path.EndsWith(".") ? "" : ".") + ext.TrimStart('.');
    }

    private int FindComponentOffset(string name)
    {
        var current = _firstRecordOffset;
        while (current != Constants.NullPointer)
        {
            var record = ReadProductRecord(current);
            if (!record.Deleted && string.Equals(record.Name, name, StringComparison.OrdinalIgnoreCase))
                return current;
            current = record.NextOffset;
        }
        return Constants.NullPointer;
    }

    private int FindComponentOffsetIncludingDeleted(string name)
    {
        var current = _firstRecordOffset;
        while (current != Constants.NullPointer)
        {
            var record = ReadProductRecord(current);
            if (string.Equals(record.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase))
                return current;
            current = record.NextOffset;
        }
        return Constants.NullPointer;
    }

    private bool IsPart(int offset)
    {
        var record = ReadProductRecord(offset);
        return record.FirstSpecOffset == Constants.NullPointer;
    }

    /// <summary>
    /// Определяет тип компонента: Деталь (нет спецификации), Узел (есть спецификация и есть в чьей-то спецификации),
    /// Изделие (есть спецификация, корневой элемент).
    /// </summary>
    private ComponentType GetComponentType(int offset)
    {
        var record = ReadProductRecord(offset);
        if (record.FirstSpecOffset == Constants.NullPointer)
            return ComponentType.Part;
        return IsReferencedInAnySpec(offset) ? ComponentType.Unit : ComponentType.Product;
    }

    private bool IsReferencedInAnySpec(int offset)
    {
        var current = _firstRecordOffset;
        while (current != Constants.NullPointer)
        {
            var record = ReadProductRecord(current);
            if (!record.Deleted && record.FirstSpecOffset != Constants.NullPointer && current != offset)
            {
                var specOffset = record.FirstSpecOffset;
                while (specOffset != Constants.NullPointer)
                {
                    _specStream!.Seek(specOffset, SeekOrigin.Begin);
                    var del = _specReader!.ReadSByte();
                    var ptrProduct = _specReader.ReadInt32();
                    if (del != Constants.DeleteFlagDeleted && ptrProduct == offset)
                        return true;
                    _specReader.ReadInt16();
                    specOffset = _specReader.ReadInt32();
                }
            }
            current = record.NextOffset;
        }
        return false;
    }

    private (bool Deleted, int FirstSpecOffset, int NextOffset, string Name) ReadProductRecord(int offset)
    {
        _productsStream!.Seek(offset, SeekOrigin.Begin);
        var deleteFlag = _productsReader!.ReadSByte();
        var firstSpec = _productsReader.ReadInt32();
        var next = _productsReader.ReadInt32();
        var nameBytes = _productsReader.ReadBytes(_recordLength);
        var name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0', ' ');
        return (deleteFlag == Constants.DeleteFlagDeleted, firstSpec, next, name);
    }

    private void AddComponentInOrder(string name, ComponentType type)
    {
        var firstSpec = type == ComponentType.Part ? Constants.NullPointer : Constants.NullPointer;
        var newRecordSize = ProductsRecordOverhead + _recordLength;

        int prevOffset = Constants.NullPointer;
        var current = _firstRecordOffset;
        int? insertBefore = null;

        while (current != Constants.NullPointer)
        {
            var record = ReadProductRecord(current);
            if (!record.Deleted && string.Compare(record.Name, name, StringComparison.Ordinal) > 0)
            {
                insertBefore = current;
                break;
            }
            prevOffset = current;
            current = record.NextOffset;
        }

        var newOffset = AllocateProductRecord();
        var namePadded = name.PadRight(_recordLength);

        _productsStream!.Seek(newOffset, SeekOrigin.Begin);
        _productsWriter!.Write(Constants.DeleteFlagActive);
        _productsWriter.Write(firstSpec);
        _productsWriter.Write(insertBefore ?? current);
        _productsWriter.Write(Encoding.ASCII.GetBytes(namePadded));

        if (prevOffset == Constants.NullPointer)
        {
            _firstRecordOffset = newOffset;
            UpdateProductsHeader();
        }
        else
        {
            _productsStream.Seek(prevOffset + 1 + 4, SeekOrigin.Begin); // после delete и firstSpec
            _productsWriter.Write(newOffset);
        }
        _productsStream.Flush();
    }

    private int AllocateProductRecord()
    {
        var recordSize = ProductsRecordOverhead + _recordLength;
        var offset = _freeAreaOffset;
        _freeAreaOffset += recordSize;
        UpdateProductsHeader();
        return offset;
    }

    private void UpdateProductsHeader()
    {
        _productsStream!.Seek(Constants.SignatureSize + 2, SeekOrigin.Begin);
        _productsWriter!.Write(_firstRecordOffset);
        _productsWriter.Write(_freeAreaOffset);
        _productsStream.Flush();
    }

    private void AddToSpecification(int componentOffset, int assemblyOffset)
    {
        _specStream!.Seek(0, SeekOrigin.Begin);
        _specReader!.ReadInt32(); // первый указатель в заголовке (не используется при множественных списках)
        var specFree = _specReader.ReadInt32();

        var compRecord = ReadProductRecord(componentOffset);
        var newSpecOffset = specFree;

        _specStream.Seek(specFree, SeekOrigin.Begin);
        _specWriter!.Write(Constants.DeleteFlagActive);
        _specWriter.Write(assemblyOffset);
        _specWriter.Write((short)1); // кратность по умолчанию 1
        _specWriter.Write(compRecord.FirstSpecOffset); // next — цепочка: новая запись указывает на старую первую

        specFree += SpecRecordSize;

        _specStream.Seek(4, SeekOrigin.Begin); // пропускаем первый указатель
        _specWriter.Write(specFree);

        _productsStream!.Seek(componentOffset + 1, SeekOrigin.Begin);
        _productsWriter!.Write(newSpecOffset); // обновляем firstSpec компонента

        _productsStream.Flush();
        _specStream.Flush();
    }

    private bool HasReferences(int offset)
    {
        var current = _firstRecordOffset;
        while (current != Constants.NullPointer)
        {
            var record = ReadProductRecord(current);
            if (!record.Deleted && record.FirstSpecOffset != Constants.NullPointer)
            {
                var specOffset = record.FirstSpecOffset;
                while (specOffset != Constants.NullPointer)
                {
                    _specStream!.Seek(specOffset, SeekOrigin.Begin);
                    var del = _specReader!.ReadSByte();
                    var ptrProduct = _specReader.ReadInt32();
                    if (del != Constants.DeleteFlagDeleted && ptrProduct == offset)
                        return true;
                    _specReader.ReadInt16();
                    specOffset = _specReader.ReadInt32();
                }
            }
            current = record.NextOffset;
        }
        return false;
    }

    private void MarkProductDeleted(int offset)
    {
        _productsStream!.Seek(offset, SeekOrigin.Begin);
        _productsWriter!.Write(Constants.DeleteFlagDeleted);
        _productsStream.Flush();
    }

    private void UnmarkProductDeleted(int offset)
    {
        _productsStream!.Seek(offset, SeekOrigin.Begin);
        _productsWriter!.Write(Constants.DeleteFlagActive);
        _productsStream.Flush();
    }

    private void MarkSpecDeleted(int componentOffset, int assemblyOffset)
    {
        var record = ReadProductRecord(componentOffset);
        var specOffset = record.FirstSpecOffset;
        while (specOffset != Constants.NullPointer)
        {
            _specStream!.Seek(specOffset, SeekOrigin.Begin);
            var del = _specReader!.ReadSByte();
            var ptrProduct = _specReader.ReadInt32();
            _specReader.ReadInt16();
            var next = _specReader.ReadInt32();
            if (del != Constants.DeleteFlagDeleted && ptrProduct == assemblyOffset)
            {
                _specStream.Seek(specOffset, SeekOrigin.Begin);
                _specWriter!.Write(Constants.DeleteFlagDeleted);
                _specStream.Flush();
                return;
            }
            specOffset = next;
        }
        throw new SpecificationException("Комплектующее не найдено в спецификации.");
    }

    private void RebuildAlphabeticalOrder()
    {
        var active = new List<(int Offset, string Name)>();
        var current = _firstRecordOffset;
        while (current != Constants.NullPointer)
        {
            var record = ReadProductRecord(current);
            if (!record.Deleted)
                active.Add((current, record.Name));
            current = record.NextOffset;
        }
        active.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

        for (var i = 0; i < active.Count; i++)
        {
            var nextOffset = i + 1 < active.Count ? active[i + 1].Offset : Constants.NullPointer;
            _productsStream!.Seek(active[i].Offset + 1 + 4, SeekOrigin.Begin);
            _productsWriter!.Write(nextOffset);
        }
        _firstRecordOffset = active.Count > 0 ? active[0].Offset : Constants.NullPointer;
        UpdateProductsHeader();
    }

    private string BuildSpecificationTree(int componentOffset, string prefix)
    {
        var record = ReadProductRecord(componentOffset);
        var sb = new StringBuilder();
        sb.AppendLine(prefix + record.Name);

        var specOffset = record.FirstSpecOffset;
        var isFirst = true;
        while (specOffset != Constants.NullPointer)
        {
            _specStream!.Seek(specOffset, SeekOrigin.Begin);
            var del = _specReader!.ReadSByte();
            var ptrProduct = _specReader.ReadInt32();
            _specReader.ReadInt16();
            var next = _specReader.ReadInt32();

            if (del != Constants.DeleteFlagDeleted)
            {
                var branch = isFirst ? "|" : "| ";
                sb.Append(prefix).Append(branch).AppendLine();
                var subRecord = ReadProductRecord(ptrProduct);
                var childPrefix = prefix + (isFirst ? "| " : "  ");
                if (subRecord.FirstSpecOffset == Constants.NullPointer)
                    sb.Append(childPrefix).Append(subRecord.Name).AppendLine();
                else
                    sb.Append(BuildSpecificationTree(ptrProduct, childPrefix));
            }
            isFirst = false;
            specOffset = next;
        }
        return sb.ToString();
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SpecificationData;

// SpecificationManager — основной класс для управления файлами компонентов и спецификаций.
// Предоставляет операции: Create/Open/Close, добавление компонентов, добавление/удаление
// комплектующих (спецификаций), логическое удаление/восстановление, а также печать данных.
// Комментарии и документация метода местами упрощены для понятности на русском.
public class SpecificationManager
{
    private const int ProductsHeaderSize = 3 + 2 + 4 + 4 + Constants.SpecFileNameSize; // sig(3)+reserved(2)+headerSize(4)+recordLength(4)+spec name
    private const int ProductsRecordOverhead = 1 + 4 + 4 + 1; // флаг удаления + указатель на спецификацию + указатель на следующую запись + тип (байт)
    private const int SpecHeaderSize = 4 + 4; // первый указатель (не используется) + указатель на свободную область
    private const int SpecRecordSize = 1 + 4 + 2 + 4; // флаг удаления + указатель на продукт + кратность (short) + указатель на следующую запись

    private BinaryWriter? _productsWriter;
    private BinaryReader? _productsReader;
    private BinaryWriter? _specWriter;
    private BinaryReader? _specReader;
    private FileStream? _productsStream;
    private FileStream? _specStream;

    private int _recordLength;
    private int _firstRecordOffset;
    private string _specFileName = "";
    private string _basePath = "";
    private bool _isOpen;

    public string? OpenedFilePath { get; private set; }
    public bool IsOpen => _isOpen;
    public int RecordLength => _recordLength;

    public SpecificationManager()
    {
        _firstRecordOffset = Constants.NullPointer;
        _isOpen = false;
    }

    public static bool CheckSignature(string filePath)
    {
        if (!File.Exists(filePath))
            return false;
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (fs.Length < 3) return false;
        var buf = new byte[3];
        fs.Read(buf, 0, 3);
        var sig = Encoding.ASCII.GetString(buf);
        return sig.StartsWith("PS");
    }

    public void Create(string baseFileName, int maxComponentNameLength, string? specFileName = null, bool overwrite = false)
    {
        if (string.IsNullOrWhiteSpace(baseFileName))
            throw new ArgumentException("Имя базового файла не может быть пустым.", nameof(baseFileName));

        _basePath = Path.GetFullPath(baseFileName);
        _recordLength = maxComponentNameLength;
        _specFileName = string.IsNullOrWhiteSpace(specFileName) ? Path.GetFileNameWithoutExtension(_basePath) + ".prs" : Path.GetFileName(specFileName);

        if (File.Exists(_basePath) && !overwrite)
            throw new InvalidOperationException("Файл уже существует. Укажите overwrite=true для перезаписи.");

        using (var pfs = new FileStream(_basePath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var pwr = new BinaryWriter(pfs, Encoding.UTF8, leaveOpen: false))
        {
            pwr.Write(Encoding.ASCII.GetBytes("PS2"));
            pwr.Write((short)0); // зарезервировано
            pwr.Write(ProductsHeaderSize);
            pwr.Write(_recordLength);
            var nameBuf = new byte[Constants.SpecFileNameSize];
            var specNameBytes = Encoding.ASCII.GetBytes(_specFileName);
            Array.Copy(specNameBytes, nameBuf, Math.Min(nameBuf.Length, specNameBytes.Length));
            pwr.Write(nameBuf);

            // заголовок: указатель на первую запись, зарезервированное поле
            pwr.Write(Constants.NullPointer);
            pwr.Write(Constants.NullPointer);
            pwr.Flush();
        }

        var specFull = Path.Combine(Path.GetDirectoryName(_basePath) ?? ".", _specFileName);
        using (var sfs = new FileStream(specFull, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var sw = new BinaryWriter(sfs, Encoding.UTF8, leaveOpen: false))
        {
            sw.Write(Constants.NullPointer); // first ptr (unused)
            sw.Write(SpecHeaderSize); // free ptr
            sw.Flush();
        }
    }

    public void Open(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Имя файла не задано.", nameof(fileName));

        var basePath = Path.GetFullPath(fileName);
        if (!File.Exists(basePath))
            throw new FileNotFoundException("Файл списка компонентов не найден.", basePath);
        if (!CheckSignature(basePath))
            throw new InvalidDataException("Неверный формат файла (сигнатура).");

        _productsStream = new FileStream(basePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        _productsReader = new BinaryReader(_productsStream, Encoding.UTF8, leaveOpen: true);
        _productsWriter = new BinaryWriter(_productsStream, Encoding.UTF8, leaveOpen: true);

        _productsStream.Seek(0, SeekOrigin.Begin);
        _productsReader.ReadBytes(3); // сигнатура "PS2"
        _productsReader.ReadInt16(); // зарезервировано
        _productsReader.ReadInt32(); // размер заголовка
        _recordLength = _productsReader.ReadInt32();
        var nameBytes = _productsReader.ReadBytes(Constants.SpecFileNameSize);
        _specFileName = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0', ' ');

        _firstRecordOffset = _productsReader.ReadInt32();
        _productsReader.ReadInt32(); // зарезервировано

        var specFullPath = Path.Combine(Path.GetDirectoryName(basePath) ?? ".", _specFileName);
        if (!File.Exists(specFullPath))
            throw new FileNotFoundException("Файл спецификаций не найден.", specFullPath);

        _specStream = new FileStream(specFullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        _specReader = new BinaryReader(_specStream, Encoding.UTF8, leaveOpen: true);
        _specWriter = new BinaryWriter(_specStream, Encoding.UTF8, leaveOpen: true);

        _basePath = basePath;
        _isOpen = true;
        OpenedFilePath = basePath;
    }

    public void Close()
    {
        _productsWriter?.Flush();
        _specWriter?.Flush();
        _productsWriter?.Dispose();
        _productsReader?.Dispose();
        _specWriter?.Dispose();
        _specReader?.Dispose();
        _productsStream?.Dispose();
        _specStream?.Dispose();

        _productsWriter = null;
        _productsReader = null;
        _specWriter = null;
        _specReader = null;
        _productsStream = null;
        _specStream = null;
        _isOpen = false;
        OpenedFilePath = null;
    }

    public void InputComponent(string name, ComponentType type)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Имя не может быть пустым.", nameof(name));

        var trimmedName = name.Trim();
        if (trimmedName.Length > _recordLength)
            throw new ArgumentException($"Длина имени не должна превышать {_recordLength} символов.", nameof(name));

        EnsureOpen();

        if (FindComponentOffset(trimmedName) != Constants.NullPointer)
            throw new InvalidOperationException("Компонент с таким именем уже существует.");

        // firstSpec stored as pointer; parts can have empty spec (NullPointer)
        AddProductRecord(trimmedName, type, Constants.NullPointer);
    }

    // overload used by older callers
    public void InputAssembly(string ownerName, string componentName)
    {
        InputAssembly($"{ownerName}/{componentName}");
    }

    // new single-string format used by console parsing
    public void InputAssembly(string ownerAndComponent)
    {
        if (string.IsNullOrWhiteSpace(ownerAndComponent))
            throw new ArgumentException("Аргумент не может быть пустым.", nameof(ownerAndComponent));

        var parts = ownerAndComponent.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            throw new ArgumentException("Формат должен быть Owner/Component.", nameof(ownerAndComponent));

        var ownerName = parts[0].Trim();
        var compName = parts[1].Trim();

        EnsureOpen();

        var ownerOffset = FindComponentOffset(ownerName);
        // ищем комплектующее включая удаленные (например при восстановлении)
        var compOffset = FindComponentOffsetIncludingDeleted(compName);
        if (ownerOffset == Constants.NullPointer)
            throw new InvalidOperationException("Владелец не найден в списке.");
        if (compOffset == Constants.NullPointer)
            throw new InvalidOperationException("Комплектующее не найдено в списке.");

        var ownerRecord = ReadProductRecord(ownerOffset);
        if (ownerRecord.Type == ComponentType.Part)
            throw new InvalidOperationException("Компонент является деталью и не может иметь спецификацию.");

        _specStream!.Seek(0, SeekOrigin.Begin);
        _specReader!.ReadInt32(); // первый указатель в заголовке (не используется)
        var freePtr = _specReader.ReadInt32();

        var newSpecOffset = freePtr;
        _specStream.Seek(newSpecOffset, SeekOrigin.Begin);
        _specWriter!.Write(Constants.DeleteFlagActive);
        _specWriter.Write(compOffset);
        _specWriter.Write((short)1);

        var nextPointer = ownerRecord.FirstSpecOffset;
        _specWriter.Write(nextPointer);

        var updatedFree = GetNextFreeSpecOffset(newSpecOffset);
        _specStream.Seek(sizeof(int), SeekOrigin.Begin);
        _specWriter.Write(updatedFree);

        _productsStream!.Seek(ownerOffset + 1, SeekOrigin.Begin); // позиция после флага удаления
        _productsWriter!.Write(newSpecOffset);

        _productsStream.Flush();
        _specStream.Flush();
    }

    public void DeleteAssembly(string ownerName, string componentName)
    {
        EnsureOpen();
        var ownerOffset = FindComponentOffset(ownerName);
        var compOffset = FindComponentOffset(componentName);
        if (ownerOffset == Constants.NullPointer || compOffset == Constants.NullPointer)
            throw new InvalidOperationException("Владелец или комплектующее не найдено.");

        var ownerRecord = ReadProductRecord(ownerOffset);
        var specOffset = ownerRecord.FirstSpecOffset;
        while (specOffset != Constants.NullPointer)
        {
            _specStream!.Seek(specOffset, SeekOrigin.Begin);
            var del = _specReader!.ReadSByte();
            var ptr = _specReader.ReadInt32();
            var mult = _specReader.ReadInt16();
            var next = _specReader.ReadInt32();
            if (del != Constants.DeleteFlagDeleted && ptr == compOffset)
            {
                _specStream.Seek(specOffset, SeekOrigin.Begin);
                _specWriter!.Write(Constants.DeleteFlagDeleted);
                _specStream.Flush();
                return;
            }
            specOffset = next;
        }
        throw new InvalidOperationException("Комплектующее не найдено в спецификации владельца.");
    }

    public void RestoreAll()
    {
        EnsureOpen();
        // restore products
        var cur = _firstRecordOffset;
        while (cur != Constants.NullPointer)
        {
            _productsStream!.Seek(cur, SeekOrigin.Begin);
            _productsWriter!.Write(Constants.DeleteFlagActive);
            _productsStream.Flush();

            // read next
            _productsStream.Seek(cur + 1 + 4, SeekOrigin.Begin);
            var next = _productsReader!.ReadInt32();
            cur = next;
        }

        // restore specs
        _specStream!.Seek(0, SeekOrigin.Begin);
        _specReader!.ReadInt32();
        var ptr = _specReader.ReadInt32();
        while (ptr < _specStream.Length && ptr >= SpecHeaderSize)
        {
            _specStream!.Seek(ptr, SeekOrigin.Begin);
            _specWriter!.Write(Constants.DeleteFlagActive);
            ptr = GetNextFreeSpecOffset(ptr);
        }
        _specStream.Flush();
    }

    public void Truncate()
    {
        EnsureOpen();
        _productsWriter!.Flush();
        _specWriter!.Flush();
    }

    public List<(string, ComponentType)> PrintAll()
    {
        EnsureOpen();
        var result = new List<(string, ComponentType)>();
        var cur = _firstRecordOffset;
        while (cur != Constants.NullPointer)
        {
            var r = ReadProductRecord(cur);
            // Показываем все компоненты, включая логически удаленные (скрывается на уровне UI)
            result.Add((r.Name, r.Type));
            cur = r.NextOffset;
        }
        return result;
    }

    public string PrintComponent(string name)
    {
        EnsureOpen();
        var offset = FindComponentOffset(name);
        if (offset == Constants.NullPointer)
            throw new InvalidOperationException("Компонент не найден.");
        var rec = ReadProductRecord(offset);
        if (rec.Deleted)
            throw new InvalidOperationException("Компонент удалён.");
        if (rec.Type == ComponentType.Part)
            throw new InvalidOperationException("Для детали эта команда вызывает ошибку.");
        return BuildSpecificationTree(offset, "");
    }

    public void DeleteComponent(string name)
    {
        EnsureOpen();
        var offset = FindComponentOffset(name);
        if (offset == Constants.NullPointer)
            throw new InvalidOperationException("Компонент не найден.");
        // Проверяем, есть ли ссылки на этот компонент в спецификациях
        if (IsReferencedInAnySpec(offset))
            throw new InvalidOperationException("На компонент имеются ссылки в спецификациях других компонентов.");

        _productsStream!.Seek(offset, SeekOrigin.Begin);
        _productsWriter!.Write(Constants.DeleteFlagDeleted);
        _productsStream.Flush();
    }

    public void RestoreComponent(string name)
    {
        EnsureOpen();
        var offset = FindComponentOffsetIncludingDeleted(name);
        if (offset == Constants.NullPointer)
            throw new InvalidOperationException("Компонент не найден.");
        _productsStream!.Seek(offset, SeekOrigin.Begin);
        _productsWriter!.Write(Constants.DeleteFlagActive);
        _productsStream.Flush();
    }

    private void AddProductRecord(string name, ComponentType type, int firstSpec)
    {
        EnsureOpen();

        _productsStream!.Seek(0, SeekOrigin.End);
        var newOffset = (int)_productsStream.Position; // смещение для новой записи

        _productsWriter!.Write(Constants.DeleteFlagActive);
        _productsWriter.Write(firstSpec);
        _productsWriter.Write(Constants.NullPointer); // указатель на следующую запись
        _productsWriter.Write((byte)type); // тип компонента

        var nameBytes = new byte[_recordLength];
        var b = Encoding.UTF8.GetBytes(name);
        Array.Copy(b, nameBytes, Math.Min(b.Length, nameBytes.Length));
        _productsWriter.Write(nameBytes);

        if (_firstRecordOffset == Constants.NullPointer)
        {
            _firstRecordOffset = newOffset;
            // записать firstRecordOffset в заголовок (после signature+reserved+headerSize+recordLength+specName)
            var pos = 3 + 2 + 4 + 4 + Constants.SpecFileNameSize;
            _productsStream.Seek(pos, SeekOrigin.Begin);
            _productsWriter.Write(_firstRecordOffset);
            _productsWriter.Write(Constants.NullPointer);
        }
        else
        {
            var cur = _firstRecordOffset;
            while (true)
            {
                _productsStream!.Seek(cur, SeekOrigin.Begin);
                _productsReader!.ReadSByte();
                _productsReader.ReadInt32();
                var next = _productsReader.ReadInt32();
                if (next == Constants.NullPointer)
                {
                    _productsStream.Seek(cur + 1 + 4, SeekOrigin.Begin);
                    _productsWriter!.Write(newOffset);
                    break;
                }
                cur = next;
            }
        }

        _productsWriter.Flush();
        _productsStream.Flush();
    }

    private int FindComponentOffset(string name)
    {
        EnsureOpen();
        var cur = _firstRecordOffset;
        while (cur != Constants.NullPointer)
        {
            _productsStream!.Seek(cur, SeekOrigin.Begin);
            var del = _productsReader!.ReadSByte();
            var firstSpec = _productsReader.ReadInt32();
            var next = _productsReader.ReadInt32();
            var typeByte = _productsReader.ReadByte();
            var nameBytes = _productsReader.ReadBytes(_recordLength);
            var recName = Encoding.UTF8.GetString(nameBytes).TrimEnd('\0', ' ');
            if (del != Constants.DeleteFlagDeleted && recName.Equals(name, StringComparison.OrdinalIgnoreCase))
                return cur;
            cur = next;
        }
        return Constants.NullPointer;
    }

    // поиск компонента включая удаленные (для восстановления и добавления в спецификацию)
    private int FindComponentOffsetIncludingDeleted(string name)
    {
        EnsureOpen();
        var cur = _firstRecordOffset;
        while (cur != Constants.NullPointer)
        {
            _productsStream!.Seek(cur, SeekOrigin.Begin);
            _productsReader!.ReadSByte(); // skip delete flag
            _productsReader.ReadInt32(); // skip firstSpec
            var next = _productsReader.ReadInt32();
            _productsReader.ReadByte(); // skip type
            var nameBytes = _productsReader.ReadBytes(_recordLength);
            var recName = Encoding.UTF8.GetString(nameBytes).TrimEnd('\0', ' ');
            if (recName.Equals(name, StringComparison.OrdinalIgnoreCase))
                return cur;
            cur = next;
        }
        return Constants.NullPointer;
    }

    private (bool Deleted, int FirstSpecOffset, int NextOffset, string Name, ComponentType Type) ReadProductRecord(int offset)
    {
        EnsureOpen();
        _productsStream!.Seek(offset, SeekOrigin.Begin);
        var del = _productsReader!.ReadSByte();
        var firstSpec = _productsReader.ReadInt32();
        var next = _productsReader.ReadInt32();
        var typeByte = _productsReader.ReadByte();
        var nameBytes = _productsReader.ReadBytes(_recordLength);
        var name = Encoding.UTF8.GetString(nameBytes).TrimEnd('\0', ' ');
        ComponentType type = Enum.IsDefined(typeof(ComponentType), (int)typeByte) ? (ComponentType)typeByte : ComponentType.Part;
        return (del == Constants.DeleteFlagDeleted, firstSpec, next, name, type);
    }

    private int GetNextFreeSpecOffset(int current)
    {
        return current + SpecRecordSize;
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

    private string BuildSpecificationTree(int componentOffset, string prefix)
    {
        var record = ReadProductRecord(componentOffset);
        var sb = new StringBuilder();
        sb.AppendLine(prefix + record.Name);

        var specOffset = record.FirstSpecOffset;
        while (specOffset != Constants.NullPointer)
        {
            _specStream!.Seek(specOffset, SeekOrigin.Begin);
            var del = _specReader!.ReadSByte();
            var ptrProduct = _specReader.ReadInt32();
            _specReader.ReadInt16();
            var next = _specReader.ReadInt32();
            if (del != Constants.DeleteFlagDeleted)
            {
                var child = ReadProductRecord(ptrProduct);
                if (!child.Deleted)
                    sb.Append(BuildSpecificationTree(ptrProduct, prefix + "  "));
            }
            specOffset = next;
        }
        return sb.ToString();
    }

    private void EnsureOpen()
    {
        if (!_isOpen)
            throw new InvalidOperationException("Файлы не открыты. Вызовите Open или Create перед операциями.");
    }
}
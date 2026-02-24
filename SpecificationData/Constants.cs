namespace SpecificationData;

/// <summary>
/// Константы для формата файлов спецификаций.
/// </summary>
internal static class Constants
{
    /// <summary>Сигнатура файлов — два байта 'P' и 'S'.</summary>
    public const ushort Signature = 0x5350; // 'P' (0x50) + 'S' (0x53) в little-endian

    /// <summary>Размер сигнатуры в байтах.</summary>
    public const int SignatureSize = 2;

    /// <summary>Пустое значение указателя (смещение -1).</summary>
    public const int NullPointer = -1;

    /// <summary>Размер указателя в байтах (4 байта — int32).</summary>
    public const int PointerSize = 4;

    /// <summary>Размер бита удаления (1 байт).</summary>
    public const int DeleteFlagSize = 1;

    /// <summary>Максимальная длина имени файла спецификаций в заголовке.</summary>
    public const int SpecFileNameSize = 16;

    /// <summary>Расширение файла списка изделий.</summary>
    public const string ProductsExtension = ".prd";

    /// <summary>Расширение файла спецификаций.</summary>
    public const string SpecsExtension = ".prs";

    /// <summary>Бит удаления: запись активна.</summary>
    public const sbyte DeleteFlagActive = 0;

    /// <summary>Бит удаления: запись помечена на удаление.</summary>
    public const sbyte DeleteFlagDeleted = -1;
}

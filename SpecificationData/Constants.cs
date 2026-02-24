namespace SpecificationData;

/// Константы для формата файлов спецификаций.
internal static class Constants
{
    /// Сигнатура файлов — два байта 'P' и 'S'.
    public const ushort Signature = 0x5350; // 'P' (0x50) + 'S' (0x53) в little-endian

    /// Размер сигнатуры в байтах.
    public const int SignatureSize = 2;

    /// Пустое значение указателя (смещение -1).
    public const int NullPointer = -1;

    /// Размер указателя в байтах (4 байта — int32).
    public const int PointerSize = 4;

    /// Размер бита удаления (1 байт).
    public const int DeleteFlagSize = 1;

    /// Максимальная длина имени файла спецификаций в заголовке.
    public const int SpecFileNameSize = 16;

    /// Расширение файла списка изделий.
    public const string ProductsExtension = ".prd";

    /// Расширение файла спецификаций.
    public const string SpecsExtension = ".prs";

    /// Бит удаления: запись активна.
    public const sbyte DeleteFlagActive = 0;

    /// Бит удаления: запись помечена на удаление.
    public const sbyte DeleteFlagDeleted = -1;
}

namespace SpecificationData;

/// <summary>
/// Исключение при работе с данными спецификаций.
/// </summary>
public class SpecificationException : Exception
{
    public SpecificationException(string message) : base(message) { }

    public SpecificationException(string message, Exception inner) : base(message, inner) { }
}

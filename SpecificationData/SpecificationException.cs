namespace SpecificationData;

/// Исключение при работе с данными спецификаций.
public class SpecificationException : Exception
{
    public SpecificationException(string message) : base(message) { }

    public SpecificationException(string message, Exception inner) : base(message, inner) { }
}

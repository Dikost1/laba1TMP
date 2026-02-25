namespace SpecificationData;

/// Исключение при работе с данными спецификаций.
// Исключение уровня спецификаций — используется для сигнализации об ошибках доменной логики.
public class SpecificationException : Exception
{
    public SpecificationException(string message) : base(message) { }
    public SpecificationException(string message, Exception inner) : base(message, inner) { }
}

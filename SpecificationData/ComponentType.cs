namespace SpecificationData;

/// <summary>
/// Тип компонента в спецификации: Изделие, Узел или Деталь.
/// </summary>
public enum ComponentType
{
    /// <summary>Изделие — может содержать узлы и детали.</summary>
    Product,

    /// <summary>Узел — может содержать узлы и детали.</summary>
    Unit,

    /// <summary>Деталь — конечный элемент без вложенных компонентов.</summary>
    Part
}

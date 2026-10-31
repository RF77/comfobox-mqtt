namespace ComfoBox.Domain.Values;

/// <summary>
/// Der Wert eines Items.
/// </summary>
public abstract record ItemValue
{
    /// <summary>
    /// Der Rohwert des Items (für JSON und Protokollierung).
    /// </summary>
    public abstract object? Raw { get; }
}

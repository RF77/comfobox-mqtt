using ComfoBox.Bacnet.Serial;

namespace ComfoBox.Bacnet.Tests;

/// <summary>
/// Erzeugt <see cref="FakeSerialPort"/>-Attrappen.
/// </summary>
/// <remarks>
/// Zählt die Öffnungsversuche, damit der Reconnect-Algorithmus geprüft
/// werden kann.
/// </remarks>
public sealed class FakeSerialPortFactory : ISerialPortFactory
{
    private readonly List<FakeSerialPort> _created = [];

    /// <summary>Anzahl der erzeugten Ports.</summary>
    public int CreatedCount => _created.Count;

    /// <summary>Der zuletzt erzeugte Port.</summary>
    public FakeSerialPort? Last => _created.Count > 0 ? _created[^1] : null;

    /// <summary>Alle erzeugten Ports.</summary>
    public IReadOnlyList<FakeSerialPort> Created => _created;

    /// <summary>Wird bei jedem Erzeugen angewendet (z. B. Portfehler simulieren).</summary>
    public Action<FakeSerialPort>? Configure { get; set; }

    /// <inheritdoc />
    public ISerialPort Create()
    {
        var port = new FakeSerialPort();
        Configure?.Invoke(port);
        _created.Add(port);
        return port;
    }
}

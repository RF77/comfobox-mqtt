using ComfoBox.Bacnet;
using ComfoBox.Bacnet.Serialize;

namespace ComfoBox.Gateway.Tests;

/// <summary>
/// Ein BACnet-Client-Attrapp.
/// </summary>
/// <remarks>
/// Der Attrapp merkt sich die Reihenfolge der Lesezugriffe und kann für
/// einzelne Objekte einen Fehler oder einen Timeout simulieren.
/// </remarks>
internal sealed class FakeBacnetClient : IBacnetClient
{
    private EventHandler<BacnetIamEventArgs>? _iamReceived;

    /// <summary>Die Objekte, die gelesen wurden - in Reihenfolge.</summary>
    public List<uint> ReadObjects { get; } = [];

    /// <summary>Die Objekte, die geschrieben wurden - in Reihenfolge.</summary>
    public List<uint> WrittenObjects { get; } = [];

    /// <summary>Die Werte je Objekt-Instanz.</summary>
    public Dictionary<uint, float> Values { get; } = [];

    /// <summary>Die Objekte, bei denen der Lesevorgang fehlschlägt.</summary>
    public HashSet<uint> FailingObjects { get; } = [];

    /// <summary>Die Objekte, bei denen der Lesevorgang hängt (Timeout).</summary>
    public HashSet<uint> HangingObjects { get; } = [];

    /// <summary>Wird der Schreibvorgang fehlschlagen?</summary>
    public bool FailWrites { get; set; }

    /// <inheritdoc />
    public int Timeout { get; set; } = 1000;

    /// <inheritdoc />
    public int Retries { get; set; } = 3;

    /// <inheritdoc />
    public int TransmitTimeout { get; set; } = 30000;

    /// <inheritdoc />
    public uint WritePriority { get; set; }

    /// <inheritdoc />
    public event EventHandler<BacnetIamEventArgs>? IamReceived
    {
        add => _iamReceived += value;
        remove => _iamReceived -= value;
    }

    /// <inheritdoc />
    public void Start()
    {
    }

    /// <inheritdoc />
    public Task StopAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public Task WhoIsAsync(int lowLimit = -1, int highLimit = -1, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public async Task<IList<BacnetValue>> ReadPropertyAsync(
        BacnetAddress address,
        BacnetObjectId objectId,
        BacnetPropertyIds propertyId,
        uint arrayIndex = ASN1.BACNET_ARRAY_ALL,
        CancellationToken cancellationToken = default)
    {
        ReadObjects.Add(objectId.Instance);

        if (HangingObjects.Contains(objectId.Instance))
        {
            // Der Timeout muss von aussen kommen - hier wird gewartet.
            await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }

        if (FailingObjects.Contains(objectId.Instance))
        {
            throw new BacnetTimeoutException($"Objekt {objectId.Instance} antwortet nicht.");
        }

        var value = Values.TryGetValue(objectId.Instance, out var known) ? known : 0f;
        return [new BacnetValue(value)];
    }

    /// <inheritdoc />
    public Task WritePropertyAsync(
        BacnetAddress address,
        BacnetObjectId objectId,
        BacnetPropertyIds propertyId,
        IEnumerable<BacnetValue> values,
        CancellationToken cancellationToken = default)
    {
        WrittenObjects.Add(objectId.Instance);

        if (FailWrites)
        {
            throw new BacnetDeviceErrorException(
                BacnetErrorClasses.ERROR_CLASS_DEVICE,
                BacnetErrorCodes.ERROR_CODE_OTHER);
        }

        var value = values.First();
        Values[objectId.Instance] = Convert.ToSingle(value.Value, System.Globalization.CultureInfo.InvariantCulture);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}

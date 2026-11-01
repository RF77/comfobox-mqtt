using Microsoft.Extensions.Logging.Abstractions;

namespace ComfoBox.Bacnet.Tests;

/// <summary>
/// Tests für den BACnet-Client.
/// </summary>
/// <remarks>
/// Der Client wird gegen einen <see cref="FakeTransport"/> getestet - ohne
/// Hardware. Geprüft werden Anfrage/Aufbau, Antwort-Zuordnung und die
/// typisierten Exceptions.
/// </remarks>
public sealed class BacnetClientTests
{
    private static BacnetAddress Device(byte mac = 1) =>
        new(BacnetAddressTypes.MSTP, 0, new[] { mac });

    private static (BacnetClient Client, FakeTransport Transport) CreateClient()
    {
        var transport = new FakeTransport();
        var client = new BacnetClient(transport, NullLogger<BacnetClient>.Instance);
        client.Start();
        return (client, transport);
    }

    [Fact]
    public async Task ReadProperty_Liest_Den_Wert_Und_Gibt_Ihn_Zurueck()
    {
        var (client, transport) = CreateClient();

        try
        {
            var objectId = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, 62);

            transport.Responder = _ => FakeTransport.BuildReadPropertyAcknowledge(
                InvokeIdOf(transport),
                objectId,
                BacnetPropertyIds.PROP_PRESENT_VALUE,
                new BacnetValue(21.5f));

            var values = await client.ReadPropertyAsync(
                Device(),
                objectId,
                BacnetPropertyIds.PROP_PRESENT_VALUE);

            Assert.Single(values);
            Assert.Equal(21.5f, Convert.ToSingle(values[0].Value, System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public async Task ReadProperty_Sendet_Den_Invoke_Id_Und_Die_Objektkennung()
    {
        var (client, transport) = CreateClient();

        try
        {
            var objectId = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, 62);

            transport.Responder = _ => FakeTransport.BuildReadPropertyAcknowledge(
                InvokeIdOf(transport),
                objectId,
                BacnetPropertyIds.PROP_PRESENT_VALUE,
                new BacnetValue(21.5f));

            await client.ReadPropertyAsync(Device(), objectId, BacnetPropertyIds.PROP_PRESENT_VALUE);

            var sent = Assert.Single(transport.Sent);

            // Der Request muss als ReadProperty-Request erkennbar sein.
            var npduLength = ComfoBox.Bacnet.Serialize.NPDU.Decode(
                sent,
                0,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _);

            Assert.True(npduLength > 0);

            var headerLength = ComfoBox.Bacnet.Serialize.APDU.DecodeConfirmedServiceRequest(
                sent,
                npduLength,
                out var type,
                out var service,
                out _,
                out _,
                out var invokeId,
                out _,
                out _);

            Assert.True(headerLength > 0);
            Assert.Equal(BacnetPduTypes.PDU_TYPE_CONFIRMED_SERVICE_REQUEST, type);
            Assert.Equal(BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROPERTY, service);
            Assert.NotEqual(0, invokeId);
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public async Task Fehlerantwort_Wirft_BacnetDeviceErrorException()
    {
        var (client, transport) = CreateClient();

        try
        {
            transport.Responder = _ => FakeTransport.BuildError(
                InvokeIdOf(transport),
                BacnetErrorClasses.ERROR_CLASS_PROPERTY,
                BacnetErrorCodes.ERROR_CODE_UNKNOWN_PROPERTY);

            var exception = await Assert.ThrowsAsync<BacnetDeviceErrorException>(() =>
                client.ReadPropertyAsync(
                    Device(),
                    new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, 62),
                    BacnetPropertyIds.PROP_PRESENT_VALUE));

            Assert.Equal(BacnetErrorClasses.ERROR_CLASS_PROPERTY, exception.ErrorClass);
            Assert.Equal(BacnetErrorCodes.ERROR_CODE_UNKNOWN_PROPERTY, exception.ErrorCode);
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public async Task Abort_Antwort_Wirft_BacnetDeviceAbortException()
    {
        var (client, transport) = CreateClient();

        try
        {
            transport.Responder = _ => FakeTransport.BuildAbort(InvokeIdOf(transport), 3);

            var exception = await Assert.ThrowsAsync<BacnetDeviceAbortException>(() =>
                client.ReadPropertyAsync(
                    Device(),
                    new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, 62),
                    BacnetPropertyIds.PROP_PRESENT_VALUE));

            Assert.Equal(3, exception.Reason);
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public async Task Keine_Antwort_Wirft_BacnetTimeoutException()
    {
        var (client, transport) = CreateClient();

        try
        {
            client.Timeout = 100;
            client.Retries = 2;

            // Kein Responder -> es kommt keine Antwort.
            await Assert.ThrowsAsync<BacnetTimeoutException>(() =>
                client.ReadPropertyAsync(
                    Device(),
                    new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, 62),
                    BacnetPropertyIds.PROP_PRESENT_VALUE));

            // Zwei Versuche (Retries = 2).
            Assert.Equal(2, transport.Sent.Count);
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public async Task WriteProperty_Wird_Gesendet_Und_Bestaetigt()
    {
        var (client, transport) = CreateClient();

        try
        {
            var objectId = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, 62);

            // Die Antwort auf ein WriteProperty ist ein SimpleAck.
            transport.Responder = _ => FakeTransport.BuildSimpleAck(InvokeIdOf(transport));

            await client.WritePropertyAsync(
                Device(),
                objectId,
                BacnetPropertyIds.PROP_PRESENT_VALUE,
                new[] { new BacnetValue(21.5f) });

            var sent = Assert.Single(transport.Sent);
            Assert.True(sent.Length > 0);
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public async Task IAm_Loest_Das_Ereignis_Aus()
    {
        var (client, transport) = CreateClient();

        try
        {
            // Nicht auf eine feste Zeit warten: auf einem belasteten
            // CI-Läufer waren 50 ms nicht immer genug, das Ereignis kam
            // danach an und die Zusicherung schlug fehl.
            var tcs = new TaskCompletionSource<BacnetIamEventArgs>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            client.IamReceived += (_, args) => tcs.TrySetResult(args);

            transport.Respond(FakeTransport.BuildIam(deviceId: 7));

            var fertig = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.Same(tcs.Task, fertig);

            var received = await tcs.Task;

            Assert.NotNull(received);
            Assert.Equal(7u, received.DeviceId);
            Assert.Equal(260, received.VendorId);
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public async Task WhoIs_Wird_Gesendet_Ohne_Eine_Antwort_Zu_Erwarten()
    {
        var (client, transport) = CreateClient();

        try
        {
            await client.WhoIsAsync();

            var sent = Assert.Single(transport.Sent);

            var npduLength = ComfoBox.Bacnet.Serialize.NPDU.Decode(
                sent,
                0,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _);

            var headerLength = ComfoBox.Bacnet.Serialize.APDU.DecodeUnconfirmedServiceRequest(
                sent,
                npduLength,
                out _,
                out var service);

            Assert.True(headerLength > 0);
            Assert.Equal(BacnetUnconfirmedServices.SERVICE_UNCONFIRMED_WHO_IS, service);
        }
        finally
        {
            client.Dispose();
        }
    }

    /// <summary>
    /// Liest die Invoke-ID aus dem zuletzt gesendeten Request.
    /// </summary>
    private static byte InvokeIdOf(FakeTransport transport)
    {
        var sent = transport.Sent[transport.Sent.Count - 1];

        var npduLength = ComfoBox.Bacnet.Serialize.NPDU.Decode(
            sent,
            0,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _);

        ComfoBox.Bacnet.Serialize.APDU.DecodeConfirmedServiceRequest(
            sent,
            npduLength,
            out _,
            out _,
            out _,
            out _,
            out var invokeId,
            out _,
            out _);

        return invokeId;
    }
}

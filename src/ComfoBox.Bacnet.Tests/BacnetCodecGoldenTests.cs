using System.Globalization;
using System.Text.Json;
using ComfoBox.Bacnet;
using ComfoBox.Bacnet.Serialize;

namespace ComfoBox.Bacnet.Tests;

/// <summary>
/// Golden-Tests für den portierten BACnet-Codec.
/// </summary>
/// <remarks>
/// Die Frames stammen aus dem Legacy-Codec. Der Test dekodiert jeden Frame und
/// vergleicht das Ergebnis mit dem erwarteten Wert aus der Fixture.
/// Ein Fehlschlag bedeutet: das Verhalten hat sich gegenüber dem Legacy geändert.
/// </remarks>
public sealed class BacnetCodecGoldenTests
{
    public static IEnumerable<object[]> AllFrames()
    {
        foreach (var frame in BacnetFrames.All)
        {
            yield return
            [
                frame.GetProperty("name").GetString()!,
                frame.GetProperty("kind").GetString()!,
                frame.GetProperty("raw").GetString()!,
            ];
        }
    }

    [Fact]
    public void Fixture_Contains_At_Least_Twenty_Frames()
    {
        Assert.True(BacnetFrames.All.Count() >= 20,
            $"Die Fixture muss mindestens 20 Frames enthalten, hat aber {BacnetFrames.All.Count()}.");
    }

    [Theory]
    [MemberData(nameof(AllFrames))]
    public void Frame_Decodes_As_Expected(string name, string kind, string raw)
    {
        var frame = BacnetFrames.All.Single(f => f.GetProperty("name").GetString() == name);
        var expect = frame.GetProperty("expect");
        var bytes = BacnetFrames.ParseHex(raw);

        switch (kind)
        {
            case "iam":
                AssertIam(bytes, expect);
                break;
            case "whois":
                AssertWhoIs(bytes, expect);
                break;
            case "readProperty":
                AssertReadProperty(bytes, expect);
                break;
            case "writeProperty":
                AssertWriteProperty(bytes, expect);
                break;
            case "readPropertyAcknowledge":
                AssertReadPropertyAcknowledge(bytes, expect);
                break;
            case "error":
                AssertError(bytes, expect);
                break;
            case "confirmedRequest":
                AssertConfirmedRequest(bytes, expect);
                break;
            case "unconfirmedRequest":
                AssertUnconfirmedRequest(bytes, expect);
                break;
            case "simpleAck":
                AssertSimpleAck(bytes, expect);
                break;
            case "complexAck":
                AssertComplexAck(bytes, expect);
                break;
            case "apduError":
                AssertApduError(bytes, expect);
                break;
            case "abort":
                AssertAbort(bytes, expect);
                break;
            case "segmentAck":
                AssertSegmentAck(bytes, expect);
                break;
            case "npdu":
                AssertNpdu(bytes, expect);
                break;
            case "addressMac":
            case "addressIp":
            case "addressDevice":
                AssertAddress(bytes, expect);
                break;
            default:
                Assert.Fail($"Unbekannte Frame-Art '{kind}'.");
                break;
        }
    }

    private static void AssertIam(byte[] bytes, JsonElement expect)
    {
        var length = Services.DecodeIamBroadcast(bytes, 0, out var deviceId, out var maxApdu,
            out var segmentation, out var vendorId);

        // Eigenheit des Legacy-Codecs: DecodeIamBroadcast gibt bei Erfolg 0
        // zurück ("offset - org_offset"), bei Fehlern -1.
        Assert.True(length >= 0, "DecodeIamBroadcast muss erfolgreich dekodieren.");
        Assert.Equal(expect.GetProperty("deviceId").GetUInt32(), deviceId);
        Assert.Equal(expect.GetProperty("maxApdu").GetUInt32(), maxApdu);
        Assert.Equal(expect.GetProperty("segmentation").GetString(), segmentation.ToString());
        Assert.Equal(expect.GetProperty("vendorId").GetUInt32(), vendorId);
    }

    private static void AssertWhoIs(byte[] bytes, JsonElement expect)
    {
        var length = Services.DecodeWhoIsBroadcast(bytes, 0, bytes.Length, out var low, out var high);

        Assert.True(length > 0);
        Assert.Equal(expect.GetProperty("lowLimit").GetInt32(), low);
        Assert.Equal(expect.GetProperty("highLimit").GetInt32(), high);
    }

    private static void AssertReadProperty(byte[] bytes, JsonElement expect)
    {
        var length = Services.DecodeReadProperty(bytes, 0, bytes.Length, out var objectId, out var property);

        Assert.True(length > 0);
        Assert.Equal(expect.GetProperty("objectType").GetString(), objectId.type.ToString());
        Assert.Equal(expect.GetProperty("instance").GetUInt32(), objectId.instance);
        Assert.Equal(expect.GetProperty("propertyId").GetUInt32(), property.propertyIdentifier);
        Assert.Equal(expect.GetProperty("arrayIndex").GetUInt32(), property.propertyArrayIndex);
    }

    private static void AssertWriteProperty(byte[] bytes, JsonElement expect)
    {
        var length = Services.DecodeWriteProperty(bytes, 0, bytes.Length, out var objectId, out var value);

        Assert.True(length > 0);
        Assert.Equal(expect.GetProperty("objectType").GetString(), objectId.type.ToString());
        Assert.Equal(expect.GetProperty("instance").GetUInt32(), objectId.instance);
        Assert.Equal(expect.GetProperty("propertyId").GetUInt32(), value.property.propertyIdentifier);
        Assert.Equal(expect.GetProperty("priority").GetByte(), value.priority);
        Assert.Equal(expect.GetProperty("value").GetString(),
            Convert.ToString(value.value[0].Value, CultureInfo.InvariantCulture));
    }

    private static void AssertReadPropertyAcknowledge(byte[] bytes, JsonElement expect)
    {
        var length = Services.DecodeReadPropertyAcknowledge(bytes, 0, bytes.Length, out var objectId,
            out var property, out var values);

        Assert.True(length > 0);
        Assert.Equal(expect.GetProperty("objectType").GetString(), objectId.type.ToString());
        Assert.Equal(expect.GetProperty("instance").GetUInt32(), objectId.instance);
        Assert.Equal(expect.GetProperty("propertyId").GetUInt32(), property.propertyIdentifier);
        Assert.Equal(expect.GetProperty("arrayIndex").GetUInt32(), property.propertyArrayIndex);
        Assert.Equal(expect.GetProperty("value").GetString(),
            Convert.ToString(values[0].Value, CultureInfo.InvariantCulture));
    }

    private static void AssertError(byte[] bytes, JsonElement expect)
    {
        var length = Services.DecodeError(bytes, 0, bytes.Length, out var errorClass, out var errorCode);

        Assert.True(length > 0);
        Assert.Equal(expect.GetProperty("errorClass").GetString(), errorClass.ToString());
        Assert.Equal(expect.GetProperty("errorCode").GetString(), errorCode.ToString());
    }

    private static void AssertConfirmedRequest(byte[] bytes, JsonElement expect)
    {
        var length = APDU.DecodeConfirmedServiceRequest(bytes, 0, out var type, out var service,
            out var maxSegments, out var maxApdu, out var invokeId, out var sequenceNumber,
            out var proposedWindow);

        Assert.True(length > 0);
        Assert.Equal(expect.GetProperty("type").GetString(), type.ToString());
        Assert.Equal(expect.GetProperty("service").GetString(), service.ToString());
        Assert.Equal(expect.GetProperty("maxSegments").GetString(), maxSegments.ToString());
        Assert.Equal(expect.GetProperty("maxApdu").GetString(), maxApdu.ToString());
        Assert.Equal(expect.GetProperty("invokeId").GetByte(), invokeId);
        Assert.Equal(expect.GetProperty("sequenceNumber").GetByte(), sequenceNumber);
        Assert.Equal(expect.GetProperty("proposedWindow").GetByte(), proposedWindow);
    }

    private static void AssertUnconfirmedRequest(byte[] bytes, JsonElement expect)
    {
        var length = APDU.DecodeUnconfirmedServiceRequest(bytes, 0, out var type, out var service);

        Assert.True(length > 0);
        Assert.Equal(expect.GetProperty("type").GetString(), type.ToString());
        Assert.Equal(expect.GetProperty("service").GetString(), service.ToString());
    }

    private static void AssertSimpleAck(byte[] bytes, JsonElement expect)
    {
        var length = APDU.DecodeSimpleAck(bytes, 0, out var type, out var service, out var invokeId);

        Assert.True(length > 0);
        Assert.Equal(expect.GetProperty("type").GetString(), type.ToString());
        Assert.Equal(expect.GetProperty("service").GetString(), service.ToString());
        Assert.Equal(expect.GetProperty("invokeId").GetByte(), invokeId);
    }

    private static void AssertComplexAck(byte[] bytes, JsonElement expect)
    {
        var length = APDU.DecodeComplexAck(bytes, 0, out var type, out var service, out var invokeId,
            out var sequenceNumber, out var proposedWindow);

        Assert.True(length > 0);
        Assert.Equal(expect.GetProperty("type").GetString(), type.ToString());
        Assert.Equal(expect.GetProperty("service").GetString(), service.ToString());
        Assert.Equal(expect.GetProperty("invokeId").GetByte(), invokeId);
        Assert.Equal(expect.GetProperty("sequenceNumber").GetByte(), sequenceNumber);
        Assert.Equal(expect.GetProperty("proposedWindow").GetByte(), proposedWindow);
    }

    private static void AssertApduError(byte[] bytes, JsonElement expect)
    {
        var length = APDU.DecodeError(bytes, 0, out var type, out var service, out var invokeId);

        Assert.True(length > 0);
        Assert.Equal(expect.GetProperty("type").GetString(), type.ToString());
        Assert.Equal(expect.GetProperty("service").GetString(), service.ToString());
        Assert.Equal(expect.GetProperty("invokeId").GetByte(), invokeId);
    }

    private static void AssertAbort(byte[] bytes, JsonElement expect)
    {
        var length = APDU.DecodeAbort(bytes, 0, out var type, out var invokeId, out var reason);

        Assert.True(length > 0);
        Assert.Equal(expect.GetProperty("type").GetString(), type.ToString());
        Assert.Equal(expect.GetProperty("invokeId").GetByte(), invokeId);
        Assert.Equal(expect.GetProperty("reason").GetByte(), reason);
    }

    private static void AssertSegmentAck(byte[] bytes, JsonElement expect)
    {
        var length = APDU.DecodeSegmentAck(bytes, 0, out var type, out var originalInvokeId,
            out var sequenceNumber, out var actualWindowSize);

        Assert.True(length > 0);
        Assert.Equal(expect.GetProperty("type").GetString(), type.ToString());
        Assert.Equal(expect.GetProperty("originalInvokeId").GetByte(), originalInvokeId);
        Assert.Equal(expect.GetProperty("sequenceNumber").GetByte(), sequenceNumber);
        Assert.Equal(expect.GetProperty("actualWindowSize").GetByte(), actualWindowSize);
    }

    private static void AssertNpdu(byte[] bytes, JsonElement expect)
    {
        var length = NPDU.Decode(bytes, 0, out var function, out _, out var source, out var hopCount,
            out var networkMessageType, out _);

        Assert.True(length > 0);
        Assert.Equal(expect.GetProperty("function").GetString(), function.ToString());
        Assert.Equal(expect.GetProperty("hopCount").GetByte(), hopCount);
        Assert.Equal(expect.GetProperty("networkMessageType").GetString(), networkMessageType.ToString());
        Assert.Equal(expect.GetProperty("source").GetString(), source?.ToString() ?? string.Empty);
    }

    private static void AssertAddress(byte[] bytes, JsonElement expect)
    {
        // Die Adresse wird mit dem portierten Codec erneut kodiert - das
        // Ergebnis muss byteweise dem Legacy-Frame entsprechen.
        var adr = BacnetFrames.ParseHex(expect.GetProperty("adr").GetString()!);
        var address = new BacnetAddress(
            Enum.Parse<BacnetAddressTypes>(expect.GetProperty("type").GetString()!),
            (ushort)expect.GetProperty("net").GetUInt32(),
            adr);

        var buffer = new EncodeBuffer();
        address.ASN1encode(buffer);

        var encoded = buffer.ToArray().Take(buffer.offset).ToArray();
        Assert.Equal(bytes, encoded);
    }
}

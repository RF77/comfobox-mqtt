using ComfoBox.Bacnet.Serialize;
using Microsoft.Extensions.Logging.Abstractions;

namespace ComfoBox.Bacnet.Tests;

/// <summary>
/// Der MS/TP-Frame-Typ entscheidet, ob die Gegenstelle antworten darf.
/// </summary>
/// <remarks>
/// <para>
/// <b>Regression (2026-11-14):</b> Lesen funktionierte, Schreiben lief immer in
/// die Zeitüberschreitung. Ursache war kein BACnet-Fehler, sondern der
/// MS/TP-Frame-Typ: der Transport leitet ihn aus dem NPDU-Bit
/// <see cref="BacnetNpduControls.ExpectingReply"/> ab. Der Schreibpfad setzte
/// das Bit nicht - der Request ging als
/// <c>FRAME_TYPE_BACNET_DATA_NOT_EXPECTING_REPLY</c> auf den Bus, und die
/// Gegenstelle **darf** darauf nicht antworten (ASHRAE 135, MS/TP).
/// </para>
/// <para>
/// Die Tests hier prüfen genau diese Zuordnung - ohne Hardware, mit einem
/// nachgebauten seriellen Port.
/// </para>
/// </remarks>
public sealed class MstpFrameTypeTests
{
    /// <summary>Ein BACnet-Frame, der eine Antwort verlangt, wird als solcher gesendet.</summary>
    [Fact]
    public async Task Ein_Request_Mit_ExpectingReply_Wird_Als_Solcher_Gesendet()
    {
        var frameType = await SendAndInspectAsync(BacnetNpduControls.ExpectingReply);

        Assert.Equal(BacnetMstpFrameTypes.FRAME_TYPE_BACNET_DATA_EXPECTING_REPLY, frameType);
    }

    /// <summary>Ohne das Bit wird "keine Antwort erwartet" gesendet - das war der Fehler.</summary>
    [Fact]
    public async Task Ein_Request_Ohne_ExpectingReply_Wird_Als_Nicht_Erwartend_Gesendet()
    {
        var frameType = await SendAndInspectAsync(BacnetNpduControls.PriorityNormalMessage);

        Assert.Equal(BacnetMstpFrameTypes.FRAME_TYPE_BACNET_DATA_NOT_EXPECTING_REPLY, frameType);
    }

    /// <summary>
    /// Ein WriteProperty des Clients fordert eine Antwort an.
    /// </summary>
    /// <remarks>
    /// Der Test greift auf der Ebene des Clients: er prüft das NPDU-Bit des
    /// gesendeten Frames. Damit ist die Ursache vom 2026-11-14 dauerhaft
    /// abgesichert - ohne diesen Test wäre derselbe Fehler beim nächsten
    /// "1:1-Portieren" wieder da.
    /// </remarks>
    [Fact]
    public async Task WriteProperty_Setzt_ExpectingReply()
    {
        var transport = new FakeTransport
        {
            Responder = _ => FakeTransport.BuildSimpleAck(1),
        };

        var client = new BacnetClient(transport, NullLogger<BacnetClient>.Instance) { Retries = 1 };
        client.Start();

        try
        {
            await client.WritePropertyAsync(
                new BacnetAddress(BacnetAddressTypes.MSTP, 0, [1]),
                new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, 62),
                BacnetPropertyIds.PROP_PRESENT_VALUE,
                [new BacnetValue(21.5f)]);

            var sent = Assert.Single(transport.Sent);
            var function = NPDU.DecodeFunction(sent, 0);

            Assert.True(
                (function & BacnetNpduControls.ExpectingReply) == BacnetNpduControls.ExpectingReply,
                $"Das NPDU-Control des Schreibvorgangs ist 0x{(byte)function:X2} - das ExpectingReply-Bit fehlt. "
                + "Der MS/TP-Transport sendet den Frame dann als 'keine Antwort erwartet', "
                + "und die ComfoBox antwortet nicht.");
        }
        finally
        {
            client.Dispose();
        }
    }

    /// <summary>
    /// Baut einen Request, sendet ihn und liefert den entstandenen MS/TP-Frame-Typ.
    /// </summary>
    private static async Task<BacnetMstpFrameTypes> SendAndInspectAsync(BacnetNpduControls controls)
    {
        var factory = new FakeSerialPortFactory();
        var transport = new BacnetMstpTransport(
            factory,
            new BacnetMstpOptions { ThisStation = 3, StateLogging = false },
            NullLogger<BacnetMstpTransport>.Instance);

        try
        {
            transport.Start();

            var port = await WaitForPortAsync(factory);

            // Nutzdaten mit einer unverwechselbaren Kennung, damit sie im
            // geschriebenen Byte-Strom wiedergefunden werden können.
            var payload = new byte[] { 0x01, (byte)controls, 0xAA, 0xBB, 0xCC, 0xDD };

            var send = transport.SendAsync(
                payload,
                0,
                payload.Length,
                new BacnetAddress(BacnetAddressTypes.MSTP, 0, [1]),
                waitForTransmission: false,
                timeout: 0,
                CancellationToken.None);

            // Der Automat sendet nur, wenn er den Token hat.
            port.Enqueue(BuildToken(destination: 3, source: 1));

            await send.WaitAsync(TimeSpan.FromSeconds(5));

            return await WaitForFrameTypeAsync(port, payload);
        }
        finally
        {
            await transport.StopAsync();
        }
    }

    /// <summary>Wartet, bis der Transport den Port geöffnet hat.</summary>
    private static async Task<FakeSerialPort> WaitForPortAsync(FakeSerialPortFactory factory)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (DateTime.UtcNow < deadline)
        {
            if (factory.Last is { } port)
            {
                return port;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("Der Transport hat den Port nicht geöffnet.");
    }

    /// <summary>
    /// Sucht die Nutzdaten im geschriebenen Strom und liest den Frame-Typ davor.
    /// </summary>
    private static async Task<BacnetMstpFrameTypes> WaitForFrameTypeAsync(FakeSerialPort port, byte[] payload)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (DateTime.UtcNow < deadline)
        {
            var written = port.Written;
            var index = IndexOf(written, payload);

            if (index >= MSTP.MSTP_HEADER_LENGTH)
            {
                // Der Frame-Typ steht im MS/TP-Header, also
                // MSTP_HEADER_LENGTH Bytes **vor** den Nutzdaten
                // (Header: Präambel 2 + Typ 1 + Ziel 1 + Quelle 1 + Länge 2 +
                // Header-CRC 1).
                return (BacnetMstpFrameTypes)written[index - MSTP.MSTP_HEADER_LENGTH + 2];
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("Der Frame wurde nicht gesendet.");
    }

    /// <summary>Sucht eine Bytefolge.</summary>
    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i + needle.Length <= haystack.Length; i++)
        {
            var found = true;

            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    found = false;
                    break;
                }
            }

            if (found)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Baut einen Token-Frame.</summary>
    private static byte[] BuildToken(byte destination, byte source)
    {
        var frame = new byte[MSTP.MSTP_HEADER_LENGTH];
        MSTP.Encode(frame, 0, BacnetMstpFrameTypes.FRAME_TYPE_TOKEN, destination, source, 0);
        return frame;
    }
}

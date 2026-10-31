using ComfoBox.Bacnet.Serialize;

namespace ComfoBox.Bacnet.Tests;

/// <summary>
/// Tests für das MS/TP-Framing (Header, CRC, Zustandsautomat).
/// </summary>
/// <remarks>
/// Der Zustandsautomat ist die 1:1-Übernahme des Legacy-Codes. Diese Tests
/// prüfen das Framing ohne Hardware: die Bytes werden von Hand in eine
/// <see cref="FakeSerialPort"/>-Attrappe geschrieben.
/// </remarks>
public sealed class MstpFramingTests
{
    /// <summary>
    /// Baut einen MS/TP-Frame (Header + Nutzdaten + CRC).
    /// </summary>
    /// <remarks>
    /// Die Reihenfolge ist wichtig: <see cref="MSTP.Encode"/> schreibt den
    /// Header und berechnet die Daten-CRC über die Nutzdaten - die Nutzdaten
    /// müssen also vor dem Aufruf im Puffer stehen.
    /// </remarks>
    private static byte[] BuildFrame(
        BacnetMstpFrameTypes frameType,
        byte destination,
        byte source,
        byte[]? payload)
    {
        var length = payload?.Length ?? 0;
        var frame = new byte[MSTP.MSTP_HEADER_LENGTH + length + (length > 0 ? 2 : 0)];

        if (payload is not null)
        {
            Array.Copy(payload, 0, frame, MSTP.MSTP_HEADER_LENGTH, payload.Length);
        }

        MSTP.Encode(frame, 0, frameType, destination, source, length);

        return frame;
    }

    [Fact]
    public void Encode_Decode_Liefert_Die_Originalwerte_Zurueck()
    {
        var payload = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var frame = BuildFrame(BacnetMstpFrameTypes.FRAME_TYPE_BACNET_DATA_NOT_EXPECTING_REPLY, 3, 1, payload);

        var length = MSTP.Decode(
            frame,
            0,
            frame.Length,
            out var frameType,
            out var destination,
            out var source,
            out var messageLength);

        Assert.True(length > 0, "Der Frame muss dekodierbar sein.");
        Assert.Equal(BacnetMstpFrameTypes.FRAME_TYPE_BACNET_DATA_NOT_EXPECTING_REPLY, frameType);
        Assert.Equal(3, destination);
        Assert.Equal(1, source);
        Assert.Equal(payload.Length, messageLength);
    }

    [Fact]
    public void Encode_Decode_Ohne_Nutzdaten()
    {
        var frame = BuildFrame(BacnetMstpFrameTypes.FRAME_TYPE_TOKEN, 3, 0, null);

        var length = MSTP.Decode(
            frame,
            0,
            frame.Length,
            out var frameType,
            out _,
            out _,
            out var messageLength);

        Assert.True(length > 0);
        Assert.Equal(BacnetMstpFrameTypes.FRAME_TYPE_TOKEN, frameType);
        Assert.Equal(0, messageLength);
        Assert.Equal(MSTP.MSTP_HEADER_LENGTH, frame.Length);
    }

    [Fact]
    public void Decode_Mit_Manipuliertem_Daten_Crc_Schlaegt_Fehl()
    {
        var payload = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var frame = BuildFrame(BacnetMstpFrameTypes.FRAME_TYPE_BACNET_DATA_NOT_EXPECTING_REPLY, 3, 1, payload);

        // Letztes Datenbyte verfälschen -> CRC stimmt nicht mehr.
        frame[MSTP.MSTP_HEADER_LENGTH + payload.Length - 1] ^= 0xFF;

        var length = MSTP.Decode(
            frame,
            0,
            frame.Length,
            out _,
            out _,
            out _,
            out _);

        Assert.True(length < 0, "Ein fehlerhafter CRC muss abgewiesen werden.");
    }

    [Fact]
    public void Decode_Mit_Manipuliertem_Header_Crc_Schlaegt_Fehl()
    {
        var frame = BuildFrame(BacnetMstpFrameTypes.FRAME_TYPE_TOKEN, 3, 0, null);

        // Zieladresse verfälschen -> Header-CRC stimmt nicht mehr.
        frame[3] ^= 0xFF;

        var length = MSTP.Decode(
            frame,
            0,
            frame.Length,
            out _,
            out _,
            out _,
            out _);

        Assert.True(length < 0, "Ein fehlerhafter Header-CRC muss abgewiesen werden.");
    }

    [Fact]
    public void Decode_Ohne_Preamble_Schlaegt_Fehl()
    {
        var frame = BuildFrame(BacnetMstpFrameTypes.FRAME_TYPE_TOKEN, 3, 0, null);
        frame[0] = 0x00;

        var length = MSTP.Decode(
            frame,
            0,
            frame.Length,
            out _,
            out _,
            out _,
            out _);

        Assert.True(length < 0);
    }

    [Fact]
    public async Task Zustandsautomat_Stellt_Einen_Empfangenen_Frame_Bereit()
    {
        // Der Automat muss einen eingehenden BACnet-Frame an die obere Schicht
        // weiterreichen.
        var port = new FakeSerialPort();
        var options = new BacnetMstpOptions { ThisStation = 3, StateLogging = false };
        var queue = new System.Collections.Concurrent.ConcurrentQueue<MstpOutboundFrame>();

        var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

        var machine = new MstpStateMachine(
            port,
            options,
            queue,
            (payload, _) => received.TrySetResult(payload),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Der Frame muss vor dem Start des Automaten bereitliegen: Der
        // Automat liest im Idle-Zustand und würde einen später eintreffenden
        // Frame in der Poll-For-Master-Runde verschlucken.
        var payload = new byte[] { 0x01, 0x0C, 0x00, 0x80 };
        var frame = BuildFrame(
            BacnetMstpFrameTypes.FRAME_TYPE_BACNET_DATA_NOT_EXPECTING_REPLY,
            destination: 3,
            source: 1,
            payload);

        port.Enqueue(frame);

        var runner = Task.Run(() => machine.Run(cts.Token));

        try
        {
            var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));

            Assert.Equal(payload, result);
        }
        finally
        {
            await cts.CancelAsync();
            await runner.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}

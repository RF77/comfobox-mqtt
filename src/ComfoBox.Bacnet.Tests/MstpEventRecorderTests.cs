namespace ComfoBox.Bacnet.Tests;

/// <summary>
/// Tests für den Ringpuffer der MS/TP-Ereignisse.
/// </summary>
/// <remarks>
/// Der Rekorder wird im Hot-Path des Zustandsautomaten gefüllt. Deshalb
/// werden hier zwei Dinge geprüft: dass die Zähler stimmen und dass die
/// Schreibseite **nicht** allokiert.
/// </remarks>
public sealed class MstpEventRecorderTests
{
    /// <summary>
    /// Die Zähler zählen je Ereignisart.
    /// </summary>
    [Fact]
    public void Die_Zaehler_Zaehlen_Je_Ereignisart()
    {
        var recorder = new MstpEventRecorder();

        recorder.OnMstpEvent(MstpEventKind.TokenReceived, 1, 0);
        recorder.OnMstpEvent(MstpEventKind.TokenReceived, 2, 0);
        recorder.OnMstpEvent(MstpEventKind.TokenPassed, 2, 0);

        var snapshot = recorder.Snapshot();

        Assert.Equal(3, snapshot.TotalEvents);
        Assert.Equal(2, snapshot.Counters[MstpEventKind.TokenReceived]);
        Assert.Equal(1, snapshot.Counters[MstpEventKind.TokenPassed]);
        Assert.Equal(0, snapshot.Counters[MstpEventKind.DataReceived]);
    }

    /// <summary>
    /// Die gesehenen Stationen werden erfasst.
    /// </summary>
    [Fact]
    public void Die_Gesehenen_Stationen_Werden_Erfasst()
    {
        var recorder = new MstpEventRecorder();

        recorder.OnMstpEvent(MstpEventKind.TokenReceived, 1, 0);
        recorder.OnMstpEvent(MstpEventKind.TokenReceived, 3, 0);
        recorder.OnMstpEvent(MstpEventKind.TokenReceived, 1, 0);

        var snapshot = recorder.Snapshot();

        Assert.Equal([1, 3], snapshot.StationsSeen);
    }

    /// <summary>
    /// Der Ringpuffer behält die **letzten** Ereignisse - ältere fallen
    /// heraus.
    /// </summary>
    [Fact]
    public void Der_Ringpuffer_Behaelt_Die_Letzten_Ereignisse()
    {
        var recorder = new MstpEventRecorder(capacity: 16);

        for (var i = 0; i < 40; i++)
        {
            recorder.OnMstpEvent(MstpEventKind.TokenReceived, (byte)(i % 200), (byte)i);
        }

        var snapshot = recorder.Snapshot(maxEvents: 100);

        Assert.Equal(40, snapshot.TotalEvents);
        Assert.Equal(16, snapshot.Events.Count);

        // Das älteste im Ring ist Ereignis 24, das neueste ist 39.
        Assert.Equal(24, snapshot.Events[0].FrameType);
        Assert.Equal(39, snapshot.Events[^1].FrameType);
    }

    /// <summary>
    /// Ein Reset leert die Zähler und den Ringpuffer.
    /// </summary>
    [Fact]
    public void Ein_Reset_Leert_Zaehler_Und_Ringpuffer()
    {
        var recorder = new MstpEventRecorder();

        recorder.OnMstpEvent(MstpEventKind.TokenReceived, 1, 0);
        recorder.Reset();

        var snapshot = recorder.Snapshot();

        Assert.Equal(0, snapshot.TotalEvents);
        Assert.Empty(snapshot.Events);
        Assert.Empty(snapshot.StationsSeen);
    }

    /// <summary>
    /// Die Schreibseite allokiert **nicht** - das ist entscheidend, weil der
    /// Zustandsautomat im Hot-Path des Tokens läuft.
    /// </summary>
    [Fact]
    public void Die_Schreibseite_Allokiert_Nicht()
    {
        var recorder = new MstpEventRecorder();

        // Warmlauf: die JIT kompiliert den Aufruf.
        for (var i = 0; i < 1000; i++)
        {
            recorder.OnMstpEvent(MstpEventKind.FrameReceived, 1, 0);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < 10_000; i++)
        {
            recorder.OnMstpEvent(MstpEventKind.FrameReceived, 1, 0);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    /// <summary>
    /// Die Zeitstempel sind monoton und in der Nähe der Uhrzeit.
    /// </summary>
    [Fact]
    public void Die_Zeitstempel_Sind_In_Der_Naehe_Der_Uhrzeit()
    {
        var recorder = new MstpEventRecorder();

        recorder.OnMstpEvent(MstpEventKind.TokenReceived, 1, 0);

        var snapshot = recorder.Snapshot();
        var evt = snapshot.Events[0];

        var delta = Math.Abs((DateTimeOffset.UtcNow - evt.TimestampUtc).TotalSeconds);

        Assert.True(delta < 5, $"Der Zeitstempel weicht {delta} s von der Uhrzeit ab.");
    }
}

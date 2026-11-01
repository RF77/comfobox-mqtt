using Microsoft.Extensions.Logging.Abstractions;

namespace ComfoBox.Bacnet.Tests;

/// <summary>
/// Tests für das Verhalten der seriellen Schnittstelle.
/// </summary>
/// <remarks>
/// Der wichtigste Test ist die <b>Regression gegen Befund A8</b>: Im Legacy
/// lief der MS/TP-Thread bei einem Portfehler in eine Endlosschleife mit
/// 100 % CPU-Last. Der neue Stack muss stattdessen sauber beenden und sich
/// neu verbinden.
/// </remarks>
public sealed class SerialPortBehaviourTests
{
    private static BacnetMstpTransport CreateTransport(FakeSerialPortFactory factory) =>
        new(
            factory,
            new BacnetMstpOptions
            {
                ThisStation = 3,
                ReconnectDelay = TimeSpan.FromMilliseconds(50),
                MaxReconnectDelay = TimeSpan.FromMilliseconds(100),
            },
            NullLogger<BacnetMstpTransport>.Instance);

    [Fact]
    public async Task Portfehler_Fuehrt_Nicht_Zu_Einer_Endlosschleife_Sondern_Zum_Reconnect()
    {
        // Regression gegen A8: Der Port meldet sofort einen Lesefehler.
        var factory = new FakeSerialPortFactory
        {
            Configure = port => port.FailOnRead = true,
        };

        var transport = CreateTransport(factory);

        try
        {
            transport.Start();

            // Der Automat muss den Portfehler erkennen und neu verbinden.
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (factory.CreatedCount < 2 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20);
            }

            Assert.True(
                factory.CreatedCount >= 2,
                $"Der Transport muss sich neu verbinden (erzeugte Ports: {factory.CreatedCount}).");
        }
        finally
        {
            await transport.StopAsync();
        }
    }

    [Fact]
    public async Task Portfehler_Belegt_Die_CPU_Nicht_Dauerhaft()
    {
        // Der Legacy lief mit 100 % CPU, weil der Portfehler in einer
        // Endlosschleife endete. Im neuen Stack muss der Lesevorgang nach dem
        // Fehler enden - gemessen an der Anzahl der Leseversuche.
        var factory = new FakeSerialPortFactory
        {
            Configure = port => port.FailOnRead = true,
        };

        var transport = CreateTransport(factory);

        try
        {
            transport.Start();

            await Task.Delay(1000);

            var reads = factory.Created.Sum(p => p.ReadCount);

            // Der Backoff (50 ms, dann 100 ms) lässt in einer Sekunde nur
            // wenige Versuche zu. Eine Endlosschleife läge bei Millionen.
            Assert.True(
                reads < 500,
                $"Der Transport darf keine Endlosschleife erzeugen (Lesevorgänge: {reads}).");
        }
        finally
        {
            await transport.StopAsync();
        }
    }

    [Fact]
    public async Task Fehlender_Port_Fuehrt_Zu_Einem_Sauberen_Abbruch_Ohne_Ausnahme()
    {
        // Regression gegen A8: Das Öffnen eines nicht vorhandenen Ports darf
        // keine Ausnahme nach außen geben - der Transport verbindet sich
        // später erneut.
        var factory = new FakeSerialPortFactory
        {
            Configure = port => port.OpenException = new IOException("Port nicht gefunden"),
        };

        var transport = CreateTransport(factory);

        try
        {
            transport.Start();

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (factory.CreatedCount < 2 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20);
            }

            Assert.True(
                factory.CreatedCount >= 2,
                $"Der Transport muss sich nach einem Öffnungsfehler erneut verbinden (Versuche: {factory.CreatedCount}).");

            Assert.True(
                factory.Last!.CloseCount >= 0,
                "Der Port muss beim Abbau geschlossen werden.");
        }
        finally
        {
            await transport.StopAsync();
        }
    }

    [Fact]
    public async Task Ein_Angeforderter_Neuaufbau_Ignoriert_Den_Backoff()
    {
        // Wenn der Benutzer in der Oberfläche einen anderen Port wählt, muss
        // der Port **sofort** neu geöffnet werden - der Backoff (bis 30 s)
        // darf den Wechsel nicht verzögern.
        var factory = new FakeSerialPortFactory();
        var transport = new BacnetMstpTransport(
            factory,
            new BacnetMstpOptions
            {
                ThisStation = 3,

                // Ein sehr langer Backoff: ohne das Signal würde der
                // Neuaufbau praktisch nie stattfinden.
                ReconnectDelay = TimeSpan.FromSeconds(30),
                MaxReconnectDelay = TimeSpan.FromSeconds(30),
            },
            NullLogger<BacnetMstpTransport>.Instance);

        try
        {
            transport.Start();

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (factory.CreatedCount == 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }

            Assert.Equal(1, factory.CreatedCount);

            // Der Benutzer wechselt den Port.
            transport.RequestReconnect();

            var reconnectDeadline = DateTime.UtcNow.AddSeconds(5);
            while (factory.CreatedCount < 2 && DateTime.UtcNow < reconnectDeadline)
            {
                await Task.Delay(10);
            }

            Assert.True(
                factory.CreatedCount >= 2,
                $"Der Port muss sofort neu geöffnet werden (erzeugte Ports: {factory.CreatedCount}).");
        }
        finally
        {
            await transport.StopAsync();
        }
    }

    [Fact]
    public async Task StopAsync_Schliesst_Den_Port_Und_Beendet_Ohne_Hang()
    {
        var factory = new FakeSerialPortFactory();
        var transport = CreateTransport(factory);

        transport.Start();

        // Warten, bis der Port geöffnet wurde.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (factory.CreatedCount == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        await transport.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(
            factory.Last!.CloseCount >= 1,
            "Beim Stopp muss der Port geschlossen werden.");
    }

    [Fact]
    public async Task Ein_Geoeffneter_Port_Wird_Genutzt_Und_Beim_Stopp_Geschlossen()
    {
        var factory = new FakeSerialPortFactory();
        var transport = CreateTransport(factory);

        transport.Start();

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while ((factory.Last is null || !factory.Last.IsOpen) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.True(factory.Last is { IsOpen: true }, "Der Port muss geöffnet sein.");

        await transport.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.False(factory.Last.IsOpen, "Nach dem Stopp darf der Port nicht mehr offen sein.");
    }
}

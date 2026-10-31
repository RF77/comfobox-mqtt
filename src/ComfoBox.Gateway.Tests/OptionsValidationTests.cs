using System.ComponentModel.DataAnnotations;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Protection;

namespace ComfoBox.Gateway.Tests;

/// <summary>
/// Tests für die Konfigurationsvalidierung.
/// </summary>
/// <remarks>
/// Der Gateway muss bei ungültiger Konfiguration **nicht starten** - lieber
/// einmal beim Start abbrechen als im Betrieb böse überraschen.
/// </remarks>
public sealed class OptionsValidationTests
{
    private static bool IsValid<T>(T instance) where T : class
    {
        var validationResults = new List<ValidationResult>();
        return Validator.TryValidateObject(instance, new ValidationContext(instance), validationResults, validateAllProperties: true);
    }

    [Fact]
    public void Ein_Fehlender_Serieller_Port_Ist_Ungueltig()
    {
        Assert.False(IsValid(new BacnetOptions { Port = string.Empty }));
        Assert.False(IsValid(new BacnetOptions { Port = "   " }));
        Assert.True(IsValid(new BacnetOptions { Port = "COM3" }));
    }

    [Theory]
    [InlineData(9600, true)]
    [InlineData(19200, true)]
    [InlineData(38400, true)]
    [InlineData(57600, true)]
    [InlineData(76800, true)]
    [InlineData(115200, true)]
    [InlineData(12345, false)]
    [InlineData(4800, false)]
    public void Nur_Unterstuetzte_Baudraten_Sind_Erlaubt(int baudrate, bool erlaubt)
    {
        Assert.Equal(erlaubt, BacnetOptions.IsAllowedBaudrate(baudrate));
    }

    [Fact]
    public void Der_Standard_Basetopic_Ist_ComfoBox()
    {
        Assert.Equal("ComfoBox", new GatewayOptions().BaseTopic);
    }

    [Fact]
    public void Ein_Basetopic_Mit_Leerzeichen_Ist_Ungueltig()
    {
        Assert.False(IsValid(new GatewayOptions { BaseTopic = "Comfo Box" }));
        Assert.True(IsValid(new GatewayOptions { BaseTopic = "ComfoBox2" }));
    }

    [Fact]
    public void Ohne_Broker_Ist_Die_Konfiguration_Ungueltig()
    {
        var ohne = new MqttOptions { Brokers = [] };

        Assert.False(MqttOptions.HasValidBroker(ohne));
    }

    [Fact]
    public void Ein_Broker_Ohne_Host_Ist_Ungueltig()
    {
        var ohneHost = new MqttOptions
        {
            Brokers = [new MqttBrokerOptions { Host = string.Empty, Port = 1883 }],
        };

        Assert.False(MqttOptions.HasValidBroker(ohneHost));
    }

    [Fact]
    public void Ein_Broker_Mit_Host_Und_Port_Ist_Gueltig()
    {
        var mitBroker = new MqttOptions
        {
            Brokers = [new MqttBrokerOptions { Host = "localhost", Port = 1883 }],
        };

        Assert.True(MqttOptions.HasValidBroker(mitBroker));
    }

    [Fact]
    public void Die_Ringpuffer_Kapazitaet_Ist_Begrenzt()
    {
        // DataAnnotations validieren verschachtelte Objekte nicht - der
        // Ringpuffer wird deshalb einzeln geprüft.
        Assert.False(IsValid(new RingBufferOptions { Capacity = -1 }));
        Assert.False(IsValid(new RingBufferOptions { Capacity = 100_001 }));
        Assert.True(IsValid(new RingBufferOptions { Capacity = 1000 }));
        Assert.True(IsValid(new RingBufferOptions { Capacity = 0 }));
        Assert.False(IsValid(new RingBufferOptions { MinimumLevel = string.Empty }));
    }

    [Fact]
    public void Der_Standard_Ringpuffer_Hat_Platz_Fuer_1000_Eintraege()
    {
        Assert.Equal(1000, new LoggingOptions().RingBuffer.Capacity);
        Assert.Equal("Information", new LoggingOptions().RingBuffer.MinimumLevel);
    }

    [Fact]
    public void Das_Standardlimit_Des_Schreibzaehlers_Ist_200()
    {
        Assert.Equal(200, new GatewayOptions().MaxWritesPer24h);
        Assert.Equal(200, WriteCounter.DefaultLimit);
    }
}

using System.Globalization;
using ComfoBox.Domain.Items;
using ComfoBox.Gateway.Mqtt;

namespace ComfoBox.Gateway.Tests;

/// <summary>
/// Tests für das Formatieren und Parsen der MQTT-Payloads.
/// </summary>
/// <remarks>
/// Die beiden Regressionen der Legacy sind hier abgesichert:
/// <list type="bullet">
/// <item><b>A5</b> - die Zahlen sind kultur-invariant;</item>
/// <item><b>A6</b> - ein ungültiger Enum-Wert wird abgelehnt, es wird
/// <b>nicht</b> der Wert 0 geschrieben.</item>
/// </list>
/// </remarks>
public sealed class MqttPayloadTests
{
    private static readonly IReadOnlyDictionary<int, string> OperationModes = ItemEnums.OperationModes;

    [Fact]
    public void Zahlen_Werden_Kultur_Invariant_Formatiert()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            Assert.Equal("21.5", MqttPayload.Format(21.5f));
            Assert.Equal("0", MqttPayload.Format(0f));
            Assert.Equal("-12.25", MqttPayload.Format(-12.25f));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Zahlen_Werden_Kultur_Invariant_Gelesen()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            Assert.True(MqttPayload.TryParse("23.5", out var wert));
            Assert.Equal(23.5f, wert);

            Assert.True(MqttPayload.TryParse("0", out var nullValue));
            Assert.Equal(0f, nullValue);

            Assert.True(MqttPayload.TryParse("-12.25", out var negativ));
            Assert.Equal(-12.25f, negativ);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Ein_Dezimalkomma_Wird_Abgelehnt()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            // Der Legacy las "23,5" als 235 - das war der Grund für kaputte
            // Schreibvorgänge auf deutschen Systemen.
            Assert.False(MqttPayload.TryParse("23,5", out _));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("21.5")]
    [InlineData("0")]
    [InlineData("-12.25")]
    [InlineData("100")]
    public void Zahlen_Round_Trip_Ohne_Verlust(string payload)
    {
        Assert.True(MqttPayload.TryParse(payload, out var wert));
        Assert.Equal(payload, MqttPayload.Format(wert));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("21,5")]
    public void Ungueltige_Zahlen_Werden_Abgelehnt(string? payload)
    {
        Assert.False(MqttPayload.TryParse(payload, out _));
    }

    [Fact]
    public void Ein_Enum_Wird_Ueber_Den_Namen_Gelesen()
    {
        Assert.True(MqttPayload.TryParseEnum("Normal", OperationModes, out var wert));
        Assert.Equal(6, wert);
    }

    [Fact]
    public void Ein_Enum_Wird_Gross_Und_Klein_Ignorierend_Gelesen()
    {
        Assert.True(MqttPayload.TryParseEnum("normal", OperationModes, out var wert));
        Assert.Equal(6, wert);
    }

    [Fact]
    public void Ein_Enum_Wird_Ueber_Die_Zahl_Gelesen()
    {
        Assert.True(MqttPayload.TryParseEnum("6", OperationModes, out var wert));
        Assert.Equal(6, wert);
    }

    [Fact]
    public void Ein_Enum_Mit_Dem_Wert_0_Ist_Gueltig()
    {
        // "WpOff" hat den Wert 0 - der darf nicht mit "unbekannt" verwechselt werden.
        Assert.True(MqttPayload.TryParseEnum("WpOff", ItemEnums.HeatPumpStatusEnums, out var name));
        Assert.Equal(0, name);

        Assert.True(MqttPayload.TryParseEnum("0", ItemEnums.HeatPumpStatusEnums, out var zahl));
        Assert.Equal(0, zahl);
    }

    [Fact]
    public void Ein_Ungueltiger_Enum_Wird_Abgelehnt_Und_Liefert_Keine_Null()
    {
        // Befund A6: der Legacy hat hier den Wert 0 geschrieben.
        Assert.False(MqttPayload.TryParseEnum("abc", OperationModes, out var wert));
        Assert.Null(wert);
    }

    [Fact]
    public void Eine_Unbekannte_Enum_Zahl_Wird_Abgelehnt()
    {
        Assert.False(MqttPayload.TryParseEnum("99", OperationModes, out var wert));
        Assert.Null(wert);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Eine_Leere_Enum_Payload_Wird_Abgelehnt(string? payload)
    {
        Assert.False(MqttPayload.TryParseEnum(payload, OperationModes, out var wert));
        Assert.Null(wert);
    }
}

using System.Net;
using System.Text;
using ComfoBox.Domain.Items;
using ComfoBox.Domain.Values;
using ComfoBox.Gateway.Health;
using ComfoBox.Gateway.Protection;
using Microsoft.Extensions.DependencyInjection;

namespace ComfoBox.Gateway.Tests.Api;

/// <summary>
/// Die Tests der Item-Endpunkte (Aufgabe 4.1).
/// </summary>
public sealed class ItemsApiTests
{
    /// <summary>
    /// Der Katalog liefert alle Items mit den unveränderten Metadaten.
    /// </summary>
    [Fact]
    public async Task Die_Item_Liste_Enthaelt_Alle_Items_Mit_Metadaten()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.GetAsync("/api/items");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.Equal(127, items.GetArrayLength());

        var first = items[0];
        Assert.Equal("zone.operation_mode", first.GetProperty("id").GetString());
        Assert.Equal("Zone", first.GetProperty("group").GetString());
        Assert.Equal("OperationMode", first.GetProperty("name").GetString());
        Assert.Equal("enum", first.GetProperty("kind").GetString());
        Assert.Equal("ComfoBox/Zone/OperationMode", first.GetProperty("stateTopic").GetString());
        Assert.False(first.GetProperty("requiresExpertMode").GetBoolean());
    }

    /// <summary>
    /// Ein einzelnes Item liefert den aktuellen Wert.
    /// </summary>
    [Fact]
    public async Task Ein_Item_Liefert_Den_Gecachten_Wert()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var cache = fixture.Services.GetRequiredService<IValueCache>();
        cache.Update("zone.set_point_normal", new AnalogValue(21.5f, 5f, 30f, "°C"));

        var response = await fixture.Client.GetAsync("/api/items/zone.set_point_normal");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var item = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.Equal("zone.set_point_normal", item.GetProperty("id").GetString());
        Assert.Equal(21.5, item.GetProperty("value").GetDouble(), 3);
        Assert.Equal("°C", item.GetProperty("unit").GetString());
        Assert.Equal(5, item.GetProperty("min").GetDouble(), 3);
        Assert.Equal(30, item.GetProperty("max").GetDouble(), 3);
        Assert.True(item.GetProperty("writable").GetBoolean());
        Assert.Equal("ComfoBox/Zone/SetPointNormal/Set", item.GetProperty("setTopic").GetString());
        Assert.False(item.GetProperty("stale").GetBoolean());
    }

    /// <summary>
    /// Ein unbekanntes Item ergibt 404.
    /// </summary>
    [Fact]
    public async Task Ein_Unbekanntes_Item_Ergibt_404()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.GetAsync("/api/items/gibts.nicht");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.Equal("item_not_found", error.GetProperty("error").GetString());
    }

    /// <summary>
    /// Die Gruppenansicht liefert nur die Items der Gruppe.
    /// </summary>
    [Fact]
    public async Task Die_Gruppenansicht_Liefert_Nur_Die_Gruppe()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.GetAsync("/api/items/group/Zone");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.True(items.GetArrayLength() > 0);

        foreach (var item in items.EnumerateArray())
        {
            Assert.Equal("Zone", item.GetProperty("group").GetString());
        }
    }

    /// <summary>
    /// Ein gültiger Analogwert wird geschrieben und der Zähler steigt.
    /// </summary>
    [Fact]
    public async Task Ein_Gueltiger_Wert_Wird_Geschrieben_Und_Gezahlt()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var counter = fixture.Services.GetRequiredService<IWriteCounter>();
        var before = counter.Count;

        var response = await fixture.Client.PutAsync(
            "/api/items/zone.set_point_normal",
            Json("{\"value\":21.5}"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([62u], fixture.Bacnet.WrittenObjects);
        Assert.Equal(before + 1, counter.Count);
    }

    /// <summary>
    /// Der geschriebene Wert steht sofort im Cache (die UI sieht ihn ohne
    /// den nächsten Poll-Zyklus).
    /// </summary>
    [Fact]
    public async Task Der_Geschriebene_Wert_Landet_Sofort_Im_Cache()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        await fixture.Client.PutAsync(
            "/api/items/zone.set_point_normal",
            Json("{\"value\":21.5}"));

        var cache = fixture.Services.GetRequiredService<IValueCache>();
        Assert.True(cache.TryGet("zone.set_point_normal", out var cached));
        Assert.Equal(21.5f, ((AnalogValue)cached!.Value!).Value);
    }

    /// <summary>
    /// Ein Wert außerhalb des Bereichs wird abgelehnt - es wird **nichts** geschrieben.
    /// </summary>
    [Fact]
    public async Task Ein_Wert_Ausserhalb_Der_Grenzen_Wird_Abgelehnt()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PutAsync(
            "/api/items/zone.set_point_normal",
            Json("{\"value\":40}"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty(fixture.Bacnet.WrittenObjects);

        var error = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.Equal("value_out_of_range", error.GetProperty("error").GetString());
        Assert.Equal(5, error.GetProperty("min").GetDouble(), 3);
        Assert.Equal(30, error.GetProperty("max").GetDouble(), 3);
    }

    /// <summary>
    /// Ein Item ohne Schreibpfad wird mit 409 abgelehnt.
    /// </summary>
    [Fact]
    public async Task Ein_Item_Ohne_Schreibpfad_Wird_Abgelehnt()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        // Befund A20: die Legacy-DateItems haben keinen Wert.
        var response = await fixture.Client.PutAsync(
            "/api/items/states.controller.initial_operation_date",
            Json("{\"value\":1}"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(fixture.Bacnet.WrittenObjects);

        var error = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.Equal("read_only", error.GetProperty("error").GetString());
    }

    /// <summary>
    /// Ohne Expert-Modus wird der Schreibvorgang mit 403 abgelehnt.
    /// </summary>
    [Fact]
    public async Task Ohne_Experten_Modus_Wird_Abgelehnt()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var catalog = fixture.Services.GetRequiredService<IItemCatalog>();
        var expertItem = catalog.All.First(item => item.RequiresExpertMode);

        var response = await fixture.Client.PutAsync(
            $"/api/items/{expertItem.Id}",
            Json("{\"value\":1}"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(fixture.Bacnet.WrittenObjects);

        var error = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.Equal("expert_mode_required", error.GetProperty("error").GetString());
    }

    /// <summary>
    /// Mit Expert-Modus wird geschrieben.
    /// </summary>
    [Fact]
    public async Task Mit_Experten_Modus_Wird_Geschrieben()
    {
        await using var fixture = await GatewayApiFixture.StartAsync(
            new Dictionary<string, string?> { ["Gateway:ExpertMode"] = "true" });

        var catalog = fixture.Services.GetRequiredService<IItemCatalog>();
        var expertItem = catalog.All.First(item => item.RequiresExpertMode);

        var response = await fixture.Client.PutAsync(
            $"/api/items/{expertItem.Id}",
            Json("{\"value\":1}"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Single(fixture.Bacnet.WrittenObjects);
    }

    /// <summary>
    /// Ein ungültiger Enum-Name wird abgelehnt - es wird **nie** der Wert 0
    /// geschrieben (Befund A6).
    /// </summary>
    [Fact]
    public async Task Ein_Ungueltiger_Enum_Name_Wird_Abgelehnt()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PutAsync(
            "/api/items/zone.operation_mode",
            Json("{\"value\":\"GibtEsNicht\"}"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty(fixture.Bacnet.WrittenObjects);

        var error = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.Equal("invalid_value", error.GetProperty("error").GetString());
    }

    /// <summary>
    /// Die Enum-Zahl 0 ist kein gültiger Wert (Befund A6).
    /// </summary>
    [Fact]
    public async Task Die_Enum_Zahl_0_Wird_Abgelehnt()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PutAsync(
            "/api/items/zone.operation_mode",
            Json("{\"value\":0}"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty(fixture.Bacnet.WrittenObjects);
    }

    /// <summary>
    /// Ein Enum lässt sich über den Namen schreiben.
    /// </summary>
    [Fact]
    public async Task Ein_Enum_Laesst_Sich_Ueber_Den_Namen_Schreiben()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PutAsync(
            "/api/items/zone.operation_mode",
            Json("{\"value\":\"Normal\"}"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([830u], fixture.Bacnet.WrittenObjects);
        Assert.Equal(6f, fixture.Bacnet.Values[830]);
    }

    /// <summary>
    /// Ein Enum lässt sich über die Zahl schreiben.
    /// </summary>
    [Fact]
    public async Task Ein_Enum_Laesst_Sich_Ueber_Die_Zahl_Schreiben()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PutAsync(
            "/api/items/zone.operation_mode",
            Json("{\"value\":7}"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(7f, fixture.Bacnet.Values[830]);
    }

    /// <summary>
    /// Ein Dezimalkomma wird abgelehnt (Befund A5).
    /// </summary>
    [Fact]
    public async Task Ein_Dezimalkomma_Wird_Abgelehnt()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PutAsync(
            "/api/items/zone.set_point_normal",
            Json("{\"value\":\"23,5\"}"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty(fixture.Bacnet.WrittenObjects);
    }

    /// <summary>
    /// Ein Wert als Text mit Punkt wird akzeptiert (InvariantCulture).
    /// </summary>
    [Fact]
    public async Task Ein_Text_Mit_Punkt_Wird_Akzeptiert()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PutAsync(
            "/api/items/zone.set_point_normal",
            Json("{\"value\":\"23.5\"}"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(23.5f, fixture.Bacnet.Values[62]);
    }

    /// <summary>
    /// Eine Anfrage ohne Wert ergibt 400.
    /// </summary>
    [Fact]
    public async Task Eine_Anfrage_Ohne_Wert_Ergibt_400()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PutAsync(
            "/api/items/zone.set_point_normal",
            Json("{}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(fixture.Bacnet.WrittenObjects);

        var error = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.Equal("invalid_request", error.GetProperty("error").GetString());
    }

    /// <summary>
    /// Ein Fehler landet in der Fehler-Senke (und damit in Status/Errors).
    /// </summary>
    [Fact]
    public async Task Ein_Fehler_Landet_In_Der_Fehler_Senke()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        await fixture.Client.PutAsync(
            "/api/items/zone.set_point_normal",
            Json("{\"value\":40}"));

        var errors = fixture.Services.GetRequiredService<IErrorCollector>();
        var snapshot = errors.Snapshot();

        Assert.Contains(snapshot, entry => entry.Code == "value_out_of_range");
    }

    /// <summary>
    /// Ein Busfehler ergibt 502.
    /// </summary>
    [Fact]
    public async Task Ein_Busfehler_Ergibt_502()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();
        fixture.Bacnet.FailWrites = true;

        var response = await fixture.Client.PutAsync(
            "/api/items/zone.set_point_normal",
            Json("{\"value\":21.5}"));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        var error = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.Equal("bus_error", error.GetProperty("error").GetString());
    }

    /// <summary>
    /// Das 24-h-Limit ergibt 409 - und es wird nicht geschrieben.
    /// </summary>
    [Fact]
    public async Task Das_Tageslimit_Ergibt_409()
    {
        await using var fixture = await GatewayApiFixture.StartAsync(
            new Dictionary<string, string?> { ["Gateway:MaxWritesPer24h"] = "1" });

        var first = await fixture.Client.PutAsync(
            "/api/items/zone.set_point_normal",
            Json("{\"value\":21.5}"));
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await fixture.Client.PutAsync(
            "/api/items/zone.set_point_normal",
            Json("{\"value\":22}"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var error = await GatewayApiFixture.ReadJsonAsync(second);
        Assert.Equal("write_limit_exceeded", error.GetProperty("error").GetString());
        Assert.Equal(1, error.GetProperty("maxWritesPer24h").GetInt32());
    }

    /// <summary>
    /// Erzeugt einen JSON-Body.
    /// </summary>
    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");
}

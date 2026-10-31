using System.Globalization;
using System.Text.Json;
using ComfoBox.Domain.Items;
using ComfoBox.Domain.Values;
using ComfoBox.Gateway.Api.Models;
using ComfoBox.Gateway.Bacnet;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Health;
using ComfoBox.Gateway.Mqtt;
using ComfoBox.Gateway.Protection;

namespace ComfoBox.Gateway.Api;

/// <summary>
/// Die Endpunkte der Items.
/// </summary>
/// <remarks>
/// <para>
/// Jeder Schreibvorgang läuft über <see cref="BacnetDevice.WriteAsync"/> -
/// nur dort wird der 24-h-Zähler erhöht. Ein Schreibvorgang per REST darf den
/// Schreibschutz nicht umgehen.
/// </para>
/// </remarks>
internal static class ItemsEndpoints
{
    /// <summary>
    /// Registriert die Item-Endpunkte.
    /// </summary>
    /// <param name="app">Der Endpoint-Builder.</param>
    public static void MapItems(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/items", (
                IItemCatalog catalog,
                IValueCache cache,
                MqttTopicMap topics,
                GatewayOptions options) =>
            {
                var items = catalog.All
                    .Select(item => ItemDto.From(item, Find(cache, item.Id), topics, options.ExpertMode))
                    .ToArray();

                return Results.Ok(items);
            })
            .WithTags("Items")
            .WithName("GetItems");

        app.MapGet("/api/items/group/{group}", (
                string group,
                IItemCatalog catalog,
                IValueCache cache,
                MqttTopicMap topics,
                GatewayOptions options) =>
            {
                var items = catalog.ByGroup(group)
                    .Select(item => ItemDto.From(item, Find(cache, item.Id), topics, options.ExpertMode))
                    .ToArray();

                return Results.Ok(items);
            })
            .WithTags("Items")
            .WithName("GetItemsByGroup");

        app.MapGet("/api/items/{id}", (
                string id,
                IItemCatalog catalog,
                IValueCache cache,
                MqttTopicMap topics,
                GatewayOptions options) =>
            {
                var item = catalog.Find(id);
                if (item is null)
                {
                    return Results.Json(
                        new ApiError
                        {
                            Error = "item_not_found",
                            Message = $"Unbekanntes Item '{id}'.",
                            ItemId = id,
                        },
                        statusCode: StatusCodes.Status404NotFound);
                }

                return Results.Ok(ItemDto.From(item, Find(cache, item.Id), topics, options.ExpertMode));
            })
            .WithTags("Items")
            .WithName("GetItem");

        app.MapPut("/api/items/{id}", async (
                string id,
                WriteValueRequest request,
                IItemCatalog catalog,
                BacnetDevice device,
                IValueCache cache,
                IErrorCollector errors,
                IWriteCounter counter,
                CancellationToken cancellationToken) =>
            {
                var item = catalog.Find(id);
                if (item is null)
                {
                    return Results.Json(
                        new ApiError
                        {
                            Error = "item_not_found",
                            Message = $"Unbekanntes Item '{id}'.",
                            ItemId = id,
                        },
                        statusCode: StatusCodes.Status404NotFound);
                }

                // Befund A20: die Legacy-DateItems haben keinen Wert - für sie
                // darf es keinen Schreibpfad geben.
                if (item.Kind == ItemValueKind.Date)
                {
                    return ApiErrors.Failure(
                        errors,
                        new ApiError
                        {
                            Error = "read_only",
                            Message = "Dieses Item hat keinen schreibbaren Wert.",
                            ItemId = item.Id,
                        },
                        StatusCodes.Status409Conflict,
                        itemId: item.Id);
                }

                if (request is null || request.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                {
                    return ApiErrors.Failure(
                        errors,
                        new ApiError
                        {
                            Error = "invalid_request",
                            Message = "Es wurde kein Wert übergeben. Erwartet wird {\"value\": …}.",
                            ItemId = item.Id,
                        },
                        StatusCodes.Status400BadRequest,
                        itemId: item.Id);
                }

                if (!TryResolveTarget(item, request.Value, out var target, out var error))
                {
                    return ApiErrors.Failure(
                        errors,
                        error,
                        error.Error == "invalid_request"
                            ? StatusCodes.Status400BadRequest
                            : StatusCodes.Status422UnprocessableEntity,
                        itemId: item.Id);
                }

                var outcome = await device.WriteAsync(item, target, cancellationToken).ConfigureAwait(false);

                switch (outcome)
                {
                    case WriteOutcome.Ok:
                        // Der Wert steht sofort im Cache - die Oberfläche sieht ihn
                        // ohne den nächsten Poll-Zyklus abwarten zu müssen.
                        cache.Update(item.Id, ValueOf(item, target));
                        return Results.NoContent();

                    case WriteOutcome.ReadOnly:
                        return ApiErrors.Failure(
                            errors,
                            new ApiError
                            {
                                Error = "read_only",
                                Message = "Das Item ist nicht schreibbar.",
                                ItemId = item.Id,
                            },
                            StatusCodes.Status409Conflict,
                            itemId: item.Id);

                    case WriteOutcome.ExpertModeRequired:
                        return ApiErrors.Failure(
                            errors,
                            new ApiError
                            {
                                Error = "expert_mode_required",
                                Message = "Dieser Wert erfordert den Experten-Modus.",
                                ItemId = item.Id,
                            },
                            StatusCodes.Status403Forbidden,
                            itemId: item.Id);

                    case WriteOutcome.OutOfRange:
                        return ApiErrors.Failure(
                            errors,
                            new ApiError
                            {
                                Error = "value_out_of_range",
                                Message = $"Der Wert {target.ToString(CultureInfo.InvariantCulture)} liegt außerhalb des zulässigen Bereichs "
                                    + $"[{Format(item.Min)}, {Format(item.Max)}].",
                                ItemId = item.Id,
                                Min = item.Min,
                                Max = item.Max,
                            },
                            StatusCodes.Status422UnprocessableEntity,
                            itemId: item.Id);

                    case WriteOutcome.InvalidType:
                        return ApiErrors.Failure(
                            errors,
                            new ApiError
                            {
                                Error = "invalid_type",
                                Message = "Der Wert hat den falschen Typ.",
                                ItemId = item.Id,
                            },
                            StatusCodes.Status422UnprocessableEntity,
                            itemId: item.Id);

                    case WriteOutcome.LimitExceeded:
                    {
                        // Das BACnet-Gerät meldet den Fall bereits.
                        var stats = counter.GetStats();
                        return ApiErrors.Failure(
                            errors,
                            new ApiError
                            {
                                Error = "write_limit_exceeded",
                                Message = $"Maximale Anzahl Schreibvorgänge pro 24 h überschritten ({stats.Limit}).",
                                ItemId = item.Id,
                                WritesLast24h = stats.Count,
                                MaxWritesPer24h = stats.Limit,
                            },
                            StatusCodes.Status409Conflict,
                            report: false,
                            itemId: item.Id);
                    }

                    case WriteOutcome.BusError:
                        // Das BACnet-Gerät meldet den Fall bereits.
                        return ApiErrors.Failure(
                            errors,
                            new ApiError
                            {
                                Error = "bus_error",
                                Message = "Die Heizung hat nicht geantwortet.",
                                ItemId = item.Id,
                            },
                            StatusCodes.Status502BadGateway,
                            report: false,
                            itemId: item.Id);

                    default:
                        return Results.StatusCode(StatusCodes.Status502BadGateway);
                }
            })
            .WithTags("Items")
            .WithName("WriteItem");
    }

    /// <summary>
    /// Ermittelt den gecachten Wert eines Items (oder <c>null</c>).
    /// </summary>
    private static CachedValue? Find(IValueCache cache, string id)
        => cache.TryGet(id, out var value) ? value : null;

    /// <summary>
    /// Wandelt die JSON-Nutzlast in einen Schreibwert.
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <param name="element">Die Nutzlast.</param>
    /// <param name="target">Der ermittelte Wert.</param>
    /// <param name="error">Die Fehlerbeschreibung (bei einem Fehler).</param>
    /// <returns><c>true</c>, wenn ein gültiger Wert ermittelt wurde.</returns>
    private static bool TryResolveTarget(
        ItemDefinition item,
        JsonElement element,
        out double target,
        out ApiError error)
    {
        ArgumentNullException.ThrowIfNull(item);

        target = 0;
        error = new ApiError { Error = "invalid_type", Message = "Der Wert ist ungültig." };

        if (item.Kind == ItemValueKind.Enum)
        {
            string? text = element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetRawText(),
                _ => null,
            };

            if (text is null)
            {
                error = new ApiError
                {
                    Error = "invalid_type",
                    Message = $"Für {item.Name} wird der Name oder die Zahl des Werts erwartet.",
                };
                return false;
            }

            // Befund A6: ein ungültiger Wert wird abgelehnt - es wird **nie**
            // der Wert 0 geschrieben.
            if (!MqttPayload.TryParseEnum(text, item.EnumValues, out var enumValue) || enumValue is null)
            {
                error = new ApiError
                {
                    Error = "invalid_value",
                    Message = $"'{text}' ist kein gültiger Wert für {item.Name}.",
                };
                return false;
            }

            target = enumValue.Value;
            return true;
        }

        double number;

        if (element.ValueKind == JsonValueKind.Number)
        {
            if (!element.TryGetDouble(out number) || double.IsNaN(number) || double.IsInfinity(number))
            {
                error = new ApiError
                {
                    Error = "invalid_type",
                    Message = "Der Wert muss eine endliche Zahl sein.",
                };
                return false;
            }
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            // Befund A5: nur der Punkt ist als Dezimaltrenner gültig.
            if (!AnalogValue.TryParse(element.GetString(), out var parsed))
            {
                error = new ApiError
                {
                    Error = "invalid_value",
                    Message = "Der Wert muss eine Zahl mit Punkt als Dezimaltrenner sein (z. B. 21.5).",
                };
                return false;
            }

            number = parsed;
        }
        else
        {
            error = new ApiError
            {
                Error = "invalid_type",
                Message = "Der Wert muss eine Zahl sein.",
            };
            return false;
        }

        target = number;
        return true;
    }

    /// <summary>
    /// Erzeugt den Cache-Wert zu einem geschriebenen Wert.
    /// </summary>
    private static ItemValue ValueOf(ItemDefinition item, double value)
        => item.Kind == ItemValueKind.Enum
            ? new EnumValue((int)value, item.EnumValues)
            : new AnalogValue((float)value, item.Min, item.Max, item.Unit);

    /// <summary>
    /// Formatiert eine Grenze kultur-invariant.
    /// </summary>
    private static string Format(float? value)
        => value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "–";
}

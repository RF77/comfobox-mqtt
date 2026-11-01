namespace ComfoBox.Gateway.Mqtt;

/// <summary>
/// Die wirksame Client-ID des Gateways gegenüber dem Broker.
/// </summary>
/// <remarks>
/// <para>
/// Die Client-ID muss je Gateway-Instanz eindeutig sein: zwei Clients mit
/// derselben ID verdrängen sich beim Broker - der Broker trennt die ältere
/// Sitzung („Session taken over") und deren Last-Will meldet das andere
/// Gateway als <c>offline</c>.
/// </para>
/// <para>
/// Deshalb wird die Geräte-ID (<c>Gateway:DeviceId</c>) an die konfigurierte
/// Client-ID angehängt. Zwei Gateways auf demselben Host - der Parallelbetrieb
/// aus Phase 7 - bekommen so automatisch verschiedene IDs, solange sie
/// verschiedene Geräte-IDs haben.
/// </para>
/// </remarks>
public static class MqttClientId
{
    /// <summary>
    /// Die wirksame Client-ID.
    /// </summary>
    /// <param name="clientId">Die konfigurierte Client-ID.</param>
    /// <param name="deviceId">Die Geräte-ID des Gateways.</param>
    /// <returns>Die Client-ID, die der Broker sieht.</returns>
    public static string Effective(string clientId, string deviceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        return $"{clientId}-{deviceId}";
    }
}

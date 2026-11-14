namespace ComfoBox.Gateway.Configuration;

/// <summary>
/// Die zur Laufzeit geltenden Gateway-Einstellungen.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GatewayOptions"/> wird beim Start gebunden und ändert sich
/// danach nicht mehr. Für Werte, die in der Oberfläche umgeschaltet werden
/// sollen, ohne den Gateway neu zu starten, gibt es deshalb diese Klasse.
/// </para>
/// <para>
/// <b>Warum das nötig ist:</b> Der Experten-Modus entscheidet bei **jedem**
/// Schreibvorgang, ob ein sensibles Item geschrieben werden darf. Wird er nur
/// in der <c>config.json</c> gespeichert, gilt die Änderung erst nach einem
/// Neustart - der Benutzer schaltet in der Oberfläche um und nichts passiert.
/// </para>
/// </remarks>
public sealed class GatewayRuntimeSettings
{
    private int _expertMode;

    /// <summary>
    /// Erzeugt die Einstellungen.
    /// </summary>
    /// <param name="expertMode">Ist der Experten-Modus aktiv?</param>
    public GatewayRuntimeSettings(bool expertMode)
    {
        _expertMode = expertMode ? 1 : 0;
    }

    /// <summary>
    /// Ist der Experten-Modus aktiv?
    /// </summary>
    /// <remarks>
    /// Er erlaubt das Schreiben von Items mit
    /// <see cref="ComfoBox.Domain.Items.ItemDefinition.RequiresExpertMode"/>.
    /// </remarks>
    public bool ExpertMode
    {
        get => Volatile.Read(ref _expertMode) == 1;
        set => Volatile.Write(ref _expertMode, value ? 1 : 0);
    }
}

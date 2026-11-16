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
/// <para>
/// <b>MQTT und Discovery folgen dem Schalter (Aufgabe 10.13):</b> Ein Wechsel
/// löst <see cref="ExpertModeChanged"/> aus. Der
/// <c>ExpertModeCoordinator</c> baut darauf die Topic-Zuordnung, die
/// abonnierten <c>/Set</c>-Topics und die Home-Assistant-Discovery um - ohne
/// Neustart.
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
    /// Wird ausgelöst, wenn der Experten-Modus **wirklich** umgeschaltet wurde.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Das Ereignis feuert **nicht**, wenn derselbe Wert erneut gesetzt wird -
    /// die Oberfläche schickt die gesamte Konfiguration, also auch unveränderte
    /// Felder. Ohne diese Prüfung würde jeder Speichervorgang die Discovery neu
    /// publizieren.
    /// </para>
    /// <para>
    /// Das Ereignis wird **synchron** im Setter ausgelöst. Ein Abonnent, der
    /// länger dauert, blockiert den Aufrufer - deshalb startet der Koordinator
    /// aus dem Handler heraus nur einen Task. Eine Ausnahme eines Abonnenten
    /// wird geschluckt: der Wert ist zu diesem Zeitpunkt bereits gesetzt, und
    /// ein fehlerhafter Zuhörer darf den Schalter nicht unbrauchbar machen.
    /// </para>
    /// </remarks>
    public event EventHandler<bool>? ExpertModeChanged;

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
        set
        {
            // Interlocked.Exchange liefert den vorherigen Wert - damit ist die
            // Prüfung "hat sich wirklich etwas geändert?" wettlaufsicher, auch
            // wenn zwei Aufrufer gleichzeitig schalten.
            var previous = Interlocked.Exchange(ref _expertMode, value ? 1 : 0);

            if ((previous == 1) == value)
            {
                return;
            }

            RaiseExpertModeChanged(value);
        }
    }

    /// <summary>
    /// Meldet den Wechsel - jeder Abonnent wird einzeln abgesichert.
    /// </summary>
    /// <param name="expertModeEnabled">Der neue Zustand.</param>
    private void RaiseExpertModeChanged(bool expertModeEnabled)
    {
        var handlers = ExpertModeChanged;
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((EventHandler<bool>)handler).Invoke(this, expertModeEnabled);
            }
            catch (Exception)
            {
                // Bewusst geschluckt: der Setter ist der Schreibpfad der
                // Oberfläche. Ein Abonnent mit einem Fehler (z. B. ein
                // geschlossener Broker) darf das Umschalten nicht verhindern.
            }
        }
    }
}

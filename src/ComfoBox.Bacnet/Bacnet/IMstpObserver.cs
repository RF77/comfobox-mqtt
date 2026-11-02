namespace ComfoBox.Bacnet;

/// <summary>
/// Beobachtet die Ereignisse des MS/TP-Zustandsautomaten.
/// </summary>
/// <remarks>
/// <para>
/// Der Beobachter wird **im Hot-Path** des Zustandsautomaten aufgerufen.
/// Die Implementierung muss deshalb:
/// </para>
/// <list type="bullet">
/// <item><b>keine Allokation</b> erzeugen (kein <c>new</c>, keine Strings, keine LINQ);</item>
/// <item><b>kein Logging</b> auslösen - ein Log-Eintrag kostet I/O und würde
/// das Zeitfenster des Tokens (95 ms) sprengen;</item>
/// <item><b>keine Sperre</b> halten - der Zustandsautomat darf nie warten.</item>
/// </list>
/// <para>
/// Die Auswertung (Log-Ausgabe, HTTP-Antwort) muss daher **außerhalb** des
/// Zustandsautomaten erfolgen - siehe <see cref="MstpEventRecorder"/>.
/// </para>
/// </remarks>
public interface IMstpObserver
{
    /// <summary>
    /// Meldet ein Ereignis aus dem Zustandsautomaten.
    /// </summary>
    /// <param name="kind">Die Art des Ereignisses.</param>
    /// <param name="station">Die beteiligte Station (MAC-Adresse).</param>
    /// <param name="frameType">Der MS/TP-Frame-Typ.</param>
    void OnMstpEvent(MstpEventKind kind, byte station, byte frameType);
}

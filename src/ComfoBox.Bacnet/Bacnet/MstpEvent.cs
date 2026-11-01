namespace ComfoBox.Bacnet;

/// <summary>
/// Die Art eines beobachteten MS/TP-Ereignisses.
/// </summary>
public enum MstpEventKind
{
    /// <summary>Ein Token ist eingegangen (die Station hat den Token übernommen).</summary>
    TokenReceived,

    /// <summary>Der Token wurde für einen eigenen Frame benutzt.</summary>
    TokenUsed,

    /// <summary>Der Token wurde an den nächsten Knoten weitergegeben.</summary>
    TokenPassed,

    /// <summary>Eine Poll-For-Master-Runde hat begonnen.</summary>
    PollForMaster,

    /// <summary>Die Station hat sich als einziger Master erklärt (kein anderer Knoten im Bus).</summary>
    SoleMaster,

    /// <summary>Ein Frame ist eingegangen.</summary>
    FrameReceived,

    /// <summary>Ein Frame wurde gesendet.</summary>
    FrameSent,

    /// <summary>Eine BACnet-Nutzlast (APDU/NPDU) ist eingegangen.</summary>
    DataReceived,

    /// <summary>Eine Datenanfrage wurde mit "Reply Postponed" beantwortet.</summary>
    ReplyPosted,

    /// <summary>Anzahl der Ereignisarten (für die Zähler-Arrays).</summary>
    Count,
}

/// <summary>
/// Ein beobachtetes MS/TP-Ereignis.
/// </summary>
/// <param name="Kind">Die Art des Ereignisses.</param>
/// <param name="Station">Die beteiligte Station (MAC-Adresse).</param>
/// <param name="FrameType">Der MS/TP-Frame-Typ.</param>
/// <param name="TimestampUtc">Der Zeitpunkt in UTC.</param>
public readonly record struct MstpEvent(
    MstpEventKind Kind,
    byte Station,
    byte FrameType,
    DateTimeOffset TimestampUtc);

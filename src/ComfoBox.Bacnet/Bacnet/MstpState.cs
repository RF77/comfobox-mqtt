using ComfoBox.Bacnet.Serialize;

namespace ComfoBox.Bacnet;

/// <summary>
/// Zustände des MS/TP-Zustandsautomaten.
/// </summary>
/// <remarks>
/// Die Bezeichner entsprechen 1:1 den Kommentaren im Legacy-Code
/// (<c>src_old/Bacnet/BACnetTransport.cs</c>, Klasse
/// <c>BacnetMstpProtocolTransport</c>, Enum <c>StateChanges</c>). Die
/// Zustandsübergänge sind unverändert - nur die Abbruchbedingung
/// <see cref="Fatal"/> kam für den Spin-Loop-Fix (Befund A8) dazu.
/// </remarks>
internal enum MstpState
{
    /// <summary>Initialisierung.</summary>
    Reset,

    /// <summary>Initialisierung abgeschlossen.</summary>
    DoneInitializing,

    /// <summary>Token erzeugen (wir sind Master).</summary>
    GenerateToken,

    /// <summary>Antwort auf eine Datenanfrage ist fällig.</summary>
    ReceivedDataNeedingReply,

    /// <summary>Token empfangen.</summary>
    ReceivedToken,

    /// <summary>Unerwarteter Frame empfangen (auch aus WaitForReply).</summary>
    ReceivedUnexpectedFrame,

    /// <summary>Poll-For-Master beendet.</summary>
    DoneWithPFM,

    /// <summary>Antwort auf Poll-For-Master empfangen.</summary>
    ReceivedReplyToPFM,

    /// <summary>Einziger Master am Bus.</summary>
    SoleMaster,

    /// <summary>Alleiniger Master festgestellt.</summary>
    DeclareSoleMaster,

    /// <summary>Frame gesendet, warte auf Antwort.</summary>
    SendAndWait,

    /// <summary>Nichts zu senden.</summary>
    NothingToSend,

    /// <summary>Frame gesendet, keine Antwort erwartet.</summary>
    SendNoWait,

    /// <summary>Token weitergeben.</summary>
    SendToken,

    /// <summary>Wartungs-Poll-For-Master zurücksetzen.</summary>
    ResetMaintenancePFM,

    /// <summary>Wartungs-Poll-For-Master senden.</summary>
    SendMaintenancePFM,

    /// <summary>Wartungs-Poll-For-Master als Allein-Master fortsetzen.</summary>
    SoleMasterRestartMaintenancePFM,

    /// <summary>Weiteren Frame senden.</summary>
    SendAnotherFrame,

    /// <summary>Nachfolger unbekannt.</summary>
    NextStationUnknown,

    /// <summary>Zeitüberschreitung beim Warten auf eine Antwort.</summary>
    ReplyTimeOut,

    /// <summary>Ungültiger Frame empfangen.</summary>
    InvalidFrame,

    /// <summary>Antwort empfangen.</summary>
    ReceivedReply,

    /// <summary>Antwort aufgeschoben (Reply Postponed).</summary>
    ReceivedPostpone,

    /// <summary>Neuen Nachfolger suchen.</summary>
    FindNewSuccessor,

    /// <summary>Token wurde benutzt.</summary>
    SawTokenUser,

    /// <summary>Antwort gesendet.</summary>
    Reply,

    /// <summary>Antwort aufgeschoben.</summary>
    DeferredReply,

    /// <summary>
    /// Der Port ist ausgefallen - der Zustandsautomat muss beendet werden.
    /// </summary>
    Fatal,
}

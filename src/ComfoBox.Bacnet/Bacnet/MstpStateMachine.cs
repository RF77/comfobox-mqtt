using System.Collections.Concurrent;
using ComfoBox.Bacnet.Serialize;
using ComfoBox.Bacnet.Serial;
using Microsoft.Extensions.Logging;

namespace ComfoBox.Bacnet;

/// <summary>
/// MS/TP-Zustandsautomat: Token-Ring, Poll-For-Master und Frame-Austausch.
/// </summary>
/// <remarks>
/// <para>
/// Der Zustandsautomat ist eine 1:1-Übernahme des Legacy-Codes
/// (<c>src_old/Bacnet/BACnetTransport.cs</c>, Klasse
/// <c>BacnetMstpProtocolTransport</c>). Zustände, Zeiten und Übergänge sind
/// unverändert - die Zeitlimits sind Teil des Bus-Verhaltens und dürfen nicht
/// verändert werden.
/// </para>
/// <para>
/// Abweichungen gegenüber dem Legacy (alle begründet):
/// </para>
/// <list type="bullet">
/// <item>
/// <b>A8 (Spin-Loop):</b> Bei einem Portfehler liefert
/// <see cref="GetNextMessage"/> den Status <see cref="GetMessageStatus.ConnectionError"/>.
/// Der Legacy lief in diesem Fall in eine Endlosschleife mit 100 % CPU-Last.
/// Hier endet der Automat stattdessen (<see cref="MstpState.Fatal"/>) und der
/// Transport verbindet sich mit Backoff neu.
/// </item>
/// <item>
/// <b>A23:</b> Kein <c>ThreadPriority.Highest</c> - der Thread läuft mit der
/// Standardpriorität.
/// </item>
/// <item>
/// <b>A19:</b> Die Sende-Warteschlange ist threadsicher
/// (<see cref="ConcurrentQueue{T}"/>); im Legacy war sie eine ungeschützte
/// <c>LinkedList</c>, auf die gleichzeitig der Aufrufer und der Zustandsautomat
/// zugegriffen haben.
/// </item>
/// <item>
/// Der Legacy wartete 250 ms (<c>T_REPLY_DELAY</c>) auf eine mögliche Antwort,
/// bevor er "Reply Postponed" sendete. Da das Gateway ausschließlich BACnet-
/// <i>Client</i> ist (es beantwortet keine Anfragen), wird "Reply Postponed"
/// sofort gesendet.
/// </item>
/// </list>
/// </remarks>
internal sealed class MstpStateMachine
{
    /// <summary>Minimale Zeit ohne Frame, bevor ein Frame verworfen werden darf (ms).</summary>
    public const int TFrameAbort = 80;

    /// <summary>Zeit ohne Token, bevor der Tokenverlust erklärt wird (ms).</summary>
    public const int TNoToken = 500;

    /// <summary>Mindestwartezeit auf den Beginn einer Antwort (ms).</summary>
    public const int TReplyTimeout = 295;

    /// <summary>Mindestwartezeit auf die Nutzung eines Tokens (ms).</summary>
    public const int TUsageTimeout = 95;

    /// <summary>Maximale Wartezeit auf eine Antwort (ms).</summary>
    public const int TReplyDelay = 250;

    private const int MaxBufferLength = 502;
    private const byte Preamble1 = MSTP.MSTP_PREAMBLE1;
    private const byte Preamble2 = MSTP.MSTP_PREAMBLE2;

    private readonly ILogger _logger;
    private readonly ISerialPort _port;
    private readonly BacnetMstpOptions _options;
    private readonly ConcurrentQueue<MstpOutboundFrame> _sendQueue;
    private readonly Action<byte[], BacnetAddress> _frameReceived;
    private readonly IMstpObserver? _observer;
    private readonly byte[] _localBuffer = new byte[MaxBufferLength];

    private short _thisStation;
    private byte _nextStation;
    private byte _pollStation;
    private byte _maxMaster;
    private byte _maxInfoFrames;
    private byte _maxPoll;
    private byte _frameCount;
    private byte _tokenCount;
    private bool _soleMaster;
    private byte _retryToken = 1;
    private byte _replySource;
    private int _localOffset;

    /// <summary>
    /// Erzeugt den Zustandsautomaten.
    /// </summary>
    /// <param name="port">Serielle Schnittstelle (wird exklusiv vom Automaten benutzt).</param>
    /// <param name="options">Konfiguration.</param>
    /// <param name="sendQueue">Warteschlange der zu sendenden Frames.</param>
    /// <param name="frameReceived">Callback für empfangene NPDU/APDU-Nutzdaten.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="observer">
    /// Optionaler Beobachter für die Bus-Analyse. Er wird im Hot-Path
    /// aufgerufen und darf dort weder allokieren noch loggen.
    /// </param>
    public MstpStateMachine(
        ISerialPort port,
        BacnetMstpOptions options,
        ConcurrentQueue<MstpOutboundFrame> sendQueue,
        Action<byte[], BacnetAddress> frameReceived,
        ILogger logger,
        IMstpObserver? observer = null)
    {
        _port = port;
        _options = options;
        _sendQueue = sendQueue;
        _frameReceived = frameReceived;
        _logger = logger;
        _observer = observer;

        _thisStation = options.ThisStation;
        _maxMaster = options.MaxMaster;
        _maxInfoFrames = options.MaxInfoFrames;
        _maxPoll = options.MaxPoll;
    }

    /// <summary>
    /// Führt den Zustandsautomaten aus, bis der Port ausfällt oder der
    /// Abbruch angefordert wird.
    /// </summary>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>
    /// <see langword="true"/>, wenn der Automat wegen eines Portfehlers endete,
    /// <see langword="false"/>, wenn er regulär beendet wurde.
    /// </returns>
    public bool Run(CancellationToken cancellationToken)
    {
        var state = MstpState.Reset;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // **Kein Logging in diesem Pfad.**
                //
                // Der MS/TP-Token hat nur wenige Millisekunden Zeit
                // (TUsageTimeout = 95 ms). Ein Log-Eintrag - Datei, Konsole
                // oder Console-Handler - kostet I/O und würde das Zeitfenster
                // sprengen: der Token ginge verloren, der Master suchte einen
                // Nachfolger, der Bus kollabierte.
                //
                // Die Zustände werden deshalb **gesammelt**, nicht geschrieben:
                // der Beobachter (IMstpObserver) schreibt sie in einen
                // allokationsfreien Ringpuffer. Die Auswertung passiert
                // außerhalb dieses Threads.
                switch (state)
                {
                    case MstpState.Reset:
                        state = Initialize();
                        break;
                    case MstpState.DoneInitializing:
                    case MstpState.ReceivedUnexpectedFrame:
                    case MstpState.Reply:
                    case MstpState.DeferredReply:
                    case MstpState.SawTokenUser:
                        state = Idle(cancellationToken);
                        break;
                    case MstpState.GenerateToken:
                    case MstpState.FindNewSuccessor:
                    case MstpState.SendMaintenancePFM:
                    case MstpState.SoleMasterRestartMaintenancePFM:
                    case MstpState.NextStationUnknown:
                        state = PollForMaster(cancellationToken);
                        break;
                    case MstpState.DoneWithPFM:
                    case MstpState.ResetMaintenancePFM:
                    case MstpState.ReceivedReplyToPFM:
                    case MstpState.SendToken:
                        state = PassToken(cancellationToken);
                        break;
                    case MstpState.ReceivedDataNeedingReply:
                        state = AnswerDataRequest();
                        break;
                    case MstpState.ReceivedToken:
                    case MstpState.SoleMaster:
                    case MstpState.DeclareSoleMaster:
                    case MstpState.SendAnotherFrame:
                        state = UseToken();
                        break;
                    case MstpState.NothingToSend:
                    case MstpState.SendNoWait:
                    case MstpState.ReplyTimeOut:
                    case MstpState.InvalidFrame:
                    case MstpState.ReceivedReply:
                    case MstpState.ReceivedPostpone:
                        state = DoneWithToken();
                        break;
                    case MstpState.SendAndWait:
                        state = WaitForReply(cancellationToken);
                        break;
                    case MstpState.Fatal:
                        return true;
                    default:
                        throw new InvalidOperationException($"Unbekannter MS/TP-Zustand: {state}.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Regulärer Abbruch.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Der MS/TP-Zustandsautomat wurde wegen eines Fehlers beendet.");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Empfängt und verarbeitet Frames, bis ein Token eintrifft.
    /// </summary>
    private MstpState Idle(CancellationToken cancellationToken)
    {
        var noTokenTimeout = TNoToken + (10 * _thisStation);

        while (!cancellationToken.IsCancellationRequested)
        {
            var status = GetNextMessage(
                noTokenTimeout,
                cancellationToken,
                out var frameType,
                out var destinationAddress,
                out var sourceAddress,
                out var messageLength);

            if (status == GetMessageStatus.Good)
            {
                try
                {
                    if (destinationAddress == _thisStation || destinationAddress == 0xFF)
                    {
                        switch (frameType)
                        {
                            case BacnetMstpFrameTypes.FRAME_TYPE_POLL_FOR_MASTER:
                                if (destinationAddress == 0xFF)
                                {
                                    QueueFrame(BacnetMstpFrameTypes.FRAME_TYPE_REPLY_TO_POLL_FOR_MASTER, sourceAddress);
                                }
                                else
                                {
                                    SendFrame(BacnetMstpFrameTypes.FRAME_TYPE_REPLY_TO_POLL_FOR_MASTER, sourceAddress);
                                }

                                break;

                            case BacnetMstpFrameTypes.FRAME_TYPE_TOKEN:
                                if (destinationAddress != 0xFF)
                                {
                                    _frameCount = 0;
                                    _soleMaster = false;

                                    // Der Token wurde übernommen - das ist das
                                    // Ereignis, auf das die Analyse zeigt.
                                    _observer?.OnMstpEvent(
                                        MstpEventKind.TokenReceived,
                                        sourceAddress,
                                        (byte)frameType);

                                    return MstpState.ReceivedToken;
                                }

                                break;

                            case BacnetMstpFrameTypes.FRAME_TYPE_TEST_REQUEST:
                                if (destinationAddress == 0xFF)
                                {
                                    QueueFrame(BacnetMstpFrameTypes.FRAME_TYPE_TEST_RESPONSE, sourceAddress);
                                }
                                else
                                {
                                    SendFrame(BacnetMstpFrameTypes.FRAME_TYPE_TEST_RESPONSE, sourceAddress);
                                }

                                break;

                            case BacnetMstpFrameTypes.FRAME_TYPE_BACNET_DATA_NOT_EXPECTING_REPLY:
                            case BacnetMstpFrameTypes.FRAME_TYPE_BACNET_DATA_EXPECTING_REPLY:
                                DeliverFrame(sourceAddress, messageLength);

                                if (frameType == BacnetMstpFrameTypes.FRAME_TYPE_BACNET_DATA_EXPECTING_REPLY)
                                {
                                    _replySource = sourceAddress;
                                    return MstpState.ReceivedDataNeedingReply;
                                }

                                break;
                        }
                    }
                }
                finally
                {
                    RemoveCurrentMessage(messageLength);
                }
            }
            else if (status == GetMessageStatus.Timeout)
            {
                // Tokenverlust: wir erzeugen einen neuen Token.
                _pollStation = (byte)((_thisStation + 1) % (_maxMaster + 1));
                _nextStation = (byte)_thisStation;
                _tokenCount = 0;
                return MstpState.GenerateToken;
            }
            else if (status == GetMessageStatus.ConnectionClose)
            {
                _logger.LogWarning("Die serielle Schnittstelle wurde geschlossen.");
                return MstpState.Fatal;
            }
            else if (status == GetMessageStatus.ConnectionError)
            {
                _logger.LogError("Fehler auf der seriellen Schnittstelle.");
                return MstpState.Fatal;
            }
            else
            {
                _logger.LogDebug("Unsinnige Daten auf der seriellen Schnittstelle.");
            }
        }

        return MstpState.Fatal;
    }

    /// <summary>
    /// Master-Runde: Poll-For-Master senden und auf die Antwort warten.
    /// </summary>
    private MstpState PollForMaster(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Eine Warteschleife: der Master fragt der Reihe nach alle
            // Stationen ab, bis sich eine meldet.
            _observer?.OnMstpEvent(
                MstpEventKind.PollForMaster,
                _pollStation,
                (byte)BacnetMstpFrameTypes.FRAME_TYPE_POLL_FOR_MASTER);

            SendFrame(BacnetMstpFrameTypes.FRAME_TYPE_POLL_FOR_MASTER, _pollStation);

            var status = GetNextMessage(
                TUsageTimeout,
                cancellationToken,
                out var frameType,
                out var destinationAddress,
                out var sourceAddress,
                out var messageLength);

            if (status == GetMessageStatus.Good)
            {
                try
                {
                    if (frameType == BacnetMstpFrameTypes.FRAME_TYPE_REPLY_TO_POLL_FOR_MASTER &&
                        destinationAddress == _thisStation)
                    {
                        _soleMaster = false;
                        _nextStation = sourceAddress;
                        _pollStation = (byte)_thisStation;
                        _tokenCount = 0;
                        return MstpState.ReceivedReplyToPFM;
                    }

                    return MstpState.ReceivedUnexpectedFrame;
                }
                finally
                {
                    RemoveCurrentMessage(messageLength);
                }
            }

            if (status == GetMessageStatus.ConnectionClose || status == GetMessageStatus.ConnectionError)
            {
                return MstpState.Fatal;
            }

            if (_soleMaster)
            {
                _frameCount = 0;
                return MstpState.SoleMaster;
            }

            if (_nextStation != _thisStation)
            {
                return MstpState.DoneWithPFM;
            }

            if ((_pollStation + 1) % (_maxMaster + 1) != _thisStation)
            {
                _pollStation = (byte)((_pollStation + 1) % (_maxMaster + 1));
                continue;
            }

            _soleMaster = true;
            _frameCount = 0;

            // Kein anderer Knoten hat sich gemeldet - es gibt (noch) keinen
            // zweiten Teilnehmer auf dem Bus.
            _observer?.OnMstpEvent(MstpEventKind.SoleMaster, (byte)_thisStation, 0);

            return MstpState.DeclareSoleMaster;
        }

        return MstpState.Fatal;
    }

    /// <summary>
    /// Bestimmt, was nach dem Senden eines Frames zu tun ist.
    /// </summary>
    private MstpState DoneWithToken()
    {
        if (_frameCount < _maxInfoFrames)
        {
            return MstpState.SendAnotherFrame;
        }

        if (!_soleMaster && _nextStation == _thisStation)
        {
            _pollStation = (byte)((_thisStation + 1) % (_maxMaster + 1));
            return MstpState.NextStationUnknown;
        }

        if (_tokenCount < (_maxPoll - 1))
        {
            _tokenCount++;

            if (_soleMaster && _nextStation != ((_thisStation + 1) % (_maxMaster + 1)))
            {
                _frameCount = 0;
                return MstpState.SoleMaster;
            }

            return MstpState.SendToken;
        }

        if ((_pollStation + 1) % (_maxMaster + 1) == _nextStation)
        {
            if (!_soleMaster)
            {
                _pollStation = (byte)_thisStation;
                _tokenCount = 1;
                return MstpState.ResetMaintenancePFM;
            }

            _pollStation = (byte)((_nextStation + 1) % (_maxMaster + 1));
            _nextStation = (byte)_thisStation;
            _tokenCount = 1;
            return MstpState.SoleMasterRestartMaintenancePFM;
        }

        _pollStation = (byte)((_pollStation + 1) % (_maxMaster + 1));
        return MstpState.SendMaintenancePFM;
    }

    /// <summary>
    /// Sendet den nächsten Frame aus der Warteschlange.
    /// </summary>
    private MstpState UseToken()
    {
        if (!_sendQueue.TryDequeue(out var frame))
        {
            _frameCount = _maxInfoFrames;
            return MstpState.NothingToSend;
        }

        // Der Token wurde für einen eigenen Frame benutzt.
        _observer?.OnMstpEvent(MstpEventKind.TokenUsed, (byte)_thisStation, (byte)frame.FrameType);

        SendFrame(frame);
        _frameCount++;

        if (frame.FrameType == BacnetMstpFrameTypes.FRAME_TYPE_BACNET_DATA_EXPECTING_REPLY ||
            frame.FrameType == BacnetMstpFrameTypes.FRAME_TYPE_TEST_REQUEST)
        {
            return MstpState.SendAndWait;
        }

        return MstpState.SendNoWait;
    }

    /// <summary>
    /// Wartet nach dem Senden eines Frames auf die Antwort der Gegenstelle.
    /// </summary>
    private MstpState WaitForReply(CancellationToken cancellationToken)
    {
        var status = GetNextMessage(
            TReplyTimeout,
            cancellationToken,
            out var frameType,
            out var destinationAddress,
            out var sourceAddress,
            out var messageLength);

        if (status == GetMessageStatus.Good)
        {
            try
            {
                if (destinationAddress == _thisStation &&
                    (frameType == BacnetMstpFrameTypes.FRAME_TYPE_TEST_RESPONSE ||
                     frameType == BacnetMstpFrameTypes.FRAME_TYPE_BACNET_DATA_NOT_EXPECTING_REPLY))
                {
                    if (frameType == BacnetMstpFrameTypes.FRAME_TYPE_BACNET_DATA_NOT_EXPECTING_REPLY)
                    {
                        DeliverFrame(sourceAddress, messageLength);
                    }

                    return MstpState.ReceivedReply;
                }

                if (frameType == BacnetMstpFrameTypes.FRAME_TYPE_REPLY_POSTPONED)
                {
                    return MstpState.ReceivedPostpone;
                }

                return MstpState.ReceivedUnexpectedFrame;
            }
            finally
            {
                RemoveCurrentMessage(messageLength);
            }
        }

        if (status == GetMessageStatus.Timeout)
        {
            _frameCount = _maxInfoFrames;
            return MstpState.ReplyTimeOut;
        }

        if (status == GetMessageStatus.ConnectionClose || status == GetMessageStatus.ConnectionError)
        {
            return MstpState.Fatal;
        }

        return MstpState.InvalidFrame;
    }

    /// <summary>
    /// Gibt den Token an den nächsten Knoten weiter.
    /// </summary>
    private MstpState PassToken(CancellationToken cancellationToken)
    {
        for (var i = 0; i <= _retryToken; i++)
        {
            // Der Token wandert weiter - das ist der sichtbare "Umlauf".
            _observer?.OnMstpEvent(
                MstpEventKind.TokenPassed,
                _nextStation,
                (byte)BacnetMstpFrameTypes.FRAME_TYPE_TOKEN);

            SendFrame(BacnetMstpFrameTypes.FRAME_TYPE_TOKEN, _nextStation);

            var status = GetNextMessage(
                TUsageTimeout,
                cancellationToken,
                out _,
                out _,
                out _,
                out _);

            if (status == GetMessageStatus.Good || status == GetMessageStatus.DecodeError)
            {
                // Der Frame bleibt im Puffer - er wird im Idle-State verarbeitet.
                return MstpState.SawTokenUser;
            }

            if (status == GetMessageStatus.ConnectionClose || status == GetMessageStatus.ConnectionError)
            {
                return MstpState.Fatal;
            }
        }

        // Niemand hat den Token genommen: neuen Nachfolger suchen.
        _pollStation = (byte)((_nextStation + 1) % (_maxMaster + 1));
        _nextStation = (byte)_thisStation;
        _tokenCount = 0;
        return MstpState.FindNewSuccessor;
    }

    /// <summary>
    /// Beantwortet eine eingegangene Datenanfrage. Das Gateway ist reiner
    /// Client und kann keine Anfragen beantworten - es wird "Reply Postponed"
    /// gesendet.
    /// </summary>
    private MstpState AnswerDataRequest()
    {
        // Das Gateway ist reiner Client: es kann keine Anfrage beantworten
        // und sagt das dem Bus.
        _observer?.OnMstpEvent(MstpEventKind.ReplyPosted, _replySource, 0);

        SendFrame(BacnetMstpFrameTypes.FRAME_TYPE_REPLY_POSTPONED, _replySource);
        return MstpState.DeferredReply;
    }

    /// <summary>
    /// Setzt den Automaten in den Ausgangszustand.
    /// </summary>
    private MstpState Initialize()
    {
        // Der Token-Zähler wird auf das Maximum gesetzt, damit der erste
        // empfangene Token eine Poll-For-Master-Runde auslöst.
        _tokenCount = _maxPoll;
        _frameCount = 0;
        _soleMaster = false;
        _nextStation = (byte)_thisStation;
        _pollStation = (byte)_thisStation;
        return MstpState.DoneInitializing;
    }

    /// <summary>
    /// Übergibt die empfangene Nutzlast an die höhere Schicht.
    /// </summary>
    private void DeliverFrame(byte sourceAddress, int messageLength)
    {
        if (messageLength <= 0)
        {
            return;
        }

        // Eine BACnet-Nutzlast ist angekommen (nicht nur ein MS/TP-Frame).
        _observer?.OnMstpEvent(MstpEventKind.DataReceived, sourceAddress, 0);

        var payload = new byte[messageLength];
        Array.Copy(_localBuffer, MSTP.MSTP_HEADER_LENGTH, payload, 0, messageLength);

        var remoteAddress = new BacnetAddress(BacnetAddressTypes.MSTP, 0, new[] { sourceAddress });
        _frameReceived(payload, remoteAddress);
    }

    /// <summary>
    /// Legt einen Frame ohne Nutzdaten in die Sendewarteschlange.
    /// </summary>
    private void QueueFrame(BacnetMstpFrameTypes frameType, byte destinationAddress)
    {
        _sendQueue.Enqueue(new MstpOutboundFrame(frameType, destinationAddress, null, 0));
    }

    /// <summary>
    /// Schreibt einen Frame ohne Nutzdaten direkt auf die Schnittstelle.
    /// </summary>
    private void SendFrame(BacnetMstpFrameTypes frameType, byte destinationAddress)
    {
        SendFrame(new MstpOutboundFrame(frameType, destinationAddress, null, 0));
    }

    /// <summary>
    /// Schreibt einen Frame auf die Schnittstelle.
    /// </summary>
    private void SendFrame(MstpOutboundFrame frame)
    {
        if (_thisStation == -1)
        {
            return;
        }

        try
        {
            if (frame.Data is null || frame.Data.Length == 0)
            {
                var headerOnly = new byte[MSTP.MSTP_HEADER_LENGTH];
                var tx = MSTP.Encode(headerOnly, 0, frame.FrameType, frame.DestinationAddress, (byte)_thisStation, 0);
                _port.Write(headerOnly, 0, tx);
            }
            else
            {
                var tx = MSTP.Encode(frame.Data, 0, frame.FrameType, frame.DestinationAddress, (byte)_thisStation, frame.DataLength);
                _port.Write(frame.Data, 0, tx);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Schreiben eines MS/TP-Frames.");
        }
        finally
        {
            frame.Sent.TrySetResult(true);
        }

        // **Kein Logging im Hot-Path** - siehe Run(). Der Beobachter
        // bekommt das Ereignis allokationsfrei mitgeteilt.
        _observer?.OnMstpEvent(
            MstpEventKind.FrameSent,
            (byte)_thisStation,
            (byte)frame.FrameType);
    }

    /// <summary>
    /// Entfernt einen empfangenen Frame aus dem Empfangspuffer.
    /// </summary>
    private void RemoveCurrentMessage(int messageLength)
    {
        var fullMessageLength = MSTP.MSTP_HEADER_LENGTH + messageLength + (messageLength > 0 ? 2 : 0);

        if (_localOffset > fullMessageLength)
        {
            Array.Copy(_localBuffer, fullMessageLength, _localBuffer, 0, _localOffset - fullMessageLength);
        }

        _localOffset -= fullMessageLength;
    }

    /// <summary>
    /// Sucht im Empfangspuffer die Preamble und entfernt alles davor.
    /// </summary>
    private void RemoveGarbage()
    {
        for (var i = 0; i < (_localOffset - 1); i++)
        {
            if (_localBuffer[i] == Preamble1 && _localBuffer[i + 1] == Preamble2)
            {
                if (i > 0)
                {
                    Array.Copy(_localBuffer, i, _localBuffer, 0, _localOffset - i);
                    _localOffset -= i;
                }

                return;
            }
        }

        if (_localOffset > 0 && _localBuffer[_localOffset - 1] == Preamble1)
        {
            _localBuffer[0] = Preamble1;
            _localOffset = 1;
            return;
        }

        if (_localOffset > 0)
        {
            _localOffset = 0;
        }
    }

    /// <summary>
    /// Status eines Lesevorgangs auf der seriellen Schnittstelle.
    /// </summary>
    internal enum GetMessageStatus
    {
        /// <summary>Ein vollständiger Frame wurde gelesen.</summary>
        Good,

        /// <summary>Zeitüberschreitung.</summary>
        Timeout,

        /// <summary>Zeitüberschreitung innerhalb eines Frames.</summary>
        SubTimeout,

        /// <summary>Die Schnittstelle wurde geschlossen.</summary>
        ConnectionClose,

        /// <summary>Fehler auf der Schnittstelle.</summary>
        ConnectionError,

        /// <summary>Der Frame war fehlerhaft (z. B. CRC).</summary>
        DecodeError,
    }

    /// <summary>
    /// Liest den nächsten Frame von der seriellen Schnittstelle.
    /// </summary>
    private GetMessageStatus GetNextMessage(
        int timeoutMs,
        CancellationToken cancellationToken,
        out BacnetMstpFrameTypes frameType,
        out byte destinationAddress,
        out byte sourceAddress,
        out int messageLength)
    {
        frameType = BacnetMstpFrameTypes.FRAME_TYPE_TOKEN;
        destinationAddress = 0;
        sourceAddress = 0;
        messageLength = 0;

        // Header lesen.
        while (_localOffset < MSTP.MSTP_HEADER_LENGTH)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return GetMessageStatus.ConnectionClose;
            }

            // Innerhalb eines Frames gilt die kürzere Abbruchzeit.
            var timeout = _localOffset > 0 ? TFrameAbort : timeoutMs;

            var read = _port.Read(_localBuffer, _localOffset, MSTP.MSTP_HEADER_LENGTH - _localOffset, timeout);

            switch (read.Status)
            {
                case SerialReadStatus.Timeout:
                {
                    var status = _localOffset == 0 ? GetMessageStatus.Timeout : GetMessageStatus.SubTimeout;
                    _localBuffer[0] = 0xFF;
                    RemoveGarbage();
                    return status;
                }

                case SerialReadStatus.Error:
                    _localBuffer[0] = 0xFF;
                    RemoveGarbage();
                    return GetMessageStatus.ConnectionError;

                case SerialReadStatus.Closed:
                    _localBuffer[0] = 0xFF;
                    RemoveGarbage();
                    return GetMessageStatus.ConnectionClose;
            }

            _localOffset += read.BytesRead;

            // Füllbytes und Müll entfernen.
            RemoveGarbage();
        }

        // Header dekodieren.
        if (MSTP.Decode(_localBuffer, 0, _localOffset, out frameType, out destinationAddress, out sourceAddress, out messageLength) < 0)
        {
            _localBuffer[0] = 0xFF;
            RemoveGarbage();
            return GetMessageStatus.DecodeError;
        }

        var fullMessageLength = MSTP.MSTP_HEADER_LENGTH + messageLength + (messageLength > 0 ? 2 : 0);

        if (messageLength > MaxBufferLength)
        {
            _localBuffer[0] = 0xFF;
            RemoveGarbage();
            return GetMessageStatus.DecodeError;
        }

        // Nutzdaten lesen.
        if (messageLength > 0)
        {
            while (_localOffset < fullMessageLength)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return GetMessageStatus.ConnectionClose;
                }

                var read = _port.Read(_localBuffer, _localOffset, fullMessageLength - _localOffset, TFrameAbort);

                switch (read.Status)
                {
                    case SerialReadStatus.Timeout:
                    {
                        var status = _localOffset == 0 ? GetMessageStatus.Timeout : GetMessageStatus.SubTimeout;
                        _localBuffer[0] = 0xFF;
                        RemoveGarbage();
                        return status;
                    }

                    case SerialReadStatus.Error:
                        _localBuffer[0] = 0xFF;
                        RemoveGarbage();
                        return GetMessageStatus.ConnectionError;

                    case SerialReadStatus.Closed:
                        _localBuffer[0] = 0xFF;
                        RemoveGarbage();
                        return GetMessageStatus.ConnectionClose;
                }

                _localOffset += read.BytesRead;
            }

            // Daten-CRC prüfen.
            if (MSTP.Decode(_localBuffer, 0, _localOffset, out frameType, out destinationAddress, out sourceAddress, out messageLength) < 0)
            {
                _localBuffer[0] = 0xFF;
                RemoveGarbage();
                return GetMessageStatus.DecodeError;
            }
        }

        // **Kein Logging im Hot-Path** - siehe Run(). Der Beobachter
        // bekommt das Ereignis allokationsfrei mitgeteilt.
        _observer?.OnMstpEvent(MstpEventKind.FrameReceived, sourceAddress, (byte)frameType);

        return GetMessageStatus.Good;
    }
}

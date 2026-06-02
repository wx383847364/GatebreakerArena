using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using App.HotUpdate.GatebreakerArena.Application;
using App.HotUpdate.GatebreakerArena.Match;
using App.Shared.Contracts;

namespace App.HotUpdate.GatebreakerArena.Network
{
    public sealed class LanRoomService : ITickable
    {
        // LAN runtime currently starts MAP_ARENA_01 / Scene3v3, whose default layout is 1v1v1.
        private const int DefaultLanMapPlayerCount = 3;
        private const int DefaultMaxPlayers = DefaultLanMapPlayerCount;
        private const float AdvertiseIntervalSeconds = 1f;

        private readonly List<RoomSlot> _slots = new List<RoomSlot>();
        private readonly Dictionary<string, DiscoveredRoom> _discoveredRooms =
            new Dictionary<string, DiscoveredRoom>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, Dictionary<int, uint>> _checksumReportsByFrame =
            new Dictionary<int, Dictionary<int, uint>>();
        private readonly IAppLogger _logger;
        private readonly ILanDiagnosticsSink _diagnostics;
        private readonly LockstepSession _lockstepSession;
        private uint _nextSequence = 1;
        private float _advertiseTimer;
        private int _maxPlayers;
        private bool _isHost;
        private bool _playersFrozen;
        private string _roomCode = string.Empty;
        private string _lastError = string.Empty;
        private MatchAbortReason _abortReason = MatchAbortReason.None;
        private string _abortMessage = string.Empty;
        private object _hostEndpoint;
        private object _hostConnectionId;
        private int _hostTcpPort;
        private LockstepSyncState _lastDiagnosticSyncState = LockstepSyncState.Idle;
        private string _lastDiagnosticWaitingSlots = string.Empty;

        public LanRoomService(IAppLogger logger = null, ILanDiagnosticsSink diagnostics = null)
        {
            _logger = logger;
            _diagnostics = diagnostics;
            _lockstepSession = new LockstepSession();
            _lockstepSession.LocalInputReady += OnLocalInputReady;
            _lockstepSession.FrameBundleReady += OnFrameBundleReady;
            _lockstepSession.ChecksumReportReady += OnChecksumReportReady;
            _lockstepSession.Aborted += OnLockstepAborted;
        }

        public event Action<byte[]> UdpBroadcastRequested;
        public event Action<byte[], object> UdpSendRequested;
        public event Action<byte[], object> ReliableSendRequested;
        public event Action<RoomSnapshot> SnapshotChanged;
        public event Action<DiscoveredRoom> RoomDiscovered;

        public ulong SessionId { get; private set; }
        public uint ChannelId { get; private set; }
        public ulong LocalClientInstanceId { get; private set; }
        public string LocalPlayerName { get; private set; } = string.Empty;
        public LanRoomState State { get; private set; } = LanRoomState.Idle;
        public int LocalSlotIndex { get; private set; } = -1;
        public LockstepSession Lockstep => _lockstepSession;
        public IReadOnlyList<DiscoveredRoom> DiscoveredRooms => _discoveredRooms.Values.ToArray();

        public RoomSnapshot CurrentSnapshot => CreateSnapshot();

        public void Tick(float deltaTime)
        {
            if (_isHost && State == LanRoomState.Lobby)
            {
                _advertiseTimer -= Math.Max(0f, deltaTime);
                if (_advertiseTimer <= 0f)
                {
                    _advertiseTimer = AdvertiseIntervalSeconds;
                    AdvertiseRoom();
                }
            }

            _lockstepSession.Tick(deltaTime);
            RecordLockstepStateChanges();
        }

        public RoomSnapshot CreateHost(
            string playerName,
            ulong clientInstanceId,
            int maxPlayers = DefaultMaxPlayers,
            string roomCode = null,
            int tcpPort = 0)
        {
            ResetRoom();
            _isHost = true;
            State = LanRoomState.Lobby;
            SessionId = CreateSessionId(clientInstanceId);
            ChannelId = (uint)(SessionId & uint.MaxValue);
            LocalClientInstanceId = clientInstanceId;
            LocalPlayerName = NormalizeName(playerName);
            LocalSlotIndex = 0;
            _maxPlayers = Math.Max(1, maxPlayers);
            _hostTcpPort = tcpPort;
            _roomCode = string.IsNullOrWhiteSpace(roomCode) ? CreateRoomCode(SessionId) : roomCode.Trim().ToUpperInvariant();
            _slots.Add(new RoomSlot
            {
                SlotIndex = 0,
                SideOrder = 0,
                PlayerId = 1,
                ClientInstanceId = clientInstanceId,
                PlayerName = LocalPlayerName,
                IsHost = true,
                IsLocal = true,
                IsReady = true,
                IsLoadingAcked = false,
                IsActive = true,
            });

            for (int i = 1; i < _maxPlayers; i++)
            {
                _slots.Add(new RoomSlot { SlotIndex = i, SideOrder = i, PlayerId = i + 1 });
            }

            EnsureAiBackfillPlayers();
            _advertiseTimer = 0f;
            RecordRoomEvent("CreateHost", "ok", "tcpPort=" + tcpPort);
            PublishSnapshot();
            return CreateSnapshot();
        }

        public void StartDiscovery(ulong localClientInstanceId, string playerName)
        {
            ResetRoom();
            LocalClientInstanceId = localClientInstanceId;
            LocalPlayerName = NormalizeName(playerName);
            State = LanRoomState.Discovering;
            RecordRoomEvent("DiscoveryStart", "ok", string.Empty);
            PublishSnapshot();
        }

        public bool JoinDiscoveredRoom(string roomCode)
        {
            if (State != LanRoomState.Discovering ||
                string.IsNullOrWhiteSpace(roomCode) ||
                !_discoveredRooms.TryGetValue(roomCode.Trim().ToUpperInvariant(), out DiscoveredRoom room))
            {
                SetError("Room was not discovered.");
                RecordRoomEvent("JoinDiscoveredRoom", "notFound", roomCode);
                return false;
            }

            _hostEndpoint = room.ReliableEndpoint;
            SessionId = room.Advertise.SessionId;
            ChannelId = room.Advertise.ChannelId;
            _roomCode = room.Advertise.RoomCode;
            _maxPlayers = room.Advertise.MaxPlayers;
            State = LanRoomState.Joining;
            RecordRoomEvent("JoinRequestSend", "ok", EndpointToString(_hostEndpoint));
            SendReliable(
                GatebreakerNetworkMessageType.RoomJoinRequest,
                GatebreakerPayloadCodec.EncodeJoinRequest(new RoomJoinRequest
                {
                    ProtocolVersion = GatebreakerEnvelopeCodec.ProtocolVersion,
                    ClientInstanceId = LocalClientInstanceId,
                    PlayerName = LocalPlayerName,
                    RoomCode = _roomCode,
                }),
                _hostEndpoint);
            PublishSnapshot();
            return true;
        }

        public void HandleHostTransportStartFailed(string reason)
        {
            ResetRoom();
            RecordRoomEvent("HostTransportStartFailed", "error", reason ?? string.Empty);
            SetError("Host TCP start failed; LAN clients cannot join this room.");
        }

        public bool SetReady(bool isReady)
        {
            if (_playersFrozen || State != LanRoomState.Lobby)
            {
                RecordRoomEvent("ReadyChanged", "ignored", "state=" + State);
                return false;
            }

            if (_isHost)
            {
                RoomSlot slot = FindLocalSlot();
                if (slot == null)
                {
                    return false;
                }

                slot.IsReady = true;
                RecordRoomEvent("ReadyChanged", "ok", "ready=true;requested=" + isReady);
                BroadcastRoomSnapshot();
                PublishSnapshot();
                return true;
            }

            SendReliableToHost(
                GatebreakerNetworkMessageType.RoomReady,
                GatebreakerPayloadCodec.EncodeRoomReady(new RoomReadyCommand
                {
                    ClientInstanceId = LocalClientInstanceId,
                    IsReady = isReady,
                }));
            RecordRoomEvent("ReadyChanged", "sent", "ready=" + isReady);
            return true;
        }

        public void HandleReliableSendFailed(object endpoint, string reason)
        {
            string detail = string.IsNullOrWhiteSpace(reason) ? EndpointToString(endpoint) : reason;
            RecordRoomEvent("ReliableSendFailed", "error", detail);
            if (!_isHost && State == LanRoomState.Joining)
            {
                State = LanRoomState.Discovering;
                SetError("Join failed: TCP connection to host was not established.");
            }
        }

        public void RecordUiAction(string action, string detail = null)
        {
            RecordRoomEvent("Ui" + (string.IsNullOrWhiteSpace(action) ? "Action" : action), State.ToString(), detail ?? string.Empty);
        }

        public bool StartLoading()
        {
            if (!_isHost || State != LanRoomState.Lobby || !CanStart())
            {
                SetError("Room is not ready to start.");
                RecordRoomEvent("StartLoading", "rejected", State.ToString());
                return false;
            }

            EnsureAiBackfillPlayers();
            State = LanRoomState.Loading;
            RecordRoomEvent("StartLoading", "ok", string.Empty);
            foreach (RoomSlot slot in _slots)
            {
                if (slot.IsActive)
                {
                    slot.IsLoadingAcked = slot.IsHost || slot.IsAi;
                }
            }

            Broadcast(
                GatebreakerNetworkMessageType.RoomStartLoading,
                GatebreakerPayloadCodec.EncodeRoomSnapshot(CreateSnapshot()));
            BroadcastRoomSnapshot();
            PublishSnapshot();
            TryEnterPlaying();
            return true;
        }

        public bool AcknowledgeStart()
        {
            if (State != LanRoomState.Loading)
            {
                RecordRoomEvent("StartAck", "ignored", "state=" + State);
                return false;
            }

            if (_isHost)
            {
                RoomSlot slot = FindLocalSlot();
                if (slot != null)
                {
                    slot.IsLoadingAcked = true;
                }

                RecordRoomEvent("StartAckReceive", "localHost", string.Empty);
                TryEnterPlaying();
                PublishSnapshot();
                return true;
            }

            SendReliableToHost(
                GatebreakerNetworkMessageType.RoomStartAck,
                GatebreakerPayloadCodec.EncodeStartAck(new RoomStartAck
                {
                    ClientInstanceId = LocalClientInstanceId,
                    SlotIndex = LocalSlotIndex,
                }));
            RoomSlot localSlot = FindLocalSlot();
            if (localSlot != null)
            {
                localSlot.IsLoadingAcked = true;
            }

            RecordRoomEvent("StartAckSend", "ok", string.Empty);
            PublishSnapshot();
            return true;
        }

        public void Leave(string reason = null)
        {
            if (State == LanRoomState.Idle || State == LanRoomState.Left)
            {
                return;
            }

            RecordRoomEvent("Leave", "requested", reason ?? string.Empty);

            var notice = new RoomLeaveNotice
            {
                ClientInstanceId = LocalClientInstanceId,
                SlotIndex = LocalSlotIndex,
                Reason = reason ?? string.Empty,
            };

            if (_isHost)
            {
                Broadcast(
                    GatebreakerNetworkMessageType.RoomLeave,
                    GatebreakerPayloadCodec.EncodeLeaveNotice(notice));
            }
            else
            {
                SendReliableToHost(
                    GatebreakerNetworkMessageType.RoomLeave,
                    GatebreakerPayloadCodec.EncodeLeaveNotice(notice));
            }

            State = LanRoomState.Left;
            _diagnostics?.Flush();
            PublishSnapshot();
        }

        public void Abort(MatchAbortReason reason, string message = null)
        {
            RecordRoomEvent("Abort", reason.ToString(), message ?? string.Empty);
            ApplyAbort(reason, message);
            var notice = new RoomAbortNotice
            {
                Reason = reason,
                Message = message ?? string.Empty,
            };
            if (_isHost)
            {
                Broadcast(
                    GatebreakerNetworkMessageType.RoomAbort,
                    GatebreakerPayloadCodec.EncodeAbortNotice(notice));
            }
            else
            {
                SendReliableToHost(
                    GatebreakerNetworkMessageType.RoomAbort,
                    GatebreakerPayloadCodec.EncodeAbortNotice(notice));
            }
        }

        public bool HandleIncomingPacket(byte[] bytes, object endpoint = null, object connectionId = null)
        {
            if (!GatebreakerEnvelopeCodec.TryDecode(bytes, out GatebreakerEnvelope envelope))
            {
                RecordPacketEvent("PacketDecodeFailed", GatebreakerNetworkMessageType.Unknown, 0, 0, bytes != null ? bytes.Length : 0, 0, "decode");
                return false;
            }

            if (envelope.ProtocolVersion != GatebreakerEnvelopeCodec.ProtocolVersion)
            {
                RecordPacketEvent("PacketIgnored", envelope.MessageType, envelope.Sequence, envelope.PayloadHash, envelope.PayloadBytes.Length, envelope.SessionId, "protocolMismatch");
                return false;
            }

            if (SessionId != 0 &&
                envelope.SessionId != 0 &&
                envelope.SessionId != SessionId)
            {
                RecordPacketEvent("PacketIgnored", envelope.MessageType, envelope.Sequence, envelope.PayloadHash, envelope.PayloadBytes.Length, envelope.SessionId, "sessionMismatch");
                return false;
            }

            RecordPacketEvent("PacketReceived", envelope.MessageType, envelope.Sequence, envelope.PayloadHash, envelope.PayloadBytes.Length, envelope.SessionId, EndpointToString(endpoint));

            try
            {
                switch (envelope.MessageType)
                {
                    case GatebreakerNetworkMessageType.RoomAdvertise:
                        HandleAdvertise(GatebreakerPayloadCodec.DecodeRoomAdvertise(envelope.PayloadBytes), endpoint);
                        return true;
                    case GatebreakerNetworkMessageType.RoomJoinRequest:
                        HandleJoinRequest(GatebreakerPayloadCodec.DecodeJoinRequest(envelope.PayloadBytes), endpoint, connectionId);
                        return true;
                    case GatebreakerNetworkMessageType.RoomJoinResponse:
                        HandleJoinResponse(GatebreakerPayloadCodec.DecodeJoinResponse(envelope.PayloadBytes), endpoint, connectionId);
                        return true;
                    case GatebreakerNetworkMessageType.RoomSnapshot:
                        ApplyRemoteSnapshot(GatebreakerPayloadCodec.DecodeRoomSnapshot(envelope.PayloadBytes));
                        return true;
                    case GatebreakerNetworkMessageType.RoomReady:
                        HandleReady(GatebreakerPayloadCodec.DecodeRoomReady(envelope.PayloadBytes));
                        return true;
                    case GatebreakerNetworkMessageType.RoomStartLoading:
                        HandleStartLoading(GatebreakerPayloadCodec.DecodeRoomSnapshot(envelope.PayloadBytes));
                        return true;
                    case GatebreakerNetworkMessageType.RoomStartAck:
                        HandleStartAck(GatebreakerPayloadCodec.DecodeStartAck(envelope.PayloadBytes));
                        return true;
                    case GatebreakerNetworkMessageType.RoomPlaying:
                        ApplyRemoteSnapshot(GatebreakerPayloadCodec.DecodeRoomSnapshot(envelope.PayloadBytes));
                        EnterClientPlaying();
                        return true;
                    case GatebreakerNetworkMessageType.RoomLeave:
                        HandleLeave(GatebreakerPayloadCodec.DecodeLeaveNotice(envelope.PayloadBytes));
                        return true;
                    case GatebreakerNetworkMessageType.RoomAbort:
                        HandleAbort(GatebreakerPayloadCodec.DecodeAbortNotice(envelope.PayloadBytes));
                        return true;
                    case GatebreakerNetworkMessageType.LockstepInput:
                        HandleLockstepInput(GatebreakerPayloadCodec.DecodeLockstepInput(envelope.PayloadBytes));
                        return true;
                    case GatebreakerNetworkMessageType.LockstepFrameBundle:
                        LockstepFrameBundle receivedBundle = GatebreakerPayloadCodec.DecodeFrameBundle(envelope.PayloadBytes);
                        _lockstepSession.ReceiveFrameBundle(receivedBundle);
                        RecordFrameBundle("BundleReceived", receivedBundle);
                        PublishSnapshot();
                        return true;
                    case GatebreakerNetworkMessageType.ChecksumReport:
                        HandleChecksumReport(GatebreakerPayloadCodec.DecodeChecksumReport(envelope.PayloadBytes));
                        return true;
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                SetError(ex.Message);
                RecordPacketEvent("PacketHandleFailed", envelope.MessageType, envelope.Sequence, envelope.PayloadHash, envelope.PayloadBytes.Length, envelope.SessionId, ex.Message);
                _logger?.LogWarning("Gatebreaker LAN packet decode failed: {0}", ex.Message);
                return false;
            }
        }

        private void HandleAdvertise(RoomAdvertise advertise, object endpoint)
        {
            if (State != LanRoomState.Discovering ||
                advertise == null ||
                advertise.ProtocolVersion != GatebreakerEnvelopeCodec.ProtocolVersion ||
                advertise.TcpPort <= 0 ||
                string.IsNullOrWhiteSpace(advertise.RoomCode))
            {
                RecordRoomEvent("AdvertiseIgnored", "ignored", advertise != null ? advertise.RoomCode : string.Empty);
                return;
            }

            object reliableEndpoint = BuildReliableEndpoint(endpoint, advertise.TcpPort);
            var room = new DiscoveredRoom(advertise, endpoint, reliableEndpoint);
            string key = advertise.RoomCode.Trim().ToUpperInvariant();
            bool changed = !_discoveredRooms.TryGetValue(key, out DiscoveredRoom previous) ||
                           previous.Advertise == null ||
                           previous.Advertise.SessionId != advertise.SessionId ||
                           previous.Advertise.ChannelId != advertise.ChannelId ||
                           previous.Advertise.TcpPort != advertise.TcpPort ||
                           previous.Advertise.ActivePlayers != advertise.ActivePlayers ||
                           previous.Advertise.State != advertise.State ||
                           !Equals(previous.ReliableEndpoint, reliableEndpoint);
            _discoveredRooms[key] = room;
            if (changed)
            {
                RecordRoomEvent("AdvertiseAccepted", "ok", "udp=" + EndpointToString(endpoint) + ";tcpPort=" + advertise.TcpPort + ";reliable=" + EndpointToString(reliableEndpoint));
                RoomDiscovered?.Invoke(room);
            }
        }

        private void HandleJoinRequest(RoomJoinRequest request, object endpoint, object connectionId)
        {
            if (!_isHost || request == null)
            {
                return;
            }

            RoomJoinResponse response = TryAcceptJoin(request, endpoint, connectionId);
            RecordRoomEvent("JoinRequestReceive", response.Accepted ? "accepted" : response.Result.ToString(), request.RoomCode);
            SendReliable(
                GatebreakerNetworkMessageType.RoomJoinResponse,
                GatebreakerPayloadCodec.EncodeJoinResponse(response),
                connectionId ?? endpoint);
            if (response.Accepted)
            {
                BroadcastRoomSnapshot();
                PublishSnapshot();
            }
        }

        private RoomJoinResponse TryAcceptJoin(RoomJoinRequest request, object endpoint, object connectionId)
        {
            if (request.ProtocolVersion != GatebreakerEnvelopeCodec.ProtocolVersion)
            {
                return RejectJoin(LanRoomJoinResult.VersionMismatch, "Protocol version mismatch.");
            }

            if (_playersFrozen || State != LanRoomState.Lobby)
            {
                return RejectJoin(LanRoomJoinResult.AlreadyPlaying, "Room is already playing.");
            }

            if (!string.Equals(request.RoomCode, _roomCode, StringComparison.OrdinalIgnoreCase))
            {
                return RejectJoin(LanRoomJoinResult.InvalidRoom, "Room code mismatch.");
            }

            if (_slots.Any(slot => slot.IsActive && slot.ClientInstanceId == request.ClientInstanceId))
            {
                return RejectJoin(LanRoomJoinResult.DuplicateIdentity, "Client identity already joined.");
            }

            RoomSlot freeSlot = FindJoinTargetSlot();
            if (freeSlot == null)
            {
                RoomSnapshot snapshot = CreateSnapshot();
                string snapshotDetail = BuildRoomSnapshotDetail(snapshot);
                RecordRoomEvent("JoinTargetNotFound", "roomFull", snapshotDetail);
                return RejectJoin(LanRoomJoinResult.RoomFull, "Room is full. " + BuildRoomCountDetail(snapshot));
            }

            RecordRoomEvent("JoinTargetSelected", "ok", "slot=" + freeSlot.SlotIndex + ";playerId=" + freeSlot.PlayerId + ";name=" + freeSlot.PlayerName + ";isAi=" + freeSlot.IsAi);
            freeSlot.ClientInstanceId = request.ClientInstanceId;
            freeSlot.PlayerName = NormalizeName(request.PlayerName);
            freeSlot.IsHost = false;
            freeSlot.IsLocal = false;
            freeSlot.IsReady = false;
            freeSlot.IsLoadingAcked = false;
            freeSlot.IsActive = true;
            freeSlot.IsAi = false;
            freeSlot.Endpoint = endpoint;
            freeSlot.ConnectionId = connectionId;
            return new RoomJoinResponse
            {
                Accepted = true,
                Result = LanRoomJoinResult.Accepted,
                SessionId = SessionId,
                ChannelId = ChannelId,
                SlotIndex = freeSlot.SlotIndex,
                SideOrder = freeSlot.SideOrder,
                PlayerId = freeSlot.PlayerId,
                Snapshot = CreateSnapshot(),
            };
        }

        private RoomJoinResponse RejectJoin(LanRoomJoinResult result, string error)
        {
            return new RoomJoinResponse
            {
                Accepted = false,
                Result = result,
                Error = error,
                SessionId = SessionId,
                ChannelId = ChannelId,
                Snapshot = CreateSnapshot(),
            };
        }

        private void HandleJoinResponse(RoomJoinResponse response, object endpoint, object connectionId)
        {
            if (_isHost || response == null || State != LanRoomState.Joining)
            {
                return;
            }

            if (!response.Accepted)
            {
                State = LanRoomState.Discovering;
                SetError(response.Error);
                RecordRoomEvent("JoinResponseReceive", response.Result.ToString(), response.Error);
                return;
            }

            _hostEndpoint = endpoint ?? _hostEndpoint;
            _hostConnectionId = connectionId ?? _hostConnectionId;
            SessionId = response.SessionId;
            ChannelId = response.ChannelId;
            LocalSlotIndex = response.SlotIndex;
            State = LanRoomState.Lobby;
            RecordRoomEvent("JoinResponseReceive", "accepted", "slot=" + response.SlotIndex);
            ApplyRemoteSnapshot(response.Snapshot);
            PublishSnapshot();
        }

        private void HandleReady(RoomReadyCommand ready)
        {
            if (!_isHost || ready == null || State != LanRoomState.Lobby || _playersFrozen)
            {
                return;
            }

            RoomSlot slot = _slots.FirstOrDefault(item => item.IsActive && item.ClientInstanceId == ready.ClientInstanceId);
            if (slot == null || slot.IsHost)
            {
                return;
            }

            slot.IsReady = ready.IsReady;
            RecordRoomEvent("ReadyChanged", "remote", "slot=" + slot.SlotIndex + ";ready=" + ready.IsReady);
            BroadcastRoomSnapshot();
            PublishSnapshot();
        }

        private void HandleStartLoading(RoomSnapshot snapshot)
        {
            if (_isHost || snapshot == null)
            {
                return;
            }

            ApplyRemoteSnapshot(snapshot);
            State = LanRoomState.Loading;
            RecordRoomEvent("StartLoadingReceive", "ok", string.Empty);
            PublishSnapshot();
            AcknowledgeStart();
        }

        private void HandleStartAck(RoomStartAck ack)
        {
            if (!_isHost || ack == null || State != LanRoomState.Loading)
            {
                return;
            }

            RoomSlot slot = _slots.FirstOrDefault(item =>
                item.IsActive &&
                item.ClientInstanceId == ack.ClientInstanceId &&
                item.SlotIndex == ack.SlotIndex);
            if (slot == null)
            {
                return;
            }

            slot.IsLoadingAcked = true;
            RecordRoomEvent("StartAckReceive", "remote", "slot=" + ack.SlotIndex);
            TryEnterPlaying();
            BroadcastRoomSnapshot();
            PublishSnapshot();
        }

        private void HandleLeave(RoomLeaveNotice leave)
        {
            if (leave == null)
            {
                return;
            }

            if (_isHost)
            {
                RoomSlot slot = _slots.FirstOrDefault(item =>
                    item.IsActive &&
                    item.ClientInstanceId == leave.ClientInstanceId &&
                    (leave.SlotIndex < 0 || item.SlotIndex == leave.SlotIndex));
                if (slot == null)
                {
                    return;
                }

                if (_playersFrozen)
                {
                    RecordRoomEvent("LeaveReceive", "clientLeftDuringPlay", "slot=" + slot.SlotIndex);
                    Abort(MatchAbortReason.ClientLeft, "A player left during play.");
                    return;
                }

                ReplaceSlotWithAi(slot);
                RecordRoomEvent("LeaveReceive", "aiBackfill", leave.Reason);
                BroadcastRoomSnapshot();
                PublishSnapshot();
                return;
            }

            ApplyAbort(MatchAbortReason.HostLeft, "Host left the room.");
            RecordRoomEvent("LeaveReceive", "hostLeft", leave.Reason);
        }

        private void HandleAbort(RoomAbortNotice notice)
        {
            if (notice == null)
            {
                return;
            }

            ApplyAbort(notice.Reason, notice.Message);
            RecordRoomEvent("AbortReceive", notice.Reason.ToString(), notice.Message);
        }

        private void HandleLockstepInput(LockstepInputFrame input)
        {
            if (!_isHost || State != LanRoomState.Playing)
            {
                return;
            }

            _lockstepSession.SubmitInput(input);
            Record(new LanDiagnosticEvent
            {
                EventName = "RemoteInputReceived",
                SlotIndex = input.SlotIndex,
                PlayerId = input.PlayerId,
                FrameIndex = input.FrameIndex,
                Sequence = input.InputSeq,
            });
        }

        private void HandleChecksumReport(ChecksumReport report)
        {
            if (report == null)
            {
                return;
            }

            if (_isHost)
            {
                Record(new LanDiagnosticEvent
                {
                    EventName = "ChecksumReportReceived",
                    SlotIndex = report.SlotIndex,
                    FrameIndex = report.FrameIndex,
                    Checksum = report.Checksum,
                    Result = report.DesyncDetected ? "desyncFlag" : "ok",
                });
                RecordChecksumReport(report);
                return;
            }

            _lockstepSession.SubmitChecksumReport(report);
        }

        private void TryEnterPlaying()
        {
            if (!_isHost ||
                State != LanRoomState.Loading ||
                !_slots.Where(slot => slot.IsActive).All(slot => slot.IsLoadingAcked))
            {
                return;
            }

            State = LanRoomState.Playing;
            _playersFrozen = true;
            _lockstepSession.StartHost(CreateSnapshot().Players, LocalSlotIndex);
            RecordRoomEvent("PlayingEntered", "host", string.Empty);
            RoomSnapshot snapshot = CreateSnapshot();
            Broadcast(
                GatebreakerNetworkMessageType.RoomPlaying,
                GatebreakerPayloadCodec.EncodeRoomSnapshot(snapshot));
            foreach (LockstepFrameBundle bundle in _lockstepSession.ConsumeStartupBundles())
            {
                Broadcast(
                    GatebreakerNetworkMessageType.LockstepFrameBundle,
                    GatebreakerPayloadCodec.EncodeFrameBundle(bundle));
                RecordFrameBundle("BundleSent", bundle);
            }

            PublishSnapshot();
        }

        private void EnterClientPlaying()
        {
            if (_isHost)
            {
                return;
            }

            State = LanRoomState.Playing;
            _playersFrozen = true;
            _lockstepSession.StartClient(CreateSnapshot().Players, LocalSlotIndex);
            RecordRoomEvent("PlayingEntered", "client", string.Empty);
            PublishSnapshot();
        }

        private void ApplyRemoteSnapshot(RoomSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            SessionId = snapshot.SessionId;
            ChannelId = snapshot.ChannelId;
            _roomCode = snapshot.RoomCode ?? string.Empty;
            State = snapshot.State;
            _playersFrozen = snapshot.PlayersFrozen;
            _maxPlayers = snapshot.MaxPlayers;
            _lastError = snapshot.Error ?? string.Empty;
            _abortReason = snapshot.AbortReason;
            _abortMessage = snapshot.AbortMessage ?? string.Empty;
            _slots.Clear();
            foreach (RoomPlayerSnapshot player in snapshot.Players ?? Array.Empty<RoomPlayerSnapshot>())
            {
                _slots.Add(new RoomSlot
                {
                    SlotIndex = player.SlotIndex,
                    SideOrder = player.SideOrder,
                    PlayerId = player.PlayerId,
                    ClientInstanceId = player.ClientInstanceId,
                    PlayerName = player.PlayerName,
                    IsHost = player.IsHost,
                    IsLocal = player.ClientInstanceId == LocalClientInstanceId,
                    IsReady = player.IsReady,
                    IsLoadingAcked = player.IsLoadingAcked,
                    IsActive = player.IsActive,
                    IsAi = player.IsAi,
                });
            }

            RoomSlot local = FindLocalSlot();
            LocalSlotIndex = local?.SlotIndex ?? LocalSlotIndex;
            RecordRoomEvent("SnapshotReceive", "ok", "state=" + State);
            PublishSnapshot();
        }

        private void AdvertiseRoom()
        {
            if (!_isHost)
            {
                return;
            }

            var advertise = new RoomAdvertise
            {
                ProtocolVersion = GatebreakerEnvelopeCodec.ProtocolVersion,
                SessionId = SessionId,
                ChannelId = ChannelId,
                RoomCode = _roomCode,
                HostClientInstanceId = LocalClientInstanceId,
                HostPlayerName = LocalPlayerName,
                TcpPort = _hostTcpPort,
                MaxPlayers = _maxPlayers,
                ActivePlayers = _slots.Count(slot => slot.IsActive),
                State = State,
            };
            byte[] bytes = CreatePacket(
                GatebreakerNetworkMessageType.RoomAdvertise,
                GatebreakerPayloadCodec.EncodeRoomAdvertise(advertise),
                0,
                ChannelId);
            RecordRoomEvent("AdvertiseSend", "ok", "tcpPort=" + _hostTcpPort);
            UdpBroadcastRequested?.Invoke(bytes);
        }

        private void BroadcastRoomSnapshot()
        {
            Broadcast(
                GatebreakerNetworkMessageType.RoomSnapshot,
                GatebreakerPayloadCodec.EncodeRoomSnapshot(CreateSnapshot()));
        }

        private void Broadcast(GatebreakerNetworkMessageType messageType, byte[] payload)
        {
            foreach (RoomSlot slot in _slots)
            {
                if (!slot.IsActive || slot.IsHost || slot.IsAi)
                {
                    continue;
                }

                SendReliable(messageType, payload, slot.ConnectionId ?? slot.Endpoint);
            }
        }

        private void SendUdp(GatebreakerNetworkMessageType messageType, byte[] payload, object endpoint)
        {
            byte[] packet = CreatePacket(messageType, payload, SessionId, ChannelId);
            UdpSendRequested?.Invoke(packet, endpoint);
        }

        private void SendReliableToHost(GatebreakerNetworkMessageType messageType, byte[] payload)
        {
            SendReliable(messageType, payload, _hostConnectionId ?? _hostEndpoint);
        }

        private void SendReliable(GatebreakerNetworkMessageType messageType, byte[] payload, object connectionOrEndpoint)
        {
            byte[] packet = CreatePacket(messageType, payload, SessionId, ChannelId);
            RecordPacketEvent("PacketSend", messageType, _nextSequence - 1U, GatebreakerEnvelopeCodec.ComputePayloadHash(payload), payload != null ? payload.Length : 0, SessionId, EndpointToString(connectionOrEndpoint));
            ReliableSendRequested?.Invoke(packet, connectionOrEndpoint);
        }

        private byte[] CreatePacket(GatebreakerNetworkMessageType messageType, byte[] payload, ulong sessionId, uint channelId)
        {
            return GatebreakerEnvelopeCodec.Encode(
                messageType,
                _nextSequence++,
                sessionId,
                channelId,
                payload);
        }

        private RoomSnapshot CreateSnapshot()
        {
            return new RoomSnapshot
            {
                SessionId = SessionId,
                ChannelId = ChannelId,
                RoomCode = _roomCode,
                State = State,
                IsHost = _isHost,
                CanStart = _isHost && CanStart(),
                PlayersFrozen = _playersFrozen,
                LocalSlotIndex = LocalSlotIndex,
                MaxPlayers = _maxPlayers > 0 ? _maxPlayers : _slots.Count,
                Error = _lastError,
                AbortReason = _abortReason,
                AbortMessage = _abortMessage,
                Players = _slots.Where(slot => slot.IsActive).Select(slot => slot.ToSnapshot()).ToArray(),
                Lockstep = _lockstepSession.CreateSnapshot(),
            };
        }

        private bool CanStart()
        {
            if (!_isHost || State != LanRoomState.Lobby || _playersFrozen)
            {
                return false;
            }

            RoomSlot[] active = _slots.Where(slot => slot.IsActive).ToArray();
            return active.Any(slot => !slot.IsAi) &&
                   active.All(slot => slot.IsAi || slot.IsReady);
        }

        private void EnsureAiBackfillPlayers()
        {
            if (!_isHost || _playersFrozen)
            {
                return;
            }

            int targetPlayerCount = DefaultLanMapPlayerCount;
            int activeCount = _slots.Count(slot => slot.IsActive);
            if (activeCount >= targetPlayerCount)
            {
                return;
            }

            _maxPlayers = Math.Max(_maxPlayers, targetPlayerCount);
            EnsureSlotCapacity(targetPlayerCount);
            for (int i = 0; i < _slots.Count && activeCount < targetPlayerCount; i++)
            {
                RoomSlot slot = _slots[i];
                if (slot.IsActive)
                {
                    continue;
                }

                ReplaceSlotWithAi(slot);
                activeCount++;
                RecordRoomEvent("AiBackfillAdded", "ok", "slot=" + slot.SlotIndex + ";playerId=" + slot.PlayerId);
            }
        }

        private RoomSlot FindJoinTargetSlot()
        {
            RoomSlot aiSlot = _slots
                .Where(slot => IsReplaceableComputerSlot(slot))
                .OrderBy(slot => slot.SlotIndex)
                .FirstOrDefault();
            if (aiSlot != null)
            {
                return aiSlot;
            }

            return _slots.FirstOrDefault(slot => !slot.IsActive);
        }

        private static bool IsReplaceableComputerSlot(RoomSlot slot)
        {
            if (slot == null || !slot.IsActive || slot.IsHost)
            {
                return false;
            }

            if (slot.IsAi)
            {
                return true;
            }

            return slot.ClientInstanceId == 0UL &&
                   !string.IsNullOrWhiteSpace(slot.PlayerName) &&
                   slot.PlayerName.Trim().StartsWith("Computer ", StringComparison.OrdinalIgnoreCase);
        }

        private static void ReplaceSlotWithAi(RoomSlot slot)
        {
            if (slot == null)
            {
                return;
            }

            slot.ClientInstanceId = 0UL;
            slot.PlayerName = "Computer " + slot.PlayerId;
            slot.IsHost = false;
            slot.IsLocal = false;
            slot.IsReady = true;
            slot.IsLoadingAcked = true;
            slot.IsActive = true;
            slot.IsAi = true;
            slot.Endpoint = null;
            slot.ConnectionId = null;
        }

        private void EnsureSlotCapacity(int targetPlayerCount)
        {
            for (int i = _slots.Count; i < targetPlayerCount; i++)
            {
                _slots.Add(new RoomSlot
                {
                    SlotIndex = i,
                    SideOrder = i,
                    PlayerId = i + 1,
                });
            }
        }

        private void ApplyAbort(MatchAbortReason reason, string message)
        {
            _abortReason = reason;
            _abortMessage = message ?? string.Empty;
            _lastError = _abortMessage;
            State = LanRoomState.Aborted;
            _lockstepSession.Abort(reason, _abortMessage);
            _diagnostics?.Flush();
            PublishSnapshot();
        }

        private void OnLocalInputReady(LockstepInputFrame input)
        {
            if (_isHost || State != LanRoomState.Playing)
            {
                return;
            }

            SendReliableToHost(
                GatebreakerNetworkMessageType.LockstepInput,
                GatebreakerPayloadCodec.EncodeLockstepInput(input));
            Record(new LanDiagnosticEvent
            {
                EventName = "LocalInputSubmitted",
                SlotIndex = input.SlotIndex,
                PlayerId = input.PlayerId,
                FrameIndex = input.FrameIndex,
                Sequence = input.InputSeq,
            });
        }

        private void OnFrameBundleReady(LockstepFrameBundle bundle)
        {
            if (!_isHost || State != LanRoomState.Playing)
            {
                return;
            }

            Broadcast(
                GatebreakerNetworkMessageType.LockstepFrameBundle,
                GatebreakerPayloadCodec.EncodeFrameBundle(bundle));
            RecordFrameBundle("HostBundleBuilt", bundle);
        }

        private void OnChecksumReportReady(ChecksumReport report)
        {
            if (State != LanRoomState.Playing)
            {
                return;
            }

            if (_isHost)
            {
                Record(new LanDiagnosticEvent
                {
                    EventName = "ChecksumCreated",
                    SlotIndex = report.SlotIndex,
                    FrameIndex = report.FrameIndex,
                    Checksum = report.Checksum,
                });
                RecordChecksumReport(report);
                return;
            }

            Record(new LanDiagnosticEvent
            {
                EventName = "ChecksumReportSent",
                SlotIndex = report.SlotIndex,
                FrameIndex = report.FrameIndex,
                Checksum = report.Checksum,
            });
            SendReliableToHost(
                GatebreakerNetworkMessageType.ChecksumReport,
                GatebreakerPayloadCodec.EncodeChecksumReport(report));
        }

        private void OnLockstepAborted(MatchAbortReason reason, string message)
        {
            if (State == LanRoomState.Playing)
            {
                RecordRoomEvent("LockstepAborted", reason.ToString(), message);
                Abort(reason, message);
            }
        }

        private RoomSlot FindLocalSlot()
        {
            return _slots.FirstOrDefault(slot => slot.IsActive && slot.ClientInstanceId == LocalClientInstanceId);
        }

        private void ClearSlot(RoomSlot slot)
        {
            slot.ClientInstanceId = 0;
            slot.PlayerName = string.Empty;
            slot.IsHost = false;
            slot.IsLocal = false;
            slot.IsReady = false;
            slot.IsLoadingAcked = false;
            slot.IsActive = false;
            slot.IsAi = false;
            slot.Endpoint = null;
            slot.ConnectionId = null;
        }

        private void SetError(string error)
        {
            _lastError = error ?? string.Empty;
            PublishSnapshot();
        }

        private void PublishSnapshot()
        {
            RoomSnapshot snapshot = CreateSnapshot();
            RecordRoomSnapshotState(snapshot);
            SnapshotChanged?.Invoke(snapshot);
        }

        private void ResetRoom()
        {
            _slots.Clear();
            _discoveredRooms.Clear();
            _checksumReportsByFrame.Clear();
            _lockstepSession.Reset();
            _nextSequence = 1;
            _advertiseTimer = 0f;
            _maxPlayers = 0;
            _isHost = false;
            _playersFrozen = false;
            _roomCode = string.Empty;
            _lastError = string.Empty;
            _abortReason = MatchAbortReason.None;
            _abortMessage = string.Empty;
            _hostEndpoint = null;
            _hostConnectionId = null;
            _hostTcpPort = 0;
            SessionId = 0;
            ChannelId = 0;
            LocalSlotIndex = -1;
            State = LanRoomState.Idle;
            _lastDiagnosticSyncState = LockstepSyncState.Idle;
            _lastDiagnosticWaitingSlots = string.Empty;
        }

        private static string NormalizeName(string playerName)
        {
            return string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
        }

        private static ulong CreateSessionId(ulong clientInstanceId)
        {
            unchecked
            {
                ulong ticks = (ulong)DateTime.UtcNow.Ticks;
                return ticks ^ (clientInstanceId * 1099511628211UL);
            }
        }

        private static string CreateRoomCode(ulong sessionId)
        {
            const int roomCodeLength = 6;
            char[] chars = new char[roomCodeLength];
            ulong value = sessionId % 1000000UL;
            for (int i = 0; i < chars.Length; i++)
            {
                chars[chars.Length - 1 - i] = (char)('0' + value % 10UL);
                value /= 10UL;
            }

            return new string(chars);
        }

        private static object BuildReliableEndpoint(object discoveryEndpoint, int tcpPort)
        {
            if (tcpPort <= 0)
            {
                return discoveryEndpoint;
            }

            if (discoveryEndpoint is LanEndpoint lanEndpoint &&
                !string.IsNullOrEmpty(lanEndpoint.Address))
            {
                return new LanEndpoint(lanEndpoint.Address, tcpPort);
            }

            return discoveryEndpoint;
        }

        private void RecordChecksumReport(ChecksumReport report)
        {
            if (report == null || State != LanRoomState.Playing)
            {
                return;
            }

            if (report.DesyncDetected)
            {
                Record(new LanDiagnosticEvent
                {
                    EventName = "ChecksumMismatch",
                    SlotIndex = report.SlotIndex,
                    FrameIndex = report.FrameIndex,
                    Checksum = report.Checksum,
                    Result = "desyncFlag",
                });
                Abort(MatchAbortReason.Desync, "Checksum desync detected.");
                return;
            }

            if (!_slots.Any(slot => slot.IsActive && slot.SlotIndex == report.SlotIndex))
            {
                return;
            }

            if (!_checksumReportsByFrame.TryGetValue(report.FrameIndex, out Dictionary<int, uint> frameReports))
            {
                frameReports = new Dictionary<int, uint>();
                _checksumReportsByFrame.Add(report.FrameIndex, frameReports);
            }

            if (frameReports.TryGetValue(report.SlotIndex, out uint previousOwnChecksum) &&
                previousOwnChecksum != report.Checksum)
            {
                Record(new LanDiagnosticEvent
                {
                    EventName = "ChecksumMismatch",
                    SlotIndex = report.SlotIndex,
                    FrameIndex = report.FrameIndex,
                    Checksum = report.Checksum,
                    ReferenceChecksum = previousOwnChecksum,
                    Result = "changed",
                });
                Abort(MatchAbortReason.Desync, "Checksum changed for a reported slot.");
                return;
            }

            foreach (KeyValuePair<int, uint> existing in frameReports)
            {
                if (existing.Key != report.SlotIndex && existing.Value != report.Checksum)
                {
                    Record(new LanDiagnosticEvent
                    {
                        EventName = "ChecksumMismatch",
                        SlotIndex = report.SlotIndex,
                        FrameIndex = report.FrameIndex,
                        Checksum = report.Checksum,
                        ReferenceChecksum = existing.Value,
                        Result = "mismatchWithSlot=" + existing.Key,
                    });
                    Abort(MatchAbortReason.Desync, "Checksum mismatch detected.");
                    return;
                }
            }

            frameReports[report.SlotIndex] = report.Checksum;
        }

        private void RecordLockstepStateChanges()
        {
            LockstepSnapshot snapshot = _lockstepSession.CreateSnapshot();
            string waitingSlots = JoinInts(snapshot.WaitingSlotIndexes);
            if (snapshot.State != _lastDiagnosticSyncState)
            {
                string eventName = snapshot.State == LockstepSyncState.SyncWaiting
                    ? "WaitingStarted"
                    : (_lastDiagnosticSyncState == LockstepSyncState.SyncWaiting ? "WaitingRecovered" : "LockstepStateChanged");
                Record(new LanDiagnosticEvent
                {
                    EventName = eventName,
                    FrameIndex = snapshot.NextRequiredFrame,
                    Result = snapshot.State.ToString(),
                    Detail = waitingSlots,
                });
                _lastDiagnosticSyncState = snapshot.State;
            }

            if (snapshot.State == LockstepSyncState.SyncWaiting &&
                !string.Equals(waitingSlots, _lastDiagnosticWaitingSlots, StringComparison.Ordinal))
            {
                Record(new LanDiagnosticEvent
                {
                    EventName = "WaitingSlotsChanged",
                    FrameIndex = snapshot.NextRequiredFrame,
                    Detail = waitingSlots,
                });
            }

            _lastDiagnosticWaitingSlots = waitingSlots;
        }

        private static string JoinInts(int[] values)
        {
            if (values == null || values.Length <= 0)
            {
                return "-";
            }

            return string.Join(",", Array.ConvertAll(values, item => item.ToString()));
        }

        private void Record(LanDiagnosticEvent diagnosticEvent)
        {
            if (diagnosticEvent == null)
            {
                return;
            }

            diagnosticEvent.Role = string.IsNullOrEmpty(diagnosticEvent.Role)
                ? (_isHost ? "Host" : "Client")
                : diagnosticEvent.Role;
            diagnosticEvent.RoomCode = string.IsNullOrEmpty(diagnosticEvent.RoomCode) ? _roomCode : diagnosticEvent.RoomCode;
            diagnosticEvent.SessionId = diagnosticEvent.SessionId != 0UL ? diagnosticEvent.SessionId : SessionId;
            diagnosticEvent.ChannelId = diagnosticEvent.ChannelId != 0U ? diagnosticEvent.ChannelId : ChannelId;
            diagnosticEvent.ClientInstanceId = diagnosticEvent.ClientInstanceId != 0UL ? diagnosticEvent.ClientInstanceId : LocalClientInstanceId;
            diagnosticEvent.SlotIndex = diagnosticEvent.SlotIndex >= 0 ? diagnosticEvent.SlotIndex : LocalSlotIndex;
            _diagnostics?.Record(diagnosticEvent);
        }

        private void RecordRoomEvent(string eventName, string result, string detail)
        {
            Record(new LanDiagnosticEvent
            {
                EventName = eventName,
                Result = result ?? string.Empty,
                Detail = detail ?? string.Empty,
            });
        }

        private void RecordRoomSnapshotState(RoomSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            Record(new LanDiagnosticEvent
            {
                EventName = "RoomSnapshotState",
                Role = snapshot.IsHost ? "Host" : "Client",
                RoomCode = snapshot.RoomCode,
                SessionId = snapshot.SessionId,
                ChannelId = snapshot.ChannelId,
                SlotIndex = snapshot.LocalSlotIndex,
                Result = snapshot.State.ToString(),
                Detail = BuildRoomSnapshotDetail(snapshot),
            });
        }

        private static string BuildRoomSnapshotDetail(RoomSnapshot snapshot)
        {
            RoomPlayerSnapshot[] players = snapshot?.Players ?? Array.Empty<RoomPlayerSnapshot>();
            int active = players.Count(player => player != null && player.IsActive);
            int human = players.Count(player => player != null && player.IsActive && !player.IsAi);
            int ai = players.Count(player => player != null && player.IsActive && player.IsAi);
            var builder = new StringBuilder(256);
            builder.Append("state=").Append(snapshot != null ? snapshot.State.ToString() : "-")
                .Append(";canStart=").Append(snapshot != null && snapshot.CanStart ? "true" : "false")
                .Append(";active=").Append(active)
                .Append(";human=").Append(human)
                .Append(";ai=").Append(ai)
                .Append(";total=").Append(players.Length)
                .Append(";max=").Append(snapshot != null ? snapshot.MaxPlayers : 0)
                .Append(";localSlot=").Append(snapshot != null ? snapshot.LocalSlotIndex : -1)
                .Append(";frozen=").Append(snapshot != null && snapshot.PlayersFrozen ? "true" : "false")
                .Append(";roster=").Append(BuildRosterDetail(players));
            return builder.ToString();
        }

        private static string BuildRoomCountDetail(RoomSnapshot snapshot)
        {
            RoomPlayerSnapshot[] players = snapshot?.Players ?? Array.Empty<RoomPlayerSnapshot>();
            int active = players.Count(player => player != null && player.IsActive);
            int human = players.Count(player => player != null && player.IsActive && !player.IsAi);
            int ai = players.Count(player => player != null && player.IsActive && player.IsAi);
            return "active=" + active +
                   ";human=" + human +
                   ";ai=" + ai +
                   ";total=" + players.Length +
                   ";max=" + (snapshot != null ? snapshot.MaxPlayers : 0);
        }

        private static string BuildRosterDetail(RoomPlayerSnapshot[] players)
        {
            if (players == null || players.Length <= 0)
            {
                return "-";
            }

            var ordered = players
                .Where(player => player != null)
                .OrderBy(player => player.SlotIndex)
                .ThenBy(player => player.PlayerId)
                .ToArray();
            if (ordered.Length <= 0)
            {
                return "-";
            }

            var builder = new StringBuilder(256);
            for (int i = 0; i < ordered.Length; i++)
            {
                RoomPlayerSnapshot player = ordered[i];
                if (i > 0)
                {
                    builder.Append('|');
                }

                builder.Append("slot").Append(player.SlotIndex)
                    .Append("/p").Append(player.PlayerId)
                    .Append('/').Append(string.IsNullOrWhiteSpace(player.PlayerName) ? "-" : player.PlayerName)
                    .Append(player.IsAi ? "/AI" : "/Human")
                    .Append(player.IsHost ? "/Host" : string.Empty)
                    .Append(player.IsLocal ? "/Local" : string.Empty)
                    .Append(player.IsReady ? "/Ready" : "/NotReady")
                    .Append("/cid=").Append(player.ClientInstanceId);
            }

            return builder.ToString();
        }

        private void RecordPacketEvent(
            string eventName,
            GatebreakerNetworkMessageType messageType,
            uint sequence,
            uint payloadHash,
            int payloadBytes,
            ulong envelopeSessionId,
            string detail)
        {
            Record(new LanDiagnosticEvent
            {
                EventName = eventName,
                MessageType = messageType.ToString(),
                Sequence = sequence,
                PayloadHash = payloadHash,
                PayloadBytes = payloadBytes,
                SessionId = envelopeSessionId != 0UL ? envelopeSessionId : SessionId,
                Detail = detail ?? string.Empty,
            });
        }

        private void RecordFrameBundle(string eventName, LockstepFrameBundle bundle)
        {
            if (bundle == null)
            {
                return;
            }

            Record(new LanDiagnosticEvent
            {
                EventName = eventName,
                FrameIndex = bundle.FrameIndex,
                Sequence = bundle.BundleSeq,
                PayloadBytes = bundle.Inputs != null ? bundle.Inputs.Length : 0,
            });
            _diagnostics?.RecordFrameTrace(new LanFrameTrace
            {
                FrameIndex = bundle.FrameIndex,
                BundleSeq = bundle.BundleSeq,
                LatestConfirmedFrame = _lockstepSession.LatestConfirmedFrame,
                LocalTargetFrame = _lockstepSession.LocalTargetFrame,
                InputSlots = bundle.Inputs != null ? bundle.Inputs.Select(input => input.SlotIndex).ToArray() : Array.Empty<int>(),
                WaitingSlots = _lockstepSession.CreateSnapshot().WaitingSlotIndexes,
            });
        }

        private static string EndpointToString(object endpoint)
        {
            if (endpoint is LanEndpoint lanEndpoint)
            {
                return lanEndpoint.IsValid ? lanEndpoint.ToString() : string.Empty;
            }

            if (endpoint is LanConnectionId connectionId)
            {
                return connectionId.IsValid ? "connection:" + connectionId.Value : string.Empty;
            }

            return endpoint != null ? endpoint.ToString() : string.Empty;
        }

        private sealed class RoomSlot
        {
            public int SlotIndex { get; set; } = -1;
            public int SideOrder { get; set; } = -1;
            public int PlayerId { get; set; }
            public ulong ClientInstanceId { get; set; }
            public string PlayerName { get; set; } = string.Empty;
            public bool IsHost { get; set; }
            public bool IsLocal { get; set; }
            public bool IsReady { get; set; }
            public bool IsLoadingAcked { get; set; }
            public bool IsActive { get; set; }
            public bool IsAi { get; set; }
            public object Endpoint { get; set; }
            public object ConnectionId { get; set; }

            public RoomPlayerSnapshot ToSnapshot()
            {
                return new RoomPlayerSnapshot
                {
                    SlotIndex = SlotIndex,
                    SideOrder = SideOrder,
                    PlayerId = PlayerId,
                    ClientInstanceId = ClientInstanceId,
                    PlayerName = PlayerName,
                    IsHost = IsHost,
                    IsLocal = IsLocal,
                    IsReady = IsReady,
                    IsLoadingAcked = IsLoadingAcked,
                    IsActive = IsActive,
                    IsAi = IsAi,
                };
            }
        }
    }

    public sealed class DiscoveredRoom
    {
        public DiscoveredRoom(RoomAdvertise advertise, object discoveryEndpoint, object reliableEndpoint)
        {
            Advertise = advertise;
            DiscoveryEndpoint = discoveryEndpoint;
            ReliableEndpoint = reliableEndpoint;
        }

        public RoomAdvertise Advertise { get; }
        public object DiscoveryEndpoint { get; }
        public object ReliableEndpoint { get; }
        public object Endpoint => ReliableEndpoint;
    }

    /// <summary>
    /// Bridges the generic AOT LAN byte transport to the Gatebreaker room service.
    /// The bridge keeps Gatebreaker packet semantics in HotUpdate; AOT still only sees bytes.
    /// </summary>
    public sealed class LanRoomTransportBridge : IDisposable
    {
        private readonly LanRoomService _roomService;
        private readonly ILanTransport _transport;
        private readonly ILanDiagnosticsSink _diagnostics;
        private readonly Dictionary<LanEndpoint, LanConnectionId> _endpointConnections =
            new Dictionary<LanEndpoint, LanConnectionId>();
        private bool _disposed;

        public LanRoomTransportBridge(LanRoomService roomService, ILanTransport transport, ILanDiagnosticsSink diagnostics = null)
        {
            _roomService = roomService ?? throw new ArgumentNullException(nameof(roomService));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _diagnostics = diagnostics;

            _roomService.UdpBroadcastRequested += OnUdpBroadcastRequested;
            _roomService.UdpSendRequested += OnUdpSendRequested;
            _roomService.ReliableSendRequested += OnReliableSendRequested;
            _transport.EventReceived += OnTransportEvent;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _roomService.UdpBroadcastRequested -= OnUdpBroadcastRequested;
            _roomService.UdpSendRequested -= OnUdpSendRequested;
            _roomService.ReliableSendRequested -= OnReliableSendRequested;
            _transport.EventReceived -= OnTransportEvent;
            _endpointConnections.Clear();
        }

        private void OnUdpBroadcastRequested(byte[] payload)
        {
            _diagnostics?.Record(new LanDiagnosticEvent
            {
                EventName = "DiscoverySend",
                PayloadBytes = payload != null ? payload.Length : 0,
                Endpoint = "broadcast:" + _transport.DiscoveryPort,
            });
            _transport.SendDiscovery(payload ?? Array.Empty<byte>());
        }

        private void OnUdpSendRequested(byte[] payload, object endpoint)
        {
            if (endpoint is LanEndpoint lanEndpoint)
            {
                _diagnostics?.Record(new LanDiagnosticEvent
                {
                    EventName = "DiscoverySend",
                    PayloadBytes = payload != null ? payload.Length : 0,
                    Endpoint = lanEndpoint.ToString(),
                });
                _transport.SendDiscovery(payload ?? Array.Empty<byte>(), lanEndpoint);
            }
        }

        private void OnReliableSendRequested(byte[] payload, object connectionOrEndpoint)
        {
            byte[] safePayload = payload ?? Array.Empty<byte>();
            if (connectionOrEndpoint is LanConnectionId connectionId)
            {
                bool sent = _transport.Send(connectionId, safePayload);
                if (!sent)
                {
                    _diagnostics?.Record(new LanDiagnosticEvent
                    {
                        EventName = "TcpSendFail",
                        ConnectionId = connectionId.Value,
                        PayloadBytes = safePayload.Length,
                    });
                    _roomService.HandleReliableSendFailed(connectionId, "send failed");
                }

                return;
            }

            if (connectionOrEndpoint is LanEndpoint endpoint)
            {
                LanConnectionId resolved = ResolveConnection(endpoint);
                if (resolved.IsValid)
                {
                    bool sent = _transport.Send(resolved, safePayload);
                    if (!sent)
                    {
                        _diagnostics?.Record(new LanDiagnosticEvent
                        {
                            EventName = "TcpSendFail",
                            Endpoint = endpoint.ToString(),
                            ConnectionId = resolved.Value,
                            PayloadBytes = safePayload.Length,
                        });
                        _roomService.HandleReliableSendFailed(endpoint, "send failed");
                    }
                }
                else
                {
                    _roomService.HandleReliableSendFailed(endpoint, "connect failed");
                }
            }
        }

        private void OnTransportEvent(LanTransportEvent transportEvent)
        {
            if (transportEvent == null)
            {
                return;
            }

            _diagnostics?.RecordTransportEvent(transportEvent);
            switch (transportEvent.Type)
            {
                case LanTransportEventType.DiscoveryReceived:
                    _roomService.HandleIncomingPacket(
                        transportEvent.Payload,
                        transportEvent.RemoteEndpoint,
                        LanConnectionId.Invalid);
                    break;
                case LanTransportEventType.Connected:
                    if (transportEvent.RemoteEndpoint.IsValid && transportEvent.ConnectionId.IsValid)
                    {
                        _endpointConnections[transportEvent.RemoteEndpoint] = transportEvent.ConnectionId;
                    }

                    break;
                case LanTransportEventType.DataReceived:
                    _roomService.HandleIncomingPacket(
                        transportEvent.Payload,
                        transportEvent.RemoteEndpoint,
                        transportEvent.ConnectionId);
                    break;
                case LanTransportEventType.Disconnected:
                    RemoveConnection(transportEvent.ConnectionId);
                    break;
                case LanTransportEventType.Error:
                    break;
            }
        }

        private LanConnectionId ResolveConnection(LanEndpoint endpoint)
        {
            if (_endpointConnections.TryGetValue(endpoint, out LanConnectionId existing) && existing.IsValid)
            {
                return existing;
            }

            _diagnostics?.Record(new LanDiagnosticEvent
            {
                EventName = "TcpConnectStart",
                Endpoint = endpoint.ToString(),
            });
            LanConnectionId connectionId = _transport.Connect(endpoint);
            if (connectionId.IsValid)
            {
                _endpointConnections[endpoint] = connectionId;
            }
            else
            {
                _diagnostics?.Record(new LanDiagnosticEvent
                {
                    EventName = "TcpConnectFail",
                    Endpoint = endpoint.ToString(),
                });
            }

            return connectionId;
        }

        private void RemoveConnection(LanConnectionId connectionId)
        {
            if (!connectionId.IsValid)
            {
                return;
            }

            var endpointsToRemove = new List<LanEndpoint>();
            foreach (KeyValuePair<LanEndpoint, LanConnectionId> item in _endpointConnections)
            {
                if (item.Value.Equals(connectionId))
                {
                    endpointsToRemove.Add(item.Key);
                }
            }

            for (int i = 0; i < endpointsToRemove.Count; i++)
            {
                _endpointConnections.Remove(endpointsToRemove[i]);
            }
        }
    }

    public sealed class GatebreakerNetworkMatchController : ITickable
    {
        private const int ChecksumIntervalFrames = 30;

        private readonly LanRoomService _roomService;
        private readonly GatebreakerMatchRuntime _runtime;
        private readonly ILanDiagnosticsSink _diagnostics;
        private bool _runtimeStarted;
        private ulong _activeSessionId;
        private float _frameAccumulator;

        public GatebreakerNetworkMatchController(
            LanRoomService roomService,
            GatebreakerMatchRuntime runtime,
            ILanDiagnosticsSink diagnostics = null)
        {
            _roomService = roomService ?? throw new ArgumentNullException(nameof(roomService));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _diagnostics = diagnostics;
        }

        public void Tick(float deltaTime)
        {
            RoomSnapshot snapshot = _roomService.CurrentSnapshot;
            if (snapshot.State != LanRoomState.Playing)
            {
                _runtimeStarted = false;
                _activeSessionId = 0UL;
                _frameAccumulator = 0f;
                return;
            }

            if (!_runtimeStarted || _activeSessionId != snapshot.SessionId)
            {
                StartRuntime(snapshot);
            }

            SubmitHostAiInputs(snapshot);
            _frameAccumulator = Math.Min(
                _frameAccumulator + Math.Max(0f, deltaTime),
                _runtime.FrameDelta * 4f);
            if (_frameAccumulator < _runtime.FrameDelta)
            {
                return;
            }

            if (_roomService.Lockstep.TryDequeueConfirmedFrame(out LockstepFrameBundle bundle))
            {
                _frameAccumulator -= _runtime.FrameDelta;
                _diagnostics?.Record(new LanDiagnosticEvent
                {
                    EventName = "BundleDequeued",
                    Role = snapshot.IsHost ? "Host" : "Client",
                    RoomCode = snapshot.RoomCode,
                    SessionId = snapshot.SessionId,
                    ChannelId = snapshot.ChannelId,
                    SlotIndex = snapshot.LocalSlotIndex,
                    FrameIndex = bundle.FrameIndex,
                    Sequence = bundle.BundleSeq,
                    PayloadBytes = bundle.Inputs != null ? bundle.Inputs.Length : 0,
                });
                _runtime.StepFrame(
                    bundle.FrameIndex,
                    GatebreakerLockstepInputConverter.ToGatebreakerFrameInputs(bundle));
                if (bundle.FrameIndex % ChecksumIntervalFrames == 0)
                {
                    GatebreakerMatchChecksum checksum = _runtime.CreateChecksum(bundle.FrameIndex);
                    _diagnostics?.Record(new LanDiagnosticEvent
                    {
                        EventName = "ChecksumCreated",
                        Role = snapshot.IsHost ? "Host" : "Client",
                        RoomCode = snapshot.RoomCode,
                        SessionId = snapshot.SessionId,
                        ChannelId = snapshot.ChannelId,
                        SlotIndex = snapshot.LocalSlotIndex,
                        FrameIndex = bundle.FrameIndex,
                        Checksum = checksum.Value,
                    });
                    _diagnostics?.RecordFrameTrace(new LanFrameTrace
                    {
                        FrameIndex = bundle.FrameIndex,
                        BundleSeq = bundle.BundleSeq,
                        Checksum = checksum.Value,
                        LatestConfirmedFrame = _roomService.Lockstep.LatestConfirmedFrame,
                        LocalTargetFrame = _roomService.Lockstep.LocalTargetFrame,
                        InputSlots = bundle.Inputs != null ? bundle.Inputs.Select(input => input.SlotIndex).ToArray() : Array.Empty<int>(),
                        WaitingSlots = snapshot.Lockstep != null ? snapshot.Lockstep.WaitingSlotIndexes : Array.Empty<int>(),
                    });
                    _roomService.Lockstep.SubmitChecksumReport(new ChecksumReport
                    {
                        SlotIndex = snapshot.LocalSlotIndex,
                        FrameIndex = bundle.FrameIndex,
                        Checksum = checksum.Value,
                        DesyncDetected = false,
                    });
                }
            }
        }

        private void StartRuntime(RoomSnapshot snapshot)
        {
            RoomPlayerSnapshot[] activePlayers = (snapshot.Players ?? Array.Empty<RoomPlayerSnapshot>())
                .Where(player => player != null && player.IsActive)
                .OrderBy(player => player.SideOrder)
                .ToArray();
            int localPlayerId = activePlayers
                .Where(player => player.SlotIndex == snapshot.LocalSlotIndex)
                .Select(player => player.PlayerId)
                .FirstOrDefault();
            if (localPlayerId <= 0 && activePlayers.Length > 0)
            {
                localPlayerId = activePlayers[0].PlayerId;
            }

            _runtime.StartMatch(new GatebreakerMatchStartConfig
            {
                MatchId = snapshot.SessionId.ToString(),
                Seed = unchecked((int)snapshot.SessionId),
                SimulationFps = LockstepSession.SimulationFps,
                InputDelayFrames = 0,
                ModeId = "PVE_STANDARD",
                MapId = "MAP_ARENA_01",
                BallTypeId = "BALL_NORMAL",
                ActiveSlots = activePlayers.Select(player => player.PlayerId).ToArray(),
                PlayerSlots = activePlayers.Select(player => new GatebreakerMatchPlayerSlot
                {
                    SlotIndex = player.SlotIndex,
                    SideOrder = player.SideOrder,
                    PlayerId = player.PlayerId,
                    IsAi = player.IsAi,
                }).ToArray(),
                LocalPlayerId = localPlayerId,
                ConfigHash = "LAN_DEFAULT",
                TuningHash = "LAN_DEFAULT",
            });
            _runtimeStarted = true;
            _activeSessionId = snapshot.SessionId;
            _frameAccumulator = 0f;
            _diagnostics?.Record(new LanDiagnosticEvent
            {
                EventName = "RuntimeStarted",
                Role = snapshot.IsHost ? "Host" : "Client",
                RoomCode = snapshot.RoomCode,
                SessionId = snapshot.SessionId,
                ChannelId = snapshot.ChannelId,
                SlotIndex = snapshot.LocalSlotIndex,
                PlayerId = localPlayerId,
                Detail = "players=" + activePlayers.Length,
            });
        }

        private void SubmitHostAiInputs(RoomSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsHost || snapshot.Players == null)
            {
                return;
            }

            int frameIndex = _roomService.Lockstep.HostNextBundleFrame;
            for (int i = 0; i < snapshot.Players.Length; i++)
            {
                RoomPlayerSnapshot player = snapshot.Players[i];
                if (player == null || !player.IsActive || !player.IsAi || player.SlotIndex < 0)
                {
                    continue;
                }

                PlayerInputFrame aiFrame = _runtime.BuildAiInputFrame(player.PlayerId);
                ushort buttons = aiFrame.ServePressed ? GatebreakerLockstepInputConverter.ServeButton : (ushort)0;
                LockstepInputFrame input = _roomService.Lockstep.SubmitHostInputForSlot(
                    player.SlotIndex,
                    frameIndex,
                    GatebreakerLockstepInputConverter.QuantizeSignedUnit(aiFrame.MoveAxis),
                    GatebreakerLockstepInputConverter.QuantizeSignedUnit(aiFrame.AimDirection.x),
                    GatebreakerLockstepInputConverter.QuantizeSignedUnit(aiFrame.AimDirection.y),
                    buttons);
                if (input.PlayerId <= 0)
                {
                    continue;
                }

                _diagnostics?.Record(new LanDiagnosticEvent
                {
                    EventName = "AiInputSubmitted",
                    Role = "Host",
                    RoomCode = snapshot.RoomCode,
                    SessionId = snapshot.SessionId,
                    ChannelId = snapshot.ChannelId,
                    SlotIndex = input.SlotIndex,
                    PlayerId = input.PlayerId,
                    FrameIndex = input.FrameIndex,
                    Sequence = input.InputSeq,
                });
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.GatebreakerArena.Match;
using App.Shared.Contracts;

namespace App.HotUpdate.GatebreakerArena.Network
{
    public sealed class LanRoomService : ITickable
    {
        private const int DefaultMaxPlayers = 4;
        private const float AdvertiseIntervalSeconds = 1f;

        private readonly List<RoomSlot> _slots = new List<RoomSlot>();
        private readonly Dictionary<string, DiscoveredRoom> _discoveredRooms =
            new Dictionary<string, DiscoveredRoom>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, Dictionary<int, uint>> _checksumReportsByFrame =
            new Dictionary<int, Dictionary<int, uint>>();
        private readonly IAppLogger _logger;
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

        public LanRoomService(IAppLogger logger = null)
        {
            _logger = logger;
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

            _advertiseTimer = 0f;
            PublishSnapshot();
            return CreateSnapshot();
        }

        public void StartDiscovery(ulong localClientInstanceId, string playerName)
        {
            ResetRoom();
            LocalClientInstanceId = localClientInstanceId;
            LocalPlayerName = NormalizeName(playerName);
            State = LanRoomState.Discovering;
            PublishSnapshot();
        }

        public bool JoinDiscoveredRoom(string roomCode)
        {
            if (State != LanRoomState.Discovering ||
                string.IsNullOrWhiteSpace(roomCode) ||
                !_discoveredRooms.TryGetValue(roomCode.Trim().ToUpperInvariant(), out DiscoveredRoom room))
            {
                SetError("Room was not discovered.");
                return false;
            }

            _hostEndpoint = room.ReliableEndpoint;
            SessionId = room.Advertise.SessionId;
            ChannelId = room.Advertise.ChannelId;
            _roomCode = room.Advertise.RoomCode;
            _maxPlayers = room.Advertise.MaxPlayers;
            State = LanRoomState.Joining;
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

        public bool SetReady(bool isReady)
        {
            if (_playersFrozen || State != LanRoomState.Lobby)
            {
                return false;
            }

            if (_isHost)
            {
                RoomSlot slot = FindLocalSlot();
                if (slot == null)
                {
                    return false;
                }

                slot.IsReady = isReady;
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
            return true;
        }

        public bool StartLoading()
        {
            if (!_isHost || State != LanRoomState.Lobby || !CanStart())
            {
                SetError("Room is not ready to start.");
                return false;
            }

            State = LanRoomState.Loading;
            foreach (RoomSlot slot in _slots)
            {
                if (slot.IsActive)
                {
                    slot.IsLoadingAcked = slot.IsHost;
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
                return false;
            }

            if (_isHost)
            {
                RoomSlot slot = FindLocalSlot();
                if (slot != null)
                {
                    slot.IsLoadingAcked = true;
                }

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
            return true;
        }

        public void Leave(string reason = null)
        {
            if (State == LanRoomState.Idle || State == LanRoomState.Left)
            {
                return;
            }

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
            PublishSnapshot();
        }

        public void Abort(MatchAbortReason reason, string message = null)
        {
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
                return false;
            }

            if (envelope.ProtocolVersion != GatebreakerEnvelopeCodec.ProtocolVersion)
            {
                return false;
            }

            if (SessionId != 0 &&
                envelope.SessionId != 0 &&
                envelope.SessionId != SessionId)
            {
                return false;
            }

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
                        _lockstepSession.ReceiveFrameBundle(GatebreakerPayloadCodec.DecodeFrameBundle(envelope.PayloadBytes));
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
                _logger?.LogWarning("Gatebreaker LAN packet decode failed: {0}", ex.Message);
                return false;
            }
        }

        private void HandleAdvertise(RoomAdvertise advertise, object endpoint)
        {
            if (State != LanRoomState.Discovering ||
                advertise == null ||
                advertise.ProtocolVersion != GatebreakerEnvelopeCodec.ProtocolVersion ||
                string.IsNullOrWhiteSpace(advertise.RoomCode))
            {
                return;
            }

            object reliableEndpoint = BuildReliableEndpoint(endpoint, advertise.TcpPort);
            var room = new DiscoveredRoom(advertise, endpoint, reliableEndpoint);
            _discoveredRooms[advertise.RoomCode.Trim().ToUpperInvariant()] = room;
            RoomDiscovered?.Invoke(room);
        }

        private void HandleJoinRequest(RoomJoinRequest request, object endpoint, object connectionId)
        {
            if (!_isHost || request == null)
            {
                return;
            }

            RoomJoinResponse response = TryAcceptJoin(request, endpoint, connectionId);
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

            RoomSlot freeSlot = _slots.FirstOrDefault(slot => !slot.IsActive);
            if (freeSlot == null)
            {
                return RejectJoin(LanRoomJoinResult.RoomFull, "Room is full.");
            }

            freeSlot.ClientInstanceId = request.ClientInstanceId;
            freeSlot.PlayerName = NormalizeName(request.PlayerName);
            freeSlot.IsHost = false;
            freeSlot.IsLocal = false;
            freeSlot.IsReady = false;
            freeSlot.IsLoadingAcked = false;
            freeSlot.IsActive = true;
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
                return;
            }

            _hostEndpoint = endpoint ?? _hostEndpoint;
            _hostConnectionId = connectionId ?? _hostConnectionId;
            SessionId = response.SessionId;
            ChannelId = response.ChannelId;
            LocalSlotIndex = response.SlotIndex;
            State = LanRoomState.Lobby;
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
            PublishSnapshot();
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
                    Abort(MatchAbortReason.ClientLeft, "A player left during play.");
                    return;
                }

                ClearSlot(slot);
                BroadcastRoomSnapshot();
                PublishSnapshot();
                return;
            }

            ApplyAbort(MatchAbortReason.HostLeft, "Host left the room.");
        }

        private void HandleAbort(RoomAbortNotice notice)
        {
            if (notice == null)
            {
                return;
            }

            ApplyAbort(notice.Reason, notice.Message);
        }

        private void HandleLockstepInput(LockstepInputFrame input)
        {
            if (!_isHost || State != LanRoomState.Playing)
            {
                return;
            }

            _lockstepSession.SubmitInput(input);
        }

        private void HandleChecksumReport(ChecksumReport report)
        {
            if (report == null)
            {
                return;
            }

            if (_isHost)
            {
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
            RoomSnapshot snapshot = CreateSnapshot();
            Broadcast(
                GatebreakerNetworkMessageType.RoomPlaying,
                GatebreakerPayloadCodec.EncodeRoomSnapshot(snapshot));
            foreach (LockstepFrameBundle bundle in _lockstepSession.ConsumeStartupBundles())
            {
                Broadcast(
                    GatebreakerNetworkMessageType.LockstepFrameBundle,
                    GatebreakerPayloadCodec.EncodeFrameBundle(bundle));
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
                });
            }

            RoomSlot local = FindLocalSlot();
            LocalSlotIndex = local?.SlotIndex ?? LocalSlotIndex;
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
                if (!slot.IsActive || slot.IsHost)
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
            return active.Length >= 2 && active.All(slot => slot.IsReady);
        }

        private void ApplyAbort(MatchAbortReason reason, string message)
        {
            _abortReason = reason;
            _abortMessage = message ?? string.Empty;
            _lastError = _abortMessage;
            State = LanRoomState.Aborted;
            _lockstepSession.Abort(reason, _abortMessage);
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
        }

        private void OnChecksumReportReady(ChecksumReport report)
        {
            if (State != LanRoomState.Playing)
            {
                return;
            }

            if (_isHost)
            {
                RecordChecksumReport(report);
                return;
            }

            SendReliableToHost(
                GatebreakerNetworkMessageType.ChecksumReport,
                GatebreakerPayloadCodec.EncodeChecksumReport(report));
        }

        private void OnLockstepAborted(MatchAbortReason reason, string message)
        {
            if (State == LanRoomState.Playing)
            {
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
            SnapshotChanged?.Invoke(CreateSnapshot());
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
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            char[] chars = new char[6];
            ulong value = sessionId;
            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = alphabet[(int)(value % (ulong)alphabet.Length)];
                value /= (ulong)alphabet.Length;
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
                Abort(MatchAbortReason.Desync, "Checksum changed for a reported slot.");
                return;
            }

            foreach (KeyValuePair<int, uint> existing in frameReports)
            {
                if (existing.Key != report.SlotIndex && existing.Value != report.Checksum)
                {
                    Abort(MatchAbortReason.Desync, "Checksum mismatch detected.");
                    return;
                }
            }

            frameReports[report.SlotIndex] = report.Checksum;
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
        private readonly Dictionary<LanEndpoint, LanConnectionId> _endpointConnections =
            new Dictionary<LanEndpoint, LanConnectionId>();
        private bool _disposed;

        public LanRoomTransportBridge(LanRoomService roomService, ILanTransport transport)
        {
            _roomService = roomService ?? throw new ArgumentNullException(nameof(roomService));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));

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
            _transport.SendDiscovery(payload ?? Array.Empty<byte>());
        }

        private void OnUdpSendRequested(byte[] payload, object endpoint)
        {
            if (endpoint is LanEndpoint lanEndpoint)
            {
                _transport.SendDiscovery(payload ?? Array.Empty<byte>(), lanEndpoint);
            }
        }

        private void OnReliableSendRequested(byte[] payload, object connectionOrEndpoint)
        {
            byte[] safePayload = payload ?? Array.Empty<byte>();
            if (connectionOrEndpoint is LanConnectionId connectionId)
            {
                _transport.Send(connectionId, safePayload);
                return;
            }

            if (connectionOrEndpoint is LanEndpoint endpoint)
            {
                LanConnectionId resolved = ResolveConnection(endpoint);
                if (resolved.IsValid)
                {
                    _transport.Send(resolved, safePayload);
                }
            }
        }

        private void OnTransportEvent(LanTransportEvent transportEvent)
        {
            if (transportEvent == null)
            {
                return;
            }

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
            }
        }

        private LanConnectionId ResolveConnection(LanEndpoint endpoint)
        {
            if (_endpointConnections.TryGetValue(endpoint, out LanConnectionId existing) && existing.IsValid)
            {
                return existing;
            }

            LanConnectionId connectionId = _transport.Connect(endpoint);
            if (connectionId.IsValid)
            {
                _endpointConnections[endpoint] = connectionId;
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
        private bool _runtimeStarted;
        private ulong _activeSessionId;
        private float _frameAccumulator;

        public GatebreakerNetworkMatchController(LanRoomService roomService, GatebreakerMatchRuntime runtime)
        {
            _roomService = roomService ?? throw new ArgumentNullException(nameof(roomService));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
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
                _runtime.StepFrame(
                    bundle.FrameIndex,
                    GatebreakerLockstepInputConverter.ToGatebreakerFrameInputs(bundle));
                if (bundle.FrameIndex % ChecksumIntervalFrames == 0)
                {
                    GatebreakerMatchChecksum checksum = _runtime.CreateChecksum(bundle.FrameIndex);
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
                }).ToArray(),
                LocalPlayerId = localPlayerId,
                ConfigHash = "LAN_DEFAULT",
                TuningHash = "LAN_DEFAULT",
            });
            _runtimeStarted = true;
            _activeSessionId = snapshot.SessionId;
            _frameAccumulator = 0f;
        }
    }
}

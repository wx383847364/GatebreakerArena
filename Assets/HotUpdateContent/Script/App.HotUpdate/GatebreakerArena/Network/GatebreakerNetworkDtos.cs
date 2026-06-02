using System;

namespace App.HotUpdate.GatebreakerArena.Network
{
    public enum GatebreakerNetworkMessageType : ushort
    {
        Unknown = 0,
        RoomAdvertise = 1,
        RoomJoinRequest = 2,
        RoomJoinResponse = 3,
        RoomSnapshot = 4,
        RoomReady = 5,
        RoomStartLoading = 6,
        RoomStartAck = 7,
        RoomPlaying = 8,
        RoomLeave = 9,
        RoomAbort = 10,
        LockstepInput = 20,
        LockstepFrameBundle = 21,
        ChecksumReport = 22,
    }

    public enum LanRoomState : byte
    {
        Idle = 0,
        Discovering = 1,
        Lobby = 2,
        Joining = 3,
        Loading = 4,
        Playing = 5,
        Left = 6,
        Aborted = 7,
    }

    public enum LanRoomJoinResult : byte
    {
        Accepted = 0,
        RoomFull = 1,
        AlreadyPlaying = 2,
        VersionMismatch = 3,
        DuplicateIdentity = 4,
        InvalidRoom = 5,
        Rejected = 6,
    }

    public enum LockstepSyncState : byte
    {
        Idle = 0,
        Running = 1,
        SyncWaiting = 2,
        Desync = 3,
        Aborted = 4,
    }

    public enum MatchAbortReason : byte
    {
        None = 0,
        HostLeft = 1,
        ClientLeft = 2,
        MissingInputTimeout = 3,
        PayloadTooLarge = 4,
        PayloadHashMismatch = 5,
        ProtocolMismatch = 6,
        Desync = 7,
        ManualAbort = 8,
        TransportError = 9,
    }

    public sealed class RoomAdvertise
    {
        public ushort ProtocolVersion { get; set; }
        public ulong SessionId { get; set; }
        public uint ChannelId { get; set; }
        public string RoomCode { get; set; } = string.Empty;
        public ulong HostClientInstanceId { get; set; }
        public string HostPlayerName { get; set; } = string.Empty;
        public int TcpPort { get; set; }
        public int MaxPlayers { get; set; }
        public int ActivePlayers { get; set; }
        public LanRoomState State { get; set; }
    }

    public sealed class RoomJoinRequest
    {
        public ushort ProtocolVersion { get; set; }
        public ulong ClientInstanceId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string RoomCode { get; set; } = string.Empty;
    }

    public sealed class RoomJoinResponse
    {
        public bool Accepted { get; set; }
        public LanRoomJoinResult Result { get; set; }
        public string Error { get; set; } = string.Empty;
        public ulong SessionId { get; set; }
        public uint ChannelId { get; set; }
        public int SlotIndex { get; set; } = -1;
        public int SideOrder { get; set; } = -1;
        public int PlayerId { get; set; }
        public RoomSnapshot Snapshot { get; set; }
    }

    public sealed class RoomSnapshot
    {
        public ulong SessionId { get; set; }
        public uint ChannelId { get; set; }
        public string RoomCode { get; set; } = string.Empty;
        public LanRoomState State { get; set; }
        public bool IsHost { get; set; }
        public bool CanStart { get; set; }
        public bool PlayersFrozen { get; set; }
        public int LocalSlotIndex { get; set; } = -1;
        public int MaxPlayers { get; set; }
        public string Error { get; set; } = string.Empty;
        public MatchAbortReason AbortReason { get; set; }
        public string AbortMessage { get; set; } = string.Empty;
        public RoomPlayerSnapshot[] Players { get; set; } = Array.Empty<RoomPlayerSnapshot>();
        public LockstepSnapshot Lockstep { get; set; }
    }

    public sealed class RoomPlayerSnapshot
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
    }

    public sealed class RoomReadyCommand
    {
        public ulong ClientInstanceId { get; set; }
        public bool IsReady { get; set; }
    }

    public sealed class RoomStartAck
    {
        public ulong ClientInstanceId { get; set; }
        public int SlotIndex { get; set; }
    }

    public sealed class RoomLeaveNotice
    {
        public ulong ClientInstanceId { get; set; }
        public int SlotIndex { get; set; } = -1;
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class RoomAbortNotice
    {
        public MatchAbortReason Reason { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public readonly struct LockstepInputFrame
    {
        public LockstepInputFrame(
            int slotIndex,
            int playerId,
            int frameIndex,
            uint inputSeq,
            short moveAxisQ,
            short aimXQ,
            short aimYQ,
            ushort buttons)
        {
            SlotIndex = slotIndex;
            PlayerId = playerId;
            FrameIndex = frameIndex;
            InputSeq = inputSeq;
            MoveAxisQ = moveAxisQ;
            AimXQ = aimXQ;
            AimYQ = aimYQ;
            Buttons = buttons;
        }

        public int SlotIndex { get; }
        public int PlayerId { get; }
        public int FrameIndex { get; }
        public uint InputSeq { get; }
        public short MoveAxisQ { get; }
        public short AimXQ { get; }
        public short AimYQ { get; }
        public ushort Buttons { get; }
    }

    public sealed class LockstepFrameBundle
    {
        public int FrameIndex { get; set; }
        public uint BundleSeq { get; set; }
        public LockstepInputFrame[] Inputs { get; set; } = Array.Empty<LockstepInputFrame>();
    }

    public sealed class ChecksumReport
    {
        public int SlotIndex { get; set; }
        public int FrameIndex { get; set; }
        public uint Checksum { get; set; }
        public bool DesyncDetected { get; set; }
    }

    public sealed class LockstepSnapshot
    {
        public LockstepSyncState State { get; set; }
        public int SimulationFps { get; set; }
        public int InputDelay { get; set; }
        public int LatestConfirmedFrame { get; set; }
        public int NextRequiredFrame { get; set; }
        public int LocalTargetFrame { get; set; }
        public float WaitingSeconds { get; set; }
        public bool IsWaitingForInput { get; set; }
        public bool IsDesynced { get; set; }
        public bool IsAborted { get; set; }
        public MatchAbortReason AbortReason { get; set; }
        public string Error { get; set; } = string.Empty;
        public int[] WaitingSlotIndexes { get; set; } = Array.Empty<int>();
        public ChecksumReport[] DesyncReports { get; set; } = Array.Empty<ChecksumReport>();
    }
}

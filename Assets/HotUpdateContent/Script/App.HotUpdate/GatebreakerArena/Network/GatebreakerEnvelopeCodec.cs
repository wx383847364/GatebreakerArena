using System;
using System.Collections.Generic;
using System.Text;

namespace App.HotUpdate.GatebreakerArena.Network
{
    public readonly struct GatebreakerEnvelope
    {
        public GatebreakerEnvelope(
            ushort protocolVersion,
            GatebreakerNetworkMessageType messageType,
            uint sequence,
            ulong sessionId,
            uint channelId,
            byte[] payloadBytes,
            uint payloadHash)
        {
            ProtocolVersion = protocolVersion;
            MessageType = messageType;
            Sequence = sequence;
            SessionId = sessionId;
            ChannelId = channelId;
            PayloadBytes = payloadBytes ?? Array.Empty<byte>();
            PayloadHash = payloadHash;
        }

        public ushort ProtocolVersion { get; }
        public GatebreakerNetworkMessageType MessageType { get; }
        public uint Sequence { get; }
        public ulong SessionId { get; }
        public uint ChannelId { get; }
        public byte[] PayloadBytes { get; }
        public uint PayloadHash { get; }
    }

    public static class GatebreakerEnvelopeCodec
    {
        public const ushort ProtocolVersion = 2;
        public const int MaxPayloadBytes = 4096;
        private const int HeaderSize = 28;

        public static bool IsKnownMessageType(GatebreakerNetworkMessageType messageType)
        {
            switch (messageType)
            {
                case GatebreakerNetworkMessageType.RoomAdvertise:
                case GatebreakerNetworkMessageType.RoomJoinRequest:
                case GatebreakerNetworkMessageType.RoomJoinResponse:
                case GatebreakerNetworkMessageType.RoomSnapshot:
                case GatebreakerNetworkMessageType.RoomReady:
                case GatebreakerNetworkMessageType.RoomStartLoading:
                case GatebreakerNetworkMessageType.RoomStartAck:
                case GatebreakerNetworkMessageType.RoomPlaying:
                case GatebreakerNetworkMessageType.RoomLeave:
                case GatebreakerNetworkMessageType.RoomAbort:
                case GatebreakerNetworkMessageType.LockstepInput:
                case GatebreakerNetworkMessageType.LockstepFrameBundle:
                case GatebreakerNetworkMessageType.ChecksumReport:
                    return true;
                default:
                    return false;
            }
        }

        public static byte[] Encode(
            GatebreakerNetworkMessageType messageType,
            uint sequence,
            ulong sessionId,
            uint channelId,
            byte[] payloadBytes,
            ushort protocolVersion = ProtocolVersion)
        {
            if (!IsKnownMessageType(messageType))
            {
                throw new ArgumentOutOfRangeException(nameof(messageType), messageType, "Unknown Gatebreaker message type.");
            }

            payloadBytes = payloadBytes ?? Array.Empty<byte>();
            if (payloadBytes.Length > MaxPayloadBytes)
            {
                throw new ArgumentException("Gatebreaker LAN payload exceeds max payload bytes.", nameof(payloadBytes));
            }

            var writer = new LittleEndianWriter(HeaderSize + payloadBytes.Length);
            writer.WriteUInt16(protocolVersion);
            writer.WriteUInt16((ushort)messageType);
            writer.WriteUInt32(sequence);
            writer.WriteUInt64(sessionId);
            writer.WriteUInt32(channelId);
            writer.WriteInt32(payloadBytes.Length);
            writer.WriteUInt32(ComputePayloadHash(payloadBytes));
            writer.WriteBytes(payloadBytes);
            return writer.ToArray();
        }

        public static bool TryDecode(byte[] bytes, out GatebreakerEnvelope envelope)
        {
            envelope = new GatebreakerEnvelope();
            if (bytes == null || bytes.Length < HeaderSize)
            {
                return false;
            }

            var reader = new LittleEndianReader(bytes);
            ushort protocolVersion = reader.ReadUInt16();
            var messageType = (GatebreakerNetworkMessageType)reader.ReadUInt16();
            uint sequence = reader.ReadUInt32();
            ulong sessionId = reader.ReadUInt64();
            uint channelId = reader.ReadUInt32();
            int payloadLength = reader.ReadInt32();
            uint payloadHash = reader.ReadUInt32();
            if (protocolVersion != ProtocolVersion ||
                !IsKnownMessageType(messageType) ||
                payloadLength < 0 ||
                payloadLength > MaxPayloadBytes ||
                bytes.Length - HeaderSize != payloadLength)
            {
                return false;
            }

            byte[] payloadBytes = reader.ReadBytes(payloadLength);
            if (ComputePayloadHash(payloadBytes) != payloadHash)
            {
                return false;
            }

            envelope = new GatebreakerEnvelope(
                protocolVersion,
                messageType,
                sequence,
                sessionId,
                channelId,
                payloadBytes,
                payloadHash);
            return true;
        }

        public static uint ComputePayloadHash(byte[] payloadBytes)
        {
            unchecked
            {
                const uint fnvOffset = 2166136261;
                const uint fnvPrime = 16777619;
                uint hash = fnvOffset;
                if (payloadBytes == null)
                {
                    return hash;
                }

                for (int i = 0; i < payloadBytes.Length; i++)
                {
                    hash ^= payloadBytes[i];
                    hash *= fnvPrime;
                }

                return hash;
            }
        }
    }

    public static class GatebreakerPayloadCodec
    {
        public static byte[] EncodeRoomAdvertise(RoomAdvertise value)
        {
            var writer = new LittleEndianWriter();
            writer.WriteUInt16(value?.ProtocolVersion ?? GatebreakerEnvelopeCodec.ProtocolVersion);
            writer.WriteUInt64(value?.SessionId ?? 0UL);
            writer.WriteUInt32(value?.ChannelId ?? 0U);
            writer.WriteString(value?.RoomCode);
            writer.WriteUInt64(value?.HostClientInstanceId ?? 0UL);
            writer.WriteString(value?.HostPlayerName);
            writer.WriteInt32(value?.TcpPort ?? 0);
            writer.WriteInt32(value?.MaxPlayers ?? 0);
            writer.WriteInt32(value?.ActivePlayers ?? 0);
            writer.WriteByte((byte)(value?.State ?? LanRoomState.Idle));
            return writer.ToArray();
        }

        public static RoomAdvertise DecodeRoomAdvertise(byte[] payload)
        {
            var reader = new LittleEndianReader(payload);
            return new RoomAdvertise
            {
                ProtocolVersion = reader.ReadUInt16(),
                SessionId = reader.ReadUInt64(),
                ChannelId = reader.ReadUInt32(),
                RoomCode = reader.ReadString(),
                HostClientInstanceId = reader.ReadUInt64(),
                HostPlayerName = reader.ReadString(),
                TcpPort = reader.ReadInt32(),
                MaxPlayers = reader.ReadInt32(),
                ActivePlayers = reader.ReadInt32(),
                State = (LanRoomState)reader.ReadByte(),
            };
        }

        public static byte[] EncodeJoinRequest(RoomJoinRequest value)
        {
            var writer = new LittleEndianWriter();
            writer.WriteUInt16(value?.ProtocolVersion ?? GatebreakerEnvelopeCodec.ProtocolVersion);
            writer.WriteUInt64(value?.ClientInstanceId ?? 0UL);
            writer.WriteString(value?.PlayerName);
            writer.WriteString(value?.RoomCode);
            return writer.ToArray();
        }

        public static RoomJoinRequest DecodeJoinRequest(byte[] payload)
        {
            var reader = new LittleEndianReader(payload);
            return new RoomJoinRequest
            {
                ProtocolVersion = reader.ReadUInt16(),
                ClientInstanceId = reader.ReadUInt64(),
                PlayerName = reader.ReadString(),
                RoomCode = reader.ReadString(),
            };
        }

        public static byte[] EncodeJoinResponse(RoomJoinResponse value)
        {
            var writer = new LittleEndianWriter();
            writer.WriteBool(value?.Accepted ?? false);
            writer.WriteByte((byte)(value?.Result ?? LanRoomJoinResult.Rejected));
            writer.WriteString(value?.Error);
            writer.WriteUInt64(value?.SessionId ?? 0UL);
            writer.WriteUInt32(value?.ChannelId ?? 0U);
            writer.WriteInt32(value?.SlotIndex ?? -1);
            writer.WriteInt32(value?.SideOrder ?? -1);
            writer.WriteInt32(value?.PlayerId ?? 0);
            WriteRoomSnapshot(writer, value?.Snapshot);
            return writer.ToArray();
        }

        public static RoomJoinResponse DecodeJoinResponse(byte[] payload)
        {
            var reader = new LittleEndianReader(payload);
            return new RoomJoinResponse
            {
                Accepted = reader.ReadBool(),
                Result = (LanRoomJoinResult)reader.ReadByte(),
                Error = reader.ReadString(),
                SessionId = reader.ReadUInt64(),
                ChannelId = reader.ReadUInt32(),
                SlotIndex = reader.ReadInt32(),
                SideOrder = reader.ReadInt32(),
                PlayerId = reader.ReadInt32(),
                Snapshot = ReadRoomSnapshot(reader),
            };
        }

        public static byte[] EncodeRoomSnapshot(RoomSnapshot value)
        {
            var writer = new LittleEndianWriter();
            WriteRoomSnapshot(writer, value);
            return writer.ToArray();
        }

        public static RoomSnapshot DecodeRoomSnapshot(byte[] payload)
        {
            return ReadRoomSnapshot(new LittleEndianReader(payload));
        }

        public static byte[] EncodeRoomReady(RoomReadyCommand value)
        {
            var writer = new LittleEndianWriter();
            writer.WriteUInt64(value?.ClientInstanceId ?? 0UL);
            writer.WriteBool(value?.IsReady ?? false);
            return writer.ToArray();
        }

        public static RoomReadyCommand DecodeRoomReady(byte[] payload)
        {
            var reader = new LittleEndianReader(payload);
            return new RoomReadyCommand
            {
                ClientInstanceId = reader.ReadUInt64(),
                IsReady = reader.ReadBool(),
            };
        }

        public static byte[] EncodeStartAck(RoomStartAck value)
        {
            var writer = new LittleEndianWriter();
            writer.WriteUInt64(value?.ClientInstanceId ?? 0UL);
            writer.WriteInt32(value?.SlotIndex ?? -1);
            return writer.ToArray();
        }

        public static RoomStartAck DecodeStartAck(byte[] payload)
        {
            var reader = new LittleEndianReader(payload);
            return new RoomStartAck
            {
                ClientInstanceId = reader.ReadUInt64(),
                SlotIndex = reader.ReadInt32(),
            };
        }

        public static byte[] EncodeLeaveNotice(RoomLeaveNotice value)
        {
            var writer = new LittleEndianWriter();
            writer.WriteUInt64(value?.ClientInstanceId ?? 0UL);
            writer.WriteInt32(value?.SlotIndex ?? -1);
            writer.WriteString(value?.Reason);
            return writer.ToArray();
        }

        public static RoomLeaveNotice DecodeLeaveNotice(byte[] payload)
        {
            var reader = new LittleEndianReader(payload);
            return new RoomLeaveNotice
            {
                ClientInstanceId = reader.ReadUInt64(),
                SlotIndex = reader.ReadInt32(),
                Reason = reader.ReadString(),
            };
        }

        public static byte[] EncodeAbortNotice(RoomAbortNotice value)
        {
            var writer = new LittleEndianWriter();
            writer.WriteByte((byte)(value?.Reason ?? MatchAbortReason.None));
            writer.WriteString(value?.Message);
            return writer.ToArray();
        }

        public static RoomAbortNotice DecodeAbortNotice(byte[] payload)
        {
            var reader = new LittleEndianReader(payload);
            return new RoomAbortNotice
            {
                Reason = (MatchAbortReason)reader.ReadByte(),
                Message = reader.ReadString(),
            };
        }

        public static byte[] EncodeLockstepInput(LockstepInputFrame value)
        {
            var writer = new LittleEndianWriter();
            WriteLockstepInput(writer, value);
            return writer.ToArray();
        }

        public static LockstepInputFrame DecodeLockstepInput(byte[] payload)
        {
            return ReadLockstepInput(new LittleEndianReader(payload));
        }

        public static byte[] EncodeFrameBundle(LockstepFrameBundle value)
        {
            var writer = new LittleEndianWriter();
            writer.WriteInt32(value?.FrameIndex ?? 0);
            writer.WriteUInt32(value?.BundleSeq ?? 0U);
            WriteInputArray(writer, value?.Inputs);
            return writer.ToArray();
        }

        public static LockstepFrameBundle DecodeFrameBundle(byte[] payload)
        {
            var reader = new LittleEndianReader(payload);
            return new LockstepFrameBundle
            {
                FrameIndex = reader.ReadInt32(),
                BundleSeq = reader.ReadUInt32(),
                Inputs = ReadInputArray(reader),
            };
        }

        public static byte[] EncodeChecksumReport(ChecksumReport value)
        {
            var writer = new LittleEndianWriter();
            writer.WriteInt32(value?.SlotIndex ?? -1);
            writer.WriteInt32(value?.FrameIndex ?? 0);
            writer.WriteUInt32(value?.Checksum ?? 0U);
            writer.WriteBool(value?.DesyncDetected ?? false);
            return writer.ToArray();
        }

        public static ChecksumReport DecodeChecksumReport(byte[] payload)
        {
            var reader = new LittleEndianReader(payload);
            return new ChecksumReport
            {
                SlotIndex = reader.ReadInt32(),
                FrameIndex = reader.ReadInt32(),
                Checksum = reader.ReadUInt32(),
                DesyncDetected = reader.ReadBool(),
            };
        }

        private static void WriteRoomSnapshot(LittleEndianWriter writer, RoomSnapshot value)
        {
            writer.WriteUInt64(value?.SessionId ?? 0UL);
            writer.WriteUInt32(value?.ChannelId ?? 0U);
            writer.WriteString(value?.RoomCode);
            writer.WriteByte((byte)(value?.State ?? LanRoomState.Idle));
            writer.WriteBool(value?.IsHost ?? false);
            writer.WriteBool(value?.CanStart ?? false);
            writer.WriteBool(value?.PlayersFrozen ?? false);
            writer.WriteInt32(value?.LocalSlotIndex ?? -1);
            writer.WriteInt32(value?.MaxPlayers ?? 0);
            writer.WriteString(value?.Error);
            writer.WriteByte((byte)(value?.AbortReason ?? MatchAbortReason.None));
            writer.WriteString(value?.AbortMessage);
            WritePlayers(writer, value?.Players);
            WriteLockstepSnapshot(writer, value?.Lockstep);
        }

        private static RoomSnapshot ReadRoomSnapshot(LittleEndianReader reader)
        {
            return new RoomSnapshot
            {
                SessionId = reader.ReadUInt64(),
                ChannelId = reader.ReadUInt32(),
                RoomCode = reader.ReadString(),
                State = (LanRoomState)reader.ReadByte(),
                IsHost = reader.ReadBool(),
                CanStart = reader.ReadBool(),
                PlayersFrozen = reader.ReadBool(),
                LocalSlotIndex = reader.ReadInt32(),
                MaxPlayers = reader.ReadInt32(),
                Error = reader.ReadString(),
                AbortReason = (MatchAbortReason)reader.ReadByte(),
                AbortMessage = reader.ReadString(),
                Players = ReadPlayers(reader),
                Lockstep = ReadLockstepSnapshot(reader),
            };
        }

        private static void WritePlayers(LittleEndianWriter writer, RoomPlayerSnapshot[] players)
        {
            players = players ?? Array.Empty<RoomPlayerSnapshot>();
            writer.WriteInt32(players.Length);
            for (int i = 0; i < players.Length; i++)
            {
                RoomPlayerSnapshot player = players[i] ?? new RoomPlayerSnapshot();
                writer.WriteInt32(player.SlotIndex);
                writer.WriteInt32(player.SideOrder);
                writer.WriteInt32(player.PlayerId);
                writer.WriteUInt64(player.ClientInstanceId);
                writer.WriteString(player.PlayerName);
                writer.WriteBool(player.IsHost);
                writer.WriteBool(player.IsLocal);
                writer.WriteBool(player.IsReady);
                writer.WriteBool(player.IsLoadingAcked);
                writer.WriteBool(player.IsActive);
                writer.WriteBool(player.IsAi);
            }
        }

        private static RoomPlayerSnapshot[] ReadPlayers(LittleEndianReader reader)
        {
            int count = reader.ReadBoundedCount(16);
            var players = new RoomPlayerSnapshot[count];
            for (int i = 0; i < players.Length; i++)
            {
                players[i] = new RoomPlayerSnapshot
                {
                    SlotIndex = reader.ReadInt32(),
                    SideOrder = reader.ReadInt32(),
                    PlayerId = reader.ReadInt32(),
                    ClientInstanceId = reader.ReadUInt64(),
                    PlayerName = reader.ReadString(),
                    IsHost = reader.ReadBool(),
                    IsLocal = reader.ReadBool(),
                    IsReady = reader.ReadBool(),
                    IsLoadingAcked = reader.ReadBool(),
                    IsActive = reader.ReadBool(),
                    IsAi = reader.ReadBool(),
                };
            }

            return players;
        }

        private static void WriteLockstepSnapshot(LittleEndianWriter writer, LockstepSnapshot value)
        {
            writer.WriteBool(value != null);
            if (value == null)
            {
                return;
            }

            writer.WriteByte((byte)value.State);
            writer.WriteInt32(value.SimulationFps);
            writer.WriteInt32(value.InputDelay);
            writer.WriteInt32(value.LatestConfirmedFrame);
            writer.WriteInt32(value.NextRequiredFrame);
            writer.WriteInt32(value.LocalTargetFrame);
            writer.WriteSingle(value.WaitingSeconds);
            writer.WriteBool(value.IsWaitingForInput);
            writer.WriteBool(value.IsDesynced);
            writer.WriteBool(value.IsAborted);
            writer.WriteByte((byte)value.AbortReason);
            writer.WriteString(value.Error);
            WriteIntArray(writer, value.WaitingSlotIndexes);
            WriteChecksumReports(writer, value.DesyncReports);
        }

        private static LockstepSnapshot ReadLockstepSnapshot(LittleEndianReader reader)
        {
            if (!reader.ReadBool())
            {
                return null;
            }

            return new LockstepSnapshot
            {
                State = (LockstepSyncState)reader.ReadByte(),
                SimulationFps = reader.ReadInt32(),
                InputDelay = reader.ReadInt32(),
                LatestConfirmedFrame = reader.ReadInt32(),
                NextRequiredFrame = reader.ReadInt32(),
                LocalTargetFrame = reader.ReadInt32(),
                WaitingSeconds = reader.ReadSingle(),
                IsWaitingForInput = reader.ReadBool(),
                IsDesynced = reader.ReadBool(),
                IsAborted = reader.ReadBool(),
                AbortReason = (MatchAbortReason)reader.ReadByte(),
                Error = reader.ReadString(),
                WaitingSlotIndexes = ReadIntArray(reader),
                DesyncReports = ReadChecksumReports(reader),
            };
        }

        private static void WriteInputArray(LittleEndianWriter writer, LockstepInputFrame[] inputs)
        {
            inputs = inputs ?? Array.Empty<LockstepInputFrame>();
            writer.WriteInt32(inputs.Length);
            for (int i = 0; i < inputs.Length; i++)
            {
                WriteLockstepInput(writer, inputs[i]);
            }
        }

        private static LockstepInputFrame[] ReadInputArray(LittleEndianReader reader)
        {
            int count = reader.ReadBoundedCount(16);
            var inputs = new LockstepInputFrame[count];
            for (int i = 0; i < inputs.Length; i++)
            {
                inputs[i] = ReadLockstepInput(reader);
            }

            return inputs;
        }

        private static void WriteLockstepInput(LittleEndianWriter writer, LockstepInputFrame input)
        {
            writer.WriteInt32(input.SlotIndex);
            writer.WriteInt32(input.PlayerId);
            writer.WriteInt32(input.FrameIndex);
            writer.WriteUInt32(input.InputSeq);
            writer.WriteInt16(input.MoveAxisQ);
            writer.WriteInt16(input.AimXQ);
            writer.WriteInt16(input.AimYQ);
            writer.WriteUInt16(input.Buttons);
        }

        private static LockstepInputFrame ReadLockstepInput(LittleEndianReader reader)
        {
            return new LockstepInputFrame(
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadUInt32(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadUInt16());
        }

        private static void WriteIntArray(LittleEndianWriter writer, int[] values)
        {
            values = values ?? Array.Empty<int>();
            writer.WriteInt32(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                writer.WriteInt32(values[i]);
            }
        }

        private static int[] ReadIntArray(LittleEndianReader reader)
        {
            int count = reader.ReadBoundedCount(32);
            var values = new int[count];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = reader.ReadInt32();
            }

            return values;
        }

        private static void WriteChecksumReports(LittleEndianWriter writer, ChecksumReport[] reports)
        {
            reports = reports ?? Array.Empty<ChecksumReport>();
            writer.WriteInt32(reports.Length);
            for (int i = 0; i < reports.Length; i++)
            {
                ChecksumReport report = reports[i] ?? new ChecksumReport();
                writer.WriteInt32(report.SlotIndex);
                writer.WriteInt32(report.FrameIndex);
                writer.WriteUInt32(report.Checksum);
                writer.WriteBool(report.DesyncDetected);
            }
        }

        private static ChecksumReport[] ReadChecksumReports(LittleEndianReader reader)
        {
            int count = reader.ReadBoundedCount(32);
            var reports = new ChecksumReport[count];
            for (int i = 0; i < reports.Length; i++)
            {
                reports[i] = new ChecksumReport
                {
                    SlotIndex = reader.ReadInt32(),
                    FrameIndex = reader.ReadInt32(),
                    Checksum = reader.ReadUInt32(),
                    DesyncDetected = reader.ReadBool(),
                };
            }

            return reports;
        }
    }

    internal sealed class LittleEndianWriter
    {
        private const int MaxStringBytes = 256;
        private readonly List<byte> _bytes;

        public LittleEndianWriter(int capacity = 128)
        {
            _bytes = new List<byte>(capacity);
        }

        public void WriteByte(byte value)
        {
            _bytes.Add(value);
        }

        public void WriteBool(bool value)
        {
            _bytes.Add(value ? (byte)1 : (byte)0);
        }

        public void WriteInt16(short value)
        {
            WriteUInt16((ushort)value);
        }

        public void WriteUInt16(ushort value)
        {
            _bytes.Add((byte)value);
            _bytes.Add((byte)(value >> 8));
        }

        public void WriteInt32(int value)
        {
            WriteUInt32((uint)value);
        }

        public void WriteUInt32(uint value)
        {
            _bytes.Add((byte)value);
            _bytes.Add((byte)(value >> 8));
            _bytes.Add((byte)(value >> 16));
            _bytes.Add((byte)(value >> 24));
        }

        public void WriteUInt64(ulong value)
        {
            WriteUInt32((uint)value);
            WriteUInt32((uint)(value >> 32));
        }

        public void WriteSingle(float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            WriteBytes(bytes);
        }

        public void WriteString(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            if (bytes.Length > MaxStringBytes)
            {
                throw new ArgumentException("Gatebreaker LAN string exceeds max string bytes.");
            }

            WriteUInt16((ushort)bytes.Length);
            WriteBytes(bytes);
        }

        public void WriteBytes(byte[] bytes)
        {
            if (bytes == null)
            {
                return;
            }

            _bytes.AddRange(bytes);
        }

        public byte[] ToArray()
        {
            return _bytes.ToArray();
        }
    }

    internal sealed class LittleEndianReader
    {
        private readonly byte[] _bytes;
        private int _offset;

        public LittleEndianReader(byte[] bytes)
        {
            _bytes = bytes ?? Array.Empty<byte>();
        }

        public byte ReadByte()
        {
            EnsureAvailable(1);
            return _bytes[_offset++];
        }

        public bool ReadBool()
        {
            return ReadByte() != 0;
        }

        public short ReadInt16()
        {
            return (short)ReadUInt16();
        }

        public ushort ReadUInt16()
        {
            EnsureAvailable(2);
            ushort value = (ushort)(_bytes[_offset] | (_bytes[_offset + 1] << 8));
            _offset += 2;
            return value;
        }

        public int ReadInt32()
        {
            return (int)ReadUInt32();
        }

        public uint ReadUInt32()
        {
            EnsureAvailable(4);
            uint value =
                (uint)_bytes[_offset] |
                ((uint)_bytes[_offset + 1] << 8) |
                ((uint)_bytes[_offset + 2] << 16) |
                ((uint)_bytes[_offset + 3] << 24);
            _offset += 4;
            return value;
        }

        public ulong ReadUInt64()
        {
            ulong lo = ReadUInt32();
            ulong hi = ReadUInt32();
            return lo | (hi << 32);
        }

        public float ReadSingle()
        {
            EnsureAvailable(4);
            byte[] bytes = new byte[4];
            Buffer.BlockCopy(_bytes, _offset, bytes, 0, bytes.Length);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            float value = BitConverter.ToSingle(bytes, 0);
            _offset += 4;
            return value;
        }

        public string ReadString()
        {
            int count = ReadUInt16();
            EnsureAvailable(count);
            string value = Encoding.UTF8.GetString(_bytes, _offset, count);
            _offset += count;
            return value;
        }

        public byte[] ReadBytes(int count)
        {
            EnsureAvailable(count);
            var result = new byte[count];
            Buffer.BlockCopy(_bytes, _offset, result, 0, count);
            _offset += count;
            return result;
        }

        public int ReadBoundedCount(int maxCount)
        {
            int count = ReadInt32();
            if (count < 0 || count > maxCount)
            {
                throw new InvalidOperationException("Gatebreaker LAN payload count is outside the allowed range.");
            }

            return count;
        }

        private void EnsureAvailable(int count)
        {
            if (count < 0 || _offset + count > _bytes.Length)
            {
                throw new InvalidOperationException("Gatebreaker LAN payload is truncated.");
            }
        }
    }
}

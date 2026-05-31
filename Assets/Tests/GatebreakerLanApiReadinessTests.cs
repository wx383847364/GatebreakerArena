using System;
using System.IO;
using System.Linq;
using App.HotUpdate.GatebreakerArena.Network;
using App.Shared.Contracts;
using NUnit.Framework;

namespace Gatebreaker.Tests
{
    public sealed class GatebreakerLanApiReadinessTests
    {
        private static readonly string[] AotByteFramingCodecCandidates =
        {
            "App.AOT.Networking.Lan.LanByteFramingCodec, App.AOT",
            "App.AOT.Networking.Lan.LanFrameCodec, App.AOT",
            "App.Shared.Lan.LanByteFramingCodec, App.Shared",
            "App.Shared.Lockstep.LockstepFrameCodec, App.Shared",
        };

        private static readonly string[] RoomStateMachineCandidates =
        {
            "App.HotUpdate.GatebreakerArena.Network.LanRoomService, App.HotUpdate",
            "App.HotUpdate.GatebreakerArena.Lan.GatebreakerLanRoomClient, App.HotUpdate",
            "App.HotUpdate.GatebreakerArena.Lockstep.LockstepRoomStateMachine, App.HotUpdate",
            "App.HotUpdate.GatebreakerArena.Networking.GatebreakerRoomStateMachine, App.HotUpdate",
        };

        [Test]
        public void AotByteFramingCodecRejectsMalformedFramesWhenPublicApiExists()
        {
            // TODO: Replace this readiness guard with concrete byte framing roundtrip and malformed frame assertions
            // once the client AOT LAN framing codec has a public API.
            IgnoreUntilApiExists(
                FindType(AotByteFramingCodecCandidates),
                "AOT LAN byte framing codec public API is not present in Client/Assets.");
        }

        [Test]
        public void HotUpdateCodecRejectsPayloadHashMismatchWhenPublicApiExists()
        {
            byte[] payload = GatebreakerPayloadCodec.EncodeLockstepInput(
                new LockstepInputFrame(0, 1, 42, 7, 1000, 0, 1000, 1));
            byte[] encoded = GatebreakerEnvelopeCodec.Encode(
                GatebreakerNetworkMessageType.LockstepInput,
                7,
                123UL,
                1U,
                payload);
            encoded[encoded.Length - 1] ^= 0x7F;

            Assert.IsFalse(GatebreakerEnvelopeCodec.TryDecode(encoded, out _));
        }

        [Test]
        public void HotUpdateCodecRejectsUnknownMessageTypeWhenPublicApiExists()
        {
            byte[] encoded = EncodeEnvelopeForDecodeOnly(
                999,
                GatebreakerEnvelopeCodec.ProtocolVersion,
                1,
                123UL,
                1U,
                new byte[] { 1, 2, 3 });

            Assert.IsFalse(GatebreakerEnvelopeCodec.TryDecode(encoded, out _));
        }

        [Test]
        public void HotUpdateCodecRejectsProtocolVersionMismatchWhenPublicApiExists()
        {
            byte[] encoded = EncodeEnvelopeForDecodeOnly(
                (ushort)GatebreakerNetworkMessageType.RoomReady,
                GatebreakerEnvelopeCodec.ProtocolVersion + 1,
                1,
                123UL,
                1U,
                GatebreakerPayloadCodec.EncodeRoomReady(new RoomReadyCommand
                {
                    ClientInstanceId = 123UL,
                    IsReady = true,
                }));

            Assert.IsFalse(GatebreakerEnvelopeCodec.TryDecode(encoded, out _));
        }

        [Test]
        public void HotUpdateCodecRoundTripsReadyStartAckFrameBundleAndAbortPayloads()
        {
            RoomReadyCommand ready = GatebreakerPayloadCodec.DecodeRoomReady(RoundTrip(
                GatebreakerNetworkMessageType.RoomReady,
                GatebreakerPayloadCodec.EncodeRoomReady(new RoomReadyCommand
                {
                    ClientInstanceId = 123UL,
                    IsReady = true,
                })));
            RoomStartAck startAck = GatebreakerPayloadCodec.DecodeStartAck(RoundTrip(
                GatebreakerNetworkMessageType.RoomStartAck,
                GatebreakerPayloadCodec.EncodeStartAck(new RoomStartAck
                {
                    ClientInstanceId = 123UL,
                    SlotIndex = 0,
                })));
            LockstepFrameBundle bundle = GatebreakerPayloadCodec.DecodeFrameBundle(RoundTrip(
                GatebreakerNetworkMessageType.LockstepFrameBundle,
                GatebreakerPayloadCodec.EncodeFrameBundle(new LockstepFrameBundle
                {
                    FrameIndex = 10,
                    BundleSeq = 77,
                    Inputs = new[]
                    {
                        new LockstepInputFrame(0, 1, 10, 77, 500, 0, 1000, 1),
                    },
                })));
            RoomAbortNotice abort = GatebreakerPayloadCodec.DecodeAbortNotice(RoundTrip(
                GatebreakerNetworkMessageType.RoomAbort,
                GatebreakerPayloadCodec.EncodeAbortNotice(new RoomAbortNotice
                {
                    Reason = MatchAbortReason.MissingInputTimeout,
                    Message = "missing input",
                })));

            Assert.IsTrue(ready.IsReady);
            Assert.AreEqual(0, startAck.SlotIndex);
            Assert.AreEqual(10, bundle.FrameIndex);
            Assert.AreEqual(1, bundle.Inputs.Length);
            Assert.AreEqual(MatchAbortReason.MissingInputTimeout, abort.Reason);
        }

        [Test]
        public void RoomAdvertiseCarriesTcpPortForReliableJoinEndpoint()
        {
            var client = new LanRoomService();
            client.StartDiscovery(2222UL, "Client");

            byte[] advertisePacket = GatebreakerEnvelopeCodec.Encode(
                GatebreakerNetworkMessageType.RoomAdvertise,
                1,
                0,
                7,
                GatebreakerPayloadCodec.EncodeRoomAdvertise(new RoomAdvertise
                {
                    ProtocolVersion = GatebreakerEnvelopeCodec.ProtocolVersion,
                    SessionId = 12345UL,
                    ChannelId = 7,
                    RoomCode = "ABC123",
                    HostClientInstanceId = 1111UL,
                    HostPlayerName = "Host",
                    TcpPort = 54321,
                    MaxPlayers = 4,
                    ActivePlayers = 1,
                    State = LanRoomState.Lobby,
                }));

            Assert.IsTrue(client.HandleIncomingPacket(advertisePacket, new LanEndpoint("192.168.1.20", 47680)));

            DiscoveredRoom room = client.DiscoveredRooms.Single();
            Assert.AreEqual(54321, room.Advertise.TcpPort);
            Assert.AreEqual(new LanEndpoint("192.168.1.20", 54321), room.ReliableEndpoint);
        }

        [Test]
        public void CreateHostGeneratesNumericRoomCodeByDefault()
        {
            var host = new LanRoomService();

            RoomSnapshot snapshot = host.CreateHost("Host", 1001UL);

            Assert.AreEqual(6, snapshot.RoomCode.Length);
            Assert.IsTrue(snapshot.RoomCode.All(char.IsDigit));
        }

        [Test]
        public void LockstepLocalInputTargetAdvancesSequentiallyAfterStartupBundles()
        {
            var session = new LockstepSession();
            var activePlayers = new[]
            {
                new RoomPlayerSnapshot { SlotIndex = 0, SideOrder = 0, PlayerId = 1, IsActive = true },
                new RoomPlayerSnapshot { SlotIndex = 1, SideOrder = 1, PlayerId = 2, IsActive = true },
            };
            session.StartHost(activePlayers, 0);

            Assert.IsTrue(session.TryDequeueConfirmedFrame(out LockstepFrameBundle firstStartup));
            Assert.IsTrue(session.TryDequeueConfirmedFrame(out LockstepFrameBundle secondStartup));
            Assert.AreEqual(0, firstStartup.FrameIndex);
            Assert.AreEqual(1, secondStartup.FrameIndex);

            LockstepInputFrame firstLocal = session.SubmitLocalInput(0, 0, 0, 0);
            Assert.AreEqual(2, firstLocal.FrameIndex);
            session.SubmitInput(new LockstepInputFrame(1, 2, 2, 1, 0, 0, 0, 0));
            Assert.IsTrue(session.TryDequeueConfirmedFrame(out LockstepFrameBundle frameTwo));
            Assert.AreEqual(2, frameTwo.FrameIndex);

            LockstepInputFrame secondLocal = session.SubmitLocalInput(0, 0, 0, 0);
            Assert.AreEqual(3, secondLocal.FrameIndex);
        }

        [Test]
        public void HostAbortsWhenPeerChecksumDiffersFromLocalChecksum()
        {
            var host = new LanRoomService();
            ulong hostId = 1001UL;
            ulong clientId = 2002UL;
            host.CreateHost("Host", hostId, 4, "ABC123");
            JoinClientAndEnterPlaying(host, clientId);

            host.Lockstep.SubmitChecksumReport(new ChecksumReport
            {
                SlotIndex = 0,
                FrameIndex = 30,
                Checksum = 111U,
            });

            byte[] checksumPacket = GatebreakerEnvelopeCodec.Encode(
                GatebreakerNetworkMessageType.ChecksumReport,
                4,
                host.SessionId,
                host.ChannelId,
                GatebreakerPayloadCodec.EncodeChecksumReport(new ChecksumReport
                {
                    SlotIndex = 1,
                    FrameIndex = 30,
                    Checksum = 222U,
                }));

            Assert.IsTrue(host.HandleIncomingPacket(checksumPacket));
            Assert.AreEqual(LanRoomState.Aborted, host.CurrentSnapshot.State);
            Assert.AreEqual(MatchAbortReason.Desync, host.CurrentSnapshot.AbortReason);
        }

        [Test]
        public void FakeTransportQueuesOutboundMessagesForLanStateMachineTests()
        {
            var transport = new GatebreakerLanTestTransport();
            int connectedEvents = 0;
            transport.EventReceived += transportEvent =>
            {
                if (transportEvent.Type == LanTransportEventType.Connected)
                {
                    connectedEvents++;
                }
            };

            LanConnectionId connectionId = transport.Connect(new LanEndpoint("127.0.0.1", 7777));
            transport.Tick();
            Assert.IsTrue(connectionId.IsValid);
            Assert.IsTrue(transport.Send(connectionId, new byte[] { 1, 2, 3 }));

            Assert.AreEqual(1, connectedEvents);
            Assert.AreEqual(1, transport.Stats.PacketsSent);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, transport.DequeueOutboundMessage());
        }

        [Test]
        public void FakeTransportDrivesReadyStartAckMissingInputAndAbortWhenRoomStateMachineApiExists()
        {
            Assert.IsNotNull(
                FindType(RoomStateMachineCandidates),
                "LAN room/lockstep state machine public API should be present.");

            var host = new LanRoomService();
            ulong hostId = 1001UL;
            ulong clientId = 2002UL;
            RoomSnapshot hostSnapshot = host.CreateHost("Host", hostId, 4, "ABC123");
            Assert.AreEqual(LanRoomState.Lobby, hostSnapshot.State);
            Assert.IsFalse(hostSnapshot.CanStart);

            byte[] joinPacket = GatebreakerEnvelopeCodec.Encode(
                GatebreakerNetworkMessageType.RoomJoinRequest,
                1,
                host.SessionId,
                host.ChannelId,
                GatebreakerPayloadCodec.EncodeJoinRequest(new RoomJoinRequest
                {
                    ProtocolVersion = GatebreakerEnvelopeCodec.ProtocolVersion,
                    ClientInstanceId = clientId,
                    PlayerName = "Client",
                    RoomCode = "ABC123",
                }));
            Assert.IsTrue(host.HandleIncomingPacket(
                joinPacket,
                new LanEndpoint("127.0.0.1", 47780),
                new LanConnectionId(1)));

            byte[] readyPacket = GatebreakerEnvelopeCodec.Encode(
                GatebreakerNetworkMessageType.RoomReady,
                2,
                host.SessionId,
                host.ChannelId,
                GatebreakerPayloadCodec.EncodeRoomReady(new RoomReadyCommand
                {
                    ClientInstanceId = clientId,
                    IsReady = true,
                }));
            Assert.IsTrue(host.HandleIncomingPacket(readyPacket));
            Assert.IsTrue(host.CurrentSnapshot.CanStart);

            Assert.IsTrue(host.StartLoading());
            byte[] ackPacket = GatebreakerEnvelopeCodec.Encode(
                GatebreakerNetworkMessageType.RoomStartAck,
                3,
                host.SessionId,
                host.ChannelId,
                GatebreakerPayloadCodec.EncodeStartAck(new RoomStartAck
                {
                    ClientInstanceId = clientId,
                    SlotIndex = 1,
                }));
            Assert.IsTrue(host.HandleIncomingPacket(ackPacket));
            Assert.AreEqual(LanRoomState.Playing, host.CurrentSnapshot.State);

            Assert.IsTrue(host.Lockstep.TryDequeueConfirmedFrame(out _));
            Assert.IsTrue(host.Lockstep.TryDequeueConfirmedFrame(out _));
            host.Tick(5.1f);

            RoomSnapshot aborted = host.CurrentSnapshot;
            Assert.AreEqual(LanRoomState.Aborted, aborted.State);
            Assert.AreEqual(MatchAbortReason.MissingInputTimeout, aborted.AbortReason);
        }

        [Test]
        public void DiagnosticsJsonEscapesTextAndIncludesStableSchemaFields()
        {
            string json = LanDiagnosticJson.WriteEvent(
                new LanDiagnosticEvent
                {
                    EventName = "JoinRejected",
                    MonotonicMs = 123,
                    RoomCode = "ABC123",
                    Detail = "line\nquote\"slash\\",
                    ErrorCode = "RoomFull",
                },
                "diag-1",
                "1.0",
                "Device",
                "Editor");

            StringAssert.Contains("\"schemaVersion\":1", json);
            StringAssert.Contains("\"diagSessionId\":\"diag-1\"", json);
            StringAssert.Contains("\"eventName\":\"JoinRejected\"", json);
            StringAssert.Contains("\"detail\":\"line\\nquote\\\"slash\\\\\"", json);
            StringAssert.Contains("\"errorCode\":\"RoomFull\"", json);
        }

        [Test]
        public void DiagnosticsRingBufferKeepsMostRecentEvents()
        {
            var writer = new MemoryDiagnosticsWriter();
            var clock = new ManualDiagnosticsClock();
            var diagnostics = new LanDiagnosticsService(writer, clock);

            for (int i = 0; i < LanDiagnosticsService.EventCapacity + 25; i++)
            {
                clock.Advance(1);
                diagnostics.Record(new LanDiagnosticEvent
                {
                    EventName = "E" + i,
                    Detail = i.ToString(),
                });
            }

            LanDiagnosticsSnapshot snapshot = diagnostics.CreateSnapshot();
            Assert.AreEqual(LanDiagnosticsService.EventCapacity + 26, snapshot.EventCount);
            Assert.AreEqual(30, snapshot.RecentEvents.Length);
            Assert.AreEqual("E" + (LanDiagnosticsService.EventCapacity + 24), snapshot.RecentEvents.Last().EventName);
        }

        [Test]
        public void DiagnosticsFlushCallsWriter()
        {
            var writer = new MemoryDiagnosticsWriter();
            var diagnostics = new LanDiagnosticsService(writer, new ManualDiagnosticsClock());

            diagnostics.Record(new LanDiagnosticEvent { EventName = "FlushProbe" });
            diagnostics.Flush();

            Assert.Greater(writer.Lines.Count, 0);
            Assert.AreEqual(1, writer.FlushCount);
        }

        [Test]
        public void TransportBridgeRecordsTransportEventsForDiagnostics()
        {
            var writer = new MemoryDiagnosticsWriter();
            var diagnostics = new LanDiagnosticsService(writer, new ManualDiagnosticsClock());
            var room = new LanRoomService(null, diagnostics);
            var transport = new GatebreakerLanTestTransport();
            using (new LanRoomTransportBridge(room, transport, diagnostics))
            {
                transport.StartDiscovery();
                transport.StartTcpHost(47780);
                transport.Connect(new LanEndpoint("127.0.0.1", 47780));
                transport.Send(new LanConnectionId(999), new byte[] { 1 });
                transport.Tick();
            }

            string joined = string.Join("\n", writer.Lines.ToArray());
            StringAssert.Contains("TransportDiscoveryStarted", joined);
            StringAssert.Contains("TransportTcpHostStarted", joined);
            StringAssert.Contains("TransportConnected", joined);
            StringAssert.Contains("TransportError", joined);
        }

        [Test]
        public void ChecksumMismatchWritesDiagnosticEventBeforeAbort()
        {
            var writer = new MemoryDiagnosticsWriter();
            var diagnostics = new LanDiagnosticsService(writer, new ManualDiagnosticsClock());
            var host = new LanRoomService(null, diagnostics);
            ulong hostId = 1001UL;
            ulong clientId = 2002UL;
            host.CreateHost("Host", hostId, 4, "ABC123");
            JoinClientAndEnterPlaying(host, clientId);

            host.Lockstep.SubmitChecksumReport(new ChecksumReport
            {
                SlotIndex = 0,
                FrameIndex = 30,
                Checksum = 111U,
            });
            byte[] checksumPacket = GatebreakerEnvelopeCodec.Encode(
                GatebreakerNetworkMessageType.ChecksumReport,
                4,
                host.SessionId,
                host.ChannelId,
                GatebreakerPayloadCodec.EncodeChecksumReport(new ChecksumReport
                {
                    SlotIndex = 1,
                    FrameIndex = 30,
                    Checksum = 222U,
                }));

            Assert.IsTrue(host.HandleIncomingPacket(checksumPacket));

            string joined = string.Join("\n", writer.Lines.ToArray());
            StringAssert.Contains("ChecksumMismatch", joined);
            StringAssert.Contains("\"frameIndex\":30", joined);
            StringAssert.Contains("\"checksum\":222", joined);
        }

        private static Type FindType(string[] candidates)
        {
            return candidates
                .Select(Type.GetType)
                .FirstOrDefault(type => type != null);
        }

        private static void IgnoreUntilApiExists(Type type, string message)
        {
            if (type == null)
            {
                Assert.Ignore(message);
            }

            Assert.Inconclusive("TODO: Bind this test to " + type.FullName + " once the public API shape is stable.");
        }

        private static byte[] RoundTrip(GatebreakerNetworkMessageType messageType, byte[] payload)
        {
            byte[] encoded = GatebreakerEnvelopeCodec.Encode(messageType, 1, 123UL, 1U, payload);
            Assert.IsTrue(GatebreakerEnvelopeCodec.TryDecode(encoded, out GatebreakerEnvelope envelope));
            Assert.AreEqual(GatebreakerEnvelopeCodec.ProtocolVersion, envelope.ProtocolVersion);
            Assert.AreEqual(messageType, envelope.MessageType);
            return envelope.PayloadBytes;
        }

        private static byte[] EncodeEnvelopeForDecodeOnly(
            ushort messageType,
            int protocolVersion,
            uint sequence,
            ulong sessionId,
            uint channelId,
            byte[] payload)
        {
            payload = payload ?? new byte[0];
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((ushort)protocolVersion);
                writer.Write(messageType);
                writer.Write(sequence);
                writer.Write(sessionId);
                writer.Write(channelId);
                writer.Write(payload.Length);
                writer.Write(GatebreakerEnvelopeCodec.ComputePayloadHash(payload));
                writer.Write(payload);
                return stream.ToArray();
            }
        }

        private static void JoinClientAndEnterPlaying(LanRoomService host, ulong clientId)
        {
            byte[] joinPacket = GatebreakerEnvelopeCodec.Encode(
                GatebreakerNetworkMessageType.RoomJoinRequest,
                1,
                host.SessionId,
                host.ChannelId,
                GatebreakerPayloadCodec.EncodeJoinRequest(new RoomJoinRequest
                {
                    ProtocolVersion = GatebreakerEnvelopeCodec.ProtocolVersion,
                    ClientInstanceId = clientId,
                    PlayerName = "Client",
                    RoomCode = "ABC123",
                }));
            Assert.IsTrue(host.HandleIncomingPacket(
                joinPacket,
                new LanEndpoint("127.0.0.1", 47780),
                new LanConnectionId(1)));

            byte[] readyPacket = GatebreakerEnvelopeCodec.Encode(
                GatebreakerNetworkMessageType.RoomReady,
                2,
                host.SessionId,
                host.ChannelId,
                GatebreakerPayloadCodec.EncodeRoomReady(new RoomReadyCommand
                {
                    ClientInstanceId = clientId,
                    IsReady = true,
                }));
            Assert.IsTrue(host.HandleIncomingPacket(readyPacket));
            Assert.IsTrue(host.StartLoading());

            byte[] ackPacket = GatebreakerEnvelopeCodec.Encode(
                GatebreakerNetworkMessageType.RoomStartAck,
                3,
                host.SessionId,
                host.ChannelId,
                GatebreakerPayloadCodec.EncodeStartAck(new RoomStartAck
                {
                    ClientInstanceId = clientId,
                    SlotIndex = 1,
                }));
            Assert.IsTrue(host.HandleIncomingPacket(ackPacket));
            Assert.AreEqual(LanRoomState.Playing, host.CurrentSnapshot.State);
        }

        private sealed class ManualDiagnosticsClock : ILanDiagnosticsClock
        {
            public long MonotonicMilliseconds { get; private set; }

            public void Advance(long milliseconds)
            {
                MonotonicMilliseconds += milliseconds;
            }
        }

        private sealed class MemoryDiagnosticsWriter : ILanDiagnosticsWriter
        {
            public string CurrentLogPath { get; set; } = "memory://lan_diag.jsonl";
            public string LastWriteError { get; set; } = string.Empty;
            public int FlushCount { get; private set; }
            public System.Collections.Generic.List<string> Lines { get; } = new System.Collections.Generic.List<string>();

            public void WriteLine(string line)
            {
                Lines.Add(line);
            }

            public void Flush()
            {
                FlushCount++;
            }
        }
    }
}

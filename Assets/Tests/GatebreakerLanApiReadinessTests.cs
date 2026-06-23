using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using App.AOT.Networking.Lan;
using App.HotUpdate.GatebreakerArena.Ball;
using App.HotUpdate.GatebreakerArena.Match;
using App.HotUpdate.GatebreakerArena.Mode;
using App.HotUpdate.GatebreakerArena.Network;
using App.HotUpdate.GatebreakerArena.Serve;
using App.HotUpdate.GatebreakerArena.Zone;
using App.Shared.Contracts;
using NUnit.Framework;

namespace Gatebreaker.Tests
{
    public sealed class GatebreakerLanApiReadinessTests
    {
        private static readonly string[] RoomStateMachineCandidates =
        {
            "App.HotUpdate.GatebreakerArena.Network.LanRoomService, App.HotUpdate",
            "App.HotUpdate.GatebreakerArena.Lan.GatebreakerLanRoomClient, App.HotUpdate",
            "App.HotUpdate.GatebreakerArena.Lockstep.LockstepRoomStateMachine, App.HotUpdate",
            "App.HotUpdate.GatebreakerArena.Networking.GatebreakerRoomStateMachine, App.HotUpdate",
        };

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
            RoomReturnToLobbyCommand returnToLobby = GatebreakerPayloadCodec.DecodeReturnToLobby(RoundTrip(
                GatebreakerNetworkMessageType.RoomReturnToLobby,
                GatebreakerPayloadCodec.EncodeReturnToLobby(new RoomReturnToLobbyCommand
                {
                    ClientInstanceId = 123UL,
                    SlotIndex = 0,
                    IsReady = true,
                })));

            Assert.IsTrue(ready.IsReady);
            Assert.AreEqual(0, startAck.SlotIndex);
            Assert.AreEqual(10, bundle.FrameIndex);
            Assert.AreEqual(1, bundle.Inputs.Length);
            Assert.AreEqual(MatchAbortReason.MissingInputTimeout, abort.Reason);
            Assert.AreEqual(123UL, returnToLobby.ClientInstanceId);
            Assert.AreEqual(0, returnToLobby.SlotIndex);
            Assert.IsTrue(returnToLobby.IsReady);
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
        public void LanTransportCalculatesDirectedBroadcastForLocalSubnet()
        {
            LanEndpoint endpoint;
            Assert.IsTrue(LanTransport.TryCreateDirectedBroadcastEndpoint(
                IPAddress.Parse("192.168.0.115"),
                IPAddress.Parse("255.255.255.0"),
                LanTransportDefaults.DiscoveryPort,
                out endpoint));

            Assert.AreEqual("192.168.0.255", endpoint.Address);
            Assert.AreEqual(LanTransportDefaults.DiscoveryPort, endpoint.Port);
        }

        [Test]
        public void JoinReturnsToDiscoveryWhenTcpConnectionFails()
        {
            var client = new LanRoomService();
            var transport = new GatebreakerLanTestTransport { FailConnect = true };
            ulong clientId = 2002UL;

            using (new LanRoomTransportBridge(client, transport))
            {
                client.StartDiscovery(clientId, "Client");
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
                        TcpPort = 47780,
                        MaxPlayers = 4,
                        ActivePlayers = 1,
                        State = LanRoomState.Lobby,
                    }));

                Assert.IsTrue(client.HandleIncomingPacket(advertisePacket, new LanEndpoint("192.168.0.115", 47680)));
                Assert.IsTrue(client.JoinDiscoveredRoom("ABC123"));
            }

            RoomSnapshot snapshot = client.CurrentSnapshot;
            Assert.AreEqual(LanRoomState.Discovering, snapshot.State);
            StringAssert.Contains("TCP connection", snapshot.Error);
        }

        [Test]
        public void JoinRoomByCodeWaitsForAdvertiseThenSendsJoinRequest()
        {
            var client = new LanRoomService();
            var transport = new GatebreakerLanTestTransport();
            ulong clientId = 2002UL;

            using (new LanRoomTransportBridge(client, transport))
            {
                Assert.IsTrue(client.JoinRoomByCode("abc123", clientId, "Client"));
                Assert.AreEqual(LanRoomState.Discovering, client.CurrentSnapshot.State);

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
                        TcpPort = 47780,
                        MaxPlayers = 4,
                        ActivePlayers = 1,
                        State = LanRoomState.Lobby,
                    }));

                Assert.IsTrue(client.HandleIncomingPacket(advertisePacket, new LanEndpoint("192.168.0.115", 47680)));
            }

            Assert.AreEqual(LanRoomState.Joining, client.CurrentSnapshot.State);
            Assert.AreEqual(1, transport.OutboundMessages.Count);
            byte[] joinPacket = transport.OutboundMessages.Single();
            Assert.IsTrue(GatebreakerEnvelopeCodec.TryDecode(joinPacket, out GatebreakerEnvelope envelope));
            Assert.AreEqual(GatebreakerNetworkMessageType.RoomJoinRequest, envelope.MessageType);
            RoomJoinRequest request = GatebreakerPayloadCodec.DecodeJoinRequest(envelope.PayloadBytes);
            Assert.AreEqual("ABC123", request.RoomCode);
            Assert.AreEqual(clientId, request.ClientInstanceId);
        }

        [Test]
        public void DiscoveryIgnoresAdvertiseWithoutTcpPort()
        {
            var client = new LanRoomService();
            client.StartDiscovery(2002UL, "Client");
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
                    TcpPort = 0,
                    MaxPlayers = 4,
                    ActivePlayers = 1,
                    State = LanRoomState.Lobby,
                }));

            Assert.IsTrue(client.HandleIncomingPacket(advertisePacket, new LanEndpoint("192.168.0.115", 47680)));
            Assert.AreEqual(0, client.DiscoveredRooms.Count);
            Assert.IsFalse(client.JoinDiscoveredRoom("ABC123"));
        }

        [Test]
        public void DuplicateAdvertiseDoesNotFloodDiagnostics()
        {
            var writer = new MemoryDiagnosticsWriter();
            var diagnostics = new LanDiagnosticsService(writer, new ManualDiagnosticsClock());
            var client = new LanRoomService(null, diagnostics);
            client.StartDiscovery(2002UL, "Client");
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
                    TcpPort = 47780,
                    MaxPlayers = 4,
                    ActivePlayers = 1,
                    State = LanRoomState.Lobby,
                }));

            var endpoint = new LanEndpoint("192.168.0.115", 47680);
            Assert.IsTrue(client.HandleIncomingPacket(advertisePacket, endpoint));
            Assert.IsTrue(client.HandleIncomingPacket(advertisePacket, endpoint));

            int acceptedCount = writer.Lines.Count(line => line.Contains("\"eventName\":\"AdvertiseAccepted\""));
            Assert.AreEqual(1, acceptedCount);
            Assert.AreEqual(1, client.DiscoveredRooms.Count);
        }

        [Test]
        public void RoomSnapshotCodecRoundTripsAiPlayerFlag()
        {
            var snapshot = new RoomSnapshot
            {
                SessionId = 12345UL,
                ChannelId = 7U,
                RoomCode = "ABC123",
                State = LanRoomState.Loading,
                IsHost = true,
                CanStart = false,
                PlayersFrozen = true,
                LocalSlotIndex = 0,
                MaxPlayers = 4,
                Players = new[]
                {
                    new RoomPlayerSnapshot
                    {
                        SlotIndex = 0,
                        SideOrder = 0,
                        PlayerId = 1,
                        ClientInstanceId = 1001UL,
                        PlayerName = "Host",
                        IsHost = true,
                        IsReady = true,
                        IsLoadingAcked = true,
                        IsActive = true,
                    },
                    new RoomPlayerSnapshot
                    {
                        SlotIndex = 2,
                        SideOrder = 2,
                        PlayerId = 3,
                        PlayerName = "Computer 3",
                        IsReady = true,
                        IsLoadingAcked = true,
                        IsActive = true,
                        IsAi = true,
                    },
                },
            };

            RoomSnapshot decoded = GatebreakerPayloadCodec.DecodeRoomSnapshot(
                GatebreakerPayloadCodec.EncodeRoomSnapshot(snapshot));

            Assert.AreEqual(2, GatebreakerEnvelopeCodec.ProtocolVersion);
            Assert.AreEqual(2, decoded.Players.Length);
            RoomPlayerSnapshot ai = decoded.Players.Single(player => player.PlayerId == 3);
            Assert.IsTrue(ai.IsAi);
            Assert.AreEqual("Computer 3", ai.PlayerName);
        }

        [Test]
        public void EnvelopeRejectsPreviousProtocolVersionAfterAiSnapshotSchemaChange()
        {
            byte[] encoded = EncodeEnvelopeForDecodeOnly(
                (ushort)GatebreakerNetworkMessageType.RoomSnapshot,
                GatebreakerEnvelopeCodec.ProtocolVersion - 1,
                1,
                123UL,
                7U,
                GatebreakerPayloadCodec.EncodeRoomSnapshot(new RoomSnapshot
                {
                    SessionId = 123UL,
                    ChannelId = 7U,
                    Players = new[]
                    {
                        new RoomPlayerSnapshot
                        {
                            SlotIndex = 2,
                            SideOrder = 2,
                            PlayerId = 3,
                            IsActive = true,
                            IsAi = true,
                        },
                    },
                }));

            Assert.IsFalse(GatebreakerEnvelopeCodec.TryDecode(encoded, out _));
        }

        [Test]
        public void CreateHostGeneratesNumericRoomCodeByDefault()
        {
            var host = new LanRoomService();

            RoomSnapshot snapshot = host.CreateHost("Host", 1001UL);

            Assert.AreEqual(6, snapshot.RoomCode.Length);
            Assert.IsTrue(snapshot.RoomCode.All(char.IsDigit));
            Assert.AreEqual(2, snapshot.MaxPlayers);
            Assert.AreEqual(2, snapshot.Players.Length);
            Assert.AreEqual(1, snapshot.Players.Count(player => player.IsAi));
        }

        [Test]
        public void HostAiBackfillMatchesSelectedPlayerCount()
        {
            var host = new LanRoomService();

            RoomSnapshot twoPlayers = host.CreateHost("Host", 1001UL, 2, "ROOM02");
            Assert.AreEqual(2, twoPlayers.MaxPlayers);
            Assert.AreEqual(2, twoPlayers.Players.Length);
            Assert.AreEqual(1, twoPlayers.Players.Count(player => player.IsAi));

            RoomSnapshot threePlayers = host.CreateHost("Host", 1001UL, 3, "ROOM03");
            Assert.AreEqual(3, threePlayers.MaxPlayers);
            Assert.AreEqual(3, threePlayers.Players.Length);
            Assert.AreEqual(2, threePlayers.Players.Count(player => player.IsAi));

            RoomSnapshot fourPlayers = host.CreateHost("Host", 1001UL, 4, "ROOM04");
            Assert.AreEqual(4, fourPlayers.MaxPlayers);
            Assert.AreEqual(4, fourPlayers.Players.Length);
            Assert.AreEqual(3, fourPlayers.Players.Count(player => player.IsAi));
        }

        [Test]
        public void HostCreateLeaveResetThenCreateHostStartsFreshLobby()
        {
            var host = new LanRoomService();
            ulong hostId = 1001UL;

            host.CreateHost("Host", hostId, 3, "ABC123");
            host.Leave("ui");
            Assert.AreEqual(LanRoomState.Left, host.CurrentSnapshot.State);

            host.ResetAfterLocalLeave("ui");
            RoomSnapshot reset = host.CurrentSnapshot;
            Assert.AreEqual(LanRoomState.Idle, reset.State);
            Assert.IsFalse(reset.IsHost);
            Assert.AreEqual(-1, reset.LocalSlotIndex);
            Assert.AreEqual(string.Empty, reset.RoomCode);
            Assert.IsFalse(reset.PlayersFrozen);
            Assert.AreEqual(0, reset.Players.Length);

            RoomSnapshot recreated = host.CreateHost("Host", hostId, 3, "DEF456", 47780);
            Assert.AreEqual(LanRoomState.Lobby, recreated.State);
            Assert.IsTrue(recreated.IsHost);
            Assert.AreEqual(0, recreated.LocalSlotIndex);
            Assert.AreEqual("DEF456", recreated.RoomCode);
            Assert.AreEqual(hostId, recreated.Players.Single(player => player.IsLocal).ClientInstanceId);
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
        public void HostAuthorityInputCompletesAiSlotInLockstepBundle()
        {
            var session = new LockstepSession();
            var activePlayers = new[]
            {
                new RoomPlayerSnapshot { SlotIndex = 0, SideOrder = 0, PlayerId = 1, IsActive = true },
                new RoomPlayerSnapshot { SlotIndex = 1, SideOrder = 1, PlayerId = 2, IsActive = true },
                new RoomPlayerSnapshot { SlotIndex = 2, SideOrder = 2, PlayerId = 3, IsActive = true, IsAi = true },
            };
            session.StartHost(activePlayers, 0);
            Assert.IsTrue(session.TryDequeueConfirmedFrame(out _));
            Assert.IsTrue(session.TryDequeueConfirmedFrame(out _));

            LockstepInputFrame local = session.SubmitLocalInput(0, 0, 0, 0);
            Assert.AreEqual(2, local.FrameIndex);
            session.SubmitInput(new LockstepInputFrame(1, 2, 2, 1, 0, 0, 0, 0));
            session.SubmitHostInputForSlot(2, session.HostNextBundleFrame, 0, 0, 0, 0);

            Assert.IsTrue(session.TryDequeueConfirmedFrame(out LockstepFrameBundle frameTwo));
            Assert.AreEqual(2, frameTwo.FrameIndex);
            Assert.AreEqual(3, frameTwo.Inputs.Length);
            Assert.IsTrue(frameTwo.Inputs.Any(input => input.SlotIndex == 2 && input.PlayerId == 3));
        }

        [Test]
        public void HostAuthorityInputCompletesConsecutiveAiLockstepBundles()
        {
            var session = new LockstepSession();
            var activePlayers = new[]
            {
                new RoomPlayerSnapshot { SlotIndex = 0, SideOrder = 0, PlayerId = 1, IsActive = true },
                new RoomPlayerSnapshot { SlotIndex = 1, SideOrder = 1, PlayerId = 2, IsActive = true },
                new RoomPlayerSnapshot { SlotIndex = 2, SideOrder = 2, PlayerId = 3, IsActive = true, IsAi = true },
            };
            session.StartHost(activePlayers, 0);
            Assert.IsTrue(session.TryDequeueConfirmedFrame(out _));
            Assert.IsTrue(session.TryDequeueConfirmedFrame(out _));

            for (int frame = 2; frame < 12; frame++)
            {
                LockstepInputFrame local = session.SubmitLocalInput(0, 0, 0, 0);
                Assert.AreEqual(frame, local.FrameIndex);
                session.SubmitInput(new LockstepInputFrame(1, 2, frame, (uint)frame, 0, 0, 0, 0));
                session.SubmitHostInputForSlot(2, session.HostNextBundleFrame, 0, 0, 0, 0);
                session.Tick(0f);

                Assert.IsTrue(session.TryDequeueConfirmedFrame(out LockstepFrameBundle bundle), "frame=" + frame);
                Assert.AreEqual(frame, bundle.FrameIndex);
                Assert.AreEqual(3, bundle.Inputs.Length);
                Assert.IsTrue(bundle.Inputs.Any(input => input.SlotIndex == 2 && input.PlayerId == 3), "frame=" + frame);
                Assert.IsFalse(session.CreateSnapshot().WaitingSlotIndexes.Contains(2), "frame=" + frame);
            }
        }

        [Test]
        public void HostAbortsWhenPeerChecksumDiffersFromLocalChecksum()
        {
            var host = new LanRoomService();
            ulong hostId = 1001UL;
            ulong clientId = 2002UL;
            host.CreateHost("Host", hostId, 3, "ABC123");
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
        public void HostRoomStartsWithComputerBackfillAndJoinReplacesComputerSlot()
        {
            var host = new LanRoomService();
            ulong hostId = 1001UL;
            ulong clientId = 2002UL;
            RoomSnapshot hostSnapshot = host.CreateHost("Host", hostId, 4, "ABC123");
            Assert.AreEqual(LanRoomState.Lobby, hostSnapshot.State);
            Assert.IsTrue(hostSnapshot.CanStart);
            Assert.AreEqual(4, hostSnapshot.Players.Length);
            Assert.AreEqual(3, hostSnapshot.Players.Count(player => player.IsAi));
            Assert.AreEqual("Computer 2", hostSnapshot.Players.Single(player => player.PlayerId == 2).PlayerName);
            Assert.AreEqual("Computer 3", hostSnapshot.Players.Single(player => player.PlayerId == 3).PlayerName);
            Assert.AreEqual("Computer 4", hostSnapshot.Players.Single(player => player.PlayerId == 4).PlayerName);

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
            RoomSnapshot joined = host.CurrentSnapshot;
            Assert.AreEqual(4, joined.Players.Length);
            Assert.IsFalse(joined.CanStart);
            RoomPlayerSnapshot client = joined.Players.Single(player => player.ClientInstanceId == clientId);
            Assert.AreEqual(1, client.SlotIndex);
            Assert.AreEqual(2, client.PlayerId);
            Assert.IsFalse(client.IsAi);
            Assert.AreEqual(2, joined.Players.Count(player => player.IsAi));

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
            RoomSnapshot loading = host.CurrentSnapshot;
            Assert.AreEqual(LanRoomState.Loading, loading.State);
            Assert.AreEqual(4, loading.Players.Length);
            RoomPlayerSnapshot ai = loading.Players.Single(player => player.PlayerId == 3);
            Assert.IsTrue(ai.IsAi);
            Assert.IsTrue(ai.IsReady);
            Assert.IsTrue(ai.IsLoadingAcked);
            Assert.AreEqual(2, ai.SlotIndex);
            Assert.AreEqual("Computer 3", ai.PlayerName);

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
            Assert.IsTrue(host.CurrentSnapshot.Players.Single(player => player.PlayerId == 3).IsAi);
            Assert.IsTrue(host.Lockstep.TryDequeueConfirmedFrame(out LockstepFrameBundle firstStartup));
            Assert.AreEqual(4, firstStartup.Inputs.Length);
            Assert.IsTrue(firstStartup.Inputs.Any(input => input.SlotIndex == 2 && input.PlayerId == 3));
        }

        [Test]
        public void RoomAdvertiseCountsHumanPlayersInsteadOfAiBackfillSlots()
        {
            var host = new LanRoomService();
            ulong hostId = 1001UL;
            ulong clientId = 2002UL;
            RoomAdvertise latestAdvertise = null;
            host.UdpBroadcastRequested += payload =>
            {
                Assert.IsTrue(GatebreakerEnvelopeCodec.TryDecode(payload, out GatebreakerEnvelope envelope));
                Assert.AreEqual(GatebreakerNetworkMessageType.RoomAdvertise, envelope.MessageType);
                latestAdvertise = GatebreakerPayloadCodec.DecodeRoomAdvertise(envelope.PayloadBytes);
            };

            host.CreateHost("Host", hostId, 4, "ABC123", 47780);
            host.Tick(0f);
            Assert.IsNotNull(latestAdvertise);
            Assert.AreEqual(1, latestAdvertise.ActivePlayers);
            Assert.AreEqual(4, latestAdvertise.MaxPlayers);

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

            latestAdvertise = null;
            host.Tick(1f);
            Assert.IsNotNull(latestAdvertise);
            Assert.AreEqual(2, latestAdvertise.ActivePlayers);
        }

        [Test]
        public void HostReadyButtonDoesNotToggleHostToNotReady()
        {
            var host = new LanRoomService();
            RoomSnapshot snapshot = host.CreateHost("Host", 1001UL, 3, "ABC123");
            Assert.IsTrue(snapshot.CanStart);
            Assert.IsTrue(snapshot.Players.Single(player => player.IsHost).IsReady);

            Assert.IsTrue(host.SetReady(false));

            RoomSnapshot afterReadyClick = host.CurrentSnapshot;
            Assert.IsTrue(afterReadyClick.CanStart);
            Assert.IsTrue(afterReadyClick.Players.Single(player => player.IsHost).IsReady);
        }

        [Test]
        public void HostLogsRemotePlayerEnterAndLeaveWithEndpoint()
        {
            var logger = new CapturingAppLogger();
            var host = new LanRoomService(logger);
            ulong hostId = 1001UL;
            ulong clientId = 2002UL;
            host.CreateHost("Host", hostId, 3, "ABC123");

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
                new LanEndpoint("192.168.0.42", 47780),
                new LanConnectionId(1)));

            Assert.IsTrue(logger.InfoMessages.Any(message =>
                message.Contains("Client") &&
                message.Contains("192.168.0.42:47780") &&
                message.Contains("entered room ABC123")));

            byte[] leavePacket = GatebreakerEnvelopeCodec.Encode(
                GatebreakerNetworkMessageType.RoomLeave,
                2,
                host.SessionId,
                host.ChannelId,
                GatebreakerPayloadCodec.EncodeLeaveNotice(new RoomLeaveNotice
                {
                    ClientInstanceId = clientId,
                    SlotIndex = 1,
                    Reason = "ui",
                }));
            Assert.IsTrue(host.HandleIncomingPacket(leavePacket));

            Assert.IsTrue(logger.InfoMessages.Any(message =>
                message.Contains("Client") &&
                message.Contains("192.168.0.42:47780") &&
                message.Contains("left room ABC123") &&
                message.Contains("reason=ui")));
        }

        [Test]
        public void HostResultRestartReturnsLobbyWithOnlyHostReady()
        {
            var host = new LanRoomService();
            ulong hostId = 1001UL;
            ulong clientId = 2002UL;
            host.CreateHost("Host", hostId, 3, "ABC123");
            JoinClientAndEnterPlaying(host, clientId);

            Assert.IsTrue(host.ReturnToLobbyFromResult(true));

            RoomSnapshot snapshot = host.CurrentSnapshot;
            Assert.AreEqual(LanRoomState.Lobby, snapshot.State);
            Assert.IsFalse(snapshot.PlayersFrozen);
            Assert.IsTrue(snapshot.Players.Single(player => player.ClientInstanceId == hostId).IsReady);
            Assert.IsFalse(snapshot.Players.Single(player => player.ClientInstanceId == clientId).IsReady);
            Assert.IsTrue(snapshot.Players.Where(player => player.IsAi).All(player => player.IsReady));
            Assert.IsFalse(snapshot.CanStart);
        }

        [Test]
        public void ClientResultRestartRequestsLobbyAndReturnsClientNotReady()
        {
            var host = new LanRoomService();
            var client = new LanRoomService();
            var hostToClient = new Queue<byte[]>();
            var clientToHost = new Queue<byte[]>();
            host.ReliableSendRequested += (payload, _) => hostToClient.Enqueue(payload);
            client.ReliableSendRequested += (payload, _) => clientToHost.Enqueue(payload);

            ulong hostId = 1001UL;
            ulong clientId = 2002UL;
            ConnectClientAndEnterPlaying(host, client, hostId, clientId, hostToClient, clientToHost);

            Assert.IsTrue(client.ReturnToLobbyFromResult(false));
            Assert.AreEqual(LanRoomState.Lobby, client.CurrentSnapshot.State);
            Assert.IsFalse(client.CurrentSnapshot.Players.Single(player => player.ClientInstanceId == clientId).IsReady);

            PumpReliable(clientToHost, host, new LanConnectionId(1), null);
            RoomSnapshot hostSnapshot = host.CurrentSnapshot;
            Assert.AreEqual(LanRoomState.Lobby, hostSnapshot.State);
            Assert.IsFalse(hostSnapshot.PlayersFrozen);
            Assert.IsFalse(hostSnapshot.Players.Single(player => player.ClientInstanceId == hostId).IsReady);
            Assert.IsFalse(hostSnapshot.Players.Single(player => player.ClientInstanceId == clientId).IsReady);

            PumpReliable(hostToClient, client, null, new LanConnectionId(1));
            RoomSnapshot clientSnapshot = client.CurrentSnapshot;
            Assert.AreEqual(LanRoomState.Lobby, clientSnapshot.State);
            Assert.IsFalse(clientSnapshot.Players.Single(player => player.ClientInstanceId == clientId).IsReady);
        }

        [Test]
        public void ClientResultBackLeavesRoomWithoutDisbandingHostRoom()
        {
            var host = new LanRoomService();
            ulong hostId = 1001UL;
            ulong clientId = 2002UL;
            host.CreateHost("Host", hostId, 3, "ABC123");
            JoinClientAndEnterPlaying(host, clientId);

            byte[] leavePacket = GatebreakerEnvelopeCodec.Encode(
                GatebreakerNetworkMessageType.RoomLeave,
                4,
                host.SessionId,
                host.ChannelId,
                GatebreakerPayloadCodec.EncodeLeaveNotice(new RoomLeaveNotice
                {
                    ClientInstanceId = clientId,
                    SlotIndex = 1,
                    Reason = "resultBack",
                }));

            Assert.IsTrue(host.HandleIncomingPacket(leavePacket, null, new LanConnectionId(1)));

            RoomSnapshot snapshot = host.CurrentSnapshot;
            Assert.AreEqual(LanRoomState.Lobby, snapshot.State);
            Assert.IsFalse(snapshot.PlayersFrozen);
            Assert.AreEqual(MatchAbortReason.None, snapshot.AbortReason);
            Assert.IsFalse(snapshot.Players.Any(player => player.ClientInstanceId == clientId));
            Assert.IsTrue(snapshot.Players.Any(player => player.ClientInstanceId == hostId));
        }

        [Test]
        public void JoinReplacesComputerNamedSlotEvenWhenAiFlagWasMissing()
        {
            var host = new LanRoomService();
            ulong hostId = 1001UL;
            ulong clientId = 2002UL;
            host.CreateHost("Host", hostId, 3, "ABC123");
            ForceComputerSlotsToLoseAiFlag(host);

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

            RoomSnapshot joined = host.CurrentSnapshot;
            RoomPlayerSnapshot client = joined.Players.Single(player => player.ClientInstanceId == clientId);
            Assert.AreEqual(1, client.SlotIndex);
            Assert.AreEqual(2, client.PlayerId);
            Assert.IsFalse(client.IsAi);
            Assert.AreEqual("Client", client.PlayerName);
        }

        [Test]
        public void RoomFullJoinResponseIncludesHostCountDetail()
        {
            var host = new LanRoomService();
            byte[] responsePacket = null;
            host.ReliableSendRequested += (payload, _) => responsePacket = payload;
            ulong hostId = 1001UL;
            ulong clientId = 2002UL;
            host.CreateHost("Host", hostId, 3, "ABC123");
            ForceComputerSlotsToBecomeNonReplaceableHumans(host);

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
            Assert.IsNotNull(responsePacket);
            Assert.IsTrue(GatebreakerEnvelopeCodec.TryDecode(responsePacket, out GatebreakerEnvelope envelope));
            Assert.AreEqual(GatebreakerNetworkMessageType.RoomJoinResponse, envelope.MessageType);
            RoomJoinResponse response = GatebreakerPayloadCodec.DecodeJoinResponse(envelope.PayloadBytes);
            Assert.IsFalse(response.Accepted);
            Assert.AreEqual(LanRoomJoinResult.RoomFull, response.Result);
            StringAssert.Contains("active=3;human=3;ai=0;total=3", response.Error);
        }

        [Test]
        public void HostAiBackfillDrivesClientRuntimeToMatchingThirtyFrameChecksum()
        {
            var host = new LanRoomService();
            var client = new LanRoomService();
            var hostRuntime = CreateGatebreakerRuntime();
            var clientRuntime = CreateGatebreakerRuntime();
            var hostController = new GatebreakerNetworkMatchController(host, hostRuntime);
            var clientController = new GatebreakerNetworkMatchController(client, clientRuntime);
            var hostToClient = new Queue<byte[]>();
            var clientToHost = new Queue<byte[]>();
            host.ReliableSendRequested += (payload, _) => hostToClient.Enqueue(payload);
            client.ReliableSendRequested += (payload, _) => clientToHost.Enqueue(payload);

            ulong hostId = 1001UL;
            ulong clientId = 2002UL;
            host.CreateHost("Host", hostId, 4, "ABC123");
            client.StartDiscovery(clientId, "Client");
            client.HandleIncomingPacket(CreatePacket(
                GatebreakerNetworkMessageType.RoomAdvertise,
                1,
                host.SessionId,
                host.ChannelId,
                GatebreakerPayloadCodec.EncodeRoomAdvertise(new RoomAdvertise
                {
                    ProtocolVersion = GatebreakerEnvelopeCodec.ProtocolVersion,
                    SessionId = host.SessionId,
                    ChannelId = host.ChannelId,
                    RoomCode = "ABC123",
                    HostClientInstanceId = hostId,
                    HostPlayerName = "Host",
                    TcpPort = 47780,
                    MaxPlayers = 4,
                    ActivePlayers = 1,
                    State = LanRoomState.Lobby,
                })),
                new LanEndpoint("127.0.0.1", 47680));
            Assert.IsTrue(client.JoinDiscoveredRoom("ABC123"));
            PumpReliable(clientToHost, host, new LanConnectionId(1), null);
            PumpReliable(hostToClient, client, null, new LanConnectionId(1));
            Assert.AreEqual(LanRoomState.Lobby, host.CurrentSnapshot.State);
            Assert.AreEqual(LanRoomState.Lobby, client.CurrentSnapshot.State);

            Assert.IsTrue(client.SetReady(true));
            PumpReliable(clientToHost, host, new LanConnectionId(1), null);
            PumpReliable(hostToClient, client, null, new LanConnectionId(1));
            Assert.IsTrue(host.CurrentSnapshot.CanStart);
            Assert.IsTrue(host.StartLoading());
            PumpReliable(hostToClient, client, null, new LanConnectionId(1));
            Assert.AreEqual(LanRoomState.Loading, client.CurrentSnapshot.State);
            PumpReliable(clientToHost, host, new LanConnectionId(1), null);
            PumpReliable(hostToClient, client, null, new LanConnectionId(1));
            Assert.AreEqual(LanRoomState.Playing, host.CurrentSnapshot.State);
            Assert.AreEqual(LanRoomState.Playing, client.CurrentSnapshot.State);
            Assert.IsTrue(host.CurrentSnapshot.Players.Any(player => player.PlayerId == 3 && player.IsAi));
            Assert.IsTrue(client.CurrentSnapshot.Players.Any(player => player.PlayerId == 3 && player.IsAi));

            var deliveredBundles = new List<LockstepFrameBundle>();
            for (int startupFrame = 0; startupFrame < LockstepSession.InputDelay - 1; startupFrame++)
            {
                hostController.Tick(1f / LockstepSession.SimulationFps);
                clientController.Tick(1f / LockstepSession.SimulationFps);
                PumpReliable(clientToHost, host, new LanConnectionId(1), null);
                Assert.AreEqual(startupFrame, hostRuntime.LastFrameIndex, "host startup frame=" + startupFrame);
                Assert.AreEqual(startupFrame, clientRuntime.LastFrameIndex, "client startup frame=" + startupFrame);
            }

            for (int frame = LockstepSession.InputDelay - 1; frame <= 30; frame++)
            {
                SubmitLocalInput(host.Lockstep, frame, 0, 1);
                SubmitRemoteInputToHost(host, frame, 1, 2);
                hostController.Tick(1f / LockstepSession.SimulationFps);
                PumpReliable(hostToClient, client, null, new LanConnectionId(1), deliveredBundles);
                clientController.Tick(1f / LockstepSession.SimulationFps);
                PumpReliable(clientToHost, host, new LanConnectionId(1), null);

                Assert.AreEqual(frame, hostRuntime.LastFrameIndex, "host frame=" + frame);
                Assert.AreEqual(frame, clientRuntime.LastFrameIndex, "client frame=" + frame);
                LockstepSnapshot hostLockstep = host.Lockstep.CreateSnapshot();
                LockstepSnapshot clientLockstep = client.Lockstep.CreateSnapshot();
                Assert.IsFalse(hostLockstep.WaitingSlotIndexes.Contains(2), "host waiting ai frame=" + frame);
                Assert.AreEqual(LockstepSyncState.Running, hostLockstep.State, "host lockstep frame=" + frame);
                Assert.AreEqual(LockstepSyncState.Running, clientLockstep.State, "client lockstep frame=" + frame);
                Assert.IsTrue(deliveredBundles.Any(bundle =>
                    bundle.FrameIndex == frame &&
                    bundle.Inputs != null &&
                    bundle.Inputs.Any(input => input.SlotIndex == 2 && input.PlayerId == 3)), "ai bundle frame=" + frame);
            }

            uint hostChecksum = hostRuntime.CreateChecksum(30).Value;
            uint clientChecksum = clientRuntime.CreateChecksum(30).Value;
            Assert.AreEqual(hostChecksum, clientChecksum);
            Assert.AreEqual(LanRoomState.Playing, host.CurrentSnapshot.State);
            Assert.AreEqual(LanRoomState.Playing, client.CurrentSnapshot.State);
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
            Assert.IsTrue(hostSnapshot.CanStart);

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
        public void RoomSnapshotDiagnosticsIncludeCountsAndRoster()
        {
            var writer = new MemoryDiagnosticsWriter();
            var diagnostics = new LanDiagnosticsService(writer, new ManualDiagnosticsClock());
            var host = new LanRoomService(null, diagnostics);

            host.CreateHost("Host", 1001UL, 4, "ABC123");

            string joined = string.Join("\n", writer.Lines.ToArray());
            StringAssert.Contains("RoomSnapshotState", joined);
            StringAssert.Contains("active=4;human=1;ai=3;total=4", joined);
            StringAssert.Contains("slot0/p1/Host/Human/Host/Local/Ready", joined);
            StringAssert.Contains("slot1/p2/Computer 2/AI/Ready", joined);
        }

        [Test]
        public void DiagnosticsSummaryIncludesRoomCountsAndRoster()
        {
            var diagnostics = new LanDiagnosticsService(new MemoryDiagnosticsWriter(), new ManualDiagnosticsClock());
            var host = new LanRoomService(null, diagnostics);
            RoomSnapshot snapshot = host.CreateHost("Host", 1001UL, 4, "ABC123");

            string summary = diagnostics.CreateSummaryText(snapshot);
            StringAssert.Contains("canStart=true", summary);
            StringAssert.Contains("players=active=4;human=1;ai=3;total=4;max=4", summary);
            StringAssert.Contains("roster=slot0/p1/Host/Human/Host/Local/Ready", summary);
        }

        [Test]
        public void DiagnosticsRecentEventsFilterResolvedLanNoise()
        {
            var diagnostics = new LanDiagnosticsService(new MemoryDiagnosticsWriter(), new ManualDiagnosticsClock());
            for (int i = 0; i < 40; i++)
            {
                diagnostics.Record(new LanDiagnosticEvent
                {
                    EventName = "RoomSnapshotState",
                    Detail = "state=Lobby",
                });
                diagnostics.Record(new LanDiagnosticEvent
                {
                    EventName = "SnapshotReceive",
                    Detail = "state=Lobby",
                });
                diagnostics.Record(new LanDiagnosticEvent
                {
                    EventName = "PacketSend",
                    Detail = "connection:1",
                });
                diagnostics.Record(new LanDiagnosticEvent
                {
                    EventName = "PacketReceived",
                    MessageType = GatebreakerNetworkMessageType.RoomSnapshot.ToString(),
                    Endpoint = "192.168.0.115:" + i,
                });
                diagnostics.Record(new LanDiagnosticEvent
                {
                    EventName = "TransportConnected",
                    Endpoint = "192.168.0.115:" + i,
                });
                diagnostics.Record(new LanDiagnosticEvent
                {
                    EventName = "TransportDataReceived",
                    Endpoint = "192.168.0.115:" + i,
                });
                diagnostics.Record(new LanDiagnosticEvent
                {
                    EventName = "TransportDiscoveryReceived",
                    Endpoint = "192.168.0.115:" + i,
                });
                diagnostics.Record(new LanDiagnosticEvent
                {
                    EventName = "PacketReceived",
                    MessageType = GatebreakerNetworkMessageType.RoomAdvertise.ToString(),
                    Endpoint = "192.168.0.115:" + i,
                });
                diagnostics.Record(new LanDiagnosticEvent
                {
                    EventName = "AdvertiseSend",
                    Detail = "tcpPort=47780",
                });
                diagnostics.Record(new LanDiagnosticEvent
                {
                    EventName = "DiscoverySend",
                    Detail = string.Empty,
                });
                diagnostics.Record(new LanDiagnosticEvent
                {
                    EventName = "AdvertiseIgnored",
                    Detail = "ABC123",
                });
            }

            diagnostics.Record(new LanDiagnosticEvent
            {
                EventName = "JoinResponseReceive",
                Detail = "Room is full.",
            });

            LanDiagnosticsSnapshot snapshot = diagnostics.CreateSnapshot();
            Assert.AreEqual(2, snapshot.RecentEvents.Length);
            Assert.AreEqual("DiagnosticsStarted", snapshot.RecentEvents[0].EventName);
            Assert.AreEqual("JoinResponseReceive", snapshot.RecentEvents[1].EventName);
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

        private static byte[] RoundTrip(GatebreakerNetworkMessageType messageType, byte[] payload)
        {
            byte[] encoded = GatebreakerEnvelopeCodec.Encode(messageType, 1, 123UL, 1U, payload);
            Assert.IsTrue(GatebreakerEnvelopeCodec.TryDecode(encoded, out GatebreakerEnvelope envelope));
            Assert.AreEqual(GatebreakerEnvelopeCodec.ProtocolVersion, envelope.ProtocolVersion);
            Assert.AreEqual(messageType, envelope.MessageType);
            return envelope.PayloadBytes;
        }

        private static GatebreakerMatchRuntime CreateGatebreakerRuntime()
        {
            return new GatebreakerMatchRuntime(
                GatebreakerModeCatalog.CreateDefault(),
                new BallSimulationSystem(),
                new ServeResourceSystem(),
                new GoalJudgeSystem(),
                new ScoreSystem(),
                null);
        }

        private static byte[] CreatePacket(
            GatebreakerNetworkMessageType messageType,
            uint sequence,
            ulong sessionId,
            uint channelId,
            byte[] payload)
        {
            return GatebreakerEnvelopeCodec.Encode(messageType, sequence, sessionId, channelId, payload);
        }

        private static void PumpReliable(
            Queue<byte[]> packets,
            LanRoomService target,
            object endpoint,
            object connectionId,
            List<LockstepFrameBundle> deliveredBundles = null)
        {
            while (packets.Count > 0)
            {
                byte[] packet = packets.Dequeue();
                if (deliveredBundles != null &&
                    GatebreakerEnvelopeCodec.TryDecode(packet, out GatebreakerEnvelope envelope) &&
                    envelope.MessageType == GatebreakerNetworkMessageType.LockstepFrameBundle)
                {
                    deliveredBundles.Add(GatebreakerPayloadCodec.DecodeFrameBundle(envelope.PayloadBytes));
                }

                Assert.IsTrue(target.HandleIncomingPacket(packet, endpoint, connectionId));
            }
        }

        private static void SubmitLocalInput(
            LockstepSession session,
            int expectedFrame,
            short moveAxisQ,
            ushort buttons)
        {
            LockstepInputFrame input = session.SubmitLocalInput(moveAxisQ, 0, 0, buttons);
            Assert.AreEqual(expectedFrame, input.FrameIndex);
        }

        private static void SubmitRemoteInputToHost(
            LanRoomService host,
            int frameIndex,
            int slotIndex,
            int playerId)
        {
            byte[] packet = CreatePacket(
                GatebreakerNetworkMessageType.LockstepInput,
                (uint)(1000 + frameIndex),
                host.SessionId,
                host.ChannelId,
                GatebreakerPayloadCodec.EncodeLockstepInput(new LockstepInputFrame(
                    slotIndex,
                    playerId,
                    frameIndex,
                    (uint)frameIndex,
                    0,
                    0,
                    0,
                    0)));
            Assert.IsTrue(host.HandleIncomingPacket(packet, null, new LanConnectionId(1)));
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

        private static void ConnectClientAndEnterPlaying(
            LanRoomService host,
            LanRoomService client,
            ulong hostId,
            ulong clientId,
            Queue<byte[]> hostToClient,
            Queue<byte[]> clientToHost)
        {
            host.CreateHost("Host", hostId, 4, "ABC123");
            client.StartDiscovery(clientId, "Client");
            client.HandleIncomingPacket(CreatePacket(
                GatebreakerNetworkMessageType.RoomAdvertise,
                1,
                host.SessionId,
                host.ChannelId,
                GatebreakerPayloadCodec.EncodeRoomAdvertise(new RoomAdvertise
                {
                    ProtocolVersion = GatebreakerEnvelopeCodec.ProtocolVersion,
                    SessionId = host.SessionId,
                    ChannelId = host.ChannelId,
                    RoomCode = "ABC123",
                    HostClientInstanceId = hostId,
                    HostPlayerName = "Host",
                    TcpPort = 47780,
                    MaxPlayers = 4,
                    ActivePlayers = 1,
                    State = LanRoomState.Lobby,
                })),
                new LanEndpoint("127.0.0.1", 47680));
            Assert.IsTrue(client.JoinDiscoveredRoom("ABC123"));
            PumpReliable(clientToHost, host, new LanConnectionId(1), null);
            PumpReliable(hostToClient, client, null, new LanConnectionId(1));
            Assert.AreEqual(LanRoomState.Lobby, host.CurrentSnapshot.State);
            Assert.AreEqual(LanRoomState.Lobby, client.CurrentSnapshot.State);

            Assert.IsTrue(client.SetReady(true));
            PumpReliable(clientToHost, host, new LanConnectionId(1), null);
            PumpReliable(hostToClient, client, null, new LanConnectionId(1));
            Assert.IsTrue(host.CurrentSnapshot.CanStart);
            Assert.IsTrue(host.StartLoading());
            PumpReliable(hostToClient, client, null, new LanConnectionId(1));
            Assert.AreEqual(LanRoomState.Loading, client.CurrentSnapshot.State);
            PumpReliable(clientToHost, host, new LanConnectionId(1), null);
            PumpReliable(hostToClient, client, null, new LanConnectionId(1));
            Assert.AreEqual(LanRoomState.Playing, host.CurrentSnapshot.State);
            Assert.AreEqual(LanRoomState.Playing, client.CurrentSnapshot.State);
        }

        private static void ForceComputerSlotsToLoseAiFlag(LanRoomService room)
        {
            FieldInfo slotsField = typeof(LanRoomService).GetField("_slots", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(slotsField);
            var slots = slotsField.GetValue(room) as System.Collections.IEnumerable;
            Assert.IsNotNull(slots);
            foreach (object slot in slots)
            {
                PropertyInfo playerNameProperty = slot.GetType().GetProperty("PlayerName", BindingFlags.Instance | BindingFlags.Public);
                PropertyInfo isAiProperty = slot.GetType().GetProperty("IsAi", BindingFlags.Instance | BindingFlags.Public);
                Assert.IsNotNull(playerNameProperty);
                Assert.IsNotNull(isAiProperty);
                string playerName = playerNameProperty.GetValue(slot) as string;
                if (!string.IsNullOrWhiteSpace(playerName) &&
                    playerName.Trim().StartsWith("Computer ", StringComparison.OrdinalIgnoreCase))
                {
                    isAiProperty.SetValue(slot, false);
                }
            }
        }

        private static void ForceComputerSlotsToBecomeNonReplaceableHumans(LanRoomService room)
        {
            FieldInfo slotsField = typeof(LanRoomService).GetField("_slots", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(slotsField);
            var slots = slotsField.GetValue(room) as System.Collections.IEnumerable;
            Assert.IsNotNull(slots);
            foreach (object slot in slots)
            {
                PropertyInfo playerNameProperty = slot.GetType().GetProperty("PlayerName", BindingFlags.Instance | BindingFlags.Public);
                PropertyInfo clientIdProperty = slot.GetType().GetProperty("ClientInstanceId", BindingFlags.Instance | BindingFlags.Public);
                PropertyInfo isAiProperty = slot.GetType().GetProperty("IsAi", BindingFlags.Instance | BindingFlags.Public);
                PropertyInfo playerIdProperty = slot.GetType().GetProperty("PlayerId", BindingFlags.Instance | BindingFlags.Public);
                Assert.IsNotNull(playerNameProperty);
                Assert.IsNotNull(clientIdProperty);
                Assert.IsNotNull(isAiProperty);
                Assert.IsNotNull(playerIdProperty);
                string playerName = playerNameProperty.GetValue(slot) as string;
                if (!string.IsNullOrWhiteSpace(playerName) &&
                    playerName.Trim().StartsWith("Computer ", StringComparison.OrdinalIgnoreCase))
                {
                    int playerId = (int)playerIdProperty.GetValue(slot);
                    playerNameProperty.SetValue(slot, "Occupied " + playerId);
                    clientIdProperty.SetValue(slot, 3000UL + (ulong)playerId);
                    isAiProperty.SetValue(slot, false);
                }
            }
        }

        private sealed class CapturingAppLogger : IAppLogger
        {
            public List<string> InfoMessages { get; } = new List<string>();

            public void Log(LogLevel level, string message, params object[] args)
            {
                if (level == LogLevel.Info)
                {
                    InfoMessages.Add(Format(message, args));
                }
            }

            public void LogDebug(string message, params object[] args)
            {
            }

            public void LogInfo(string message, params object[] args)
            {
                InfoMessages.Add(Format(message, args));
            }

            public void LogWarning(string message, params object[] args)
            {
            }

            public void LogError(string message, params object[] args)
            {
            }

            private static string Format(string message, object[] args)
            {
                return args != null && args.Length > 0
                    ? string.Format(message, args)
                    : message;
            }
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

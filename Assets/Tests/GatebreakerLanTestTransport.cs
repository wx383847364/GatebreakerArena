using System;
using System.Collections.Generic;
using App.Shared.Contracts;

namespace Gatebreaker.Tests
{
    public sealed class GatebreakerLanTestTransport : ILanTransport
    {
        private readonly Queue<LanTransportEvent> _events = new Queue<LanTransportEvent>();
        private readonly Queue<byte[]> _outboundMessages = new Queue<byte[]>();
        private int _nextConnectionId = 1;
        private LanTransportStats _stats;

        public LanTransportStats Stats => _stats;
        public LanEndpoint TcpListenEndpoint { get; private set; }
        public int DiscoveryPort { get; private set; } = LanTransportDefaults.DiscoveryPort;
        public IReadOnlyCollection<byte[]> OutboundMessages => _outboundMessages;
        public LanConnectionId LastConnectionId { get; private set; } = LanConnectionId.Invalid;

        public event Action<LanTransportEvent> EventReceived;

        public void Initialize()
        {
        }

        public void Update(float deltaTime)
        {
        }

        public void Shutdown()
        {
            StopDiscovery();
            StopTcpHost();
            if (LastConnectionId.IsValid)
            {
                Disconnect(LastConnectionId);
            }
        }

        public bool StartDiscovery()
        {
            _stats.IsDiscoveryActive = true;
            _stats.DiscoveryPort = DiscoveryPort;
            Enqueue(new LanTransportEvent
            {
                Type = LanTransportEventType.DiscoveryStarted,
                LocalEndpoint = new LanEndpoint("0.0.0.0", DiscoveryPort),
            });
            return true;
        }

        public void StopDiscovery()
        {
            if (!_stats.IsDiscoveryActive)
            {
                return;
            }

            _stats.IsDiscoveryActive = false;
            Enqueue(new LanTransportEvent
            {
                Type = LanTransportEventType.DiscoveryStopped,
                LocalEndpoint = new LanEndpoint("0.0.0.0", DiscoveryPort),
            });
        }

        public bool SendDiscovery(byte[] payload)
        {
            return SendDiscovery(payload, new LanEndpoint("255.255.255.255", DiscoveryPort));
        }

        public bool SendDiscovery(byte[] payload, LanEndpoint endpoint)
        {
            if (!endpoint.IsValid)
            {
                EnqueueError(LanTransportError.InvalidEndpoint, endpoint);
                return false;
            }

            TrackSent(payload);
            _outboundMessages.Enqueue(payload ?? new byte[0]);
            return true;
        }

        public bool StartTcpHost(int preferredPort = 0)
        {
            int port = preferredPort > 0 ? preferredPort : 47780;
            TcpListenEndpoint = new LanEndpoint("127.0.0.1", port);
            _stats.IsTcpHostActive = true;
            _stats.TcpListenPort = port;
            Enqueue(new LanTransportEvent
            {
                Type = LanTransportEventType.TcpHostStarted,
                LocalEndpoint = TcpListenEndpoint,
            });
            return true;
        }

        public void StopTcpHost()
        {
            if (!_stats.IsTcpHostActive)
            {
                return;
            }

            _stats.IsTcpHostActive = false;
            Enqueue(new LanTransportEvent
            {
                Type = LanTransportEventType.TcpHostStopped,
                LocalEndpoint = TcpListenEndpoint,
            });
        }

        public LanConnectionId Connect(LanEndpoint endpoint)
        {
            if (!endpoint.IsValid)
            {
                EnqueueError(LanTransportError.InvalidEndpoint, endpoint);
                return LanConnectionId.Invalid;
            }

            LastConnectionId = new LanConnectionId(_nextConnectionId++);
            _stats.ActiveConnections += 1;
            _stats.ConnectionsOpened += 1;
            Enqueue(new LanTransportEvent
            {
                Type = LanTransportEventType.Connected,
                ConnectionId = LastConnectionId,
                RemoteEndpoint = endpoint,
            });
            return LastConnectionId;
        }

        public bool Send(LanConnectionId connectionId, byte[] payload)
        {
            if (!connectionId.IsValid || !connectionId.Equals(LastConnectionId))
            {
                EnqueueError(LanTransportError.TcpSendFailed, default(LanEndpoint));
                return false;
            }

            if (payload != null && payload.Length > LanTransportDefaults.MaxFrameSizeBytes)
            {
                EnqueueError(LanTransportError.FrameTooLarge, default(LanEndpoint));
                return false;
            }

            TrackSent(payload);
            _outboundMessages.Enqueue(payload ?? new byte[0]);
            return true;
        }

        public void Disconnect(LanConnectionId connectionId)
        {
            if (!connectionId.IsValid || !connectionId.Equals(LastConnectionId))
            {
                return;
            }

            _stats.ActiveConnections = Math.Max(0, _stats.ActiveConnections - 1);
            _stats.ConnectionsClosed += 1;
            Enqueue(new LanTransportEvent
            {
                Type = LanTransportEventType.Disconnected,
                ConnectionId = connectionId,
            });
            LastConnectionId = LanConnectionId.Invalid;
        }

        public void Tick()
        {
            while (_events.Count > 0)
            {
                EventReceived?.Invoke(_events.Dequeue());
            }
        }

        public byte[] DequeueOutboundMessage()
        {
            return _outboundMessages.Dequeue();
        }

        public void Receive(LanConnectionId connectionId, byte[] payload)
        {
            _stats.BytesReceived += payload != null ? payload.Length : 0;
            _stats.PacketsReceived += 1;
            Enqueue(new LanTransportEvent
            {
                Type = LanTransportEventType.DataReceived,
                ConnectionId = connectionId,
                Payload = payload ?? new byte[0],
            });
        }

        private void TrackSent(byte[] payload)
        {
            _stats.BytesSent += payload != null ? payload.Length : 0;
            _stats.PacketsSent += 1;
        }

        private void EnqueueError(LanTransportError error, LanEndpoint endpoint)
        {
            _stats.Errors += 1;
            Enqueue(new LanTransportEvent
            {
                Type = LanTransportEventType.Error,
                Error = error,
                RemoteEndpoint = endpoint,
            });
        }

        private void Enqueue(LanTransportEvent transportEvent)
        {
            _events.Enqueue(transportEvent);
        }
    }
}

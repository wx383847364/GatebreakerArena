using System;

namespace App.Shared.Contracts
{
    /// <summary>
    /// Stable LAN transport defaults shared across AOT and HotUpdate layers.
    /// </summary>
    public static class LanTransportDefaults
    {
        public const int DiscoveryPort = 47680;
        public const int MaxFrameSizeBytes = 1024 * 1024;
    }

    public enum LanTransportEventType
    {
        DiscoveryStarted,
        DiscoveryStopped,
        DiscoveryReceived,
        TcpHostStarted,
        TcpHostStopped,
        Connected,
        Disconnected,
        DataReceived,
        Error,
    }

    public enum LanTransportError
    {
        None,
        DiscoveryPortInUse,
        DiscoveryStartFailed,
        DiscoveryReceiveFailed,
        DiscoverySendFailed,
        TcpPortInUse,
        TcpHostStartFailed,
        TcpConnectFailed,
        TcpSendFailed,
        TcpReceiveFailed,
        FrameTooLarge,
        InvalidEndpoint,
        ShutdownFailed,
        Unknown,
    }

    public struct LanEndpoint : IEquatable<LanEndpoint>
    {
        public string Address { get; set; }
        public int Port { get; set; }

        public LanEndpoint(string address, int port)
        {
            Address = address;
            Port = port;
        }

        public bool IsValid
        {
            get { return !string.IsNullOrEmpty(Address) && Port > 0 && Port <= 65535; }
        }

        public bool Equals(LanEndpoint other)
        {
            return string.Equals(Address, other.Address, StringComparison.OrdinalIgnoreCase) && Port == other.Port;
        }

        public override bool Equals(object obj)
        {
            return obj is LanEndpoint && Equals((LanEndpoint)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Address != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Address) : 0) * 397) ^ Port;
            }
        }

        public override string ToString()
        {
            return string.Concat(Address ?? string.Empty, ":", Port);
        }
    }

    public struct LanConnectionId : IEquatable<LanConnectionId>
    {
        public static readonly LanConnectionId Invalid = new LanConnectionId(0);

        public int Value { get; private set; }

        public LanConnectionId(int value)
        {
            Value = value;
        }

        public bool IsValid
        {
            get { return Value > 0; }
        }

        public bool Equals(LanConnectionId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is LanConnectionId && Equals((LanConnectionId)obj);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public sealed class LanTransportEvent
    {
        public LanTransportEventType Type { get; set; }
        public LanTransportError Error { get; set; }
        public string Message { get; set; }
        public LanEndpoint LocalEndpoint { get; set; }
        public LanEndpoint RemoteEndpoint { get; set; }
        public LanConnectionId ConnectionId { get; set; }
        public byte[] Payload { get; set; }
        public string SessionId { get; set; }
        public string Channel { get; set; }
    }

    public struct LanTransportStats
    {
        public long BytesSent { get; set; }
        public long BytesReceived { get; set; }
        public long PacketsSent { get; set; }
        public long PacketsReceived { get; set; }
        public long ConnectionsAccepted { get; set; }
        public long ConnectionsOpened { get; set; }
        public long ConnectionsClosed { get; set; }
        public long Errors { get; set; }
        public int ActiveConnections { get; set; }
        public bool IsDiscoveryActive { get; set; }
        public int DiscoveryPort { get; set; }
        public bool IsTcpHostActive { get; set; }
        public int TcpListenPort { get; set; }
    }

    /// <summary>
    /// Generic LAN byte transport. It does not define or inspect application payload semantics.
    /// </summary>
    public interface ILanTransport : IService
    {
        event Action<LanTransportEvent> EventReceived;

        LanTransportStats Stats { get; }
        LanEndpoint TcpListenEndpoint { get; }
        int DiscoveryPort { get; }

        bool StartDiscovery();
        void StopDiscovery();
        bool SendDiscovery(byte[] payload);
        bool SendDiscovery(byte[] payload, LanEndpoint endpoint);

        bool StartTcpHost(int preferredPort = 0);
        void StopTcpHost();
        LanConnectionId Connect(LanEndpoint endpoint);
        bool Send(LanConnectionId connectionId, byte[] payload);
        void Disconnect(LanConnectionId connectionId);

        void Tick();
    }
}

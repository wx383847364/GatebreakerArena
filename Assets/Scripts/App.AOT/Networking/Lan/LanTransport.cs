using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using App.Shared.Contracts;

namespace App.AOT.Networking.Lan
{
    /// <summary>
    /// AOT-owned LAN socket transport. It frames and dispatches raw bytes only.
    /// </summary>
    public sealed class LanTransport : ILanTransport
    {
        private const int TcpBacklog = 16;
        private const int ThreadJoinTimeoutMs = 250;

        private readonly object _syncRoot = new object();
        private readonly ConcurrentQueue<LanTransportEvent> _mainThreadEvents = new ConcurrentQueue<LanTransportEvent>();
        private readonly Dictionary<int, TcpConnection> _connections = new Dictionary<int, TcpConnection>();

        private IAppLogger _logger;
        private volatile bool _running;
        private volatile bool _discoveryActive;
        private volatile bool _tcpHostActive;
        private int _nextConnectionId;
        private int _discoveryPort = LanTransportDefaults.DiscoveryPort;
        private LanEndpoint _tcpListenEndpoint;
        private UdpClient _discoveryReceiver;
        private Thread _discoveryThread;
        private TcpListener _tcpListener;
        private Thread _acceptThread;

        private long _bytesSent;
        private long _bytesReceived;
        private long _packetsSent;
        private long _packetsReceived;
        private long _connectionsAccepted;
        private long _connectionsOpened;
        private long _connectionsClosed;
        private long _errors;

        public event Action<LanTransportEvent> EventReceived;

        public LanTransportStats Stats
        {
            get
            {
                return new LanTransportStats
                {
                    BytesSent = Interlocked.Read(ref _bytesSent),
                    BytesReceived = Interlocked.Read(ref _bytesReceived),
                    PacketsSent = Interlocked.Read(ref _packetsSent),
                    PacketsReceived = Interlocked.Read(ref _packetsReceived),
                    ConnectionsAccepted = Interlocked.Read(ref _connectionsAccepted),
                    ConnectionsOpened = Interlocked.Read(ref _connectionsOpened),
                    ConnectionsClosed = Interlocked.Read(ref _connectionsClosed),
                    Errors = Interlocked.Read(ref _errors),
                    ActiveConnections = GetActiveConnectionCount(),
                    IsDiscoveryActive = _discoveryActive,
                    DiscoveryPort = _discoveryPort,
                    IsTcpHostActive = _tcpHostActive,
                    TcpListenPort = _tcpListenEndpoint.Port,
                };
            }
        }

        public LanEndpoint TcpListenEndpoint
        {
            get { return _tcpListenEndpoint; }
        }

        public int DiscoveryPort
        {
            get { return _discoveryPort; }
        }

        public void SetLogger(IAppLogger logger)
        {
            _logger = logger;
        }

        public void Initialize()
        {
            _running = true;
        }

        public void Update(float deltaTime)
        {
            Tick();
        }

        public bool StartDiscovery()
        {
            if (_discoveryActive)
            {
                return true;
            }

            UdpClient receiver = null;
            try
            {
                receiver = new UdpClient(AddressFamily.InterNetwork)
                {
                    EnableBroadcast = true,
                };
                receiver.Client.Bind(new IPEndPoint(IPAddress.Any, LanTransportDefaults.DiscoveryPort));

                _discoveryReceiver = receiver;
                _discoveryPort = LanTransportDefaults.DiscoveryPort;
                _discoveryActive = true;
                _running = true;
                _discoveryThread = new Thread(DiscoveryReceiveLoop);
                _discoveryThread.IsBackground = true;
                _discoveryThread.Name = "LanTransport Discovery";
                _discoveryThread.Start();

                EnqueueEvent(new LanTransportEvent
                {
                    Type = LanTransportEventType.DiscoveryStarted,
                    LocalEndpoint = new LanEndpoint("0.0.0.0", _discoveryPort),
                });
                return true;
            }
            catch (SocketException ex)
            {
                CloseUdpClient(receiver);
                var error = ex.SocketErrorCode == SocketError.AddressAlreadyInUse
                    ? LanTransportError.DiscoveryPortInUse
                    : LanTransportError.DiscoveryStartFailed;
                EnqueueError(error, "Discovery bind failed: " + ex.Message, LanConnectionId.Invalid, default(LanEndpoint));
                return false;
            }
            catch (Exception ex)
            {
                CloseUdpClient(receiver);
                EnqueueError(LanTransportError.DiscoveryStartFailed, "Discovery start failed: " + ex.Message, LanConnectionId.Invalid, default(LanEndpoint));
                return false;
            }
        }

        public void StopDiscovery()
        {
            if (!_discoveryActive && _discoveryReceiver == null)
            {
                return;
            }

            _discoveryActive = false;
            CloseUdpClient(_discoveryReceiver);
            _discoveryReceiver = null;
            JoinThread(_discoveryThread);
            _discoveryThread = null;

            EnqueueEvent(new LanTransportEvent
            {
                Type = LanTransportEventType.DiscoveryStopped,
                LocalEndpoint = new LanEndpoint("0.0.0.0", _discoveryPort),
            });
        }

        public bool SendDiscovery(byte[] payload)
        {
            return SendDiscovery(payload, new LanEndpoint("255.255.255.255", LanTransportDefaults.DiscoveryPort));
        }

        public bool SendDiscovery(byte[] payload, LanEndpoint endpoint)
        {
            if (payload == null)
            {
                payload = new byte[0];
            }

            if (!endpoint.IsValid)
            {
                EnqueueError(LanTransportError.InvalidEndpoint, "Invalid discovery endpoint.", LanConnectionId.Invalid, endpoint);
                return false;
            }

            try
            {
                using (var sender = new UdpClient(AddressFamily.InterNetwork))
                {
                    sender.EnableBroadcast = true;
                    sender.Send(payload, payload.Length, endpoint.Address, endpoint.Port);
                }

                Interlocked.Add(ref _bytesSent, payload.Length);
                Interlocked.Increment(ref _packetsSent);
                return true;
            }
            catch (Exception ex)
            {
                EnqueueError(LanTransportError.DiscoverySendFailed, "Discovery send failed: " + ex.Message, LanConnectionId.Invalid, endpoint);
                return false;
            }
        }

        public bool StartTcpHost(int preferredPort = 0)
        {
            if (_tcpHostActive)
            {
                return true;
            }

            try
            {
                var listener = new TcpListener(IPAddress.Any, preferredPort);
                listener.Start(TcpBacklog);

                var local = listener.LocalEndpoint as IPEndPoint;
                _tcpListener = listener;
                _tcpListenEndpoint = local != null
                    ? new LanEndpoint(local.Address.ToString(), local.Port)
                    : new LanEndpoint("0.0.0.0", preferredPort);
                _tcpHostActive = true;
                _running = true;
                _acceptThread = new Thread(AcceptLoop);
                _acceptThread.IsBackground = true;
                _acceptThread.Name = "LanTransport TCP Accept";
                _acceptThread.Start();

                EnqueueEvent(new LanTransportEvent
                {
                    Type = LanTransportEventType.TcpHostStarted,
                    LocalEndpoint = _tcpListenEndpoint,
                });
                return true;
            }
            catch (SocketException ex)
            {
                var error = ex.SocketErrorCode == SocketError.AddressAlreadyInUse
                    ? LanTransportError.TcpPortInUse
                    : LanTransportError.TcpHostStartFailed;
                EnqueueError(error, "TCP host bind failed: " + ex.Message, LanConnectionId.Invalid, new LanEndpoint("0.0.0.0", preferredPort));
                return false;
            }
            catch (Exception ex)
            {
                EnqueueError(LanTransportError.TcpHostStartFailed, "TCP host start failed: " + ex.Message, LanConnectionId.Invalid, new LanEndpoint("0.0.0.0", preferredPort));
                return false;
            }
        }

        public void StopTcpHost()
        {
            if (!_tcpHostActive && _tcpListener == null)
            {
                return;
            }

            _tcpHostActive = false;
            try
            {
                if (_tcpListener != null)
                {
                    _tcpListener.Stop();
                }
            }
            catch (Exception ex)
            {
                EnqueueError(LanTransportError.Unknown, "TCP host stop failed: " + ex.Message, LanConnectionId.Invalid, _tcpListenEndpoint);
            }

            _tcpListener = null;
            JoinThread(_acceptThread);
            _acceptThread = null;

            EnqueueEvent(new LanTransportEvent
            {
                Type = LanTransportEventType.TcpHostStopped,
                LocalEndpoint = _tcpListenEndpoint,
            });
        }

        public LanConnectionId Connect(LanEndpoint endpoint)
        {
            if (!endpoint.IsValid)
            {
                EnqueueError(LanTransportError.InvalidEndpoint, "Invalid TCP endpoint.", LanConnectionId.Invalid, endpoint);
                return LanConnectionId.Invalid;
            }

            TcpClient client = null;
            try
            {
                client = new TcpClient(AddressFamily.InterNetwork);
                client.NoDelay = true;
                client.Connect(endpoint.Address, endpoint.Port);
                var connection = AddConnection(client);
                Interlocked.Increment(ref _connectionsOpened);
                StartReceiveThread(connection);

                EnqueueEvent(new LanTransportEvent
                {
                    Type = LanTransportEventType.Connected,
                    ConnectionId = connection.Id,
                    LocalEndpoint = connection.LocalEndpoint,
                    RemoteEndpoint = connection.RemoteEndpoint,
                });

                return connection.Id;
            }
            catch (Exception ex)
            {
                CloseTcpClient(client);
                EnqueueError(LanTransportError.TcpConnectFailed, "TCP connect failed: " + ex.Message, LanConnectionId.Invalid, endpoint);
                return LanConnectionId.Invalid;
            }
        }

        public bool Send(LanConnectionId connectionId, byte[] payload)
        {
            if (!connectionId.IsValid)
            {
                EnqueueError(LanTransportError.TcpSendFailed, "Invalid connection id.", connectionId, default(LanEndpoint));
                return false;
            }

            if (payload == null)
            {
                payload = new byte[0];
            }

            if (payload.Length > LanTransportDefaults.MaxFrameSizeBytes)
            {
                EnqueueError(LanTransportError.FrameTooLarge, "Frame exceeds max size.", connectionId, default(LanEndpoint));
                return false;
            }

            TcpConnection connection;
            lock (_syncRoot)
            {
                if (!_connections.TryGetValue(connectionId.Value, out connection) || !connection.IsActive)
                {
                    EnqueueError(LanTransportError.TcpSendFailed, "Connection is not active.", connectionId, default(LanEndpoint));
                    return false;
                }
            }

            try
            {
                var lengthPrefix = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(payload.Length));
                lock (connection.WriteLock)
                {
                    connection.Stream.Write(lengthPrefix, 0, lengthPrefix.Length);
                    if (payload.Length > 0)
                    {
                        connection.Stream.Write(payload, 0, payload.Length);
                    }

                    connection.Stream.Flush();
                }

                Interlocked.Add(ref _bytesSent, payload.Length);
                Interlocked.Increment(ref _packetsSent);
                return true;
            }
            catch (Exception ex)
            {
                EnqueueError(LanTransportError.TcpSendFailed, "TCP send failed: " + ex.Message, connectionId, connection.RemoteEndpoint);
                CloseConnection(connection, true, "send failed");
                return false;
            }
        }

        public void Disconnect(LanConnectionId connectionId)
        {
            if (!connectionId.IsValid)
            {
                return;
            }

            TcpConnection connection = null;
            lock (_syncRoot)
            {
                _connections.TryGetValue(connectionId.Value, out connection);
            }

            if (connection != null)
            {
                CloseConnection(connection, true, "local disconnect");
            }
        }

        public void Tick()
        {
            LanTransportEvent transportEvent;
            while (_mainThreadEvents.TryDequeue(out transportEvent))
            {
                var handler = EventReceived;
                if (handler != null)
                {
                    handler(transportEvent);
                }
            }
        }

        public void Shutdown()
        {
            _running = false;
            StopDiscovery();
            StopTcpHost();

            List<TcpConnection> snapshot;
            lock (_syncRoot)
            {
                snapshot = new List<TcpConnection>(_connections.Values);
            }

            for (var i = 0; i < snapshot.Count; i++)
            {
                CloseConnection(snapshot[i], true, "transport shutdown");
            }

            Tick();
        }

        private void DiscoveryReceiveLoop()
        {
            while (_running && _discoveryActive)
            {
                try
                {
                    var remote = new IPEndPoint(IPAddress.Any, 0);
                    var payload = _discoveryReceiver.Receive(ref remote);

                    Interlocked.Add(ref _bytesReceived, payload != null ? payload.Length : 0);
                    Interlocked.Increment(ref _packetsReceived);
                    EnqueueEvent(new LanTransportEvent
                    {
                        Type = LanTransportEventType.DiscoveryReceived,
                        Payload = payload ?? new byte[0],
                        LocalEndpoint = new LanEndpoint("0.0.0.0", _discoveryPort),
                        RemoteEndpoint = ToLanEndpoint(remote),
                    });
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                    if (_running && _discoveryActive)
                    {
                        EnqueueError(LanTransportError.DiscoveryReceiveFailed, "Discovery receive failed.", LanConnectionId.Invalid, default(LanEndpoint));
                    }
                }
                catch (Exception ex)
                {
                    if (_running && _discoveryActive)
                    {
                        EnqueueError(LanTransportError.DiscoveryReceiveFailed, "Discovery receive failed: " + ex.Message, LanConnectionId.Invalid, default(LanEndpoint));
                    }
                }
            }
        }

        private void AcceptLoop()
        {
            while (_running && _tcpHostActive)
            {
                try
                {
                    var client = _tcpListener.AcceptTcpClient();
                    client.NoDelay = true;
                    var connection = AddConnection(client);
                    Interlocked.Increment(ref _connectionsAccepted);
                    StartReceiveThread(connection);

                    EnqueueEvent(new LanTransportEvent
                    {
                        Type = LanTransportEventType.Connected,
                        ConnectionId = connection.Id,
                        LocalEndpoint = connection.LocalEndpoint,
                        RemoteEndpoint = connection.RemoteEndpoint,
                    });
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                    if (_running && _tcpHostActive)
                    {
                        EnqueueError(LanTransportError.TcpHostStartFailed, "TCP accept failed.", LanConnectionId.Invalid, _tcpListenEndpoint);
                    }
                }
                catch (Exception ex)
                {
                    if (_running && _tcpHostActive)
                    {
                        EnqueueError(LanTransportError.TcpHostStartFailed, "TCP accept failed: " + ex.Message, LanConnectionId.Invalid, _tcpListenEndpoint);
                    }
                }
            }
        }

        private TcpConnection AddConnection(TcpClient client)
        {
            var connectionId = new LanConnectionId(Interlocked.Increment(ref _nextConnectionId));
            var connection = new TcpConnection
            {
                Id = connectionId,
                Client = client,
                Stream = client.GetStream(),
                IsActive = true,
                LocalEndpoint = ToLanEndpoint(client.Client.LocalEndPoint),
                RemoteEndpoint = ToLanEndpoint(client.Client.RemoteEndPoint),
            };

            lock (_syncRoot)
            {
                _connections[connectionId.Value] = connection;
            }

            return connection;
        }

        private void StartReceiveThread(TcpConnection connection)
        {
            connection.ReceiveThread = new Thread(() => ReceiveLoop(connection));
            connection.ReceiveThread.IsBackground = true;
            connection.ReceiveThread.Name = "LanTransport TCP Receive " + connection.Id.Value;
            connection.ReceiveThread.Start();
        }

        private void ReceiveLoop(TcpConnection connection)
        {
            try
            {
                var lengthBuffer = new byte[4];
                while (_running && connection.IsActive)
                {
                    if (!ReadExactly(connection.Stream, lengthBuffer, 0, lengthBuffer.Length))
                    {
                        break;
                    }

                    var frameLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lengthBuffer, 0));
                    if (frameLength < 0 || frameLength > LanTransportDefaults.MaxFrameSizeBytes)
                    {
                        EnqueueError(LanTransportError.FrameTooLarge, "Received invalid frame size.", connection.Id, connection.RemoteEndpoint);
                        break;
                    }

                    var payload = new byte[frameLength];
                    if (frameLength > 0 && !ReadExactly(connection.Stream, payload, 0, frameLength))
                    {
                        break;
                    }

                    Interlocked.Add(ref _bytesReceived, frameLength);
                    Interlocked.Increment(ref _packetsReceived);
                    EnqueueEvent(new LanTransportEvent
                    {
                        Type = LanTransportEventType.DataReceived,
                        ConnectionId = connection.Id,
                        LocalEndpoint = connection.LocalEndpoint,
                        RemoteEndpoint = connection.RemoteEndpoint,
                        Payload = payload,
                    });
                }
            }
            catch (IOException ex)
            {
                if (_running && connection.IsActive)
                {
                    EnqueueError(LanTransportError.TcpReceiveFailed, "TCP receive failed: " + ex.Message, connection.Id, connection.RemoteEndpoint);
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                if (_running && connection.IsActive)
                {
                    EnqueueError(LanTransportError.TcpReceiveFailed, "TCP receive failed: " + ex.Message, connection.Id, connection.RemoteEndpoint);
                }
            }
            finally
            {
                CloseConnection(connection, true, "remote disconnect");
            }
        }

        private bool ReadExactly(Stream stream, byte[] buffer, int offset, int length)
        {
            var readTotal = 0;
            while (readTotal < length)
            {
                var read = stream.Read(buffer, offset + readTotal, length - readTotal);
                if (read <= 0)
                {
                    return false;
                }

                readTotal += read;
            }

            return true;
        }

        private void CloseConnection(TcpConnection connection, bool enqueueDisconnected, string message)
        {
            var removed = false;
            lock (_syncRoot)
            {
                if (connection.IsActive)
                {
                    connection.IsActive = false;
                }

                removed = _connections.Remove(connection.Id.Value);
            }

            if (!removed)
            {
                return;
            }

            CloseTcpClient(connection.Client);
            Interlocked.Increment(ref _connectionsClosed);

            if (enqueueDisconnected)
            {
                EnqueueEvent(new LanTransportEvent
                {
                    Type = LanTransportEventType.Disconnected,
                    Message = message,
                    ConnectionId = connection.Id,
                    LocalEndpoint = connection.LocalEndpoint,
                    RemoteEndpoint = connection.RemoteEndpoint,
                });
            }
        }

        private int GetActiveConnectionCount()
        {
            lock (_syncRoot)
            {
                return _connections.Count;
            }
        }

        private void EnqueueError(LanTransportError error, string message, LanConnectionId connectionId, LanEndpoint remoteEndpoint)
        {
            Interlocked.Increment(ref _errors);
            if (_logger != null)
            {
                _logger.LogWarning("LanTransport: {0}", message);
            }

            EnqueueEvent(new LanTransportEvent
            {
                Type = LanTransportEventType.Error,
                Error = error,
                Message = message,
                ConnectionId = connectionId,
                RemoteEndpoint = remoteEndpoint,
            });
        }

        private void EnqueueEvent(LanTransportEvent transportEvent)
        {
            _mainThreadEvents.Enqueue(transportEvent);
        }

        private static LanEndpoint ToLanEndpoint(EndPoint endpoint)
        {
            var ipEndpoint = endpoint as IPEndPoint;
            if (ipEndpoint == null)
            {
                return default(LanEndpoint);
            }

            return new LanEndpoint(ipEndpoint.Address.ToString(), ipEndpoint.Port);
        }

        private static void CloseUdpClient(UdpClient client)
        {
            if (client == null)
            {
                return;
            }

            try
            {
                client.Close();
            }
            catch
            {
            }
        }

        private static void CloseTcpClient(TcpClient client)
        {
            if (client == null)
            {
                return;
            }

            try
            {
                client.Close();
            }
            catch
            {
            }
        }

        private static void JoinThread(Thread thread)
        {
            if (thread == null || thread == Thread.CurrentThread)
            {
                return;
            }

            try
            {
                thread.Join(ThreadJoinTimeoutMs);
            }
            catch
            {
            }
        }

        private sealed class TcpConnection
        {
            public readonly object WriteLock = new object();
            public LanConnectionId Id;
            public TcpClient Client;
            public NetworkStream Stream;
            public Thread ReceiveThread;
            public volatile bool IsActive;
            public LanEndpoint LocalEndpoint;
            public LanEndpoint RemoteEndpoint;
        }
    }
}

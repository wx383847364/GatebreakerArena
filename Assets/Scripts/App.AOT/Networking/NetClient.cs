using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.Shared.Contracts;
using App.AOT.Networking.Transport;
using App.AOT.Networking.Auth;

namespace App.AOT.Networking
{
    /// <summary>
    /// 网络客户端：连接、心跳、重连、收发、消息分发
    /// </summary>
    public class NetClient : INetClient
    {
        private IAppLogger _logger;
        private ITransport _transport;
        private AuthContext _authContext;
        private bool _isConnected;
        private float _heartbeatInterval = 30f;
        private float _heartbeatTimer;

        public bool IsConnected => _isConnected && _transport != null && _transport.IsConnected;

        public NetClient()
        {
        }

        /// <summary>
        /// 设置日志器（通过DI注入）
        /// </summary>
        public void SetLogger(IAppLogger logger)
        {
            _logger = logger;
        }

        public void Initialize()
        {
            // 初始化传输层（根据平台选择HTTP或WebSocket）
            _transport = new HttpTransport();
            _transport.Initialize();
            _transport.OnConnectionChanged += OnConnectionChanged;

            _authContext = new AuthContext();

            _logger?.LogInfo("NetClient: 初始化完成");
        }

        public void Update(float deltaTime)
        {
            _transport?.Update(deltaTime);

            // 心跳
            if (IsConnected)
            {
                _heartbeatTimer += deltaTime;
                if (_heartbeatTimer >= _heartbeatInterval)
                {
                    _heartbeatTimer = 0f;
                    SendHeartbeatAsync();
                }
            }
        }

        public void Shutdown()
        {
            _transport?.DisconnectWebSocket();
            _transport?.Shutdown();
            _logger?.LogInfo("NetClient: 已关闭");
        }

        /// <summary>
        /// 发送HTTP请求
        /// </summary>
        public async Task<TransportResponse> SendRequestAsync(string url, string method = "GET", byte[] body = null, Dictionary<string, string> headers = null)
        {
            if (_transport == null)
            {
                return new TransportResponse { Error = "Transport未初始化" };
            }

            var request = new TransportRequest
            {
                Url = url,
                Method = method,
                Body = body,
                Headers = headers ?? new Dictionary<string, string>()
            };

            // 添加认证头
            AddAuthHeaders(request.Headers);

            return await _transport.SendHttpAsync(request);
        }

        /// <summary>
        /// 连接WebSocket
        /// </summary>
        public async Task<bool> ConnectWebSocketAsync(string url)
        {
            if (_transport == null)
            {
                return false;
            }

            return await _transport.ConnectWebSocketAsync(url);
        }

        /// <summary>
        /// 断开WebSocket
        /// </summary>
        public void DisconnectWebSocket()
        {
            _transport?.DisconnectWebSocket();
        }

        /// <summary>
        /// 发送WebSocket消息
        /// </summary>
        public async Task<bool> SendWebSocketAsync(byte[] data)
        {
            if (_transport == null || !IsConnected)
            {
                return false;
            }

            return await _transport.SendWebSocketAsync(data);
        }

        private void OnConnectionChanged(bool connected)
        {
            _isConnected = connected;
            _logger?.LogInfo($"NetClient: 连接状态变化: {connected}");
        }

        private void AddAuthHeaders(Dictionary<string, string> headers)
        {
            if (_authContext != null && !string.IsNullOrEmpty(_authContext.Token))
            {
                headers["Authorization"] = $"Bearer {_authContext.Token}";
                headers["X-User-Id"] = _authContext.UserId ?? "";
            }
        }

        private async void SendHeartbeatAsync()
        {
            try
            {
                // TODO: 实现心跳消息
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"NetClient: 心跳失败: {ex}");
            }
        }
    }
}

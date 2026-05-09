using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using App.Shared.Contracts;

namespace App.AOT.Networking.Transport
{
    /// <summary>
    /// HTTP传输层实现
    /// </summary>
    public class HttpTransport : ITransport
    {
        private bool _isConnected;
        private Action<byte[]> _onWebSocketMessage;

        public bool IsConnected => _isConnected;
        public event Action<bool> OnConnectionChanged;
        public event Action<byte[]> OnWebSocketMessage
        {
            add => _onWebSocketMessage += value;
            remove => _onWebSocketMessage -= value;
        }

        public void Initialize()
        {
            _isConnected = true; // HTTP是无状态的，始终"连接"
            OnConnectionChanged?.Invoke(true);
        }

        public void Update(float deltaTime)
        {
            // HTTP不需要每帧更新
        }

        public void Shutdown()
        {
            _isConnected = false;
            OnConnectionChanged?.Invoke(false);
        }

        public async Task<TransportResponse> SendHttpAsync(TransportRequest request)
        {
            try
            {
                UnityWebRequest www;

                if (request.Method == "GET")
                {
                    www = UnityWebRequest.Get(request.Url);
                }
                else if (request.Method == "POST")
                {
                    www = UnityWebRequest.PostWwwForm(request.Url, "");
                    if (request.Body != null && request.Body.Length > 0)
                    {
                        www.uploadHandler = new UploadHandlerRaw(request.Body);
                        www.SetRequestHeader("Content-Type", "application/json");
                    }
                }
                else
                {
                    www = UnityWebRequest.Put(request.Url, request.Body ?? new byte[0]);
                }

                // 添加自定义头
                if (request.Headers != null)
                {
                    foreach (var header in request.Headers)
                    {
                        www.SetRequestHeader(header.Key, header.Value);
                    }
                }

                www.timeout = request.Timeout;

                var operation = www.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                var response = new TransportResponse
                {
                    StatusCode = (int)www.responseCode,
                    Data = www.downloadHandler?.data,
                    Error = www.error
                };

                www.Dispose();
                return response;
            }
            catch (Exception ex)
            {
                return new TransportResponse
                {
                    StatusCode = 0,
                    Error = ex.Message
                };
            }
        }

        public Task<bool> ConnectWebSocketAsync(string url)
        {
            // HTTP传输不支持WebSocket
            return Task.FromResult(false);
        }

        public void DisconnectWebSocket()
        {
            // HTTP传输不支持WebSocket
        }

        public Task<bool> SendWebSocketAsync(byte[] data)
        {
            // HTTP传输不支持WebSocket
            return Task.FromResult(false);
        }

        private void NotifyWebSocketMessage(byte[] data)
        {
            _onWebSocketMessage?.Invoke(data);
        }
    }
}

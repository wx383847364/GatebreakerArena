using System;
using System.Threading.Tasks;

namespace App.Shared.Contracts
{
    /// <summary>
    /// 网络传输层接口，支持HTTP和WebSocket
    /// </summary>
    public interface ITransport : IService
    {
        /// <summary>
        /// 是否已连接
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        event Action<bool> OnConnectionChanged;

        /// <summary>
        /// 发送HTTP请求
        /// </summary>
        Task<TransportResponse> SendHttpAsync(TransportRequest request);

        /// <summary>
        /// 连接WebSocket
        /// </summary>
        Task<bool> ConnectWebSocketAsync(string url);

        /// <summary>
        /// 断开WebSocket连接
        /// </summary>
        void DisconnectWebSocket();

        /// <summary>
        /// 发送WebSocket消息
        /// </summary>
        Task<bool> SendWebSocketAsync(byte[] data);

        /// <summary>
        /// WebSocket消息接收事件
        /// </summary>
        event Action<byte[]> OnWebSocketMessage;
    }

    /// <summary>
    /// HTTP请求
    /// </summary>
    public class TransportRequest
    {
        public string Url { get; set; }
        public string Method { get; set; } = "GET";
        public byte[] Body { get; set; }
        public System.Collections.Generic.Dictionary<string, string> Headers { get; set; }
        public int Timeout { get; set; } = 30;
    }

    /// <summary>
    /// HTTP响应
    /// </summary>
    public class TransportResponse
    {
        public int StatusCode { get; set; }
        public byte[] Data { get; set; }
        public string Error { get; set; }
        public bool IsSuccess => StatusCode >= 200 && StatusCode < 300 && string.IsNullOrEmpty(Error);
    }
}

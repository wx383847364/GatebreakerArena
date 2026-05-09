using UnityEngine;
using App.Shared.Contracts;
using SharedLogLevel = App.Shared.Contracts.LogLevel;

namespace App.AOT.Infrastructure.Logger
{
    /// <summary>
    /// Unity日志实现
    /// </summary>
    public class UnityLogger : IAppLogger, IService
    {
        private SharedLogLevel _minLevel = SharedLogLevel.Debug;

        public void Initialize()
        {
            // 可以在这里初始化文件日志
        }

        public void Update(float deltaTime)
        {
            // 日志不需要每帧更新
        }

        public void Shutdown()
        {
            // 清理资源
        }

        public void Log(SharedLogLevel level, string message, params object[] args)
        {
            if (level < _minLevel) return;

            var formattedMessage = args != null && args.Length > 0 
                ? string.Format(message, args) 
                : message;

            switch (level)
            {
                case SharedLogLevel.Debug:
                    Debug.Log($"[DEBUG] {formattedMessage}");
                    break;
                case SharedLogLevel.Info:
                    Debug.Log($"[INFO] {formattedMessage}");
                    break;
                case SharedLogLevel.Warning:
                    Debug.LogWarning($"[WARN] {formattedMessage}");
                    break;
                case SharedLogLevel.Error:
                    Debug.LogError($"[ERROR] {formattedMessage}");
                    break;
            }
        }

        public void LogDebug(string message, params object[] args)
        {
            Log(SharedLogLevel.Debug, message, args);
        }

        public void LogInfo(string message, params object[] args)
        {
            Log(SharedLogLevel.Info, message, args);
        }

        public void LogWarning(string message, params object[] args)
        {
            Log(SharedLogLevel.Warning, message, args);
        }

        public void LogError(string message, params object[] args)
        {
            Log(SharedLogLevel.Error, message, args);
        }
    }
}

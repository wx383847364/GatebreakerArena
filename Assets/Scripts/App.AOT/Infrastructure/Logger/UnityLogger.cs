using System;
using System.IO;
using System.Text;
using System.Threading;
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
        private FileLogWriter _fileLogWriter;
        private bool _subscribedToUnityLog;
        private bool _shutdown;

        public string CurrentLogPath => _fileLogWriter?.CurrentLogPath ?? string.Empty;

        public void Initialize()
        {
            if (_shutdown)
            {
                return;
            }

            InitializeFileLogging();
            SubscribeUnityLogCallback();

            if (!string.IsNullOrEmpty(CurrentLogPath))
            {
                LogInfo("UnityLogger: 文件日志已启用。path={0}", CurrentLogPath);
            }
        }

        public void Update(float deltaTime)
        {
            // 日志不需要每帧更新
        }

        public void Shutdown()
        {
            if (_shutdown)
            {
                return;
            }

            _shutdown = true;
            UnsubscribeUnityLogCallback();
            _fileLogWriter?.Dispose();
            _fileLogWriter = null;
        }

        public void Flush()
        {
            _fileLogWriter?.Flush();
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

        private void InitializeFileLogging()
        {
            if (_fileLogWriter != null)
            {
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            _fileLogWriter = new FileLogWriter(string.Empty);
#else
            string logDirectory = Path.Combine(Application.persistentDataPath, "logs");
            _fileLogWriter = new FileLogWriter(logDirectory);
#endif
            _fileLogWriter.Initialize();
        }

        private void SubscribeUnityLogCallback()
        {
            if (_subscribedToUnityLog)
            {
                return;
            }

            Application.logMessageReceivedThreaded += HandleUnityLogMessage;
            _subscribedToUnityLog = true;
        }

        private void UnsubscribeUnityLogCallback()
        {
            if (!_subscribedToUnityLog)
            {
                return;
            }

            Application.logMessageReceivedThreaded -= HandleUnityLogMessage;
            _subscribedToUnityLog = false;
        }

        private void HandleUnityLogMessage(string condition, string stackTrace, LogType type)
        {
            try
            {
                _fileLogWriter?.WriteEntry(BuildLogEntry(condition, stackTrace, type));
            }
            catch (Exception)
            {
                // Never route file logging failures back through Unity logging.
            }
        }

        private static string BuildLogEntry(string condition, string stackTrace, LogType type)
        {
            string prefix = BuildLinePrefix(type);
            var builder = new StringBuilder();
            AppendPrefixedLines(builder, prefix, condition);

            if (ShouldWriteStackTrace(type) && !string.IsNullOrWhiteSpace(stackTrace))
            {
                AppendPrefixedLines(builder, prefix, "STACK:");
                AppendPrefixedLines(builder, prefix, stackTrace);
            }

            return builder.ToString();
        }

        private static string BuildLinePrefix(LogType type)
        {
            return string.Format(
                "{0} [{1}] [T:{2}] ",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                type,
                Thread.CurrentThread.ManagedThreadId);
        }

        private static void AppendPrefixedLines(StringBuilder builder, string prefix, string text)
        {
            string normalized = string.IsNullOrEmpty(text)
                ? string.Empty
                : text.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                builder.Append(prefix);
                builder.AppendLine(lines[i]);
            }
        }

        private static bool ShouldWriteStackTrace(LogType type)
        {
            return type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
        }
    }
}

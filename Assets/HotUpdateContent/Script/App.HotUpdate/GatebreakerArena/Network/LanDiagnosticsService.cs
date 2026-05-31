using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using App.Shared.Contracts;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Network
{
    public interface ILanDiagnosticsSink
    {
        void Record(LanDiagnosticEvent diagnosticEvent);
        void RecordTransportEvent(LanTransportEvent transportEvent);
        void RecordFrameTrace(LanFrameTrace trace);
        void Flush();
        LanDiagnosticsSnapshot CreateSnapshot();
        string CreateSummaryText(RoomSnapshot roomSnapshot);
    }

    public interface ILanDiagnosticsWriter
    {
        string CurrentLogPath { get; }
        string LastWriteError { get; }
        void WriteLine(string line);
        void Flush();
    }

    public interface ILanDiagnosticsClock
    {
        long MonotonicMilliseconds { get; }
    }

    public sealed class LanDiagnosticEvent
    {
        public const int SchemaVersion = 1;

        public string EventName { get; set; } = string.Empty;
        public long MonotonicMs { get; set; }
        public string Role { get; set; } = string.Empty;
        public string RoomCode { get; set; } = string.Empty;
        public ulong SessionId { get; set; }
        public uint ChannelId { get; set; }
        public ulong ClientInstanceId { get; set; }
        public int SlotIndex { get; set; } = -1;
        public int PlayerId { get; set; }
        public int FrameIndex { get; set; } = -1;
        public int ConnectionId { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public string MessageType { get; set; } = string.Empty;
        public uint Sequence { get; set; }
        public int PayloadBytes { get; set; }
        public uint PayloadHash { get; set; }
        public uint Checksum { get; set; }
        public uint ReferenceChecksum { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;

        public LanDiagnosticEvent Clone()
        {
            return (LanDiagnosticEvent)MemberwiseClone();
        }
    }

    public sealed class LanFrameTrace
    {
        public int FrameIndex { get; set; } = -1;
        public uint BundleSeq { get; set; }
        public uint Checksum { get; set; }
        public int LatestConfirmedFrame { get; set; } = -1;
        public int LocalTargetFrame { get; set; } = -1;
        public int[] InputSlots { get; set; } = Array.Empty<int>();
        public int[] WaitingSlots { get; set; } = Array.Empty<int>();

        public LanFrameTrace Clone()
        {
            return new LanFrameTrace
            {
                FrameIndex = FrameIndex,
                BundleSeq = BundleSeq,
                Checksum = Checksum,
                LatestConfirmedFrame = LatestConfirmedFrame,
                LocalTargetFrame = LocalTargetFrame,
                InputSlots = CloneArray(InputSlots),
                WaitingSlots = CloneArray(WaitingSlots),
            };
        }

        private static int[] CloneArray(int[] values)
        {
            return values != null ? (int[])values.Clone() : Array.Empty<int>();
        }
    }

    public sealed class LanDiagnosticsSnapshot
    {
        public bool IsEnabled { get; set; }
        public string DiagSessionId { get; set; } = string.Empty;
        public string CurrentLogPath { get; set; } = string.Empty;
        public string LastWriteError { get; set; } = string.Empty;
        public int EventCount { get; set; }
        public LanDiagnosticEvent[] RecentEvents { get; set; } = Array.Empty<LanDiagnosticEvent>();
        public LanFrameTrace[] RecentFrames { get; set; } = Array.Empty<LanFrameTrace>();
    }

    public sealed class LanDiagnosticsService : ILanDiagnosticsSink, ITickable
    {
        public const int EventCapacity = 512;
        public const int FrameTraceCapacity = 300;
        private const float FlushIntervalSeconds = 2f;
        private const int FileSizeLimitBytes = 5 * 1024 * 1024;
        private const int RetainedFileCount = 5;

        private readonly Queue<LanDiagnosticEvent> _events = new Queue<LanDiagnosticEvent>();
        private readonly Queue<LanFrameTrace> _frameTraces = new Queue<LanFrameTrace>();
        private readonly ILanDiagnosticsClock _clock;
        private readonly ILanDiagnosticsWriter _writer;
        private readonly string _diagSessionId;
        private float _flushTimer;
        private int _eventCount;
        private bool _isEnabled;

        public LanDiagnosticsService()
            : this(null, null)
        {
        }

        public LanDiagnosticsService(ILanDiagnosticsWriter writer, ILanDiagnosticsClock clock)
        {
            _clock = clock ?? new UnityLanDiagnosticsClock();
            _writer = writer ?? new FileLanDiagnosticsWriter(
                Path.Combine(UnityEngine.Application.persistentDataPath, "logs"),
                FileSizeLimitBytes,
                RetainedFileCount);
            _diagSessionId = CreateDiagSessionId();
            _isEnabled = UnityEngine.Application.isEditor || Debug.isDebugBuild;
            Record(new LanDiagnosticEvent
            {
                EventName = "DiagnosticsStarted",
                Detail = BuildDeviceDetail(),
            });
        }

        public string CurrentLogPath => _writer.CurrentLogPath;
        public bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        public void Initialize()
        {
        }

        public void Update(float deltaTime)
        {
            Tick(deltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (!_isEnabled)
            {
                return;
            }

            _flushTimer += Math.Max(0f, deltaTime);
            if (_flushTimer >= FlushIntervalSeconds)
            {
                _flushTimer = 0f;
                Flush();
            }
        }

        public void Shutdown()
        {
            Record(new LanDiagnosticEvent { EventName = "DiagnosticsShutdown" });
            Flush();
        }

        public void Record(LanDiagnosticEvent diagnosticEvent)
        {
            if (!_isEnabled || diagnosticEvent == null || string.IsNullOrWhiteSpace(diagnosticEvent.EventName))
            {
                return;
            }

            diagnosticEvent.MonotonicMs = diagnosticEvent.MonotonicMs > 0
                ? diagnosticEvent.MonotonicMs
                : _clock.MonotonicMilliseconds;
            EnqueueEvent(diagnosticEvent.Clone());
            _writer.WriteLine(LanDiagnosticJson.WriteEvent(
                diagnosticEvent,
                _diagSessionId,
                UnityEngine.Application.version,
                SystemInfo.deviceName,
                UnityEngine.Application.platform.ToString()));
        }

        public void RecordTransportEvent(LanTransportEvent transportEvent)
        {
            if (transportEvent == null)
            {
                return;
            }

            Record(new LanDiagnosticEvent
            {
                EventName = "Transport" + transportEvent.Type,
                Endpoint = FormatEndpoint(transportEvent.RemoteEndpoint.IsValid ? transportEvent.RemoteEndpoint : transportEvent.LocalEndpoint),
                ConnectionId = transportEvent.ConnectionId.Value,
                PayloadBytes = transportEvent.Payload != null ? transportEvent.Payload.Length : 0,
                ErrorCode = transportEvent.Error != LanTransportError.None ? transportEvent.Error.ToString() : string.Empty,
                Detail = transportEvent.Message ?? string.Empty,
            });
        }

        public void RecordFrameTrace(LanFrameTrace trace)
        {
            if (!_isEnabled || trace == null)
            {
                return;
            }

            if (_frameTraces.Count >= FrameTraceCapacity)
            {
                _frameTraces.Dequeue();
            }

            _frameTraces.Enqueue(trace.Clone());
        }

        public void Flush()
        {
            _writer.Flush();
        }

        public LanDiagnosticsSnapshot CreateSnapshot()
        {
            return new LanDiagnosticsSnapshot
            {
                IsEnabled = _isEnabled,
                DiagSessionId = _diagSessionId,
                CurrentLogPath = _writer.CurrentLogPath,
                LastWriteError = _writer.LastWriteError,
                EventCount = _eventCount,
                RecentEvents = CloneRecentEvents(30),
                RecentFrames = CloneRecentFrames(12),
            };
        }

        public string CreateSummaryText(RoomSnapshot roomSnapshot)
        {
            LanDiagnosticsSnapshot snapshot = CreateSnapshot();
            var builder = new StringBuilder(1024);
            builder.AppendLine("Gatebreaker LAN Diagnostics");
            builder.Append("diagSessionId=").AppendLine(snapshot.DiagSessionId);
            builder.Append("logPath=").AppendLine(snapshot.CurrentLogPath);
            builder.Append("writeError=").AppendLine(string.IsNullOrEmpty(snapshot.LastWriteError) ? "-" : snapshot.LastWriteError);
            if (roomSnapshot != null)
            {
                builder.Append("role=").AppendLine(roomSnapshot.IsHost ? "Host" : "Client");
                builder.Append("roomCode=").AppendLine(roomSnapshot.RoomCode ?? string.Empty);
                builder.Append("sessionId=").AppendLine(roomSnapshot.SessionId.ToString(CultureInfo.InvariantCulture));
                builder.Append("state=").AppendLine(roomSnapshot.State.ToString());
                builder.Append("slot=").AppendLine(roomSnapshot.LocalSlotIndex.ToString(CultureInfo.InvariantCulture));
                if (roomSnapshot.Lockstep != null)
                {
                    builder.Append("latestConfirmed=").AppendLine(roomSnapshot.Lockstep.LatestConfirmedFrame.ToString(CultureInfo.InvariantCulture));
                    builder.Append("localTarget=").AppendLine(roomSnapshot.Lockstep.LocalTargetFrame.ToString(CultureInfo.InvariantCulture));
                    builder.Append("waitingSlots=").AppendLine(JoinInts(roomSnapshot.Lockstep.WaitingSlotIndexes));
                }
            }

            builder.AppendLine("recentEvents:");
            for (int i = 0; i < snapshot.RecentEvents.Length; i++)
            {
                LanDiagnosticEvent item = snapshot.RecentEvents[i];
                builder.Append(item.MonotonicMs.ToString(CultureInfo.InvariantCulture))
                    .Append(' ')
                    .Append(item.EventName)
                    .Append(' ')
                    .Append(item.Detail ?? string.Empty)
                    .AppendLine();
            }

            return builder.ToString();
        }

        public string ExportDiagnosticsPackage(RoomSnapshot roomSnapshot)
        {
            string summary = CreateSummaryText(roomSnapshot);
            try
            {
                string logPath = CurrentLogPath;
                string directory = !string.IsNullOrEmpty(logPath)
                    ? Path.GetDirectoryName(logPath)
                    : Path.Combine(UnityEngine.Application.persistentDataPath, "logs");
                Directory.CreateDirectory(directory);
                string exportDirectory = Path.Combine(
                    directory,
                    "lan_diag_export_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture));
                Directory.CreateDirectory(exportDirectory);
                string summaryPath = Path.Combine(exportDirectory, "summary.txt");
                File.WriteAllText(summaryPath, summary, Encoding.UTF8);
                CopyIfExists(logPath, Path.Combine(exportDirectory, Path.GetFileName(logPath)));
                CopyIfExists(Path.Combine(directory, "game.log"), Path.Combine(exportDirectory, "game.log"));
                CopyIfExists(Path.Combine(directory, "game.previous.log"), Path.Combine(exportDirectory, "game.previous.log"));
                Record(new LanDiagnosticEvent
                {
                    EventName = "DiagnosticsExportCreated",
                    Detail = exportDirectory,
                });
                Flush();
                return exportDirectory;
            }
            catch (Exception ex)
            {
                Record(new LanDiagnosticEvent
                {
                    EventName = "DiagnosticsWriteFailed",
                    ErrorCode = "ExportSummary",
                    Detail = ex.Message,
                });
                return string.Empty;
            }
        }

        public string ExportSummary(RoomSnapshot roomSnapshot)
        {
            return ExportDiagnosticsPackage(roomSnapshot);
        }

        public void RecordRoomEvent(string eventName, RoomSnapshot snapshot, string result = null, string detail = null)
        {
            Record(new LanDiagnosticEvent
            {
                EventName = eventName,
                Role = snapshot != null ? (snapshot.IsHost ? "Host" : "Client") : string.Empty,
                RoomCode = snapshot?.RoomCode ?? string.Empty,
                SessionId = snapshot?.SessionId ?? 0UL,
                ChannelId = snapshot?.ChannelId ?? 0U,
                SlotIndex = snapshot?.LocalSlotIndex ?? -1,
                Result = result ?? string.Empty,
                Detail = detail ?? string.Empty,
            });
        }

        private void EnqueueEvent(LanDiagnosticEvent diagnosticEvent)
        {
            if (_events.Count >= EventCapacity)
            {
                _events.Dequeue();
            }

            _events.Enqueue(diagnosticEvent);
            _eventCount++;
        }

        private LanDiagnosticEvent[] CloneRecentEvents(int maxCount)
        {
            LanDiagnosticEvent[] all = _events.ToArray();
            int count = Math.Min(maxCount, all.Length);
            var result = new LanDiagnosticEvent[count];
            int start = all.Length - count;
            for (int i = 0; i < count; i++)
            {
                result[i] = all[start + i].Clone();
            }

            return result;
        }

        private LanFrameTrace[] CloneRecentFrames(int maxCount)
        {
            LanFrameTrace[] all = _frameTraces.ToArray();
            int count = Math.Min(maxCount, all.Length);
            var result = new LanFrameTrace[count];
            int start = all.Length - count;
            for (int i = 0; i < count; i++)
            {
                result[i] = all[start + i].Clone();
            }

            return result;
        }

        private static string CreateDiagSessionId()
        {
            return DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + "_" +
                   Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private static string BuildDeviceDetail()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "appVersion={0};platform={1};device={2};model={3};os={4}",
                UnityEngine.Application.version,
                UnityEngine.Application.platform,
                SystemInfo.deviceName,
                SystemInfo.deviceModel,
                SystemInfo.operatingSystem);
        }

        private static string FormatEndpoint(LanEndpoint endpoint)
        {
            return endpoint.IsValid ? endpoint.ToString() : string.Empty;
        }

        private static string JoinInts(int[] values)
        {
            if (values == null || values.Length <= 0)
            {
                return "-";
            }

            return string.Join(",", Array.ConvertAll(values, item => item.ToString(CultureInfo.InvariantCulture)));
        }

        private static void CopyIfExists(string sourcePath, string destinationPath)
        {
            if (string.IsNullOrEmpty(sourcePath) ||
                string.IsNullOrEmpty(destinationPath) ||
                !File.Exists(sourcePath))
            {
                return;
            }

            File.Copy(sourcePath, destinationPath, true);
        }
    }

    public sealed class UnityLanDiagnosticsClock : ILanDiagnosticsClock
    {
        public long MonotonicMilliseconds => (long)(Time.realtimeSinceStartup * 1000f);
    }

    public sealed class FileLanDiagnosticsWriter : ILanDiagnosticsWriter
    {
        private readonly string _directory;
        private readonly int _fileSizeLimitBytes;
        private readonly int _retainedFileCount;
        private readonly object _sync = new object();
        private StreamWriter _writer;

        public FileLanDiagnosticsWriter(string directory, int fileSizeLimitBytes, int retainedFileCount)
        {
            _directory = directory ?? string.Empty;
            _fileSizeLimitBytes = Math.Max(1024, fileSizeLimitBytes);
            _retainedFileCount = Math.Max(1, retainedFileCount);
            CurrentLogPath = string.Empty;
        }

        public string CurrentLogPath { get; private set; }
        public string LastWriteError { get; private set; } = string.Empty;

        public void WriteLine(string line)
        {
            if (string.IsNullOrEmpty(_directory))
            {
                return;
            }

            lock (_sync)
            {
                try
                {
                    EnsureWriter();
                    _writer.WriteLine(line ?? string.Empty);
                    if (_writer.BaseStream.Length >= _fileSizeLimitBytes)
                    {
                        RotateWriter();
                    }
                }
                catch (Exception ex)
                {
                    LastWriteError = ex.Message;
                }
            }
        }

        public void Flush()
        {
            lock (_sync)
            {
                try
                {
                    _writer?.Flush();
                }
                catch (Exception ex)
                {
                    LastWriteError = ex.Message;
                }
            }
        }

        private void EnsureWriter()
        {
            if (_writer != null)
            {
                return;
            }

            Directory.CreateDirectory(_directory);
            CurrentLogPath = Path.Combine(
                _directory,
                "lan_diag_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".jsonl");
            _writer = new StreamWriter(CurrentLogPath, true, Encoding.UTF8);
            PruneOldFiles();
        }

        private void RotateWriter()
        {
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
            EnsureWriter();
        }

        private void PruneOldFiles()
        {
            string[] files = Directory.GetFiles(_directory, "lan_diag_*.jsonl");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            int removeCount = files.Length - _retainedFileCount;
            for (int i = 0; i < removeCount; i++)
            {
                File.Delete(files[i]);
            }
        }
    }

    public static class LanDiagnosticJson
    {
        public static string WriteEvent(
            LanDiagnosticEvent value,
            string diagSessionId,
            string appVersion,
            string deviceName,
            string platform)
        {
            var builder = new StringBuilder(512);
            builder.Append('{');
            Append(builder, "schemaVersion", LanDiagnosticEvent.SchemaVersion, false);
            Append(builder, "diagSessionId", diagSessionId, true);
            Append(builder, "appVersion", appVersion, true);
            Append(builder, "deviceName", deviceName, true);
            Append(builder, "platform", platform, true);
            Append(builder, "eventName", value.EventName, true);
            Append(builder, "monotonicMs", value.MonotonicMs, true);
            Append(builder, "role", value.Role, true);
            Append(builder, "roomCode", value.RoomCode, true);
            Append(builder, "sessionId", value.SessionId, true);
            Append(builder, "channelId", value.ChannelId, true);
            Append(builder, "clientInstanceId", value.ClientInstanceId, true);
            Append(builder, "slotIndex", value.SlotIndex, true);
            Append(builder, "playerId", value.PlayerId, true);
            Append(builder, "frameIndex", value.FrameIndex, true);
            Append(builder, "connectionId", value.ConnectionId, true);
            Append(builder, "endpoint", value.Endpoint, true);
            Append(builder, "messageType", value.MessageType, true);
            Append(builder, "sequence", value.Sequence, true);
            Append(builder, "payloadBytes", value.PayloadBytes, true);
            Append(builder, "payloadHash", value.PayloadHash, true);
            Append(builder, "checksum", value.Checksum, true);
            Append(builder, "referenceChecksum", value.ReferenceChecksum, true);
            Append(builder, "errorCode", value.ErrorCode, true);
            Append(builder, "result", value.Result, true);
            Append(builder, "detail", value.Detail, true);
            builder.Append('}');
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, string key, string value, bool prependComma)
        {
            if (prependComma)
            {
                builder.Append(',');
            }

            builder.Append('"').Append(Escape(key)).Append("\":\"").Append(Escape(value ?? string.Empty)).Append('"');
        }

        private static void Append(StringBuilder builder, string key, int value, bool prependComma)
        {
            AppendNumber(builder, key, value.ToString(CultureInfo.InvariantCulture), prependComma);
        }

        private static void Append(StringBuilder builder, string key, long value, bool prependComma)
        {
            AppendNumber(builder, key, value.ToString(CultureInfo.InvariantCulture), prependComma);
        }

        private static void Append(StringBuilder builder, string key, uint value, bool prependComma)
        {
            AppendNumber(builder, key, value.ToString(CultureInfo.InvariantCulture), prependComma);
        }

        private static void Append(StringBuilder builder, string key, ulong value, bool prependComma)
        {
            AppendNumber(builder, key, value.ToString(CultureInfo.InvariantCulture), prependComma);
        }

        private static void AppendNumber(StringBuilder builder, string key, string value, bool prependComma)
        {
            if (prependComma)
            {
                builder.Append(',');
            }

            builder.Append('"').Append(Escape(key)).Append("\":").Append(value);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (c < ' ')
                        {
                            builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }

                        break;
                }
            }

            return builder.ToString();
        }
    }
}

#if UNITY_WEBGL && !UNITY_EDITOR
using System;

namespace App.AOT.Infrastructure.Logger
{
    internal sealed class FileLogWriter : IDisposable
    {
        public const long MaxFileBytes = 10L * 1024L * 1024L;

        public string CurrentLogPath => string.Empty;
        public bool IsEnabled => false;
        public string LastInternalError => string.Empty;

        public FileLogWriter(string logDirectory)
        {
        }

        public void Initialize()
        {
        }

        public void WriteEntry(string entry)
        {
        }

        public void Flush()
        {
        }

        public void Dispose()
        {
        }
    }
}
#else
using System;
using System.IO;
using System.Text;

namespace App.AOT.Infrastructure.Logger
{
    internal sealed class FileLogWriter : IDisposable
    {
        public const long MaxFileBytes = 10L * 1024L * 1024L;

        private const string CurrentFileName = "game.log";
        private const string PreviousFileName = "game.previous.log";
        private const string TruncatedMarkerText = "[TRUNCATED]";

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
        private readonly object _syncRoot = new object();
        private readonly string _logDirectory;
        private readonly string _currentLogPath;
        private readonly string _previousLogPath;

        private StreamWriter _writer;
        private long _currentSizeBytes;
        private bool _disposed;
        private bool _disabled;
        private string _lastInternalError = string.Empty;

        public FileLogWriter(string logDirectory)
        {
            _logDirectory = logDirectory ?? string.Empty;
            _currentLogPath = Path.Combine(_logDirectory, CurrentFileName);
            _previousLogPath = Path.Combine(_logDirectory, PreviousFileName);
        }

        public string CurrentLogPath => _disabled ? string.Empty : _currentLogPath;

        public bool IsEnabled => !_disabled && !_disposed && _writer != null;

        public string LastInternalError => _lastInternalError;

        public void Initialize()
        {
            lock (_syncRoot)
            {
                if (_disposed || _disabled || _writer != null)
                {
                    return;
                }

                try
                {
                    Directory.CreateDirectory(_logDirectory);
                    RotateFilesForNewSession();
                    OpenCurrentWriter();
                }
                catch (Exception ex)
                {
                    DisableLocked(ex);
                }
            }
        }

        public void WriteEntry(string entry)
        {
            if (string.IsNullOrEmpty(entry))
            {
                return;
            }

            lock (_syncRoot)
            {
                if (_disposed || _disabled || _writer == null)
                {
                    return;
                }

                try
                {
                    string writableEntry = TrimEntryToLimit(entry);
                    long entryBytes = Utf8NoBom.GetByteCount(writableEntry);
                    if (_currentSizeBytes > 0 && _currentSizeBytes + entryBytes > MaxFileBytes)
                    {
                        RollCurrentFileLocked();
                    }

                    _writer.Write(writableEntry);
                    _currentSizeBytes += entryBytes;

                    if (_currentSizeBytes >= MaxFileBytes)
                    {
                        _writer.Flush();
                    }
                }
                catch (Exception ex)
                {
                    DisableLocked(ex);
                }
            }
        }

        public void Flush()
        {
            lock (_syncRoot)
            {
                if (_disposed || _disabled || _writer == null)
                {
                    return;
                }

                try
                {
                    _writer.Flush();
                }
                catch (Exception ex)
                {
                    DisableLocked(ex);
                }
            }
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                CloseWriterLocked();
            }
        }

        private void RotateFilesForNewSession()
        {
            DeleteIfExists(_previousLogPath);
            if (File.Exists(_currentLogPath))
            {
                File.Move(_currentLogPath, _previousLogPath);
            }
        }

        private void RollCurrentFileLocked()
        {
            Exception closeError = CloseWriterLocked();
            if (closeError != null)
            {
                throw closeError;
            }

            DeleteIfExists(_previousLogPath);
            if (File.Exists(_currentLogPath))
            {
                File.Move(_currentLogPath, _previousLogPath);
            }

            OpenCurrentWriter();
        }

        private void OpenCurrentWriter()
        {
            var stream = new FileStream(_currentLogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            _writer = new StreamWriter(stream, Utf8NoBom);
            _currentSizeBytes = stream.Length;
        }

        private Exception CloseWriterLocked()
        {
            StreamWriter writer = _writer;
            _writer = null;
            _currentSizeBytes = 0L;

            if (writer == null)
            {
                return null;
            }

            Exception closeError = null;
            try
            {
                writer.Flush();
            }
            catch (Exception ex)
            {
                _lastInternalError = ex.Message;
                closeError = ex;
            }

            try
            {
                writer.Dispose();
            }
            catch (Exception ex)
            {
                _lastInternalError = ex.Message;
                closeError = closeError ?? ex;
            }

            return closeError;
        }

        private void DisableLocked(Exception ex)
        {
            _lastInternalError = ex?.Message ?? "unknown";
            _disabled = true;
            CloseWriterLocked();
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static string TrimEntryToLimit(string entry)
        {
            int entryBytes = Utf8NoBom.GetByteCount(entry);
            if (entryBytes <= MaxFileBytes)
            {
                return entry;
            }

            string marker = BuildTruncatedMarker(entry);
            int markerBytes = Utf8NoBom.GetByteCount(marker);
            int byteLimit = (int)Math.Max(0L, MaxFileBytes - markerBytes);
            string trimmed = TrimToUtf8ByteCount(entry, byteLimit);
            return trimmed + marker;
        }

        private static string BuildTruncatedMarker(string entry)
        {
            string prefix = ExtractFirstLinePrefix(entry);
            return Environment.NewLine + prefix + TruncatedMarkerText + Environment.NewLine;
        }

        private static string ExtractFirstLinePrefix(string entry)
        {
            if (string.IsNullOrEmpty(entry))
            {
                return string.Empty;
            }

            int firstNewLine = entry.IndexOf('\n');
            string firstLine = firstNewLine >= 0 ? entry.Substring(0, firstNewLine) : entry;
            int threadTokenIndex = firstLine.IndexOf("[T:", StringComparison.Ordinal);
            if (threadTokenIndex < 0)
            {
                return string.Empty;
            }

            int threadTokenEnd = firstLine.IndexOf("] ", threadTokenIndex, StringComparison.Ordinal);
            if (threadTokenEnd < 0)
            {
                return string.Empty;
            }

            return firstLine.Substring(0, threadTokenEnd + 2);
        }

        private static string TrimToUtf8ByteCount(string text, int byteLimit)
        {
            if (string.IsNullOrEmpty(text) || byteLimit <= 0)
            {
                return string.Empty;
            }

            int low = 0;
            int high = text.Length;
            while (low < high)
            {
                int mid = low + ((high - low + 1) / 2);
                if (Utf8NoBom.GetByteCount(text.Substring(0, mid)) <= byteLimit)
                {
                    low = mid;
                }
                else
                {
                    high = mid - 1;
                }
            }

            if (low > 0 && char.IsHighSurrogate(text[low - 1]))
            {
                low--;
            }

            return text.Substring(0, low);
        }
    }
}
#endif

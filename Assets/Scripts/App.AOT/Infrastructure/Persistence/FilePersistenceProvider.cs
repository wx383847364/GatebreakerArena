using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using App.Shared.Contracts;

namespace App.AOT.Infrastructure.Persistence
{
    /// <summary>
    /// 文件系统持久化提供者
    /// </summary>
    public class FilePersistenceProvider : IPersistence
    {
        private const string PlayerPrefsPrefix = "GatebreakerArena.Persistence.";

        private readonly string _basePath;
        private readonly SemaphoreSlim _fileGate = new SemaphoreSlim(1, 1);

        public FilePersistenceProvider(string basePath = null)
        {
            _basePath = basePath ?? Application.persistentDataPath;
        }

        public static string BuildSafeFileName(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return "empty";
            }

            var builder = new System.Text.StringBuilder(key.Length);
            foreach (char c in key)
            {
                if ((c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    c == '.' ||
                    c == '_' ||
                    c == '-')
                {
                    builder.Append(c);
                }
                else
                {
                    builder.Append('_');
                }
            }

            return builder.Length > 0 ? builder.ToString() : "empty";
        }

        private string GetFilePath(string key)
        {
            return Path.Combine(_basePath, $"{BuildSafeFileName(key)}.dat");
        }

        private string GetTempFilePath(string key)
        {
            return $"{GetFilePath(key)}.tmp";
        }

        private string GetBackupFilePath(string key)
        {
            return $"{GetFilePath(key)}.bak";
        }

        private string GetLegacyFilePath(string key)
        {
            return Path.Combine(_basePath, $"{key}.dat");
        }

        private static string GetPlayerPrefsKey(string key)
        {
            return $"{PlayerPrefsPrefix}{BuildSafeFileName(key)}";
        }

        public async Task<bool> SaveAsync(string key, byte[] data)
        {
            byte[] bytes = data ?? Array.Empty<byte>();
            await _fileGate.WaitAsync().ConfigureAwait(false);
            try
            {
                var filePath = GetFilePath(key);
                var tempPath = GetTempFilePath(key);
                var backupPath = GetBackupFilePath(key);
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllBytesAsync(tempPath, bytes).ConfigureAwait(false);
                if (File.Exists(filePath))
                {
                    ReplaceFileWithRecovery(tempPath, filePath, backupPath);
                }
                else
                {
                    File.Move(tempPath, filePath);
                }

                DeleteFileIfExists(backupPath);
                DeletePlayerPrefsIfExists(key);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"FilePersistenceProvider: File save failed for {key}, using PlayerPrefs fallback. {ex}");
                return SaveFallbackAndRetireFiles(key, bytes);
            }
            finally
            {
                _fileGate.Release();
            }
        }

        public async Task<byte[]> LoadAsync(string key)
        {
            await _fileGate.WaitAsync().ConfigureAwait(false);
            try
            {
                string filePath = GetFilePath(key);
                byte[] fileBytes = await TryLoadFileAsync(filePath, key).ConfigureAwait(false);
                if (fileBytes != null)
                {
                    return fileBytes;
                }

                fileBytes = await TryRecoverFileAsync(GetTempFilePath(key), filePath, key).ConfigureAwait(false);
                if (fileBytes != null)
                {
                    return fileBytes;
                }

                fileBytes = await TryRecoverFileAsync(GetBackupFilePath(key), filePath, key).ConfigureAwait(false);
                if (fileBytes != null)
                {
                    return fileBytes;
                }

                string legacyPath = GetLegacyFilePath(key);
                if (!string.Equals(legacyPath, filePath, StringComparison.Ordinal))
                {
                    fileBytes = await TryLoadFileAsync(legacyPath, key).ConfigureAwait(false);
                    if (fileBytes != null)
                    {
                        return fileBytes;
                    }
                }

                return LoadPlayerPrefs(key);
            }
            finally
            {
                _fileGate.Release();
            }
        }

        public async Task<bool> DeleteAsync(string key)
        {
            await _fileGate.WaitAsync().ConfigureAwait(false);
            try
            {
                bool success = true;
                try
                {
                    DeleteFileIfExists(GetFilePath(key));
                    DeleteFileIfExists(GetTempFilePath(key));
                    DeleteFileIfExists(GetBackupFilePath(key));

                    string legacyPath = GetLegacyFilePath(key);
                    if (!string.Equals(legacyPath, GetFilePath(key), StringComparison.Ordinal))
                    {
                        DeleteFileIfExists(legacyPath);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"FilePersistenceProvider: Failed to delete file data for {key}: {ex}");
                    success = false;
                }

                try
                {
                    PlayerPrefs.DeleteKey(GetPlayerPrefsKey(key));
                    PlayerPrefs.Save();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"FilePersistenceProvider: Failed to delete PlayerPrefs data for {key}: {ex}");
                    success = false;
                }

                return success;
            }
            finally
            {
                _fileGate.Release();
            }
        }

        public bool Exists(string key)
        {
            try
            {
                if (File.Exists(GetFilePath(key)) ||
                    File.Exists(GetTempFilePath(key)) ||
                    File.Exists(GetBackupFilePath(key)) ||
                    File.Exists(GetLegacyFilePath(key)))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"FilePersistenceProvider: Failed to check file existence for {key}: {ex}");
            }

            return PlayerPrefs.HasKey(GetPlayerPrefsKey(key));
        }

        private static async Task<byte[]> TryLoadFileAsync(string filePath, string key)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    return null;
                }

                return await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"FilePersistenceProvider: Failed to load file data for {key}: {ex}");
                return null;
            }
        }

        private static async Task<byte[]> TryRecoverFileAsync(
            string recoveryPath,
            string filePath,
            string key)
        {
            byte[] bytes = await TryLoadFileAsync(recoveryPath, key).ConfigureAwait(false);
            if (bytes == null)
            {
                return null;
            }

            try
            {
                if (!File.Exists(filePath))
                {
                    File.Move(recoveryPath, filePath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"FilePersistenceProvider: Failed to promote recovery data for {key}: {ex}");
            }

            return bytes;
        }

        private static void ReplaceFileWithRecovery(string tempPath, string filePath, string backupPath)
        {
            DeleteFileIfExists(backupPath);
            try
            {
                File.Replace(tempPath, filePath, backupPath, true);
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceFileWithPortableRecovery(tempPath, filePath, backupPath);
            }
            catch (IOException)
            {
                ReplaceFileWithPortableRecovery(tempPath, filePath, backupPath);
            }
        }

        private static void ReplaceFileWithPortableRecovery(string tempPath, string filePath, string backupPath)
        {
            File.Copy(filePath, backupPath, true);
            File.Delete(filePath);
            File.Move(tempPath, filePath);
        }

        private static void DeleteFileIfExists(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        private static bool SavePlayerPrefs(string key, byte[] data)
        {
            try
            {
                PlayerPrefs.SetString(GetPlayerPrefsKey(key), Convert.ToBase64String(data ?? Array.Empty<byte>()));
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"FilePersistenceProvider: PlayerPrefs fallback save failed for {key}: {ex}");
                return false;
            }
        }

        private bool SaveFallbackAndRetireFiles(string key, byte[] data)
        {
            if (!SavePlayerPrefs(key, data)) return false;
            try
            {
                DeleteFileIfExists(GetFilePath(key));
                DeleteFileIfExists(GetTempFilePath(key));
                DeleteFileIfExists(GetBackupFilePath(key));
                return true;
            }
            catch (Exception ex)
            {
                DeletePlayerPrefsIfExists(key);
                Debug.LogError($"FilePersistenceProvider: Could not retire stale file data after fallback save for {key}. {ex}");
                return false;
            }
        }

        private static void DeletePlayerPrefsIfExists(string key)
        {
            try
            {
                string playerPrefsKey = GetPlayerPrefsKey(key);
                if (!PlayerPrefs.HasKey(playerPrefsKey)) return;
                PlayerPrefs.DeleteKey(playerPrefsKey);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"FilePersistenceProvider: Failed to clear stale PlayerPrefs data for {key}: {ex}");
            }
        }

        private static byte[] LoadPlayerPrefs(string key)
        {
            try
            {
                string playerPrefsKey = GetPlayerPrefsKey(key);
                if (!PlayerPrefs.HasKey(playerPrefsKey))
                {
                    return null;
                }

                string encoded = PlayerPrefs.GetString(playerPrefsKey, string.Empty);
                return string.IsNullOrEmpty(encoded) ? Array.Empty<byte>() : Convert.FromBase64String(encoded);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"FilePersistenceProvider: PlayerPrefs fallback load failed for {key}: {ex}");
                return null;
            }
        }
    }
}

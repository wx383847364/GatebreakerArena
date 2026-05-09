using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YooAsset;
using App.Shared.Contracts;
using App.AOT.YooRuntimeAssets.PatchFlow;

namespace App.AOT.YooRuntimeAssets
{
    /// <summary>
    /// YooAssets运行时：初始化、包管理、下载器、版本对齐
    /// </summary>
    public class YooAssetsRuntime : IAssetsRuntime
    {
        private readonly IAppLogger _logger;
        private ResourcePackage _defaultPackage;
        private PatchOperationHandler _patchHandler;
#if UNITY_EDITOR
        private bool _isEditorMode;
#endif

        // CDN地址配置
        private const string DefaultHostServer = "https://your-cdn-url.com/bundles";
        private const string FallbackHostServer = "https://your-cdn-url-backup.com/bundles";

        public YooAssetsRuntime(IAppLogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 初始化YooAssets
        /// </summary>
        public Task InitializeAsync()
        {
#if UNITY_EDITOR
            try
            {
                _logger?.LogInfo("YooAssetsRuntime: 开始初始化YooAssets...");
                _isEditorMode = true;
                _logger?.LogInfo("YooAssetsRuntime: 编辑器模式 - 使用 AssetDatabase 直接加载");
                _logger?.LogInfo("YooAssetsRuntime: YooAssets初始化完成");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.LogError("YooAssetsRuntime: 初始化失败: {0}", ex);
                throw;
            }
#else
            return InitializePlayerAsync();
#endif
        }

#if !UNITY_EDITOR
        private async Task InitializePlayerAsync()
        {
            try
            {
                _logger?.LogInfo("YooAssetsRuntime: 开始初始化YooAssets...");

                // 初始化YooAssets
                YooAssets.Initialize();

                // 创建默认资源包
                _defaultPackage = YooAssets.CreatePackage("DefaultPackage");

                // 设置资源包
                YooAssets.SetDefaultPackage(_defaultPackage);

#if GATEBREAKER_YOO_OFFLINE_PLAYMODE
                // Player smoke 使用内置包离线初始化，避免依赖 CDN。
                var offlineModeParameters = new OfflinePlayModeParameters();
                offlineModeParameters.BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters();
                var initOperation = _defaultPackage.InitializeAsync(offlineModeParameters);
#else
                // 运行时模式：使用主机模式（从CDN下载）
                var hostModeParameters = new HostPlayModeParameters();
                // 内置文件系统参数
                hostModeParameters.BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters();
                // 缓存文件系统参数（需要远程服务）
                hostModeParameters.CacheFileSystemParameters = FileSystemParameters.CreateDefaultCacheFileSystemParameters(new RemoteServices());
                var initOperation = _defaultPackage.InitializeAsync(hostModeParameters);
#endif

                if (initOperation != null)
                {
                    await initOperation.Task;
                    if (initOperation.Status != EOperationStatus.Succeed)
                    {
                        _logger?.LogError("YooAssetsRuntime: 资源包初始化失败: {0}", initOperation.Error);
                        throw new Exception(initOperation.Error);
                    }
                }

#if GATEBREAKER_YOO_OFFLINE_PLAYMODE
                var versionOperation = _defaultPackage.RequestPackageVersionAsync();
                await versionOperation.Task;
                if (versionOperation.Status != EOperationStatus.Succeed)
                {
                    _logger?.LogError("YooAssetsRuntime: 资源包版本请求失败: {0}", versionOperation.Error);
                    throw new Exception(versionOperation.Error);
                }

                var manifestOperation = _defaultPackage.UpdatePackageManifestAsync(versionOperation.PackageVersion);
                await manifestOperation.Task;
                if (manifestOperation.Status != EOperationStatus.Succeed)
                {
                    _logger?.LogError("YooAssetsRuntime: 资源包清单更新失败: {0}", manifestOperation.Error);
                    throw new Exception(manifestOperation.Error);
                }
#endif

                // 初始化补丁处理器
                _patchHandler = new PatchOperationHandler(_defaultPackage, _logger);

                _logger?.LogInfo("YooAssetsRuntime: YooAssets初始化完成");
            }
            catch (Exception ex)
            {
                _logger?.LogError("YooAssetsRuntime: 初始化失败: {0}", ex);
                throw;
            }
        }
#endif

        /// <summary>
        /// 远程资源服务
        /// </summary>
        private class RemoteServices : IRemoteServices
        {
            public string GetRemoteMainURL(string fileName)
            {
                return $"{DefaultHostServer}/{fileName}";
            }

            public string GetRemoteFallbackURL(string fileName)
            {
                return $"{FallbackHostServer}/{fileName}";
            }
        }

        /// <summary>
        /// 执行补丁流程：检查版本、下载、校验
        /// </summary>
        public async Task<bool> RunPatchFlowAsync(string packageVersion = null)
        {
            if (_patchHandler == null)
            {
                _logger?.LogError("YooAssetsRuntime: 补丁处理器未初始化");
                return false;
            }

            return await _patchHandler.RunPatchFlowAsync(packageVersion);
        }

        /// <summary>
        /// 加载资源（IAssetsRuntime 接口实现）
        /// </summary>
        public async Task<IAssetHandle> LoadAssetAsync(string location)
        {
            if (string.IsNullOrEmpty(location))
                return null;

#if UNITY_EDITOR
            if (_isEditorMode)
            {
                // 编辑器模式：使用 AssetDatabase 直接加载
                UnityEngine.Object asset = null;
                foreach (var candidate in GetEditorLoadCandidates(location))
                {
                    asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(candidate);
                    if (asset != null)
                        break;
                }

                if (asset == null)
                {
                    _logger?.LogWarning("YooAssetsRuntime: 编辑器模式加载失败: {0}", location);
                    return null;
                }
                return new EditorAssetHandleWrapper(asset);
            }
#endif
            if (_defaultPackage == null)
                return null;

            var handle = _defaultPackage.LoadAssetAsync<UnityEngine.Object>(location);
            await handle.Task;
            if (handle.Status != EOperationStatus.Succeed)
            {
                _logger?.LogError("YooAssetsRuntime: 加载资源失败 {0}: {1}", location, handle.LastError);
                return null;
            }
            return new AssetHandleWrapper(handle);
        }

        /// <summary>
        /// 加载资源（内部/泛型用）
        /// </summary>
        public AssetHandle LoadAssetAsync<T>(string location) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            if (_isEditorMode)
            {
                // 编辑器模式下返回 null，应使用 LoadAssetAsync(string) 代替
                _logger?.LogWarning("YooAssetsRuntime: 编辑器模式不支持泛型 LoadAssetAsync<T>，请使用 LoadAssetAsync(string)");
                return null;
            }
#endif
            return _defaultPackage?.LoadAssetAsync<T>(location);
        }

        private sealed class AssetHandleWrapper : IAssetHandle
        {
            private readonly AssetHandle _handle;
            public UnityEngine.Object AssetObject => _handle?.AssetObject;
            public AssetHandleWrapper(AssetHandle handle) { _handle = handle; }
            public void Release() => _handle?.Release();
        }

#if UNITY_EDITOR
        /// <summary>
        /// 编辑器模式下的资源句柄包装器
        /// </summary>
        private sealed class EditorAssetHandleWrapper : IAssetHandle
        {
            private readonly UnityEngine.Object _asset;
            public UnityEngine.Object AssetObject => _asset;
            public EditorAssetHandleWrapper(UnityEngine.Object asset) { _asset = asset; }
            public void Release() { /* 编辑器模式下无需释放 */ }
        }
#endif

        /// <summary>
        /// 获取资源包
        /// </summary>
        public ResourcePackage GetPackage(string packageName = null)
        {
            if (string.IsNullOrEmpty(packageName))
            {
                return _defaultPackage;
            }
            return YooAssets.GetPackage(packageName);
        }

        /// <summary>
        /// 关闭
        /// </summary>
        public void Shutdown()
        {
            YooAssets.Destroy();
            _logger?.LogInfo("YooAssetsRuntime: 已关闭");
        }

#if UNITY_EDITOR
        private static IEnumerable<string> GetEditorLoadCandidates(string location)
        {
            var candidates = new List<string>();
            string normalized = location.Replace('\\', '/').Trim();
            string[] extensions = { string.Empty, ".prefab", ".asset", ".mat", ".png", ".jpg", ".bytes" };

            void AddWithExtensions(string basePath)
            {
                if (string.IsNullOrWhiteSpace(basePath))
                    return;

                foreach (var ext in extensions)
                {
                    string candidate = basePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(ext)
                        ? basePath
                        : basePath + ext;
                    candidates.Add(candidate);
                }
            }

            AddWithExtensions(normalized);

            string fileName = Path.GetFileName(normalized);
            if (!string.IsNullOrEmpty(fileName))
            {
                AddWithExtensions($"Assets/HotUpdateContent/Res/Map/{fileName}");
                AddWithExtensions($"Assets/HotUpdateContent/Res/{fileName}");
            }

            string resolvedHotUpdatePath = ResolveHotUpdateTerrainPath(normalized);
            if (!string.IsNullOrEmpty(resolvedHotUpdatePath))
            {
                AddWithExtensions(resolvedHotUpdatePath);
            }

            return candidates
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static string ResolveHotUpdateTerrainPath(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
                return string.Empty;

            string normalized = location.Replace('\\', '/').Trim();
            if (normalized.Contains("://", StringComparison.Ordinal))
                return string.Empty;

            if (normalized.StartsWith("Assets/HotUpdateContent/Res/Map/", StringComparison.OrdinalIgnoreCase))
                return normalized;

            string fileName = Path.GetFileName(normalized);
            if (string.IsNullOrEmpty(fileName))
                return string.Empty;

            return $"Assets/HotUpdateContent/Res/Map/{fileName}";
        }
#endif
    }
}

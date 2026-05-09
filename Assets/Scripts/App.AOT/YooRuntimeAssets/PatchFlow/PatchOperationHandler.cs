using System;
using System.Threading.Tasks;
using YooAsset;
using App.Shared.Contracts;

namespace App.AOT.YooRuntimeAssets.PatchFlow
{
    /// <summary>
    /// 补丁流程处理器：检查版本、下载、校验、切换manifest
    /// </summary>
    public class PatchOperationHandler
    {
        private readonly ResourcePackage _package;
        private readonly IAppLogger _logger;

        public PatchOperationHandler(ResourcePackage package, IAppLogger logger)
        {
            _package = package;
            _logger = logger;
        }

        /// <summary>
        /// 执行完整的补丁流程
        /// </summary>
        public async Task<bool> RunPatchFlowAsync(string packageVersion = null)
        {
            try
            {
                _logger?.LogInfo("PatchOperationHandler: 开始补丁流程...");

                // 1. 检查版本
                var versionInfo = await CheckVersionAsync(packageVersion);
                if (versionInfo == null)
                {
                    _logger?.LogError("PatchOperationHandler: 版本检查失败");
                    return false;
                }

                // 2. 下载manifest
                if (!await DownloadManifestAsync(versionInfo))
                {
                    _logger?.LogError("PatchOperationHandler: Manifest下载失败");
                    return false;
                }

                // 3. 下载资源
                if (!await DownloadAssetsAsync())
                {
                    _logger?.LogError("PatchOperationHandler: 资源下载失败");
                    return false;
                }

                _logger?.LogInfo("PatchOperationHandler: 补丁流程完成");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError("PatchOperationHandler: 补丁流程异常: {0}", ex);
                return false;
            }
        }

        private async Task<PatchVersionInfo> CheckVersionAsync(string packageVersion)
        {
            _logger?.LogInfo("PatchOperationHandler: 检查版本...");

            // TODO: 从服务器获取版本信息
            // 这里先返回一个模拟的版本信息
            await Task.Delay(100); // 模拟网络请求

            return new PatchVersionInfo
            {
                PackageVersion = packageVersion ?? "1.0.0",
                ResourceVersion = "1.0.0",
                CodeVersion = "1.0.0"
            };
        }

        private async Task<bool> DownloadManifestAsync(PatchVersionInfo versionInfo)
        {
            _logger?.LogInfo("PatchOperationHandler: 下载Manifest...");

            try
            {
                // 创建补丁下载器
                var downloader = _package.CreateResourceDownloader(10, 30);
                if (downloader.TotalDownloadCount == 0)
                {
                    _logger?.LogInfo("PatchOperationHandler: 无需下载");
                    return true;
                }

                // 开始下载
                downloader.BeginDownload();
                while (downloader.IsDone == false)
                {
                    await Task.Delay(100);
                    var progress = downloader.Progress;
                    _logger?.LogDebug("PatchOperationHandler: 下载进度 {0}%", (int)(progress * 100));
                }

                if (downloader.Status != EOperationStatus.Succeed)
                {
                    _logger?.LogError("PatchOperationHandler: Manifest下载失败: {0}", downloader.Error);
                    return false;
                }

                _logger?.LogInfo("PatchOperationHandler: Manifest下载完成");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError("PatchOperationHandler: Manifest下载异常: {0}", ex);
                return false;
            }
        }

        private async Task<bool> DownloadAssetsAsync()
        {
            _logger?.LogInfo("PatchOperationHandler: 下载资源...");

            try
            {
                // 创建资源下载器
                var downloader = _package.CreateResourceDownloader(10, 30);
                if (downloader.TotalDownloadCount == 0)
                {
                    _logger?.LogInfo("PatchOperationHandler: 无需下载资源");
                    return true;
                }

                // 开始下载
                downloader.BeginDownload();
                while (downloader.IsDone == false)
                {
                    await Task.Delay(100);
                    var progress = downloader.Progress;
                    _logger?.LogDebug("PatchOperationHandler: 资源下载进度 {0}%", (int)(progress * 100));
                }

                if (downloader.Status != EOperationStatus.Succeed)
                {
                    _logger?.LogError("PatchOperationHandler: 资源下载失败: {0}", downloader.Error);
                    return false;
                }

                _logger?.LogInfo("PatchOperationHandler: 资源下载完成");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError("PatchOperationHandler: 资源下载异常: {0}", ex);
                return false;
            }
        }
    }

    /// <summary>
    /// 补丁版本信息
    /// </summary>
    public class PatchVersionInfo
    {
        public string PackageVersion { get; set; }
        public string ResourceVersion { get; set; }
        public string CodeVersion { get; set; }
    }
}

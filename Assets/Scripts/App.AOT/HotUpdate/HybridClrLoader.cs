using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using HybridCLR;
using App.Shared.Contracts;
using App.AOT.Infrastructure.DI;
using App.AOT.YooRuntimeAssets;

namespace App.AOT.HotUpdate
{
    /// <summary>
    /// HybridCLR加载器：加载AOT metadata + HotUpdate DLL，反射调用入口
    /// </summary>
    public class HybridClrLoader
    {
        private const string HotUpdateDllLocation = "Assets/HotUpdateContent/Res/HotUpdate/App.HotUpdate.dll.bytes";

        private static readonly string[] AOTMetadataLocations =
        {
            "Assets/HotUpdateContent/Res/HotUpdate/Metadata/mscorlib.dll.bytes",
            "Assets/HotUpdateContent/Res/HotUpdate/Metadata/System.dll.bytes",
            "Assets/HotUpdateContent/Res/HotUpdate/Metadata/System.Core.dll.bytes",
            "Assets/HotUpdateContent/Res/HotUpdate/Metadata/UnityEngine.CoreModule.dll.bytes",
            "Assets/HotUpdateContent/Res/HotUpdate/Metadata/UnityEngine.UI.dll.bytes",
            "Assets/HotUpdateContent/Res/HotUpdate/Metadata/UnityEngine.UIModule.dll.bytes",
            "Assets/HotUpdateContent/Res/HotUpdate/Metadata/UnityEngine.TextRenderingModule.dll.bytes",
            "Assets/HotUpdateContent/Res/HotUpdate/Metadata/UnityEngine.JSONSerializeModule.dll.bytes",
            "Assets/HotUpdateContent/Res/HotUpdate/Metadata/UnityEngine.InputLegacyModule.dll.bytes",
            "Assets/HotUpdateContent/Res/HotUpdate/Metadata/Unity.TextMeshPro.dll.bytes",
            "Assets/HotUpdateContent/Res/HotUpdate/Metadata/App.Shared.dll.bytes",
        };

        private readonly IAppLogger _logger;
        private readonly YooAssetsRuntime _yooAssets;
        private readonly IServiceContainer _serviceContainer;
        private Assembly _hotUpdateAssembly;

        public HybridClrLoader(IAppLogger logger, YooAssetsRuntime yooAssets, IServiceContainer serviceContainer)
        {
            _logger = logger;
            _yooAssets = yooAssets;
            _serviceContainer = serviceContainer;
        }

        /// <summary>
        /// 加载HybridCLR热更代码
        /// </summary>
        public async Task LoadAsync()
        {
            try
            {
                _logger?.LogInfo("HybridClrLoader: 开始加载HybridCLR热更代码...");

#if UNITY_EDITOR
                // 编辑器模式：代码已经编译，直接调用入口
                _logger?.LogInfo("HybridClrLoader: 编辑器模式 - 直接调用热更入口");
                await InvokeHotUpdateEntryEditorAsync();
#else
                // 1. 加载AOT元数据补充文件
                await LoadAOTMetadataAsync();

                // 2. 加载HotUpdate DLL
                await LoadHotUpdateDllAsync();

                // 3. 调用热更入口
                await InvokeHotUpdateEntryAsync();
#endif

                _logger?.LogInfo("HybridClrLoader: HybridCLR热更代码加载完成");
            }
            catch (Exception ex)
            {
                _logger?.LogError("HybridClrLoader: 加载失败: {0}", ex);
                throw;
            }
        }

        private async Task LoadAOTMetadataAsync()
        {
            _logger?.LogInfo("HybridClrLoader: 加载AOT元数据...");

            int loadedCount = 0;
            foreach (string location in AOTMetadataLocations)
            {
                IAssetHandle metadataHandle = await _yooAssets.LoadAssetAsync(location);
                try
                {
                    if (!(metadataHandle?.AssetObject is TextAsset metadataAsset) || metadataAsset.bytes == null || metadataAsset.bytes.Length == 0)
                    {
                        _logger?.LogWarning("HybridClrLoader: AOT元数据不存在或为空，跳过。{0}", location);
                        continue;
                    }

                    LoadMetadataForAOTAssembly(metadataAsset.bytes, location);
                    loadedCount++;
                }
                finally
                {
                    metadataHandle?.Release();
                }
            }

            if (loadedCount <= 0)
            {
                throw new InvalidOperationException("HybridClrLoader: 未能加载任何AOT metadata。");
            }

            _logger?.LogInfo("HybridClrLoader: AOT元数据加载完成，count={0}", loadedCount);
        }

        private async Task LoadHotUpdateDllAsync()
        {
            _logger?.LogInfo("HybridClrLoader: 加载HotUpdate DLL...");

            try
            {
                IAssetHandle dllHandle = await _yooAssets.LoadAssetAsync(HotUpdateDllLocation);
                try
                {
                    if (!(dllHandle?.AssetObject is TextAsset dllAsset) || dllAsset.bytes == null || dllAsset.bytes.Length == 0)
                    {
                        throw new FileNotFoundException("加载HotUpdate DLL失败", HotUpdateDllLocation);
                    }

                    _hotUpdateAssembly = Assembly.Load(dllAsset.bytes);
                }
                finally
                {
                    dllHandle?.Release();
                }

                _logger?.LogInfo("HybridClrLoader: HotUpdate DLL加载完成");
            }
            catch (Exception ex)
            {
                _logger?.LogError("HybridClrLoader: 加载HotUpdate DLL失败: {0}", ex);
                throw;
            }
        }

        private void LoadMetadataForAOTAssembly(byte[] metadataBytes, string location)
        {
            LoadImageErrorCode result = RuntimeApi.LoadMetadataForAOTAssembly(metadataBytes, HomologousImageMode.SuperSet);
            _logger?.LogInfo("HybridClrLoader: AOT metadata加载完成。location={0}, result={1}", location, result);
        }

        private async Task InvokeHotUpdateEntryAsync()
        {
            _logger?.LogInfo("HybridClrLoader: 调用热更入口...");

            try
            {
                if (_hotUpdateAssembly == null)
                {
                    throw new Exception("HotUpdate程序集未加载");
                }

                // 查找HotUpdateEntry类
                var entryType = _hotUpdateAssembly.GetType("App.HotUpdate.Entry.HotUpdateEntry");
                if (entryType == null)
                {
                    throw new Exception("未找到HotUpdateEntry类");
                }

                var startMethod = FindHotUpdateEntryStartMethod(entryType);
                if (startMethod == null)
                {
                    throw new Exception("未找到HotUpdateEntry.StartAsync或Start方法");
                }

                await InvokeHotUpdateEntryStartMethodAsync(startMethod);

                _logger?.LogInfo("HybridClrLoader: 热更入口调用成功");
            }
            catch (Exception ex)
            {
                _logger?.LogError("HybridClrLoader: 调用热更入口失败: {0}", ex);
                throw;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// 编辑器模式下直接调用热更入口（代码已编译，无需加载 DLL）
        /// </summary>
        private async Task InvokeHotUpdateEntryEditorAsync()
        {
            _logger?.LogInfo("HybridClrLoader: 编辑器模式调用热更入口...");

            try
            {
                // 在编辑器中，所有程序集都已加载，通过反射查找类型
                var entryType = Type.GetType("App.HotUpdate.Entry.HotUpdateEntry, App.HotUpdate");
                if (entryType == null)
                {
                    // 尝试从所有已加载的程序集中查找
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        entryType = assembly.GetType("App.HotUpdate.Entry.HotUpdateEntry");
                        if (entryType != null) break;
                    }
                }

                if (entryType == null)
                {
                    throw new Exception("未找到HotUpdateEntry类");
                }

                var startMethod = FindHotUpdateEntryStartMethod(entryType);
                if (startMethod == null)
                {
                    throw new Exception("未找到HotUpdateEntry.StartAsync或Start方法");
                }

                await InvokeHotUpdateEntryStartMethodAsync(startMethod);

                _logger?.LogInfo("HybridClrLoader: 编辑器模式热更入口调用成功");
            }
            catch (Exception ex)
            {
                _logger?.LogError("HybridClrLoader: 编辑器模式调用热更入口失败: {0}", ex);
                throw;
            }
        }
#endif

        private MethodInfo FindHotUpdateEntryStartMethod(Type entryType)
        {
            return entryType.GetMethod("StartAsync", BindingFlags.Public | BindingFlags.Static)
                   ?? entryType.GetMethod("Start", BindingFlags.Public | BindingFlags.Static);
        }

        private async Task InvokeHotUpdateEntryStartMethodAsync(MethodInfo startMethod)
        {
            object result = startMethod.Invoke(null, new object[] { _serviceContainer });
            if (result is Task task)
            {
                await task;
            }
        }

        public void Shutdown()
        {
            _hotUpdateAssembly = null;
            _logger?.LogInfo("HybridClrLoader: 已关闭");
        }
    }
}

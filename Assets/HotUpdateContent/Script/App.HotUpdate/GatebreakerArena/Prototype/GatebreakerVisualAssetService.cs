using System;
using System.Threading.Tasks;
using App.HotUpdate.GatebreakerArena.Mode;
using App.Shared.Contracts;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Prototype
{
    public sealed class GatebreakerVisualAssetService
    {
        private readonly IAssetsRuntime _assetsRuntime;
        private readonly IAppLogger _logger;

        public GatebreakerVisualAssetService(IAssetsRuntime assetsRuntime, IAppLogger logger = null)
        {
            _assetsRuntime = assetsRuntime;
            _logger = logger;
        }

        public async Task<GatebreakerVisualAssetSet> LoadAsync(EffectiveMatchRule effectiveRule, BallRuleDefinition ballRule)
        {
            var result = new GatebreakerVisualAssetSet();
            MapRuleDefinition map = effectiveRule?.Map;
            result.Scene = await LoadPrefabAsync(map?.ScenePrefabLocation, "scene");
            result.Paddle = await LoadPrefabAsync(map?.PaddlePrefabLocation, "paddle");
            result.Ball = await LoadPrefabAsync(ballRule?.PrefabLocation, "ball");
            return result;
        }

        private async Task<GatebreakerLoadedPrefab> LoadPrefabAsync(string location, string role)
        {
            if (_assetsRuntime == null)
            {
                _logger?.LogWarning("GatebreakerVisualAssetService: IAssetsRuntime missing for {0} prefab.", role);
                return null;
            }

            if (string.IsNullOrWhiteSpace(location))
            {
                _logger?.LogWarning("GatebreakerVisualAssetService: {0} prefab location is empty.", role);
                return null;
            }

            IAssetHandle handle = null;
            try
            {
                handle = await _assetsRuntime.LoadAssetAsync(location);
                if (!(handle?.AssetObject is GameObject prefab))
                {
                    _logger?.LogWarning(
                        "GatebreakerVisualAssetService: {0} prefab load failed or asset is not a GameObject. location={1}",
                        role,
                        location);
                    handle?.Release();
                    return null;
                }

                return new GatebreakerLoadedPrefab(location, prefab, handle);
            }
            catch (Exception ex)
            {
                handle?.Release();
                _logger?.LogWarning(
                    "GatebreakerVisualAssetService: exception while loading {0} prefab. location={1}, error={2}",
                    role,
                    location,
                    ex.Message);
                return null;
            }
        }
    }

    public sealed class GatebreakerVisualAssetSet : IDisposable
    {
        public GatebreakerLoadedPrefab Scene { get; internal set; }
        public GatebreakerLoadedPrefab Paddle { get; internal set; }
        public GatebreakerLoadedPrefab Ball { get; internal set; }

        public bool IsComplete => Scene != null && Paddle != null && Ball != null;

        public void Dispose()
        {
            Scene?.Dispose();
            Paddle?.Dispose();
            Ball?.Dispose();
            Scene = null;
            Paddle = null;
            Ball = null;
        }
    }

    public sealed class GatebreakerLoadedPrefab : IDisposable
    {
        private readonly IAssetHandle _handle;

        public GatebreakerLoadedPrefab(string location, GameObject prefab, IAssetHandle handle)
        {
            Location = location ?? string.Empty;
            Prefab = prefab;
            _handle = handle;
        }

        public string Location { get; }
        public GameObject Prefab { get; }

        public void Dispose()
        {
            _handle?.Release();
        }
    }
}

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
            result.SetPlayerBall(1, result.Ball);
            for (int playerId = 2; playerId <= 4; playerId++)
            {
                GatebreakerLoadedPrefab playerBall = await LoadOptionalPrefabAsync(
                    ResolveSiblingBallPrefabLocation(ballRule?.PrefabLocation, playerId),
                    $"ball-player-{playerId}");
                result.SetPlayerBall(playerId, playerBall ?? result.Ball);
            }

            return result;
        }

        private Task<GatebreakerLoadedPrefab> LoadOptionalPrefabAsync(string location, string role)
        {
            return string.IsNullOrWhiteSpace(location)
                ? Task.FromResult<GatebreakerLoadedPrefab>(null)
                : LoadPrefabAsync(location, role);
        }

        private static string ResolveSiblingBallPrefabLocation(string baseLocation, int index)
        {
            if (string.IsNullOrWhiteSpace(baseLocation) || index <= 1)
            {
                return baseLocation;
            }

            const string ball01 = "Ball01.prefab";
            int suffixIndex = baseLocation.LastIndexOf(ball01, StringComparison.OrdinalIgnoreCase);
            return suffixIndex >= 0
                ? baseLocation.Substring(0, suffixIndex) + $"Ball{index:00}.prefab"
                : null;
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
        private readonly GatebreakerLoadedPrefab[] _playerBalls = new GatebreakerLoadedPrefab[4];

        public GatebreakerLoadedPrefab Scene { get; internal set; }
        public GatebreakerLoadedPrefab Paddle { get; internal set; }
        public GatebreakerLoadedPrefab Ball { get; internal set; }

        public bool IsComplete => Scene != null && Paddle != null && Ball != null;

        public GatebreakerLoadedPrefab GetBallForPlayerId(int playerId)
        {
            if (playerId > 0 && playerId <= _playerBalls.Length && _playerBalls[playerId - 1] != null)
            {
                return _playerBalls[playerId - 1];
            }

            return Ball;
        }

        internal void SetPlayerBall(int playerId, GatebreakerLoadedPrefab prefab)
        {
            if (playerId <= 0 || playerId > _playerBalls.Length)
            {
                return;
            }

            _playerBalls[playerId - 1] = prefab;
        }

        public void Dispose()
        {
            Scene?.Dispose();
            Paddle?.Dispose();
            for (int i = 0; i < _playerBalls.Length; i++)
            {
                GatebreakerLoadedPrefab playerBall = _playerBalls[i];
                if (playerBall != null && playerBall != Ball)
                {
                    playerBall.Dispose();
                }

                _playerBalls[i] = null;
            }

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

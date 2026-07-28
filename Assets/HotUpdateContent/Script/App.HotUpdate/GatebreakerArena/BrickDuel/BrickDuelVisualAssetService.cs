using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.HotUpdate.GatebreakerArena.Mode;
using App.Shared.Contracts;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.BrickDuel
{
    public sealed class BrickDuelVisualAssetService
    {
        private readonly IAssetsRuntime _assetsRuntime;
        private readonly IAppLogger _logger;

        public BrickDuelVisualAssetService(IAssetsRuntime assetsRuntime, IAppLogger logger = null)
        {
            _assetsRuntime = assetsRuntime;
            _logger = logger;
        }

        public async Task<BrickDuelVisualAssetSet> LoadAsync(BrickDuelRuleDefinition rule)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            var assets = new BrickDuelVisualAssetSet();
            try
            {
                assets.Scene = await LoadRequiredAsync(rule.ScenePrefabLocation, "scene");
                assets.Paddle = await LoadRequiredAsync(rule.PaddlePrefabLocation, "paddle");
                assets.PlayerBall = await LoadRequiredAsync(rule.PlayerBallPrefabLocation, "player-ball");
                assets.AiBall = await LoadRequiredAsync(rule.AiBallPrefabLocation, "ai-ball");
                assets.SetBrick(
                    BrickDuelBrickType.Green,
                    await LoadRequiredAsync(rule.GreenBrickPrefabLocation, "green-brick"));
                assets.SetBrick(
                    BrickDuelBrickType.Red,
                    await LoadRequiredAsync(rule.RedBrickPrefabLocation, "red-brick"));
                assets.SetBrick(
                    BrickDuelBrickType.Yellow,
                    await LoadRequiredAsync(rule.YellowBrickPrefabLocation, "yellow-brick"));
                assets.SetBrick(
                    BrickDuelBrickType.Mystery,
                    await LoadRequiredAsync(rule.MysteryBrickPrefabLocation, "mystery-brick"));
                return assets;
            }
            catch (Exception ex)
            {
                assets.Dispose();
                _logger?.LogWarning("BrickDuel asset load failed atomically: {0}", ex.Message);
                return null;
            }
        }

        private async Task<BrickDuelLoadedPrefab> LoadRequiredAsync(string location, string role)
        {
            if (_assetsRuntime == null)
            {
                throw new InvalidOperationException("IAssetsRuntime is unavailable.");
            }
            if (string.IsNullOrWhiteSpace(location))
            {
                throw new InvalidOperationException($"{role} prefab location is empty.");
            }

            IAssetHandle handle = null;
            try
            {
                handle = await _assetsRuntime.LoadAssetAsync(location);
                if (!(handle?.AssetObject is GameObject prefab))
                {
                    throw new InvalidOperationException($"{role} is not a GameObject: {location}");
                }

                return new BrickDuelLoadedPrefab(location, prefab, handle);
            }
            catch
            {
                handle?.Release();
                throw;
            }
        }
    }

    public sealed class BrickDuelVisualAssetSet : IDisposable
    {
        private readonly Dictionary<BrickDuelBrickType, BrickDuelLoadedPrefab> _bricks =
            new Dictionary<BrickDuelBrickType, BrickDuelLoadedPrefab>();

        public BrickDuelLoadedPrefab Scene { get; internal set; }
        public BrickDuelLoadedPrefab Paddle { get; internal set; }
        public BrickDuelLoadedPrefab PlayerBall { get; internal set; }
        public BrickDuelLoadedPrefab AiBall { get; internal set; }
        public bool IsComplete =>
            Scene != null &&
            Paddle != null &&
            PlayerBall != null &&
            AiBall != null &&
            _bricks.Count == 4;

        public BrickDuelLoadedPrefab GetBrick(BrickDuelBrickType type)
        {
            return _bricks.TryGetValue(type, out BrickDuelLoadedPrefab prefab) ? prefab : null;
        }

        internal void SetBrick(BrickDuelBrickType type, BrickDuelLoadedPrefab prefab)
        {
            _bricks[type] = prefab;
        }

        public void Dispose()
        {
            Scene?.Dispose();
            Paddle?.Dispose();
            PlayerBall?.Dispose();
            AiBall?.Dispose();
            Scene = null;
            Paddle = null;
            PlayerBall = null;
            AiBall = null;
            foreach (BrickDuelLoadedPrefab brick in _bricks.Values)
            {
                brick?.Dispose();
            }
            _bricks.Clear();
        }
    }

    public sealed class BrickDuelLoadedPrefab : IDisposable
    {
        private IAssetHandle _handle;

        public BrickDuelLoadedPrefab(string location, GameObject prefab, IAssetHandle handle)
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
            _handle = null;
        }
    }
}

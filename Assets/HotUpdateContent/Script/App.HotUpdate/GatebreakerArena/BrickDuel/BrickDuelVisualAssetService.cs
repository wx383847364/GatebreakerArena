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

                if (rule.ItemDrops != null && rule.ItemDrops.Count > 0)
                {
                    for (int i = 0; i < rule.ItemDrops.Count; i++)
                    {
                        BrickDuelItemDropDefinition item = rule.ItemDrops[i];
                        if (item == null || string.IsNullOrWhiteSpace(item.ItemId))
                        {
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(item.PrefabLocation))
                        {
                            BrickDuelLoadedPrefab prefab = await LoadOptionalPrefabAsync(
                                item.PrefabLocation,
                                item.ItemId);
                            if (prefab != null)
                            {
                                assets.SetItemPrefab(item.ItemId, prefab);
                            }
                        }

                        BrickDuelLoadedSprite loaded = await LoadOptionalSpriteAsync(
                            item.IconLocation,
                            item.ItemId);
                        if (loaded != null)
                        {
                            assets.SetItemSprite(item.ItemId, loaded);
                        }
                    }
                }

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

        private async Task<BrickDuelLoadedPrefab> LoadOptionalPrefabAsync(string location, string itemId)
        {
            if (_assetsRuntime == null || string.IsNullOrWhiteSpace(location))
            {
                return null;
            }

            IAssetHandle handle = null;
            try
            {
                handle = await _assetsRuntime.LoadAssetAsync(location);
                if (!(handle?.AssetObject is GameObject prefab))
                {
                    handle?.Release();
                    _logger?.LogWarning(
                        "BrickDuel item prefab '{0}' is not a GameObject: {1}",
                        itemId,
                        location);
                    return null;
                }

                return new BrickDuelLoadedPrefab(location, prefab, handle);
            }
            catch (Exception ex)
            {
                handle?.Release();
                _logger?.LogWarning(
                    "BrickDuel item prefab load failed for '{0}': {1}",
                    itemId,
                    ex.Message);
                return null;
            }
        }

        private async Task<BrickDuelLoadedSprite> LoadOptionalSpriteAsync(string location, string itemId)
        {
            if (_assetsRuntime == null || string.IsNullOrWhiteSpace(location))
            {
                return null;
            }

            IAssetHandle handle = null;
            try
            {
                handle = await _assetsRuntime.LoadAssetAsync(location);
                if (handle == null)
                {
                    return null;
                }

                if (handle.AssetObject is Sprite sprite)
                {
                    return new BrickDuelLoadedSprite(location, sprite, handle, ownedTexture: null);
                }

                if (handle.AssetObject is Texture2D texture)
                {
                    Sprite created = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        Mathf.Max(texture.width, texture.height));
                    return new BrickDuelLoadedSprite(location, created, handle, ownedTexture: null);
                }

                handle.Release();
                _logger?.LogWarning(
                    "BrickDuel item icon '{0}' is not a Sprite/Texture2D: {1}",
                    itemId,
                    location);
                return null;
            }
            catch (Exception ex)
            {
                handle?.Release();
                _logger?.LogWarning(
                    "BrickDuel item icon load failed for '{0}': {1}",
                    itemId,
                    ex.Message);
                return null;
            }
        }
    }

    public sealed class BrickDuelVisualAssetSet : IDisposable
    {
        private readonly Dictionary<BrickDuelBrickType, BrickDuelLoadedPrefab> _bricks =
            new Dictionary<BrickDuelBrickType, BrickDuelLoadedPrefab>();
        private readonly Dictionary<string, BrickDuelLoadedSprite> _itemSprites =
            new Dictionary<string, BrickDuelLoadedSprite>(StringComparer.Ordinal);
        private readonly Dictionary<string, BrickDuelLoadedPrefab> _itemPrefabs =
            new Dictionary<string, BrickDuelLoadedPrefab>(StringComparer.Ordinal);

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

        public Sprite GetItemSprite(string itemId)
        {
            return !string.IsNullOrEmpty(itemId) &&
                   _itemSprites.TryGetValue(itemId, out BrickDuelLoadedSprite loaded)
                ? loaded.Sprite
                : null;
        }

        public GameObject GetItemPrefab(string itemId)
        {
            return !string.IsNullOrEmpty(itemId) &&
                   _itemPrefabs.TryGetValue(itemId, out BrickDuelLoadedPrefab loaded)
                ? loaded.Prefab
                : null;
        }

        internal void SetBrick(BrickDuelBrickType type, BrickDuelLoadedPrefab prefab)
        {
            _bricks[type] = prefab;
        }

        internal void SetItemSprite(string itemId, BrickDuelLoadedSprite sprite)
        {
            if (string.IsNullOrEmpty(itemId) || sprite == null)
            {
                return;
            }

            _itemSprites[itemId] = sprite;
        }

        internal void SetItemPrefab(string itemId, BrickDuelLoadedPrefab prefab)
        {
            if (string.IsNullOrEmpty(itemId) || prefab == null)
            {
                return;
            }

            _itemPrefabs[itemId] = prefab;
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
            foreach (BrickDuelLoadedSprite sprite in _itemSprites.Values)
            {
                sprite?.Dispose();
            }
            _itemSprites.Clear();
            foreach (BrickDuelLoadedPrefab prefab in _itemPrefabs.Values)
            {
                prefab?.Dispose();
            }
            _itemPrefabs.Clear();
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

    public sealed class BrickDuelLoadedSprite : IDisposable
    {
        private IAssetHandle _handle;
        private Texture2D _ownedTexture;

        public BrickDuelLoadedSprite(
            string location,
            Sprite sprite,
            IAssetHandle handle,
            Texture2D ownedTexture)
        {
            Location = location ?? string.Empty;
            Sprite = sprite;
            _handle = handle;
            _ownedTexture = ownedTexture;
        }

        public string Location { get; }
        public Sprite Sprite { get; }

        public void Dispose()
        {
            _handle?.Release();
            _handle = null;
            if (_ownedTexture != null)
            {
                UnityEngine.Object.Destroy(_ownedTexture);
                _ownedTexture = null;
            }
        }
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;
using App.HotUpdate.GatebreakerArena.Mode;
using App.HotUpdate.GatebreakerArena.Prototype;
using App.Shared.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace Gatebreaker.Tests
{
    public sealed class GatebreakerVisualAssetServiceTests
    {
        [Test]
        public async Task LoadAsync_LoadsConfiguredPrefabsThroughAssetsRuntime()
        {
            var assetsRuntime = new FakeAssetsRuntime();
            assetsRuntime.Add("scene", new GameObject("ScenePrefab"));
            assetsRuntime.Add("paddle", new GameObject("PaddlePrefab"));
            assetsRuntime.Add("ball", new GameObject("BallPrefab"));
            var service = new GatebreakerVisualAssetService(assetsRuntime);

            GatebreakerVisualAssetSet set = await service.LoadAsync(CreateEffectiveRule(), CreateBallRule());

            Assert.IsTrue(set.IsComplete);
            CollectionAssert.AreEqual(new[] { "scene", "paddle", "ball" }, assetsRuntime.LoadedLocations);
            Assert.AreEqual("scene", set.Scene.Location);
            Assert.AreEqual("paddle", set.Paddle.Location);
            Assert.AreEqual("ball", set.Ball.Location);

            set.Dispose();

            Assert.IsTrue(assetsRuntime.Handles["scene"].Released);
            Assert.IsTrue(assetsRuntime.Handles["paddle"].Released);
            Assert.IsTrue(assetsRuntime.Handles["ball"].Released);

            Object.DestroyImmediate(assetsRuntime.Handles["scene"].AssetObject);
            Object.DestroyImmediate(assetsRuntime.Handles["paddle"].AssetObject);
            Object.DestroyImmediate(assetsRuntime.Handles["ball"].AssetObject);
        }

        [Test]
        public async Task LoadAsync_ReturnsIncompleteSetWhenAssetIsMissing()
        {
            var assetsRuntime = new FakeAssetsRuntime();
            assetsRuntime.Add("scene", new GameObject("ScenePrefab"));
            assetsRuntime.Add("ball", new GameObject("BallPrefab"));
            var service = new GatebreakerVisualAssetService(assetsRuntime);

            GatebreakerVisualAssetSet set = await service.LoadAsync(CreateEffectiveRule(), CreateBallRule());

            Assert.IsFalse(set.IsComplete);
            Assert.IsNotNull(set.Scene);
            Assert.IsNull(set.Paddle);
            Assert.IsNotNull(set.Ball);

            set.Dispose();
            Object.DestroyImmediate(assetsRuntime.Handles["scene"].AssetObject);
            Object.DestroyImmediate(assetsRuntime.Handles["ball"].AssetObject);
        }

        [Test]
        public async Task LoadAsync_ReleasesNonGameObjectAssetImmediately()
        {
            var assetsRuntime = new FakeAssetsRuntime();
            assetsRuntime.Add("scene", new TextAsset("not a prefab"));
            var service = new GatebreakerVisualAssetService(assetsRuntime);

            GatebreakerVisualAssetSet set = await service.LoadAsync(CreateEffectiveRule(), CreateBallRule());

            Assert.IsFalse(set.IsComplete);
            Assert.IsNull(set.Scene);
            Assert.IsTrue(assetsRuntime.Handles["scene"].Released);
        }

        private static EffectiveMatchRule CreateEffectiveRule()
        {
            var mode = new ModeRuleDefinition();
            var map = new MapRuleDefinition
            {
                ScenePrefabLocation = "scene",
                PaddlePrefabLocation = "paddle",
            };
            return new EffectiveMatchRule(mode, map, 1, 4, 6f);
        }

        private static BallRuleDefinition CreateBallRule()
        {
            return new BallRuleDefinition
            {
                PrefabLocation = "ball",
            };
        }

        private sealed class FakeAssetsRuntime : IAssetsRuntime
        {
            private readonly Dictionary<string, FakeAssetHandle> _handles = new Dictionary<string, FakeAssetHandle>();

            public IReadOnlyList<string> LoadedLocations => _loadedLocations;
            public IReadOnlyDictionary<string, FakeAssetHandle> Handles => _handles;

            private readonly List<string> _loadedLocations = new List<string>();

            public void Add(string location, Object asset)
            {
                _handles[location] = new FakeAssetHandle(asset);
            }

            public Task InitializeAsync()
            {
                return Task.CompletedTask;
            }

            public Task<bool> RunPatchFlowAsync(string packageVersion = null)
            {
                return Task.FromResult(true);
            }

            public Task<IAssetHandle> LoadAssetAsync(string location)
            {
                _loadedLocations.Add(location);
                _handles.TryGetValue(location, out FakeAssetHandle handle);
                return Task.FromResult<IAssetHandle>(handle);
            }

            public void Shutdown()
            {
            }
        }

        private sealed class FakeAssetHandle : IAssetHandle
        {
            public FakeAssetHandle(Object assetObject)
            {
                AssetObject = assetObject;
            }

            public Object AssetObject { get; }
            public bool Released { get; private set; }

            public void Release()
            {
                Released = true;
            }
        }
    }
}

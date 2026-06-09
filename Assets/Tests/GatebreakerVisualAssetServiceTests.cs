using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.HotUpdate.GatebreakerArena.Mode;
using App.HotUpdate.GatebreakerArena.Prototype;
using App.Shared.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gatebreaker.Tests
{
    public sealed class GatebreakerVisualAssetServiceTests
    {
        [UnityTest]
        public IEnumerator LoadAsync_LoadsConfiguredPrefabsThroughAssetsRuntime()
        {
            var assetsRuntime = new FakeAssetsRuntime();
            assetsRuntime.Add("scene", new GameObject("ScenePrefab"));
            assetsRuntime.Add("paddle", new GameObject("PaddlePrefab"));
            assetsRuntime.Add("ball", new GameObject("BallPrefab"));
            var service = new GatebreakerVisualAssetService(assetsRuntime);

            Task<GatebreakerVisualAssetSet> loadTask = service.LoadAsync(CreateEffectiveRule(), CreateBallRule());
            yield return WaitForTask(loadTask);
            GatebreakerVisualAssetSet set = loadTask.Result;

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

        [UnityTest]
        public IEnumerator LoadAsync_LoadsSiblingPlayerBallPrefabsWhenUsingBall01()
        {
            var assetsRuntime = new FakeAssetsRuntime();
            string ball01 = "Assets/HotUpdateContent/Res/prefabs/Ball01.prefab";
            string ball02 = "Assets/HotUpdateContent/Res/prefabs/Ball02.prefab";
            string ball03 = "Assets/HotUpdateContent/Res/prefabs/Ball03.prefab";
            string ball04 = "Assets/HotUpdateContent/Res/prefabs/Ball04.prefab";
            assetsRuntime.Add("scene", new GameObject("ScenePrefab"));
            assetsRuntime.Add("paddle", new GameObject("PaddlePrefab"));
            assetsRuntime.Add(ball01, new GameObject("Ball01Prefab"));
            assetsRuntime.Add(ball02, new GameObject("Ball02Prefab"));
            assetsRuntime.Add(ball03, new GameObject("Ball03Prefab"));
            assetsRuntime.Add(ball04, new GameObject("Ball04Prefab"));
            var service = new GatebreakerVisualAssetService(assetsRuntime);

            Task<GatebreakerVisualAssetSet> loadTask = service.LoadAsync(CreateEffectiveRule(), new BallRuleDefinition
            {
                PrefabLocation = ball01,
            });
            yield return WaitForTask(loadTask);
            GatebreakerVisualAssetSet set = loadTask.Result;

            CollectionAssert.AreEqual(new[] { "scene", "paddle", ball01, ball02, ball03, ball04 }, assetsRuntime.LoadedLocations);
            Assert.AreEqual(ball01, set.GetBallForPlayerId(1).Location);
            Assert.AreEqual(ball02, set.GetBallForPlayerId(2).Location);
            Assert.AreEqual(ball03, set.GetBallForPlayerId(3).Location);
            Assert.AreEqual(ball04, set.GetBallForPlayerId(4).Location);

            set.Dispose();

            Assert.IsTrue(assetsRuntime.Handles[ball01].Released);
            Assert.IsTrue(assetsRuntime.Handles[ball02].Released);
            Assert.IsTrue(assetsRuntime.Handles[ball03].Released);
            Assert.IsTrue(assetsRuntime.Handles[ball04].Released);
            Object.DestroyImmediate(assetsRuntime.Handles["scene"].AssetObject);
            Object.DestroyImmediate(assetsRuntime.Handles["paddle"].AssetObject);
            Object.DestroyImmediate(assetsRuntime.Handles[ball01].AssetObject);
            Object.DestroyImmediate(assetsRuntime.Handles[ball02].AssetObject);
            Object.DestroyImmediate(assetsRuntime.Handles[ball03].AssetObject);
            Object.DestroyImmediate(assetsRuntime.Handles[ball04].AssetObject);
        }

        [Test]
        public void ResolveScenePrefabLocation_UsesPlayerCountSpecificScenePrefab()
        {
            Assert.AreEqual(
                "Assets/HotUpdateContent/Res/prefabs/Scene2P.prefab",
                GatebreakerVisualAssetService.ResolveScenePrefabLocation("scene", 2));
            Assert.AreEqual(
                "Assets/HotUpdateContent/Res/prefabs/Scene3P.prefab",
                GatebreakerVisualAssetService.ResolveScenePrefabLocation("scene", 3));
            Assert.AreEqual(
                "Assets/HotUpdateContent/Res/prefabs/Scene4P.prefab",
                GatebreakerVisualAssetService.ResolveScenePrefabLocation("scene", 4));
            Assert.AreEqual("scene", GatebreakerVisualAssetService.ResolveScenePrefabLocation("scene", 0));
        }

        [UnityTest]
        public IEnumerator LoadAsync_ReturnsIncompleteSetWhenAssetIsMissing()
        {
            var assetsRuntime = new FakeAssetsRuntime();
            assetsRuntime.Add("scene", new GameObject("ScenePrefab"));
            assetsRuntime.Add("ball", new GameObject("BallPrefab"));
            var service = new GatebreakerVisualAssetService(assetsRuntime);

            Task<GatebreakerVisualAssetSet> loadTask = service.LoadAsync(CreateEffectiveRule(), CreateBallRule());
            yield return WaitForTask(loadTask);
            GatebreakerVisualAssetSet set = loadTask.Result;

            Assert.IsFalse(set.IsComplete);
            Assert.IsNotNull(set.Scene);
            Assert.IsNull(set.Paddle);
            Assert.IsNotNull(set.Ball);

            set.Dispose();
            Object.DestroyImmediate(assetsRuntime.Handles["scene"].AssetObject);
            Object.DestroyImmediate(assetsRuntime.Handles["ball"].AssetObject);
        }

        [UnityTest]
        public IEnumerator LoadAsync_ReleasesNonGameObjectAssetImmediately()
        {
            var assetsRuntime = new FakeAssetsRuntime();
            assetsRuntime.Add("scene", new TextAsset("not a prefab"));
            var service = new GatebreakerVisualAssetService(assetsRuntime);

            Task<GatebreakerVisualAssetSet> loadTask = service.LoadAsync(CreateEffectiveRule(), CreateBallRule());
            yield return WaitForTask(loadTask);
            GatebreakerVisualAssetSet set = loadTask.Result;

            Assert.IsFalse(set.IsComplete);
            Assert.IsNull(set.Scene);
            Assert.IsTrue(assetsRuntime.Handles["scene"].Released);
        }

        private static IEnumerator WaitForTask(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                throw task.Exception;
            }

            if (task.IsCanceled)
            {
                Assert.Fail("Task was canceled.");
            }
        }

        private static EffectiveMatchRule CreateEffectiveRule()
        {
            var mode = new ModeRuleDefinition();
            var map = new MapRuleDefinition
            {
                ScenePrefabLocation = "scene",
                PaddlePrefabLocation = "paddle",
            };
            return new EffectiveMatchRule(mode, map, 1, 4, 1, 5, 5, 5f);
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

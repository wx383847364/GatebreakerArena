using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.HotUpdate.GatebreakerArena.BrickDuel;
using App.HotUpdate.GatebreakerArena.Mode;
using App.Shared.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gatebreaker.Tests
{
    public sealed class BrickDuelVisualAssetServiceTests
    {
        private static readonly string[] Locations =
        {
            "scene",
            "paddle",
            "player-ball",
            "ai-ball",
            "green",
            "red",
            "yellow",
            "mystery",
        };

        [UnityTest]
        public IEnumerator LoadAsync_LoadsAndReleasesEveryRequiredPrefab()
        {
            var runtime = CreateRuntime();
            var service = new BrickDuelVisualAssetService(runtime);

            Task<BrickDuelVisualAssetSet> task = service.LoadAsync(CreateRule());
            yield return WaitForTask(task);

            BrickDuelVisualAssetSet assets = task.Result;
            Assert.IsNotNull(assets);
            Assert.IsTrue(assets.IsComplete);
            CollectionAssert.AreEqual(Locations, runtime.LoadedLocations);
            Assert.AreEqual("green", assets.GetBrick(BrickDuelBrickType.Green).Location);
            Assert.AreEqual("red", assets.GetBrick(BrickDuelBrickType.Red).Location);
            Assert.AreEqual("yellow", assets.GetBrick(BrickDuelBrickType.Yellow).Location);
            Assert.AreEqual("mystery", assets.GetBrick(BrickDuelBrickType.Mystery).Location);

            assets.Dispose();
            for (int i = 0; i < Locations.Length; i++)
            {
                Assert.IsTrue(runtime.Handles[Locations[i]].Released, Locations[i]);
            }

            runtime.DestroyAssets();
        }

        [UnityTest]
        public IEnumerator LoadAsync_WhenAnyRequiredPrefabIsMissing_FailsAtomically()
        {
            for (int missingIndex = 0; missingIndex < Locations.Length; missingIndex++)
            {
                var runtime = CreateRuntime(missingIndex);
                var service = new BrickDuelVisualAssetService(runtime);

                Task<BrickDuelVisualAssetSet> task = service.LoadAsync(CreateRule());
                yield return WaitForTask(task);

                Assert.IsNull(task.Result, $"missing asset index {missingIndex}");
                Assert.AreEqual(missingIndex + 1, runtime.LoadedLocations.Count);
                for (int loadedIndex = 0; loadedIndex < missingIndex; loadedIndex++)
                {
                    string location = Locations[loadedIndex];
                    Assert.IsTrue(runtime.Handles[location].Released, location);
                }

                runtime.DestroyAssets();
            }
        }

        [UnityTest]
        public IEnumerator SessionStart_WhenStoppedDuringLoad_DiscardsContinuationAndReleasesHandles()
        {
            FakeAssetsRuntime runtime = CreateRuntime();
            runtime.DelayNextLoad();
            var session = new BrickDuelSessionController(
                new BrickDuelVisualAssetService(runtime));

            Task<bool> task = session.StartAsync(
                CreateRule(),
                new BrickDuelAiRuleDefinition
                {
                    RuleId = "BRICK_DUEL_AI_TACTICAL",
                    DecisionIntervalFrames = 1,
                    EmergencyDistance = 0.92f,
                    MoveDeadZone = 0.04f,
                });
            Assert.IsFalse(task.IsCompleted);

            session.Stop();
            runtime.CompleteDelayedLoad();
            yield return WaitForTask(task);

            Assert.IsFalse(task.Result);
            Assert.IsFalse(session.IsActive);
            for (int i = 0; i < Locations.Length; i++)
            {
                Assert.IsTrue(runtime.Handles[Locations[i]].Released, Locations[i]);
            }

            session.Dispose();
            runtime.DestroyAssets();
        }

        private static BrickDuelRuleDefinition CreateRule()
        {
            return new BrickDuelRuleDefinition
            {
                ScenePrefabLocation = Locations[0],
                PaddlePrefabLocation = Locations[1],
                PlayerBallPrefabLocation = Locations[2],
                AiBallPrefabLocation = Locations[3],
                GreenBrickPrefabLocation = Locations[4],
                RedBrickPrefabLocation = Locations[5],
                YellowBrickPrefabLocation = Locations[6],
                MysteryBrickPrefabLocation = Locations[7],
            };
        }

        private static FakeAssetsRuntime CreateRuntime(int missingIndex = -1)
        {
            var runtime = new FakeAssetsRuntime();
            for (int i = 0; i < Locations.Length; i++)
            {
                if (i != missingIndex)
                {
                    runtime.Add(Locations[i], new GameObject(Locations[i]));
                }
            }

            return runtime;
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

        private sealed class FakeAssetsRuntime : IAssetsRuntime
        {
            private readonly Dictionary<string, FakeAssetHandle> _handles =
                new Dictionary<string, FakeAssetHandle>();
            private readonly List<string> _loadedLocations = new List<string>();
            private TaskCompletionSource<IAssetHandle> _delayedLoad;
            private IAssetHandle _delayedHandle;

            public IReadOnlyDictionary<string, FakeAssetHandle> Handles => _handles;
            public IReadOnlyList<string> LoadedLocations => _loadedLocations;

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
                if (_delayedLoad != null && _delayedHandle == null)
                {
                    _delayedHandle = handle;
                    return _delayedLoad.Task;
                }
                return Task.FromResult<IAssetHandle>(handle);
            }

            public void DelayNextLoad()
            {
                _delayedLoad = new TaskCompletionSource<IAssetHandle>();
                _delayedHandle = null;
            }

            public void CompleteDelayedLoad()
            {
                TaskCompletionSource<IAssetHandle> delayedLoad = _delayedLoad;
                IAssetHandle delayedHandle = _delayedHandle;
                _delayedLoad = null;
                _delayedHandle = null;
                delayedLoad.SetResult(delayedHandle);
            }

            public void Shutdown()
            {
            }

            public void DestroyAssets()
            {
                foreach (FakeAssetHandle handle in _handles.Values)
                {
                    Object.DestroyImmediate(handle.AssetObject);
                }
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

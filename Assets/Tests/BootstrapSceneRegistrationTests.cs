using System.Linq;
using App.AOT.Bootstrap;
using App.HotUpdate.GatebreakerArena.Match;
using App.HotUpdate.GatebreakerArena.Network;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gatebreaker.Tests
{
    public sealed class BootstrapSceneRegistrationTests
    {
        private const string BootstrapScenePath = "Assets/Scenes/BootstrapScene.scene";

        [Test]
        public void BootstrapSceneIsPresentAndEnabledInBuildSettings()
        {
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath),
                "BootstrapScene asset should exist at the path used by the playmode smoke test.");

            bool isEnabled = EditorBuildSettings.scenes.Any(
                scene => scene.enabled && scene.path == BootstrapScenePath);

            Assert.IsTrue(
                isEnabled,
                "BootstrapScene should be enabled in EditorBuildSettings so PlayMode smoke can load it by name.");
        }

        [Test]
        public void BootstrapSceneGatebreakerUiBindingHasStaticCoreReferences()
        {
            var scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            GatebreakerArenaSceneUiBinding binding = Resources
                .FindObjectsOfTypeAll<GatebreakerArenaSceneUiBinding>()
                .FirstOrDefault(item => item != null && item.gameObject.scene == scene);

            Assert.IsNotNull(binding, "BootstrapScene should contain the Gatebreaker scene UI binding bridge.");
            Assert.IsTrue(
                binding.HasStaticCoreBindings,
                "BootstrapScene should serialize the stable core UI references; the generated v0.3 phase panel is completed during Awake.");
        }

        [Test]
        public void RuntimeFramePolicyUsesSixtyDisplayAndThirtyLogicFps()
        {
            Assert.AreEqual(60, RuntimeFrameRateSettings.MaxDisplayFps);
            Assert.AreEqual(30, GatebreakerMatchStartConfig.DefaultSimulationFps);
            Assert.AreEqual(GatebreakerMatchStartConfig.DefaultSimulationFps, LockstepSession.SimulationFps);
        }
    }
}

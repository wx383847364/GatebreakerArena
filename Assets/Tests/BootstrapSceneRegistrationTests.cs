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
        public void BootstrapSceneGatebreakerUiBindingHasStaticReferences()
        {
            var scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            GatebreakerArenaSceneUiBinding binding = Resources
                .FindObjectsOfTypeAll<GatebreakerArenaSceneUiBinding>()
                .FirstOrDefault(item => item != null && item.gameObject.scene == scene);

            Assert.IsNotNull(binding, "BootstrapScene should contain the Gatebreaker scene UI binding bridge.");
            Assert.IsTrue(
                binding.HasRequiredBindings,
                "Gatebreaker scene UI binding should include Skill/BallCount and player Score/Hit panel references.");
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

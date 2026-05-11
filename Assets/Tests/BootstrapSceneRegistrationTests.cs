using System.Linq;
using NUnit.Framework;
using UnityEditor;

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
    }
}

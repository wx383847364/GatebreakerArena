using App.HotUpdate.GatebreakerArena.UI;
using Gatebreaker.Editor;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Gatebreaker.Tests.Editor
{
    public sealed class GatebreakerFontRoleToolTests
    {
        [Test]
        public void ClassifyRole_UsesExpectedGatebreakerRules()
        {
            Assert.That(
                GatebreakerFontRoleToolLogic.ClassifyRole("Hud/Title/BattleMode", "BATTLE MODE", 32f),
                Is.EqualTo(GatebreakerFontRole.ArcadeTitle));

            Assert.That(
                GatebreakerFontRoleToolLogic.ClassifyRole("Hud/StatusText", "阶段：进行中", 24f),
                Is.EqualTo(GatebreakerFontRole.PixelChinese));

            Assert.That(
                GatebreakerFontRoleToolLogic.ClassifyRole("TopPanel/Score", "12,345", 18f),
                Is.EqualTo(GatebreakerFontRole.PixelBody));

            Assert.That(
                GatebreakerFontRoleToolLogic.ClassifyRole("PreviewAlt/Title", "BATTLE MODE", 24f),
                Is.EqualTo(GatebreakerFontRole.PixelAlt));
        }

        [Test]
        public void ResolveRole_ManualOverrideWinsOverAutomaticRules()
        {
            GatebreakerFontRoleProfile profile = ScriptableObject.CreateInstance<GatebreakerFontRoleProfile>();
            try
            {
                profile.EnsureDefaultEntries();
                profile.SetManualOverride(
                    "Assets/Fake.prefab",
                    "Hud/TitleText",
                    nameof(TextMeshProUGUI),
                    GatebreakerFontRole.PixelAlt);

                GatebreakerFontRole role = GatebreakerFontRoleToolLogic.ResolveRole(
                    profile,
                    "Assets/Fake.prefab",
                    "Hud/TitleText",
                    nameof(TextMeshProUGUI),
                    "TitleText",
                    "BATTLE MODE",
                    42f,
                    out string source);

                Assert.That(role, Is.EqualTo(GatebreakerFontRole.PixelAlt));
                Assert.That(source, Is.EqualTo("Manual"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void GetRoleAssetWarning_ReportsMissingTmpFont()
        {
            GatebreakerFontRoleProfile profile = ScriptableObject.CreateInstance<GatebreakerFontRoleProfile>();
            try
            {
                profile.EnsureDefaultEntries();

                string warning = GatebreakerFontRoleToolLogic.GetRoleAssetWarning(
                    profile,
                    nameof(TextMeshProUGUI),
                    GatebreakerFontRole.ArcadeTitle);

                Assert.That(warning, Is.EqualTo("TMP font missing"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ScanUiTexts_ReturnsBootstrapSceneTextComponents()
        {
            GatebreakerFontRoleProfile profile = GatebreakerFontRoleToolLogic.LoadOrCreateDefaultProfile();

            System.Collections.Generic.List<GatebreakerFontRoleScanItem> items =
                GatebreakerFontRoleToolLogic.ScanUiTexts(profile);

            Assert.That(items.Count, Is.GreaterThan(0));
            Assert.That(items.Exists(item => item.TextPreview.Contains("BATTLE MODE")), Is.True);
        }
    }
}

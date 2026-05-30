using App.HotUpdate.GatebreakerArena.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;

namespace Gatebreaker.Tests
{
    public sealed class GatebreakerFontResolverTests
    {
        private const string FusionTmpPath = "Assets/HotUpdateContent/Res/fonts/tmp/Gatebreaker_PixelChinese_FusionPixel12_TMP.asset";
        private const string RequiredChineseSample = "比分阶段弹药比赛结束房间号";

        [Test]
        public void FusionPixelTmpFontSupportsRequiredChineseHudText()
        {
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FusionTmpPath);

            Assert.IsNotNull(
                fontAsset,
                "Run Gatebreaker/Fonts/Generate TMP Font Assets before validating Gatebreaker UI fonts.");
            Assert.IsTrue(
                GatebreakerRuntimeTmpFontResolver.SupportsText(fontAsset, RequiredChineseSample),
                "Fusion Pixel TMP font must cover required Chinese HUD text.");
        }

        [Test]
        public void ResolverFallsBackToFusionPixelForChineseText()
        {
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FusionTmpPath);

            Assert.IsNotNull(
                fontAsset,
                "Run Gatebreaker/Fonts/Generate TMP Font Assets before validating Gatebreaker UI fonts.");
            GatebreakerFontRuntimeSettings settings = AssetDatabase.LoadAssetAtPath<GatebreakerFontRuntimeSettings>(
                GatebreakerFontRuntimeSettings.DefaultAssetPath);
            GatebreakerRuntimeTmpFontResolver.SetRuntimeSettings(settings);

            TMP_FontAsset resolved = GatebreakerRuntimeTmpFontResolver.ResolveFontAsset(null, RequiredChineseSample);

            Assert.AreSame(fontAsset, resolved);
        }
    }
}

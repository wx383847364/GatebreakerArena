using App.HotUpdate.GatebreakerArena.UI;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Gatebreaker.Editor
{
    public static class GatebreakerFontAssetGenerator
    {
        private const string FontRoot = "Assets/HotUpdateContent/Res/fonts";
        private const string SourceRoot = FontRoot + "/source";
        private const string TmpRoot = FontRoot + "/tmp";
        private const string PreviewRoot = FontRoot + "/preview";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        private const string FusionSourcePath = SourceRoot + "/FusionPixel12-Proportional-zh_hans.otf";
        private const string PressStartSourcePath = SourceRoot + "/PressStart2P-Regular.ttf";
        private const string PixelifySourcePath = SourceRoot + "/PixelifySans-wght.ttf";
        private const string DotGothicSourcePath = SourceRoot + "/DotGothic16-Regular.ttf";

        public const string FusionTmpPath = TmpRoot + "/Gatebreaker_PixelChinese_FusionPixel12_TMP.asset";
        public const string PressStartTmpPath = TmpRoot + "/Gatebreaker_ArcadeTitle_PressStart2P_TMP.asset";
        public const string PixelifyTmpPath = TmpRoot + "/Gatebreaker_PixelBody_PixelifySans_TMP.asset";
        public const string DotGothicTmpPath = TmpRoot + "/Gatebreaker_PixelAlt_DotGothic16_TMP.asset";

        private const string ProfilePath = FontRoot + "/GatebreakerFontRoleProfile.asset";
        private const string RuntimeSettingsPath = FontRoot + "/GatebreakerFontRuntimeSettings.asset";
        private const string PreviewPrefabPath = PreviewRoot + "/GatebreakerFontPreview.prefab";

        private const string ChineseWarmupText =
            "比分阶段弹药比赛结束房间号状态人数本机房间加入大厅加载对战空闲命中位置影响板速影响最小离板分量";

        [MenuItem("Gatebreaker/Fonts/Generate TMP Font Assets")]
        public static void GenerateAll()
        {
            AssetDatabase.Refresh();
            GatebreakerFontRoleProfile profile = GatebreakerFontRoleToolLogic.LoadOrCreateDefaultProfile();
            GatebreakerFontRoleToolLogic.AssignRecommendedDefaults(profile, overwriteExisting: true);
            GatebreakerFontRoleToolLogic.GenerateOrRefreshTmpAssets(profile);
            GatebreakerFontRoleToolLogic.CreateOrRefreshRuntimeSettings(profile);
            GatebreakerFontRoleToolLogic.ConfigureTmpSettingsFallback(profile);

            TMP_FontAsset fusion = profile.GetTmpFont(GatebreakerFontRole.PixelChinese);
            TMP_FontAsset pressStart = profile.GetTmpFont(GatebreakerFontRole.ArcadeTitle);
            TMP_FontAsset pixelify = profile.GetTmpFont(GatebreakerFontRole.PixelBody);
            TMP_FontAsset dotGothic = profile.GetTmpFont(GatebreakerFontRole.PixelAlt);
            CreatePreviewPrefab(fusion, pressStart, pixelify, dotGothic);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Gatebreaker font assets generated.");
        }

        public static void GenerateAllFromCommandLine()
        {
            GenerateAll();
        }

        private static void EnsureDirectories()
        {
            EnsureDirectory(FontRoot);
            EnsureDirectory(TmpRoot);
            EnsureDirectory(PreviewRoot);
        }

        private static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folder = System.IO.Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folder))
            {
                return;
            }

            EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }

        private static TMP_FontAsset CreateFontAsset(
            string sourcePath,
            string assetPath,
            string assetName,
            int atlasSize,
            string warmupText)
        {
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (sourceFont == null)
            {
                throw new System.InvalidOperationException($"Missing source font: {sourcePath}");
            }

            AssetDatabase.DeleteAsset(assetPath);
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,
                9,
                GlyphRenderMode.SDFAA,
                atlasSize,
                atlasSize,
                AtlasPopulationMode.Dynamic,
                true);

            if (fontAsset == null)
            {
                throw new System.InvalidOperationException($"Failed to create TMP font asset from {sourcePath}");
            }

            fontAsset.name = assetName;
            fontAsset.TryAddCharacters(warmupText, true);
            AssetDatabase.CreateAsset(fontAsset, assetPath);
            EditorUtility.SetDirty(fontAsset);
            return fontAsset;
        }

        private static void ConfigureFallbacks(
            TMP_FontAsset fusion,
            TMP_FontAsset pressStart,
            TMP_FontAsset pixelify,
            TMP_FontAsset dotGothic)
        {
            AddFallback(pressStart, fusion);
            AddFallback(pixelify, fusion);
            AddFallback(dotGothic, fusion);
        }

        private static void AddFallback(TMP_FontAsset fontAsset, TMP_FontAsset fallback)
        {
            if (fontAsset == null || fallback == null || ReferenceEquals(fontAsset, fallback))
            {
                return;
            }

            if (fontAsset.fallbackFontAssetTable == null)
            {
                fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();
            }

            fontAsset.fallbackFontAssetTable.Clear();
            fontAsset.fallbackFontAssetTable.Add(fallback);
            EditorUtility.SetDirty(fontAsset);
        }

        private static void ConfigureRoleAssets(
            TMP_FontAsset fusion,
            TMP_FontAsset pressStart,
            TMP_FontAsset pixelify,
            TMP_FontAsset dotGothic)
        {
            GatebreakerFontRoleProfile profile = AssetDatabase.LoadAssetAtPath<GatebreakerFontRoleProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<GatebreakerFontRoleProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            profile.ConfigureRole(GatebreakerFontRole.ArcadeTitle, LoadFont(PressStartSourcePath), pressStart, "8bit 英文大标题，例如 BATTLE MODE、GAME OVER。");
            profile.ConfigureRole(GatebreakerFontRole.PixelChinese, LoadFont(FusionSourcePath), fusion, "中文 HUD、按钮、提示和常规 UI 文本。");
            profile.ConfigureRole(GatebreakerFontRole.PixelBody, LoadFont(PixelifySourcePath), pixelify, "英文玩家名、状态、数字和正文备选。");
            profile.ConfigureRole(GatebreakerFontRole.PixelAlt, LoadFont(DotGothicSourcePath), dotGothic, "日式像素风备选标题或风格预览。");
            profile.ConfigureRole(GatebreakerFontRole.Fallback, LoadFont(FusionSourcePath), fusion, "中文兜底字体，用于 TMP 缺字回退。");
            EditorUtility.SetDirty(profile);

            GatebreakerFontRuntimeSettings settings = AssetDatabase.LoadAssetAtPath<GatebreakerFontRuntimeSettings>(RuntimeSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<GatebreakerFontRuntimeSettings>();
                AssetDatabase.CreateAsset(settings, RuntimeSettingsPath);
            }

            settings.Configure(profile, fusion, LoadFont(FusionSourcePath));
            EditorUtility.SetDirty(settings);
        }

        private static Font LoadFont(string sourcePath)
        {
            return AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        }

        private static void ConfigureTmpSettingsFallback(TMP_FontAsset fallbackFont)
        {
            if (fallbackFont == null)
            {
                return;
            }

            TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings == null)
            {
                Debug.LogWarning($"Gatebreaker font generator: TMP settings not found at {TmpSettingsPath}");
                return;
            }

            var serializedSettings = new SerializedObject(settings);
            SerializedProperty fallbacks = serializedSettings.FindProperty("m_fallbackFontAssets");
            if (fallbacks == null)
            {
                Debug.LogWarning("Gatebreaker font generator: TMP settings fallback list not found.");
                return;
            }

            for (int i = 0; i < fallbacks.arraySize; i++)
            {
                if (fallbacks.GetArrayElementAtIndex(i).objectReferenceValue == fallbackFont)
                {
                    serializedSettings.ApplyModifiedProperties();
                    return;
                }
            }

            fallbacks.InsertArrayElementAtIndex(fallbacks.arraySize);
            fallbacks.GetArrayElementAtIndex(fallbacks.arraySize - 1).objectReferenceValue = fallbackFont;
            serializedSettings.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
        }

        private static void CreatePreviewPrefab(
            TMP_FontAsset fusion,
            TMP_FontAsset pressStart,
            TMP_FontAsset pixelify,
            TMP_FontAsset dotGothic)
        {
            GameObject root = new GameObject("GatebreakerFontPreview", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            try
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(900f, 1200f);

                GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
                background.transform.SetParent(root.transform, false);
                var backgroundRect = background.GetComponent<RectTransform>();
                backgroundRect.anchorMin = Vector2.zero;
                backgroundRect.anchorMax = Vector2.one;
                backgroundRect.offsetMin = Vector2.zero;
                backgroundRect.offsetMax = Vector2.zero;
                background.GetComponent<Image>().color = new Color(0.015f, 0.02f, 0.025f, 1f);

                CreateText(root.transform, "ArcadeTitle_PressStart2P", pressStart, "BATTLE MODE", 38f, new Vector2(0f, 440f), new Color(1f, 0.82f, 0.05f, 1f));
                CreateText(root.transform, "PixelChinese_FusionPixel", fusion, "比分：0    阶段：进行中    弹药：1/5", 32f, new Vector2(0f, 280f), Color.white);
                CreateText(root.transform, "PixelBody_PixelifySans", pixelify, "SCORE: 0    HIT: 0    TIME 01:00", 34f, new Vector2(0f, 120f), new Color(0.8f, 0.95f, 1f, 1f));
                CreateText(root.transform, "PixelAlt_DotGothic16", dotGothic, "比赛结束    房间号：AB12", 34f, new Vector2(0f, -40f), new Color(0.6f, 1f, 0.5f, 1f));
                CreateText(root.transform, "FallbackCheck", pressStart, "PressStart fallback: 比分 阶段 比赛结束", 24f, new Vector2(0f, -220f), new Color(1f, 1f, 1f, 1f));

                PrefabUtility.SaveAsPrefabAsset(root, PreviewPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateText(
            Transform parent,
            string objectName,
            TMP_FontAsset fontAsset,
            string text,
            float fontSize,
            Vector2 anchoredPosition,
            Color color)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(820f, 120f);
            rect.anchoredPosition = anchoredPosition;

            var label = textObject.GetComponent<TextMeshProUGUI>();
            label.font = fontAsset;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.raycastTarget = false;
        }
    }
}

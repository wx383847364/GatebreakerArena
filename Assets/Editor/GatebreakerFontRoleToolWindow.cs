using App.HotUpdate.GatebreakerArena.UI;
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Text = UnityEngine.UI.Text;

namespace Gatebreaker.Editor
{
    public sealed class GatebreakerFontRoleToolWindow : EditorWindow
    {
        private const float MinReportHeight = 220f;
        private const float MaxReportHeight = 520f;

        private GatebreakerFontRoleProfile _profile;
        private Vector2 _contentScroll;
        private Vector2 _reportScroll;
        private List<GatebreakerFontRoleScanItem> _scanItems = new List<GatebreakerFontRoleScanItem>();
        private string _lastSummary = string.Empty;
        private int _expandedActionIndex = -1;

        [MenuItem("Gatebreaker/Fonts/Font Role Tool")]
        public static void Open()
        {
            var window = GetWindow<GatebreakerFontRoleToolWindow>("Gatebreaker Font Roles");
            window.minSize = new Vector2(780f, 540f);
            window.Show();
        }

        private void OnEnable()
        {
            _profile = GatebreakerFontRoleToolLogic.LoadOrCreateDefaultProfile();
        }

        private void OnGUI()
        {
            _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll);
            try
            {
                EditorGUILayout.Space(8f);
                DrawProfileHeader();
                EditorGUILayout.Space(8f);

                if (_profile == null)
                {
                    EditorGUILayout.HelpBox("GatebreakerFontRoleProfile 缺失。", MessageType.Error);
                    return;
                }

                DrawRoleEntries();
                EditorGUILayout.Space(8f);
                DrawActions();
                EditorGUILayout.Space(8f);
                DrawReport();
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawProfileHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Profile", GUILayout.Width(72f));
                _profile = (GatebreakerFontRoleProfile)EditorGUILayout.ObjectField(_profile, typeof(GatebreakerFontRoleProfile), false);
                if (GUILayout.Button("Load / Create Default", GUILayout.Width(168f)))
                {
                    _profile = GatebreakerFontRoleToolLogic.LoadOrCreateDefaultProfile();
                    _scanItems.Clear();
                }
            }

            EditorGUILayout.LabelField("Profile Path", GatebreakerFontRoleProfile.DefaultAssetPath);
            EditorGUILayout.LabelField("TMP Output", GatebreakerFontRoleProfile.DefaultTmpAssetDirectory);
        }

        private void DrawRoleEntries()
        {
            _profile.EnsureDefaultEntries();
            SerializedObject serializedProfile = new SerializedObject(_profile);
            SerializedProperty entries = serializedProfile.FindProperty("roleEntries");

            EditorGUILayout.LabelField("Font Roles", EditorStyles.boldLabel);
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                GatebreakerFontRole role = (GatebreakerFontRole)entry.FindPropertyRelative("Role").enumValueIndex;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(entry.FindPropertyRelative("Enabled"), GUIContent.none, GUILayout.Width(20f));
                        EditorGUILayout.LabelField(role.ToString(), EditorStyles.boldLabel, GUILayout.Width(120f));
                        EditorGUILayout.PropertyField(entry.FindPropertyRelative("SourceFont"), GUIContent.none);
                    }

                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("TmpFontAsset"), new GUIContent("TMP Font"));
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("Description"));
                }
            }

            if (serializedProfile.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_profile);
            }
        }

        private void DrawActions()
        {
            DrawActionButtonWithHelp(
                0,
                "Scan Fonts Folder",
                "扫描字体目录，给空角色补推荐字体。",
                "用途：从 Assets/HotUpdateContent/Res/fonts/source 查找 .ttf / .otf 字体，并给空角色补推荐字体。\n会改：只更新 GatebreakerFontRoleProfile.asset 的角色源字体。\n不会：不生成 TMP，也不会修改 scene / prefab。",
                () =>
                {
                    GatebreakerFontRoleToolLogic.AssignRecommendedDefaults(_profile, overwriteExisting: false);
                    EditorUtility.SetDirty(_profile);
                    AssetDatabase.SaveAssets();
                    _lastSummary = "Font roles scanned and recommended defaults assigned.";
                });

            DrawActionButtonWithHelp(
                1,
                "Generate / Refresh TMP Assets",
                "生成 TMP 字体资产，解决 TMP Font 为 None。",
                "用途：把已启用角色的 Source Font 生成或刷新为 TMP Font Asset。\n会改：创建或刷新 fonts/tmp 下的 TMP 资产，并回填到角色 Profile。\n不会：不修改 scene / prefab 的字体引用。",
                () =>
                {
                    GatebreakerFontRoleToolLogic.GenerateOrRefreshTmpAssets(_profile);
                    GatebreakerFontRoleToolLogic.ConfigureTmpSettingsFallback(_profile);
                    EditorUtility.SetDirty(_profile);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    _lastSummary = "TMP font assets generated/refreshed under " + GatebreakerFontRoleProfile.DefaultTmpAssetDirectory + ".";
                });

            DrawActionButtonWithHelp(
                2,
                "Create / Refresh Runtime Settings",
                "同步运行时中文兜底字体。",
                "用途：创建或刷新 GatebreakerFontRuntimeSettings.asset。\n会改：runtime settings 资产。\n不会：不修改 scene / prefab，也不会替换已有界面字体。",
                () =>
                {
                    GatebreakerFontRuntimeSettings settings = GatebreakerFontRoleToolLogic.CreateOrRefreshRuntimeSettings(_profile);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    _lastSummary = "Runtime settings refreshed: " + AssetDatabase.GetAssetPath(settings);
                });

            DrawActionButtonWithHelp(
                3,
                "Scan UI Texts",
                "预览 BootstrapScene 与字体预览 prefab 的字体角色匹配结果。",
                "用途：扫描 Gatebreaker 当前 UI 文本会匹配到哪个字体角色。\n范围：BootstrapScene.scene 与 GatebreakerFontPreview.prefab。\n会改：普通扫描不写 scene / prefab；如果在报告里手动改角色，只保存 override 到 Profile。\n何时点：Apply 前或检查字体角色时先点它。",
                () =>
                {
                    _scanItems = GatebreakerFontRoleToolLogic.ScanUiTexts(_profile);
                    _lastSummary = $"Scanned {_scanItems.Count} text components.";
                });

            DrawActionButtonWithHelp(
                4,
                "Apply To UI Assets",
                "按当前角色与手动 override 替换 UI 字体引用。",
                "用途：把 Scan UI Texts 中的角色结果写入 BootstrapScene 与字体预览 prefab。\n会改：只替换 TMP/Text 的字体引用。\n不会：不改字号、颜色、对齐、文本、布局、透明度、材质参数或玩法逻辑。\n说明：Apply 前只做 TMP 资产缺失/损坏的轻量修复；换了 Source Font 后仍应先点 Generate / Refresh TMP Assets。",
                () =>
                {
                    bool confirmed = EditorUtility.DisplayDialog(
                        "Apply To UI Assets",
                        "将按当前字体角色替换 BootstrapScene 和字体预览 prefab 中的文本字体引用。\n\n只会写 TMP/Text 字体字段，不会修改字号、颜色、对齐、文本、布局或材质参数。\n\n继续执行？",
                        "Apply",
                        "Cancel");
                    if (!confirmed)
                    {
                        _lastSummary = "Apply cancelled.";
                        return;
                    }

                    if (GatebreakerFontRoleToolLogic.EnsureTmpAssetsReady(_profile))
                    {
                        _lastSummary = "TMP assets were repaired before applying fonts.";
                    }

                    _profile = GatebreakerFontRoleToolLogic.ReloadProfile(_profile);
                    GatebreakerFontRoleApplyResult result = GatebreakerFontRoleToolLogic.ApplyToUiAssets(_profile);
                    _scanItems = GatebreakerFontRoleToolLogic.ScanUiTexts(_profile);
                    _lastSummary = $"Applied fonts. Assets: {result.AssetsChanged}, TMP: {result.TmpTextsChanged}, Legacy Text: {result.LegacyTextsChanged}, Skipped: {result.Skipped}.";
                });

            if (!string.IsNullOrWhiteSpace(_lastSummary))
            {
                EditorGUILayout.HelpBox(_lastSummary, MessageType.Info);
            }
        }

        private void DrawActionButtonWithHelp(int index, string label, string summary, string detail, Action onClick)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(label, GUILayout.Width(280f), GUILayout.Height(28f)))
                    {
                        ExecuteAction(label, onClick);
                    }

                    EditorGUILayout.LabelField(summary, EditorStyles.wordWrappedMiniLabel, GUILayout.MinHeight(28f));
                    DrawDetailsToggle(index);
                }

                if (_expandedActionIndex == index)
                {
                    EditorGUILayout.HelpBox(detail, MessageType.None);
                }
            }
        }

        private void DrawDisabledActionWithHelp(int index, string label, string summary, string detail)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        GUILayout.Button(label, GUILayout.Width(280f), GUILayout.Height(28f));
                    }

                    EditorGUILayout.LabelField(summary, EditorStyles.wordWrappedMiniLabel, GUILayout.MinHeight(28f));
                    DrawDetailsToggle(index);
                }

                if (_expandedActionIndex == index)
                {
                    EditorGUILayout.HelpBox(detail, MessageType.None);
                }
            }
        }

        private void DrawDetailsToggle(int index)
        {
            bool expanded = _expandedActionIndex == index;
            if (GUILayout.Button(expanded ? "Hide" : "Details", GUILayout.Width(72f), GUILayout.Height(28f)))
            {
                _expandedActionIndex = expanded ? -1 : index;
            }
        }

        private void ExecuteAction(string label, Action onClick)
        {
            try
            {
                _lastSummary = string.Empty;
                onClick?.Invoke();

                string message = string.IsNullOrWhiteSpace(_lastSummary)
                    ? label + " completed."
                    : _lastSummary;
                EditorUtility.DisplayDialog("执行成功", label + "\n\n" + message, "OK");
            }
            catch (Exception exception)
            {
                _lastSummary = label + " failed: " + exception.Message;
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("执行失败", label + "\n\n" + exception.Message, "OK");
            }
        }

        private void DrawReport()
        {
            if (_scanItems == null || _scanItems.Count == 0)
            {
                return;
            }

            EditorGUILayout.LabelField($"Scan Report ({_scanItems.Count})", EditorStyles.boldLabel);

            float reportHeight = Mathf.Clamp(position.height * 0.42f, MinReportHeight, MaxReportHeight);
            _reportScroll = EditorGUILayout.BeginScrollView(_reportScroll, GUILayout.Height(reportHeight));
            for (int i = 0; i < _scanItems.Count; i++)
            {
                GatebreakerFontRoleScanItem item = _scanItems[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(item.AssetName, GUILayout.Width(150f));
                        EditorGUILayout.LabelField(item.ComponentType, GUILayout.Width(118f));
                        GatebreakerFontRole nextRole = (GatebreakerFontRole)EditorGUILayout.EnumPopup(item.Role, GUILayout.Width(130f));
                        if (nextRole != item.Role)
                        {
                            _profile.SetManualOverride(item.AssetPath, item.TransformPath, item.ComponentType, nextRole);
                            EditorUtility.SetDirty(_profile);
                            AssetDatabase.SaveAssets();
                            item.Role = nextRole;
                            item.Source = "Manual";
                            item.Warning = GatebreakerFontRoleToolLogic.GetRoleAssetWarning(_profile, item.ComponentType, nextRole);
                        }

                        if (GUILayout.Button("Select Asset", GUILayout.Width(102f)))
                        {
                            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(item.AssetPath);
                        }

                        if (GUILayout.Button("Ping", GUILayout.Width(54f)))
                        {
                            Object asset = AssetDatabase.LoadAssetAtPath<Object>(item.AssetPath);
                            EditorGUIUtility.PingObject(asset);
                        }
                    }

                    EditorGUILayout.LabelField("Path", string.IsNullOrEmpty(item.TransformPath) ? "(root)" : item.TransformPath);
                    EditorGUILayout.LabelField("Text", item.TextPreview);
                    EditorGUILayout.LabelField("Rule", item.Source + (string.IsNullOrWhiteSpace(item.Warning) ? string.Empty : " / " + item.Warning));
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    public sealed class GatebreakerFontRoleScanItem
    {
        public string AssetPath;
        public string AssetName;
        public string TransformPath;
        public string ComponentType;
        public string TextPreview;
        public float FontSize;
        public GatebreakerFontRole Role;
        public string Source;
        public string Warning;
    }

    public sealed class GatebreakerFontRoleApplyResult
    {
        public int AssetsChanged;
        public int TmpTextsChanged;
        public int LegacyTextsChanged;
        public int Skipped;
    }

    public static class GatebreakerFontRoleToolLogic
    {
        public static readonly string[] UiScenePaths =
        {
            "Assets/Scenes/BootstrapScene.scene",
        };

        public static readonly string[] UiPrefabPaths =
        {
            "Assets/HotUpdateContent/Res/fonts/preview/GatebreakerFontPreview.prefab",
        };

        private const string FontRoot = "Assets/HotUpdateContent/Res/fonts";
        private const string SourceRoot = FontRoot + "/source";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string TmpSample = "Gatebreaker Arena BATTLE MODE GAME OVER READY SCORE HIT TIME Player Jason Block 比分阶段弹药比赛结束房间号状态人数本机 0123456789+-/.,:%";

        public static GatebreakerFontRoleProfile LoadOrCreateDefaultProfile()
        {
            EnsureFolder(FontRoot);
            GatebreakerFontRoleProfile profile = AssetDatabase.LoadAssetAtPath<GatebreakerFontRoleProfile>(GatebreakerFontRoleProfile.DefaultAssetPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<GatebreakerFontRoleProfile>();
                profile.EnsureDefaultEntries();
                AssignRecommendedDefaults(profile, overwriteExisting: true);
                AssetDatabase.CreateAsset(profile, GatebreakerFontRoleProfile.DefaultAssetPath);
                AssetDatabase.SaveAssets();
            }
            else
            {
                profile.EnsureDefaultEntries();
                AssignRecommendedDefaults(profile, overwriteExisting: false);
                EditorUtility.SetDirty(profile);
            }

            return profile;
        }

        public static GatebreakerFontRoleProfile ReloadProfile(GatebreakerFontRoleProfile profile)
        {
            string profilePath = profile != null
                ? AssetDatabase.GetAssetPath(profile)
                : GatebreakerFontRoleProfile.DefaultAssetPath;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GatebreakerFontRoleProfile reloaded = AssetDatabase.LoadAssetAtPath<GatebreakerFontRoleProfile>(profilePath);
            if (reloaded == null)
            {
                reloaded = LoadOrCreateDefaultProfile();
            }

            reloaded.EnsureDefaultEntries();
            return reloaded;
        }

        public static void AssignRecommendedDefaults(GatebreakerFontRoleProfile profile, bool overwriteExisting)
        {
            if (profile == null)
            {
                return;
            }

            profile.EnsureDefaultEntries();
            AssignFont(profile, GatebreakerFontRole.ArcadeTitle, "PressStart2P-Regular", overwriteExisting);
            AssignFont(profile, GatebreakerFontRole.PixelChinese, "FusionPixel12-Proportional-zh_hans", overwriteExisting);
            AssignFont(profile, GatebreakerFontRole.PixelBody, "PixelifySans-wght", overwriteExisting);
            AssignFont(profile, GatebreakerFontRole.PixelAlt, "DotGothic16-Regular", overwriteExisting);
            AssignFont(profile, GatebreakerFontRole.Fallback, "FusionPixel12-Proportional-zh_hans", overwriteExisting);
        }

        public static void GenerateOrRefreshTmpAssets(GatebreakerFontRoleProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            profile.EnsureDefaultEntries();
            EnsureFolder(FontRoot);
            EnsureFolder(GatebreakerFontRoleProfile.DefaultTmpAssetDirectory);

            for (int i = 0; i < profile.RoleEntries.Count; i++)
            {
                GatebreakerFontRoleEntry entry = profile.RoleEntries[i];
                if (entry == null || !entry.Enabled || entry.SourceFont == null)
                {
                    continue;
                }

                string sourcePath = AssetDatabase.GetAssetPath(entry.SourceFont);
                string fileName = SanitizeFileName("Gatebreaker_" + entry.Role + "_" + Path.GetFileNameWithoutExtension(sourcePath));
                if (entry.Role == GatebreakerFontRole.PixelChinese)
                {
                    fileName = "Gatebreaker_PixelChinese_FusionPixel12_TMP";
                }
                else if (entry.Role == GatebreakerFontRole.ArcadeTitle)
                {
                    fileName = "Gatebreaker_ArcadeTitle_PressStart2P_TMP";
                }
                else if (entry.Role == GatebreakerFontRole.PixelBody)
                {
                    fileName = "Gatebreaker_PixelBody_PixelifySans_TMP";
                }
                else if (entry.Role == GatebreakerFontRole.PixelAlt)
                {
                    fileName = "Gatebreaker_PixelAlt_DotGothic16_TMP";
                }

                string assetPath = GatebreakerFontRoleProfile.DefaultTmpAssetDirectory + "/" + fileName + ".asset";
                TMP_FontAsset tmpAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
                int atlasSize = entry.Role == GatebreakerFontRole.PixelChinese || entry.Role == GatebreakerFontRole.Fallback
                    ? 2048
                    : 1024;

                if (tmpAsset == null)
                {
                    tmpAsset = TMP_FontAsset.CreateFontAsset(
                        entry.SourceFont,
                        90,
                        9,
                        GlyphRenderMode.SDFAA,
                        atlasSize,
                        atlasSize,
                        AtlasPopulationMode.Dynamic,
                        true);
                    tmpAsset.name = fileName;
                    tmpAsset.TryAddCharacters(TmpSample, true);
                    AssetDatabase.CreateAsset(tmpAsset, assetPath);
                    PersistTmpFontSubAssets(tmpAsset);
                }
                else
                {
                    if (!IsTmpFontAssetUsable(tmpAsset))
                    {
                        RefreshTmpFontAsset(tmpAsset, entry.SourceFont, fileName, atlasSize);
                    }
                    else
                    {
                        tmpAsset.TryAddCharacters(TmpSample, true);
                        PersistTmpFontSubAssets(tmpAsset);
                    }

                    EditorUtility.SetDirty(tmpAsset);
                }

                entry.TmpFontAsset = tmpAsset;
            }

            ConfigureRoleFallbacks(profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static bool EnsureTmpAssetsReady(GatebreakerFontRoleProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            profile.EnsureDefaultEntries();
            for (int i = 0; i < profile.RoleEntries.Count; i++)
            {
                GatebreakerFontRoleEntry entry = profile.RoleEntries[i];
                if (entry == null || !entry.Enabled)
                {
                    continue;
                }

                bool missingTmpForTmpRole = entry.SourceFont != null && entry.TmpFontAsset == null;
                bool damagedTmp = entry.TmpFontAsset != null && !IsTmpFontAssetUsable(entry.TmpFontAsset);
                if (missingTmpForTmpRole || damagedTmp)
                {
                    GenerateOrRefreshTmpAssets(profile);
                    return true;
                }
            }

            return false;
        }

        public static GatebreakerFontRuntimeSettings CreateOrRefreshRuntimeSettings(GatebreakerFontRoleProfile profile)
        {
            GatebreakerFontRuntimeSettings settings = AssetDatabase.LoadAssetAtPath<GatebreakerFontRuntimeSettings>(GatebreakerFontRuntimeSettings.DefaultAssetPath);
            if (settings == null)
            {
                EnsureFolder(FontRoot);
                settings = ScriptableObject.CreateInstance<GatebreakerFontRuntimeSettings>();
                AssetDatabase.CreateAsset(settings, GatebreakerFontRuntimeSettings.DefaultAssetPath);
            }

            TMP_FontAsset fallbackTmp = profile != null ? profile.ResolveFallbackTmpFont() : null;
            Font fallbackSource = profile != null ? profile.ResolveFallbackSourceFont() : null;
            settings.Configure(profile, fallbackTmp, fallbackSource);
            EditorUtility.SetDirty(settings);
            return settings;
        }

        public static void ConfigureTmpSettingsFallback(GatebreakerFontRoleProfile profile)
        {
            TMP_FontAsset fallbackFont = profile != null ? profile.ResolveFallbackTmpFont() : null;
            if (fallbackFont == null)
            {
                return;
            }

            TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings == null)
            {
                Debug.LogWarning($"Gatebreaker font role tool: TMP settings not found at {TmpSettingsPath}");
                return;
            }

            var serializedSettings = new SerializedObject(settings);
            SerializedProperty fallbacks = serializedSettings.FindProperty("m_fallbackFontAssets");
            if (fallbacks == null)
            {
                Debug.LogWarning("Gatebreaker font role tool: TMP settings fallback list not found.");
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

        public static List<GatebreakerFontRoleScanItem> ScanUiTexts(GatebreakerFontRoleProfile profile)
        {
            var result = new List<GatebreakerFontRoleScanItem>();
            for (int i = 0; i < UiScenePaths.Length; i++)
            {
                ScanScene(profile, UiScenePaths[i], result);
            }

            for (int i = 0; i < UiPrefabPaths.Length; i++)
            {
                ScanPrefab(profile, UiPrefabPaths[i], result);
            }

            return result;
        }

        public static GatebreakerFontRoleApplyResult ApplyToUiAssets(GatebreakerFontRoleProfile profile)
        {
            var result = new GatebreakerFontRoleApplyResult();
            if (profile == null)
            {
                return result;
            }

            profile.EnsureDefaultEntries();
            for (int i = 0; i < UiScenePaths.Length; i++)
            {
                ApplyToScene(profile, UiScenePaths[i], result);
            }

            for (int i = 0; i < UiPrefabPaths.Length; i++)
            {
                ApplyToPrefab(profile, UiPrefabPaths[i], result);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return result;
        }

        public static GatebreakerFontRole ResolveRole(
            GatebreakerFontRoleProfile profile,
            string assetPath,
            string transformPath,
            string componentType,
            string objectName,
            string text,
            float fontSize,
            out string source)
        {
            if (profile != null &&
                profile.TryGetManualOverride(assetPath, transformPath, componentType, out GatebreakerFontRole manualRole))
            {
                source = "Manual";
                return manualRole;
            }

            source = "Rule";
            return ClassifyRole(transformPath + "/" + objectName, text, fontSize);
        }

        public static GatebreakerFontRole ClassifyRole(string pathOrName, string text, float fontSize)
        {
            string haystack = (pathOrName ?? string.Empty).ToLowerInvariant();
            if (ContainsAny(haystack, "alt", "previewalt"))
            {
                return GatebreakerFontRole.PixelAlt;
            }

            if (ContainsAny(haystack, "score", "hit", "time", "count", "ammo", "ballcount") || IsMostlyNumeric(text))
            {
                return GatebreakerFontRole.PixelBody;
            }

            if (ContainsAny(haystack, "battle", "title", "mode", "gameover") ||
                string.Equals((text ?? string.Empty).Trim(), "BATTLE MODE", StringComparison.OrdinalIgnoreCase) ||
                fontSize >= 36f)
            {
                return GatebreakerFontRole.ArcadeTitle;
            }

            if (ContainsChinese(text) ||
                ContainsAny(haystack, "hud", "status", "room", "lan", "result", "state", "阶段", "房间"))
            {
                return GatebreakerFontRole.PixelChinese;
            }

            return GatebreakerFontRole.PixelBody;
        }

        public static string GetRoleAssetWarning(
            GatebreakerFontRoleProfile profile,
            string componentType,
            GatebreakerFontRole role)
        {
            GatebreakerFontRoleEntry entry = profile?.GetEntry(role);
            if (entry == null || !entry.Enabled)
            {
                return "role disabled or missing";
            }

            if ((componentType == nameof(TextMeshProUGUI) || componentType == nameof(TMP_Text)) && entry.TmpFontAsset == null)
            {
                return "TMP font missing";
            }

            if (componentType == nameof(Text) && entry.SourceFont == null)
            {
                return "source font missing";
            }

            return string.Empty;
        }

        private static void ScanScene(GatebreakerFontRoleProfile profile, string scenePath, List<GatebreakerFontRoleScanItem> result)
        {
            if (!File.Exists(scenePath))
            {
                return;
            }

            bool alreadyLoaded = TryGetLoadedScene(scenePath, out Scene openedScene);
            if (!alreadyLoaded)
            {
                openedScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }

            try
            {
                GameObject[] roots = openedScene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    ScanRoot(profile, scenePath, Path.GetFileNameWithoutExtension(scenePath), roots[i], roots[i].transform, result);
                }
            }
            finally
            {
                if (!alreadyLoaded)
                {
                    EditorSceneManager.CloseScene(openedScene, removeScene: true);
                }
            }
        }

        private static void ScanPrefab(GatebreakerFontRoleProfile profile, string prefabPath, List<GatebreakerFontRoleScanItem> result)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                ScanRoot(profile, prefabPath, root.name, root, root.transform, result);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ApplyToScene(
            GatebreakerFontRoleProfile profile,
            string scenePath,
            GatebreakerFontRoleApplyResult result)
        {
            if (!File.Exists(scenePath))
            {
                return;
            }

            bool alreadyLoaded = TryGetLoadedScene(scenePath, out Scene openedScene);
            if (!alreadyLoaded)
            {
                openedScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }

            bool changed = false;
            try
            {
                GameObject[] roots = openedScene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    changed |= ApplyToRoot(profile, scenePath, roots[i].transform, roots[i], result);
                }

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(openedScene);
                    EditorSceneManager.SaveScene(openedScene);
                    result.AssetsChanged++;
                }
            }
            finally
            {
                if (!alreadyLoaded)
                {
                    EditorSceneManager.CloseScene(openedScene, removeScene: true);
                }
            }
        }

        private static void ApplyToPrefab(
            GatebreakerFontRoleProfile profile,
            string prefabPath,
            GatebreakerFontRoleApplyResult result)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            bool changed = false;
            try
            {
                changed = ApplyToRoot(profile, prefabPath, root.transform, root, result);
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    result.AssetsChanged++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool ApplyToRoot(
            GatebreakerFontRoleProfile profile,
            string assetPath,
            Transform rootTransform,
            GameObject rootObject,
            GatebreakerFontRoleApplyResult result)
        {
            bool changed = false;

            TMP_Text[] tmpTexts = rootObject.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < tmpTexts.Length; i++)
            {
                TMP_Text text = tmpTexts[i];
                string transformPath = BuildTransformPath(rootTransform, text.transform);
                GatebreakerFontRole role = ResolveRole(profile, assetPath, transformPath, nameof(TextMeshProUGUI), text.name, text.text, text.fontSize, out _);
                TMP_FontAsset targetFont = profile.GetTmpFont(role);
                if (!IsTmpFontAssetUsable(targetFont))
                {
                    result.Skipped++;
                    continue;
                }

                if (text.font != targetFont)
                {
                    Undo.RecordObject(text, "Apply Gatebreaker TMP Font");
                    text.font = targetFont;
                    EditorUtility.SetDirty(text);
                    result.TmpTextsChanged++;
                    changed = true;
                }
            }

            Text[] legacyTexts = rootObject.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < legacyTexts.Length; i++)
            {
                Text text = legacyTexts[i];
                string transformPath = BuildTransformPath(rootTransform, text.transform);
                GatebreakerFontRole role = ResolveRole(profile, assetPath, transformPath, nameof(Text), text.name, text.text, text.fontSize, out _);
                GatebreakerFontRoleEntry entry = profile.GetEntry(role);
                Font targetFont = entry != null && entry.Enabled ? entry.SourceFont : null;
                if (targetFont == null)
                {
                    result.Skipped++;
                    continue;
                }

                if (text.font != targetFont)
                {
                    Undo.RecordObject(text, "Apply Gatebreaker Legacy Font");
                    text.font = targetFont;
                    EditorUtility.SetDirty(text);
                    result.LegacyTextsChanged++;
                    changed = true;
                }
            }

            return changed;
        }

        private static void ScanRoot(
            GatebreakerFontRoleProfile profile,
            string assetPath,
            string assetName,
            GameObject rootObject,
            Transform rootTransform,
            List<GatebreakerFontRoleScanItem> result)
        {
            TMP_Text[] tmpTexts = rootObject.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < tmpTexts.Length; i++)
            {
                TMP_Text text = tmpTexts[i];
                string transformPath = BuildTransformPath(rootTransform, text.transform);
                GatebreakerFontRole role = ResolveRole(profile, assetPath, transformPath, nameof(TextMeshProUGUI), text.name, text.text, text.fontSize, out string source);
                result.Add(CreateScanItem(profile, assetPath, assetName, transformPath, nameof(TextMeshProUGUI), text.text, text.fontSize, role, source));
            }

            Text[] legacyTexts = rootObject.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < legacyTexts.Length; i++)
            {
                Text text = legacyTexts[i];
                string transformPath = BuildTransformPath(rootTransform, text.transform);
                GatebreakerFontRole role = ResolveRole(profile, assetPath, transformPath, nameof(Text), text.name, text.text, text.fontSize, out string source);
                result.Add(CreateScanItem(profile, assetPath, assetName, transformPath, nameof(Text), text.text, text.fontSize, role, source));
            }
        }

        private static GatebreakerFontRoleScanItem CreateScanItem(
            GatebreakerFontRoleProfile profile,
            string assetPath,
            string assetName,
            string transformPath,
            string componentType,
            string text,
            float fontSize,
            GatebreakerFontRole role,
            string source)
        {
            return new GatebreakerFontRoleScanItem
            {
                AssetPath = assetPath,
                AssetName = assetName,
                TransformPath = transformPath,
                ComponentType = componentType,
                TextPreview = PreviewText(text),
                FontSize = fontSize,
                Role = role,
                Source = source,
                Warning = GetRoleAssetWarning(profile, componentType, role),
            };
        }

        private static void ConfigureRoleFallbacks(GatebreakerFontRoleProfile profile)
        {
            TMP_FontAsset fallback = profile.ResolveFallbackTmpFont();
            if (fallback == null)
            {
                return;
            }

            AddFallback(profile.GetTmpFont(GatebreakerFontRole.ArcadeTitle), fallback);
            AddFallback(profile.GetTmpFont(GatebreakerFontRole.PixelBody), fallback);
            AddFallback(profile.GetTmpFont(GatebreakerFontRole.PixelAlt), fallback);
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

        private static bool IsTmpFontAssetUsable(TMP_FontAsset fontAsset)
        {
            if (!IsUnityObjectAlive(fontAsset) || HasMissingAtlasTexture(fontAsset))
            {
                return false;
            }

            try
            {
                return IsUnityObjectAlive(fontAsset.material);
            }
            catch (MissingReferenceException)
            {
                return false;
            }
        }

        private static bool HasMissingAtlasTexture(TMP_FontAsset fontAsset)
        {
            if (!IsUnityObjectAlive(fontAsset))
            {
                return true;
            }

            Texture2D[] atlasTextures;
            try
            {
                atlasTextures = fontAsset.atlasTextures;
            }
            catch (MissingReferenceException)
            {
                return true;
            }

            if (atlasTextures == null || atlasTextures.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < atlasTextures.Length; i++)
            {
                if (!IsUnityObjectAlive(atlasTextures[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsUnityObjectAlive(Object value)
        {
            try
            {
                return value != null;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
        }

        private static void RefreshTmpFontAsset(
            TMP_FontAsset target,
            Font sourceFont,
            string fileName,
            int atlasSize)
        {
            TMP_FontAsset refreshed = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,
                9,
                GlyphRenderMode.SDFAA,
                atlasSize,
                atlasSize,
                AtlasPopulationMode.Dynamic,
                true);
            refreshed.name = fileName;
            refreshed.TryAddCharacters(TmpSample, true);

            EditorUtility.CopySerialized(refreshed, target);
            target.name = fileName;
            PersistTmpFontSubAssets(target);
        }

        private static void PersistTmpFontSubAssets(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
            {
                return;
            }

            Texture2D[] atlasTextures = fontAsset.atlasTextures;
            if (atlasTextures != null)
            {
                for (int i = 0; i < atlasTextures.Length; i++)
                {
                    Texture2D texture = atlasTextures[i];
                    if (texture == null || AssetDatabase.Contains(texture))
                    {
                        continue;
                    }

                    texture.name = fontAsset.name + " Atlas " + i;
                    AssetDatabase.AddObjectToAsset(texture, fontAsset);
                    EditorUtility.SetDirty(texture);
                }
            }

            Material material = fontAsset.material;
            if (material != null && !AssetDatabase.Contains(material))
            {
                material.name = fontAsset.name + " Material";
                AssetDatabase.AddObjectToAsset(material, fontAsset);
                EditorUtility.SetDirty(material);
            }

            EditorUtility.SetDirty(fontAsset);
        }

        private static bool TryGetLoadedScene(string scenePath, out Scene scene)
        {
            string normalizedPath = (scenePath ?? string.Empty).Replace("\\", "/");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loadedScene = SceneManager.GetSceneAt(i);
                if (string.Equals(loadedScene.path.Replace("\\", "/"), normalizedPath, StringComparison.Ordinal))
                {
                    scene = loadedScene;
                    return true;
                }
            }

            scene = default(Scene);
            return false;
        }

        private static void AssignFont(
            GatebreakerFontRoleProfile profile,
            GatebreakerFontRole role,
            string fileNameWithoutExtension,
            bool overwriteExisting)
        {
            GatebreakerFontRoleEntry entry = profile.GetEntry(role);
            if (entry == null || (!overwriteExisting && entry.SourceFont != null))
            {
                return;
            }

            Font font = FindFont(fileNameWithoutExtension);
            if (font != null)
            {
                entry.SourceFont = font;
            }
        }

        private static Font FindFont(string fileNameWithoutExtension)
        {
            string[] guids = AssetDatabase.FindAssets("t:Font", new[] { SourceRoot });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.Equals(Path.GetFileNameWithoutExtension(path), fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return AssetDatabase.LoadAssetAtPath<Font>(path);
                }
            }

            return null;
        }

        private static string BuildTransformPath(Transform root, Transform target)
        {
            if (root == null || target == null || root == target)
            {
                return root != null ? root.name : string.Empty;
            }

            var stack = new Stack<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                stack.Push(current.name);
                current = current.parent;
            }

            if (root != null)
            {
                stack.Push(root.name);
            }

            return string.Join("/", stack.ToArray());
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
            {
                if (value.Contains(needles[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsChinese(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] >= '\u4e00' && value[i] <= '\u9fff')
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsMostlyNumeric(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            int meaningful = 0;
            int numeric = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (char.IsWhiteSpace(ch))
                {
                    continue;
                }

                meaningful++;
                if (char.IsDigit(ch) || ch == '+' || ch == '-' || ch == '/' || ch == '.' || ch == ',' || ch == ':' || ch == '%' || ch == 'B' || ch == 'K' || ch == 'M')
                {
                    numeric++;
                }
            }

            return meaningful > 0 && numeric >= Mathf.CeilToInt(meaningful * 0.6f);
        }

        private static string PreviewText(string value)
        {
            string safe = string.IsNullOrEmpty(value) ? "(empty)" : value.Replace("\r", " ").Replace("\n", " ");
            return safe.Length <= 48 ? safe : safe.Substring(0, 48) + "...";
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static string SanitizeFileName(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            string safe = value ?? "FontAsset";
            for (int i = 0; i < invalid.Length; i++)
            {
                safe = safe.Replace(invalid[i], '_');
            }

            return safe.Replace(' ', '_');
        }
    }
}

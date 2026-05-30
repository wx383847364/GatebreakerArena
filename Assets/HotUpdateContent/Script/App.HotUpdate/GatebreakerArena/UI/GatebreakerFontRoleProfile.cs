using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.UI
{
    public enum GatebreakerFontRole
    {
        ArcadeTitle = 0,
        PixelChinese = 1,
        PixelBody = 2,
        PixelAlt = 3,
        Fallback = 4,
    }

    [Serializable]
    public sealed class GatebreakerFontRoleEntry
    {
        public GatebreakerFontRole Role;
        public bool Enabled = true;
        public Font SourceFont;
        public TMP_FontAsset TmpFontAsset;
        [TextArea(1, 3)]
        public string Description;
    }

    [Serializable]
    public sealed class GatebreakerFontRoleOverride
    {
        public string AssetPath;
        public string TransformPath;
        public string ComponentType;
        public GatebreakerFontRole Role;
    }

    public sealed class GatebreakerFontRoleProfile : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/HotUpdateContent/Res/fonts/GatebreakerFontRoleProfile.asset";
        public const string DefaultTmpAssetDirectory = "Assets/HotUpdateContent/Res/fonts/tmp";

        [SerializeField]
        private List<GatebreakerFontRoleEntry> roleEntries = new List<GatebreakerFontRoleEntry>();

        [SerializeField]
        private List<GatebreakerFontRoleOverride> manualOverrides = new List<GatebreakerFontRoleOverride>();

        public IReadOnlyList<GatebreakerFontRoleEntry> RoleEntries => roleEntries;

        public IReadOnlyList<GatebreakerFontRoleOverride> ManualOverrides => manualOverrides;

        public void EnsureDefaultEntries()
        {
            EnsureEntry(GatebreakerFontRole.ArcadeTitle, "8bit 英文大标题，例如 BATTLE MODE、GAME OVER。");
            EnsureEntry(GatebreakerFontRole.PixelChinese, "中文 HUD、按钮、提示和常规 UI 文本。");
            EnsureEntry(GatebreakerFontRole.PixelBody, "英文玩家名、状态、数字和正文备选。");
            EnsureEntry(GatebreakerFontRole.PixelAlt, "日式像素风备选标题或风格预览。");
            EnsureEntry(GatebreakerFontRole.Fallback, "中文兜底字体，用于 TMP 缺字回退。");
        }

        public GatebreakerFontRoleEntry GetEntry(GatebreakerFontRole role)
        {
            EnsureDefaultEntries();
            for (int i = 0; i < roleEntries.Count; i++)
            {
                GatebreakerFontRoleEntry entry = roleEntries[i];
                if (entry != null && entry.Role == role)
                {
                    return entry;
                }
            }

            return null;
        }

        public bool TryGetManualOverride(
            string assetPath,
            string transformPath,
            string componentType,
            out GatebreakerFontRole role)
        {
            string safeAssetPath = assetPath ?? string.Empty;
            string safeTransformPath = transformPath ?? string.Empty;
            string safeComponentType = componentType ?? string.Empty;

            for (int i = 0; i < manualOverrides.Count; i++)
            {
                GatebreakerFontRoleOverride item = manualOverrides[i];
                if (item == null)
                {
                    continue;
                }

                if (string.Equals(item.AssetPath, safeAssetPath, StringComparison.Ordinal) &&
                    string.Equals(item.TransformPath, safeTransformPath, StringComparison.Ordinal) &&
                    string.Equals(item.ComponentType, safeComponentType, StringComparison.Ordinal))
                {
                    role = item.Role;
                    return true;
                }
            }

            role = GatebreakerFontRole.PixelChinese;
            return false;
        }

        public void SetManualOverride(
            string assetPath,
            string transformPath,
            string componentType,
            GatebreakerFontRole role)
        {
            string safeAssetPath = assetPath ?? string.Empty;
            string safeTransformPath = transformPath ?? string.Empty;
            string safeComponentType = componentType ?? string.Empty;

            for (int i = 0; i < manualOverrides.Count; i++)
            {
                GatebreakerFontRoleOverride item = manualOverrides[i];
                if (item == null)
                {
                    continue;
                }

                if (string.Equals(item.AssetPath, safeAssetPath, StringComparison.Ordinal) &&
                    string.Equals(item.TransformPath, safeTransformPath, StringComparison.Ordinal) &&
                    string.Equals(item.ComponentType, safeComponentType, StringComparison.Ordinal))
                {
                    item.Role = role;
                    return;
                }
            }

            manualOverrides.Add(new GatebreakerFontRoleOverride
            {
                AssetPath = safeAssetPath,
                TransformPath = safeTransformPath,
                ComponentType = safeComponentType,
                Role = role,
            });
        }

        public TMP_FontAsset GetTmpFont(GatebreakerFontRole role)
        {
            GatebreakerFontRoleEntry entry = GetEntry(role);
            if (entry == null || !entry.Enabled)
            {
                return null;
            }

            return entry.TmpFontAsset;
        }

        public TMP_FontAsset ResolveFallbackTmpFont()
        {
            TMP_FontAsset fallback = GetTmpFont(GatebreakerFontRole.Fallback);
            return fallback != null ? fallback : GetTmpFont(GatebreakerFontRole.PixelChinese);
        }

        public Font ResolveFallbackSourceFont()
        {
            GatebreakerFontRoleEntry fallback = GetEntry(GatebreakerFontRole.Fallback);
            if (fallback != null && fallback.Enabled && fallback.SourceFont != null)
            {
                return fallback.SourceFont;
            }

            GatebreakerFontRoleEntry chinese = GetEntry(GatebreakerFontRole.PixelChinese);
            return chinese != null && chinese.Enabled ? chinese.SourceFont : null;
        }

        public void ConfigureRole(
            GatebreakerFontRole role,
            Font sourceFont,
            TMP_FontAsset tmpFontAsset,
            string description)
        {
            GatebreakerFontRoleEntry entry = GetOrCreateEntry(role);
            entry.Enabled = true;
            entry.SourceFont = sourceFont;
            entry.TmpFontAsset = tmpFontAsset;
            entry.Description = description ?? string.Empty;
        }

        private GatebreakerFontRoleEntry GetOrCreateEntry(GatebreakerFontRole role)
        {
            EnsureDefaultEntries();
            for (int i = 0; i < roleEntries.Count; i++)
            {
                GatebreakerFontRoleEntry entry = roleEntries[i];
                if (entry != null && entry.Role == role)
                {
                    return entry;
                }
            }

            var created = new GatebreakerFontRoleEntry { Role = role };
            roleEntries.Add(created);
            return created;
        }

        private void EnsureEntry(GatebreakerFontRole role, string description)
        {
            for (int i = 0; i < roleEntries.Count; i++)
            {
                GatebreakerFontRoleEntry entry = roleEntries[i];
                if (entry != null && entry.Role == role)
                {
                    if (string.IsNullOrWhiteSpace(entry.Description))
                    {
                        entry.Description = description;
                    }

                    return;
                }
            }

            roleEntries.Add(new GatebreakerFontRoleEntry
            {
                Role = role,
                Enabled = true,
                Description = description,
            });
        }
    }
}

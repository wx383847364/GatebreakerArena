using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace App.HotUpdate.GatebreakerArena.UI
{
    public static class GatebreakerRuntimeTmpFontResolver
    {
        public const string RequiredChineseSample = "比分阶段弹药比赛结束房间号状态人数本机";

        private static GatebreakerFontRuntimeSettings _runtimeSettings;
        private static TMP_FontAsset _dynamicFallbackFontAsset;

        public static void SetRuntimeSettings(GatebreakerFontRuntimeSettings settings)
        {
            _runtimeSettings = settings;
            _dynamicFallbackFontAsset = null;
        }

        public static void EnsureFontSupportsText(TMP_Text text, string contentSample)
        {
            if (text == null || string.IsNullOrEmpty(contentSample))
            {
                return;
            }

            TMP_FontAsset resolved = ResolveFontAsset(text.font, contentSample);
            if (resolved != null && !ReferenceEquals(text.font, resolved))
            {
                text.font = resolved;
            }
        }

        public static void EnsureFontSupportsText(Text text, string contentSample)
        {
            if (text == null || string.IsNullOrEmpty(contentSample))
            {
                return;
            }

            Font fallback = _runtimeSettings != null ? _runtimeSettings.DefaultFallbackSourceFont : null;
            if (fallback != null && !ReferenceEquals(text.font, fallback))
            {
                text.font = fallback;
            }
        }

        public static TMP_FontAsset ResolveFontAsset(TMP_FontAsset current, string sample)
        {
            if (SupportsText(current, sample))
            {
                return current;
            }

            TMP_FontAsset defaultAsset = TMP_Settings.defaultFontAsset;
            if (SupportsText(defaultAsset, sample))
            {
                return defaultAsset;
            }

            TMP_FontAsset settingsFallback = _runtimeSettings != null
                ? _runtimeSettings.ResolveFallbackTmpFont()
                : null;
            if (SupportsText(settingsFallback, sample))
            {
                return settingsFallback;
            }

            TMP_FontAsset globalFallback = FindSupportingGlobalFallback(sample);
            if (globalFallback != null)
            {
                return globalFallback;
            }

            TMP_FontAsset dynamicFallback = GetOrCreateDynamicFallbackFontAsset();
            if (SupportsText(dynamicFallback, sample))
            {
                return dynamicFallback;
            }

            return current ?? defaultAsset ?? settingsFallback ?? globalFallback ?? dynamicFallback;
        }

        public static bool SupportsText(TMP_FontAsset fontAsset, string sample)
        {
            if (fontAsset == null || string.IsNullOrEmpty(sample))
            {
                return false;
            }

            for (int i = 0; i < sample.Length; i++)
            {
                char ch = sample[i];
                if (char.IsWhiteSpace(ch) || char.IsControl(ch))
                {
                    continue;
                }

                if (!fontAsset.HasCharacter(ch, true, true))
                {
                    return false;
                }
            }

            return true;
        }

        private static TMP_FontAsset FindSupportingGlobalFallback(string sample)
        {
            IReadOnlyList<TMP_FontAsset> fallbackAssets = TMP_Settings.fallbackFontAssets;
            if (fallbackAssets == null)
            {
                return null;
            }

            for (int i = 0; i < fallbackAssets.Count; i++)
            {
                TMP_FontAsset fallback = fallbackAssets[i];
                if (SupportsText(fallback, sample))
                {
                    return fallback;
                }
            }

            return null;
        }

        private static TMP_FontAsset GetOrCreateDynamicFallbackFontAsset()
        {
            if (_dynamicFallbackFontAsset != null)
            {
                return _dynamicFallbackFontAsset;
            }

            Font sourceFont = _runtimeSettings != null ? _runtimeSettings.DefaultFallbackSourceFont : null;
            if (sourceFont == null)
            {
                return null;
            }

            _dynamicFallbackFontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,
                9,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic,
                true);
            if (_dynamicFallbackFontAsset != null)
            {
                _dynamicFallbackFontAsset.name = "Gatebreaker Runtime Chinese Fallback";
                _dynamicFallbackFontAsset.hideFlags = HideFlags.DontUnloadUnusedAsset;
                _dynamicFallbackFontAsset.TryAddCharacters(RequiredChineseSample, true);
            }

            return _dynamicFallbackFontAsset;
        }
    }
}

using TMPro;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.UI
{
    public sealed class GatebreakerFontRuntimeSettings : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/HotUpdateContent/Res/fonts/GatebreakerFontRuntimeSettings.asset";

        [SerializeField]
        private GatebreakerFontRoleProfile roleProfile;

        [SerializeField]
        private TMP_FontAsset defaultFallbackFont;

        [SerializeField]
        private Font defaultFallbackSourceFont;

        public GatebreakerFontRoleProfile RoleProfile => roleProfile;

        public TMP_FontAsset DefaultFallbackFont => defaultFallbackFont;

        public Font DefaultFallbackSourceFont => defaultFallbackSourceFont;

        public void Configure(
            GatebreakerFontRoleProfile profile,
            TMP_FontAsset fallbackFont,
            Font fallbackSourceFont)
        {
            roleProfile = profile;
            defaultFallbackFont = fallbackFont;
            defaultFallbackSourceFont = fallbackSourceFont;
        }

        public TMP_FontAsset ResolveFallbackTmpFont()
        {
            if (defaultFallbackFont != null)
            {
                return defaultFallbackFont;
            }

            return roleProfile != null ? roleProfile.ResolveFallbackTmpFont() : null;
        }
    }
}

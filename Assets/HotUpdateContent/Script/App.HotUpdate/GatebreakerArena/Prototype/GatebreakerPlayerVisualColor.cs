using App.HotUpdate.GatebreakerArena.Mode;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Prototype
{
    public static class GatebreakerPlayerVisualColor
    {
        public static Color ToUnityColor(PlayerColorRuleDefinition rule)
        {
            return new Color(rule.Red, rule.Green, rule.Blue, rule.Alpha);
        }

        public static void ApplyPaddleColor(GameObject paddleObject, Color ownerColor)
        {
            if (paddleObject == null)
            {
                return;
            }

            SpriteRenderer[] spriteRenderers = paddleObject.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                spriteRenderers[i].color = WithAlpha(ownerColor, spriteRenderers[i].color.a);
            }

            Renderer[] renderers = paddleObject.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] is SpriteRenderer || renderers[i] is TrailRenderer)
                {
                    continue;
                }

                ApplyMaterialColor(renderers[i], ownerColor);
            }
        }

        public static void ApplyZoneColor(Renderer zoneRenderer, Color ownerColor, float alpha)
        {
            ApplyMaterialColor(zoneRenderer, WithAlpha(ownerColor, alpha));
        }

        private static void ApplyMaterialColor(Renderer renderer, Color ownerColor)
        {
            if (renderer == null)
            {
                return;
            }

            Material material = UnityEngine.Application.isPlaying ? renderer.material : renderer.sharedMaterial;
            if (material == null)
            {
                return;
            }

            material.color = ownerColor;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", ownerColor);
            }
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }
    }
}

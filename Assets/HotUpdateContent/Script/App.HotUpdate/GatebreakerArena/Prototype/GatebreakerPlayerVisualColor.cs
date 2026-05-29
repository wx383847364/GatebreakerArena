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

        public static void ApplyBallOwnerColor(GameObject ballObject, Color ownerColor)
        {
            if (ballObject == null)
            {
                return;
            }

            TrailRenderer[] trails = ballObject.GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < trails.Length; i++)
            {
                ApplyTrailColor(trails[i], ownerColor);
            }

            ParticleSystem[] particleSystems = ballObject.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem.MainModule main = particleSystems[i].main;
                Color startColor = main.startColor.color;
                main.startColor = WithAlpha(ownerColor, startColor.a > 0f ? startColor.a : ownerColor.a);
            }

            SpriteRenderer[] spriteRenderers = ballObject.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                Color current = spriteRenderers[i].color;
                spriteRenderers[i].color = IsWhiteCoreTint(current)
                    ? WithAlpha(Color.white, current.a)
                    : WithAlpha(ownerColor, current.a);
            }

            Renderer[] renderers = ballObject.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] is SpriteRenderer || renderers[i] is TrailRenderer)
                {
                    continue;
                }

                ApplyMaterialColor(renderers[i], ownerColor);
            }
        }

        public static Gradient BuildTrailGradient(Color ownerColor)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(ownerColor, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0.28f * ownerColor.a, 0f),
                    new GradientAlphaKey(0.23f, 1f),
                });
            return gradient;
        }

        private static void ApplyTrailColor(TrailRenderer trail, Color ownerColor)
        {
            if (trail == null)
            {
                return;
            }

            trail.colorGradient = BuildTrailGradient(ownerColor);
            ApplyMaterialColor(trail, ownerColor);
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

        private static bool IsWhiteCoreTint(Color color)
        {
            return Mathf.Abs(color.r - 1f) <= 0.001f &&
                   Mathf.Abs(color.g - 1f) <= 0.001f &&
                   Mathf.Abs(color.b - 1f) <= 0.001f;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }
    }
}

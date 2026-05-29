using App.HotUpdate.GatebreakerArena.Mode;
using App.HotUpdate.GatebreakerArena.Prototype;
using NUnit.Framework;
using UnityEngine;

namespace Gatebreaker.Tests
{
    public sealed class GatebreakerPlayerVisualColorTests
    {
        [Test]
        public void PlayerColorsMapToAreaAndPaddleByPlayerId()
        {
            GatebreakerModeCatalog catalog = GatebreakerModeCatalog.CreateDefault();

            AssertVisualColor(1, catalog.GetPlayerColor(1));
            AssertVisualColor(2, catalog.GetPlayerColor(2));
            AssertVisualColor(3, catalog.GetPlayerColor(3));
            AssertVisualColor(4, catalog.GetPlayerColor(4));
        }

        private static void AssertVisualColor(int playerId, PlayerColorRuleDefinition rule)
        {
            Color expected = GatebreakerPlayerVisualColor.ToUnityColor(rule);
            var zone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var paddle = new GameObject($"Paddle {playerId}");
            Material zoneMaterial = null;
            try
            {
                zoneMaterial = new Material(Shader.Find("Sprites/Default"));
                zone.GetComponent<Renderer>().sharedMaterial = zoneMaterial;
                SpriteRenderer paddleRenderer = paddle.AddComponent<SpriteRenderer>();

                GatebreakerPlayerVisualColor.ApplyZoneColor(zone.GetComponent<Renderer>(), expected, 0.32f);
                GatebreakerPlayerVisualColor.ApplyPaddleColor(paddle, expected);

                AssertColor(expected, zone.GetComponent<Renderer>().sharedMaterial.color, ignoreAlpha: true);
                AssertColor(expected, paddleRenderer.color);
            }
            finally
            {
                Object.DestroyImmediate(zone);
                Object.DestroyImmediate(paddle);
                Object.DestroyImmediate(zoneMaterial);
            }
        }

        private static void AssertColor(Color expected, Color actual, bool ignoreAlpha = false)
        {
            Assert.AreEqual(expected.r, actual.r, 0.001f);
            Assert.AreEqual(expected.g, actual.g, 0.001f);
            Assert.AreEqual(expected.b, actual.b, 0.001f);
            if (!ignoreAlpha)
            {
                Assert.AreEqual(expected.a, actual.a, 0.001f);
            }
        }
    }
}

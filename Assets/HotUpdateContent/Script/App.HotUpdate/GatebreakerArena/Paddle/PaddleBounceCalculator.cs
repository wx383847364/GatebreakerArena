using System;
using App.HotUpdate.GatebreakerArena.Mode;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Paddle
{
    public sealed class PaddleBounceCalculator
    {
        public Vector2 CalculateBounce(
            Vector2 incomingVelocity,
            float hitOffsetNormalized,
            BallRuleDefinition rule,
            float outwardVerticalSign)
        {
            Vector2 normal = Mathf.Sign(outwardVerticalSign) >= 0f ? Vector2.up : Vector2.down;
            return CalculateBounce(incomingVelocity, hitOffsetNormalized, rule, normal, Vector2.right, null, 0f);
        }

        public Vector2 CalculateBounce(
            Vector2 incomingVelocity,
            float hitOffsetNormalized,
            BallRuleDefinition rule,
            Vector2 normal,
            Vector2 tangent,
            PaddleBounceTuning tuning = null,
            float normalizedPaddleVelocity = 0f)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            PaddleBounceTuning activeTuning = tuning ?? PaddleBounceTuning.CreateDefault();
            Vector2 safeNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector2.up;
            Vector2 safeTangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector2.right;
            float offset = Mathf.Clamp(hitOffsetNormalized, -1f, 1f);
            float paddleVelocity = Mathf.Clamp(normalizedPaddleVelocity, -1f, 1f);
            float speed = Mathf.Min(
                rule.MaxSpeed,
                Mathf.Max(rule.InitialSpeed, incomingVelocity.magnitude + rule.SpeedGainOnPaddleHit));
            Vector2 reflected = Vector2.Reflect(incomingVelocity, safeNormal);
            Vector2 baseDirection = reflected.sqrMagnitude > 0.0001f ? reflected.normalized : safeNormal;
            if (Vector2.Dot(baseDirection, safeNormal) < 0f)
            {
                baseDirection = Vector2.Reflect(baseDirection, safeNormal);
            }

            float tangentControl = offset * activeTuning.HitOffsetInfluence +
                                   paddleVelocity * activeTuning.PaddleVelocityInfluence;
            Vector2 controlledDirection = baseDirection + safeTangent * tangentControl;
            Vector2 direction = controlledDirection.sqrMagnitude > 0.0001f
                ? controlledDirection.normalized
                : safeNormal;
            float ruleMinimumShare = rule.MinVerticalVelocity / Mathf.Max(speed, 0.001f);
            float normalShare = Mathf.Max(ruleMinimumShare, activeTuning.MinimumOutwardShare);
            float outgoingNormalShare = Vector2.Dot(direction, safeNormal);
            if (outgoingNormalShare < normalShare)
            {
                float tangentDot = Vector2.Dot(direction, safeTangent);
                float tangentSign = Mathf.Abs(tangentDot) > 0.001f ? Mathf.Sign(tangentDot) : Mathf.Sign(offset);
                if (Mathf.Abs(tangentSign) <= 0.001f)
                {
                    tangentSign = 1f;
                }

                float tangentShare = Mathf.Sqrt(Mathf.Max(0f, 1f - normalShare * normalShare)) * tangentSign;
                direction = (safeNormal * normalShare + safeTangent * tangentShare).normalized;
            }

            return direction * speed * rule.PaddleBounceFactor;
        }
    }
}

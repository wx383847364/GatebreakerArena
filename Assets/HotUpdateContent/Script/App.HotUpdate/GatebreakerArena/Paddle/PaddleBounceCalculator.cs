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
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            float offset = Mathf.Clamp(hitOffsetNormalized, -1f, 1f);
            float speed = Mathf.Min(
                rule.MaxSpeed,
                Mathf.Max(rule.InitialSpeed, incomingVelocity.magnitude + rule.SpeedGainOnPaddleHit));
            float horizontal = offset;
            float vertical = Mathf.Max(rule.MinVerticalVelocity / Mathf.Max(speed, 0.001f), 0.25f);
            var direction = new Vector2(horizontal, Mathf.Sign(outwardVerticalSign) * vertical).normalized;
            return direction * speed * rule.PaddleBounceFactor;
        }
    }
}

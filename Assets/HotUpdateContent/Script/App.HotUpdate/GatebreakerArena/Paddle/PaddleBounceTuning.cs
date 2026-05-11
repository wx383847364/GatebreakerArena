using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Paddle
{
    public sealed class PaddleBounceTuning
    {
        public const int DefaultHitOffsetInfluenceValue = 90;
        public const int DefaultPaddleVelocityInfluenceValue = 55;
        public const int DefaultMinimumOutwardShareValue = 25;
        public const int HitOffsetInfluenceMin = 0;
        public const int HitOffsetInfluenceMax = 150;
        public const int PaddleVelocityInfluenceMin = 0;
        public const int PaddleVelocityInfluenceMax = 120;
        public const int MinimumOutwardShareMin = 10;
        public const int MinimumOutwardShareMax = 60;

        public PaddleBounceTuning()
        {
            ResetToDefaults();
        }

        public int HitOffsetInfluenceValue { get; private set; }
        public int PaddleVelocityInfluenceValue { get; private set; }
        public int MinimumOutwardShareValue { get; private set; }
        public float HitOffsetInfluence => ToActualValue(HitOffsetInfluenceValue);
        public float PaddleVelocityInfluence => ToActualValue(PaddleVelocityInfluenceValue);
        public float MinimumOutwardShare => ToActualValue(MinimumOutwardShareValue);

        public static PaddleBounceTuning CreateDefault()
        {
            return new PaddleBounceTuning();
        }

        public void SetHitOffsetInfluenceValue(int value)
        {
            HitOffsetInfluenceValue = Mathf.Clamp(value, HitOffsetInfluenceMin, HitOffsetInfluenceMax);
        }

        public void SetPaddleVelocityInfluenceValue(int value)
        {
            PaddleVelocityInfluenceValue = Mathf.Clamp(value, PaddleVelocityInfluenceMin, PaddleVelocityInfluenceMax);
        }

        public void SetMinimumOutwardShareValue(int value)
        {
            MinimumOutwardShareValue = Mathf.Clamp(value, MinimumOutwardShareMin, MinimumOutwardShareMax);
        }

        public void ResetToDefaults()
        {
            HitOffsetInfluenceValue = DefaultHitOffsetInfluenceValue;
            PaddleVelocityInfluenceValue = DefaultPaddleVelocityInfluenceValue;
            MinimumOutwardShareValue = DefaultMinimumOutwardShareValue;
        }

        private static float ToActualValue(int panelValue)
        {
            return panelValue * 0.01f;
        }
    }
}

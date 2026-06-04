using UnityEngine;

namespace App.AOT.Bootstrap
{
    /// <summary>
    /// Host-level frame pacing policy. Gameplay simulation fps stays in HotUpdate match/lockstep rules.
    /// </summary>
    public static class RuntimeFrameRateSettings
    {
        public const int MaxDisplayFps = 60;

        public static void Apply()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = MaxDisplayFps;
        }
    }
}

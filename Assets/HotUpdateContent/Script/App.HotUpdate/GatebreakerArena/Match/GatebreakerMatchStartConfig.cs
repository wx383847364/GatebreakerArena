using System.Collections.Generic;

namespace App.HotUpdate.GatebreakerArena.Match
{
    public sealed class GatebreakerMatchStartConfig
    {
        public const int DefaultSimulationFps = 30;
        public const int DefaultInputDelayFrames = 3;

        public string MatchId { get; set; }
        public int Seed { get; set; }
        public int SimulationFps { get; set; } = DefaultSimulationFps;
        public int InputDelayFrames { get; set; } = DefaultInputDelayFrames;
        public string ModeId { get; set; } = "PVE_STANDARD";
        public string MapId { get; set; } = "MAP_ARENA_01";
        public string BallTypeId { get; set; } = "BALL_NORMAL";
        public IReadOnlyList<int> ActiveSlots { get; set; }
        public IReadOnlyList<GatebreakerMatchPlayerSlot> PlayerSlots { get; set; }
        public int LocalPlayerId { get; set; } = 1;
        public string ConfigHash { get; set; }
        public string TuningHash { get; set; }
        public IReadOnlyDictionary<string, int> TuningValues { get; set; }
    }

    public sealed class GatebreakerMatchPlayerSlot
    {
        public int SlotIndex { get; set; } = -1;
        public int SideOrder { get; set; } = -1;
        public int PlayerId { get; set; }
        public bool IsAi { get; set; }
        // These selections are part of the deterministic match contract. Runtime systems
        // copy them into HeroRuntimeState before any hero or chip rules are applied.
        public string HeroId { get; set; } = string.Empty;
        public IReadOnlyList<string> DeckChipIds { get; set; } = new string[0];
    }
}

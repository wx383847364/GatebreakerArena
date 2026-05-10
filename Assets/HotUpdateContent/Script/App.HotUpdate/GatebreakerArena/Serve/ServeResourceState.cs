using App.HotUpdate.GatebreakerArena.Core;

namespace App.HotUpdate.GatebreakerArena.Serve
{
    public sealed class ServeResourceState
    {
        public int CurrentServeAmmo { get; set; }
        public int MaxServeAmmo { get; set; }
        public int OwnedBallsInField { get; set; }
        public int MaxOwnedBallsInField { get; set; }
        public float ServeCooldownRemaining { get; set; }
        public float BaseServeCooldown { get; set; }
        public ServeBlockReason LastBlockReason { get; set; }

        public bool IsReady => LastBlockReason == ServeBlockReason.None;
    }
}

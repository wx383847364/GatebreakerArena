using App.HotUpdate.GatebreakerArena.Paddle;
using App.HotUpdate.GatebreakerArena.Serve;
using App.HotUpdate.GatebreakerArena.Zone;
using App.HotUpdate.GatebreakerArena.Mode;
using App.HotUpdate.GatebreakerArena.Hero;

namespace App.HotUpdate.GatebreakerArena.Match
{
    public sealed class PlayerRuntimeState
    {
        public int PlayerId { get; set; }
        public int TeamId { get; set; }
        public bool IsLocalPlayer { get; set; }
        public bool IsAi { get; set; }
        public bool IsDisabled { get; set; }
        public int Score { get; set; }
        public int HitScore { get; set; }
        public int TrueScore => Score + HitScore;
        public int ScoreReachOrder { get; set; }
        public ServeResourceState ServeResource { get; set; }
        public PaddleRuntimeState Paddle { get; set; }
        public ZoneRuntimeState Zone { get; set; }
        public HeroRuntimeState Hero { get; set; } = new HeroRuntimeState();
        public HeroCombatState HeroCombat { get; set; } = new HeroCombatState();
    }
}

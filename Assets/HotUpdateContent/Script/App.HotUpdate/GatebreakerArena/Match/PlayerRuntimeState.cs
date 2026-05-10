using App.HotUpdate.GatebreakerArena.Serve;

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
        public ServeResourceState ServeResource { get; set; }
    }
}

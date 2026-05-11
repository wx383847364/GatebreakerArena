using System;
using System.Linq;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Match;

namespace App.HotUpdate.GatebreakerArena.UI
{
    public sealed class GatebreakerArenaHudPresenter
    {
        private readonly GatebreakerMatchRuntime _runtime;

        public GatebreakerArenaHudPresenter(GatebreakerMatchRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public GatebreakerHudSnapshot BuildSnapshot(int localPlayerId)
        {
            PlayerRuntimeState localPlayer = _runtime.FindPlayer(localPlayerId);
            ScoreboardSnapshot scoreboard = _runtime.CreateScoreboardSnapshot();
            return new GatebreakerHudSnapshot
            {
                Phase = scoreboard.Phase,
                RemainingTime = scoreboard.RemainingTime,
                PlayerScores = scoreboard.PlayerScores,
                LocalPlayerId = localPlayerId,
                HasWinner = scoreboard.HasWinner,
                WinnerPlayerId = scoreboard.WinnerPlayerId,
                CurrentServeAmmo = localPlayer?.ServeResource?.CurrentServeAmmo ?? 0,
                MaxServeAmmo = localPlayer?.ServeResource?.MaxServeAmmo ?? 0,
                OwnedBallsInField = localPlayer?.ServeResource?.OwnedBallsInField ?? 0,
                MaxOwnedBallsInField = localPlayer?.ServeResource?.MaxOwnedBallsInField ?? 0,
                ServeCooldownRemaining = localPlayer?.ServeResource?.ServeCooldownRemaining ?? 0f,
                ServeBlockReason = localPlayer?.ServeResource?.LastBlockReason ?? ServeBlockReason.PlayerDisabled,
                IsOvertime = scoreboard.Phase == MatchPhase.Overtime,
                HasDanger = _runtime.Balls.Any(ball => ball.OwnerPlayerId != localPlayerId),
            };
        }
    }

    public sealed class GatebreakerHudSnapshot
    {
        public MatchPhase Phase { get; set; }
        public float RemainingTime { get; set; }
        public System.Collections.Generic.IReadOnlyList<PlayerScoreSnapshot> PlayerScores { get; set; }
        public int LocalPlayerId { get; set; }
        public bool HasWinner { get; set; }
        public int WinnerPlayerId { get; set; }
        public int CurrentServeAmmo { get; set; }
        public int MaxServeAmmo { get; set; }
        public int OwnedBallsInField { get; set; }
        public int MaxOwnedBallsInField { get; set; }
        public float ServeCooldownRemaining { get; set; }
        public ServeBlockReason ServeBlockReason { get; set; }
        public bool IsOvertime { get; set; }
        public bool HasDanger { get; set; }
    }
}

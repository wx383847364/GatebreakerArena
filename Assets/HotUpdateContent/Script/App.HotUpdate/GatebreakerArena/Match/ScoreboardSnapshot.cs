using System.Collections.Generic;
using App.HotUpdate.GatebreakerArena.Core;

namespace App.HotUpdate.GatebreakerArena.Match
{
    public sealed class ScoreboardSnapshot
    {
        public MatchPhase Phase { get; set; }
        public float RemainingTime { get; set; }
        public IReadOnlyList<PlayerScoreSnapshot> PlayerScores { get; set; }
        public IReadOnlyList<int> OvertimeEligiblePlayerIds { get; set; }
        public int WinnerPlayerId { get; set; }
        public bool HasWinner { get; set; }
    }

    public readonly struct PlayerScoreSnapshot
    {
        public PlayerScoreSnapshot(
            int playerId,
            int teamId,
            bool isDisabled,
            int score,
            int hitScore,
            int scoreReachOrder)
        {
            PlayerId = playerId;
            TeamId = teamId;
            IsDisabled = isDisabled;
            Score = score;
            HitScore = hitScore;
            TrueScore = score + hitScore;
            ScoreReachOrder = scoreReachOrder;
        }

        public int PlayerId { get; }
        public int TeamId { get; }
        public bool IsDisabled { get; }
        public int Score { get; }
        public int HitScore { get; }
        public int TrueScore { get; }
        public int ScoreReachOrder { get; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.GatebreakerArena.Core;

namespace App.HotUpdate.GatebreakerArena.Match
{
    public sealed class ScoreSystem
    {
        public void RecordGoal(
            IReadOnlyList<PlayerRuntimeState> players,
            int scoringPlayerId,
            int hitPlayerId,
            ref int nextScoreReachOrder)
        {
            if (players == null)
            {
                throw new ArgumentNullException(nameof(players));
            }

            PlayerRuntimeState scorer = players.FirstOrDefault(item => item.PlayerId == scoringPlayerId && !item.IsDisabled);
            if (scorer != null)
            {
                scorer.Score += 1;
                scorer.ScoreReachOrder = nextScoreReachOrder++;
            }

            PlayerRuntimeState hitPlayer = players.FirstOrDefault(item => item.PlayerId == hitPlayerId && !item.IsDisabled);
            if (hitPlayer != null)
            {
                hitPlayer.HitScore -= 1;
            }
        }

        public IReadOnlyList<int> GetTopRankedPlayerIds(IReadOnlyList<PlayerRuntimeState> players, bool includeStableTieBreaker)
        {
            if (players == null || players.Count == 0)
            {
                return Array.Empty<int>();
            }

            PlayerRuntimeState leader = GetRankedPlayers(players, includeStableTieBreaker).FirstOrDefault();
            if (leader == null)
            {
                return Array.Empty<int>();
            }

            return GetRankedPlayers(players, includeStableTieBreaker)
                .Where(player => CompareRank(player, leader, includeStableTieBreaker) == 0)
                .Select(player => player.PlayerId)
                .ToArray();
        }

        public IReadOnlyList<PlayerRuntimeState> GetRankedPlayers(IReadOnlyList<PlayerRuntimeState> players, bool includeStableTieBreaker = true)
        {
            if (players == null || players.Count == 0)
            {
                return Array.Empty<PlayerRuntimeState>();
            }

            return players
                .Where(player => player != null && !player.IsDisabled)
                .OrderByDescending(player => player.Score)
                .ThenByDescending(player => player.TrueScore)
                .ThenBy(player => NormalizeScoreReachOrder(player))
                .ThenBy(player => includeStableTieBreaker ? player.PlayerId : 0)
                .ToArray();
        }

        public ScoreboardSnapshot CreateSnapshot(
            IReadOnlyList<PlayerRuntimeState> players,
            MatchPhase phase,
            float remainingTime,
            IReadOnlyList<int> overtimeEligiblePlayerIds,
            bool hasWinner,
            int winnerPlayerId)
        {
            return new ScoreboardSnapshot
            {
                Phase = phase,
                RemainingTime = remainingTime,
                PlayerScores = GetRankedPlayers(players).Select(player => new PlayerScoreSnapshot(
                    player.PlayerId,
                    player.TeamId,
                    player.IsDisabled,
                    player.Score,
                    player.HitScore,
                    player.ScoreReachOrder)).ToArray(),
                OvertimeEligiblePlayerIds = overtimeEligiblePlayerIds ?? Array.Empty<int>(),
                HasWinner = hasWinner,
                WinnerPlayerId = winnerPlayerId,
            };
        }

        private static int CompareRank(PlayerRuntimeState left, PlayerRuntimeState right, bool includeStableTieBreaker)
        {
            int scoreCompare = right.Score.CompareTo(left.Score);
            if (scoreCompare != 0)
            {
                return scoreCompare;
            }

            int trueScoreCompare = right.TrueScore.CompareTo(left.TrueScore);
            if (trueScoreCompare != 0)
            {
                return trueScoreCompare;
            }

            int reachOrderCompare = NormalizeScoreReachOrder(left).CompareTo(NormalizeScoreReachOrder(right));
            if (reachOrderCompare != 0)
            {
                return reachOrderCompare;
            }

            return includeStableTieBreaker ? left.PlayerId.CompareTo(right.PlayerId) : 0;
        }

        private static int NormalizeScoreReachOrder(PlayerRuntimeState player)
        {
            return player != null && player.ScoreReachOrder > 0
                ? player.ScoreReachOrder
                : int.MaxValue;
        }
    }
}

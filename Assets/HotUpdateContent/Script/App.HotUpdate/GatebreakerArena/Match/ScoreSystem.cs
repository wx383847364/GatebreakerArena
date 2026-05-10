using System;
using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Mode;

namespace App.HotUpdate.GatebreakerArena.Match
{
    public sealed class ScoreSystem
    {
        public void AddScore(IReadOnlyList<PlayerRuntimeState> players, ModeRuleDefinition modeRule, int scoringPlayerId, int scoringTeamId)
        {
            if (players == null)
            {
                throw new ArgumentNullException(nameof(players));
            }

            if (modeRule != null && modeRule.ScoreRuleType == ScoreRuleType.TeamScore)
            {
                foreach (PlayerRuntimeState player in players)
                {
                    if (player.TeamId == scoringTeamId)
                    {
                        player.Score += 1;
                    }
                }

                return;
            }

            PlayerRuntimeState scorer = players.FirstOrDefault(item => item.PlayerId == scoringPlayerId);
            if (scorer != null)
            {
                scorer.Score += 1;
            }
        }

        public IReadOnlyList<int> GetHighestScoringPlayerIds(IReadOnlyList<PlayerRuntimeState> players)
        {
            if (players == null || players.Count == 0)
            {
                return Array.Empty<int>();
            }

            int highScore = players.Max(player => player.Score);
            return players.Where(player => player.Score == highScore).Select(player => player.PlayerId).ToArray();
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
                PlayerScores = players.Select(player => new PlayerScoreSnapshot(player.PlayerId, player.TeamId, player.Score)).ToArray(),
                OvertimeEligiblePlayerIds = overtimeEligiblePlayerIds ?? Array.Empty<int>(),
                HasWinner = hasWinner,
                WinnerPlayerId = winnerPlayerId,
            };
        }
    }
}

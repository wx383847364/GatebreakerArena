using App.HotUpdate.GatebreakerArena.Match;
using NUnit.Framework;
using System.Collections.Generic;

namespace Gatebreaker.Tests
{
    public sealed class GatebreakerScoreSystemTests
    {
        [Test]
        public void RankedPlayersUseScoreThenTrueScoreThenReachOrderThenPlayerId()
        {
            var players = new[]
            {
                new PlayerRuntimeState { PlayerId = 4, TeamId = 4, Score = 2, HitScore = -1, ScoreReachOrder = 3 },
                new PlayerRuntimeState { PlayerId = 2, TeamId = 2, Score = 2, HitScore = -1, ScoreReachOrder = 1 },
                new PlayerRuntimeState { PlayerId = 3, TeamId = 3, Score = 2, HitScore = -2, ScoreReachOrder = 2 },
                new PlayerRuntimeState { PlayerId = 1, TeamId = 1, Score = 1, HitScore = 0, ScoreReachOrder = 4 },
            };
            var scoreSystem = new ScoreSystem();

            IReadOnlyList<PlayerRuntimeState> ranked = scoreSystem.GetRankedPlayers(players);

            Assert.AreEqual(2, ranked[0].PlayerId);
            Assert.AreEqual(4, ranked[1].PlayerId);
            Assert.AreEqual(3, ranked[2].PlayerId);
            Assert.AreEqual(1, ranked[3].PlayerId);
        }

        [Test]
        public void TopRankedPlayersCanIgnorePlayerIdForOvertimeEligibility()
        {
            var players = new[]
            {
                new PlayerRuntimeState { PlayerId = 2, TeamId = 2, Score = 0, HitScore = 0, ScoreReachOrder = 0 },
                new PlayerRuntimeState { PlayerId = 1, TeamId = 1, Score = 0, HitScore = 0, ScoreReachOrder = 0 },
            };
            var scoreSystem = new ScoreSystem();

            CollectionAssert.AreEquivalent(
                new[] { 1, 2 },
                scoreSystem.GetTopRankedPlayerIds(players, false));
            CollectionAssert.AreEqual(
                new[] { 1 },
                scoreSystem.GetTopRankedPlayerIds(players, true));
        }

        [Test]
        public void RankedPlayersIgnoreDisabledPlaceholders()
        {
            var players = new[]
            {
                new PlayerRuntimeState { PlayerId = 1, TeamId = 1, IsDisabled = true, Score = 99, HitScore = 0, ScoreReachOrder = 1 },
                new PlayerRuntimeState { PlayerId = 2, TeamId = 2, Score = 1, HitScore = 0, ScoreReachOrder = 2 },
                new PlayerRuntimeState { PlayerId = 3, TeamId = 3, Score = 2, HitScore = -1, ScoreReachOrder = 3 },
            };
            var scoreSystem = new ScoreSystem();

            IReadOnlyList<PlayerRuntimeState> ranked = scoreSystem.GetRankedPlayers(players);

            Assert.AreEqual(2, ranked.Count);
            Assert.AreEqual(3, ranked[0].PlayerId);
            Assert.AreEqual(2, ranked[1].PlayerId);
            CollectionAssert.AreEqual(
                new[] { 3 },
                scoreSystem.GetTopRankedPlayerIds(players, true));
        }
    }
}

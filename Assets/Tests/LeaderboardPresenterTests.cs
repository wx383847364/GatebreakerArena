using System.Collections.Generic;
using App.HotUpdate.GatebreakerArena.UI;
using NUnit.Framework;

namespace Gatebreaker.Tests
{
    public sealed class LeaderboardPresenterTests
    {
        [Test]
        public void RankingUsesRatingThenWinsThenLossesThenPlayerId()
        {
            var entries = new List<LeaderboardEntry>
            {
                new LeaderboardEntry("d", "D", "脉冲", 1000, 8, 2),
                new LeaderboardEntry("c", "C", "脉冲", 1200, 4, 1),
                new LeaderboardEntry("b", "B", "脉冲", 1200, 5, 3),
                new LeaderboardEntry("a", "A", "脉冲", 1200, 5, 2),
            };

            IReadOnlyList<LeaderboardEntry> ranked = LeaderboardRanking.Sort(entries);

            Assert.AreEqual("a", ranked[0].PlayerId);
            Assert.AreEqual("b", ranked[1].PlayerId);
            Assert.AreEqual("c", ranked[2].PlayerId);
            Assert.AreEqual("d", ranked[3].PlayerId);
        }

        [TestCase(0, 0, 0)]
        [TestCase(1, 2, 33)]
        [TestCase(2, 1, 67)]
        [TestCase(5, 0, 100)]
        public void WinRateIsRoundedFromCompletedMatches(int wins, int losses, int expectedPercent)
        {
            var entry = new LeaderboardEntry("player", "Player", "脉冲", 1000, wins, losses);

            Assert.AreEqual(wins + losses, entry.Matches);
            Assert.AreEqual(expectedPercent, entry.WinRatePercent);
        }

        [Test]
        public void MockDataContainsExactlyOneLocalPlayerAndIsNotAlreadyRequiredToBeSorted()
        {
            IReadOnlyList<LeaderboardEntry> entries = new LocalMockLeaderboardDataSource().LoadEntries();
            int localPlayerCount = 0;
            foreach (LeaderboardEntry entry in entries)
            {
                if (entry.IsLocalPlayer)
                {
                    localPlayerCount++;
                }
            }

            Assert.Greater(entries.Count, 10);
            Assert.AreEqual(1, localPlayerCount);
        }
    }
}

using App.HotUpdate.GatebreakerArena.Application;
using App.HotUpdate.GatebreakerArena.Ball;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Match;
using App.HotUpdate.GatebreakerArena.Mode;
using App.HotUpdate.GatebreakerArena.Network;
using App.HotUpdate.GatebreakerArena.Serve;
using App.HotUpdate.GatebreakerArena.Zone;
using NUnit.Framework;
using UnityEngine;

namespace Gatebreaker.Tests
{
    public sealed class GatebreakerHeroContractTests
    {
        [Test]
        public void DeterministicPrng_SameSeedProducesSameSequenceAndRejectsInvalidBounds()
        {
            var left = new GatebreakerDeterministicPrng(12345u);
            var right = new GatebreakerDeterministicPrng(12345u);

            for (int i = 0; i < 32; i++)
            {
                Assert.AreEqual(left.NextUInt(), right.NextUInt());
            }

            Assert.Throws<System.ArgumentOutOfRangeException>(() => left.NextInt(0));
        }

        [Test]
        public void LockstepConverter_RoundTripsAbilityButtonAlongsideServeButton()
        {
            var input = new PlayerInputFrame(7, 0.5f, true, Vector2.up, true);
            LockstepInputFrame network = GatebreakerLockstepInputConverter.FromPlayerInputFrame(input, 2, 9, 3);

            Assert.AreEqual(
                GatebreakerLockstepInputConverter.ServeButton | GatebreakerLockstepInputConverter.AbilityButton,
                network.Buttons);

            PlayerInputFrame restored = GatebreakerLockstepInputConverter.ToPlayerInputFrame(network);
            GatebreakerFrameInput gameplay = GatebreakerLockstepInputConverter.ToGatebreakerFrameInput(network);
            Assert.IsTrue(restored.AbilityPressed);
            Assert.IsTrue(gameplay.AbilityPressed);
            Assert.IsTrue(restored.ServePressed);
        }

        [Test]
        public void StartConfig_InitializesStableHeroStateAndChecksumIncludesIt()
        {
            GatebreakerMatchRuntime frost = CreateRuntime("HERO_FROST_QUEEN", new[] { "FLOW_SPEED", "GUARD_LENGTH", "STRIKE_POWER" });
            GatebreakerMatchRuntime thorn = CreateRuntime("HERO_THORN_GUARDIAN", new[] { "GUARD_LENGTH", "FLOW_SPEED", "STRIKE_POWER" });

            PlayerRuntimeState player = frost.FindPlayer(1);
            Assert.AreEqual("HERO_FROST_QUEEN", player.Hero.HeroId);
            CollectionAssert.AreEqual(new[] { "FLOW_SPEED", "GUARD_LENGTH", "STRIKE_POWER" }, player.Hero.DeckChipIds);
            Assert.AreEqual(3, player.Hero.ActiveChipIds.Count);
            Assert.AreNotEqual(frost.CreateChecksum(0), thorn.CreateChecksum(0));
        }

        private static GatebreakerMatchRuntime CreateRuntime(string heroId, string[] deck)
        {
            var runtime = new GatebreakerMatchRuntime(
                GatebreakerModeCatalog.CreateDefault(),
                new BallSimulationSystem(),
                new ServeResourceSystem(),
                new GoalJudgeSystem(),
                new ScoreSystem(),
                null);
            runtime.StartMatch(new GatebreakerMatchStartConfig
            {
                Seed = 12,
                ModeId = "PVE_STANDARD",
                MapId = "MAP_ARENA_01",
                BallTypeId = "BALL_NORMAL",
                LocalPlayerId = 1,
                PlayerSlots = new[]
                {
                    new GatebreakerMatchPlayerSlot
                    {
                        SlotIndex = 0,
                        SideOrder = 0,
                        PlayerId = 1,
                        HeroId = heroId,
                        DeckChipIds = deck,
                    },
                    new GatebreakerMatchPlayerSlot
                    {
                        SlotIndex = 1,
                        SideOrder = 1,
                        PlayerId = 2,
                    },
                },
            });
            return runtime;
        }
    }
}

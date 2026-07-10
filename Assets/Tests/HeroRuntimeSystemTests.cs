using System.Collections.Generic;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Hero;
using App.HotUpdate.GatebreakerArena.Mode;
using NUnit.Framework;

namespace Gatebreaker.Tests
{
    public sealed class HeroRuntimeSystemTests
    {
        private readonly HeroRuntimeSystem _system = new HeroRuntimeSystem();

        [Test]
        public void Initialize_ResolvesBothMatchingPathsAtM2()
        {
            HeroDefinition hero = CreateHero(HeroRuntimeSystem.FrostQueenId);
            HeroPathDefinition[] paths =
            {
                CreatePath("PATH_FROST_EXTREME", hero.HeroId, ChipCategory.Strike, ChipCategory.Guard),
                CreatePath("PATH_FROST_CRYSTAL", hero.HeroId, ChipCategory.Guard, ChipCategory.Flow),
            };
            var runtime = new HeroRuntimeState();
            var combat = new HeroCombatState();

            _system.Initialize(hero, paths, new[]
            {
                CreateChip("A", ChipCategory.Strike),
                CreateChip("B", ChipCategory.Guard),
                CreateChip("C", ChipCategory.Flow),
            }, runtime, combat);

            Assert.AreEqual(2, runtime.PathStates.Count);
            Assert.AreEqual(2, runtime.PathStates[0].Level);
            Assert.AreEqual(2, runtime.PathStates[1].Level);
            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, runtime.ActiveChipIds);
        }

        [Test]
        public void FrostExtremeM2_FreezesTargetPaddleAndAllTargetBalls()
        {
            HeroDefinition hero = CreateHero(HeroRuntimeSystem.FrostQueenId);
            HeroPathDefinition[] paths =
            {
                CreatePath("PATH_FROST_EXTREME", hero.HeroId, ChipCategory.Strike, ChipCategory.Guard),
            };
            HeroRuntimeState runtime = Initialize(hero, paths, ChipCategory.Strike, ChipCategory.Guard);
            var combat = new HeroCombatState { HeroId = hero.HeroId };

            HeroRuntimeEventResult result = null;
            for (int i = 0; i < 10; i++)
            {
                result = _system.HandleEvent(hero, paths, runtime, combat,
                    new HeroRuntimeEvent(HeroRuntimeEventType.OpponentPaddleHit, otherPlayerId: 2, ballId: 10));
            }

            Assert.IsNotNull(result);
            Assert.AreEqual(36, result.Effects.TargetPaddleFreezeFrames);
            Assert.AreEqual(23, result.Effects.TargetAllBallsFreezeFrames);
            Assert.AreEqual(0, combat.FrostByOpponent[0].Amount);
        }

        [Test]
        public void ThornM2_ConcededGoalGrowsPaddleAndPunishesScorer()
        {
            HeroDefinition hero = CreateHero(HeroRuntimeSystem.ThornGuardianId);
            HeroPathDefinition[] paths =
            {
                CreatePath("PATH_THORN", hero.HeroId, ChipCategory.Strike, ChipCategory.Guard),
                CreatePath("PATH_GROWTH", hero.HeroId, ChipCategory.Guard, ChipCategory.Flow),
            };
            HeroRuntimeState runtime = Initialize(hero, paths, ChipCategory.Strike, ChipCategory.Guard, ChipCategory.Flow);
            var combat = new HeroCombatState { HeroId = hero.HeroId };

            HeroRuntimeEventResult result = _system.HandleEvent(hero, paths, runtime, combat,
                new HeroRuntimeEvent(HeroRuntimeEventType.ConcededGoal, otherPlayerId: 2));

            Assert.AreEqual(1, combat.ThornGrowthStacks);
            Assert.AreEqual(-1, result.Effects.TargetServeAmmoDelta);
            Assert.AreEqual(90, result.Effects.TargetPaddleSlowFrames);
            Assert.AreEqual(0.8f, result.Effects.TargetPaddleMoveSpeedMultiplier);
            Assert.AreEqual(1.05f, result.Effects.OwnPaddleLengthMultiplier);
        }

        [Test]
        public void RadiantBrillianceM2_BurstAndShieldProduceConfiguredPersistentEffects()
        {
            HeroDefinition hero = CreateHero(HeroRuntimeSystem.RadiantPaladinId);
            HeroPathDefinition[] paths =
            {
                CreatePath("PATH_HOLY", hero.HeroId, ChipCategory.Strike, ChipCategory.Guard),
                CreatePath("PATH_BRILLIANCE", hero.HeroId, ChipCategory.Strike, ChipCategory.Flow),
            };
            HeroRuntimeState runtime = Initialize(hero, paths, ChipCategory.Strike, ChipCategory.Guard, ChipCategory.Flow);
            var combat = new HeroCombatState { HeroId = hero.HeroId, RadianceStacks = 5 };

            HeroRuntimeEventResult burst = _system.HandleEvent(hero, paths, runtime, combat,
                new HeroRuntimeEvent(HeroRuntimeEventType.OwnPaddleHit, ballId: 7));
            HeroRuntimeEventResult shield = _system.HandleEvent(hero, paths, runtime, combat,
                new HeroRuntimeEvent(HeroRuntimeEventType.AbilityPressed));
            HeroEffectBundle persistent = _system.GetPersistentEffects(hero, paths, runtime, combat);

            Assert.AreEqual(2f, burst.Effects.OwnPaddleBounceSpeedMultiplier);
            Assert.AreEqual(90, burst.Effects.OwnTeamBallSpeedBoostFrames);
            Assert.IsTrue(shield.AbilityActivated);
            Assert.AreEqual(90, shield.Effects.OwnGoalImmuneFrames);
            Assert.AreEqual(1.5f, persistent.OwnPaddleBounceSpeedMultiplier);
            Assert.AreEqual(1.5f, persistent.OwnPaddleMoveSpeedMultiplier);
            Assert.AreEqual(1.5f, persistent.OwnBallSpeedMultiplier);
        }

        [Test]
        public void IceCrystalM2_TracksOneBallUpToFortyFivePercentAndRedirectsAtEightDegrees()
        {
            HeroDefinition hero = CreateHero(HeroRuntimeSystem.FrostQueenId);
            HeroPathDefinition[] paths =
            {
                CreatePath("PATH_CRYSTAL", hero.HeroId, ChipCategory.Guard, ChipCategory.Flow),
            };
            HeroRuntimeState runtime = Initialize(hero, paths, ChipCategory.Guard, ChipCategory.Flow);
            var combat = new HeroCombatState { HeroId = hero.HeroId };

            HeroRuntimeEventResult result = null;
            for (int i = 0; i < 4; i++)
            {
                result = _system.HandleEvent(hero, paths, runtime, combat,
                    new HeroRuntimeEvent(HeroRuntimeEventType.OpponentPaddleHit, otherPlayerId: 2, ballId: 3));
            }

            Assert.AreEqual(1.15f, result.Effects.OwnBallSpeedMultiplier);
            Assert.AreEqual(8f, result.Effects.BounceRedirectMaxDegrees);
            Assert.AreEqual(1.45f, _system.GetIceCrystalBallSpeedMultiplier(combat, 3));
        }

        [Test]
        public void AiAbilityDecision_UsesFrozenV1ThresholdsAndCooldown()
        {
            HeroDefinition frost = CreateHero(HeroRuntimeSystem.FrostQueenId);
            var runtime = new HeroRuntimeState();
            var combat = new HeroCombatState();

            Assert.IsFalse(_system.ShouldAiUseAbility(frost, runtime, combat, new HeroAiAbilityDecisionInput(49, false)));
            Assert.IsTrue(_system.ShouldAiUseAbility(frost, runtime, combat, new HeroAiAbilityDecisionInput(50, false)));
            runtime.AbilityCooldownRemainingFrames = 1;
            Assert.IsFalse(_system.ShouldAiUseAbility(frost, runtime, combat, new HeroAiAbilityDecisionInput(100, true)));
        }

        private HeroRuntimeState Initialize(HeroDefinition hero, HeroPathDefinition[] paths, params ChipCategory[] categories)
        {
            var chips = new List<UniversalChipDefinition>();
            for (int i = 0; i < categories.Length; i++)
            {
                chips.Add(CreateChip("CHIP_" + i, categories[i]));
            }

            var runtime = new HeroRuntimeState();
            _system.Initialize(hero, paths, chips, runtime, new HeroCombatState());
            return runtime;
        }

        private static HeroDefinition CreateHero(string id)
        {
            return new HeroDefinition
            {
                HeroId = id,
                ActiveAbilityCooldownSeconds = 20f,
            };
        }

        private static HeroPathDefinition CreatePath(string id, string heroId, ChipCategory first, ChipCategory second)
        {
            return new HeroPathDefinition
            {
                PathId = id,
                HeroId = heroId,
                ResonanceCategories = new[] { first, second },
            };
        }

        private static UniversalChipDefinition CreateChip(string id, ChipCategory category)
        {
            return new UniversalChipDefinition
            {
                ChipId = id,
                Category = category,
            };
        }
    }
}

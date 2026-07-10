using System;
using App.HotUpdate.GatebreakerArena.Chip;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Mode;
using NUnit.Framework;

namespace Gatebreaker.Tests
{
    public sealed class GatebreakerChipRulesTests
    {
        [Test]
        public void DeckValidator_RejectsInvalidDecksAndReturnsCanonicalOrder()
        {
            GatebreakerModeCatalog catalog = CreateCatalog();

            DeckValidationResult valid = DeckValidator.Validate(catalog, new[] { "FLOW_SPEED", "STRIKE_POWER", "GUARD_LENGTH" });
            DeckValidationResult duplicate = DeckValidator.Validate(catalog, new[] { "FLOW_SPEED", "FLOW_SPEED", "GUARD_LENGTH" });
            DeckValidationResult tooManyOfOneCategory = DeckValidator.Validate(catalog, new[] { "STRIKE_POWER", "STRIKE_SERVE", "STRIKE_OVERCHARGE", "GUARD_LENGTH" });
            DeckValidationResult unknown = DeckValidator.Validate(catalog, new[] { "FLOW_SPEED", "GUARD_LENGTH", "NOT_A_CHIP" });

            Assert.IsTrue(valid.IsValid);
            CollectionAssert.AreEqual(new[] { "FLOW_SPEED", "GUARD_LENGTH", "STRIKE_POWER" }, valid.CanonicalDeckChipIds);
            Assert.AreEqual(DeckValidationFailure.DuplicateChip, duplicate.Failure);
            Assert.AreEqual(DeckValidationFailure.CategoryLimitExceeded, tooManyOfOneCategory.Failure);
            Assert.AreEqual(DeckValidationFailure.UnknownOrUnavailableChip, unknown.Failure);
        }

        [Test]
        public void Awakener_IsDeterministicDrawsWithoutReplacementAndCalculatesBothPaths()
        {
            GatebreakerModeCatalog catalog = CreateCatalog();
            string[] deck = { "FLOW_SPEED", "GUARD_LENGTH", "STRIKE_POWER" };

            AwakeningResult first = ResonanceAwakener.Awaken(catalog, 1337, 1, "HERO_FROST_QUEEN", deck);
            AwakeningResult second = ResonanceAwakener.Awaken(catalog, 1337, 1, "HERO_FROST_QUEEN", deck);

            Assert.IsTrue(first.IsValid, first.Error);
            CollectionAssert.AreEqual(first.ActiveChipIds, second.ActiveChipIds);
            CollectionAssert.AllItemsAreUnique(first.ActiveChipIds);
            Assert.AreEqual(3, first.ActiveChipIds.Count);
            Assert.AreEqual(2, first.PathStates[0].Level);
            Assert.AreEqual(2, first.PathStates[1].Level);
        }

        [Test]
        public void ChipRuleInjector_AppliesConditionalModifiersInIdOrderAndClampsRedlines()
        {
            GatebreakerModeCatalog catalog = CreateCatalog();
            ChipRuleSnapshot rules = ChipRuleInjector.Inject(
                catalog,
                "HERO_FROST_QUEEN",
                new[] { new HeroPathRuntimeState { PathId = "PATH_FROST_EXTREME", Level = 2 } },
                new[] { "STRIKE_POWER", "FLOW_CAPACITY", "CHAOS_DISRUPT" },
                new ChipRuleBaseValues { MaxBallsInMatch = 99, MaxServeAmmo = 2 });

            Assert.AreEqual(3f, rules.PaddleBounceSpeedMultiplier);
            Assert.AreEqual(3, rules.MaxServeAmmo);
            Assert.AreEqual(0.85f, rules.EnemyPaddleMoveSpeedMultiplier);
            Assert.AreEqual(1.5f, rules.EnemyPaddleSlowDurationSeconds);
            Assert.AreEqual(25, rules.MaxBallsInMatch);
        }

        private static GatebreakerModeCatalog CreateCatalog()
        {
            UniversalChipDefinition[] chips =
            {
                Chip("STRIKE_POWER", ChipCategory.Strike, new UniversalChipModifierDefinition { ModifierType = "PaddleBounceSpeedMultiplier", Op = ModifierOp.Multiply, ValueLv1 = 1.2f },
                    new UniversalChipConditionalModifierDefinition { HeroId = "HERO_FROST_QUEEN", PathId = "PATH_FROST_EXTREME", MinimumPathLevel = 2, ModifierType = "PaddleBounceSpeedMultiplier", Op = ModifierOp.Multiply, Value = 3f }),
                Chip("STRIKE_SERVE", ChipCategory.Strike),
                Chip("STRIKE_OVERCHARGE", ChipCategory.Strike),
                Chip("GUARD_LENGTH", ChipCategory.Guard),
                Chip("GUARD_GOAL", ChipCategory.Guard),
                Chip("GUARD_BOUNCE", ChipCategory.Guard),
                Chip("FLOW_SPEED", ChipCategory.Flow),
                Chip("FLOW_AMMO", ChipCategory.Flow),
                Chip("FLOW_CAPACITY", ChipCategory.Flow, new UniversalChipModifierDefinition { ModifierType = "ServeAmmoCapacity", Op = ModifierOp.Add, ValueLv1 = 1f }),
                Chip("CHAOS_SPIN", ChipCategory.Chaos),
                Chip("CHAOS_RICOCHET", ChipCategory.Chaos),
                new UniversalChipDefinition
                {
                    ChipId = "CHAOS_DISRUPT",
                    Category = ChipCategory.Chaos,
                    Rarity = ChipRarity.Common,
                    Modifiers = new[]
                    {
                        new UniversalChipModifierDefinition { ModifierType = "EnemyPaddleMoveSpeedMultiplier", Op = ModifierOp.Multiply, ValueLv1 = 0.85f },
                        new UniversalChipModifierDefinition { ModifierType = "EnemyPaddleSlowDurationSeconds", Op = ModifierOp.Override, ValueLv1 = 1.5f },
                    },
                    ConditionalModifiers = Array.Empty<UniversalChipConditionalModifierDefinition>(),
                },
            };
            HeroDefinition hero = new HeroDefinition
            {
                HeroId = "HERO_FROST_QUEEN",
                PathIds = new[] { "PATH_FROST_EXTREME", "PATH_FROST_CRYSTAL" },
            };
            HeroPathDefinition[] paths =
            {
                new HeroPathDefinition { PathId = "PATH_FROST_EXTREME", HeroId = hero.HeroId, ResonanceCategories = new[] { ChipCategory.Strike, ChipCategory.Guard } },
                new HeroPathDefinition { PathId = "PATH_FROST_CRYSTAL", HeroId = hero.HeroId, ResonanceCategories = new[] { ChipCategory.Guard, ChipCategory.Flow } },
            };
            return new GatebreakerModeCatalog(
                Array.Empty<ModeRuleDefinition>(), Array.Empty<BallRuleDefinition>(), Array.Empty<AiRuleDefinition>(), Array.Empty<MapRuleDefinition>(),
                Array.Empty<PlayerColorRuleDefinition>(), chips, Array.Empty<SignatureChipDefinition>(), new[] { hero }, paths);
        }

        private static UniversalChipDefinition Chip(
            string id,
            ChipCategory category,
            UniversalChipModifierDefinition modifier = null,
            UniversalChipConditionalModifierDefinition conditionalModifier = null)
        {
            UniversalChipModifierDefinition[] modifiers = modifier == null ? Array.Empty<UniversalChipModifierDefinition>() : new[] { modifier };
            UniversalChipConditionalModifierDefinition[] conditionalModifiers = conditionalModifier == null ? Array.Empty<UniversalChipConditionalModifierDefinition>() : new[] { conditionalModifier };
            return new UniversalChipDefinition
            {
                ChipId = id,
                Category = category,
                Rarity = ChipRarity.Common,
                Modifiers = modifiers,
                ConditionalModifiers = conditionalModifiers,
            };
        }
    }
}

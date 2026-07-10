using System;
using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Match;
using App.HotUpdate.GatebreakerArena.Mode;
using App.HotUpdate.GatebreakerArena.UI;
using NUnit.Framework;
using UnityEngine;

namespace Gatebreaker.Tests
{
    public sealed class GatebreakerHeroDeckSelectionPresenterTests
    {
        [Test]
        public void SelectionReadsOnlyTheThreeV1HeroesAndTwelveV1ChipsFromCatalog()
        {
            var presenter = new HeroDeckSelectionPresenter(CreateV1Catalog());

            CollectionAssert.AreEqual(
                new[]
                {
                    HeroDeckSelectionPresenter.FrostQueenHeroId,
                    HeroDeckSelectionPresenter.ThornGuardianHeroId,
                    HeroDeckSelectionPresenter.RadiantPaladinHeroId,
                },
                presenter.AvailableHeroes.Select(hero => hero.HeroId));
            Assert.AreEqual(12, presenter.AvailableChips.Count);
            Assert.IsFalse(presenter.AvailableChips.Any(chip => chip.ChipId == "SIG_FROST_DEEP_FREEZE_REFINED"));
        }

        [Test]
        public void SelectionRejectsDuplicatesAndExcludesFutureCatalogChips()
        {
            var presenter = new HeroDeckSelectionPresenter(CreateV1Catalog());

            Assert.IsTrue(presenter.TryAddChip("STRIKE_POWER", out HeroDeckSelectionValidation validation));
            Assert.IsFalse(presenter.TryAddChip("STRIKE_POWER", out validation));
            Assert.AreEqual(HeroDeckSelectionFailure.DuplicateChip, validation.Failure);

            Assert.IsTrue(presenter.TryAddChip("STRIKE_SERVE", out validation));
            Assert.IsTrue(presenter.TryAddChip("STRIKE_OVERCHARGE", out validation));
            Assert.IsFalse(presenter.TryAddChip("STRIKE_EXTRA", out validation));
            Assert.AreEqual(HeroDeckSelectionFailure.UnknownChip, validation.Failure);

            HeroDeckSelectionPresenter withFourthStrike = new HeroDeckSelectionPresenter(CreateCatalogWithExtraStrike());
            Assert.IsTrue(withFourthStrike.TryAddChip("STRIKE_POWER", out validation));
            Assert.IsTrue(withFourthStrike.TryAddChip("STRIKE_SERVE", out validation));
            Assert.IsTrue(withFourthStrike.TryAddChip("STRIKE_OVERCHARGE", out validation));
            Assert.IsFalse(withFourthStrike.TryAddChip("STRIKE_EXTRA", out validation));
            Assert.AreEqual(HeroDeckSelectionFailure.UnknownChip, validation.Failure,
                "The UI permits only the fixed V1 twelve-chip catalog even if future chips are loaded.");
        }

        [Test]
        public void SelectionBuildsStablePlayerSlotOnlyAfterHeroIsSelected()
        {
            var presenter = new HeroDeckSelectionPresenter(CreateV1Catalog());

            Assert.IsFalse(presenter.TryCreatePlayerSlot(0, 1, 7, false, out GatebreakerMatchPlayerSlot slot, out HeroDeckSelectionValidation validation));
            Assert.AreEqual(HeroDeckSelectionFailure.HeroNotSelected, validation.Failure);

            Assert.IsTrue(presenter.TrySelectHero(HeroDeckSelectionPresenter.FrostQueenHeroId, out validation));
            Assert.IsTrue(presenter.TryAddChip("FLOW_SPEED", out validation));
            Assert.IsTrue(presenter.TryAddChip("GUARD_LENGTH", out validation));
            Assert.IsTrue(presenter.TryAddChip("STRIKE_POWER", out validation));

            Assert.IsTrue(presenter.TryCreatePlayerSlot(0, 1, 7, false, out slot, out validation));
            Assert.AreEqual(HeroDeckSelectionPresenter.FrostQueenHeroId, slot.HeroId);
            CollectionAssert.AreEqual(
                new[] { "FLOW_SPEED", "GUARD_LENGTH", "STRIKE_POWER" },
                slot.DeckChipIds);
        }

        [Test]
        public void SelectionRejectsAChipBeyondTheEightChipDeckLimit()
        {
            var presenter = new HeroDeckSelectionPresenter(CreateV1Catalog());
            string[] deck =
            {
                "STRIKE_POWER", "STRIKE_SERVE", "STRIKE_OVERCHARGE",
                "GUARD_LENGTH", "GUARD_GOAL", "GUARD_BOUNCE",
                "FLOW_SPEED", "FLOW_AMMO",
            };

            for (int i = 0; i < deck.Length; i++)
            {
                Assert.IsTrue(presenter.TryAddChip(deck[i], out HeroDeckSelectionValidation validation));
            }

            Assert.IsFalse(presenter.TryAddChip("FLOW_CAPACITY", out HeroDeckSelectionValidation limitValidation));
            Assert.AreEqual(HeroDeckSelectionFailure.DeckFull, limitValidation.Failure);
        }

        [Test]
        public void InputPresenterForwardsAbilityPressedWithoutOwningMatchRules()
        {
            var presenter = new GatebreakerArenaInputPresenter();

            var frame = presenter.BuildFrame(2, 0.5f, true, Vector2.up, true);

            Assert.AreEqual(2, frame.PlayerId);
            Assert.IsTrue(frame.ServePressed);
            Assert.IsTrue(frame.AbilityPressed);
            Assert.AreEqual(Vector2.up, frame.AimDirection);
        }

        private static GatebreakerModeCatalog CreateV1Catalog()
        {
            return CreateCatalog(includeFutureStrike: false);
        }

        private static GatebreakerModeCatalog CreateCatalogWithExtraStrike()
        {
            return CreateCatalog(includeFutureStrike: true);
        }

        private static GatebreakerModeCatalog CreateCatalog(bool includeFutureStrike)
        {
            var chips = new List<UniversalChipDefinition>
            {
                CreateChip("STRIKE_POWER", ChipCategory.Strike),
                CreateChip("STRIKE_SERVE", ChipCategory.Strike),
                CreateChip("STRIKE_OVERCHARGE", ChipCategory.Strike),
                CreateChip("GUARD_LENGTH", ChipCategory.Guard),
                CreateChip("GUARD_GOAL", ChipCategory.Guard),
                CreateChip("GUARD_BOUNCE", ChipCategory.Guard),
                CreateChip("FLOW_SPEED", ChipCategory.Flow),
                CreateChip("FLOW_AMMO", ChipCategory.Flow),
                CreateChip("FLOW_CAPACITY", ChipCategory.Flow),
                CreateChip("CHAOS_SPIN", ChipCategory.Chaos),
                CreateChip("CHAOS_RICOCHET", ChipCategory.Chaos),
                CreateChip("CHAOS_DISRUPT", ChipCategory.Chaos),
            };
            if (includeFutureStrike)
            {
                chips.Add(CreateChip("STRIKE_EXTRA", ChipCategory.Strike));
            }

            return new GatebreakerModeCatalog(
                Array.Empty<ModeRuleDefinition>(),
                Array.Empty<BallRuleDefinition>(),
                Array.Empty<AiRuleDefinition>(),
                Array.Empty<MapRuleDefinition>(),
                Array.Empty<PlayerColorRuleDefinition>(),
                chips,
                Array.Empty<SignatureChipDefinition>(),
                new[]
                {
                    CreateHero(HeroDeckSelectionPresenter.FrostQueenHeroId, "冰雪女王"),
                    CreateHero(HeroDeckSelectionPresenter.ThornGuardianHeroId, "荆棘守护者"),
                    CreateHero(HeroDeckSelectionPresenter.RadiantPaladinHeroId, "辉光圣骑"),
                    CreateHero("HERO_FUTURE", "未来英雄"),
                },
                Array.Empty<HeroPathDefinition>());
        }

        private static UniversalChipDefinition CreateChip(string chipId, ChipCategory category)
        {
            return new UniversalChipDefinition
            {
                ChipId = chipId,
                DisplayName = chipId,
                Category = category,
                Rarity = ChipRarity.Common,
                Description = chipId,
            };
        }

        private static HeroDefinition CreateHero(string heroId, string displayName)
        {
            return new HeroDefinition
            {
                HeroId = heroId,
                DisplayName = displayName,
            };
        }
    }
}

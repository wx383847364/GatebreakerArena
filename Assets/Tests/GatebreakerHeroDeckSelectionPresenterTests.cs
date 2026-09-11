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
        // 全屏左右区域按住即输出满轴，中线换向，触摸优先于相反的后备输入。
        [TestCase(0f, -1f)]
        [TestCase(499f, -1f)]
        [TestCase(500f, 1f)]
        [TestCase(999f, 1f)]
        public void FullScreenTouchOverridesFallbackAndChangesDirection(float x, float expected)
        {
            var presenter = new GatebreakerArenaInputPresenter();
            presenter.ResolveMoveAxis(Array.Empty<Touch>(), 1000f, true, 0f, out _);
            Assert.AreEqual(expected, presenter.ResolveMoveAxis(
                new[] { MovementTouch(8, x, TouchPhase.Began) }, 1000f, true, -expected, out bool owns));
            Assert.IsTrue(owns);
            Assert.AreEqual(expected, presenter.ResolveMoveAxis(
                new[] { MovementTouch(8, x, TouchPhase.Stationary) }, 1000f, true, 0f, out _));
            Assert.AreEqual(-expected, presenter.ResolveMoveAxis(
                new[] { MovementTouch(8, expected < 0f ? 900f : 10f, TouchPhase.Moved) }, 1000f, true, 0f, out _));
        }

        // 即使系统改变触点顺序，第二根手指也不抢方向，主手指释放后才接替。
        [Test]
        public void TouchOwnershipSurvivesArrayReorderAndHandsOverOnRelease()
        {
            var presenter = new GatebreakerArenaInputPresenter();
            presenter.ResolveMoveAxis(Array.Empty<Touch>(), 1000f, true, 0f, out _);
            presenter.ResolveMoveAxis(new[] { MovementTouch(9, 10f, TouchPhase.Began) }, 1000f, true, 0f, out _);
            Assert.AreEqual(-1f, presenter.ResolveMoveAxis(new[]
            {
                MovementTouch(2, 900f, TouchPhase.Began), MovementTouch(9, 10f, TouchPhase.Stationary),
            }, 1000f, true, 0f, out _));
            Assert.AreEqual(1f, presenter.ResolveMoveAxis(new[]
            {
                MovementTouch(9, 10f, TouchPhase.Ended), MovementTouch(2, 900f, TouchPhase.Stationary),
            }, 1000f, true, 0f, out _));
        }

        // 松开、取消和系统漏报结束都必须在释放当帧归零，不回退到残留 UI 轴。
        [TestCase(TouchPhase.Ended)]
        [TestCase(TouchPhase.Canceled)]
        [TestCase(TouchPhase.Stationary)]
        public void TouchReleaseSuppressesFallbackForTheReleaseFrame(TouchPhase phase)
        {
            var presenter = new GatebreakerArenaInputPresenter();
            presenter.ResolveMoveAxis(Array.Empty<Touch>(), 1000f, true, 0f, out _);
            presenter.ResolveMoveAxis(new[] { MovementTouch(1, 10f, TouchPhase.Began) }, 1000f, true, 0f, out _);
            Touch[] release = phase == TouchPhase.Stationary ? Array.Empty<Touch>() : new[] { MovementTouch(1, 10f, phase) };
            Assert.AreEqual(0f, presenter.ResolveMoveAxis(release, 1000f, true, 1f, out bool owns));
            Assert.IsTrue(owns);
            Assert.AreEqual(1f, presenter.ResolveMoveAxis(Array.Empty<Touch>(), 1000f, true, 1f, out owns));
            Assert.IsFalse(owns);
        }

        // 开始、暂停及生命周期重置后，旧手指必须抬起重新按下才能移动。
        [Test]
        public void MovementGateAndResetRequireFreshPress()
        {
            var presenter = new GatebreakerArenaInputPresenter();
            Touch[] began = { MovementTouch(1, 10f, TouchPhase.Began) };
            Touch[] held = { MovementTouch(1, 10f, TouchPhase.Stationary) };
            Assert.AreEqual(0f, presenter.ResolveMoveAxis(began, 1000f, false, 1f, out _));
            Assert.AreEqual(0f, presenter.ResolveMoveAxis(held, 1000f, true, 1f, out _));
            Assert.AreEqual(0f, presenter.ResolveMoveAxis(held, 1000f, true, 1f, out _));
            presenter.ResolveMoveAxis(Array.Empty<Touch>(), 1000f, true, 0f, out _);
            Assert.AreEqual(-1f, presenter.ResolveMoveAxis(began, 1000f, true, 0f, out _));
            Assert.AreEqual(0f, presenter.ResolveMoveAxis(held, 1000f, false, 1f, out _));
            presenter.ResetMovement(held);
            Assert.AreEqual(0f, presenter.ResolveMoveAxis(held, 1000f, true, 1f, out _));
            Assert.AreEqual(0f, presenter.ResolveMoveAxis(held, 1000f, true, 1f, out _));
            presenter.ResolveMoveAxis(Array.Empty<Touch>(), 1000f, true, 0f, out _);
            Assert.AreEqual(-1f, presenter.ResolveMoveAxis(began, 1000f, true, 0f, out _));
        }

        // 可操作的首帧接受新手指，但暂停恢复按钮的同帧 Began 不得带入战斗。
        [Test]
        public void FirstPlayableFrameAcceptsFreshPressButNotResetButtonFinger()
        {
            var presenter = new GatebreakerArenaInputPresenter();
            Touch[] oldPress = { MovementTouch(1, 10f, TouchPhase.Began) };
            Assert.AreEqual(-1f, presenter.ResolveMoveAxis(oldPress, 1000f, true, 0f, out _));
            presenter.ResetMovement(oldPress);
            Assert.AreEqual(0f, presenter.ResolveMoveAxis(oldPress, 1000f, true, 0f, out _));
            Assert.AreEqual(1f, presenter.ResolveMoveAxis(new[]
            {
                MovementTouch(1, 10f, TouchPhase.Stationary), MovementTouch(2, 900f, TouchPhase.Began),
            }, 1000f, true, 0f, out _));
        }

        // 构造触摸样本，不依赖真机输入；纵向坐标不参与全屏左右划分。
        private static Touch MovementTouch(int id, float x, TouchPhase phase)
        {
            return new Touch { fingerId = id, position = new Vector2(x, 1800f), phase = phase };
        }

        [Test]
        public void SelectionReadsOnlyTheThreeV1HeroesAndTwelveV1ChipsFromCatalog()
        {
            var presenter = new HeroDeckSelectionPresenter(CreateV1Catalog());

            CollectionAssert.AreEqual(
                new[]
                {
                    HeroDeckSelectionPresenter.FrostQueenHeroId,
                    HeroDeckSelectionPresenter.EngineerHeroId,
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
            Assert.IsTrue(presenter.TryAddChip("FLOW_AMMO", out validation));
            Assert.IsTrue(presenter.TryAddChip("GUARD_GOAL", out validation));

            Assert.IsTrue(presenter.TryCreatePlayerSlot(0, 1, 7, false, out slot, out validation));
            Assert.AreEqual(HeroDeckSelectionPresenter.FrostQueenHeroId, slot.HeroId);
            Assert.AreEqual("PATH_FROST_EXTREME", slot.Loadout.PathId);
            Assert.AreEqual("SIG_FROST_DEEP_FREEZE_TOUCH", slot.Loadout.SignatureChipId);
            CollectionAssert.AreEqual(
                new[] { "FLOW_SPEED", "GUARD_LENGTH", "STRIKE_POWER", "FLOW_AMMO", "GUARD_GOAL" },
                slot.DeckChipIds);
        }

        [Test]
        public void SelectionRejectsAChipBeyondTheFiveChipDeckLimit()
        {
            var presenter = new HeroDeckSelectionPresenter(CreateV1Catalog());
            string[] deck =
            {
                "STRIKE_POWER", "STRIKE_SERVE", "GUARD_LENGTH",
                "GUARD_GOAL", "FLOW_SPEED",
            };

            for (int i = 0; i < deck.Length; i++)
            {
                Assert.IsTrue(presenter.TryAddChip(deck[i], out HeroDeckSelectionValidation validation));
            }

            Assert.IsFalse(presenter.TryAddChip("FLOW_AMMO", out HeroDeckSelectionValidation limitValidation));
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
            return GatebreakerModeCatalog.CreateDefault();
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
                CreateChip("STRIKE_ANGLE", ChipCategory.Strike),
                CreateChip("STRIKE_OVERCHARGE", ChipCategory.Strike),
                CreateChip("GUARD_LENGTH", ChipCategory.Guard),
                CreateChip("GUARD_GOAL", ChipCategory.Guard),
                CreateChip("GUARD_BOUNCE", ChipCategory.Guard),
                CreateChip("GUARD_BRAKE", ChipCategory.Guard),
                CreateChip("FLOW_SPEED", ChipCategory.Flow),
                CreateChip("FLOW_AMMO", ChipCategory.Flow),
                CreateChip("FLOW_CAPACITY", ChipCategory.Flow),
                CreateChip("FLOW_QUICK_SERVE", ChipCategory.Flow),
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
                    CreateHero(HeroDeckSelectionPresenter.EngineerHeroId, "工事"),
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

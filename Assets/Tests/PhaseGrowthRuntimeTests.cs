using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using App.HotUpdate.GatebreakerArena.BrickDuel;
using App.HotUpdate.GatebreakerArena.Chip;
using App.HotUpdate.GatebreakerArena.Mode;
using App.HotUpdate.GatebreakerArena.Phase;
using App.HotUpdate.GatebreakerArena.UI;
using App.Shared.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace Gatebreaker.Tests
{
    public sealed class PhaseGrowthRuntimeTests
    {
        [Test]
        public void DefaultLoadout_ContainsOneDefaultTechForEveryPhase()
        {
            GatebreakerModeCatalog catalog = LoadCatalog();
            PhaseMatchLoadout loadout = PhaseMatchLoadout.CreateDefault(catalog, "HERO_MIRAGE");

            Assert.AreEqual(5, loadout.TechIds.Count);
            Assert.IsTrue(PhaseLoadoutValidator.Validate(catalog, loadout).IsValid);
            CollectionAssert.AreEqual(
                new[] { "P1", "P2", "P3", "P4", "P5" },
                loadout.TechIds.Select(id => catalog.GetPhaseTech(id).SlotPhase).ToArray());
            Assert.IsTrue(loadout.TechIds.All(id => catalog.GetPhaseTech(id).Kind == "Default"));
        }

        [Test]
        public void Phi_IsCappedPerSecondAndResetsAtEachStage()
        {
            GatebreakerModeCatalog catalog = LoadCatalog();
            var phase = new BrickDuelPhaseSideRuntime(
                catalog,
                PhaseMatchLoadout.CreateDefault(catalog, "HERO_MIRAGE"),
                30);

            for (int second = 0; second < 5; second++)
            {
                phase.Tick(second * 30);
                for (int pickup = 0; pickup < 10; pickup++) phase.OnItemCollected();
                Assert.LessOrEqual(phase.State.PhiGainedThisSecond, 6f);
            }

            Assert.AreEqual(2, phase.State.PhaseLevel);
            Assert.AreEqual(0f, phase.State.Phi, 0.0001f);
        }

        [Test]
        public void SharedPiercePool_MergesToMaximumAndConsumesItemOverlayFirst()
        {
            GatebreakerModeCatalog catalog = LoadCatalog();
            var phase = new BrickDuelPhaseSideRuntime(
                catalog,
                PhaseMatchLoadout.CreateDefault(catalog, "HERO_RIFT"),
                30);

            phase.AddItemPierce(2);
            phase.AddItemPierce(2);
            Assert.AreEqual(2, phase.State.TotalPierceCharges);
            Assert.AreEqual(2, phase.State.ItemPierceCharges);

            phase.ConsumePierce(1);
            Assert.AreEqual(1, phase.State.ItemPierceCharges);
            Assert.AreEqual(0, phase.State.HeroPierceCharges);
        }

        [Test]
        public void SharedPiercePool_HeroGrantDoesNotAddOnTopOfItemOverlay()
        {
            GatebreakerModeCatalog catalog = LoadCatalog();
            var phase = new BrickDuelPhaseSideRuntime(
                catalog,
                PhaseMatchLoadout.CreateDefault(catalog, "HERO_RIFT"),
                30);
            phase.AddItemPierce(2);
            SetState(phase.State, nameof(BrickDuelPhaseSideState.RiftCharge), 4);

            phase.OnMainBallCollision(new BrickDuelCollisionFrameTelemetry { BrickHitCount = 1 });

            Assert.AreEqual(2, phase.State.TotalPierceCharges);
            Assert.AreEqual(1, phase.State.HeroPierceCharges);
            Assert.AreEqual(1, phase.State.ItemPierceCharges);
        }

        [Test]
        public void RefractCycle_WaitsForNextPaddleBounceBeforeBoostStarts()
        {
            GatebreakerModeCatalog catalog = LoadCatalog();
            var phase = new BrickDuelPhaseSideRuntime(
                catalog,
                PhaseMatchLoadout.CreateDefault(catalog, "HERO_REFRACT"),
                30);
            SetState(phase.State, nameof(BrickDuelPhaseSideState.RefractMarkFrames), 1);

            phase.OnMainBallCollision(new BrickDuelCollisionFrameTelemetry { BrickHitCount = 1 });
            Assert.IsTrue(phase.State.RefractBoostPending);
            Assert.AreEqual(1, phase.State.RefractPendingSpeedStacks);
            Assert.AreEqual(0, phase.State.RefractBoostFrames);

            phase.OnMainBallCollision(new BrickDuelCollisionFrameTelemetry { PaddleBounceCount = 1 });
            Assert.IsFalse(phase.State.RefractBoostPending);
            Assert.AreEqual(0, phase.State.RefractPendingSpeedStacks);
            Assert.AreEqual(1, phase.State.RefractSpeedStacks);
            Assert.Greater(phase.State.RefractBoostFrames, 0);
        }

        [Test]
        public void RefractP4_NewCycleQueuesStackUntilNextPaddleBounce()
        {
            GatebreakerModeCatalog catalog = LoadCatalog();
            var phase = new BrickDuelPhaseSideRuntime(
                catalog,
                PhaseMatchLoadout.CreateDefault(catalog, "HERO_REFRACT"),
                30);
            SetState(phase.State, nameof(BrickDuelPhaseSideState.PhaseLevel), 4);
            SetState(phase.State, nameof(BrickDuelPhaseSideState.RefractSpeedStacks), 1);
            SetState(phase.State, nameof(BrickDuelPhaseSideState.RefractBoostFrames), 30);
            SetState(phase.State, nameof(BrickDuelPhaseSideState.RefractMarkFrames), 1);

            phase.OnMainBallCollision(new BrickDuelCollisionFrameTelemetry { BrickHitCount = 1 });

            Assert.AreEqual(1, phase.State.RefractSpeedStacks);
            Assert.AreEqual(2, phase.State.RefractPendingSpeedStacks);
            Assert.AreEqual(1.06f, phase.GetHeroBallSpeedMultiplier(), 0.0001f);
            Assert.AreEqual(
                1.12f,
                phase.GetHeroBallSpeedMultiplier(forPaddleBounce: true),
                0.0001f);

            phase.OnMainBallCollision(new BrickDuelCollisionFrameTelemetry { PaddleBounceCount = 1 });

            Assert.AreEqual(2, phase.State.RefractSpeedStacks);
            Assert.AreEqual(0, phase.State.RefractPendingSpeedStacks);
            Assert.AreEqual(1.12f, phase.GetHeroBallSpeedMultiplier(), 0.0001f);
        }

        [TestCase(0, 0)]
        [TestCase(60, 60)]
        [TestCase(300, 210)]
        public void RefractP5_CooldownReductionNeverIncreasesRemainingCooldown(
            int initialCooldownFrames,
            int expectedCooldownFrames)
        {
            GatebreakerModeCatalog catalog = LoadCatalog();
            var phase = new BrickDuelPhaseSideRuntime(
                catalog,
                PhaseMatchLoadout.CreateDefault(catalog, "HERO_REFRACT"),
                30);
            SetState(phase.State, nameof(BrickDuelPhaseSideState.PhaseLevel), 5);
            SetState(
                phase.State,
                nameof(BrickDuelPhaseSideState.AbilityCooldownFrames),
                initialCooldownFrames);

            SetState(phase.State, nameof(BrickDuelPhaseSideState.RefractMarkFrames), 1);
            phase.OnMainBallCollisionEvent(new BrickDuelCollisionEvent(
                BrickDuelCollisionEventType.BrickHit));
            SetState(phase.State, nameof(BrickDuelPhaseSideState.RefractMarkFrames), 1);
            phase.OnMainBallCollisionEvent(new BrickDuelCollisionEvent(
                BrickDuelCollisionEventType.BrickHit));

            Assert.AreEqual(expectedCooldownFrames, phase.State.AbilityCooldownFrames);
        }

        [Test]
        public void AbilityAvailability_IsDerivedFromPhaseAndCooldown()
        {
            GatebreakerModeCatalog catalog = LoadCatalog();
            var phase = new BrickDuelPhaseSideRuntime(
                catalog,
                PhaseMatchLoadout.CreateDefault(catalog, "HERO_MIRAGE"),
                30);

            Assert.IsFalse(phase.State.AbilityUnlocked);
            Assert.IsFalse(phase.State.AbilityAvailable);

            SetState(phase.State, nameof(BrickDuelPhaseSideState.PhaseLevel), 3);
            Assert.IsTrue(phase.State.AbilityUnlocked);
            Assert.IsTrue(phase.State.AbilityAvailable);

            SetState(
                phase.State,
                nameof(BrickDuelPhaseSideState.AbilityCooldownFrames),
                1);
            Assert.IsTrue(phase.State.AbilityUnlocked);
            Assert.IsFalse(phase.State.AbilityAvailable);
        }

        [Test]
        public void RefractCollisionEvents_RespectBrickAndPaddleOrder()
        {
            GatebreakerModeCatalog catalog = LoadCatalog();
            var brickThenPaddle = new BrickDuelPhaseSideRuntime(
                catalog,
                PhaseMatchLoadout.CreateDefault(catalog, "HERO_REFRACT"),
                30);
            SetState(
                brickThenPaddle.State,
                nameof(BrickDuelPhaseSideState.RefractMarkFrames),
                1);

            brickThenPaddle.OnMainBallCollisionEvent(new BrickDuelCollisionEvent(
                BrickDuelCollisionEventType.BrickHit));
            brickThenPaddle.OnMainBallCollisionEvent(new BrickDuelCollisionEvent(
                BrickDuelCollisionEventType.PaddleBounce));

            Assert.AreEqual(0, brickThenPaddle.State.RefractPendingSpeedStacks);
            Assert.AreEqual(1, brickThenPaddle.State.RefractSpeedStacks);
            Assert.Greater(brickThenPaddle.State.RefractBoostFrames, 0);

            var paddleThenBrick = new BrickDuelPhaseSideRuntime(
                catalog,
                PhaseMatchLoadout.CreateDefault(catalog, "HERO_REFRACT"),
                30);
            paddleThenBrick.OnMainBallCollisionEvent(new BrickDuelCollisionEvent(
                BrickDuelCollisionEventType.PaddleBounce));
            paddleThenBrick.OnMainBallCollisionEvent(new BrickDuelCollisionEvent(
                BrickDuelCollisionEventType.BrickHit));

            Assert.AreEqual(1, paddleThenBrick.State.RefractPendingSpeedStacks);
            Assert.AreEqual(0, paddleThenBrick.State.RefractSpeedStacks);
            Assert.AreEqual(0, paddleThenBrick.State.RefractBoostFrames);
        }

        [Test]
        public void MainBallCollisionEvents_RespectBrickAndOuterWallOrder()
        {
            GatebreakerModeCatalog catalog = LoadCatalog();
            var brickThenWall = new BrickDuelPhaseSideRuntime(
                catalog,
                PhaseMatchLoadout.CreateDefault(catalog, "HERO_MIRAGE"),
                30);
            SetState(brickThenWall.State, nameof(BrickDuelPhaseSideState.Combo), 7);

            Assert.AreEqual(1, brickThenWall.OnMainBallCollisionEvent(
                new BrickDuelCollisionEvent(BrickDuelCollisionEventType.BrickHit)));
            brickThenWall.OnMainBallCollisionEvent(new BrickDuelCollisionEvent(
                BrickDuelCollisionEventType.OwnOuterWallBounce));

            Assert.AreEqual(0, brickThenWall.State.Combo);

            var wallThenBrick = new BrickDuelPhaseSideRuntime(
                catalog,
                PhaseMatchLoadout.CreateDefault(catalog, "HERO_MIRAGE"),
                30);
            SetState(wallThenBrick.State, nameof(BrickDuelPhaseSideState.Combo), 7);
            wallThenBrick.OnMainBallCollisionEvent(new BrickDuelCollisionEvent(
                BrickDuelCollisionEventType.OwnOuterWallBounce));
            wallThenBrick.OnMainBallCollisionEvent(new BrickDuelCollisionEvent(
                BrickDuelCollisionEventType.BrickHit));

            Assert.AreEqual(1, wallThenBrick.State.Combo);
        }

        [Test]
        public void RefractPendingBoost_AppliesToMainBounceAndSplitBallInTheSameFrame()
        {
            GatebreakerModeCatalog catalog = LoadCatalog();
            BrickDuelRuleDefinition rule = catalog.GetBrickDuelRule("BRICK_DUEL_V0");
            BrickDuelAiRuleDefinition aiRule = catalog.GetBrickDuelAiRule(rule.BrickDuelAiRuleId);
            PhaseMatchLoadout refract = PhaseMatchLoadout.CreateDefault(catalog, "HERO_REFRACT");
            var runtime = new BrickDuelRuntime(rule, aiRule, catalog, refract, refract);
            runtime.BeginCountdown();
            int countdownFrames = Mathf.RoundToInt(rule.CountdownSeconds * rule.SimulationFps);
            for (int i = 0; i < countdownFrames; i++)
            {
                runtime.StepFrame(new BrickDuelFrameInput(0f, 0f));
            }

            FieldInfo phaseField = typeof(BrickDuelRuntime).GetField(
                "_bottomPhase",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var phase = (BrickDuelPhaseSideRuntime)phaseField.GetValue(runtime);
            SetState(
                phase.State,
                nameof(BrickDuelPhaseSideState.RefractPendingSpeedStacks),
                1);
            float contactY = runtime.BottomPaddle.Position.y +
                             rule.PaddleHalfHeight +
                             runtime.BottomBallRadius;
            runtime.BottomBall.Position = new Vector2(
                0f,
                contactY + rule.BallSpeed * runtime.FrameDelta * 0.5f);
            runtime.BottomBall.Velocity = Vector2.down * rule.BallSpeed;
            MethodInfo spawn = typeof(BrickDuelRuntime).GetMethod(
                "SpawnSplitBallsFromSide",
                BindingFlags.Instance | BindingFlags.NonPublic);
            spawn.Invoke(runtime, new object[] { BrickDuelSide.Bottom, 1, true });
            BrickDuelBallState split = runtime.SplitBalls.Single();
            split.Position = new Vector2(0f, -1f);
            split.Velocity = Vector2.up * rule.BallSpeed;

            runtime.StepFrame(new BrickDuelFrameInput(0f, 0f));

            Assert.AreEqual(0, phase.State.RefractPendingSpeedStacks);
            Assert.AreEqual(1, phase.State.RefractSpeedStacks);
            Assert.AreEqual(runtime.BottomBall.Velocity.magnitude, split.Velocity.magnitude, 0.0001f);
            Assert.AreEqual(rule.BallSpeed * 1.08f, split.Velocity.magnitude, 0.0001f);
        }

        [Test]
        public void RefractMirror_ReflectsMainAndSplitBallThroughRuntimeToi()
        {
            GatebreakerModeCatalog catalog = LoadCatalog();
            BrickDuelRuleDefinition rule = catalog.GetBrickDuelRule("BRICK_DUEL_V0");
            BrickDuelAiRuleDefinition aiRule = catalog.GetBrickDuelAiRule(rule.BrickDuelAiRuleId);
            PhaseMatchLoadout refract = PhaseMatchLoadout.CreateDefault(catalog, "HERO_REFRACT");
            var runtime = new BrickDuelRuntime(rule, aiRule, catalog, refract, refract);
            runtime.BeginCountdown();
            int countdownFrames = Mathf.RoundToInt(rule.CountdownSeconds * rule.SimulationFps);
            for (int i = 0; i < countdownFrames; i++)
            {
                runtime.StepFrame(new BrickDuelFrameInput(0f, 0f));
            }

            FieldInfo phaseField = typeof(BrickDuelRuntime).GetField(
                "_bottomPhase",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var phase = (BrickDuelPhaseSideRuntime)phaseField.GetValue(runtime);
            SetState(phase.State, nameof(BrickDuelPhaseSideState.MirrorFrames), 30);
            float mirrorY = -rule.CoreLineY * phase.GetMirrorHalfFieldRatio();
            runtime.BottomBall.Position = new Vector2(-0.2f, mirrorY + 0.05f);
            runtime.BottomBall.Velocity = Vector2.down * rule.BallSpeed;
            MethodInfo spawn = typeof(BrickDuelRuntime).GetMethod(
                "SpawnSplitBallsFromSide",
                BindingFlags.Instance | BindingFlags.NonPublic);
            spawn.Invoke(runtime, new object[] { BrickDuelSide.Bottom, 1, true });
            BrickDuelBallState split = runtime.SplitBalls.Single();
            split.Position = new Vector2(0.2f, mirrorY + 0.05f);
            split.Velocity = Vector2.down * rule.BallSpeed;

            runtime.StepFrame(new BrickDuelFrameInput(0f, 0f));

            Assert.Greater(runtime.BottomBall.Position.y, mirrorY);
            Assert.Greater(split.Position.y, mirrorY);
            Assert.Greater(runtime.BottomBall.Velocity.y, 0f);
            Assert.Greater(split.Velocity.y, 0f);
        }

        [Test]
        public void Profile_UnlockAndSettlementAreTransactionalAndIdempotent()
        {
            GatebreakerModeCatalog catalog = LoadCatalog();
            var persistence = new MemoryPersistence();
            var profile = new PhaseProfileService(persistence, catalog);
            profile.LoadAsync().GetAwaiter().GetResult();

            Assert.AreEqual(15, profile.Current.Currency);
            Assert.IsTrue(profile.UnlockAsync("TECH_MIRAGE_P1_SWARM").GetAwaiter().GetResult());
            Assert.AreEqual(0, profile.Current.Currency);
            Assert.IsFalse(profile.UnlockAsync("TECH_MIRAGE_P2_FISSION").GetAwaiter().GetResult());

            Assert.AreEqual(12, profile.SettleAsync("match-1", BrickDuelResult.PlayerWin, true).GetAwaiter().GetResult());
            Assert.AreEqual(0, profile.SettleAsync("match-1", BrickDuelResult.PlayerWin, true).GetAwaiter().GetResult());
            Assert.AreEqual(12, profile.Current.Currency);
            Assert.AreEqual(0, profile.SettleAsync("aborted", BrickDuelResult.PlayerLose, false).GetAwaiter().GetResult());
        }

        [Test]
        public void Profile_SettlementFailureRollsBackAndCanRetry()
        {
            GatebreakerModeCatalog catalog = LoadCatalog();
            var persistence = new MemoryPersistence { FailuresRemaining = 1 };
            var profile = new PhaseProfileService(persistence, catalog);
            profile.LoadAsync().GetAwaiter().GetResult();
            int before = profile.Current.Currency;

            PhaseSettlementResult failed = profile
                .SettleWithResultAsync("retry-match", BrickDuelResult.PlayerWin, true)
                .GetAwaiter().GetResult();
            Assert.AreEqual(PhaseSettlementStatus.PersistenceFailed, failed.Status);
            Assert.AreEqual(before, profile.Current.Currency);
            Assert.IsFalse(profile.Current.SettledMatchIds.Contains("retry-match"));

            PhaseSettlementResult retried = profile
                .SettleWithResultAsync("retry-match", BrickDuelResult.PlayerWin, true)
                .GetAwaiter().GetResult();
            Assert.AreEqual(PhaseSettlementStatus.Completed, retried.Status);
            Assert.Greater(retried.Reward, 0);
            Assert.IsTrue(profile.Current.SettledMatchIds.Contains("retry-match"));
        }

        [Test]
        public async Task SettlementCoordinator_ReportsFirstFailureWhileBackgroundRetryContinues()
        {
            GatebreakerModeCatalog catalog = LoadCatalog();
            var persistence = new MemoryPersistence { FailuresRemaining = 1 };
            var profile = new PhaseProfileService(persistence, catalog);
            await profile.LoadAsync();
            using (var coordinator = new PhaseSettlementCoordinator(profile, TimeSpan.Zero))
            {
                PhaseSettlementResult first = await coordinator.EnsureSettlementAsync(
                    "coordinator-retry",
                    BrickDuelResult.PlayerWin,
                    true);
                Assert.AreEqual(PhaseSettlementStatus.PersistenceFailed, first.Status);

                PhaseSettlementResult final = await coordinator.EnsureSettlementAsync(
                    "coordinator-retry",
                    BrickDuelResult.PlayerWin,
                    true);
                Assert.IsTrue(final.IsFinal,
                    "A completed background retry may be observed as Completed or AlreadySettled.");
                Assert.IsTrue(profile.Current.SettledMatchIds.Contains("coordinator-retry"));
            }
        }

        [Test]
        public void Presenter_RestoresSavedFiveSlotLoadout()
        {
            GatebreakerModeCatalog catalog = LoadCatalog();
            var persistence = new MemoryPersistence();
            var profile = new PhaseProfileService(persistence, catalog);
            profile.LoadAsync().GetAwaiter().GetResult();
            PhaseTechDefinition tech = catalog.AllPhaseTechs.Values
                .Where(item => item.HeroId == "HERO_MIRAGE" && item.Kind != "Default")
                .OrderBy(item => item.CostCurrency)
                .First();
            Assert.IsTrue(profile.UnlockAsync(tech.TechId).GetAwaiter().GetResult());
            PhaseMatchLoadout defaults = PhaseMatchLoadout.CreateDefault(catalog, tech.HeroId);
            string[] ids = defaults.TechIds.ToArray();
            ids[PhaseLoadoutValidator.PhaseIndex(tech.SlotPhase)] = tech.TechId;
            var selected = new PhaseMatchLoadout(tech.HeroId, ids);
            Assert.IsTrue(profile.SaveLoadoutAsync(selected).GetAwaiter().GetResult());

            var presenter = new PhaseTechSelectionPresenter(catalog);
            Assert.IsTrue(presenter.TryApplyLoadout(
                profile.GetLoadout(tech.HeroId),
                profile.Current.CreateUnlockedSet(),
                out string error), error);
            CollectionAssert.AreEqual(ids, presenter.Build(profile.Current.CreateUnlockedSet()).TechIds);
        }

        [Test]
        public void CanonicalItemWeights_AreExactAndTotalOne()
        {
            GatebreakerModeCatalog catalog = LoadCatalog();
            Assert.AreEqual(9, catalog.AllPhaseItems.Count);
            Assert.AreEqual(1f, catalog.AllPhaseItems.Values.Sum(item => item.BaseDropWeight), 0.0001f);
            Assert.AreEqual(0.17f, catalog.GetPhaseItem("ItemSpeed").BaseDropWeight, 0.0001f);
            Assert.AreEqual(0.05f, catalog.GetPhaseItem("ItemBuffer").BaseDropWeight, 0.0001f);
        }

        [Test]
        public void LanContractHash_ChangesWhenPhaseTuningChanges()
        {
            GatebreakerModeCatalog catalog = LoadCatalog();
            string legacy = V1ContractHash.ComputeCatalog(catalog);
            string before = PhaseMatchContractHash.ComputeCatalog(catalog, legacy);

            PhaseItemDefinition speed = catalog.GetPhaseItem("ItemSpeed");
            speed.BaseDropWeight += 0.001f;
            string after = PhaseMatchContractHash.ComputeCatalog(catalog, legacy);

            Assert.AreNotEqual(before, after);
        }

        private static GatebreakerModeCatalog LoadCatalog()
        {
            string path = Path.Combine(Application.dataPath, "Config/json/gatebreaker_rules.json");
            GatebreakerConfigLoadResult result = GatebreakerConfigRuntimeLoader.ParseJson(File.ReadAllText(path));
            Assert.IsTrue(result.Succeeded, result.Message);
            return result.Catalog;
        }

        private static void SetState(object target, string propertyName, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, propertyName);
            property.SetValue(target, value);
        }

        private sealed class MemoryPersistence : IPersistence
        {
            private readonly Dictionary<string, byte[]> _values = new Dictionary<string, byte[]>();
            public int FailuresRemaining { get; set; }
            public Task<bool> SaveAsync(string key, byte[] data)
            {
                if (FailuresRemaining > 0)
                {
                    FailuresRemaining--;
                    return Task.FromResult(false);
                }
                _values[key] = data == null ? null : (byte[])data.Clone();
                return Task.FromResult(true);
            }
            public Task<byte[]> LoadAsync(string key) => Task.FromResult(
                _values.TryGetValue(key, out byte[] value) && value != null ? (byte[])value.Clone() : null);
            public Task<bool> DeleteAsync(string key) => Task.FromResult(_values.Remove(key));
            public bool Exists(string key) => _values.ContainsKey(key);
        }

        [Test]
        public async Task Profile_LoadExceptionFallsBackToDefaultProfile()
        {
            GatebreakerModeCatalog catalog = LoadCatalog();
            var profile = new PhaseProfileService(new ThrowingLoadPersistence(), catalog);

            PhasePlayerProfile loaded = await profile.LoadAsync();

            Assert.AreEqual(15, loaded.Currency);
            Assert.AreEqual(catalog.AllPhaseHeroes.Count, loaded.Loadouts.Count);
            Assert.AreEqual(loaded.Currency, profile.Current.Currency);
            Assert.AreEqual(loaded.LastSelectedHeroId, profile.Current.LastSelectedHeroId);
        }

        [Test]
        public async Task Profile_ConcurrentMutationsPersistCandidatesSeriallyBeforeCommit()
        {
            GatebreakerModeCatalog catalog = LoadCatalog();
            var persistence = new DelayedPersistence();
            var profile = new PhaseProfileService(persistence, catalog);
            await profile.LoadAsync();

            Task<PhaseSettlementResult> settlement = profile.SettleWithResultAsync(
                "serialized-match",
                BrickDuelResult.PlayerWin,
                true);
            await persistence.WaitForSaveCountAsync(1);
            Task<bool> unlock = profile.UnlockAsync("TECH_MIRAGE_P1_SWARM");

            Assert.AreEqual(1, persistence.SaveCallCount, "The unlock must wait behind the settlement write.");
            Assert.AreEqual(15, profile.Current.Currency, "An uncommitted settlement must not leak through Current.");
            Assert.IsFalse(profile.Current.SettledMatchIds.Contains("serialized-match"));

            persistence.CompleteNextSave(true);
            PhaseSettlementResult settled = await settlement;
            await persistence.WaitForSaveCountAsync(2);

            Assert.AreEqual(PhaseSettlementStatus.Completed, settled.Status);
            Assert.AreEqual(27, profile.Current.Currency);
            Assert.IsFalse(profile.Current.UnlockedTechIds.Contains("TECH_MIRAGE_P1_SWARM"),
                "The pending unlock must remain invisible until its own write commits.");

            persistence.CompleteNextSave(true);
            Assert.IsTrue(await unlock);
            Assert.AreEqual(1, persistence.MaximumConcurrentSaveCount);
            Assert.AreEqual(12, profile.Current.Currency);
            Assert.IsTrue(profile.Current.SettledMatchIds.Contains("serialized-match"));
            Assert.IsTrue(profile.Current.UnlockedTechIds.Contains("TECH_MIRAGE_P1_SWARM"));

            var reloaded = new PhaseProfileService(persistence, catalog);
            await reloaded.LoadAsync();
            Assert.AreEqual(12, reloaded.Current.Currency);
            Assert.IsTrue(reloaded.Current.SettledMatchIds.Contains("serialized-match"));
            Assert.IsTrue(reloaded.Current.UnlockedTechIds.Contains("TECH_MIRAGE_P1_SWARM"));
        }

        private sealed class ThrowingLoadPersistence : IPersistence
        {
            public Task<bool> SaveAsync(string key, byte[] data) => Task.FromResult(true);
            public Task<byte[]> LoadAsync(string key) =>
                Task.FromException<byte[]>(new System.IO.IOException("load failed"));
            public Task<bool> DeleteAsync(string key) => Task.FromResult(true);
            public bool Exists(string key) => false;
        }

        private sealed class DelayedPersistence : IPersistence
        {
            private readonly object _sync = new object();
            private readonly Dictionary<string, byte[]> _values = new Dictionary<string, byte[]>();
            private readonly Queue<PendingSave> _pendingSaves = new Queue<PendingSave>();
            private TaskCompletionSource<bool> _saveCountChanged = CreateSignal();
            private int _activeSaveCount;

            public int SaveCallCount { get; private set; }
            public int MaximumConcurrentSaveCount { get; private set; }

            public Task<bool> SaveAsync(string key, byte[] data)
            {
                lock (_sync)
                {
                    SaveCallCount++;
                    _activeSaveCount++;
                    MaximumConcurrentSaveCount = System.Math.Max(MaximumConcurrentSaveCount, _activeSaveCount);
                    var completion = CreateSignal();
                    _pendingSaves.Enqueue(new PendingSave(
                        key,
                        data == null ? null : (byte[])data.Clone(),
                        completion));
                    TaskCompletionSource<bool> changed = _saveCountChanged;
                    _saveCountChanged = CreateSignal();
                    changed.TrySetResult(true);
                    return completion.Task;
                }
            }

            public Task<byte[]> LoadAsync(string key)
            {
                lock (_sync)
                {
                    return Task.FromResult(
                        _values.TryGetValue(key, out byte[] value) && value != null
                            ? (byte[])value.Clone()
                            : null);
                }
            }

            public Task<bool> DeleteAsync(string key)
            {
                lock (_sync)
                {
                    return Task.FromResult(_values.Remove(key));
                }
            }

            public bool Exists(string key)
            {
                lock (_sync) return _values.ContainsKey(key);
            }

            public async Task WaitForSaveCountAsync(int expected)
            {
                while (true)
                {
                    Task wait;
                    lock (_sync)
                    {
                        if (SaveCallCount >= expected) return;
                        wait = _saveCountChanged.Task;
                    }
                    await wait;
                }
            }

            public void CompleteNextSave(bool succeeded)
            {
                PendingSave pending;
                lock (_sync)
                {
                    Assert.Greater(_pendingSaves.Count, 0, "No persistence write is pending.");
                    pending = _pendingSaves.Dequeue();
                    _activeSaveCount--;
                    if (succeeded)
                    {
                        _values[pending.Key] = pending.Data == null
                            ? null
                            : (byte[])pending.Data.Clone();
                    }
                }
                pending.Completion.TrySetResult(succeeded);
            }

            private static TaskCompletionSource<bool> CreateSignal() =>
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly struct PendingSave
            {
                public PendingSave(string key, byte[] data, TaskCompletionSource<bool> completion)
                {
                    Key = key;
                    Data = data;
                    Completion = completion;
                }

                public string Key { get; }
                public byte[] Data { get; }
                public TaskCompletionSource<bool> Completion { get; }
            }
        }
    }
}

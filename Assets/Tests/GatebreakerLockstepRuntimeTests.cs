using System.Collections.Generic;
using App.HotUpdate.GatebreakerArena.Application;
using App.HotUpdate.GatebreakerArena.Ball;
using App.HotUpdate.GatebreakerArena.Match;
using App.HotUpdate.GatebreakerArena.Mode;
using App.HotUpdate.GatebreakerArena.Serve;
using App.HotUpdate.GatebreakerArena.Zone;
using NUnit.Framework;
using UnityEngine;

namespace Gatebreaker.Tests
{
    public sealed class GatebreakerLockstepRuntimeTests
    {
        [Test]
        public void SameStartConfigAndInputSequenceProducesSameChecksumForThreeHundredFrames()
        {
            GatebreakerMatchStartConfig startConfig = CreateStartConfig();
            GatebreakerMatchRuntime left = CreateRuntime(startConfig);
            GatebreakerMatchRuntime right = CreateRuntime(startConfig);

            for (int frame = 0; frame < 300; frame++)
            {
                IReadOnlyList<GatebreakerFrameInput> inputs = BuildDeterministicInputs(frame);

                left.StepFrame(frame, inputs);
                right.StepFrame(frame, inputs);

                Assert.AreEqual(left.CreateChecksum(frame), right.CreateChecksum(frame), "frame=" + frame);
            }
        }

        [Test]
        public void ServePressedInputFrameIsConsumedAfterOneRuntimeTick()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            int initialBallCount = runtime.Balls.Count;

            runtime.ApplyInputFrame(new PlayerInputFrame(1, 0f, true, Vector2.up));
            runtime.Tick(1f / 60f);

            Assert.AreEqual(initialBallCount + 1, runtime.Balls.Count);

            runtime.FindPlayer(1).ServeResource.CurrentServeAmmo = 1;
            runtime.FindPlayer(1).ServeResource.OwnedBallsInField = 0;
            runtime.FindPlayer(1).ServeResource.ServeCooldownRemaining = 0f;

            runtime.Tick(1f / 60f);

            Assert.AreEqual(initialBallCount + 1, runtime.Balls.Count);
        }

        [Test]
        public void LocalPrototypeServeInputIsLatchedUntilFixedStepConsumesIt()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            int initialBallCount = runtime.Balls.Count;

            runtime.ApplyInputFrame(new PlayerInputFrame(1, 0f, true, Vector2.up));
            runtime.TickLocalPrototype(1f / 60f);
            runtime.ApplyInputFrame(new PlayerInputFrame(1, 0f, false, Vector2.up));
            runtime.TickLocalPrototype(1f / 60f);

            Assert.AreEqual(initialBallCount + 1, runtime.Balls.Count);
        }

        private static IReadOnlyList<GatebreakerFrameInput> BuildDeterministicInputs(int frame)
        {
            float playerOneAxis = ((frame / 17) % 2 == 0) ? 0.75f : -0.5f;
            float playerTwoAxis = ((frame / 23) % 2 == 0) ? -0.25f : 0.6f;
            bool playerOneServe = frame == 5 || frame == 180;
            bool playerTwoServe = frame == 11 || frame == 210;

            return new[]
            {
                new GatebreakerFrameInput(1, playerOneAxis, playerOneServe, Vector2.up),
                new GatebreakerFrameInput(2, playerTwoAxis, playerTwoServe, Vector2.down),
            };
        }

        private static GatebreakerMatchRuntime CreateRuntime(GatebreakerMatchStartConfig config)
        {
            var runtime = new GatebreakerMatchRuntime(
                GatebreakerModeCatalog.CreateDefault(),
                new BallSimulationSystem(),
                new ServeResourceSystem(),
                new GoalJudgeSystem(),
                new ScoreSystem(),
                null);
            runtime.StartMatch(config);
            return runtime;
        }

        private static GatebreakerMatchRuntime CreateRuntime()
        {
            return CreateRuntime(CreateStartConfig());
        }

        private static GatebreakerMatchStartConfig CreateStartConfig()
        {
            return new GatebreakerMatchStartConfig
            {
                MatchId = "lockstep-test",
                Seed = 20260518,
                SimulationFps = GatebreakerMatchStartConfig.DefaultSimulationFps,
                InputDelayFrames = 0,
                ModeId = "PVE_STANDARD",
                MapId = "MAP_ARENA_01",
                BallTypeId = "BALL_NORMAL",
                ActiveSlots = new[] { 1, 2 },
                LocalPlayerId = 1,
                ConfigHash = "test-config",
                TuningHash = "test-tuning",
            };
        }
    }
}

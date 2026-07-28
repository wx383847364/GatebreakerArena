using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using App.HotUpdate.GatebreakerArena.BrickDuel;
using App.HotUpdate.GatebreakerArena.Mode;
using NUnit.Framework;
using UnityEngine;

namespace Gatebreaker.Tests
{
    public sealed class BrickDuelRuntimeTests
    {
        [Test]
        public void Countdown_StartsAllSystemsOnTheSameFixedFrame()
        {
            BrickDuelRuntime runtime = CreateRuntime();
            runtime.BeginCountdown();

            Step(runtime, runtime.Rule.CountdownSeconds * runtime.Rule.SimulationFps - 1);
            Assert.AreEqual(BrickDuelPhase.Countdown, runtime.Phase);
            Assert.AreEqual(0, runtime.ElapsedFrames);
            Assert.IsFalse(runtime.BottomBall.IsActive);
            Assert.IsFalse(runtime.TopBall.IsActive);

            runtime.StepFrame(new BrickDuelFrameInput(0f));

            Assert.AreEqual(BrickDuelPhase.Playing, runtime.Phase);
            Assert.AreEqual(1, runtime.ElapsedFrames);
            Assert.IsTrue(runtime.BottomBall.IsActive);
            Assert.IsTrue(runtime.TopBall.IsActive);
        }

        [Test]
        public void OpeningRows_AreMirroredAndUseTheConfiguredThreeByNinePattern()
        {
            BrickDuelRuntime runtime = CreateRuntime();
            runtime.BeginCountdown();

            Assert.AreEqual(54, runtime.Bricks.Count);
            foreach (BrickDuelBrickState bottom in runtime.Bricks.Where(item => item.Side == BrickDuelSide.Bottom))
            {
                BrickDuelBrickState top = runtime.Bricks.Single(item =>
                    item.Side == BrickDuelSide.Top &&
                    item.InitialType == bottom.InitialType &&
                    item.Position.x == bottom.Position.x &&
                    item.Position.y == -bottom.Position.y);
                Assert.AreEqual(bottom.Health, top.Health);
            }
        }

        [Test]
        public void Ball_NeverCrossesTheCenterBoundary()
        {
            BrickDuelRuntime runtime = CreateRuntime();
            runtime.BeginCountdown();

            for (int frame = 0; frame < 1800 && runtime.Phase != BrickDuelPhase.Result; frame++)
            {
                runtime.StepFrame(new BrickDuelFrameInput(0f));
                Assert.LessOrEqual(runtime.BottomBall.Position.y, -runtime.Rule.BallRadius + 0.001f);
                Assert.GreaterOrEqual(runtime.TopBall.Position.y, runtime.Rule.BallRadius - 0.001f);
            }
        }

        [Test]
        public void Tide_RefillsOneMirroredRowAfterOneBrickHeightOfTravel()
        {
            BrickDuelRuleDefinition rule = CreateRule();
            rule.BaseTideSpeed = rule.BrickHeight;
            BrickDuelRuntime runtime = new BrickDuelRuntime(rule, CreateAiRule());
            runtime.BeginCountdown();

            Step(runtime, rule.CountdownSeconds * rule.SimulationFps + rule.SimulationFps - 1);

            Assert.AreEqual(72, runtime.Bricks.Count);
            int newestStartId = runtime.Bricks.Max(item => item.BrickId) - rule.Columns * 2 + 1;
            BrickDuelBrickState[] newest = runtime.Bricks
                .Where(item => item.BrickId >= newestStartId)
                .ToArray();
            Assert.AreEqual(rule.Columns * 2, newest.Length);
            for (int column = 0; column < rule.Columns; column++)
            {
                BrickDuelBrickState bottom = newest[column * 2];
                BrickDuelBrickState top = newest[column * 2 + 1];
                Assert.AreEqual(BrickDuelSide.Bottom, bottom.Side);
                Assert.AreEqual(BrickDuelSide.Top, top.Side);
                Assert.AreEqual(bottom.InitialType, top.InitialType);
                Assert.AreEqual(bottom.Position.x, top.Position.x);
                Assert.AreEqual(-bottom.Position.y, top.Position.y, 0.0001f);
            }
        }

        [Test]
        public void ContinuousCollision_DoesNotTunnelThroughOpeningBricks()
        {
            BrickDuelRuleDefinition rule = CreateRule();
            rule.BallSpeed = 120f;
            BrickDuelRuntime runtime = new BrickDuelRuntime(rule, CreateAiRule());
            runtime.BeginCountdown();

            Step(runtime, rule.CountdownSeconds * rule.SimulationFps);

            Assert.IsTrue(runtime.Bricks.Any(item =>
                item.Side == BrickDuelSide.Bottom &&
                item.Health < InitialHealth(rule, item.InitialType)));
        }

        [Test]
        public void ContinuousCollision_UsesMovingPaddlePathInsteadOfOnlyItsFinalPosition()
        {
            BrickDuelRuleDefinition rule = CreateRule();
            rule.ArenaHalfWidth = 10f;
            rule.CoreLineY = 10f;
            rule.BallRadius = 0.1f;
            rule.BallSpeed = 1f;
            var ball = new BrickDuelBallState();
            SetState(ball, nameof(BrickDuelBallState.Side), BrickDuelSide.Bottom);
            SetState(ball, nameof(BrickDuelBallState.Position), new Vector2(0f, -1.3f));
            SetState(ball, nameof(BrickDuelBallState.Velocity), Vector2.down);
            SetState(ball, nameof(BrickDuelBallState.IsActive), true);
            var paddle = new BrickDuelPaddleState();
            SetState(paddle, nameof(BrickDuelPaddleState.Side), BrickDuelSide.Bottom);
            SetState(paddle, nameof(BrickDuelPaddleState.Position), new Vector2(1f, -2f));
            SetState(paddle, nameof(BrickDuelPaddleState.MoveAxis), 1f);

            new BrickDuelCollisionSolver().StepBall(
                ball,
                paddle,
                new Vector2(-1f, -2f),
                new Vector2(2f, 0f),
                new List<BrickDuelBrickState>(),
                rule,
                1f,
                0f,
                new HashSet<int>());

            Assert.Greater(ball.Velocity.y, 0f);
        }

        [Test]
        public void ContinuousCollision_UsesRelativeVelocityForMovingBrickTide()
        {
            BrickDuelRuleDefinition rule = CreateRule();
            rule.ArenaHalfWidth = 10f;
            rule.CoreLineY = 10f;
            rule.BallRadius = 0.1f;
            rule.BallSpeed = 1f;
            var ball = new BrickDuelBallState();
            SetState(ball, nameof(BrickDuelBallState.Side), BrickDuelSide.Bottom);
            SetState(ball, nameof(BrickDuelBallState.Position), new Vector2(0f, -1.3f));
            SetState(ball, nameof(BrickDuelBallState.Velocity), Vector2.up);
            SetState(ball, nameof(BrickDuelBallState.IsActive), true);
            var paddle = new BrickDuelPaddleState();
            SetState(paddle, nameof(BrickDuelPaddleState.Side), BrickDuelSide.Bottom);
            SetState(paddle, nameof(BrickDuelPaddleState.Position), new Vector2(5f, -9f));
            var brick = new BrickDuelBrickState();
            SetState(brick, nameof(BrickDuelBrickState.BrickId), 7);
            SetState(brick, nameof(BrickDuelBrickState.Side), BrickDuelSide.Bottom);
            SetState(brick, nameof(BrickDuelBrickState.InitialType), BrickDuelBrickType.Green);
            SetState(brick, nameof(BrickDuelBrickState.Health), 1);
            SetState(brick, nameof(BrickDuelBrickState.Position), new Vector2(0f, -0.2f));
            var hitBrickIds = new HashSet<int>();

            new BrickDuelCollisionSolver().StepBall(
                ball,
                paddle,
                paddle.Position,
                Vector2.zero,
                new List<BrickDuelBrickState> { brick },
                rule,
                1f,
                2f,
                hitBrickIds);

            CollectionAssert.Contains(hitBrickIds, 7);
            Assert.Less(ball.Velocity.y, 0f);
        }

        [Test]
        public void ContinuousCollision_ResolvesApproachingInitialOverlap()
        {
            BrickDuelRuleDefinition rule = CreateRule();
            rule.ArenaHalfWidth = 10f;
            rule.CoreLineY = 10f;
            rule.BallRadius = 0.1f;
            rule.BallSpeed = 1f;
            var ball = new BrickDuelBallState();
            SetState(ball, nameof(BrickDuelBallState.Side), BrickDuelSide.Bottom);
            SetState(ball, nameof(BrickDuelBallState.Position), new Vector2(0f, -0.2f));
            SetState(ball, nameof(BrickDuelBallState.Velocity), Vector2.up);
            SetState(ball, nameof(BrickDuelBallState.IsActive), true);
            var paddle = new BrickDuelPaddleState();
            SetState(paddle, nameof(BrickDuelPaddleState.Side), BrickDuelSide.Bottom);
            SetState(paddle, nameof(BrickDuelPaddleState.Position), new Vector2(5f, -9f));
            var brick = new BrickDuelBrickState();
            SetState(brick, nameof(BrickDuelBrickState.BrickId), 11);
            SetState(brick, nameof(BrickDuelBrickState.Side), BrickDuelSide.Bottom);
            SetState(brick, nameof(BrickDuelBrickState.InitialType), BrickDuelBrickType.Green);
            SetState(brick, nameof(BrickDuelBrickState.Health), 1);
            SetState(brick, nameof(BrickDuelBrickState.Position), Vector2.zero);
            var hitBrickIds = new HashSet<int>();

            new BrickDuelCollisionSolver().StepBall(
                ball,
                paddle,
                paddle.Position,
                Vector2.zero,
                new List<BrickDuelBrickState> { brick },
                rule,
                0.1f,
                0f,
                hitBrickIds);

            CollectionAssert.Contains(hitBrickIds, 11);
            Assert.Less(ball.Velocity.y, 0f);
        }

        [Test]
        public void StuckBall_ResetsWithoutCoreDamageAndRelaunchesTowardItsTide()
        {
            BrickDuelRuleDefinition rule = CreateRule();
            rule.StuckMovementEpsilon = 1000f;
            rule.StuckTimeoutSeconds = 1f / rule.SimulationFps;
            BrickDuelRuntime runtime = new BrickDuelRuntime(rule, CreateAiRule());
            runtime.BeginCountdown();
            Step(runtime, rule.CountdownSeconds * rule.SimulationFps);

            Assert.IsFalse(runtime.BottomBall.IsActive);
            Assert.IsFalse(runtime.TopBall.IsActive);
            Assert.AreEqual(rule.InitialCoreHealth, runtime.BottomCoreHealth);
            Assert.AreEqual(rule.InitialCoreHealth, runtime.TopCoreHealth);

            Step(runtime, (int)(rule.BallResetSeconds * rule.SimulationFps));

            Assert.IsTrue(runtime.BottomBall.IsActive);
            Assert.IsTrue(runtime.TopBall.IsActive);
            Assert.Greater(runtime.BottomBall.Velocity.y, 0f);
            Assert.Less(runtime.TopBall.Velocity.y, 0f);
        }

        [Test]
        public void Pressure_AdvancesAtThirtySecondsAndMovesRefillCadenceWithIt()
        {
            BrickDuelRuntime runtime = CreateRuntime();
            runtime.BeginCountdown();
            Step(runtime, runtime.Rule.CountdownSeconds * runtime.Rule.SimulationFps);
            Step(runtime, runtime.PressureIntervalFrames - 2);

            Assert.AreEqual(0, runtime.PressureLevel);
            Assert.AreEqual(1f, runtime.PressureMultiplier);

            runtime.StepFrame(new BrickDuelFrameInput(0f));

            Assert.AreEqual(1, runtime.PressureLevel);
            Assert.AreEqual(1.25f, runtime.PressureMultiplier);
            Assert.IsTrue(runtime.LastFrameEvents.PressureLevelChanged);
        }

        [Test]
        public void Pause_FreezesEffectiveSimulationWithoutCatchUp()
        {
            BrickDuelRuntime runtime = CreateRuntime();
            runtime.BeginCountdown();
            Step(runtime, runtime.Rule.CountdownSeconds * runtime.Rule.SimulationFps);
            runtime.SetPaused(true);
            int elapsed = runtime.ElapsedFrames;
            ulong checksum = runtime.GetChecksum();

            Step(runtime, 120);

            Assert.AreEqual(elapsed, runtime.ElapsedFrames);
            Assert.AreEqual(checksum, runtime.GetChecksum());
            runtime.SetPaused(false);
            runtime.StepFrame(new BrickDuelFrameInput(0f));
            Assert.AreEqual(elapsed + 1, runtime.ElapsedFrames);
        }

        [Test]
        public void SameInputsAndSeed_ProduceTheSameChecksum()
        {
            BrickDuelRuntime first = CreateRuntime();
            BrickDuelRuntime second = CreateRuntime();
            first.BeginCountdown();
            second.BeginCountdown();

            for (int frame = 0; frame < 2400; frame++)
            {
                float axis = frame % 120 < 60 ? -0.75f : 0.75f;
                BrickDuelFrameInput input = new BrickDuelFrameInput(axis);
                first.StepFrame(input);
                second.StepFrame(input);
            }

            Assert.AreEqual(first.GetChecksum(), second.GetChecksum());
        }

        [Test]
        public void Restart_ResetsBrickAndAiRandomStreams()
        {
            BrickDuelRuntime runtime = CreateRuntime();
            runtime.BeginCountdown();
            for (int frame = 0; frame < 2400; frame++)
            {
                runtime.StepFrame(new BrickDuelFrameInput(frame % 90 < 45 ? -0.5f : 0.5f));
            }

            runtime.BeginCountdown();
            BrickDuelRuntime fresh = CreateRuntime();
            fresh.BeginCountdown();
            for (int frame = 0; frame < 2400; frame++)
            {
                BrickDuelFrameInput input =
                    new BrickDuelFrameInput(frame % 90 < 45 ? -0.5f : 0.5f);
                runtime.StepFrame(input);
                fresh.StepFrame(input);
            }

            Assert.AreEqual(fresh.GetChecksum(), runtime.GetChecksum());
        }

        [Test]
        public void SameFrameCoreDamage_ResolvesAsDrawAfterAllBricks()
        {
            BrickDuelRuleDefinition rule = CreateRule();
            rule.CoreLineY = 0.5f;
            rule.PaddleSpawnY = 0.4f;
            rule.InitialCoreHealth = 5;
            BrickDuelRuntime runtime = new BrickDuelRuntime(rule, CreateAiRule());
            runtime.BeginCountdown();

            Step(runtime, rule.CountdownSeconds * rule.SimulationFps);

            Assert.AreEqual(BrickDuelPhase.Result, runtime.Phase);
            Assert.AreEqual(BrickDuelResult.Draw, runtime.Result);
            Assert.AreEqual(0, runtime.BottomCoreHealth);
            Assert.AreEqual(0, runtime.TopCoreHealth);
            Assert.Greater(runtime.LastFrameEvents.BottomCoreDamage, 0);
            Assert.Greater(runtime.LastFrameEvents.TopCoreDamage, 0);

            ulong frozenChecksum = runtime.GetChecksum();
            Step(runtime, 120);
            Assert.AreEqual(frozenChecksum, runtime.GetChecksum());
        }

        [Test]
        public void YellowBrick_ChangesStateOnTheSameEntityAndOnlyLosesOneHealth()
        {
            BrickDuelRuntime runtime = CreateRuntime();
            runtime.BeginCountdown();
            BrickDuelBrickState target = runtime.Bricks.Single(item =>
                item.Side == BrickDuelSide.Bottom &&
                item.InitialType == BrickDuelBrickType.Yellow &&
                item.Position.x == 0f &&
                item.Position.y < -1f);
            int targetId = target.BrickId;

            Step(runtime, runtime.Rule.CountdownSeconds * runtime.Rule.SimulationFps + 180);

            BrickDuelBrickState after = runtime.Bricks.Single(item => item.BrickId == targetId);
            Assert.AreEqual(2, after.Health);
            Assert.AreEqual(BrickDuelBrickType.Red, after.VisualType);
        }

        private static BrickDuelRuntime CreateRuntime()
        {
            return new BrickDuelRuntime(CreateRule(), CreateAiRule());
        }

        private static void Step(BrickDuelRuntime runtime, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                runtime.StepFrame(new BrickDuelFrameInput(0f));
            }
        }

        private static BrickDuelRuleDefinition CreateRule()
        {
            return new BrickDuelRuleDefinition
            {
                RuleId = "BRICK_DUEL_V0",
                SimulationFps = 30,
                CountdownSeconds = 5,
                InitialCoreHealth = 5,
                InitialRows = 3,
                Columns = 9,
                ArenaHalfWidth = 3.5f,
                CoreLineY = 5.4f,
                PaddleSpawnY = 4.7f,
                PaddleHalfWidth = 0.375f,
                PaddleHalfHeight = 0.075f,
                PaddleMoveSpeed = 8f,
                BrickWidth = 0.66f,
                BrickHeight = 0.46f,
                BallRadius = 0.2f,
                BallSpeed = 0.865333f,
                BaseTideSpeed = 0.035733f,
                BallResetSeconds = 0.5f,
                StuckTimeoutSeconds = 2f,
                StuckMovementEpsilon = 0.01f,
                PressureIntervalSeconds = 30f,
                PressureIncrement = 0.25f,
                DangerDistance = 0.92f,
                GreenHealth = 1,
                RedHealth = 2,
                YellowHealth = 3,
                MysteryHealth = 1,
                BrickCoreDamage = 1,
                GreenWeight = 0.25f,
                RedWeight = 0.25f,
                YellowWeight = 0.25f,
                MysteryWeight = 0.25f,
                RandomSeed = 1,
                AiLevelId = "AI_NORMAL",
                InitialRowPatterns = new[]
                {
                    "Green,Red,Yellow,Mystery,Green,Red,Yellow,Mystery,Green",
                    "Red,Yellow,Mystery,Green,Red,Yellow,Mystery,Green,Red",
                    "Yellow,Mystery,Green,Red,Yellow,Mystery,Green,Red,Yellow",
                },
            };
        }

        private static AiRuleDefinition CreateAiRule()
        {
            return new AiRuleDefinition
            {
                AILevelId = "AI_NORMAL",
                ReactionDelay = 0.18f,
                PredictError = 0.25f,
            };
        }

        private static int InitialHealth(
            BrickDuelRuleDefinition rule,
            BrickDuelBrickType type)
        {
            switch (type)
            {
                case BrickDuelBrickType.Green:
                    return rule.GreenHealth;
                case BrickDuelBrickType.Red:
                    return rule.RedHealth;
                case BrickDuelBrickType.Yellow:
                    return rule.YellowHealth;
                default:
                    return rule.MysteryHealth;
            }
        }

        private static void SetState(object target, string propertyName, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, propertyName);
            property.SetValue(target, value);
        }
    }
}

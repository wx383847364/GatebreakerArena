using System.Collections.Generic;
using System.Reflection;
using App.HotUpdate.GatebreakerArena.BrickDuel;
using App.HotUpdate.GatebreakerArena.Mode;
using NUnit.Framework;
using UnityEngine;

namespace Gatebreaker.Tests
{
    public sealed class BrickDuelTacticalAiControllerTests
    {
        [Test]
        public void Step_OnlyUsesFrontBrickFromEachColumn()
        {
            BrickDuelTacticalAiController ai = CreateController();
            BrickDuelPaddleState paddle = Paddle(BrickDuelSide.Top, 0f, 4.7f);
            var bricks = new List<BrickDuelBrickState>
            {
                Brick(1, BrickDuelSide.Top, BrickDuelBrickType.Green, 1, 0, 0f, 2f),
                Brick(2, BrickDuelSide.Top, BrickDuelBrickType.Mystery, 1, 0, 0f, 1f),
                Brick(3, BrickDuelSide.Top, BrickDuelBrickType.Green, 1, 1, 0.66f, 1.5f),
            };

            ai.Step(null, null, paddle, bricks, null, 0.04f, 0.375f, 0.1f);

            Assert.AreEqual(1, ai.CurrentTargetBrickId);
            Assert.AreEqual(BrickDuelTacticalAiBehavior.AimClear, ai.CurrentBehavior);
        }

        [Test]
        public void Step_EmergencyBrickBeatsSafeMystery()
        {
            BrickDuelTacticalAiController ai = CreateController();
            BrickDuelPaddleState paddle = Paddle(BrickDuelSide.Top, 0f, 4.7f);
            var bricks = new List<BrickDuelBrickState>
            {
                Brick(10, BrickDuelSide.Top, BrickDuelBrickType.Green, 1, 0, -0.66f, 4f),
                Brick(11, BrickDuelSide.Top, BrickDuelBrickType.Mystery, 1, 1, 0.66f, 2f),
            };
            BrickDuelBallState ball = Ball(20, BrickDuelSide.Top, 0f, 3f, 0f, 3f);

            ai.Step(ball, null, paddle, bricks, null, 0.04f, 0.375f, 0.1f);

            Assert.AreEqual(10, ai.CurrentTargetBrickId);
            Assert.AreEqual(20, ai.PlannedBallId);
            Assert.AreEqual(BrickDuelTacticalAiBehavior.AimEmergency, ai.CurrentBehavior);
        }

        [Test]
        public void Step_SafeMysteryBeatsOrdinaryFrontBrick()
        {
            BrickDuelTacticalAiController ai = CreateController();
            BrickDuelPaddleState paddle = Paddle(BrickDuelSide.Top, 0f, 4.7f);
            var bricks = new List<BrickDuelBrickState>
            {
                Brick(30, BrickDuelSide.Top, BrickDuelBrickType.Green, 1, 0, -0.66f, 2f),
                Brick(31, BrickDuelSide.Top, BrickDuelBrickType.Mystery, 1, 1, 0.66f, 1.5f),
            };

            ai.Step(null, null, paddle, bricks, null, 0.04f, 0.375f, 0.1f);

            Assert.AreEqual(31, ai.CurrentTargetBrickId);
            Assert.AreEqual(BrickDuelTacticalAiBehavior.AimMystery, ai.CurrentBehavior);
        }

        [Test]
        public void Step_CollectsReachableCapsuleUnlessEmergencyBallCanBeSteered()
        {
            BrickDuelTacticalAiController ai = CreateController();
            BrickDuelPaddleState paddle = Paddle(BrickDuelSide.Top, 0f, 4.7f);
            var safeBricks = new List<BrickDuelBrickState>
            {
                Brick(40, BrickDuelSide.Top, BrickDuelBrickType.Green, 1, 0, 0f, 2f),
            };
            var capsules = new List<BrickDuelItemCapsuleState>
            {
                Capsule(50, BrickDuelSide.Top, 1f, 4f),
            };
            BrickDuelBallState ball = Ball(60, BrickDuelSide.Top, 0f, 3f, 0f, 3f);

            ai.Step(ball, null, paddle, safeBricks, capsules, 0.04f, 0.375f, 0.1f);
            Assert.AreEqual(BrickDuelTacticalAiBehavior.CollectCapsule, ai.CurrentBehavior);
            Assert.AreEqual(-1, ai.PlannedBallId);

            var emergencyBricks = new List<BrickDuelBrickState>
            {
                Brick(41, BrickDuelSide.Top, BrickDuelBrickType.Green, 1, 0, 0f, 4f),
            };
            ai.Step(ball, null, paddle, emergencyBricks, capsules, 0.04f, 0.375f, 0.1f);
            Assert.AreEqual(BrickDuelTacticalAiBehavior.AimEmergency, ai.CurrentBehavior);
            Assert.AreEqual(60, ai.PlannedBallId);
        }

        [Test]
        public void Step_CollectsCapsuleWhileEmergencyBallIsOutsideControlWindow()
        {
            BrickDuelTacticalAiController ai = CreateController();
            BrickDuelPaddleState paddle = Paddle(BrickDuelSide.Top, 0f, 4.7f);
            var bricks = new List<BrickDuelBrickState>
            {
                Brick(61, BrickDuelSide.Top, BrickDuelBrickType.Green, 1, 0, 0f, 4f),
            };
            var capsules = new List<BrickDuelItemCapsuleState>
            {
                Capsule(62, BrickDuelSide.Top, 0.5f, 4f),
            };
            BrickDuelBallState farBall = Ball(63, BrickDuelSide.Top, 0f, 0f, 0f, 3f);

            ai.Step(farBall, null, paddle, bricks, capsules, 0.04f, 0.375f, 0.1f);

            Assert.AreEqual(BrickDuelTacticalAiBehavior.CollectCapsule, ai.CurrentBehavior);
            Assert.AreEqual(-1, ai.PlannedBallId);
        }

        [Test]
        public void Step_FallsBackToLowerPathErrorWithinSamePriorityTier()
        {
            BrickDuelTacticalAiController ai = CreateController();
            BrickDuelPaddleState paddle = Paddle(BrickDuelSide.Top, 0f, 4.7f);
            var bricks = new List<BrickDuelBrickState>
            {
                Brick(64, BrickDuelSide.Top, BrickDuelBrickType.Green, 1, 0, 2.7f, 4.2f),
                Brick(65, BrickDuelSide.Top, BrickDuelBrickType.Green, 1, 1, 0.4f, 4.1f),
            };
            BrickDuelBallState ball = Ball(66, BrickDuelSide.Top, 0f, 3f, 0f, 3f);

            ai.Step(ball, null, paddle, bricks, null, 0.04f, 0.375f, 0.1f);

            Assert.AreEqual(65, ai.CurrentTargetBrickId);
            Assert.AreEqual(BrickDuelTacticalAiBehavior.AimEmergency, ai.CurrentBehavior);
        }

        [Test]
        public void Step_RemainingHealthMakesRedBrickUrgentAndKeepsItLocked()
        {
            BrickDuelTacticalAiController ai = CreateController();
            BrickDuelPaddleState paddle = Paddle(BrickDuelSide.Top, 0f, 4.7f);
            var bricks = new List<BrickDuelBrickState>
            {
                Brick(70, BrickDuelSide.Top, BrickDuelBrickType.Red, 2, 0, -0.66f, 3.87f),
                Brick(71, BrickDuelSide.Top, BrickDuelBrickType.Green, 1, 1, 0.66f, 4.17f),
            };

            ai.Step(null, null, paddle, bricks, null, 0.04f, 0.375f, 0.1f);
            Assert.AreEqual(70, ai.CurrentTargetBrickId);

            SetState(bricks[0], nameof(BrickDuelBrickState.Health), 1);
            SetState(bricks[1], nameof(BrickDuelBrickState.Position), new Vector2(0.66f, 4.3f));
            ai.Step(null, null, paddle, bricks, null, 0.04f, 0.375f, 0.1f);
            Assert.AreEqual(70, ai.CurrentTargetBrickId, "same-tier targets stay locked until invalid");
        }

        [Test]
        public void Step_SelectsEarliestPrimaryOrSplitBallDeterministically()
        {
            BrickDuelTacticalAiController first = CreateController();
            BrickDuelTacticalAiController second = CreateController();
            BrickDuelPaddleState paddle = Paddle(BrickDuelSide.Top, 0f, 4.7f);
            var bricks = new List<BrickDuelBrickState>
            {
                Brick(80, BrickDuelSide.Top, BrickDuelBrickType.Green, 1, 0, 0f, 4f),
            };
            BrickDuelBallState primary = Ball(81, BrickDuelSide.Top, 0f, 2f, 0f, 3f);
            var splits = new List<BrickDuelBallState>
            {
                Ball(82, BrickDuelSide.Top, 0.5f, 4f, 0f, 3f),
            };

            float firstAxis = first.Step(
                primary, splits, paddle, bricks, null, 0.04f, 0.375f, 0.1f);
            float secondAxis = second.Step(
                primary, splits, paddle, bricks, null, 0.04f, 0.375f, 0.1f);

            Assert.AreEqual(82, first.PlannedBallId);
            Assert.AreEqual(firstAxis, secondAxis);
            Assert.AreEqual(first.CurrentTargetBrickId, second.CurrentTargetBrickId);
            Assert.AreEqual(first.TargetX, second.TargetX, 0.0001f);
        }

        [TestCase(BrickDuelSide.Top, 1f, 1f)]
        [TestCase(BrickDuelSide.Top, -1f, -1f)]
        [TestCase(BrickDuelSide.Bottom, 1f, 1f)]
        [TestCase(BrickDuelSide.Bottom, -1f, -1f)]
        public void Step_RealCollisionSolverSendsReboundTowardTarget(
            BrickDuelSide side,
            float targetBrickX,
            float expectedVelocityXSign)
        {
            BrickDuelTacticalAiController ai = CreateController(side);
            float sideSign = side == BrickDuelSide.Top ? 1f : -1f;
            BrickDuelPaddleState paddle = Paddle(side, 0f, sideSign * 4.7f);
            var bricks = new List<BrickDuelBrickState>
            {
                Brick(85, side, BrickDuelBrickType.Green, 1, 0, targetBrickX, sideSign * 4f),
            };
            BrickDuelBallState ball = Ball(side == BrickDuelSide.Top ? 86 : 87, side, 0f, sideSign * 3f, 0f, sideSign * 3f);

            ReboundResult result = SimulateUntilPaddleRebound(ai, paddle, ball, bricks);

            Assert.AreEqual(expectedVelocityXSign, Mathf.Sign(result.Velocity.x));
            Assert.AreEqual(-sideSign, Mathf.Sign(result.Velocity.y));
            Assert.IsTrue(result.HitTarget, "planned rebound must hit the real target collider");
        }

        [Test]
        public void Step_SkipsEarlierBallThatPaddleCannotReach()
        {
            BrickDuelTacticalAiController ai = CreateController();
            BrickDuelPaddleState paddle = Paddle(BrickDuelSide.Top, 0f, 4.7f);
            var bricks = new List<BrickDuelBrickState>
            {
                Brick(92, BrickDuelSide.Top, BrickDuelBrickType.Green, 1, 0, 0f, 4f),
            };
            BrickDuelBallState unreachable = Ball(
                93, BrickDuelSide.Top, 2.8f, 4.45f, 0f, 3f);
            var splits = new List<BrickDuelBallState>
            {
                Ball(94, BrickDuelSide.Top, 0f, 4f, 0f, 3f),
            };

            ai.Step(
                unreachable,
                splits,
                paddle,
                bricks,
                null,
                0.04f,
                0.375f,
                0.1f);

            Assert.AreEqual(94, ai.PlannedBallId);
        }

        [Test]
        public void Step_FoldsIncomingWallBounceAndKeepsAimInsidePaddleLimits()
        {
            BrickDuelTacticalAiController ai = CreateController();
            BrickDuelPaddleState paddle = Paddle(BrickDuelSide.Top, 0f, 4.7f);
            var bricks = new List<BrickDuelBrickState>
            {
                Brick(87, BrickDuelSide.Top, BrickDuelBrickType.Green, 1, 0, 0f, 4f),
            };
            BrickDuelBallState ball = Ball(88, BrickDuelSide.Top, 2.8f, 3f, 1f, 3f);

            ReboundResult result = SimulateUntilPaddleRebound(ai, paddle, ball, bricks);

            Assert.IsFalse(float.IsNaN(ai.TargetX));
            Assert.LessOrEqual(Mathf.Abs(ai.TargetX), 3f - 0.375f + 0.0001f);
            Assert.Less(result.Velocity.x, 0f);
        }

        [Test]
        public void Step_ProjectsBrickAcrossIncomingAndOutgoingFlightTime()
        {
            BrickDuelPaddleState paddle = Paddle(BrickDuelSide.Top, 0f, 4.7f);
            var bricks = new List<BrickDuelBrickState>
            {
                Brick(95, BrickDuelSide.Top, BrickDuelBrickType.Green, 1, 0, 1f, 2f),
            };
            BrickDuelTacticalAiController nearAi = CreateController();
            BrickDuelTacticalAiController farAi = CreateController();
            BrickDuelBallState nearBall = Ball(96, BrickDuelSide.Top, 0f, 4f, 0f, 3f);
            BrickDuelBallState farBall = Ball(96, BrickDuelSide.Top, 0f, 1f, 0f, 3f);

            nearAi.Step(nearBall, null, paddle, bricks, null, 0.5f, 0.375f, 0.1f);
            farAi.Step(farBall, null, paddle, bricks, null, 0.5f, 0.375f, 0.1f);

            Assert.Greater(Mathf.Abs(nearAi.TargetX - farAi.TargetX), 0.01f);
        }

        [Test]
        public void Step_UsesReachableOutgoingWallShotAtPaddleEdge()
        {
            BrickDuelTacticalAiController ai = CreateController();
            BrickDuelPaddleState paddle = Paddle(BrickDuelSide.Top, 2.55f, 4.7f);
            var bricks = new List<BrickDuelBrickState>
            {
                Brick(97, BrickDuelSide.Top, BrickDuelBrickType.Green, 1, 0, 2.6f, 4f),
            };
            BrickDuelBallState ball = Ball(98, BrickDuelSide.Top, 2.8f, 4.45f, 0f, 3f);

            ReboundResult result = SimulateUntilPaddleRebound(ai, paddle, ball, bricks);

            Assert.AreEqual(1, ai.PlannedWallBounces);
            Assert.Greater(result.Velocity.x, 0f, "outgoing ball must first travel to the right wall");
            Assert.IsTrue(result.HitTarget, "wall-reflected shot must hit the target collider");
        }

        [Test]
        public void Step_MirrorsFrontSelectionForBottomSide()
        {
            BrickDuelTacticalAiController ai = CreateController(BrickDuelSide.Bottom);
            BrickDuelPaddleState paddle = Paddle(BrickDuelSide.Bottom, 0f, -4.7f);
            var bricks = new List<BrickDuelBrickState>
            {
                Brick(90, BrickDuelSide.Bottom, BrickDuelBrickType.Green, 1, 0, 0f, -4f),
                Brick(91, BrickDuelSide.Bottom, BrickDuelBrickType.Mystery, 1, 0, 0f, -2f),
            };

            ai.Step(null, null, paddle, bricks, null, 0.04f, 0.375f, 0.1f);

            Assert.AreEqual(90, ai.CurrentTargetBrickId);
            Assert.AreEqual(BrickDuelTacticalAiBehavior.AimEmergency, ai.CurrentBehavior);
        }

        private static BrickDuelTacticalAiController CreateController(
            BrickDuelSide side = BrickDuelSide.Top)
        {
            return new BrickDuelTacticalAiController(
                CreateRule(),
                new BrickDuelAiRuleDefinition
                {
                    RuleId = "BRICK_DUEL_AI_TACTICAL",
                    DecisionIntervalFrames = 1,
                    EmergencyDistance = 0.92f,
                    MoveDeadZone = 0.04f,
                },
                side);
        }

        private static BrickDuelRuleDefinition CreateRule()
        {
            return new BrickDuelRuleDefinition
            {
                SimulationFps = 30,
                ArenaHalfWidth = 3f,
                CoreLineY = 4.9f,
                PaddleSpawnY = 4.7f,
                PaddleHalfWidth = 0.375f,
                PaddleHalfHeight = 0.075f,
                PaddleMoveSpeed = 8f,
                BrickWidth = 0.66f,
                BrickHeight = 0.46f,
                BallRadius = 0.1f,
                BallSpeed = 3f,
            };
        }

        private static BrickDuelBrickState Brick(
            int id,
            BrickDuelSide side,
            BrickDuelBrickType type,
            int health,
            int column,
            float x,
            float y)
        {
            var brick = new BrickDuelBrickState();
            SetState(brick, nameof(BrickDuelBrickState.BrickId), id);
            SetState(brick, nameof(BrickDuelBrickState.Side), side);
            SetState(brick, nameof(BrickDuelBrickState.InitialType), type);
            SetState(brick, nameof(BrickDuelBrickState.Health), health);
            SetState(brick, nameof(BrickDuelBrickState.ColumnId), column);
            SetState(brick, nameof(BrickDuelBrickState.Position), new Vector2(x, y));
            return brick;
        }

        private static BrickDuelBallState Ball(
            int id,
            BrickDuelSide side,
            float x,
            float y,
            float velocityX,
            float velocityY)
        {
            var ball = new BrickDuelBallState();
            SetState(ball, nameof(BrickDuelBallState.BallId), id);
            SetState(ball, nameof(BrickDuelBallState.Side), side);
            SetState(ball, nameof(BrickDuelBallState.Position), new Vector2(x, y));
            SetState(ball, nameof(BrickDuelBallState.Velocity), new Vector2(velocityX, velocityY));
            SetState(ball, nameof(BrickDuelBallState.IsActive), true);
            return ball;
        }

        private static BrickDuelPaddleState Paddle(
            BrickDuelSide side,
            float x,
            float y)
        {
            var paddle = new BrickDuelPaddleState();
            SetState(paddle, nameof(BrickDuelPaddleState.Side), side);
            SetState(paddle, nameof(BrickDuelPaddleState.Position), new Vector2(x, y));
            return paddle;
        }

        private static BrickDuelItemCapsuleState Capsule(
            int id,
            BrickDuelSide side,
            float x,
            float y)
        {
            var capsule = new BrickDuelItemCapsuleState();
            SetState(capsule, nameof(BrickDuelItemCapsuleState.CapsuleId), id);
            SetState(capsule, nameof(BrickDuelItemCapsuleState.Side), side);
            SetState(capsule, nameof(BrickDuelItemCapsuleState.Position), new Vector2(x, y));
            return capsule;
        }

        private static void SetState<T>(T target, string propertyName, object value)
        {
            PropertyInfo property = typeof(T).GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            property.SetValue(target, value, null);
        }

        private static ReboundResult SimulateUntilPaddleRebound(
            BrickDuelTacticalAiController ai,
            BrickDuelPaddleState paddle,
            BrickDuelBallState ball,
            IReadOnlyList<BrickDuelBrickState> tacticalBricks)
        {
            BrickDuelRuleDefinition rule = CreateRule();
            var solver = new BrickDuelCollisionSolver();
            var collisionBricks = new List<BrickDuelBrickState>();
            var ignored = new HashSet<int>();
            var hit = new HashSet<int>();
            float frameDelta = 1f / rule.SimulationFps;
            float sideSign = ball.Side == BrickDuelSide.Top ? 1f : -1f;
            int pierceCharges = 0;
            for (int frame = 0; frame < 120; frame++)
            {
                Vector2 paddleStart = paddle.Position;
                float axis = ai.Step(
                    ball,
                    null,
                    paddle,
                    tacticalBricks,
                    null,
                    0.04f,
                    rule.PaddleHalfWidth,
                    rule.BallRadius);
                SetState(paddle, nameof(BrickDuelPaddleState.MoveAxis), axis);
                float limit = rule.ArenaHalfWidth - rule.PaddleHalfWidth;
                Vector2 nextPaddle = new Vector2(
                    Mathf.Clamp(
                        paddleStart.x + axis * rule.PaddleMoveSpeed * frameDelta,
                        -limit,
                        limit),
                    paddleStart.y);
                SetState(paddle, nameof(BrickDuelPaddleState.Position), nextPaddle);
                Vector2 paddleVelocity = (nextPaddle - paddleStart) / frameDelta;
                solver.StepBall(
                    ball,
                    paddle,
                    paddleStart,
                    paddleVelocity,
                    collisionBricks,
                    rule,
                    frameDelta,
                    0f,
                    rule.PaddleHalfWidth,
                    rule.BallRadius,
                    ref pierceCharges,
                    ignored,
                    hit);
                if (ball.Velocity.y * sideSign < 0f)
                {
                    Vector2 reboundVelocity = ball.Velocity;
                    collisionBricks.AddRange(tacticalBricks);
                    ignored.Clear();
                    hit.Clear();
                    for (int outgoingFrame = 0; outgoingFrame < 120; outgoingFrame++)
                    {
                        solver.StepBall(
                            ball,
                            paddle,
                            paddle.Position,
                            Vector2.zero,
                            collisionBricks,
                            rule,
                            frameDelta,
                            0.04f,
                            rule.PaddleHalfWidth,
                            rule.BallRadius,
                            ref pierceCharges,
                            ignored,
                            hit);
                        if (hit.Contains(tacticalBricks[0].BrickId))
                        {
                            return new ReboundResult(reboundVelocity, true);
                        }
                    }
                    return new ReboundResult(reboundVelocity, false);
                }
            }

            Assert.Fail("Ball did not rebound from the tactical paddle.");
            return new ReboundResult(Vector2.zero, false);
        }

        private readonly struct ReboundResult
        {
            public ReboundResult(Vector2 velocity, bool hitTarget)
            {
                Velocity = velocity;
                HitTarget = hitTarget;
            }

            public Vector2 Velocity { get; }
            public bool HitTarget { get; }
        }
    }
}

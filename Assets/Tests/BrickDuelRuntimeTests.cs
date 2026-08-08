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
        public void OpeningRows_AreMirroredAndDrawnFromCompositionStageZero()
        {
            BrickDuelRuleDefinition rule = CreateRule();
            rule.BrickCompositionStages = new[]
            {
                new BrickDuelCompositionStageDefinition
                {
                    GreenWeight = 1f,
                    RedWeight = 0f,
                    YellowWeight = 0f,
                    MysteryWeight = 0f,
                },
                new BrickDuelCompositionStageDefinition
                {
                    GreenWeight = 0f,
                    RedWeight = 1f,
                    YellowWeight = 0f,
                    MysteryWeight = 0f,
                },
                new BrickDuelCompositionStageDefinition
                {
                    GreenWeight = 0f,
                    RedWeight = 1f,
                    YellowWeight = 0f,
                    MysteryWeight = 0f,
                },
                new BrickDuelCompositionStageDefinition
                {
                    GreenWeight = 0f,
                    RedWeight = 1f,
                    YellowWeight = 0f,
                    MysteryWeight = 0f,
                },
                new BrickDuelCompositionStageDefinition
                {
                    GreenWeight = 0f,
                    RedWeight = 1f,
                    YellowWeight = 0f,
                    MysteryWeight = 0f,
                },
                new BrickDuelCompositionStageDefinition
                {
                    GreenWeight = 0f,
                    RedWeight = 1f,
                    YellowWeight = 0f,
                    MysteryWeight = 0f,
                },
            };
            BrickDuelRuntime runtime = new BrickDuelRuntime(rule, CreateAiRule());
            runtime.BeginCountdown();

            Assert.AreEqual(54, runtime.Bricks.Count);
            Assert.IsTrue(runtime.Bricks.All(item => item.InitialType == BrickDuelBrickType.Green));
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
                .OrderBy(item => item.BrickId)
                .ToArray();
            Assert.AreEqual(rule.Columns * 2, newest.Length);
            BrickDuelBrickState[] bottoms = newest.Where(item => item.Side == BrickDuelSide.Bottom).ToArray();
            BrickDuelBrickState[] tops = newest.Where(item => item.Side == BrickDuelSide.Top).ToArray();
            Assert.AreEqual(rule.Columns, bottoms.Length);
            Assert.AreEqual(rule.Columns, tops.Length);
            for (int column = 0; column < rule.Columns; column++)
            {
                BrickDuelBrickState bottom = bottoms[column];
                BrickDuelBrickState top = tops[column];
                Assert.AreEqual(bottom.InitialType, top.InitialType);
                Assert.AreEqual(bottom.ItemId, top.ItemId);
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

        [TestCase(BrickDuelSide.Bottom, 0f, -5f, 0f, -1f, 0f, 1f)]
        [TestCase(BrickDuelSide.Top, 0f, 5f, 0f, 1f, 0f, -1f)]
        [TestCase(BrickDuelSide.Bottom, -3.1f, -2f, -1f, 0f, 1f, 0f)]
        [TestCase(BrickDuelSide.Bottom, 3.1f, -2f, 1f, 0f, -1f, 0f)]
        [TestCase(BrickDuelSide.Bottom, 0f, -0.4f, 0f, 1f, 0f, -1f)]
        [TestCase(BrickDuelSide.Top, 0f, 0.4f, 0f, -1f, 0f, 1f)]
        public void ContinuousCollision_ReflectsFromEveryClosedHalfFieldWall(
            BrickDuelSide side,
            float positionX,
            float positionY,
            float velocityX,
            float velocityY,
            float expectedX,
            float expectedY)
        {
            BrickDuelRuleDefinition rule = CreateRule();
            var ball = new BrickDuelBallState();
            SetState(ball, nameof(BrickDuelBallState.Side), side);
            SetState(ball, nameof(BrickDuelBallState.Position), new Vector2(positionX, positionY));
            SetState(
                ball,
                nameof(BrickDuelBallState.Velocity),
                new Vector2(velocityX, velocityY).normalized * rule.BallSpeed);
            SetState(ball, nameof(BrickDuelBallState.IsActive), true);
            var paddle = new BrickDuelPaddleState();
            SetState(paddle, nameof(BrickDuelPaddleState.Side), side);
            SetState(paddle, nameof(BrickDuelPaddleState.Position), new Vector2(10f, 10f));

            StepBall(
                ball,
                paddle,
                paddle.Position,
                Vector2.zero,
                new List<BrickDuelBrickState>(),
                rule,
                0.2f,
                0f,
                new HashSet<int>());

            Assert.GreaterOrEqual(ball.Position.x, -rule.ArenaHalfWidth + rule.BallRadius - 0.001f);
            Assert.LessOrEqual(ball.Position.x, rule.ArenaHalfWidth - rule.BallRadius + 0.001f);
            if (side == BrickDuelSide.Bottom)
            {
                Assert.GreaterOrEqual(ball.Position.y, -rule.CoreLineY + rule.BallRadius - 0.001f);
                Assert.LessOrEqual(ball.Position.y, -rule.BallRadius + 0.001f);
            }
            else
            {
                Assert.GreaterOrEqual(ball.Position.y, rule.BallRadius - 0.001f);
                Assert.LessOrEqual(ball.Position.y, rule.CoreLineY - rule.BallRadius + 0.001f);
            }

            Assert.Greater(
                Vector2.Dot(ball.Velocity, new Vector2(expectedX, expectedY)),
                0f);
        }

        [Test]
        public void ContinuousCollision_RecoversOutsideBallByReflectingItInside()
        {
            BrickDuelRuleDefinition rule = CreateRule();
            var ball = new BrickDuelBallState();
            SetState(ball, nameof(BrickDuelBallState.Side), BrickDuelSide.Bottom);
            SetState(ball, nameof(BrickDuelBallState.Position), new Vector2(4f, -6f));
            SetState(
                ball,
                nameof(BrickDuelBallState.Velocity),
                new Vector2(1f, -1f).normalized * rule.BallSpeed);
            SetState(ball, nameof(BrickDuelBallState.IsActive), true);
            var paddle = new BrickDuelPaddleState();
            SetState(paddle, nameof(BrickDuelPaddleState.Side), BrickDuelSide.Bottom);
            SetState(paddle, nameof(BrickDuelPaddleState.Position), new Vector2(10f, 10f));

            StepBall(
                ball,
                paddle,
                paddle.Position,
                Vector2.zero,
                new List<BrickDuelBrickState>(),
                rule,
                0.01f,
                0f,
                new HashSet<int>());

            Assert.IsTrue(ball.IsActive);
            Assert.LessOrEqual(ball.Position.x, rule.ArenaHalfWidth - rule.BallRadius + 0.001f);
            Assert.GreaterOrEqual(ball.Position.y, -rule.CoreLineY + rule.BallRadius - 0.001f);
            Assert.Less(ball.Velocity.x, 0f);
            Assert.Greater(ball.Velocity.y, 0f);
        }

        [Test]
        public void OuterWallBounce_DoesNotResetBallOrDamageCore()
        {
            BrickDuelRuntime runtime = CreateRuntime();
            runtime.BeginCountdown();
            Step(runtime, runtime.Rule.CountdownSeconds * runtime.Rule.SimulationFps);
            SetState(
                runtime.BottomBall,
                nameof(BrickDuelBallState.Position),
                new Vector2(0f, -runtime.Rule.CoreLineY + runtime.Rule.BallRadius + 0.02f));
            SetState(
                runtime.BottomBall,
                nameof(BrickDuelBallState.Velocity),
                Vector2.down * runtime.Rule.BallSpeed);
            int bottomHealth = runtime.BottomCoreHealth;
            int topHealth = runtime.TopCoreHealth;

            runtime.StepFrame(new BrickDuelFrameInput(0f));

            Assert.IsTrue(runtime.BottomBall.IsActive);
            Assert.IsFalse(runtime.LastFrameEvents.BottomBallReset);
            Assert.AreEqual(bottomHealth, runtime.BottomCoreHealth);
            Assert.AreEqual(topHealth, runtime.TopCoreHealth);
            Assert.Greater(runtime.BottomBall.Velocity.y, 0f);
        }

        [TestCase(BrickDuelSide.Bottom, -2f, -1.3f, -1f)]
        [TestCase(BrickDuelSide.Top, 2f, 1.3f, 1f)]
        public void ContinuousCollision_PaddleReflectsApproachingFront(
            BrickDuelSide side,
            float paddleY,
            float ballY,
            float incomingY)
        {
            BrickDuelRuleDefinition rule = CreateRule();
            rule.ArenaHalfWidth = 10f;
            rule.CoreLineY = 10f;
            rule.BallSpeed = 120f;
            var ball = new BrickDuelBallState();
            SetState(ball, nameof(BrickDuelBallState.Side), side);
            SetState(ball, nameof(BrickDuelBallState.Position), new Vector2(0f, ballY));
            SetState(ball, nameof(BrickDuelBallState.Velocity), Vector2.up * incomingY * rule.BallSpeed);
            SetState(ball, nameof(BrickDuelBallState.IsActive), true);
            var paddle = new BrickDuelPaddleState();
            SetState(paddle, nameof(BrickDuelPaddleState.Side), side);
            SetState(paddle, nameof(BrickDuelPaddleState.Position), new Vector2(0f, paddleY));

            StepBall(
                ball,
                paddle,
                paddle.Position,
                Vector2.zero,
                new List<BrickDuelBrickState>(),
                rule,
                0.005f,
                0f,
                new HashSet<int>());

            Vector2 frontNormal = side == BrickDuelSide.Bottom ? Vector2.up : Vector2.down;
            Assert.Greater(Vector2.Dot(ball.Velocity, frontNormal), 0f);
        }

        [TestCase(BrickDuelSide.Bottom, -2f, -2.1f, 1f)]
        [TestCase(BrickDuelSide.Top, 2f, 2.1f, -1f)]
        public void ContinuousCollision_BacksideBallPassesThroughPaddle(
            BrickDuelSide side,
            float paddleY,
            float ballY,
            float returningY)
        {
            BrickDuelRuleDefinition rule = CreateRule();
            rule.ArenaHalfWidth = 10f;
            rule.CoreLineY = 10f;
            var ball = new BrickDuelBallState();
            SetState(ball, nameof(BrickDuelBallState.Side), side);
            SetState(ball, nameof(BrickDuelBallState.Position), new Vector2(0f, ballY));
            SetState(ball, nameof(BrickDuelBallState.Velocity), Vector2.up * returningY * rule.BallSpeed);
            SetState(ball, nameof(BrickDuelBallState.IsActive), true);
            var paddle = new BrickDuelPaddleState();
            SetState(paddle, nameof(BrickDuelPaddleState.Side), side);
            SetState(paddle, nameof(BrickDuelPaddleState.Position), new Vector2(0f, paddleY));

            StepBall(
                ball,
                paddle,
                paddle.Position,
                Vector2.zero,
                new List<BrickDuelBrickState>(),
                rule,
                0.15f,
                0f,
                new HashSet<int>());

            Vector2 frontNormal = side == BrickDuelSide.Bottom ? Vector2.up : Vector2.down;
            float contactDistance = rule.PaddleHalfHeight + rule.BallRadius;
            Assert.Greater(Vector2.Dot(ball.Velocity, frontNormal), 0f);
            Assert.Greater(
                Vector2.Dot(ball.Position - paddle.Position, frontNormal),
                contactDistance);
        }

        [TestCase(BrickDuelSide.Bottom, -2f, -1.8f, -1f)]
        [TestCase(BrickDuelSide.Top, 2f, 1.8f, 1f)]
        public void ContinuousCollision_RecoversApproachingFrontOverlap(
            BrickDuelSide side,
            float paddleY,
            float ballY,
            float incomingY)
        {
            BrickDuelRuleDefinition rule = CreateRule();
            rule.ArenaHalfWidth = 10f;
            rule.CoreLineY = 10f;
            var ball = new BrickDuelBallState();
            SetState(ball, nameof(BrickDuelBallState.Side), side);
            SetState(ball, nameof(BrickDuelBallState.Position), new Vector2(0f, ballY));
            SetState(ball, nameof(BrickDuelBallState.Velocity), Vector2.up * incomingY * rule.BallSpeed);
            SetState(ball, nameof(BrickDuelBallState.IsActive), true);
            var paddle = new BrickDuelPaddleState();
            SetState(paddle, nameof(BrickDuelPaddleState.Side), side);
            SetState(paddle, nameof(BrickDuelPaddleState.Position), new Vector2(0f, paddleY));

            StepBall(
                ball,
                paddle,
                paddle.Position,
                Vector2.zero,
                new List<BrickDuelBrickState>(),
                rule,
                0.05f,
                0f,
                new HashSet<int>());

            Vector2 frontNormal = side == BrickDuelSide.Bottom ? Vector2.up : Vector2.down;
            float contactDistance = rule.PaddleHalfHeight + rule.BallRadius;
            Assert.Greater(Vector2.Dot(ball.Velocity, frontNormal), 0f);
            Assert.GreaterOrEqual(
                Vector2.Dot(ball.Position - paddle.Position, frontNormal),
                contactDistance);
        }

        [TestCase(0.574f, true)]
        [TestCase(0.576f, false)]
        public void ContinuousCollision_PaddleFaceUsesExpandedHalfWidth(
            float ballX,
            bool shouldHit)
        {
            BrickDuelRuleDefinition rule = CreateRule();
            rule.ArenaHalfWidth = 10f;
            rule.CoreLineY = 10f;
            var ball = new BrickDuelBallState();
            SetState(ball, nameof(BrickDuelBallState.Side), BrickDuelSide.Bottom);
            SetState(ball, nameof(BrickDuelBallState.Position), new Vector2(ballX, -1.3f));
            SetState(ball, nameof(BrickDuelBallState.Velocity), Vector2.down * rule.BallSpeed);
            SetState(ball, nameof(BrickDuelBallState.IsActive), true);
            var paddle = new BrickDuelPaddleState();
            SetState(paddle, nameof(BrickDuelPaddleState.Side), BrickDuelSide.Bottom);
            SetState(paddle, nameof(BrickDuelPaddleState.Position), new Vector2(0f, -2f));

            StepBall(
                ball,
                paddle,
                paddle.Position,
                Vector2.zero,
                new List<BrickDuelBrickState>(),
                rule,
                0.2f,
                0f,
                new HashSet<int>());

            if (shouldHit)
            {
                Assert.Greater(ball.Velocity.y, 0f);
            }
            else
            {
                Assert.Less(ball.Velocity.y, 0f);
            }
        }

        [Test]
        public void OuterWallReturn_PassesThroughPaddleWithoutResetOrCoreDamage()
        {
            BrickDuelRuntime runtime = CreateRuntime();
            runtime.BeginCountdown();
            Step(runtime, runtime.Rule.CountdownSeconds * runtime.Rule.SimulationFps);
            SetState(
                runtime.BottomBall,
                nameof(BrickDuelBallState.Position),
                new Vector2(0f, -runtime.Rule.CoreLineY + runtime.Rule.BallRadius + 0.02f));
            SetState(
                runtime.BottomBall,
                nameof(BrickDuelBallState.Velocity),
                Vector2.down * runtime.Rule.BallSpeed);
            int bottomHealth = runtime.BottomCoreHealth;
            int topHealth = runtime.TopCoreHealth;
            bool resetOccurred = false;

            for (int frame = 0; frame < 12; frame++)
            {
                runtime.StepFrame(new BrickDuelFrameInput(0f));
                resetOccurred |= runtime.LastFrameEvents.BottomBallReset;
            }

            float frontContactY = runtime.BottomPaddle.Position.y +
                                  runtime.Rule.PaddleHalfHeight +
                                  runtime.Rule.BallRadius;
            Assert.IsTrue(runtime.BottomBall.IsActive);
            Assert.IsFalse(resetOccurred);
            Assert.AreEqual(bottomHealth, runtime.BottomCoreHealth);
            Assert.AreEqual(topHealth, runtime.TopCoreHealth);
            Assert.Greater(runtime.BottomBall.Position.y, frontContactY);
            Assert.Greater(runtime.BottomBall.Velocity.y, 0f);
        }

        [Test]
        public void CollisionOverlay_MatchesVisualBodyBoundsForWallsAndPaddles()
        {
            BrickDuelRuntime runtime = CreateRuntime();
            runtime.BeginCountdown();
            BrickDuelSnapshot snapshot = runtime.CreateSnapshot();

            IReadOnlyList<BrickDuelCollisionOverlayLine> lines =
                BrickDuelCollisionOverlayGeometry.BuildLines(runtime.Rule, snapshot);

            Assert.AreEqual(8, lines.Count(line => line.Kind == BrickDuelCollisionOverlayLineKind.Wall));
            Assert.AreEqual(8, lines.Count(line => line.Kind == BrickDuelCollisionOverlayLineKind.Paddle));
            Assert.AreEqual(
                snapshot.Bricks.Count * 4,
                lines.Count(line => line.Kind == BrickDuelCollisionOverlayLineKind.Brick));
            float minimumX = -runtime.Rule.ArenaHalfWidth;
            float maximumX = runtime.Rule.ArenaHalfWidth;
            Vector2 paddleExtents = new Vector2(
                runtime.Rule.PaddleHalfWidth,
                runtime.Rule.PaddleHalfHeight);
            Assert.IsTrue(HasOverlayLine(
                lines,
                BrickDuelCollisionOverlayLineKind.Wall,
                new Vector2(minimumX, -runtime.Rule.CoreLineY),
                new Vector2(maximumX, -runtime.Rule.CoreLineY)));
            Assert.IsTrue(HasOverlayLine(
                lines,
                BrickDuelCollisionOverlayLineKind.Wall,
                new Vector2(minimumX, 0f),
                new Vector2(maximumX, 0f)));
            Assert.IsTrue(HasOverlayLine(
                lines,
                BrickDuelCollisionOverlayLineKind.Wall,
                new Vector2(minimumX, runtime.Rule.CoreLineY),
                new Vector2(maximumX, runtime.Rule.CoreLineY)));
            Assert.IsTrue(HasOverlayAabb(
                lines,
                BrickDuelCollisionOverlayLineKind.Paddle,
                snapshot.BottomPaddle.Position,
                paddleExtents));
            Assert.IsTrue(HasOverlayAabb(
                lines,
                BrickDuelCollisionOverlayLineKind.Paddle,
                snapshot.TopPaddle.Position,
                paddleExtents));
            Vector2 brickExtents = new Vector2(
                runtime.Rule.BrickWidth * 0.5f,
                runtime.Rule.BrickHeight * 0.5f);
            BrickDuelBrickState firstBrick = snapshot.Bricks.First(brick => brick.Health > 0);
            Assert.IsTrue(HasOverlayAabb(
                lines,
                BrickDuelCollisionOverlayLineKind.Brick,
                firstBrick.Position,
                brickExtents));
        }

        [Test]
        public void CollisionOverlay_UsesExplicitSceneWallInnerBoundsWhenProvided()
        {
            BrickDuelRuntime runtime = CreateRuntime();
            runtime.BeginCountdown();
            BrickDuelSnapshot snapshot = runtime.CreateSnapshot();
            var wallBounds = new BrickDuelWallOverlayBounds(-2.7f, 2.7f, -4.6f, 4.6f);

            IReadOnlyList<BrickDuelCollisionOverlayLine> lines =
                BrickDuelCollisionOverlayGeometry.BuildLines(runtime.Rule, snapshot, wallBounds);

            Assert.IsTrue(HasOverlayLine(
                lines,
                BrickDuelCollisionOverlayLineKind.Wall,
                new Vector2(-2.7f, -4.6f),
                new Vector2(2.7f, -4.6f)));
            Assert.IsTrue(HasOverlayLine(
                lines,
                BrickDuelCollisionOverlayLineKind.Wall,
                new Vector2(-2.7f, 4.6f),
                new Vector2(2.7f, 4.6f)));
            Assert.IsTrue(HasOverlayLine(
                lines,
                BrickDuelCollisionOverlayLineKind.Wall,
                new Vector2(-2.7f, 0f),
                new Vector2(2.7f, 0f)));
        }

        [Test]
        public void ApplyWallInnerBounds_UpdatesRuleUsedByOverlayAndCollisionPlanes()
        {
            BrickDuelRuleDefinition rule = CreateRule();
            var wallBounds = new BrickDuelWallOverlayBounds(-2.75f, 2.8f, -4.55f, 4.6f);

            Assert.IsTrue(BrickDuelCollisionOverlayGeometry.TryApplyWallInnerBoundsToRule(
                rule,
                wallBounds));
            Assert.AreEqual(2.75f, rule.ArenaHalfWidth, 0.0001f);
            Assert.AreEqual(4.55f, rule.CoreLineY, 0.0001f);

            BrickDuelRuntime runtime = new BrickDuelRuntime(rule, CreateAiRule());
            runtime.BeginCountdown();
            BrickDuelSnapshot snapshot = runtime.CreateSnapshot();
            IReadOnlyList<BrickDuelCollisionOverlayLine> lines =
                BrickDuelCollisionOverlayGeometry.BuildLines(rule, snapshot);

            Assert.IsTrue(HasOverlayLine(
                lines,
                BrickDuelCollisionOverlayLineKind.Wall,
                new Vector2(-2.75f, -4.55f),
                new Vector2(2.75f, -4.55f)));
            Assert.IsTrue(HasOverlayLine(
                lines,
                BrickDuelCollisionOverlayLineKind.Wall,
                new Vector2(-2.75f, 4.55f),
                new Vector2(2.75f, 4.55f)));
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

            StepBall(
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

            StepBall(
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

            StepBall(
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
            Step(runtime, runtime.Rule.CountdownSeconds * runtime.Rule.SimulationFps);

            BrickDuelBrickState after = target;
            for (int frame = 0; frame < 600 && after.Health == 3; frame++)
            {
                runtime.StepFrame(new BrickDuelFrameInput(0f));
                after = runtime.Bricks.Single(item => item.BrickId == targetId);
            }

            Assert.AreEqual(2, after.Health);
            Assert.AreEqual(BrickDuelBrickType.Red, after.VisualType);
        }

        [Test]
        public void BrickComposition_ResolvesStageByElapsedTimeBoundaries()
        {
            BrickDuelRuleDefinition rule = CreateRule();

            Assert.AreEqual(0, rule.ResolveBrickCompositionStageIndex(0f));
            Assert.AreEqual(0, rule.ResolveBrickCompositionStageIndex(29.999f));
            Assert.AreEqual(1, rule.ResolveBrickCompositionStageIndex(30f));
            Assert.AreEqual(5, rule.ResolveBrickCompositionStageIndex(150f));
            Assert.AreEqual(5, rule.ResolveBrickCompositionStageIndex(999f));

            BrickDuelCompositionStageDefinition stage0 = rule.ResolveBrickCompositionWeights(0f);
            BrickDuelCompositionStageDefinition stage1 = rule.ResolveBrickCompositionWeights(30f);
            BrickDuelCompositionStageDefinition stage5 = rule.ResolveBrickCompositionWeights(150f);
            Assert.AreEqual(0.90f, stage0.GreenWeight, 0.0001f);
            Assert.AreEqual(0.00f, stage0.YellowWeight, 0.0001f);
            Assert.AreEqual(0.75f, stage1.GreenWeight, 0.0001f);
            Assert.AreEqual(0.15f, stage1.RedWeight, 0.0001f);
            Assert.AreEqual(0.20f, stage5.GreenWeight, 0.0001f);
            Assert.AreEqual(0.50f, stage5.RedWeight, 0.0001f);
            Assert.AreEqual(0.25f, stage5.YellowWeight, 0.0001f);
            Assert.AreEqual(
                1f,
                stage0.GreenWeight + stage0.RedWeight + stage0.YellowWeight + stage0.MysteryWeight,
                0.0001f);
            Assert.AreEqual(
                1f,
                stage5.GreenWeight + stage5.RedWeight + stage5.YellowWeight + stage5.MysteryWeight,
                0.0001f);
        }

        [Test]
        public void BrickComposition_NewRowsUseLaterStageWeightsAfterThirtySeconds()
        {
            BrickDuelRuleDefinition rule = CreateRule();
            rule.BaseTideSpeed = rule.BrickHeight;
            rule.BrickCompositionStages = new[]
            {
                new BrickDuelCompositionStageDefinition
                {
                    GreenWeight = 1f,
                    RedWeight = 0f,
                    YellowWeight = 0f,
                    MysteryWeight = 0f,
                },
                new BrickDuelCompositionStageDefinition
                {
                    GreenWeight = 0f,
                    RedWeight = 1f,
                    YellowWeight = 0f,
                    MysteryWeight = 0f,
                },
                new BrickDuelCompositionStageDefinition
                {
                    GreenWeight = 0f,
                    RedWeight = 1f,
                    YellowWeight = 0f,
                    MysteryWeight = 0f,
                },
                new BrickDuelCompositionStageDefinition
                {
                    GreenWeight = 0f,
                    RedWeight = 1f,
                    YellowWeight = 0f,
                    MysteryWeight = 0f,
                },
                new BrickDuelCompositionStageDefinition
                {
                    GreenWeight = 0f,
                    RedWeight = 1f,
                    YellowWeight = 0f,
                    MysteryWeight = 0f,
                },
                new BrickDuelCompositionStageDefinition
                {
                    GreenWeight = 0f,
                    RedWeight = 1f,
                    YellowWeight = 0f,
                    MysteryWeight = 0f,
                },
            };

            BrickDuelRuntime runtime = new BrickDuelRuntime(rule, CreateAiRule());
            runtime.BeginCountdown();
            Step(runtime, rule.CountdownSeconds * rule.SimulationFps);
            int beforeCount = runtime.Bricks.Count;

            Step(runtime, rule.SimulationFps);
            BrickDuelBrickState[] firstWave = runtime.Bricks.Skip(beforeCount).ToArray();
            Assert.Greater(firstWave.Length, 0);
            Assert.IsTrue(firstWave.All(brick => brick.InitialType == BrickDuelBrickType.Green));

            Step(runtime, 28 * rule.SimulationFps);
            Assert.AreEqual(0, rule.ResolveBrickCompositionStageIndex(
                runtime.ElapsedFrames / (float)rule.SimulationFps));
            beforeCount = runtime.Bricks.Count;
            Step(runtime, 2 * rule.SimulationFps);
            Assert.AreEqual(1, rule.ResolveBrickCompositionStageIndex(
                runtime.ElapsedFrames / (float)rule.SimulationFps));
            BrickDuelBrickState[] laterWave = runtime.Bricks.Skip(beforeCount).ToArray();
            Assert.Greater(laterWave.Length, 0);
            Assert.IsTrue(laterWave.All(brick => brick.InitialType == BrickDuelBrickType.Red));
        }

        [Test]
        public void MysteryBricks_ShareDeterministicItemIdsAcrossMirroredSides()
        {
            BrickDuelRuleDefinition rule = CreateRule();
            rule.BrickCompositionStages = CreateAllMysteryStages();
            BrickDuelRuntime runtime = new BrickDuelRuntime(rule, CreateAiRule());
            runtime.BeginCountdown();

            BrickDuelBrickState[] bottoms = runtime.Bricks
                .Where(item => item.Side == BrickDuelSide.Bottom)
                .OrderBy(item => item.LogicalRowId)
                .ThenBy(item => item.ColumnId)
                .ToArray();
            Assert.Greater(bottoms.Length, 0);
            Assert.IsTrue(bottoms.All(item => item.InitialType == BrickDuelBrickType.Mystery));
            Assert.IsTrue(bottoms.All(item => !string.IsNullOrEmpty(item.ItemId)));

            foreach (BrickDuelBrickState bottom in bottoms)
            {
                BrickDuelBrickState top = runtime.Bricks.Single(item =>
                    item.Side == BrickDuelSide.Top &&
                    item.LogicalRowId == bottom.LogicalRowId &&
                    item.ColumnId == bottom.ColumnId);
                Assert.AreEqual(bottom.ItemId, top.ItemId);
            }
        }

        [Test]
        public void DestroyingMysteryBrick_SpawnsOwnedItemCapsule()
        {
            BrickDuelRuleDefinition rule = CreateRule();
            rule.BrickCompositionStages = CreateAllMysteryStages();
            BrickDuelRuntime runtime = new BrickDuelRuntime(rule, CreateAiRule());
            runtime.BeginCountdown();

            BrickDuelBrickState target = runtime.Bricks
                .Where(item => item.Side == BrickDuelSide.Bottom)
                .OrderBy(item => item.LogicalRowId)
                .ThenBy(item => item.ColumnId)
                .First();
            string expectedItemId = target.ItemId;
            typeof(BrickDuelFrameEvents)
                .GetMethod("Clear", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(runtime.LastFrameEvents, null);
            ForceDestroyBrick(runtime, target);

            Assert.AreEqual(1, runtime.LastFrameEvents.MysteryDestroyedBrickIds.Count);
            Assert.AreEqual(1, runtime.Capsules.Count);
            BrickDuelItemCapsuleState capsule = runtime.Capsules[0];
            Assert.AreEqual(BrickDuelSide.Bottom, capsule.Side);
            Assert.AreEqual(expectedItemId, capsule.ItemId);
            Assert.AreEqual(target.Position.x, capsule.Position.x, 0.0001f);
        }

        [Test]
        public void CollectingWidePaddleCapsule_WidensPaddleWithoutStacking()
        {
            BrickDuelRuntime runtime = CreateRuntime();
            runtime.BeginCountdown();
            Step(runtime, runtime.Rule.CountdownSeconds * runtime.Rule.SimulationFps);

            SpawnCapsuleNearBottomPaddle(runtime, BrickDuelItemIds.WidePaddle);
            runtime.StepFrame(new BrickDuelFrameInput(0f));

            Assert.AreEqual(0, runtime.Capsules.Count);
            Assert.IsTrue(runtime.BottomEffects.HasWidePaddle);
            Assert.AreEqual(
                runtime.Rule.PaddleHalfWidth * BrickDuelItemConstants.WidePaddleWidthMultiplier,
                runtime.BottomPaddleHalfWidth,
                0.0001f);

            int remaining = runtime.BottomEffects.WidePaddleFramesRemaining;
            SpawnCapsuleNearBottomPaddle(runtime, BrickDuelItemIds.WidePaddle);
            runtime.StepFrame(new BrickDuelFrameInput(0f));
            Assert.Greater(runtime.BottomEffects.WidePaddleFramesRemaining, remaining - 2);
            Assert.AreEqual(
                runtime.Rule.PaddleHalfWidth * BrickDuelItemConstants.WidePaddleWidthMultiplier,
                runtime.BottomPaddleHalfWidth,
                0.0001f);
        }

        [Test]
        public void PhaseDrill_GrantsChargesAndCapsAtFive()
        {
            BrickDuelRuntime runtime = CreateRuntime();
            runtime.BeginCountdown();
            Step(runtime, runtime.Rule.CountdownSeconds * runtime.Rule.SimulationFps);

            SpawnCapsuleNearBottomPaddle(runtime, BrickDuelItemIds.PhaseDrill);
            runtime.StepFrame(new BrickDuelFrameInput(0f));
            Assert.AreEqual(3, runtime.BottomEffects.PhaseDrillCharges);
            Assert.IsTrue(runtime.BottomEffects.HasPhaseDrill);

            SpawnCapsuleNearBottomPaddle(runtime, BrickDuelItemIds.PhaseDrill);
            runtime.StepFrame(new BrickDuelFrameInput(0f));
            Assert.AreEqual(5, runtime.BottomEffects.PhaseDrillCharges);
        }

        [Test]
        public void DampingPulse_AppliesTideMultiplierOnlyToCollector()
        {
            BrickDuelRuntime runtime = CreateRuntime();
            runtime.BeginCountdown();
            Step(runtime, runtime.Rule.CountdownSeconds * runtime.Rule.SimulationFps);

            SpawnCapsuleNearBottomPaddle(runtime, BrickDuelItemIds.DampingPulse);
            runtime.StepFrame(new BrickDuelFrameInput(0f));

            Assert.IsTrue(runtime.BottomEffects.HasDamping);
            Assert.AreEqual(
                BrickDuelItemConstants.DampingTideMultiplier,
                runtime.BottomTideSpeedMultiplier,
                0.0001f);
            Assert.AreEqual(1f, runtime.TopTideSpeedMultiplier, 0.0001f);
        }

        [Test]
        public void CoreBuffer_AbsorbsFirstCoreHitOnly()
        {
            BrickDuelRuntime runtime = CreateRuntime();
            runtime.BeginCountdown();
            Step(runtime, runtime.Rule.CountdownSeconds * runtime.Rule.SimulationFps);
            SpawnCapsuleNearBottomPaddle(runtime, BrickDuelItemIds.CoreBuffer);
            runtime.StepFrame(new BrickDuelFrameInput(0f));
            Assert.IsTrue(runtime.BottomEffects.HasCoreBuffer);

            ForceCoreHit(runtime, BrickDuelSide.Bottom, columnId: 0);
            Assert.AreEqual(runtime.Rule.InitialCoreHealth, runtime.BottomCoreHealth);
            Assert.IsFalse(runtime.BottomEffects.HasCoreBuffer);
            Assert.AreEqual(1, runtime.LastFrameEvents.BottomCoreDamageAbsorbed);

            ForceCoreHit(runtime, BrickDuelSide.Bottom, columnId: 1);
            Assert.AreEqual(runtime.Rule.InitialCoreHealth - 1, runtime.BottomCoreHealth);
        }

        [Test]
        public void LargeBall_IncreasesRadiusWithoutChangingBallSpeed()
        {
            BrickDuelRuntime runtime = CreateRuntime();
            runtime.BeginCountdown();
            Step(runtime, runtime.Rule.CountdownSeconds * runtime.Rule.SimulationFps);
            float speedBefore = runtime.BottomBall.Velocity.magnitude;
            SpawnCapsuleNearBottomPaddle(runtime, BrickDuelItemIds.LargeBall);
            runtime.StepFrame(new BrickDuelFrameInput(0f));

            Assert.IsTrue(runtime.BottomEffects.HasLargeBall);
            Assert.AreEqual(
                runtime.Rule.BallRadius * BrickDuelItemConstants.LargeBallRadiusMultiplier,
                runtime.BottomBallRadius,
                0.0001f);
            Assert.AreEqual(speedBefore, runtime.BottomBall.Velocity.magnitude, 0.0001f);
        }

        [Test]
        public void CapsuleCap_ExpiresOldestWhenThirdSpawns()
        {
            BrickDuelRuntime runtime = CreateRuntime();
            runtime.BeginCountdown();
            Step(runtime, runtime.Rule.CountdownSeconds * runtime.Rule.SimulationFps);

            SpawnCapsuleAt(runtime, BrickDuelSide.Bottom, BrickDuelItemIds.WidePaddle, new Vector2(-1f, -1f));
            SpawnCapsuleAt(runtime, BrickDuelSide.Bottom, BrickDuelItemIds.LargeBall, new Vector2(0f, -1f));
            int firstId = runtime.Capsules.Min(item => item.CapsuleId);
            SpawnCapsuleAt(runtime, BrickDuelSide.Bottom, BrickDuelItemIds.CoreBuffer, new Vector2(1f, -1f));

            Assert.AreEqual(2, runtime.Capsules.Count);
            Assert.IsFalse(runtime.Capsules.Any(item => item.CapsuleId == firstId));
            Assert.AreEqual(1, runtime.LastFrameEvents.ExpiredCapsuleIds.Count);
        }

        private static BrickDuelCompositionStageDefinition[] CreateAllMysteryStages()
        {
            var stage = new BrickDuelCompositionStageDefinition
            {
                GreenWeight = 0f,
                RedWeight = 0f,
                YellowWeight = 0f,
                MysteryWeight = 1f,
            };
            return new[] { stage, stage, stage, stage, stage, stage };
        }

        private static void SpawnCapsuleNearBottomPaddle(BrickDuelRuntime runtime, string itemId)
        {
            SpawnCapsuleAt(
                runtime,
                BrickDuelSide.Bottom,
                itemId,
                runtime.BottomPaddle.Position + new Vector2(0f, 0.05f));
        }

        private static void SpawnCapsuleAt(
            BrickDuelRuntime runtime,
            BrickDuelSide side,
            string itemId,
            Vector2 position)
        {
            MethodInfo method = typeof(BrickDuelRuntime).GetMethod(
                "SpawnItemCapsule",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            var brick = new BrickDuelBrickState();
            typeof(BrickDuelBrickState)
                .GetProperty("Side")
                .SetValue(brick, side, null);
            typeof(BrickDuelBrickState)
                .GetProperty("ItemId")
                .SetValue(brick, itemId, null);
            typeof(BrickDuelBrickState)
                .GetProperty("Position")
                .SetValue(brick, position, null);
            method.Invoke(runtime, new object[] { brick });
        }

        private static void ForceDestroyBrick(BrickDuelRuntime runtime, BrickDuelBrickState brick)
        {
            MethodInfo applyHits = typeof(BrickDuelRuntime).GetMethod(
                "ApplyBrickHits",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(applyHits);
            applyHits.Invoke(runtime, new object[]
            {
                brick.Side,
                new HashSet<int> { brick.BrickId },
            });
        }

        private static void ForceCoreHit(BrickDuelRuntime runtime, BrickDuelSide side, int columnId)
        {
            float y = side == BrickDuelSide.Bottom
                ? -runtime.Rule.CoreLineY
                : runtime.Rule.CoreLineY;
            var brick = new BrickDuelBrickState();
            typeof(BrickDuelBrickState).GetProperty("BrickId").SetValue(brick, 9000 + columnId, null);
            typeof(BrickDuelBrickState).GetProperty("Side").SetValue(brick, side, null);
            typeof(BrickDuelBrickState).GetProperty("InitialType").SetValue(brick, BrickDuelBrickType.Green, null);
            typeof(BrickDuelBrickState).GetProperty("Health").SetValue(brick, 1, null);
            typeof(BrickDuelBrickState).GetProperty("Position").SetValue(brick, new Vector2(0f, y), null);
            typeof(BrickDuelBrickState).GetProperty("ColumnId").SetValue(brick, columnId, null);
            FieldInfo bricksField = typeof(BrickDuelRuntime).GetField(
                "_bricks",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var bricks = (List<BrickDuelBrickState>)bricksField.GetValue(runtime);
            bricks.Add(brick);
            MethodInfo resolve = typeof(BrickDuelRuntime).GetMethod(
                "ResolveCoreDamage",
                BindingFlags.Instance | BindingFlags.NonPublic);
            resolve.Invoke(runtime, null);
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
                ArenaHalfWidth = 3.0f,
                CoreLineY = 4.9f,
                PaddleSpawnY = 4.7f,
                PaddleHalfWidth = 0.375f,
                PaddleHalfHeight = 0.075f,
                PaddleMoveSpeed = 8f,
                BrickWidth = 0.66f,
                BrickHeight = 0.46f,
                BallRadius = 0.2f,
                BallSpeed = 3f,
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
                GreenWeight = 0.90f,
                RedWeight = 0.05f,
                YellowWeight = 0f,
                MysteryWeight = 0.05f,
                BrickCompositionIntervalSeconds = 30f,
                BrickCompositionStages = CreateDefaultCompositionStages(),
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

        private static BrickDuelCompositionStageDefinition[] CreateDefaultCompositionStages()
        {
            return new[]
            {
                new BrickDuelCompositionStageDefinition
                {
                    GreenWeight = 0.90f,
                    RedWeight = 0.05f,
                    YellowWeight = 0f,
                    MysteryWeight = 0.05f,
                },
                new BrickDuelCompositionStageDefinition
                {
                    GreenWeight = 0.75f,
                    RedWeight = 0.15f,
                    YellowWeight = 0.05f,
                    MysteryWeight = 0.05f,
                },
                new BrickDuelCompositionStageDefinition
                {
                    GreenWeight = 0.60f,
                    RedWeight = 0.25f,
                    YellowWeight = 0.10f,
                    MysteryWeight = 0.05f,
                },
                new BrickDuelCompositionStageDefinition
                {
                    GreenWeight = 0.45f,
                    RedWeight = 0.35f,
                    YellowWeight = 0.15f,
                    MysteryWeight = 0.05f,
                },
                new BrickDuelCompositionStageDefinition
                {
                    GreenWeight = 0.30f,
                    RedWeight = 0.45f,
                    YellowWeight = 0.20f,
                    MysteryWeight = 0.05f,
                },
                new BrickDuelCompositionStageDefinition
                {
                    GreenWeight = 0.20f,
                    RedWeight = 0.50f,
                    YellowWeight = 0.25f,
                    MysteryWeight = 0.05f,
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

        private static void StepBall(
            BrickDuelBallState ball,
            BrickDuelPaddleState paddle,
            Vector2 paddleStartPosition,
            Vector2 paddleVelocity,
            IList<BrickDuelBrickState> bricks,
            BrickDuelRuleDefinition rule,
            float deltaTime,
            float tideSpeed,
            ISet<int> hitBrickIds,
            float? paddleHalfWidth = null,
            float? ballRadius = null,
            int pierceCharges = 0)
        {
            var ignoredBrickIds = new HashSet<int>();
            new BrickDuelCollisionSolver().StepBall(
                ball,
                paddle,
                paddleStartPosition,
                paddleVelocity,
                bricks,
                rule,
                deltaTime,
                tideSpeed,
                paddleHalfWidth ?? rule.PaddleHalfWidth,
                ballRadius ?? rule.BallRadius,
                ref pierceCharges,
                ignoredBrickIds,
                hitBrickIds);
        }

        private static bool HasOverlayLine(
            IReadOnlyList<BrickDuelCollisionOverlayLine> lines,
            BrickDuelCollisionOverlayLineKind kind,
            Vector2 start,
            Vector2 end)
        {
            return lines.Any(line =>
                line.Kind == kind &&
                ((Vector2.Distance(line.Start, start) <= 0.0001f &&
                  Vector2.Distance(line.End, end) <= 0.0001f) ||
                 (Vector2.Distance(line.Start, end) <= 0.0001f &&
                  Vector2.Distance(line.End, start) <= 0.0001f)));
        }

        private static bool HasOverlayAabb(
            IReadOnlyList<BrickDuelCollisionOverlayLine> lines,
            BrickDuelCollisionOverlayLineKind kind,
            Vector2 center,
            Vector2 extents)
        {
            Vector2 minimum = center - extents;
            Vector2 maximum = center + extents;
            return HasOverlayLine(lines, kind, new Vector2(minimum.x, minimum.y), new Vector2(maximum.x, minimum.y)) &&
                   HasOverlayLine(lines, kind, new Vector2(maximum.x, minimum.y), new Vector2(maximum.x, maximum.y)) &&
                   HasOverlayLine(lines, kind, new Vector2(maximum.x, maximum.y), new Vector2(minimum.x, maximum.y)) &&
                   HasOverlayLine(lines, kind, new Vector2(minimum.x, maximum.y), new Vector2(minimum.x, minimum.y));
        }
    }
}

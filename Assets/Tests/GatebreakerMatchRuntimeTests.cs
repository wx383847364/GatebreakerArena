using App.HotUpdate.GatebreakerArena.Ball;
using App.HotUpdate.GatebreakerArena.Application;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Match;
using App.HotUpdate.GatebreakerArena.Mode;
using App.HotUpdate.GatebreakerArena.Paddle;
using App.HotUpdate.GatebreakerArena.Serve;
using App.HotUpdate.GatebreakerArena.Zone;
using NUnit.Framework;
using UnityEngine;

namespace Gatebreaker.Tests
{
    public sealed class GatebreakerMatchRuntimeTests
    {
        [Test]
        public void LocalPrototypeStartsWithConfiguredPlayersAndInitialBalls()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();

            runtime.StartLocalPrototype(aiCount: 3);

            Assert.AreEqual(MatchPhase.Playing, runtime.Phase);
            Assert.AreEqual(4, runtime.Players.Count);
            Assert.AreEqual(1, runtime.Balls.Count);
            Assert.AreEqual(0, runtime.FindPlayer(1).ServeResource.OwnedBallsInField);
            Assert.AreEqual(150f, runtime.RemainingTime);
        }

        [Test]
        public void ScoredBallUpdatesScoreAndRestoresOwnerBallCount()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            int initialBallId = runtime.Balls[0].BallId;

            runtime.ResolveGoalEntry(initialBallId, 2, Vector2.down);

            Assert.AreEqual(1, runtime.FindPlayer(1).Score);
            Assert.AreEqual(0, runtime.FindPlayer(1).ServeResource.OwnedBallsInField);
        }

        [Test]
        public void OwnedBallLimitBlocksConsecutiveServe()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            Assert.IsTrue(runtime.TryServe(1, out ServeBlockReason firstServeReason), firstServeReason.ToString());

            bool servedAgain = runtime.TryServe(1, out ServeBlockReason reason);

            Assert.IsFalse(servedAgain);
            Assert.AreEqual(ServeBlockReason.OwnedBallLimit, reason);
        }

        [Test]
        public void LocalPrototypeTickAppliesPlayerInputToPaddle()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            float initialAxis = runtime.FindPlayer(1).Paddle.AxisPosition;

            runtime.ApplyInputFrame(new PlayerInputFrame(1, 1f, false, Vector2.zero));
            runtime.TickLocalPrototype(0.25f);

            Assert.Greater(runtime.FindPlayer(1).Paddle.AxisPosition, initialAxis);
            Assert.Greater(runtime.FindPlayer(1).Paddle.Position.x, 0f);
        }

        [Test]
        public void FourSidePrototypePlacesSidePaddlesOnTheirOwnEdges()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 3);

            PlayerRuntimeState rightPlayer = runtime.FindPlayer(3);
            PlayerRuntimeState leftPlayer = runtime.FindPlayer(4);

            Assert.Less(rightPlayer.Paddle.Normal.x, 0f);
            Assert.Greater(rightPlayer.Paddle.Position.x, 0f);
            Assert.Greater(leftPlayer.Paddle.Normal.x, 0f);
            Assert.Less(leftPlayer.Paddle.Position.x, 0f);
        }

        [Test]
        public void SetLocalPlayerTransfersControlAwayFromAi()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 3);
            PlayerRuntimeState rightPlayer = runtime.FindPlayer(3);
            float initialAxis = rightPlayer.Paddle.AxisPosition;

            Assert.IsTrue(runtime.SetLocalPlayer(3));
            runtime.ApplyInputFrame(new PlayerInputFrame(3, 1f, false, Vector2.zero));
            runtime.TickLocalPrototype(0.25f);

            Assert.IsFalse(runtime.FindPlayer(3).IsAi);
            Assert.IsTrue(runtime.FindPlayer(1).IsAi);
            Assert.Greater(rightPlayer.Paddle.AxisPosition, initialAxis);
        }

        [Test]
        public void PaddleTangentVelocityUsesActualAxisMovement()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            PlayerRuntimeState player = runtime.FindPlayer(1);

            runtime.ApplyInputFrame(new PlayerInputFrame(1, 1f, false, Vector2.zero));
            runtime.TickLocalPrototype(0.25f);

            Assert.Greater(player.Paddle.TangentVelocity, 0f);
            Assert.LessOrEqual(player.Paddle.TangentVelocity, player.Paddle.Speed + 0.001f);
        }

        [Test]
        public void PaddleTangentVelocityUsesEachSideTangentDirection()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 3);

            for (int playerId = 1; playerId <= 4; playerId++)
            {
                Assert.IsTrue(runtime.SetLocalPlayer(playerId));
                PlayerRuntimeState player = runtime.FindPlayer(playerId);
                float initialAxis = player.Paddle.AxisPosition;

                runtime.ApplyInputFrame(new PlayerInputFrame(playerId, 1f, false, Vector2.zero));
                runtime.TickLocalPrototype(0.1f);

                Assert.Greater(player.Paddle.AxisPosition, initialAxis);
                Assert.Greater(player.Paddle.TangentVelocity, 0f);
            }
        }

        [Test]
        public void PaddleTangentVelocityIsZeroWhenDeltaTimeIsZero()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            PlayerRuntimeState player = runtime.FindPlayer(1);
            player.Paddle.TangentVelocity = 5f;

            runtime.ApplyInputFrame(new PlayerInputFrame(1, 1f, false, Vector2.zero));
            runtime.TickLocalPrototype(0f);

            Assert.AreEqual(0f, player.Paddle.TangentVelocity);
        }

        [Test]
        public void PaddleTangentVelocityDoesNotFakeMovementAtArenaEdge()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            PlayerRuntimeState player = runtime.FindPlayer(1);
            player.Paddle.AxisPosition = runtime.Arena.ClampPaddleAxis(player.Paddle.Normal, 999f);
            player.Paddle.Position = runtime.Arena.GetPaddleCenter(player.Paddle.Normal, player.Paddle.AxisPosition);

            runtime.ApplyInputFrame(new PlayerInputFrame(1, 1f, false, Vector2.zero));
            runtime.TickLocalPrototype(0.25f);

            Assert.AreEqual(0f, player.Paddle.TangentVelocity, 0.001f);
        }

        [Test]
        public void LocalPrototypeTickReboundsRightPlayerOwnedBallOnRightGoal()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 3);
            BallRuntimeState ball = runtime.Balls[0];
            ball.OwnerPlayerId = 3;
            ball.OwnerTeamId = 3;
            ball.Position = new Vector2(runtime.Arena.HalfWidth + 0.1f, 0f);
            ball.Velocity = Vector2.right;

            runtime.TickLocalPrototype(0f);

            Assert.AreEqual(0, runtime.FindPlayer(3).Score);
            Assert.AreEqual(BallState.GoalRebound, ball.BallState);
            Assert.Less(ball.Velocity.x, 0f);
        }

        [Test]
        public void LocalPrototypeTickScoresEnemyBallEnteringGoal()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            BallRuntimeState ball = runtime.Balls[0];
            ball.OwnerPlayerId = 1;
            ball.OwnerTeamId = 1;
            ball.Position = new Vector2(0f, runtime.Arena.HalfHeight + 0.1f);
            ball.Velocity = Vector2.up;

            runtime.TickLocalPrototype(0f);

            Assert.AreEqual(1, runtime.FindPlayer(1).Score);
            Assert.AreEqual(0, runtime.Balls.Count);
        }

        [Test]
        public void LocalPrototypeTickReboundsOwnedBallEnteringOwnGoal()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            BallRuntimeState ball = runtime.Balls[0];
            ball.OwnerPlayerId = 1;
            ball.OwnerTeamId = 1;
            ball.Position = new Vector2(0f, -runtime.Arena.HalfHeight - 0.1f);
            ball.Velocity = Vector2.down;

            runtime.TickLocalPrototype(0f);

            Assert.AreEqual(0, runtime.FindPlayer(1).Score);
            Assert.AreEqual(BallState.GoalRebound, ball.BallState);
            Assert.Greater(ball.Velocity.y, 0f);
        }

        [Test]
        public void BottomPaddleCenterHitReflectsBallUpward()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            PlayerRuntimeState player = runtime.FindPlayer(1);
            BallRuntimeState ball = runtime.Balls[0];
            ball.Position = player.Paddle.Position + player.Paddle.Normal * 0.1f;
            ball.Velocity = Vector2.down * 7.5f;

            runtime.TickLocalPrototype(0f);

            Assert.Greater(ball.Velocity.y, 0f);
            Assert.Less(Mathf.Abs(ball.Velocity.x), 0.05f);
        }

        [Test]
        public void BottomPaddleEdgeHitKeepsOutwardBounceDominant()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            PlayerRuntimeState player = runtime.FindPlayer(1);
            BallRuntimeState ball = runtime.Balls[0];
            ball.Position = player.Paddle.Position
                            + player.Paddle.Normal * 0.1f
                            - player.Paddle.Tangent * (player.Paddle.Length * 0.45f);
            ball.Velocity = Vector2.down * 7.5f;

            runtime.TickLocalPrototype(0f);

            Assert.Greater(ball.Velocity.y, 0f);
            Assert.Greater(ball.Velocity.y, Mathf.Abs(ball.Velocity.x));
        }

        [Test]
        public void BottomPaddleEdgeHitKeepsTangentContactPosition()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            PlayerRuntimeState player = runtime.FindPlayer(1);
            float expectedTangentDistance = -player.Paddle.Length * 0.45f;
            BallRuntimeState ball = runtime.Balls[0];
            ball.Position = player.Paddle.Position
                            + player.Paddle.Normal * 0.1f
                            + player.Paddle.Tangent * expectedTangentDistance;
            ball.Velocity = Vector2.down * 7.5f;

            runtime.TickLocalPrototype(0f);

            float actualTangentDistance = Vector2.Dot(ball.Position - player.Paddle.Position, player.Paddle.Tangent);
            Assert.AreEqual(expectedTangentDistance, actualTangentDistance, 0.001f);
        }

        [Test]
        public void LargeDeltaBallCrossingPaddleStillReboundsBeforeGoal()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            PlayerRuntimeState player = runtime.FindPlayer(1);
            BallRuntimeState ball = runtime.Balls[0];
            ball.OwnerPlayerId = 2;
            ball.OwnerTeamId = 2;
            ball.Position = player.Paddle.Position + player.Paddle.Normal * (player.Paddle.Thickness + 0.5f);
            ball.Velocity = Vector2.down * 7.5f;

            runtime.Tick(0.3f);

            Assert.AreEqual(0, runtime.FindPlayer(2).Score);
            Assert.AreEqual(BallState.Flying, ball.BallState);
            Assert.Greater(ball.Velocity.y, 0f);
        }

        [Test]
        public void MovingPaddleLargeDeltaUsesIntermediatePaddlePosition()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            PlayerRuntimeState player = runtime.FindPlayer(1);
            BallRuntimeState ball = runtime.Balls[0];
            float contactY = player.Paddle.Position.y + player.Paddle.Thickness;
            ball.OwnerPlayerId = 2;
            ball.OwnerTeamId = 2;
            ball.Position = new Vector2(0.8f, contactY + 0.6f);
            ball.Velocity = Vector2.down * 6f;

            runtime.ApplyInputFrame(new PlayerInputFrame(1, 1f, false, Vector2.zero));
            runtime.Tick(0.25f);

            Assert.AreEqual(0, runtime.FindPlayer(2).Score);
            Assert.AreEqual(BallState.Flying, ball.BallState);
            Assert.Greater(ball.Velocity.y, 0f);
        }

        [Test]
        public void MovingPaddleLargeDeltaDoesNotCreateFinalPositionGhostHit()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            PlayerRuntimeState player = runtime.FindPlayer(1);
            BallRuntimeState ball = runtime.Balls[0];
            float contactY = player.Paddle.Position.y + player.Paddle.Thickness;
            ball.OwnerPlayerId = 2;
            ball.OwnerTeamId = 2;
            ball.Position = new Vector2(2.4f, contactY + 0.6f);
            ball.Velocity = Vector2.down * 6f;

            runtime.ApplyInputFrame(new PlayerInputFrame(1, 1f, false, Vector2.zero));
            runtime.Tick(0.25f);

            Assert.AreEqual(1, runtime.FindPlayer(2).Score);
            Assert.AreEqual(0, runtime.Balls.Count);
        }

        [Test]
        public void ExtremeSpeedCrossingPaddleStillUsesSweptHit()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            PlayerRuntimeState player = runtime.FindPlayer(1);
            BallRuntimeState ball = runtime.Balls[0];
            ball.OwnerPlayerId = 2;
            ball.OwnerTeamId = 2;
            ball.Position = player.Paddle.Position + player.Paddle.Normal * (player.Paddle.Thickness + 0.6f);
            ball.Velocity = -player.Paddle.Normal * 200f;

            runtime.Tick(0.05f);

            Assert.AreEqual(0, runtime.FindPlayer(2).Score);
            Assert.AreEqual(BallState.Flying, ball.BallState);
            Assert.Greater(Vector2.Dot(ball.Velocity, player.Paddle.Normal), 0f);
        }

        [Test]
        public void SweptPaddleHitWorksForAllFourSides()
        {
            for (int playerId = 1; playerId <= 4; playerId++)
            {
                GatebreakerMatchRuntime runtime = CreateRuntime();
                runtime.StartLocalPrototype(aiCount: 3);
                PlayerRuntimeState player = runtime.FindPlayer(playerId);
                BallRuntimeState ball = runtime.Balls[0];
                int enemyPlayerId = playerId == 1 ? 2 : 1;
                ball.OwnerPlayerId = enemyPlayerId;
                ball.OwnerTeamId = enemyPlayerId;
                ball.Position = player.Paddle.Position + player.Paddle.Normal * (player.Paddle.Thickness + 0.8f);
                ball.Velocity = -player.Paddle.Normal * 50f;

                runtime.Tick(0.2f);

                Assert.AreEqual(0, runtime.FindPlayer(enemyPlayerId).Score, $"playerId={playerId}");
                Assert.AreEqual(BallState.Flying, ball.BallState, $"playerId={playerId}");
                Assert.Greater(Vector2.Dot(ball.Velocity, player.Paddle.Normal), 0f, $"playerId={playerId}");
            }
        }

        [Test]
        public void BounceTuningHitOffsetInfluenceIncreasesEdgeBounceAngle()
        {
            var calculator = new PaddleBounceCalculator();
            BallRuleDefinition rule = GatebreakerModeCatalog.CreateDefault().GetBall("BALL_NORMAL");
            var lowTuning = PaddleBounceTuning.CreateDefault();
            lowTuning.SetHitOffsetInfluenceValue(0);
            var highTuning = PaddleBounceTuning.CreateDefault();
            highTuning.SetHitOffsetInfluenceValue(120);

            Vector2 low = calculator.CalculateBounce(Vector2.down * 7.5f, 0.8f, rule, Vector2.up, Vector2.right, lowTuning, 0f);
            Vector2 high = calculator.CalculateBounce(Vector2.down * 7.5f, 0.8f, rule, Vector2.up, Vector2.right, highTuning, 0f);

            Assert.Greater(Mathf.Abs(high.x), Mathf.Abs(low.x));
            Assert.Greater(high.y, 0f);
        }

        [Test]
        public void BounceTuningPaddleVelocityInfluenceChangesCenterBounceAngle()
        {
            var calculator = new PaddleBounceCalculator();
            BallRuleDefinition rule = GatebreakerModeCatalog.CreateDefault().GetBall("BALL_NORMAL");
            var tuning = PaddleBounceTuning.CreateDefault();

            Vector2 still = calculator.CalculateBounce(Vector2.down * 7.5f, 0f, rule, Vector2.up, Vector2.right, tuning, 0f);
            Vector2 moving = calculator.CalculateBounce(Vector2.down * 7.5f, 0f, rule, Vector2.up, Vector2.right, tuning, 1f);

            Assert.Greater(moving.x, still.x);
            Assert.Greater(moving.y, 0f);
        }

        [Test]
        public void BounceTuningStoresPanelValuesAndActualValues()
        {
            PaddleBounceTuning tuning = PaddleBounceTuning.CreateDefault();

            tuning.SetHitOffsetInfluenceValue(200);
            tuning.SetPaddleVelocityInfluenceValue(-5);
            tuning.SetMinimumOutwardShareValue(31);

            Assert.AreEqual(PaddleBounceTuning.HitOffsetInfluenceMax, tuning.HitOffsetInfluenceValue);
            Assert.AreEqual(PaddleBounceTuning.PaddleVelocityInfluenceMin, tuning.PaddleVelocityInfluenceValue);
            Assert.AreEqual(31, tuning.MinimumOutwardShareValue);
            Assert.AreEqual(0.31f, tuning.MinimumOutwardShare, 0.001f);
        }

        [Test]
        public void LocalPrototypeTickLetsAiServeWhenReady()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);

            runtime.TickLocalPrototype(0.02f);

            Assert.GreaterOrEqual(runtime.Balls.Count, 2);
            Assert.AreEqual(1, runtime.FindPlayer(2).ServeResource.OwnedBallsInField);
        }

        [Test]
        public void UniqueLeaderWinsWhenRegularTimeExpires()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            runtime.FindPlayer(1).Score = 2;
            runtime.FindPlayer(2).Score = 1;

            runtime.Tick(200f);

            Assert.AreEqual(MatchPhase.Result, runtime.Phase);
            Assert.IsTrue(runtime.HasWinner);
            Assert.AreEqual(1, runtime.WinnerPlayerId);
        }

        [Test]
        public void TiedHighestScoreEntersSuddenDeathOvertime()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            runtime.FindPlayer(1).Score = 2;
            runtime.FindPlayer(2).Score = 2;

            runtime.Tick(200f);
            ScoreboardSnapshot snapshot = runtime.CreateScoreboardSnapshot();

            Assert.AreEqual(MatchPhase.Overtime, runtime.Phase);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, snapshot.OvertimeEligiblePlayerIds);
            Assert.AreEqual(60f, runtime.RemainingTime);
        }

        [Test]
        public void OvertimeEligiblePlayerWinsOnNextScore()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            runtime.FindPlayer(1).Score = 2;
            runtime.FindPlayer(2).Score = 2;
            runtime.Tick(200f);
            int initialBallId = runtime.Balls[0].BallId;

            runtime.ResolveGoalEntry(initialBallId, 2, Vector2.down);

            Assert.AreEqual(MatchPhase.Result, runtime.Phase);
            Assert.AreEqual(1, runtime.WinnerPlayerId);
        }

        private static GatebreakerMatchRuntime CreateRuntime()
        {
            return new GatebreakerMatchRuntime(
                GatebreakerModeCatalog.CreateDefault(),
                new BallSimulationSystem(),
                new ServeResourceSystem(),
                new GoalJudgeSystem(),
                new ScoreSystem(),
                null);
        }
    }
}

using App.HotUpdate.GatebreakerArena.Ball;
using App.HotUpdate.GatebreakerArena.Application;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Match;
using App.HotUpdate.GatebreakerArena.Mode;
using App.HotUpdate.GatebreakerArena.Paddle;
using App.HotUpdate.GatebreakerArena.Prototype;
using App.HotUpdate.GatebreakerArena.Serve;
using App.HotUpdate.GatebreakerArena.Zone;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            Assert.AreEqual(60f, runtime.RemainingTime);
        }

        [Test]
        public void ScoredBallUpdatesScoreAndRestoresOwnerBallCount()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            int initialBallId = runtime.Balls[0].BallId;

            runtime.ResolveGoalEntry(initialBallId, 2, Vector2.down);

            Assert.AreEqual(1, runtime.FindPlayer(1).Score);
            Assert.AreEqual(0, runtime.FindPlayer(1).HitScore);
            Assert.AreEqual(1, runtime.FindPlayer(1).TrueScore);
            Assert.AreEqual(-1, runtime.FindPlayer(2).HitScore);
            Assert.AreEqual(-1, runtime.FindPlayer(2).TrueScore);
            Assert.AreEqual(1, runtime.FindPlayer(1).ScoreReachOrder);
            Assert.AreEqual(0, runtime.FindPlayer(1).ServeResource.OwnedBallsInField);
        }

        [Test]
        public void TimeExpiredUsesTrueScoreAsScoreTieBreaker()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            runtime.FindPlayer(1).Score = 2;
            runtime.FindPlayer(1).HitScore = -2;
            runtime.FindPlayer(1).ScoreReachOrder = 1;
            runtime.FindPlayer(2).Score = 2;
            runtime.FindPlayer(2).HitScore = -1;
            runtime.FindPlayer(2).ScoreReachOrder = 2;

            runtime.Tick(200f);

            Assert.AreEqual(MatchPhase.Result, runtime.Phase);
            Assert.AreEqual(2, runtime.WinnerPlayerId);
        }

        [Test]
        public void TimeExpiredUsesEarlierScoreReachOrderWhenScoreAndTrueScoreTie()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            runtime.FindPlayer(1).Score = 2;
            runtime.FindPlayer(1).HitScore = -1;
            runtime.FindPlayer(1).ScoreReachOrder = 2;
            runtime.FindPlayer(2).Score = 2;
            runtime.FindPlayer(2).HitScore = -1;
            runtime.FindPlayer(2).ScoreReachOrder = 1;

            runtime.Tick(200f);

            Assert.AreEqual(MatchPhase.Result, runtime.Phase);
            Assert.AreEqual(2, runtime.WinnerPlayerId);
        }

        [Test]
        public void TimeExpiredKeepsSuddenDeathWhenRankingCannotBreakTie()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);

            runtime.Tick(200f);

            Assert.AreEqual(MatchPhase.Overtime, runtime.Phase);
            CollectionAssert.AreEquivalent(
                new[] { 1, 2 },
                runtime.CreateScoreboardSnapshot().OvertimeEligiblePlayerIds);
        }

        [Test]
        public void ChecksumIncludesOvertimeEligiblePlayers()
        {
            GatebreakerMatchRuntime left = CreateRuntime();
            GatebreakerMatchRuntime right = CreateRuntime();
            left.StartLocalPrototype(aiCount: 1);
            right.StartLocalPrototype(aiCount: 1);
            left.Tick(200f);
            right.Tick(200f);

            GetOvertimeEligiblePlayerIds(right).Remove(2);

            Assert.AreNotEqual(left.CreateChecksum(0), right.CreateChecksum(0));
        }

        [Test]
        public void OwnedBallLimitBlocksServeWhenOwnedFieldIsFull()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            PlayerRuntimeState player = runtime.FindPlayer(1);
            player.ServeResource.CurrentServeAmmo = 1;
            player.ServeResource.OwnedBallsInField = player.ServeResource.MaxOwnedBallsInField;

            bool served = runtime.TryServe(1, out ServeBlockReason reason);

            Assert.IsFalse(served);
            Assert.AreEqual(ServeBlockReason.OwnedBallLimit, reason);
        }

        [Test]
        public void ServeUsesPureNormalWhenAimDirectionIsMissing()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartMatch(new GatebreakerMatchStartConfig
            {
                MatchId = "serve-direction-test",
                ModeId = "PVE_STANDARD",
                MapId = "MAP_ARENA_01",
                BallTypeId = "BALL_NORMAL",
                ActiveSlots = new[] { 1, 2 },
                LocalPlayerId = 1,
                SimulationFps = GatebreakerMatchStartConfig.DefaultSimulationFps,
                InputDelayFrames = 0,
            });
            PlayerRuntimeState player = runtime.FindPlayer(1);
            Vector2 normal = player.Paddle.Normal.normalized;

            runtime.StepFrame(0, new[] { new GatebreakerFrameInput(1, 0f, true, Vector2.zero) });

            BallRuntimeState servedBall = runtime.Balls.OrderBy(ball => ball.BallId).Last();
            Assert.Less(Vector2.Distance(normal, servedBall.Velocity.normalized), 0.001f);
        }

        [Test]
        public void ServeUsesAimDirectionWhenProvided()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartMatch(new GatebreakerMatchStartConfig
            {
                MatchId = "serve-aim-test",
                ModeId = "PVE_STANDARD",
                MapId = "MAP_ARENA_01",
                BallTypeId = "BALL_NORMAL",
                ActiveSlots = new[] { 1, 2 },
                LocalPlayerId = 1,
                SimulationFps = GatebreakerMatchStartConfig.DefaultSimulationFps,
                InputDelayFrames = 0,
            });
            PlayerRuntimeState player = runtime.FindPlayer(1);
            Vector2 aimDirection = (player.Paddle.Normal + player.Paddle.Tangent).normalized;

            runtime.StepFrame(0, new[] { new GatebreakerFrameInput(1, 0f, true, aimDirection) });

            BallRuntimeState servedBall = runtime.Balls.OrderBy(ball => ball.BallId).Last();
            Assert.Less(Vector2.Distance(aimDirection, servedBall.Velocity.normalized), 0.001f);
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
        public void Scene3v3MapUsesPrefabDerivedBoundary()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();

            runtime.StartLocalPrototype(aiCount: 3);

            Assert.IsTrue(runtime.Arena.HasCustomBoundary);
            Assert.AreEqual(6, runtime.Arena.BoundarySegments.Count);
            Assert.IsTrue(runtime.Arena.Contains(Vector2.zero));
            Assert.IsFalse(runtime.Arena.Contains(new Vector2(3f, 0f)));
        }

        [Test]
        public void Scene3v3PaddlesAlignWithActiveNetSegments()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();

            runtime.StartLocalPrototype(aiCount: 3);

            AssertPaddleAlignedWithSegment(runtime.FindPlayer(1).Paddle, runtime.Arena.BoundarySegments[5], runtime.Arena.PaddleInset);
            Assert.AreEqual(0.05f, runtime.FindPlayer(1).Paddle.Thickness, 0.001f);
            Assert.IsTrue(runtime.FindPlayer(2).IsDisabled);
            Assert.IsNull(runtime.FindPlayer(2).Paddle);
            AssertPaddleAlignedWithSegment(runtime.FindPlayer(3).Paddle, runtime.Arena.BoundarySegments[1], runtime.Arena.PaddleInset);
            AssertPaddleAlignedWithSegment(runtime.FindPlayer(4).Paddle, runtime.Arena.BoundarySegments[3], runtime.Arena.PaddleInset);
        }

        [Test]
        public void Scene3v3BoundaryKeepsExistingGoalOwnership()
        {
            ArenaGeometry arena = ArenaGeometry.CreateScene3v3();
            ArenaBoundarySegment bottomGoal = arena.BoundarySegments[5];
            ArenaBoundarySegment rightGoal = arena.BoundarySegments[1];
            ArenaBoundarySegment leftGoal = arena.BoundarySegments[3];

            Assert.IsTrue(arena.TryGetGoalOwner(bottomGoal.GoalCenter - bottomGoal.InwardNormal * 0.2f, 4, SpawnLayoutType.FourSide, out int bottomOwner));
            Assert.AreEqual(0, bottomOwner);
            Assert.IsTrue(arena.TryGetGoalOwner(rightGoal.GoalCenter - rightGoal.InwardNormal * 0.2f, 4, SpawnLayoutType.FourSide, out int rightOwner));
            Assert.AreEqual(2, rightOwner);
            Assert.IsTrue(arena.TryGetGoalOwner(leftGoal.GoalCenter - leftGoal.InwardNormal * 0.2f, 4, SpawnLayoutType.FourSide, out int leftOwner));
            Assert.AreEqual(3, leftOwner);
        }

        [Test]
        public void Scene3v3InactiveNetPositionsAreWalls()
        {
            ArenaGeometry arena = ArenaGeometry.CreateScene3v3();
            ArenaBoundarySegment position02NotNet = arena.BoundarySegments[0];
            ArenaBoundarySegment position04NotNet = arena.BoundarySegments[2];
            ArenaBoundarySegment position06NotNet = arena.BoundarySegments[4];

            Assert.IsFalse(arena.TryGetGoalOwner(position02NotNet.GoalCenter - position02NotNet.InwardNormal * 0.2f, 4, SpawnLayoutType.FourSide, out _));
            Assert.IsFalse(arena.TryGetGoalOwner(position04NotNet.GoalCenter - position04NotNet.InwardNormal * 0.2f, 4, SpawnLayoutType.FourSide, out _));
            Assert.IsFalse(arena.TryGetGoalOwner(position06NotNet.GoalCenter - position06NotNet.InwardNormal * 0.2f, 4, SpawnLayoutType.FourSide, out _));
        }

        [Test]
        public void Scene3v3BoundaryDoesNotScoreOutsideGoalSpan()
        {
            ArenaGeometry arena = ArenaGeometry.CreateScene3v3();
            ArenaBoundarySegment rightGoal = arena.BoundarySegments[1];
            Vector2 nearCornerOutside = Vector2.Lerp(rightGoal.Start, rightGoal.End, 0.05f) - rightGoal.InwardNormal * 0.2f;

            Assert.IsFalse(arena.TryGetGoalOwner(nearCornerOutside, 4, SpawnLayoutType.FourSide, out _));
        }

        [Test]
        public void Scene3v3BottomNetScoresInsideTriggerBand()
        {
            ArenaGeometry arena = ArenaGeometry.CreateScene3v3();
            ArenaBoundarySegment bottomGoal = arena.BoundarySegments[5];
            Vector2 insideNetBand = bottomGoal.GoalCenter + bottomGoal.InwardNormal * (bottomGoal.GoalTriggerInset * 0.5f);

            Assert.IsTrue(arena.TryGetGoalOwner(insideNetBand, 4, SpawnLayoutType.FourSide, out int owner));
            Assert.AreEqual(0, owner);
        }

        [Test]
        public void Scene3v3DebugOverlaySeparatesWallsAndActiveGoalBands()
        {
            ArenaGeometry arena = ArenaGeometry.CreateScene3v3();
            var lines = GatebreakerCollisionOverlayGeometry.BuildLines(arena, 4);

            Assert.AreEqual(9, lines.Count(line => line.Kind == GatebreakerCollisionOverlayLineKind.Wall));
            Assert.AreEqual(3, lines.Count(line => line.Kind == GatebreakerCollisionOverlayLineKind.GoalTrigger));
            Assert.AreEqual(9, lines.Count(line => line.Kind == GatebreakerCollisionOverlayLineKind.GoalBand));
            Assert.AreEqual(3, lines.Count(line => line.Kind == GatebreakerCollisionOverlayLineKind.PaddleContact));
            Assert.IsTrue(lines.Any(line =>
                line.Kind == GatebreakerCollisionOverlayLineKind.GoalTrigger &&
                line.GoalPlayerIndex == arena.BoundarySegments[5].GoalPlayerIndex));
            Assert.IsTrue(lines.Any(line =>
                line.Kind == GatebreakerCollisionOverlayLineKind.PaddleContact &&
                line.GoalPlayerIndex == arena.BoundarySegments[5].GoalPlayerIndex));
        }

        [Test]
        public void SetArenaPaddleLengthRefreshesLogicPaddlesZonesAndDebugContact()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 3);
            float prefabMeasuredLength = 1.5f;
            float calibratedLength = runtime.Arena.PaddleLength * prefabMeasuredLength;

            Assert.IsTrue(runtime.SetArenaPaddleLength(calibratedLength));

            Assert.AreEqual(calibratedLength, runtime.Arena.PaddleLength, 0.001f);
            foreach (PlayerRuntimeState player in runtime.Players.Where(player => !player.IsDisabled))
            {
                Assert.AreEqual(calibratedLength, player.Paddle.Length, 0.001f);
                Assert.AreEqual(calibratedLength * 0.5f, player.Zone.HalfLength, 0.001f);
                Assert.LessOrEqual(
                    Mathf.Abs(player.Paddle.AxisPosition),
                    runtime.Arena.BoundarySegments.Max(segment => segment.GoalHalfLength));
            }

            GatebreakerCollisionOverlayLine bottomContact = GatebreakerCollisionOverlayGeometry
                .BuildLines(runtime.Arena, runtime.Players.Count)
                .First(line =>
                    line.Kind == GatebreakerCollisionOverlayLineKind.PaddleContact &&
                    line.GoalPlayerIndex == runtime.Arena.BoundarySegments[5].GoalPlayerIndex);
            Assert.AreEqual(calibratedLength, Vector2.Distance(bottomContact.Start, bottomContact.End), 0.001f);
        }

        [Test]
        public void SweptCollisionUsesScene3v3BoundarySegments()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 3);
            BallRuntimeState ball = runtime.Balls[0];
            ball.OwnerPlayerId = 1;
            ball.OwnerTeamId = 1;
            ball.Position = new Vector2(2.0f, 1.0f);
            ball.Velocity = Vector2.right * 7.5f;

            runtime.TickLocalPrototype(0.2f);

            Assert.AreEqual(1, runtime.FindPlayer(1).Score);
            Assert.AreEqual(0, runtime.Balls.Count);
        }

        [Test]
        public void SweptCollisionScoresWhenCrossingBottomNetTrigger()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 3);
            ArenaBoundarySegment bottomGoal = runtime.Arena.BoundarySegments[5];
            BallRuntimeState ball = runtime.Balls[0];
            ball.OwnerPlayerId = 3;
            ball.OwnerTeamId = 3;
            ball.Position = bottomGoal.GoalCenter + bottomGoal.InwardNormal * (bottomGoal.GoalTriggerInset + 0.4f);
            ball.Velocity = -bottomGoal.InwardNormal * 7.5f;

            runtime.TickLocalPrototype(0.2f);

            Assert.AreEqual(1, runtime.FindPlayer(3).Score);
            Assert.AreEqual(0, runtime.Balls.Count);
        }

        [Test]
        public void SweptCollisionBouncesScene3v3BoundaryOutsideGoalSpan()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 3);
            ArenaBoundarySegment rightGoal = runtime.Arena.BoundarySegments[1];
            Vector2 targetHit = Vector2.Lerp(rightGoal.Start, rightGoal.End, 0.05f);
            BallRuntimeState ball = runtime.Balls[0];
            ball.OwnerPlayerId = 1;
            ball.OwnerTeamId = 1;
            ball.Position = targetHit + rightGoal.InwardNormal * 0.5f;
            ball.Velocity = -rightGoal.InwardNormal * 7.5f;

            runtime.TickLocalPrototype(0.2f);

            Assert.AreEqual(0, runtime.FindPlayer(1).Score);
            Assert.AreEqual(1, runtime.Balls.Count);
            Assert.Greater(Vector2.Dot(ball.Velocity, rightGoal.InwardNormal), 0f);
        }

        [Test]
        public void SweptCollisionBouncesScene3v3InactiveNetSegment()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 3);
            ArenaBoundarySegment position04NotNet = runtime.Arena.BoundarySegments[2];
            BallRuntimeState ball = runtime.Balls[0];
            ball.OwnerPlayerId = 1;
            ball.OwnerTeamId = 1;
            ball.Position = position04NotNet.GoalCenter + position04NotNet.InwardNormal * 0.5f;
            ball.Velocity = -position04NotNet.InwardNormal * 7.5f;

            runtime.TickLocalPrototype(0.2f);

            Assert.AreEqual(0, runtime.FindPlayer(1).Score);
            Assert.AreEqual(1, runtime.Balls.Count);
            Assert.Greater(Vector2.Dot(ball.Velocity, position04NotNet.InwardNormal), 0f);
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
                PlayerRuntimeState player = runtime.FindPlayer(playerId);
                if (player.IsDisabled || player.Paddle == null)
                {
                    continue;
                }

                Assert.IsTrue(runtime.SetLocalPlayer(playerId));
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
            Vector2 entryPosition = new Vector2(runtime.Arena.HalfWidth + 0.1f, 0f);
            ball.Position = entryPosition;
            ball.Velocity = Vector2.right;

            runtime.TickLocalPrototype(0f);

            Assert.AreEqual(0, runtime.FindPlayer(3).Score);
            Assert.AreEqual(BallState.GoalRebound, ball.BallState);
            Assert.AreEqual(entryPosition, ball.Position);
            Assert.Less(ball.Velocity.x, 0f);
        }

        [Test]
        public void LocalPrototypeTickScoresEnemyBallEnteringGoal()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 3);
            ArenaBoundarySegment rightGoal = runtime.Arena.BoundarySegments[1];
            BallRuntimeState ball = runtime.Balls[0];
            ball.OwnerPlayerId = 1;
            ball.OwnerTeamId = 1;
            ball.Position = rightGoal.GoalCenter - rightGoal.InwardNormal * 0.2f;
            ball.Velocity = -rightGoal.InwardNormal;

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
            Vector2 entryPosition = new Vector2(0f, -runtime.Arena.HalfHeight - 0.1f);
            ball.Position = entryPosition;
            ball.Velocity = Vector2.down;

            runtime.TickLocalPrototype(0f);

            Assert.AreEqual(0, runtime.FindPlayer(1).Score);
            Assert.AreEqual(BallState.GoalRebound, ball.BallState);
            Assert.AreEqual(entryPosition, ball.Position);
            Assert.Greater(ball.Velocity.y, 0f);
        }

        [Test]
        public void Scene3v3OwnedGoalReboundReturnsAlongEntryPathWithoutPaddleEdgeClamp()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 3);
            PlayerRuntimeState owner = runtime.FindPlayer(1);
            ArenaBoundarySegment ownerGoal = runtime.Arena.BoundarySegments[5];
            BallRuntimeState ball = runtime.Balls[0];
            Vector2 entryPosition = ownerGoal.GoalCenter +
                                    ownerGoal.Tangent * 0.9f -
                                    ownerGoal.InwardNormal * 0.2f;
            Vector2 entryVelocity = (-ownerGoal.InwardNormal + ownerGoal.Tangent * 0.25f).normalized *
                                    runtime.BallRule.InitialSpeed;
            ball.OwnerPlayerId = owner.PlayerId;
            ball.OwnerTeamId = owner.TeamId;
            ball.Position = entryPosition;
            ball.Velocity = entryVelocity;

            runtime.TickLocalPrototype(0f);

            Assert.AreEqual(BallState.GoalRebound, ball.BallState);
            Assert.AreEqual(entryPosition, ball.Position);
            Assert.Greater(
                Mathf.Abs(Vector2.Dot(entryPosition - owner.Paddle.Position, owner.Paddle.Tangent)),
                owner.Paddle.Length * 0.5f);
            Assert.Greater(Vector2.Dot(ball.Velocity.normalized, -entryVelocity.normalized), 0.999f);
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
        public void BottomPaddleEmbeddedStartStillReboundsBeforeGoal()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            PlayerRuntimeState player = runtime.FindPlayer(1);
            BallRuntimeState ball = runtime.Balls[0];
            ball.OwnerPlayerId = 2;
            ball.OwnerTeamId = 2;
            ball.Position = player.Paddle.Position + player.Paddle.Normal * (player.Paddle.Thickness * 0.5f);
            ball.Velocity = -player.Paddle.Normal * runtime.BallRule.InitialSpeed;

            runtime.Tick(0.2f);

            Assert.AreEqual(0, runtime.FindPlayer(2).Score);
            Assert.AreEqual(BallState.Flying, ball.BallState);
            Assert.Greater(Vector2.Dot(ball.Velocity, player.Paddle.Normal), 0f);
            Assert.Greater(Vector2.Dot(ball.Position - player.Paddle.Position, player.Paddle.Normal), player.Paddle.Thickness);
        }

        [Test]
        public void PaddleHitKeepsOriginalBallOwnership()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 3);
            PlayerRuntimeState player = runtime.FindPlayer(3);
            PlayerRuntimeState owner = runtime.FindPlayer(1);
            BallRuntimeState ball = runtime.Balls[0];
            ball.OwnerPlayerId = 1;
            ball.OwnerTeamId = 1;
            owner.ServeResource.OwnedBallsInField = 1;
            player.ServeResource.OwnedBallsInField = 0;
            ball.Position = player.Paddle.Position + player.Paddle.Normal * (player.Paddle.Thickness + 0.6f);
            ball.Velocity = -player.Paddle.Normal * runtime.BallRule.InitialSpeed;

            runtime.Tick(0.2f);

            Assert.AreEqual(owner.PlayerId, ball.OwnerPlayerId);
            Assert.AreEqual(owner.TeamId, ball.OwnerTeamId);
            Assert.AreEqual(1, owner.ServeResource.OwnedBallsInField);
            Assert.AreEqual(0, player.ServeResource.OwnedBallsInField);
            Assert.Greater(Vector2.Dot(ball.Velocity, player.Paddle.Normal), 0f);
        }

        [Test]
        public void BallHitByOpponentStillScoresForOriginalOwner()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 3);
            PlayerRuntimeState owner = runtime.FindPlayer(1);
            PlayerRuntimeState defender = runtime.FindPlayer(3);
            ArenaBoundarySegment defenderGoal = runtime.Arena.BoundarySegments[1];
            BallRuntimeState ball = runtime.Balls[0];
            ball.OwnerPlayerId = owner.PlayerId;
            ball.OwnerTeamId = owner.TeamId;
            ball.Position = defender.Paddle.Position + defender.Paddle.Normal * (defender.Paddle.Thickness + 0.6f);
            ball.Velocity = -defender.Paddle.Normal * runtime.BallRule.InitialSpeed;

            runtime.Tick(0.2f);
            ball.Position = defenderGoal.GoalCenter - defenderGoal.InwardNormal * 0.2f;
            ball.Velocity = -defenderGoal.InwardNormal * runtime.BallRule.InitialSpeed;
            runtime.TickLocalPrototype(0f);

            Assert.AreEqual(1, owner.Score);
            Assert.AreEqual(0, defender.Score);
            Assert.AreEqual(0, runtime.Balls.Count);
        }

        [Test]
        public void BallHitByOpponentEnteringOwnGoalStillReboundsAsOwnBall()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 3);
            PlayerRuntimeState owner = runtime.FindPlayer(1);
            PlayerRuntimeState defender = runtime.FindPlayer(3);
            ArenaBoundarySegment ownerGoal = runtime.Arena.BoundarySegments[5];
            BallRuntimeState ball = runtime.Balls[0];
            ball.OwnerPlayerId = owner.PlayerId;
            ball.OwnerTeamId = owner.TeamId;
            ball.Position = defender.Paddle.Position + defender.Paddle.Normal * (defender.Paddle.Thickness + 0.6f);
            ball.Velocity = -defender.Paddle.Normal * runtime.BallRule.InitialSpeed;

            runtime.Tick(0.2f);
            ball.Position = ownerGoal.GoalCenter - ownerGoal.InwardNormal * 0.2f;
            ball.Velocity = -ownerGoal.InwardNormal * runtime.BallRule.InitialSpeed;
            runtime.TickLocalPrototype(0f);

            Assert.AreEqual(owner.PlayerId, ball.OwnerPlayerId);
            Assert.AreEqual(owner.TeamId, ball.OwnerTeamId);
            Assert.AreEqual(0, owner.Score);
            Assert.AreEqual(0, defender.Score);
            Assert.AreEqual(BallState.GoalRebound, ball.BallState);
            Assert.AreEqual(1, runtime.Balls.Count);
            Assert.Greater(Vector2.Dot(ball.Velocity, owner.Paddle.Normal), 0f);
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
                if (player.IsDisabled || player.Paddle == null)
                {
                    continue;
                }

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
            runtime.StartLocalPrototype(aiCount: 3);

            runtime.TickLocalPrototype(0.02f);

            Assert.GreaterOrEqual(runtime.Balls.Count, 2);
            Assert.AreEqual(1, runtime.FindPlayer(3).ServeResource.OwnedBallsInField);
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

        private static List<int> GetOvertimeEligiblePlayerIds(GatebreakerMatchRuntime runtime)
        {
            FieldInfo field = typeof(GatebreakerMatchRuntime).GetField(
                "_overtimeEligiblePlayerIds",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            return (List<int>)field.GetValue(runtime);
        }

        private static void AssertPaddleAlignedWithSegment(PaddleRuntimeState paddle, ArenaBoundarySegment segment, float inset)
        {
            Assert.IsNotNull(paddle);
            Assert.Greater(Vector2.Dot(paddle.Normal, segment.InwardNormal), 0.999f);
            Assert.Greater(Mathf.Abs(Vector2.Dot(paddle.Tangent, segment.Tangent)), 0.999f);
            Vector2 expectedCenter = segment.GoalCenter + segment.InwardNormal * inset;
            Assert.Less(Vector2.Distance(paddle.Position, expectedCenter), 0.001f);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.GatebreakerArena.AI;
using App.HotUpdate.GatebreakerArena.Application;
using App.HotUpdate.GatebreakerArena.Ball;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Mode;
using App.HotUpdate.GatebreakerArena.Paddle;
using App.HotUpdate.GatebreakerArena.Serve;
using App.HotUpdate.GatebreakerArena.Zone;
using App.Shared.Contracts;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Match
{
    public sealed class GatebreakerMatchRuntime : ITickable
    {
        private const int MaxCollisionIterations = 12;
        private const float CollisionEpsilon = 0.0001f;
        private const float CollisionSkin = 0.02f;

        private readonly GatebreakerModeCatalog _modeCatalog;
        private readonly BallSimulationSystem _ballSimulation;
        private readonly ServeResourceSystem _serveResourceSystem;
        private readonly GoalJudgeSystem _goalJudgeSystem;
        private readonly ScoreSystem _scoreSystem;
        private readonly PaddleBounceCalculator _paddleBounceCalculator;
        private readonly GatebreakerAiService _aiService;
        private readonly IAppLogger _logger;
        private readonly List<PlayerRuntimeState> _players = new List<PlayerRuntimeState>();
        private readonly List<PaddleRuntimeState> _paddles = new List<PaddleRuntimeState>();
        private readonly List<ZoneRuntimeState> _zones = new List<ZoneRuntimeState>();
        private readonly List<BallRuntimeState> _balls = new List<BallRuntimeState>();
        private readonly List<int> _overtimeEligiblePlayerIds = new List<int>();
        private readonly Dictionary<int, PlayerInputFrame> _inputFrames = new Dictionary<int, PlayerInputFrame>();
        private int _nextBallId = 1;
        private bool _hasWinner;
        private int _winnerPlayerId;

        public GatebreakerMatchRuntime(
            GatebreakerModeCatalog modeCatalog,
            BallSimulationSystem ballSimulation,
            ServeResourceSystem serveResourceSystem,
            GoalJudgeSystem goalJudgeSystem,
            ScoreSystem scoreSystem,
            IAppLogger logger)
        {
            _modeCatalog = modeCatalog ?? throw new ArgumentNullException(nameof(modeCatalog));
            _ballSimulation = ballSimulation ?? throw new ArgumentNullException(nameof(ballSimulation));
            _serveResourceSystem = serveResourceSystem ?? throw new ArgumentNullException(nameof(serveResourceSystem));
            _goalJudgeSystem = goalJudgeSystem ?? throw new ArgumentNullException(nameof(goalJudgeSystem));
            _scoreSystem = scoreSystem ?? throw new ArgumentNullException(nameof(scoreSystem));
            _paddleBounceCalculator = new PaddleBounceCalculator();
            _aiService = new GatebreakerAiService();
            BounceTuning = PaddleBounceTuning.CreateDefault();
            _logger = logger;
            Arena = ArenaGeometry.CreateDefault();
            Phase = MatchPhase.Waiting;
        }

        public MatchPhase Phase { get; private set; }
        public EffectiveMatchRule EffectiveRule { get; private set; }
        public BallRuleDefinition BallRule { get; private set; }
        public ArenaGeometry Arena { get; private set; }
        public PaddleBounceTuning BounceTuning { get; }
        public float RemainingTime { get; private set; }
        public IReadOnlyList<PlayerRuntimeState> Players => _players;
        public IReadOnlyList<PaddleRuntimeState> Paddles => _paddles;
        public IReadOnlyList<ZoneRuntimeState> Zones => _zones;
        public IReadOnlyList<BallRuntimeState> Balls => _balls;
        public bool HasWinner => _hasWinner;
        public int WinnerPlayerId => _winnerPlayerId;

        public void StartLocalPrototype(
            int aiCount = 3,
            string modeId = "PVE_STANDARD",
            string mapId = "MAP_ARENA_01",
            string ballTypeId = "BALL_NORMAL")
        {
            EffectiveRule = _modeCatalog.BuildEffectiveRule(modeId, mapId);
            BallRule = _modeCatalog.GetBall(ballTypeId);
            RemainingTime = EffectiveRule.Mode.MatchDuration;
            Phase = MatchPhase.Playing;
            _hasWinner = false;
            _winnerPlayerId = 0;
            _overtimeEligiblePlayerIds.Clear();
            _players.Clear();
            _paddles.Clear();
            _zones.Clear();
            _balls.Clear();
            _inputFrames.Clear();
            _nextBallId = 1;
            Arena = ArenaGeometry.CreateDefault();

            AddPlayer(1, 1, true, false);
            for (int i = 0; i < aiCount; i++)
            {
                int playerId = i + 2;
                AddPlayer(playerId, playerId, false, true);
            }

            for (int i = 0; i < EffectiveRule.InitialBallsInMatch; i++)
            {
                PlayerRuntimeState owner = _players[i % _players.Count];
                SpawnBallForPlayer(owner, "Initial", GetServePosition(owner), GetServeDirection(owner, Vector2.zero), false);
            }

            _logger?.LogInfo("GatebreakerMatchRuntime: 本地原型开局完成。players={0}, balls={1}", _players.Count, _balls.Count);
        }

        public void Tick(float deltaTime)
        {
            TickInternal(deltaTime, false);
        }

        public void ApplyInputFrame(PlayerInputFrame frame)
        {
            _inputFrames[frame.PlayerId] = frame;
        }

        public void TickLocalPrototype(float deltaTime)
        {
            TickInternal(deltaTime, true);
        }

        private void TickInternal(float deltaTime, bool includeAi)
        {
            if (Phase != MatchPhase.Playing && Phase != MatchPhase.Overtime)
            {
                return;
            }

            float safeDelta = Math.Max(0f, deltaTime);
            RemainingTime = Math.Max(0f, RemainingTime - safeDelta);
            foreach (PlayerRuntimeState player in _players)
            {
                _serveResourceSystem.Tick(player.ServeResource, safeDelta);
            }

            RefreshServeBlockReasons();

            if (RemainingTime <= 0f)
            {
                HandleTimeExpired();
                return;
            }

            List<PaddleMotionState> paddleMotions = ApplyControlFrames(includeAi, safeDelta);
            SimulateBallsSwept(safeDelta, paddleMotions);
            CommitPaddleMotions(paddleMotions);
            RefreshZoneDanger();
        }

        public bool TryServe(int playerId, out ServeBlockReason blockReason)
        {
            PlayerRuntimeState player = FindPlayer(playerId);
            if (player == null || EffectiveRule == null || BallRule == null)
            {
                blockReason = ServeBlockReason.PlayerDisabled;
                return false;
            }

            bool served = _serveResourceSystem.TryServe(
                player.ServeResource,
                _balls.Count(ball => ball.BallState == BallState.Flying || ball.BallState == BallState.GoalRebound),
                EffectiveRule.MaxBallsInMatch,
                player.IsDisabled,
                GetCurrentCooldownScale(),
                out blockReason);
            if (!served)
            {
                return false;
            }

            SpawnBallForPlayer(player, "Serve", GetServePosition(player), GetServeDirection(player, Vector2.zero), false);
            return true;
        }

        public GoalJudgeResult ResolveGoalEntry(int ballId, int zoneOwnerPlayerId, Vector2 reboundDirection)
        {
            BallRuntimeState ball = _balls.FirstOrDefault(item => item.BallId == ballId);
            PlayerRuntimeState zoneOwner = FindPlayer(zoneOwnerPlayerId);
            if (ball == null || zoneOwner == null)
            {
                throw new InvalidOperationException("Goal entry requires a live ball and a valid zone owner.");
            }

            GoalJudgeResult result = _goalJudgeSystem.ResolveGoalEntry(
                ball,
                zoneOwner.PlayerId,
                zoneOwner.TeamId,
                BallRule,
                reboundDirection);
            if (result.Scored)
            {
                _scoreSystem.AddScore(_players, EffectiveRule.Mode, result.ScoringPlayerId, result.ScoringTeamId);
                RemoveBall(ball);
                if (Phase == MatchPhase.Overtime && IsOvertimeWinningScore(result.ScoringPlayerId))
                {
                    EndWithWinner(result.ScoringPlayerId);
                }
            }

            return result;
        }

        public ScoreboardSnapshot CreateScoreboardSnapshot()
        {
            return _scoreSystem.CreateSnapshot(
                _players,
                Phase,
                RemainingTime,
                _overtimeEligiblePlayerIds,
                _hasWinner,
                _winnerPlayerId);
        }

        public PlayerRuntimeState FindPlayer(int playerId)
        {
            return _players.FirstOrDefault(player => player.PlayerId == playerId);
        }

        public bool SetLocalPlayer(int playerId)
        {
            PlayerRuntimeState localPlayer = FindPlayer(playerId);
            if (localPlayer == null)
            {
                return false;
            }

            for (int i = 0; i < _players.Count; i++)
            {
                PlayerRuntimeState player = _players[i];
                bool isLocalPlayer = player.PlayerId == playerId;
                player.IsLocalPlayer = isLocalPlayer;
                player.IsAi = !isLocalPlayer;
            }

            return true;
        }

        private void AddPlayer(int playerId, int teamId, bool isLocalPlayer, bool isAi)
        {
            int playerIndex = _players.Count;
            Vector2 normal = Arena.GetSideNormal(EffectiveRule.Map.SpawnLayoutType, playerIndex);
            Vector2 tangent = Arena.GetSideTangent(normal);
            var paddle = new PaddleRuntimeState
            {
                PlayerId = playerId,
                TeamId = teamId,
                Normal = normal,
                Tangent = tangent,
                AxisPosition = 0f,
                MoveAxis = 0f,
                Length = Arena.PaddleLength * (1f + EffectiveRule.Map.GoalSizeModifier),
                Thickness = Arena.PaddleThickness,
                Speed = Arena.PaddleSpeed,
            };
            paddle.Position = Arena.GetPaddleCenter(normal, paddle.AxisPosition);

            var zone = new ZoneRuntimeState
            {
                PlayerId = playerId,
                TeamId = teamId,
                Normal = normal,
                Center = GetZoneCenter(normal),
                HalfLength = paddle.Length * 0.5f,
                IsDanger = false,
                LastEnteredBallId = 0,
            };

            var player = new PlayerRuntimeState
            {
                PlayerId = playerId,
                TeamId = teamId,
                IsLocalPlayer = isLocalPlayer,
                IsAi = isAi,
                IsDisabled = false,
                Score = 0,
                ServeResource = _serveResourceSystem.CreateState(
                    EffectiveRule.Mode.InitialServeAmmo,
                    EffectiveRule.Mode.MaxServeAmmo,
                    EffectiveRule.Mode.MaxOwnedBallsInField,
                    EffectiveRule.BaseServeCooldown),
                Paddle = paddle,
                Zone = zone,
            };

            _players.Add(player);
            _paddles.Add(paddle);
            _zones.Add(zone);
        }

        private BallRuntimeState SpawnBallForPlayer(PlayerRuntimeState player, string sourceType, Vector2 position, Vector2 direction, bool countOwnedBall)
        {
            BallRuntimeState ball = _ballSimulation.SpawnBall(
                _nextBallId++,
                player.PlayerId,
                player.TeamId,
                sourceType,
                BallRule,
                position,
                direction);
            _balls.Add(ball);
            if (countOwnedBall)
            {
                player.ServeResource.OwnedBallsInField += 1;
            }

            return ball;
        }

        private void RemoveBall(BallRuntimeState ball)
        {
            ball.BallState = BallState.Destroyed;
            _balls.Remove(ball);
            PlayerRuntimeState owner = FindPlayer(ball.OwnerPlayerId);
            if (owner != null)
            {
                _serveResourceSystem.OnOwnedBallRemoved(owner.ServeResource);
            }
        }

        private List<PaddleMotionState> ApplyControlFrames(bool includeAi, float deltaTime)
        {
            var motions = new List<PaddleMotionState>(_players.Count);
            for (int i = 0; i < _players.Count; i++)
            {
                PlayerRuntimeState player = _players[i];
                PlayerInputFrame frame = GetControlFrame(player, includeAi);
                motions.Add(CreatePaddleMotion(player.Paddle, frame.MoveAxis, deltaTime));
                if (frame.ServePressed)
                {
                    TryServeFromFrame(player, frame);
                    _inputFrames[player.PlayerId] = new PlayerInputFrame(player.PlayerId, frame.MoveAxis, false, frame.AimDirection);
                }
            }

            return motions;
        }

        private PlayerInputFrame GetControlFrame(PlayerRuntimeState player, bool includeAi)
        {
            if (includeAi && player.IsAi)
            {
                return _aiService.BuildFrame(player, this);
            }

            return _inputFrames.TryGetValue(player.PlayerId, out PlayerInputFrame frame)
                ? frame
                : new PlayerInputFrame(player.PlayerId, 0f, false, Vector2.zero);
        }

        private PaddleMotionState CreatePaddleMotion(PaddleRuntimeState paddle, float moveAxis, float deltaTime)
        {
            if (paddle == null)
            {
                return PaddleMotionState.Empty;
            }

            paddle.MoveAxis = Mathf.Clamp(moveAxis, -1f, 1f);
            float previousAxis = paddle.AxisPosition;
            float safeDelta = Mathf.Max(0f, deltaTime);
            if (safeDelta <= 0f)
            {
                paddle.TangentVelocity = 0f;
                paddle.Position = Arena.GetPaddleCenter(paddle.Normal, previousAxis);
                return new PaddleMotionState(
                    paddle,
                    previousAxis,
                    previousAxis,
                    Arena.GetPaddleCenter(paddle.Normal, previousAxis),
                    Arena.GetPaddleCenter(paddle.Normal, previousAxis),
                    0f);
            }

            float targetAxis = Arena.ClampPaddleAxis(
                paddle.Normal,
                previousAxis + paddle.MoveAxis * paddle.Speed * safeDelta);
            return new PaddleMotionState(
                paddle,
                previousAxis,
                targetAxis,
                Arena.GetPaddleCenter(paddle.Normal, previousAxis),
                Arena.GetPaddleCenter(paddle.Normal, targetAxis),
                (targetAxis - previousAxis) / safeDelta);
        }

        private void CommitPaddleMotions(IReadOnlyList<PaddleMotionState> motions)
        {
            if (motions == null)
            {
                return;
            }

            for (int i = 0; i < motions.Count; i++)
            {
                PaddleMotionState motion = motions[i];
                if (motion.Paddle == null)
                {
                    continue;
                }

                motion.Paddle.AxisPosition = motion.TargetAxis;
                motion.Paddle.Position = motion.TargetPosition;
                motion.Paddle.TangentVelocity = motion.TangentVelocity;
            }
        }

        private bool TryServeFromFrame(PlayerRuntimeState player, PlayerInputFrame frame)
        {
            if (player == null)
            {
                return false;
            }

            bool served = _serveResourceSystem.TryServe(
                player.ServeResource,
                _balls.Count(ball => ball.BallState == BallState.Flying || ball.BallState == BallState.GoalRebound),
                EffectiveRule.MaxBallsInMatch,
                player.IsDisabled,
                GetCurrentCooldownScale(),
                out _);
            if (!served)
            {
                return false;
            }

            SpawnBallForPlayer(player, "Serve", GetServePosition(player), GetServeDirection(player, frame.AimDirection), false);
            return true;
        }

        private void RefreshServeBlockReasons()
        {
            if (EffectiveRule == null)
            {
                return;
            }

            int activeBallCount = _balls.Count(ball => ball.BallState == BallState.Flying || ball.BallState == BallState.GoalRebound);
            for (int i = 0; i < _players.Count; i++)
            {
                PlayerRuntimeState player = _players[i];
                _serveResourceSystem.EvaluateCanServe(
                    player.ServeResource,
                    activeBallCount,
                    EffectiveRule.MaxBallsInMatch,
                    player.IsDisabled);
            }
        }

        private void SimulateBallsSwept(float deltaTime, IReadOnlyList<PaddleMotionState> paddleMotions)
        {
            if (deltaTime <= 0f)
            {
                ResolveFieldCollisions();
                return;
            }

            for (int i = _balls.Count - 1; i >= 0; i--)
            {
                if (Phase != MatchPhase.Playing && Phase != MatchPhase.Overtime)
                {
                    return;
                }

                BallRuntimeState ball = _balls[i];
                if (ball == null || (ball.BallState != BallState.Flying && ball.BallState != BallState.GoalRebound))
                {
                    continue;
                }

                if (ball.BallState == BallState.GoalRebound)
                {
                    ball.Position += ball.Velocity * deltaTime;
                    if (IsInsideArena(ball.Position))
                    {
                        _goalJudgeSystem.FinishRebound(ball);
                    }

                    continue;
                }

                SimulateFlyingBallSwept(ball, deltaTime, paddleMotions);
            }
        }

        private void SimulateFlyingBallSwept(
            BallRuntimeState ball,
            float deltaTime,
            IReadOnlyList<PaddleMotionState> paddleMotions)
        {
            float elapsedTime = 0f;
            float remainingTime = deltaTime;
            for (int iteration = 0; iteration < MaxCollisionIterations; iteration++)
            {
                if (ball == null ||
                    ball.BallState != BallState.Flying ||
                    remainingTime <= CollisionEpsilon ||
                    (Phase != MatchPhase.Playing && Phase != MatchPhase.Overtime))
                {
                    return;
                }

                Vector2 start = ball.Position;
                Vector2 end = start + ball.Velocity * remainingTime;
                if (!TryFindEarliestSweepHit(
                        start,
                        end,
                        elapsedTime,
                        remainingTime,
                        deltaTime,
                        paddleMotions,
                        out SweepHit hit))
                {
                    ball.Position = end;
                    return;
                }

                ball.Position = hit.Point;
                float consumedTime = remainingTime * hit.Time;
                elapsedTime += consumedTime;
                remainingTime = Mathf.Max(0f, remainingTime - consumedTime);

                switch (hit.Type)
                {
                    case SweepHitType.Paddle:
                        ResolveSweptPaddleHit(ball, hit);
                        break;
                    case SweepHitType.Goal:
                        ResolveSweptGoalHit(ball, hit);
                        if (ball.BallState != BallState.Flying)
                        {
                            return;
                        }

                        break;
                    case SweepHitType.Wall:
                        ResolveSweptWallHit(ball, hit);
                        break;
                }

                if (hit.Time <= CollisionEpsilon)
                {
                    ball.Position += ball.Velocity.normalized * CollisionSkin;
                }
            }

            ball.Position += ball.Velocity * remainingTime;
        }

        private bool TryFindEarliestSweepHit(
            Vector2 start,
            Vector2 end,
            float elapsedTime,
            float segmentDuration,
            float totalDeltaTime,
            IReadOnlyList<PaddleMotionState> paddleMotions,
            out SweepHit bestHit)
        {
            bestHit = SweepHit.None;
            if (segmentDuration <= CollisionEpsilon || totalDeltaTime <= CollisionEpsilon)
            {
                return false;
            }

            if (paddleMotions != null)
            {
                for (int i = 0; i < paddleMotions.Count; i++)
                {
                    TryAddPaddleSweepHit(
                        paddleMotions[i],
                        start,
                        end,
                        elapsedTime,
                        segmentDuration,
                        totalDeltaTime,
                        ref bestHit);
                }
            }

            TryAddBoundarySweepHit(start, end, -Arena.HalfHeight, true, Vector2.up, ref bestHit);
            TryAddBoundarySweepHit(start, end, Arena.HalfHeight, true, Vector2.down, ref bestHit);
            TryAddBoundarySweepHit(start, end, -Arena.HalfWidth, false, Vector2.right, ref bestHit);
            TryAddBoundarySweepHit(start, end, Arena.HalfWidth, false, Vector2.left, ref bestHit);
            return bestHit.Type != SweepHitType.None;
        }

        private void TryAddPaddleSweepHit(
            PaddleMotionState motion,
            Vector2 start,
            Vector2 end,
            float elapsedTime,
            float segmentDuration,
            float totalDeltaTime,
            ref SweepHit bestHit)
        {
            PaddleRuntimeState paddle = motion.Paddle;
            if (paddle == null)
            {
                return;
            }

            Vector2 movement = end - start;
            if (Vector2.Dot(movement, -paddle.Normal) <= CollisionEpsilon)
            {
                return;
            }

            float segmentStartT = elapsedTime / totalDeltaTime;
            float segmentEndT = (elapsedTime + segmentDuration) / totalDeltaTime;
            Vector2 paddleStart = motion.GetPosition(segmentStartT);
            Vector2 paddleEnd = motion.GetPosition(segmentEndT);
            float n0 = Vector2.Dot(start - paddleStart, paddle.Normal);
            float n1 = Vector2.Dot(end - paddleEnd, paddle.Normal);
            if (n0 <= paddle.Thickness || n1 > paddle.Thickness)
            {
                return;
            }

            float denominator = n0 - n1;
            if (denominator <= CollisionEpsilon)
            {
                return;
            }

            float hitTime = (n0 - paddle.Thickness) / denominator;
            if (hitTime < -CollisionEpsilon || hitTime > 1f + CollisionEpsilon)
            {
                return;
            }

            hitTime = Mathf.Clamp01(hitTime);
            float globalHitT = (elapsedTime + segmentDuration * hitTime) / totalDeltaTime;
            Vector2 hitPoint = Vector2.Lerp(start, end, hitTime);
            Vector2 paddleAtHit = motion.GetPosition(globalHitT);
            float tangentDistance = Vector2.Dot(hitPoint - paddleAtHit, paddle.Tangent);
            if (Mathf.Abs(tangentDistance) > paddle.Length * 0.5f + CollisionEpsilon)
            {
                return;
            }

            var hit = SweepHit.Paddle(hitTime, globalHitT, hitPoint, motion, tangentDistance);
            ChooseEarlierHit(hit, ref bestHit);
        }

        private void TryAddBoundarySweepHit(
            Vector2 start,
            Vector2 end,
            float boundary,
            bool horizontal,
            Vector2 inwardNormal,
            ref SweepHit bestHit)
        {
            float startAxis = horizontal ? start.y : start.x;
            float endAxis = horizontal ? end.y : end.x;
            bool crosses = inwardNormal.y > 0f || inwardNormal.x > 0f
                ? startAxis >= boundary && endAxis < boundary
                : startAxis <= boundary && endAxis > boundary;
            if (!crosses)
            {
                return;
            }

            float denominator = startAxis - endAxis;
            if (Mathf.Abs(denominator) <= CollisionEpsilon)
            {
                return;
            }

            float hitTime = (startAxis - boundary) / denominator;
            if (hitTime < -CollisionEpsilon || hitTime > 1f + CollisionEpsilon)
            {
                return;
            }

            hitTime = Mathf.Clamp01(hitTime);
            Vector2 hitPoint = Vector2.Lerp(start, end, hitTime);
            if (HasZoneForSide(inwardNormal))
            {
                Vector2 outsidePoint = hitPoint - inwardNormal * CollisionSkin;
                if (Arena.TryGetGoalOwner(outsidePoint, _players.Count, EffectiveRule.Map.SpawnLayoutType, out int playerIndex))
                {
                    ChooseEarlierHit(SweepHit.Goal(hitTime, hitPoint, playerIndex), ref bestHit);
                }
            }
            else
            {
                ChooseEarlierHit(SweepHit.Wall(hitTime, hitPoint, inwardNormal), ref bestHit);
            }
        }

        private static void ChooseEarlierHit(SweepHit candidate, ref SweepHit bestHit)
        {
            if (candidate.Type == SweepHitType.None)
            {
                return;
            }

            if (bestHit.Type == SweepHitType.None ||
                candidate.Time < bestHit.Time - CollisionEpsilon ||
                (Mathf.Abs(candidate.Time - bestHit.Time) <= CollisionEpsilon && candidate.Priority < bestHit.Priority))
            {
                bestHit = candidate;
            }
        }

        private void ResolveSweptPaddleHit(BallRuntimeState ball, SweepHit hit)
        {
            PaddleRuntimeState paddle = hit.PaddleMotion.Paddle;
            float hitOffset = hit.TangentDistance / Mathf.Max(0.001f, paddle.Length * 0.5f);
            ball.Position = hit.PaddleMotion.GetPosition(hit.GlobalTime)
                            + paddle.Tangent * hit.TangentDistance
                            + paddle.Normal * (paddle.Thickness + CollisionSkin);
            ball.Velocity = _paddleBounceCalculator.CalculateBounce(
                ball.Velocity,
                hitOffset,
                BallRule,
                paddle.Normal,
                paddle.Tangent,
                BounceTuning,
                GetNormalizedPaddleVelocity(hit.PaddleMotion.TangentVelocity, paddle.Speed));
            _ballSimulation.ClampSpeed(ball, BallRule);
        }

        private void ResolveSweptGoalHit(BallRuntimeState ball, SweepHit hit)
        {
            if (hit.PlayerIndex < 0 || hit.PlayerIndex >= _players.Count)
            {
                return;
            }

            PlayerRuntimeState zoneOwner = _players[hit.PlayerIndex];
            if (zoneOwner.Zone != null)
            {
                zoneOwner.Zone.LastEnteredBallId = ball.BallId;
            }

            ResolveGoalEntry(ball.BallId, zoneOwner.PlayerId, zoneOwner.Paddle != null ? zoneOwner.Paddle.Normal : Vector2.up);
        }

        private void ResolveSweptWallHit(BallRuntimeState ball, SweepHit hit)
        {
            Vector2 normal = hit.WallNormal.sqrMagnitude > 0.0001f ? hit.WallNormal.normalized : Vector2.up;
            float normalSpeed = Vector2.Dot(ball.Velocity, normal);
            Vector2 tangentVelocity = ball.Velocity - normal * normalSpeed;
            ball.Position = hit.Point + normal * CollisionSkin;
            ball.Velocity = tangentVelocity - normal * normalSpeed * BallRule.WallBounceFactor;
            _ballSimulation.ClampSpeed(ball, BallRule);
        }

        private void ResolveFieldCollisions()
        {
            for (int i = _balls.Count - 1; i >= 0; i--)
            {
                BallRuntimeState ball = _balls[i];
                if (ball == null || (ball.BallState != BallState.Flying && ball.BallState != BallState.GoalRebound))
                {
                    continue;
                }

                if (ball.BallState == BallState.GoalRebound)
                {
                    if (IsInsideArena(ball.Position))
                    {
                        _goalJudgeSystem.FinishRebound(ball);
                    }

                    continue;
                }

                if (TryResolveGoal(ball))
                {
                    continue;
                }

                ResolvePaddleHit(ball);
                ResolveUnownedWallBounce(ball);
            }
        }

        private bool TryResolveGoal(BallRuntimeState ball)
        {
            if (!Arena.TryGetGoalOwner(ball.Position, _players.Count, EffectiveRule.Map.SpawnLayoutType, out int playerIndex))
            {
                return false;
            }

            PlayerRuntimeState zoneOwner = _players[playerIndex];
            if (zoneOwner.Zone != null)
            {
                zoneOwner.Zone.LastEnteredBallId = ball.BallId;
            }

            ResolveGoalEntry(ball.BallId, zoneOwner.PlayerId, zoneOwner.Paddle != null ? zoneOwner.Paddle.Normal : Vector2.up);
            return ball.BallState == BallState.Destroyed || ball.BallState == BallState.GoalRebound;
        }

        private void ResolvePaddleHit(BallRuntimeState ball)
        {
            for (int i = 0; i < _paddles.Count; i++)
            {
                PaddleRuntimeState paddle = _paddles[i];
                float approach = Vector2.Dot(ball.Velocity, -paddle.Normal);
                if (approach <= 0f)
                {
                    continue;
                }

                Vector2 relative = ball.Position - paddle.Position;
                float normalDistance = Vector2.Dot(relative, paddle.Normal);
                if (normalDistance < -paddle.Thickness || normalDistance > paddle.Thickness)
                {
                    continue;
                }

                float tangentDistance = Vector2.Dot(relative, paddle.Tangent);
                if (Mathf.Abs(tangentDistance) > paddle.Length * 0.5f)
                {
                    continue;
                }

                float hitOffset = tangentDistance / Mathf.Max(0.001f, paddle.Length * 0.5f);
                ball.Position = paddle.Position
                                + paddle.Tangent * tangentDistance
                                + paddle.Normal * (paddle.Thickness + 0.02f);
                ball.Velocity = _paddleBounceCalculator.CalculateBounce(
                    ball.Velocity,
                    hitOffset,
                    BallRule,
                    paddle.Normal,
                    paddle.Tangent,
                    BounceTuning,
                    GetNormalizedPaddleVelocity(paddle));
                _ballSimulation.ClampSpeed(ball, BallRule);
                return;
            }
        }

        private static float GetNormalizedPaddleVelocity(PaddleRuntimeState paddle)
        {
            if (paddle == null || paddle.Speed <= 0.001f)
            {
                return 0f;
            }

            return GetNormalizedPaddleVelocity(paddle.TangentVelocity, paddle.Speed);
        }

        private static float GetNormalizedPaddleVelocity(float tangentVelocity, float paddleSpeed)
        {
            if (paddleSpeed <= 0.001f)
            {
                return 0f;
            }

            return Mathf.Clamp(tangentVelocity / paddleSpeed, -1f, 1f);
        }

        private void ResolveUnownedWallBounce(BallRuntimeState ball)
        {
            Vector2 position = ball.Position;
            Vector2 velocity = ball.Velocity;
            bool bounced = false;

            if (!HasZoneForSide(Vector2.up) && position.y < -Arena.HalfHeight)
            {
                position.y = -Arena.HalfHeight;
                velocity.y = Mathf.Abs(velocity.y) * BallRule.WallBounceFactor;
                bounced = true;
            }
            else if (!HasZoneForSide(Vector2.down) && position.y > Arena.HalfHeight)
            {
                position.y = Arena.HalfHeight;
                velocity.y = -Mathf.Abs(velocity.y) * BallRule.WallBounceFactor;
                bounced = true;
            }

            if (!HasZoneForSide(Vector2.right) && position.x < -Arena.HalfWidth)
            {
                position.x = -Arena.HalfWidth;
                velocity.x = Mathf.Abs(velocity.x) * BallRule.WallBounceFactor;
                bounced = true;
            }
            else if (!HasZoneForSide(Vector2.left) && position.x > Arena.HalfWidth)
            {
                position.x = Arena.HalfWidth;
                velocity.x = -Mathf.Abs(velocity.x) * BallRule.WallBounceFactor;
                bounced = true;
            }

            if (!bounced)
            {
                return;
            }

            ball.Position = position;
            ball.Velocity = velocity;
            _ballSimulation.ClampSpeed(ball, BallRule);
        }

        private void RefreshZoneDanger()
        {
            for (int zoneIndex = 0; zoneIndex < _zones.Count; zoneIndex++)
            {
                ZoneRuntimeState zone = _zones[zoneIndex];
                zone.IsDanger = false;
                for (int ballIndex = 0; ballIndex < _balls.Count; ballIndex++)
                {
                    BallRuntimeState ball = _balls[ballIndex];
                    if (ball == null || ball.OwnerPlayerId == zone.PlayerId || ball.BallState != BallState.Flying)
                    {
                        continue;
                    }

                    float approach = Vector2.Dot(ball.Velocity, -zone.Normal);
                    float distance = Mathf.Abs(Vector2.Dot(ball.Position - zone.Center, zone.Normal));
                    if (approach > 0f && distance <= BallRule.DangerPromptThreshold)
                    {
                        zone.IsDanger = true;
                        break;
                    }
                }
            }
        }

        private bool HasZoneForSide(Vector2 normal)
        {
            for (int i = 0; i < _zones.Count; i++)
            {
                if (Vector2.Dot(_zones[i].Normal, normal) > 0.95f)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsInsideArena(Vector2 position)
        {
            return position.x >= -Arena.HalfWidth &&
                   position.x <= Arena.HalfWidth &&
                   position.y >= -Arena.HalfHeight &&
                   position.y <= Arena.HalfHeight;
        }

        private Vector2 GetServePosition(PlayerRuntimeState player)
        {
            return player != null && player.Paddle != null
                ? player.Paddle.Position + player.Paddle.Normal * 0.45f
                : Vector2.zero;
        }

        private Vector2 GetServeDirection(PlayerRuntimeState player, Vector2 aimDirection)
        {
            if (player != null && EffectiveRule.Mode.AllowAimServe && aimDirection.sqrMagnitude > 0.0001f)
            {
                return aimDirection.normalized;
            }

            return player != null && player.Paddle != null
                ? (player.Paddle.Normal + player.Paddle.Tangent * 0.1f).normalized
                : InitialDirectionFor(player != null ? player.PlayerId : 1);
        }

        private Vector2 GetZoneCenter(Vector2 normal)
        {
            if (Mathf.Abs(normal.y) > 0.5f)
            {
                return new Vector2(0f, normal.y > 0f ? -Arena.HalfHeight : Arena.HalfHeight);
            }

            return new Vector2(normal.x > 0f ? -Arena.HalfWidth : Arena.HalfWidth, 0f);
        }

        private void HandleTimeExpired()
        {
            IReadOnlyList<int> highest = _scoreSystem.GetHighestScoringPlayerIds(_players);
            if (Phase == MatchPhase.Playing && highest.Count == 1)
            {
                EndWithWinner(highest[0]);
                return;
            }

            if (Phase == MatchPhase.Playing &&
                EffectiveRule.Mode.EnableOvertime &&
                EffectiveRule.Mode.OvertimeRuleType == OvertimeRuleType.SuddenDeath)
            {
                _overtimeEligiblePlayerIds.Clear();
                _overtimeEligiblePlayerIds.AddRange(highest);
                Phase = MatchPhase.Overtime;
                RemainingTime = EffectiveRule.Mode.OvertimeDuration;
                return;
            }

            if (highest.Count > 0)
            {
                EndWithWinner(highest[0]);
            }
        }

        private bool IsOvertimeWinningScore(int scoringPlayerId)
        {
            if (!EffectiveRule.Mode.OvertimeEligibleOnly)
            {
                return true;
            }

            return _overtimeEligiblePlayerIds.Contains(scoringPlayerId);
        }

        private void EndWithWinner(int playerId)
        {
            _hasWinner = true;
            _winnerPlayerId = playerId;
            Phase = MatchPhase.Result;
            RemainingTime = 0f;
        }

        private float GetCurrentCooldownScale()
        {
            return RemainingTime <= EffectiveRule.Mode.FinalPhaseStartTime
                ? EffectiveRule.Mode.FinalPhaseCooldownScale
                : 1f;
        }

        private enum SweepHitType
        {
            None,
            Paddle,
            Goal,
            Wall
        }

        private readonly struct PaddleMotionState
        {
            public static readonly PaddleMotionState Empty = new PaddleMotionState(null, 0f, 0f, Vector2.zero, Vector2.zero, 0f);

            public PaddleMotionState(
                PaddleRuntimeState paddle,
                float startAxis,
                float targetAxis,
                Vector2 startPosition,
                Vector2 targetPosition,
                float tangentVelocity)
            {
                Paddle = paddle;
                StartAxis = startAxis;
                TargetAxis = targetAxis;
                StartPosition = startPosition;
                TargetPosition = targetPosition;
                TangentVelocity = tangentVelocity;
            }

            public PaddleRuntimeState Paddle { get; }
            public float StartAxis { get; }
            public float TargetAxis { get; }
            public Vector2 StartPosition { get; }
            public Vector2 TargetPosition { get; }
            public float TangentVelocity { get; }

            public Vector2 GetPosition(float normalizedTime)
            {
                return Vector2.Lerp(StartPosition, TargetPosition, Mathf.Clamp01(normalizedTime));
            }
        }

        private readonly struct SweepHit
        {
            private SweepHit(
                SweepHitType type,
                float time,
                float globalTime,
                Vector2 point,
                PaddleMotionState paddleMotion,
                float tangentDistance,
                int playerIndex,
                Vector2 wallNormal)
            {
                Type = type;
                Time = time;
                GlobalTime = globalTime;
                Point = point;
                PaddleMotion = paddleMotion;
                TangentDistance = tangentDistance;
                PlayerIndex = playerIndex;
                WallNormal = wallNormal;
            }

            public static readonly SweepHit None = new SweepHit(SweepHitType.None, 1f, 1f, Vector2.zero, PaddleMotionState.Empty, 0f, -1, Vector2.zero);

            public SweepHitType Type { get; }
            public float Time { get; }
            public float GlobalTime { get; }
            public Vector2 Point { get; }
            public PaddleMotionState PaddleMotion { get; }
            public float TangentDistance { get; }
            public int PlayerIndex { get; }
            public Vector2 WallNormal { get; }

            public int Priority
            {
                get
                {
                    switch (Type)
                    {
                        case SweepHitType.Paddle:
                            return 0;
                        case SweepHitType.Goal:
                            return 1;
                        case SweepHitType.Wall:
                            return 2;
                        default:
                            return 3;
                    }
                }
            }

            public static SweepHit Paddle(
                float time,
                float globalTime,
                Vector2 point,
                PaddleMotionState paddleMotion,
                float tangentDistance)
            {
                return new SweepHit(SweepHitType.Paddle, time, globalTime, point, paddleMotion, tangentDistance, -1, Vector2.zero);
            }

            public static SweepHit Goal(float time, Vector2 point, int playerIndex)
            {
                return new SweepHit(SweepHitType.Goal, time, time, point, PaddleMotionState.Empty, 0f, playerIndex, Vector2.zero);
            }

            public static SweepHit Wall(float time, Vector2 point, Vector2 wallNormal)
            {
                return new SweepHit(SweepHitType.Wall, time, time, point, PaddleMotionState.Empty, 0f, -1, wallNormal);
            }
        }

        private static Vector2 InitialDirectionFor(int playerId)
        {
            switch (playerId % 4)
            {
                case 1:
                    return new Vector2(0.15f, 1f);
                case 2:
                    return new Vector2(-0.15f, -1f);
                case 3:
                    return new Vector2(1f, 0.15f);
                default:
                    return new Vector2(-1f, -0.15f);
            }
        }
    }
}

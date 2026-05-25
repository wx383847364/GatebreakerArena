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
        private const int DefaultPlayerCount = 4;
        private const int MaxPlayerCount = 4;
        private const uint ChecksumOffsetBasis = 2166136261u;
        private const uint ChecksumPrime = 16777619u;

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
        private readonly Dictionary<int, List<GatebreakerFrameInput>> _stepInputBuffer = new Dictionary<int, List<GatebreakerFrameInput>>();
        private int _nextBallId = 1;
        private bool _hasWinner;
        private int _winnerPlayerId;
        private float _localPrototypeFrameAccumulator;

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
        public string MatchId { get; private set; }
        public int Seed { get; private set; }
        public int SimulationFps { get; private set; } = GatebreakerMatchStartConfig.DefaultSimulationFps;
        public int InputDelayFrames { get; private set; }
        public int LocalPlayerId { get; private set; } = 1;
        public string ConfigHash { get; private set; }
        public string TuningHash { get; private set; }
        public int LastFrameIndex { get; private set; } = -1;
        public float FrameDelta => 1f / Math.Max(1, SimulationFps);
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
            int playerCount = Mathf.Clamp(aiCount + 1, 1, MaxPlayerCount);
            var activeSlots = new List<int>(playerCount);
            for (int i = 0; i < playerCount; i++)
            {
                activeSlots.Add(i + 1);
            }

            StartMatch(new GatebreakerMatchStartConfig
            {
                MatchId = "LOCAL_PROTOTYPE",
                ModeId = modeId,
                MapId = mapId,
                BallTypeId = ballTypeId,
                ActiveSlots = activeSlots,
                LocalPlayerId = 1,
                SimulationFps = GatebreakerMatchStartConfig.DefaultSimulationFps,
                InputDelayFrames = 0,
            });

            for (int i = 0; i < _players.Count; i++)
            {
                PlayerRuntimeState player = _players[i];
                bool isLocalPlayer = player.PlayerId == LocalPlayerId;
                player.IsLocalPlayer = isLocalPlayer;
                player.IsAi = !isLocalPlayer;
            }

            _logger?.LogInfo("GatebreakerMatchRuntime: 本地原型开局完成。players={0}, balls={1}", _players.Count, _balls.Count);
        }

        public void StartMatch(GatebreakerMatchStartConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            List<int> activePlayerIds = ResolveActivePlayerIds(config);
            EffectiveRule = _modeCatalog.BuildEffectiveRule(
                string.IsNullOrEmpty(config.ModeId) ? "PVE_STANDARD" : config.ModeId,
                string.IsNullOrEmpty(config.MapId) ? "MAP_ARENA_01" : config.MapId);
            BallRule = _modeCatalog.GetBall(string.IsNullOrEmpty(config.BallTypeId) ? "BALL_NORMAL" : config.BallTypeId);
            MatchId = config.MatchId ?? string.Empty;
            Seed = config.Seed;
            SimulationFps = config.SimulationFps > 0
                ? config.SimulationFps
                : GatebreakerMatchStartConfig.DefaultSimulationFps;
            InputDelayFrames = Math.Max(0, config.InputDelayFrames);
            LocalPlayerId = config.LocalPlayerId > 0 && activePlayerIds.Contains(config.LocalPlayerId)
                ? config.LocalPlayerId
                : activePlayerIds[0];
            ConfigHash = config.ConfigHash ?? string.Empty;
            TuningHash = config.TuningHash ?? string.Empty;
            RemainingTime = EffectiveRule.Mode.MatchDuration;
            Phase = MatchPhase.Playing;
            _hasWinner = false;
            _winnerPlayerId = 0;
            _localPrototypeFrameAccumulator = 0f;
            LastFrameIndex = -1;
            _overtimeEligiblePlayerIds.Clear();
            _players.Clear();
            _paddles.Clear();
            _zones.Clear();
            _balls.Clear();
            _inputFrames.Clear();
            _stepInputBuffer.Clear();
            _nextBallId = 1;
            Arena = ArenaGeometry.CreateForMap(EffectiveRule.Map);
            ApplyTuningValues(config.TuningValues);

            for (int i = 0; i < activePlayerIds.Count; i++)
            {
                int playerId = activePlayerIds[i];
                AddPlayer(playerId, playerId, playerId == LocalPlayerId, false);
            }

            for (int i = 0; i < EffectiveRule.InitialBallsInMatch; i++)
            {
                PlayerRuntimeState owner = _players[i % _players.Count];
                SpawnBallForPlayer(owner, "Initial", GetServePosition(owner), GetInitialBallDirection(owner, i), false);
            }
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
            if (deltaTime <= 0f)
            {
                TickInternal(deltaTime, true);
                return;
            }

            _localPrototypeFrameAccumulator += deltaTime;
            int stepCount = Mathf.FloorToInt(_localPrototypeFrameAccumulator / FrameDelta);
            if (stepCount <= 0)
            {
                return;
            }

            _localPrototypeFrameAccumulator -= stepCount * FrameDelta;
            for (int i = 0; i < stepCount; i++)
            {
                StepFrame(LastFrameIndex + 1, BuildLocalPrototypeFrameInputs());
            }
        }

        public void StepFrame(int frameIndex, IReadOnlyList<GatebreakerFrameInput> inputs)
        {
            BufferStepInputs(frameIndex, inputs);
            PrepareStepInputs(GetBufferedInputs(frameIndex - InputDelayFrames));
            TrimStepInputBuffer(frameIndex - InputDelayFrames);
            LastFrameIndex = frameIndex;
            TickInternal(FrameDelta, false);
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

        public GatebreakerMatchChecksum CreateChecksum(int frameIndex)
        {
            uint hash = ChecksumOffsetBasis;
            HashInt(ref hash, frameIndex);
            HashInt(ref hash, (int)Phase);
            HashInt(ref hash, Mathf.RoundToInt(RemainingTime * SimulationFps));
            HashInt(ref hash, QuantizeFloat(RemainingTime));
            HashInt(ref hash, _hasWinner ? 1 : 0);
            HashInt(ref hash, _winnerPlayerId);
            HashInt(ref hash, BounceTuning.HitOffsetInfluenceValue);
            HashInt(ref hash, BounceTuning.PaddleVelocityInfluenceValue);
            HashInt(ref hash, BounceTuning.MinimumOutwardShareValue);

            HashInt(ref hash, _players.Count);
            foreach (PlayerRuntimeState player in _players.OrderBy(player => player.PlayerId))
            {
                HashInt(ref hash, player.PlayerId);
                HashInt(ref hash, player.TeamId);
                HashInt(ref hash, player.Score);
                HashInt(ref hash, player.IsDisabled ? 1 : 0);
                if (player.ServeResource != null)
                {
                    HashInt(ref hash, player.ServeResource.CurrentServeAmmo);
                    HashInt(ref hash, player.ServeResource.MaxServeAmmo);
                    HashInt(ref hash, player.ServeResource.OwnedBallsInField);
                    HashInt(ref hash, player.ServeResource.MaxOwnedBallsInField);
                    HashInt(ref hash, QuantizeFloat(player.ServeResource.ServeCooldownRemaining));
                    HashInt(ref hash, (int)player.ServeResource.LastBlockReason);
                }
                else
                {
                    HashInt(ref hash, 0);
                    HashInt(ref hash, 0);
                    HashInt(ref hash, 0);
                    HashInt(ref hash, 0);
                    HashInt(ref hash, 0);
                    HashInt(ref hash, 0);
                }

                PaddleRuntimeState paddle = player.Paddle;
                HashInt(ref hash, QuantizeFloat(paddle != null ? paddle.AxisPosition : 0f));
                HashInt(ref hash, QuantizeFloat(paddle != null ? paddle.MoveAxis : 0f));
            }

            HashInt(ref hash, _balls.Count);
            foreach (BallRuntimeState ball in _balls.OrderBy(ball => ball.BallId))
            {
                HashInt(ref hash, ball.BallId);
                HashInt(ref hash, ball.OwnerPlayerId);
                HashInt(ref hash, ball.OwnerTeamId);
                HashInt(ref hash, (int)ball.BallState);
                HashVector(ref hash, ball.Position);
                HashVector(ref hash, ball.Velocity);
            }

            return new GatebreakerMatchChecksum(frameIndex, hash);
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

            if (localPlayer.IsDisabled || localPlayer.Paddle == null)
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

        private static List<int> ResolveActivePlayerIds(GatebreakerMatchStartConfig config)
        {
            if (config?.PlayerSlots != null && config.PlayerSlots.Count > 0)
            {
                if (config.PlayerSlots.Count > MaxPlayerCount)
                {
                    throw new ArgumentException("Gatebreaker match supports up to four active slots.");
                }

                var playerSlots = config.PlayerSlots
                    .Where(slot => slot != null)
                    .OrderBy(slot => slot.SideOrder >= 0 ? slot.SideOrder : slot.SlotIndex)
                    .ToArray();
                var playerIds = new List<int>(playerSlots.Length);
                for (int i = 0; i < playerSlots.Length; i++)
                {
                    int playerId = playerSlots[i].PlayerId;
                    if (playerId <= 0)
                    {
                        throw new ArgumentException("Player slot ids must be positive.");
                    }

                    if (playerIds.Contains(playerId))
                    {
                        throw new ArgumentException("Player slot ids must be unique.");
                    }

                    playerIds.Add(playerId);
                }

                if (playerIds.Count > 0)
                {
                    return playerIds;
                }
            }

            IReadOnlyList<int> activeSlots = config?.ActiveSlots;
            int playerCount = activeSlots != null && activeSlots.Count > 0
                ? activeSlots.Count
                : DefaultPlayerCount;
            if (playerCount > MaxPlayerCount)
            {
                throw new ArgumentException("Gatebreaker match supports up to four active slots.");
            }

            var result = new List<int>(playerCount);
            bool zeroBasedSlots = false;
            if (activeSlots != null)
            {
                for (int i = 0; i < activeSlots.Count; i++)
                {
                    if (activeSlots[i] == 0)
                    {
                        zeroBasedSlots = true;
                        break;
                    }
                }
            }

            for (int i = 0; i < playerCount; i++)
            {
                int playerId = activeSlots != null && activeSlots.Count > 0
                    ? activeSlots[i] + (zeroBasedSlots ? 1 : 0)
                    : i + 1;
                if (playerId <= 0)
                {
                    throw new ArgumentException("Active slot player ids must be positive.");
                }

                if (result.Contains(playerId))
                {
                    throw new ArgumentException("Active slot player ids must be unique.");
                }

                result.Add(playerId);
            }

            return result;
        }

        private void ApplyTuningValues(IReadOnlyDictionary<string, int> tuningValues)
        {
            BounceTuning.ResetToDefaults();
            if (tuningValues == null)
            {
                return;
            }

            if (TryGetTuningValue(tuningValues, out int hitOffsetValue, "HitOffsetInfluenceValue", "hitOffsetInfluenceValue", "hitOffsetInfluence"))
            {
                BounceTuning.SetHitOffsetInfluenceValue(hitOffsetValue);
            }

            if (TryGetTuningValue(tuningValues, out int paddleVelocityValue, "PaddleVelocityInfluenceValue", "paddleVelocityInfluenceValue", "paddleVelocityInfluence"))
            {
                BounceTuning.SetPaddleVelocityInfluenceValue(paddleVelocityValue);
            }

            if (TryGetTuningValue(tuningValues, out int minimumOutwardValue, "MinimumOutwardShareValue", "minimumOutwardShareValue", "minimumOutwardShare"))
            {
                BounceTuning.SetMinimumOutwardShareValue(minimumOutwardValue);
            }
        }

        private static bool TryGetTuningValue(IReadOnlyDictionary<string, int> values, out int value, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (values.TryGetValue(keys[i], out value))
                {
                    return true;
                }
            }

            foreach (KeyValuePair<string, int> item in values)
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    if (string.Equals(item.Key, keys[i], StringComparison.OrdinalIgnoreCase))
                    {
                        value = item.Value;
                        return true;
                    }
                }
            }

            value = 0;
            return false;
        }

        private IReadOnlyList<GatebreakerFrameInput> BuildLocalPrototypeFrameInputs()
        {
            var inputs = new List<GatebreakerFrameInput>(_players.Count);
            for (int i = 0; i < _players.Count; i++)
            {
                PlayerRuntimeState player = _players[i];
                PlayerInputFrame frame = player.IsAi
                    ? _aiService.BuildFrame(player, this)
                    : GetControlFrame(player, false);
                inputs.Add(new GatebreakerFrameInput(frame.PlayerId, frame.MoveAxis, frame.ServePressed, frame.AimDirection));
            }

            return inputs;
        }

        private void PrepareStepInputs(IReadOnlyList<GatebreakerFrameInput> inputs)
        {
            _inputFrames.Clear();
            if (inputs == null)
            {
                return;
            }

            for (int i = 0; i < inputs.Count; i++)
            {
                GatebreakerFrameInput input = inputs[i];
                if (FindPlayer(input.PlayerId) == null)
                {
                    continue;
                }

                _inputFrames[input.PlayerId] = new PlayerInputFrame(
                    input.PlayerId,
                    Mathf.Clamp(input.MoveAxis, -1f, 1f),
                    input.ServePressed,
                    input.AimDirection);
            }
        }

        private void BufferStepInputs(int frameIndex, IReadOnlyList<GatebreakerFrameInput> inputs)
        {
            if (inputs == null || inputs.Count <= 0)
            {
                _stepInputBuffer[frameIndex] = new List<GatebreakerFrameInput>();
                return;
            }

            var bufferedInputs = new List<GatebreakerFrameInput>(inputs.Count);
            for (int i = 0; i < inputs.Count; i++)
            {
                bufferedInputs.Add(inputs[i]);
            }

            _stepInputBuffer[frameIndex] = bufferedInputs;
        }

        private IReadOnlyList<GatebreakerFrameInput> GetBufferedInputs(int frameIndex)
        {
            return _stepInputBuffer.TryGetValue(frameIndex, out List<GatebreakerFrameInput> inputs)
                ? inputs
                : null;
        }

        private void TrimStepInputBuffer(int lastAppliedFrameIndex)
        {
            if (_stepInputBuffer.Count <= 0)
            {
                return;
            }

            var framesToRemove = new List<int>();
            foreach (int frameIndex in _stepInputBuffer.Keys)
            {
                if (frameIndex < lastAppliedFrameIndex)
                {
                    framesToRemove.Add(frameIndex);
                }
            }

            for (int i = 0; i < framesToRemove.Count; i++)
            {
                _stepInputBuffer.Remove(framesToRemove[i]);
            }
        }

        private void AddPlayer(int playerId, int teamId, bool isLocalPlayer, bool isAi)
        {
            int playerIndex = _players.Count;
            bool hasPlayableGoal = !Arena.HasCustomBoundary || Arena.TryGetGoalSegmentForPlayer(playerIndex, out _);
            if (!hasPlayableGoal)
            {
                var disabledPlayer = new PlayerRuntimeState
                {
                    PlayerId = playerId,
                    TeamId = teamId,
                    IsLocalPlayer = isLocalPlayer,
                    IsAi = isAi,
                    IsDisabled = true,
                    Score = 0,
                    ServeResource = _serveResourceSystem.CreateState(
                        EffectiveRule.Mode.InitialServeAmmo,
                        EffectiveRule.Mode.MaxServeAmmo,
                        EffectiveRule.Mode.MaxOwnedBallsInField,
                        EffectiveRule.BaseServeCooldown),
                    Paddle = null,
                    Zone = null,
                };
                _players.Add(disabledPlayer);
                return;
            }

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

            if (Arena.HasCustomBoundary)
            {
                for (int i = 0; i < Arena.BoundarySegments.Count; i++)
                {
                    TryAddBoundarySegmentSweepHit(start, end, Arena.BoundarySegments[i], ref bestHit);
                }
            }
            else
            {
                TryAddBoundarySweepHit(start, end, -Arena.HalfHeight, true, Vector2.up, ref bestHit);
                TryAddBoundarySweepHit(start, end, Arena.HalfHeight, true, Vector2.down, ref bestHit);
                TryAddBoundarySweepHit(start, end, -Arena.HalfWidth, false, Vector2.right, ref bestHit);
                TryAddBoundarySweepHit(start, end, Arena.HalfWidth, false, Vector2.left, ref bestHit);
            }

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

        private void TryAddBoundarySegmentSweepHit(
            Vector2 start,
            Vector2 end,
            ArenaBoundarySegment boundary,
            ref SweepHit bestHit)
        {
            Vector2 movement = end - start;
            if (Vector2.Dot(movement, boundary.InwardNormal) >= -CollisionEpsilon)
            {
                return;
            }

            if (boundary.GoalPlayerIndex >= 0 && boundary.GoalPlayerIndex < _players.Count)
            {
                TryAddGoalTriggerSweepHit(start, end, boundary, ref bestHit);
            }

            Vector2 edge = boundary.End - boundary.Start;
            float denominator = Cross(movement, edge);
            if (Mathf.Abs(denominator) <= CollisionEpsilon)
            {
                return;
            }

            Vector2 offset = boundary.Start - start;
            float hitTime = Cross(offset, edge) / denominator;
            float edgeTime = Cross(offset, movement) / denominator;
            if (hitTime < -CollisionEpsilon ||
                hitTime > 1f + CollisionEpsilon ||
                edgeTime < -CollisionEpsilon ||
                edgeTime > 1f + CollisionEpsilon)
            {
                return;
            }

            hitTime = Mathf.Clamp01(hitTime);
            Vector2 hitPoint = Vector2.Lerp(start, end, hitTime);
            if (boundary.GoalPlayerIndex >= 0 &&
                boundary.GoalPlayerIndex < _players.Count &&
                boundary.IsPastGoalLine(hitPoint))
            {
                ChooseEarlierHit(SweepHit.Goal(hitTime, hitPoint, boundary.GoalPlayerIndex), ref bestHit);
            }
            else
            {
                ChooseEarlierHit(SweepHit.Wall(hitTime, hitPoint, boundary.InwardNormal), ref bestHit);
            }
        }

        private void TryAddGoalTriggerSweepHit(
            Vector2 start,
            Vector2 end,
            ArenaBoundarySegment boundary,
            ref SweepHit bestHit)
        {
            Vector2 movement = end - start;
            Vector2 triggerEdge = boundary.GoalTriggerEnd - boundary.GoalTriggerStart;
            float denominator = Cross(movement, triggerEdge);
            if (Mathf.Abs(denominator) <= CollisionEpsilon)
            {
                return;
            }

            Vector2 offset = boundary.GoalTriggerStart - start;
            float hitTime = Cross(offset, triggerEdge) / denominator;
            float edgeTime = Cross(offset, movement) / denominator;
            if (hitTime < -CollisionEpsilon ||
                hitTime > 1f + CollisionEpsilon ||
                edgeTime < -CollisionEpsilon ||
                edgeTime > 1f + CollisionEpsilon)
            {
                return;
            }

            hitTime = Mathf.Clamp01(hitTime);
            Vector2 hitPoint = Vector2.Lerp(start, end, hitTime);
            if (boundary.IsPastGoalLine(hitPoint))
            {
                ChooseEarlierHit(SweepHit.Goal(hitTime, hitPoint, boundary.GoalPlayerIndex), ref bestHit);
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
            TransferBallOwnerToPaddle(ball, paddle);
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
                TransferBallOwnerToPaddle(ball, paddle);
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

        private void TransferBallOwnerToPaddle(BallRuntimeState ball, PaddleRuntimeState paddle)
        {
            if (ball == null || paddle == null || ball.OwnerPlayerId == paddle.PlayerId)
            {
                return;
            }

            PlayerRuntimeState previousOwner = FindPlayer(ball.OwnerPlayerId);
            if (previousOwner?.ServeResource != null)
            {
                _serveResourceSystem.OnOwnedBallRemoved(previousOwner.ServeResource);
            }

            PlayerRuntimeState nextOwner = FindPlayer(paddle.PlayerId);
            if (nextOwner?.ServeResource != null)
            {
                nextOwner.ServeResource.OwnedBallsInField += 1;
            }

            ball.OwnerPlayerId = paddle.PlayerId;
            ball.OwnerTeamId = paddle.TeamId;
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
            if (Arena.HasCustomBoundary)
            {
                ResolveCustomBoundaryWallBounce(ball);
                return;
            }

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

        private void ResolveCustomBoundaryWallBounce(BallRuntimeState ball)
        {
            Vector2 position = ball.Position;
            Vector2 velocity = ball.Velocity;
            bool bounced = false;
            for (int i = 0; i < Arena.BoundarySegments.Count; i++)
            {
                ArenaBoundarySegment segment = Arena.BoundarySegments[i];
                bool activeGoal = segment.GoalPlayerIndex >= 0 &&
                                  segment.GoalPlayerIndex < _players.Count &&
                                  segment.ContainsGoalPoint(position);
                if (activeGoal)
                {
                    continue;
                }

                float distanceInside = Vector2.Dot(position - segment.Start, segment.InwardNormal);
                if (distanceInside >= 0f)
                {
                    continue;
                }

                position += segment.InwardNormal * -distanceInside;
                float normalSpeed = Vector2.Dot(velocity, segment.InwardNormal);
                if (normalSpeed < 0f)
                {
                    velocity -= segment.InwardNormal * normalSpeed * (1f + BallRule.WallBounceFactor);
                }

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
            return Arena.Contains(position);
        }

        private static float Cross(Vector2 left, Vector2 right)
        {
            return left.x * right.y - left.y * right.x;
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

        private Vector2 GetInitialBallDirection(PlayerRuntimeState player, int spawnIndex)
        {
            if (Seed == 0 || player?.Paddle == null)
            {
                return GetServeDirection(player, Vector2.zero);
            }

            uint mixed = MixSeed(Seed, player.PlayerId, spawnIndex);
            float normalized = (mixed & 0xffff) / 65535f;
            float tangentOffset = 0.1f + (normalized - 0.5f) * 0.24f;
            return (player.Paddle.Normal + player.Paddle.Tangent * tangentOffset).normalized;
        }

        private Vector2 GetZoneCenter(Vector2 normal)
        {
            return Arena.GetZoneCenter(normal);
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

        private static int QuantizeFloat(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0;
            }

            return Mathf.RoundToInt(value * 10000f);
        }

        private static void HashVector(ref uint hash, Vector2 value)
        {
            HashInt(ref hash, QuantizeFloat(value.x));
            HashInt(ref hash, QuantizeFloat(value.y));
        }

        private static void HashInt(ref uint hash, int value)
        {
            unchecked
            {
                hash ^= (byte)value;
                hash *= ChecksumPrime;
                hash ^= (byte)(value >> 8);
                hash *= ChecksumPrime;
                hash ^= (byte)(value >> 16);
                hash *= ChecksumPrime;
                hash ^= (byte)(value >> 24);
                hash *= ChecksumPrime;
            }
        }

        private static uint MixSeed(int seed, int playerId, int spawnIndex)
        {
            unchecked
            {
                uint value = (uint)seed;
                value ^= (uint)(playerId * 374761393);
                value = (value << 13) | (value >> 19);
                value ^= (uint)(spawnIndex * 668265263);
                value *= 2246822519u;
                value ^= value >> 15;
                return value;
            }
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

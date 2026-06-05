using System;
using System.Collections.Generic;
using System.Globalization;
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
        private const float EditorFallbackBallContactRadius = 0.08f;
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
        private float _ballContactRadius = EditorFallbackBallContactRadius;
        private int _nextBallId = 1;
        private int _nextScoreReachOrder = 1;
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
        public GatebreakerModeCatalog ModeCatalog => _modeCatalog;
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
        public float BallContactRadius => _ballContactRadius;
        public float BallGoalContactRadius => _ballContactRadius;
        public string LastGoalContactDiagnostic { get; private set; } = string.Empty;
        public int LastGoalContactBallId { get; private set; }
        public Vector2 LastGoalContactPosition { get; private set; }
        public bool HasWinner => _hasWinner;
        public int WinnerPlayerId => _winnerPlayerId;

        public bool SetBallContactRadius(float radius)
        {
            float nextRadius = Mathf.Clamp(radius, 0.01f, 0.5f);
            bool changed = Mathf.Abs(_ballContactRadius - nextRadius) > 0.0001f;
            _ballContactRadius = nextRadius;
            for (int i = 0; i < _balls.Count; i++)
            {
                if (_balls[i] != null)
                {
                    changed |= Mathf.Abs(GetBallContactRadius(_balls[i]) - nextRadius) > 0.0001f;
                    _balls[i].ContactRadius = nextRadius;
                }
            }

            return changed;
        }

        public bool SetBallGoalContactRadius(float radius)
        {
            return SetBallContactRadius(radius);
        }

        public bool SetBallContactRadiusForBall(int ballId, float radius)
        {
            BallRuntimeState ball = _balls.FirstOrDefault(item => item != null && item.BallId == ballId);
            if (ball == null)
            {
                return false;
            }

            float nextRadius = Mathf.Clamp(radius, 0.01f, 0.5f);
            if (Mathf.Abs(GetBallContactRadius(ball) - nextRadius) <= 0.0001f)
            {
                return false;
            }

            ball.ContactRadius = nextRadius;
            return true;
        }

        public bool SetArenaPaddleLength(float paddleLength)
        {
            if (Arena == null)
            {
                return false;
            }

            float nextPaddleLength = Math.Max(0.25f, paddleLength);
            if (Mathf.Abs(Arena.PaddleLength - nextPaddleLength) <= 0.0001f)
            {
                return false;
            }

            Arena = Arena.WithPaddleLength(nextPaddleLength);
            RefreshPaddleGeometryFromArena();
            return true;
        }

        public void StartLocalPrototype(
            int aiCount = -1,
            string modeId = "PVE_STANDARD",
            string mapId = "MAP_ARENA_01",
            string ballTypeId = "BALL_NORMAL")
        {
            int playerCount = ResolveLocalPrototypePlayerCount(aiCount, mapId);
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

        private int ResolveLocalPrototypePlayerCount(int aiCount, string mapId)
        {
            if (aiCount >= 0)
            {
                return Mathf.Clamp(aiCount + 1, 1, MaxPlayerCount);
            }

            string resolvedMapId = string.IsNullOrEmpty(mapId) ? "MAP_ARENA_01" : mapId;
            MapRuleDefinition map = _modeCatalog.GetMap(resolvedMapId);
            int defaultPlayerCount = map.DefaultPlayerCount > 0 ? map.DefaultPlayerCount : DefaultPlayerCount;
            return Mathf.Clamp(defaultPlayerCount, 1, MaxPlayerCount);
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
            SimulationFps = GatebreakerMatchStartConfig.DefaultSimulationFps;
            InputDelayFrames = Math.Max(0, config.InputDelayFrames);
            LocalPlayerId = config.LocalPlayerId > 0 && activePlayerIds.Contains(config.LocalPlayerId)
                ? config.LocalPlayerId
                : activePlayerIds[0];
            ConfigHash = config.ConfigHash ?? string.Empty;
            TuningHash = config.TuningHash ?? string.Empty;
            RemainingTime = EffectiveRule.Mode.CountdownSeconds;
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
            LastGoalContactDiagnostic = string.Empty;
            LastGoalContactBallId = 0;
            LastGoalContactPosition = Vector2.zero;
            _nextBallId = 1;
            _nextScoreReachOrder = 1;
            if (BallRule == null || BallRule.BallContactRadius <= 0f)
            {
                throw new InvalidOperationException("Gatebreaker ball contact radius must be configured by JSON before match start.");
            }

            _ballContactRadius = Mathf.Clamp(BallRule.BallContactRadius, 0.01f, 0.5f);
            Arena = ArenaGeometry.CreateForMap(EffectiveRule.Map, activePlayerIds);
            ApplyTuningValues(EffectiveRule.Mode.TuningValues, config.TuningValues);

            HashSet<int> aiPlayerIds = ResolveAiPlayerIds(config);
            for (int i = 0; i < activePlayerIds.Count; i++)
            {
                int playerId = activePlayerIds[i];
                AddPlayer(playerId, playerId, playerId == LocalPlayerId, aiPlayerIds.Contains(playerId));
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

        public bool ForceFinishWithCurrentLeader()
        {
            if (Phase != MatchPhase.Playing && Phase != MatchPhase.Overtime)
            {
                return false;
            }

            IReadOnlyList<int> stableTopPlayers = _scoreSystem.GetTopRankedPlayerIds(_players, true);
            if (stableTopPlayers.Count <= 0)
            {
                return false;
            }

            EndWithWinner(stableTopPlayers[0]);
            return true;
        }

        public void ApplyInputFrame(PlayerInputFrame frame)
        {
            if (!_inputFrames.TryGetValue(frame.PlayerId, out PlayerInputFrame existing) || !existing.ServePressed)
            {
                _inputFrames[frame.PlayerId] = frame;
                return;
            }

            Vector2 aimDirection = frame.ServePressed
                ? frame.AimDirection
                : existing.AimDirection;
            _inputFrames[frame.PlayerId] = new PlayerInputFrame(
                frame.PlayerId,
                frame.MoveAxis,
                true,
                aimDirection);
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

        public PlayerInputFrame BuildAiInputFrame(int playerId)
        {
            PlayerRuntimeState player = FindPlayer(playerId);
            return player != null && player.IsAi
                ? _aiService.BuildFrame(player, this)
                : new PlayerInputFrame(playerId, 0f, false, Vector2.zero);
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
                GetOwnGoalReboundDirection(ball, reboundDirection));
            if (result.Scored)
            {
                _scoreSystem.RecordGoal(
                    _players,
                    result.ScoringPlayerId,
                    result.ZoneOwnerPlayerId,
                    ref _nextScoreReachOrder);
                RemoveBall(ball);
                if (Phase == MatchPhase.Overtime && IsOvertimeWinningScore(result.ScoringPlayerId))
                {
                    EndWithWinner(result.ScoringPlayerId);
                }
            }
            else if (result.Rebounded)
            {
                ball.BallState = BallState.GoalRebound;
            }

            return result;
        }

        private static Vector2 GetOwnGoalReboundDirection(BallRuntimeState ball, Vector2 fallbackDirection)
        {
            if (ball != null && ball.Velocity.sqrMagnitude > 0.0001f)
            {
                return -ball.Velocity.normalized;
            }

            return fallbackDirection.sqrMagnitude > 0.0001f ? fallbackDirection.normalized : Vector2.up;
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
            HashInt(ref hash, _nextScoreReachOrder);
            HashInt(ref hash, _overtimeEligiblePlayerIds.Count);
            foreach (int playerId in _overtimeEligiblePlayerIds.OrderBy(playerId => playerId))
            {
                HashInt(ref hash, playerId);
            }

            HashInt(ref hash, BounceTuning.HitOffsetInfluenceValue);
            HashInt(ref hash, BounceTuning.PaddleVelocityInfluenceValue);
            HashInt(ref hash, BounceTuning.MinimumOutwardShareValue);
            HashInt(ref hash, QuantizeFloat(_ballContactRadius));

            HashInt(ref hash, _players.Count);
            foreach (PlayerRuntimeState player in _players.OrderBy(player => player.PlayerId))
            {
                HashInt(ref hash, player.PlayerId);
                HashInt(ref hash, player.TeamId);
                HashInt(ref hash, player.Score);
                HashInt(ref hash, player.HitScore);
                HashInt(ref hash, player.ScoreReachOrder);
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
                HashInt(ref hash, QuantizeFloat(GetBallContactRadius(ball)));
            }

            return new GatebreakerMatchChecksum(frameIndex, hash);
        }

        public PlayerRuntimeState FindPlayer(int playerId)
        {
            return _players.FirstOrDefault(player => player.PlayerId == playerId);
        }

        public bool SetLocalPlayer(int playerId, bool preserveAiFlags = false)
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
                if (preserveAiFlags)
                {
                    if (isLocalPlayer)
                    {
                        player.IsAi = false;
                    }
                }
                else
                {
                    player.IsAi = !isLocalPlayer;
                }
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

        private static HashSet<int> ResolveAiPlayerIds(GatebreakerMatchStartConfig config)
        {
            var result = new HashSet<int>();
            if (config?.PlayerSlots == null)
            {
                return result;
            }

            for (int i = 0; i < config.PlayerSlots.Count; i++)
            {
                GatebreakerMatchPlayerSlot slot = config.PlayerSlots[i];
                if (slot != null && slot.IsAi && slot.PlayerId > 0)
                {
                    result.Add(slot.PlayerId);
                }
            }

            return result;
        }

        private void ApplyTuningValues(
            IReadOnlyDictionary<string, int> modeTuningValues,
            IReadOnlyDictionary<string, int> startConfigTuningValues)
        {
            BounceTuning.ResetToDefaults();
            ApplyTuningValueMap(modeTuningValues);
            ApplyTuningValueMap(startConfigTuningValues);
        }

        private void ApplyTuningValueMap(IReadOnlyDictionary<string, int> tuningValues)
        {
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
                    HitScore = 0,
                    ScoreReachOrder = 0,
                    ServeResource = _serveResourceSystem.CreateState(
                        EffectiveRule.InitialServeAmmo,
                        EffectiveRule.MaxServeAmmo,
                        EffectiveRule.MaxOwnedBallsInField,
                        EffectiveRule.ServeRechargeSeconds),
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
                HitScore = 0,
                ScoreReachOrder = 0,
                ServeResource = _serveResourceSystem.CreateState(
                    EffectiveRule.InitialServeAmmo,
                    EffectiveRule.MaxServeAmmo,
                    EffectiveRule.MaxOwnedBallsInField,
                    EffectiveRule.ServeRechargeSeconds),
                Paddle = paddle,
                Zone = zone,
            };

            _players.Add(player);
            _paddles.Add(paddle);
            _zones.Add(zone);
        }

        private void RefreshPaddleGeometryFromArena()
        {
            float lengthModifier = EffectiveRule?.Map != null
                ? 1f + EffectiveRule.Map.GoalSizeModifier
                : 1f;
            for (int i = 0; i < _players.Count; i++)
            {
                PlayerRuntimeState player = _players[i];
                PaddleRuntimeState paddle = player.Paddle;
                if (paddle == null)
                {
                    continue;
                }

                paddle.Length = Arena.PaddleLength * lengthModifier;
                paddle.Thickness = Arena.PaddleThickness;
                paddle.Speed = Arena.PaddleSpeed;
                paddle.AxisPosition = Arena.ClampPaddleAxis(paddle.Normal, paddle.AxisPosition);
                paddle.Position = Arena.GetPaddleCenter(paddle.Normal, paddle.AxisPosition);

                ZoneRuntimeState zone = player.Zone;
                if (zone == null)
                {
                    continue;
                }

                zone.Center = GetZoneCenter(zone.Normal);
                zone.HalfLength = paddle.Length * 0.5f;
            }
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
            ball.ContactRadius = _ballContactRadius;
            _balls.Add(ball);
            if (countOwnedBall)
            {
                player.ServeResource.OwnedBallsInField += 1;
            }

            return ball;
        }

        private float GetBallContactRadius(BallRuntimeState ball)
        {
            if (ball != null && ball.ContactRadius > 0f)
            {
                return Mathf.Clamp(ball.ContactRadius, 0.01f, 0.5f);
            }

            return Mathf.Clamp(_ballContactRadius, 0.01f, 0.5f);
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
                if (ResolvePaddleHit(ball))
                {
                    continue;
                }

                if (TryResolveGoal(ball))
                {
                    return;
                }

                Vector2 end = start + ball.Velocity * remainingTime;
                if (!TryFindEarliestSweepHit(
                        ball,
                        start,
                        end,
                        elapsedTime,
                        remainingTime,
                        deltaTime,
                        paddleMotions,
                        out SweepHit hit))
                {
                    ball.Position = end;
                    if (ResolvePaddleHit(ball))
                    {
                        return;
                    }

                    if (!TryResolveGoal(ball))
                    {
                        ResolveUnownedWallBounce(ball);
                    }

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
            BallRuntimeState ball,
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
                        ball,
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
                    TryAddBoundarySegmentSweepHit(ball, start, end, Arena.BoundarySegments[i], ref bestHit);
                }
            }
            else
            {
                TryAddBoundarySweepHit(ball, start, end, -Arena.HalfHeight, true, Vector2.up, ref bestHit);
                TryAddBoundarySweepHit(ball, start, end, Arena.HalfHeight, true, Vector2.down, ref bestHit);
                TryAddBoundarySweepHit(ball, start, end, -Arena.HalfWidth, false, Vector2.right, ref bestHit);
                TryAddBoundarySweepHit(ball, start, end, Arena.HalfWidth, false, Vector2.left, ref bestHit);
            }

            return bestHit.Type != SweepHitType.None;
        }

        private void TryAddPaddleSweepHit(
            BallRuntimeState ball,
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
            float contactDistance = paddle.Thickness + GetBallContactRadius(ball);
            TryAddPaddleEndpointSweepHits(
                ball,
                motion,
                start,
                end,
                segmentStartT,
                segmentEndT,
                elapsedTime,
                segmentDuration,
                totalDeltaTime,
                ref bestHit);
            if (TryAddEmbeddedPaddleSweepHit(
                    motion,
                    start,
                    end,
                    n0,
                    n1,
                    contactDistance,
                    elapsedTime,
                    segmentDuration,
                    totalDeltaTime,
                    ref bestHit))
            {
                return;
            }

            if (n0 <= contactDistance || n1 > contactDistance)
            {
                return;
            }

            float denominator = n0 - n1;
            if (denominator <= CollisionEpsilon)
            {
                return;
            }

            float hitTime = (n0 - contactDistance) / denominator;
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

        private void TryAddPaddleEndpointSweepHits(
            BallRuntimeState ball,
            PaddleMotionState motion,
            Vector2 start,
            Vector2 end,
            float segmentStartT,
            float segmentEndT,
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

            float halfLength = paddle.Length * 0.5f;
            Vector2 frontOffset = paddle.Normal * paddle.Thickness;
            float endpointRadius = GetPaddleEndpointCollisionRadius(ball, paddle);
            TryAddPaddleEndpointSweepHit(
                motion,
                start,
                end,
                motion.GetPosition(segmentStartT) + frontOffset - paddle.Tangent * halfLength,
                motion.GetPosition(segmentEndT) + frontOffset - paddle.Tangent * halfLength,
                -halfLength,
                endpointRadius,
                elapsedTime,
                segmentDuration,
                totalDeltaTime,
                ref bestHit);
            TryAddPaddleEndpointSweepHit(
                motion,
                start,
                end,
                motion.GetPosition(segmentStartT) + frontOffset + paddle.Tangent * halfLength,
                motion.GetPosition(segmentEndT) + frontOffset + paddle.Tangent * halfLength,
                halfLength,
                endpointRadius,
                elapsedTime,
                segmentDuration,
                totalDeltaTime,
                ref bestHit);
        }

        private static void TryAddPaddleEndpointSweepHit(
            PaddleMotionState motion,
            Vector2 start,
            Vector2 end,
            Vector2 endpointStart,
            Vector2 endpointEnd,
            float endpointTangentDistance,
            float endpointRadius,
            float elapsedTime,
            float segmentDuration,
            float totalDeltaTime,
            ref SweepHit bestHit)
        {
            Vector2 relativeStart = start - endpointStart;
            Vector2 relativeMovement = (end - start) - (endpointEnd - endpointStart);
            float radius = Mathf.Max(0.01f, endpointRadius);
            float c = Vector2.Dot(relativeStart, relativeStart) - radius * radius;
            if (c <= CollisionEpsilon)
            {
                float segmentStartT = elapsedTime / totalDeltaTime;
                ChooseEarlierHit(SweepHit.Paddle(0f, segmentStartT, start, motion, endpointTangentDistance), ref bestHit);
                return;
            }

            float a = Vector2.Dot(relativeMovement, relativeMovement);
            if (a <= CollisionEpsilon)
            {
                return;
            }

            float b = 2f * Vector2.Dot(relativeStart, relativeMovement);
            if (b >= -CollisionEpsilon)
            {
                return;
            }

            float discriminant = b * b - 4f * a * c;
            if (discriminant < -CollisionEpsilon)
            {
                return;
            }

            float sqrt = Mathf.Sqrt(Mathf.Max(0f, discriminant));
            float hitTime = (-b - sqrt) / (2f * a);
            if (hitTime < -CollisionEpsilon || hitTime > 1f + CollisionEpsilon)
            {
                return;
            }

            hitTime = Mathf.Clamp01(hitTime);
            float globalHitT = (elapsedTime + segmentDuration * hitTime) / totalDeltaTime;
            Vector2 hitPoint = Vector2.Lerp(start, end, hitTime);
            ChooseEarlierHit(SweepHit.Paddle(hitTime, globalHitT, hitPoint, motion, endpointTangentDistance), ref bestHit);
        }

        private bool TryAddEmbeddedPaddleSweepHit(
            PaddleMotionState motion,
            Vector2 start,
            Vector2 end,
            float n0,
            float n1,
            float contactDistance,
            float elapsedTime,
            float segmentDuration,
            float totalDeltaTime,
            ref SweepHit bestHit)
        {
            PaddleRuntimeState paddle = motion.Paddle;
            if (paddle == null || n0 > contactDistance || n1 >= n0 - CollisionEpsilon)
            {
                return false;
            }

            float segmentStartT = elapsedTime / totalDeltaTime;
            Vector2 paddleAtStart = motion.GetPosition(segmentStartT);
            float tangentDistance = Vector2.Dot(start - paddleAtStart, paddle.Tangent);
            if (Mathf.Abs(tangentDistance) > paddle.Length * 0.5f + CollisionSkin)
            {
                return false;
            }

            ChooseEarlierHit(SweepHit.Paddle(0f, segmentStartT, start, motion, tangentDistance), ref bestHit);
            return true;
        }

        private void TryAddBoundarySweepHit(
            BallRuntimeState ball,
            Vector2 start,
            Vector2 end,
            float boundary,
            bool horizontal,
            Vector2 inwardNormal,
            ref SweepHit bestHit)
        {
            float contactRadius = GetBallContactRadius(ball);
            bool isGoalSide = HasZoneForSide(inwardNormal);
            float contactBoundary = boundary;
            if (isGoalSide)
            {
                contactBoundary += horizontal
                    ? inwardNormal.y * contactRadius
                    : inwardNormal.x * contactRadius;
            }

            float startAxis = horizontal ? start.y : start.x;
            float endAxis = horizontal ? end.y : end.x;
            bool crosses = inwardNormal.y > 0f || inwardNormal.x > 0f
                ? startAxis >= contactBoundary && endAxis < contactBoundary
                : startAxis <= contactBoundary && endAxis > contactBoundary;
            if (!crosses)
            {
                return;
            }

            float denominator = startAxis - endAxis;
            if (Mathf.Abs(denominator) <= CollisionEpsilon)
            {
                return;
            }

            float hitTime = (startAxis - contactBoundary) / denominator;
            if (hitTime < -CollisionEpsilon || hitTime > 1f + CollisionEpsilon)
            {
                return;
            }

            hitTime = Mathf.Clamp01(hitTime);
            Vector2 hitPoint = Vector2.Lerp(start, end, hitTime);
            if (isGoalSide)
            {
                if (Arena.TryGetGoalOwner(
                        hitPoint,
                        _players.Count,
                        EffectiveRule.Map.SpawnLayoutType,
                        out int playerIndex,
                        contactRadius))
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
            BallRuntimeState ball,
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
                TryAddGoalContactSweepHit(ball, start, end, boundary, ref bestHit);
                TryAddActiveGoalWallSweepHits(ball, start, end, boundary, ref bestHit);
                return;
            }

            TryAddBoundaryWallSpanSweepHit(
                ball,
                start,
                end,
                boundary.Start,
                boundary.End,
                boundary.InwardNormal,
                true,
                true,
                ref bestHit);
        }

        private void TryAddActiveGoalWallSweepHits(
            BallRuntimeState ball,
            Vector2 start,
            Vector2 end,
            ArenaBoundarySegment boundary,
            ref SweepHit bestHit)
        {
            Vector2 edge = boundary.End - boundary.Start;
            float edgeLength = edge.magnitude;
            if (edgeLength <= CollisionEpsilon)
            {
                return;
            }

            Vector2 tangent = edge / edgeLength;
            float goalCenterDistance = Vector2.Dot(boundary.GoalCenter - boundary.Start, tangent);
            float goalStartDistance = Mathf.Clamp(goalCenterDistance - boundary.GoalHalfLength, 0f, edgeLength);
            float goalEndDistance = Mathf.Clamp(goalCenterDistance + boundary.GoalHalfLength, 0f, edgeLength);

            TryAddBoundaryWallSpanSweepHit(
                ball,
                start,
                end,
                boundary.Start,
                boundary.Start + tangent * goalStartDistance,
                boundary.InwardNormal,
                true,
                false,
                ref bestHit);
            TryAddBoundaryWallSpanSweepHit(
                ball,
                start,
                end,
                boundary.Start + tangent * goalEndDistance,
                boundary.End,
                boundary.InwardNormal,
                false,
                true,
                ref bestHit);
        }

        private void TryAddBoundaryWallSpanSweepHit(
            BallRuntimeState ball,
            Vector2 start,
            Vector2 end,
            Vector2 wallStart,
            Vector2 wallEnd,
            Vector2 inwardNormal,
            bool includeStartCap,
            bool includeEndCap,
            ref SweepHit bestHit)
        {
            Vector2 edge = wallEnd - wallStart;
            if (edge.sqrMagnitude <= CollisionEpsilon * CollisionEpsilon)
            {
                return;
            }

            TryAddBoundaryWallLineSweepHit(ball, start, end, wallStart, wallEnd, inwardNormal, ref bestHit);
            if (includeStartCap)
            {
                TryAddBoundaryWallEndpointSweepHit(ball, start, end, wallStart, ref bestHit);
            }

            if (includeEndCap)
            {
                TryAddBoundaryWallEndpointSweepHit(ball, start, end, wallEnd, ref bestHit);
            }
        }

        private void TryAddBoundaryWallLineSweepHit(
            BallRuntimeState ball,
            Vector2 start,
            Vector2 end,
            Vector2 wallStart,
            Vector2 wallEnd,
            Vector2 inwardNormal,
            ref SweepHit bestHit)
        {
            Vector2 movement = end - start;
            Vector2 edge = wallEnd - wallStart;
            float denominator = Cross(movement, edge);
            if (Mathf.Abs(denominator) <= CollisionEpsilon)
            {
                return;
            }

            float contactRadius = GetBallContactRadius(ball);
            Vector2 offset = wallStart + inwardNormal * contactRadius - start;
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
            ChooseEarlierHit(SweepHit.Wall(hitTime, hitPoint, inwardNormal), ref bestHit);
        }

        private void TryAddBoundaryWallEndpointSweepHit(
            BallRuntimeState ball,
            Vector2 start,
            Vector2 end,
            Vector2 endpoint,
            ref SweepHit bestHit)
        {
            Vector2 movement = end - start;
            Vector2 relativeStart = start - endpoint;
            float a = Vector2.Dot(movement, movement);
            if (a <= CollisionEpsilon)
            {
                return;
            }

            float b = 2f * Vector2.Dot(relativeStart, movement);
            float contactRadius = GetBallContactRadius(ball);
            float c = Vector2.Dot(relativeStart, relativeStart) - contactRadius * contactRadius;
            float discriminant = b * b - 4f * a * c;
            if (discriminant < -CollisionEpsilon)
            {
                return;
            }

            float sqrt = Mathf.Sqrt(Mathf.Max(0f, discriminant));
            float hitTime = (-b - sqrt) / (2f * a);
            if (hitTime < -CollisionEpsilon || hitTime > 1f + CollisionEpsilon)
            {
                return;
            }

            hitTime = Mathf.Clamp01(hitTime);
            Vector2 hitPoint = Vector2.Lerp(start, end, hitTime);
            Vector2 normal = hitPoint - endpoint;
            if (normal.sqrMagnitude <= CollisionEpsilon * CollisionEpsilon)
            {
                return;
            }

            normal.Normalize();
            if (Vector2.Dot(movement, normal) >= -CollisionEpsilon)
            {
                return;
            }

            ChooseEarlierHit(SweepHit.Wall(hitTime, hitPoint, normal), ref bestHit);
        }

        private void TryAddGoalContactSweepHit(
            BallRuntimeState ball,
            Vector2 start,
            Vector2 end,
            ArenaBoundarySegment boundary,
            ref SweepHit bestHit)
        {
            Vector2 movement = end - start;
            float contactRadius = GetBallContactRadius(ball);
            Vector2 contactStart = boundary.GetGoalContactStart(contactRadius);
            Vector2 contactEnd = boundary.GetGoalContactEnd(contactRadius);
            Vector2 contactEdge = contactEnd - contactStart;
            float denominator = Cross(movement, contactEdge);
            if (Mathf.Abs(denominator) <= CollisionEpsilon)
            {
                return;
            }

            Vector2 offset = contactStart - start;
            float hitTime = Cross(offset, contactEdge) / denominator;
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
            if (boundary.IsPastGoalLine(hitPoint, contactRadius))
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
                            + paddle.Normal * (paddle.Thickness + GetBallContactRadius(ball) + CollisionSkin);
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

            RecordGoalContactDiagnostic(ball, hit.PlayerIndex, "swept");
            ResolveGoalEntry(ball.BallId, zoneOwner.PlayerId, zoneOwner.Paddle != null ? zoneOwner.Paddle.Normal : Vector2.up);
        }

        private void ResolveSweptWallHit(BallRuntimeState ball, SweepHit hit)
        {
            Vector2 normal = hit.WallNormal.sqrMagnitude > 0.0001f ? hit.WallNormal.normalized : Vector2.up;
            float normalSpeed = Vector2.Dot(ball.Velocity, normal);
            Vector2 tangentVelocity = ball.Velocity - normal * normalSpeed;
            ball.Position = hit.Point + normal * (GetBallContactRadius(ball) + CollisionSkin);
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

                if (ResolvePaddleHit(ball))
                {
                    continue;
                }

                if (TryResolveGoal(ball))
                {
                    continue;
                }

                ResolveUnownedWallBounce(ball);
            }
        }

        private bool TryResolveGoal(BallRuntimeState ball)
        {
            float contactRadius = GetBallContactRadius(ball);
            if (!Arena.TryGetGoalOwner(
                    ball.Position,
                    _players.Count,
                    EffectiveRule.Map.SpawnLayoutType,
                    out int playerIndex,
                    contactRadius))
            {
                return false;
            }

            PlayerRuntimeState zoneOwner = _players[playerIndex];
            if (zoneOwner.Zone != null)
            {
                zoneOwner.Zone.LastEnteredBallId = ball.BallId;
            }

            RecordGoalContactDiagnostic(ball, playerIndex, "static");
            ResolveGoalEntry(ball.BallId, zoneOwner.PlayerId, zoneOwner.Paddle != null ? zoneOwner.Paddle.Normal : Vector2.up);
            return ball.BallState == BallState.Destroyed || ball.BallState == BallState.GoalRebound;
        }

        private void RecordGoalContactDiagnostic(BallRuntimeState ball, int playerIndex, string source)
        {
            if (ball == null)
            {
                return;
            }

            LastGoalContactBallId = ball.BallId;
            LastGoalContactPosition = ball.Position;
            string diagnostic = BuildGoalContactDiagnostic(ball, playerIndex, source);
            LastGoalContactDiagnostic = diagnostic;
            _logger?.LogInfo("Gatebreaker goal contact: {0}", diagnostic);
        }

        private string BuildGoalContactDiagnostic(BallRuntimeState ball, int playerIndex, string source)
        {
            if (Arena == null)
            {
                return string.Empty;
            }

            if (Arena.TryGetGoalSegmentForPlayer(playerIndex, out ArenaBoundarySegment segment))
            {
                Vector2 goalContactCenter = segment.GoalContactCenter;
                float signedDistance = Vector2.Dot(ball.Position - goalContactCenter, segment.InwardNormal);
                float contactRadius = GetBallContactRadius(ball);
                float edgeGap = signedDistance - contactRadius;
                float tangentDistance = Vector2.Dot(ball.Position - segment.GoalCenter, segment.Tangent);
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "source={0} ball={1} playerIndex={2} pos=({3:0.0000},{4:0.0000}) goalCenter=({5:0.0000},{6:0.0000}) goalContactCenter=({7:0.0000},{8:0.0000}) signedDistance={9:0.0000} contactRadius={10:0.0000} edgeGap={11:0.0000} tangentDistance={12:0.0000} goalHalfLength={13:0.0000}",
                    source,
                    ball.BallId,
                    playerIndex,
                    ball.Position.x,
                    ball.Position.y,
                    segment.GoalCenter.x,
                    segment.GoalCenter.y,
                    goalContactCenter.x,
                    goalContactCenter.y,
                    signedDistance,
                    contactRadius,
                    edgeGap,
                    tangentDistance,
                    segment.GoalHalfLength);
            }

            Vector2 normal = Arena.GetSideNormal(EffectiveRule.Map.SpawnLayoutType, playerIndex);
            float signedDefaultDistance = CalculateDefaultGoalSignedDistance(ball.Position, normal);
            float defaultContactRadius = GetBallContactRadius(ball);
            float defaultEdgeGap = signedDefaultDistance - defaultContactRadius;
            return string.Format(
                CultureInfo.InvariantCulture,
                "source={0} ball={1} playerIndex={2} pos=({3:0.0000},{4:0.0000}) signedDistance={5:0.0000} contactRadius={6:0.0000} edgeGap={7:0.0000} normal=({8:0.0000},{9:0.0000})",
                source,
                ball.BallId,
                playerIndex,
                ball.Position.x,
                ball.Position.y,
                signedDefaultDistance,
                defaultContactRadius,
                defaultEdgeGap,
                normal.x,
                normal.y);
        }

        private float CalculateDefaultGoalSignedDistance(Vector2 position, Vector2 normal)
        {
            if (Mathf.Abs(normal.y) > 0.5f)
            {
                return normal.y > 0f ? position.y + Arena.HalfHeight : Arena.HalfHeight - position.y;
            }

            return normal.x > 0f ? position.x + Arena.HalfWidth : Arena.HalfWidth - position.x;
        }

        private bool ResolvePaddleHit(BallRuntimeState ball)
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
                float contactDistance = paddle.Thickness + GetBallContactRadius(ball);
                if (normalDistance < -contactDistance || normalDistance > contactDistance)
                {
                    continue;
                }

                float tangentDistance = Vector2.Dot(relative, paddle.Tangent);
                if (Mathf.Abs(tangentDistance) > paddle.Length * 0.5f)
                {
                    if (!TryResolvePaddleEndpointHit(ball, paddle, tangentDistance))
                    {
                        continue;
                    }

                    return true;
                }

                ResolvePaddleHit(ball, paddle, tangentDistance, GetNormalizedPaddleVelocity(paddle));
                return true;
            }

            return false;
        }

        private bool TryResolvePaddleEndpointHit(BallRuntimeState ball, PaddleRuntimeState paddle, float tangentDistance)
        {
            float halfLength = paddle.Length * 0.5f;
            float endpointTangentDistance = tangentDistance < 0f ? -halfLength : halfLength;
            Vector2 endpoint = paddle.Position
                               + paddle.Normal * paddle.Thickness
                               + paddle.Tangent * endpointTangentDistance;
            Vector2 relativeEndpoint = ball.Position - endpoint;
            float endpointRadius = GetPaddleEndpointCollisionRadius(ball, paddle);
            if (relativeEndpoint.sqrMagnitude > endpointRadius * endpointRadius)
            {
                return false;
            }

            ResolvePaddleHit(ball, paddle, endpointTangentDistance, GetNormalizedPaddleVelocity(paddle));
            return true;
        }

        private float GetPaddleEndpointCollisionRadius(BallRuntimeState ball, PaddleRuntimeState paddle)
        {
            float paddleThickness = paddle != null ? Mathf.Max(0f, paddle.Thickness) : 0f;
            return Mathf.Max(0.01f, GetBallContactRadius(ball) + paddleThickness);
        }

        private void ResolvePaddleHit(
            BallRuntimeState ball,
            PaddleRuntimeState paddle,
            float tangentDistance,
            float normalizedPaddleVelocity)
        {
            float halfLength = Mathf.Max(0.001f, paddle.Length * 0.5f);
            float clampedTangentDistance = Mathf.Clamp(tangentDistance, -halfLength, halfLength);
            float hitOffset = clampedTangentDistance / halfLength;
            ball.Position = paddle.Position
                            + paddle.Tangent * clampedTangentDistance
                            + paddle.Normal * (paddle.Thickness + GetBallContactRadius(ball) + CollisionSkin);
            ball.Velocity = _paddleBounceCalculator.CalculateBounce(
                ball.Velocity,
                hitOffset,
                BallRule,
                paddle.Normal,
                paddle.Tangent,
                BounceTuning,
                normalizedPaddleVelocity);
            _ballSimulation.ClampSpeed(ball, BallRule);
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
            float contactRadius = GetBallContactRadius(ball);
            bool bounced = false;

            if (!HasZoneForSide(Vector2.up) && position.y < -Arena.HalfHeight)
            {
                position.y = -Arena.HalfHeight + contactRadius;
                velocity.y = Mathf.Abs(velocity.y) * BallRule.WallBounceFactor;
                bounced = true;
            }
            else if (!HasZoneForSide(Vector2.down) && position.y > Arena.HalfHeight)
            {
                position.y = Arena.HalfHeight - contactRadius;
                velocity.y = -Mathf.Abs(velocity.y) * BallRule.WallBounceFactor;
                bounced = true;
            }

            if (!HasZoneForSide(Vector2.right) && position.x < -Arena.HalfWidth)
            {
                position.x = -Arena.HalfWidth + contactRadius;
                velocity.x = Mathf.Abs(velocity.x) * BallRule.WallBounceFactor;
                bounced = true;
            }
            else if (!HasZoneForSide(Vector2.left) && position.x > Arena.HalfWidth)
            {
                position.x = Arena.HalfWidth - contactRadius;
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
            float contactRadius = GetBallContactRadius(ball);
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
                if (distanceInside >= contactRadius)
                {
                    continue;
                }

                position += segment.InwardNormal * (contactRadius - distanceInside);
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
                ? player.Paddle.Position
                : Vector2.zero;
        }

        private Vector2 GetServeDirection(PlayerRuntimeState player, Vector2 aimDirection)
        {
            if (player != null && EffectiveRule.Mode.AllowAimServe && aimDirection.sqrMagnitude > 0.0001f)
            {
                return aimDirection.normalized;
            }

            return player != null && player.Paddle != null
                ? player.Paddle.Normal.normalized
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
            IReadOnlyList<int> topPlayers = _scoreSystem.GetTopRankedPlayerIds(_players, false);
            if (Phase == MatchPhase.Playing && topPlayers.Count == 1)
            {
                EndWithWinner(topPlayers[0]);
                return;
            }

            if (Phase == MatchPhase.Playing &&
                EffectiveRule.Mode.EnableOvertime &&
                EffectiveRule.Mode.OvertimeRuleType == OvertimeRuleType.SuddenDeath)
            {
                _overtimeEligiblePlayerIds.Clear();
                _overtimeEligiblePlayerIds.AddRange(topPlayers);
                Phase = MatchPhase.Overtime;
                RemainingTime = EffectiveRule.Mode.OvertimeDuration;
                return;
            }

            IReadOnlyList<int> stableTopPlayers = _scoreSystem.GetTopRankedPlayerIds(_players, true);
            if (stableTopPlayers.Count > 0)
            {
                EndWithWinner(stableTopPlayers[0]);
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

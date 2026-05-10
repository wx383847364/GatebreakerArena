using System;
using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.GatebreakerArena.Ball;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Mode;
using App.HotUpdate.GatebreakerArena.Serve;
using App.HotUpdate.GatebreakerArena.Zone;
using App.Shared.Contracts;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Match
{
    public sealed class GatebreakerMatchRuntime : ITickable
    {
        private readonly GatebreakerModeCatalog _modeCatalog;
        private readonly BallSimulationSystem _ballSimulation;
        private readonly ServeResourceSystem _serveResourceSystem;
        private readonly GoalJudgeSystem _goalJudgeSystem;
        private readonly ScoreSystem _scoreSystem;
        private readonly IAppLogger _logger;
        private readonly List<PlayerRuntimeState> _players = new List<PlayerRuntimeState>();
        private readonly List<BallRuntimeState> _balls = new List<BallRuntimeState>();
        private readonly List<int> _overtimeEligiblePlayerIds = new List<int>();
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
            _logger = logger;
            Phase = MatchPhase.Waiting;
        }

        public MatchPhase Phase { get; private set; }
        public EffectiveMatchRule EffectiveRule { get; private set; }
        public BallRuleDefinition BallRule { get; private set; }
        public float RemainingTime { get; private set; }
        public IReadOnlyList<PlayerRuntimeState> Players => _players;
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
            _balls.Clear();
            _nextBallId = 1;

            AddPlayer(1, 1, true, false);
            for (int i = 0; i < aiCount; i++)
            {
                int playerId = i + 2;
                AddPlayer(playerId, playerId, false, true);
            }

            for (int i = 0; i < EffectiveRule.InitialBallsInMatch; i++)
            {
                PlayerRuntimeState owner = _players[i % _players.Count];
                SpawnBallForPlayer(owner, "Initial", Vector2.zero, InitialDirectionFor(owner.PlayerId), true);
            }

            _logger?.LogInfo("GatebreakerMatchRuntime: 本地原型开局完成。players={0}, balls={1}", _players.Count, _balls.Count);
        }

        public void Tick(float deltaTime)
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

            _ballSimulation.Tick(_balls, safeDelta);
            if (RemainingTime <= 0f)
            {
                HandleTimeExpired();
            }
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

            SpawnBallForPlayer(player, "Serve", Vector2.zero, InitialDirectionFor(playerId), false);
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

        private void AddPlayer(int playerId, int teamId, bool isLocalPlayer, bool isAi)
        {
            _players.Add(new PlayerRuntimeState
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
            });
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

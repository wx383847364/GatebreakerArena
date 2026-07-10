using System;
using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Match;
using App.HotUpdate.GatebreakerArena.Mode;

namespace App.HotUpdate.GatebreakerArena.UI
{
    public sealed class GatebreakerArenaHudPresenter
    {
        private readonly GatebreakerMatchRuntime _runtime;

        public GatebreakerArenaHudPresenter(GatebreakerMatchRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public GatebreakerHudSnapshot BuildSnapshot(int localPlayerId)
        {
            PlayerRuntimeState localPlayer = _runtime.FindPlayer(localPlayerId);
            ScoreboardSnapshot scoreboard = _runtime.CreateScoreboardSnapshot();
            HeroHudSnapshot hero = BuildHeroSnapshot(localPlayer?.Hero);
            return new GatebreakerHudSnapshot
            {
                Phase = scoreboard.Phase,
                RemainingTime = scoreboard.RemainingTime,
                PlayerScores = scoreboard.PlayerScores,
                LocalPlayerId = localPlayerId,
                HasWinner = scoreboard.HasWinner,
                WinnerPlayerId = scoreboard.WinnerPlayerId,
                CurrentServeAmmo = localPlayer?.ServeResource?.CurrentServeAmmo ?? 0,
                MaxServeAmmo = localPlayer?.ServeResource?.MaxServeAmmo ?? 0,
                OwnedBallsInField = localPlayer?.ServeResource?.OwnedBallsInField ?? 0,
                MaxOwnedBallsInField = localPlayer?.ServeResource?.MaxOwnedBallsInField ?? 0,
                ServeCooldownRemaining = localPlayer?.ServeResource?.ServeCooldownRemaining ?? 0f,
                ServeBlockReason = localPlayer?.ServeResource?.LastBlockReason ?? ServeBlockReason.PlayerDisabled,
                IsOvertime = scoreboard.Phase == MatchPhase.Overtime,
                HasDanger = _runtime.Balls.Any(ball => ball.OwnerPlayerId != localPlayerId),
                Hero = hero,
                CountdownReveal = scoreboard.Phase == MatchPhase.Countdown ? hero : null,
            };
        }

        private HeroHudSnapshot BuildHeroSnapshot(HeroRuntimeState heroState)
        {
            if (heroState == null)
            {
                return HeroHudSnapshot.Empty;
            }

            string displayName = heroState.HeroId ?? string.Empty;
            if (!string.IsNullOrEmpty(heroState.HeroId) &&
                _runtime.ModeCatalog.AllHeroes.TryGetValue(heroState.HeroId, out HeroDefinition heroDefinition) &&
                !string.IsNullOrEmpty(heroDefinition?.DisplayName))
            {
                displayName = heroDefinition.DisplayName;
            }

            return new HeroHudSnapshot(
                heroState.HeroId,
                displayName,
                CopyChipIds(heroState.ActiveChipIds),
                CopyPaths(heroState.PathStates),
                heroState.AbilityCooldownRemainingFrames,
                CopyStatuses(heroState.TemporaryStatuses));
        }

        private static IReadOnlyList<string> CopyChipIds(IReadOnlyList<string> chipIds)
        {
            return chipIds == null ? Array.Empty<string>() : chipIds.ToArray();
        }

        private static IReadOnlyList<HeroPathHudSnapshot> CopyPaths(IReadOnlyList<HeroPathRuntimeState> paths)
        {
            if (paths == null || paths.Count == 0)
            {
                return Array.Empty<HeroPathHudSnapshot>();
            }

            return paths
                .Where(path => path != null)
                .Select(path => new HeroPathHudSnapshot(path.PathId, path.Level))
                .ToArray();
        }

        private static IReadOnlyList<HeroTemporaryStatusHudSnapshot> CopyStatuses(
            IReadOnlyList<HeroTemporaryStatusState> statuses)
        {
            if (statuses == null || statuses.Count == 0)
            {
                return Array.Empty<HeroTemporaryStatusHudSnapshot>();
            }

            return statuses
                .Where(status => status != null)
                .Select(status => new HeroTemporaryStatusHudSnapshot(
                    status.StatusType,
                    status.RemainingFrames,
                    status.Magnitude))
                .ToArray();
        }
    }

    public sealed class GatebreakerHudSnapshot
    {
        public MatchPhase Phase { get; set; }
        public float RemainingTime { get; set; }
        public System.Collections.Generic.IReadOnlyList<PlayerScoreSnapshot> PlayerScores { get; set; }
        public int LocalPlayerId { get; set; }
        public bool HasWinner { get; set; }
        public int WinnerPlayerId { get; set; }
        public int CurrentServeAmmo { get; set; }
        public int MaxServeAmmo { get; set; }
        public int OwnedBallsInField { get; set; }
        public int MaxOwnedBallsInField { get; set; }
        public float ServeCooldownRemaining { get; set; }
        public ServeBlockReason ServeBlockReason { get; set; }
        public bool IsOvertime { get; set; }
        public bool HasDanger { get; set; }
        public HeroHudSnapshot Hero { get; set; } = HeroHudSnapshot.Empty;
        // Countdown-only reveal payload. The presenter deliberately does not decide
        // when countdown ends or which paths are active.
        public HeroHudSnapshot CountdownReveal { get; set; }
    }

    public sealed class HeroHudSnapshot
    {
        public static readonly HeroHudSnapshot Empty = new HeroHudSnapshot(
            string.Empty,
            string.Empty,
            Array.Empty<string>(),
            Array.Empty<HeroPathHudSnapshot>(),
            0,
            Array.Empty<HeroTemporaryStatusHudSnapshot>());

        public HeroHudSnapshot(
            string heroId,
            string displayName,
            IReadOnlyList<string> activeChipIds,
            IReadOnlyList<HeroPathHudSnapshot> pathLevels,
            int abilityCooldownRemainingFrames,
            IReadOnlyList<HeroTemporaryStatusHudSnapshot> temporaryStatuses)
        {
            HeroId = heroId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            ActiveChipIds = activeChipIds ?? Array.Empty<string>();
            PathLevels = pathLevels ?? Array.Empty<HeroPathHudSnapshot>();
            AbilityCooldownRemainingFrames = abilityCooldownRemainingFrames;
            TemporaryStatuses = temporaryStatuses ?? Array.Empty<HeroTemporaryStatusHudSnapshot>();
        }

        public string HeroId { get; }
        public string DisplayName { get; }
        public IReadOnlyList<string> ActiveChipIds { get; }
        public IReadOnlyList<HeroPathHudSnapshot> PathLevels { get; }
        public int AbilityCooldownRemainingFrames { get; }
        public IReadOnlyList<HeroTemporaryStatusHudSnapshot> TemporaryStatuses { get; }
    }

    public sealed class HeroPathHudSnapshot
    {
        public HeroPathHudSnapshot(string pathId, int level)
        {
            PathId = pathId ?? string.Empty;
            Level = level;
        }

        public string PathId { get; }
        public int Level { get; }
    }

    public sealed class HeroTemporaryStatusHudSnapshot
    {
        public HeroTemporaryStatusHudSnapshot(HeroTemporaryStatusType statusType, int remainingFrames, float magnitude)
        {
            StatusType = statusType;
            RemainingFrames = remainingFrames;
            Magnitude = magnitude;
        }

        public HeroTemporaryStatusType StatusType { get; }
        public int RemainingFrames { get; }
        public float Magnitude { get; }
    }
}

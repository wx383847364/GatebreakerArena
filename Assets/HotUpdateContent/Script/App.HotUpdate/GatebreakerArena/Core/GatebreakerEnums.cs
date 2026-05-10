namespace App.HotUpdate.GatebreakerArena.Core
{
    public enum MatchPhase
    {
        Waiting,
        Countdown,
        Playing,
        GoalPause,
        Overtime,
        Result,
    }

    public enum BallState
    {
        Spawned,
        Flying,
        GoalRebound,
        ScoredOut,
        Destroyed,
    }

    public enum ServeBlockReason
    {
        None,
        PlayerDisabled,
        CoolingDown,
        NoAmmo,
        OwnedBallLimit,
        MatchBallLimit,
    }

    public enum ScoreRuleType
    {
        AddScore,
        TeamScore,
        LoseLife,
        Mixed,
    }

    public enum OvertimeRuleType
    {
        SuddenDeath,
        TimedScore,
        Disabled,
    }

    public enum SpawnLayoutType
    {
        FourSide,
        Ring,
        DualFront,
    }
}

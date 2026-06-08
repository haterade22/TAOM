namespace TAOM.Features.Music;

public static class MusicRotationPolicy
{
    public readonly struct RotationSnapshot
    {
        public RotationSnapshot(
            bool musicEnabled,
            bool enableWorldRotation,
            bool enableTownRotation,
            bool enableBattleRotation,
            float worldRotateIntervalSeconds,
            float townRotateIntervalSeconds,
            float battleRotateIntervalSeconds,
            float characterCreationRotateIntervalSeconds)
        {
            MusicEnabled = musicEnabled;
            EnableWorldRotation = enableWorldRotation;
            EnableTownRotation = enableTownRotation;
            EnableBattleRotation = enableBattleRotation;
            WorldRotateIntervalSeconds = ClampNonNegative(worldRotateIntervalSeconds);
            TownRotateIntervalSeconds = ClampNonNegative(townRotateIntervalSeconds);
            BattleRotateIntervalSeconds = ClampNonNegative(battleRotateIntervalSeconds);
            CharacterCreationRotateIntervalSeconds = ClampNonNegative(characterCreationRotateIntervalSeconds);
        }

        public bool MusicEnabled { get; }

        public bool EnableWorldRotation { get; }

        public bool EnableTownRotation { get; }

        public bool EnableBattleRotation { get; }

        public float WorldRotateIntervalSeconds { get; }

        public float TownRotateIntervalSeconds { get; }

        public float BattleRotateIntervalSeconds { get; }

        public float CharacterCreationRotateIntervalSeconds { get; }
    }

    public readonly struct RotationDecision
    {
        public RotationDecision(bool rotate, float nextRotateAt)
        {
            Rotate = rotate;
            NextRotateAt = nextRotateAt;
        }

        public bool Rotate { get; }

        public float NextRotateAt { get; }
    }

    public static RotationDecision ShouldRotate(
        RotationSnapshot snapshot,
        MusicBucket bucket,
        float timer,
        float nextRotateAt)
    {
        if (!snapshot.MusicEnabled)
            return new RotationDecision(false, nextRotateAt);

        var interval = GetInterval(bucket, snapshot);
        var enabled = IsEnabled(bucket, snapshot);
        if (!enabled || interval <= 0f)
            return new RotationDecision(false, float.MaxValue);

        if (nextRotateAt <= 0f)
            nextRotateAt = timer + interval;

        if (timer < nextRotateAt)
            return new RotationDecision(false, nextRotateAt);

        return new RotationDecision(true, timer + interval);
    }

    private static bool IsEnabled(MusicBucket bucket, RotationSnapshot snapshot)
    {
        switch (bucket)
        {
            case MusicBucket.World:
                return snapshot.EnableWorldRotation;
            case MusicBucket.Town:
            case MusicBucket.Tavern:
                return snapshot.EnableTownRotation;
            case MusicBucket.Battle:
            case MusicBucket.Siege:
                return snapshot.EnableBattleRotation;
            case MusicBucket.CharacterCreation:
                return snapshot.CharacterCreationRotateIntervalSeconds > 0f;
            default:
                return false;
        }
    }

    private static float GetInterval(MusicBucket bucket, RotationSnapshot snapshot)
    {
        switch (bucket)
        {
            case MusicBucket.World:
                return snapshot.WorldRotateIntervalSeconds;
            case MusicBucket.Town:
            case MusicBucket.Tavern:
                return snapshot.TownRotateIntervalSeconds;
            case MusicBucket.Battle:
            case MusicBucket.Siege:
                return snapshot.BattleRotateIntervalSeconds;
            case MusicBucket.CharacterCreation:
                return snapshot.CharacterCreationRotateIntervalSeconds;
            default:
                return 0f;
        }
    }

    private static float ClampNonNegative(float value)
    {
        return value < 0f ? 0f : value;
    }
}

namespace TAOM.Features.BattleLoadDiagnostics;

internal enum FreezeStandDown { None, PrecompileWalk, ExitSampler, MissionFrame }

internal enum FreezeSampleAction { None, LogWithoutStack, LogWithStack }

/// <summary>
/// When a silent main thread earns a [Freeze] line and whether it is safe to photograph it. The heap
/// gate keeps a suspension away from a thread busy in managed code such as a save or a load; its
/// numbers come from player log E4: 2 gen0 collections across a 69 s blocked wait (0.03/s), 7,098
/// across 1,625 s of normal play (4.4/s). Pure: no engine types.
/// </summary>
internal static class FreezeSamplePolicy
{
    internal static readonly double[] ThresholdsSeconds = { 10d, 20d, 40d, 80d };

    public const int HeapMovingMinCollections = 3;
    public const double HeapMovingMinPerSecond = 0.3;

    public static bool CrossesThreshold(double silentSeconds, int samplesTaken)
        => samplesTaken < ThresholdsSeconds.Length && silentSeconds >= ThresholdsSeconds[samplesTaken];

    public static bool HeapMoving(int gen0Delta, double windowSeconds)
        => windowSeconds > 0 && gen0Delta >= HeapMovingMinCollections && gen0Delta >= HeapMovingMinPerSecond * windowSeconds;

    public static FreezeSampleAction Decide(int thresholdIndex, FreezeStandDown standDown, bool heapMoving, bool heapLineLogged)
    {
        if (standDown == FreezeStandDown.PrecompileWalk || standDown == FreezeStandDown.ExitSampler) return FreezeSampleAction.None;
        if (standDown == FreezeStandDown.MissionFrame) return FreezeSampleAction.LogWithoutStack;
        if (heapMoving) return thresholdIndex >= 2 && !heapLineLogged ? FreezeSampleAction.LogWithoutStack : FreezeSampleAction.None;
        return FreezeSampleAction.LogWithStack;
    }
}

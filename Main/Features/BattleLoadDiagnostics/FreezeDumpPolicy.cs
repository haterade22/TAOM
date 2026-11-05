namespace TAOM.Features.BattleLoadDiagnostics;

/// <summary>
/// When the opt-in freeze dump may be written: at the 80 s threshold, once per process, with the heap
/// flat and the engine log showing no recent compile. A mission frame in flight does not stop it: a
/// frozen mission frame is a case the dump exists for. Pure: no engine types.
/// </summary>
internal static class FreezeDumpPolicy
{
    private const int EightySecondIndex = 3;
    private const double CompilingWithinSeconds = 30d;

    public static bool ShouldWrite(bool enabled, bool alreadyWritten, int thresholdIndex, FreezeStandDown standDown,
        bool heapMoving, bool rglAvailable, double? secondsSinceLastCompile)
    {
        if (!enabled || alreadyWritten || thresholdIndex != EightySecondIndex) return false;
        if (standDown == FreezeStandDown.PrecompileWalk || standDown == FreezeStandDown.ExitSampler) return false;
        if (heapMoving) return false;
        return !(rglAvailable && secondsSinceLastCompile < CompilingWithinSeconds);
    }
}

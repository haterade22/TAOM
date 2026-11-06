using System;
using TAOM.Core.Validation;

namespace TAOM.Features.CastleRecruitment;

/// <summary>
/// The campaign day the #918 check keys its daily line by (#1072). The same floor as 1.5.x's War Chronicle
/// snapshot adapter, which is not on this line.
/// </summary>
internal static class CastleRecruitDay
{
    /// <summary>
    /// Whole days, floored; false (day 0) for null, a non-finite or negative value, or one at or past 2^31.
    /// The check runs on a double, where int.MaxValue is exact: the float bound would round up to 2^31,
    /// which the cast would turn into int.MinValue.
    /// </summary>
    internal static bool TryFloorElapsedDays(float? days, out int day)
    {
        day = 0;
        if (!days.HasValue || !FiniteFloatValidator.IsFiniteInRange((double)days.Value, 0d, int.MaxValue))
            return false;
        day = (int)Math.Floor(days.Value);
        return true;
    }
}

using System;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat.Diagnostics;

/// <summary>
/// The verdict of one upgrade order (#1161), key <c>u&lt;N&gt;</c> for the run's Nth order. It passes when the field replaced every soldier of the old troop it found, up to the order's count, and
/// every new soldier read back as the target troop; a soldier who died between the buy and the tick is only <c>found</c> short of <c>ordered</c>, and his gold is paid back. An order that found
/// nobody is a skip (<c>reason=none-found</c>). Every word of the detail is <c>name=value</c> (the sanitiser rule in <see cref="LongDefeatPostsChecks"/>).
/// </summary>
internal static class LongDefeatUpgradeChecks
{
    internal const string Id = "taom.long-defeat.upgrade";

    internal static CheckVerdict Replaced(int beforeWave, UpgradeOrder order, LdUpgradeReport report)
    {
        var detail = $"w={beforeWave} from={order.Step.FromTroop} to={order.Step.ToTroop} ordered={order.Count} found={report.Found} replaced={report.Replaced} read={report.ReadBack}";
        if (report.Problem != null)
            return LongDefeatChecks.Verdict(false, detail + " problem=" + string.Join("_", report.Problem.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));

        var expected = Math.Min(order.Count, report.Found);
        if (expected <= 0)
            return new CheckVerdict(CheckResult.Skip, detail + " reason=none-found");
        return LongDefeatChecks.Verdict(report.Replaced == expected && report.ReadBack == report.Replaced, detail);
    }
}

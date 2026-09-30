using System.Linq;

namespace TAOM.Features.Elephant;

/// <summary>
/// Which elephant harness gets the howdah platform, and with it the crew (#627): the three howdahs in
/// <see cref="ElephantConfig.HowdahHarnessStringIds"/>, which share one deck placement (Mike 2026-09-29). The plain
/// armours carry no howdah, so crew on them would stand on an invisible deck. Pure, so HowdahHarnessTests pins it.
/// </summary>
public static class HowdahHarness
{
    public static bool GetsPlatform(string? harnessId) =>
        harnessId != null && ElephantConfig.HowdahHarnessStringIds.Contains(harnessId);
}

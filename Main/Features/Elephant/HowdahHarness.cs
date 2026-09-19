namespace TAOM.Features.Elephant;

/// <summary>
/// Which elephant harness gets the howdah platform and which also gets its crew (#627, Mike 2026-09-19). The howdah
/// harness (<see cref="ElephantConfig.HowdahHarnessStringId"/>, the elite howdah mesh) gets both. The plain armour
/// (<see cref="ElephantConfig.HarnessStringId"/>) keeps the crewless platform it has had since June: crew on it would
/// stand on an invisible deck over a back with no howdah. Pure, so HowdahHarnessTests pins it.
/// </summary>
public static class HowdahHarness
{
    public static bool GetsPlatform(string? harnessId) =>
        harnessId == ElephantConfig.HowdahHarnessStringId || harnessId == ElephantConfig.HarnessStringId;

    public static bool CarriesCrew(string? harnessId) => harnessId == ElephantConfig.HowdahHarnessStringId;
}

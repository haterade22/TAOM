using TAOM.Features.LongDefeat.Hooks;
using TAOM.Features.SignatureStrikes.Hooks;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.HeroAbilities.Hooks;

/// <summary>
/// The abilities work only in a Long Defeat mission (#1359): one holding LongDefeatMissionLogic and also an eligible
/// combat mission (<c>SignatureMissionGate.IsEligible</c>). One marker is enough: the Long Defeat launcher adds its
/// logic (LongDefeatMissionLauncher.cs:125), and so does the story launcher, which builds
/// <c>new LongDefeatMissionLogic(plan, session, ...)</c> beside StoryMissionLogic (StoryMissionLauncher.cs:107). A
/// separate StoryMissionLogic arm could never change the answer.
/// </summary>
internal static class HeroMissionGate
{
    internal static bool IsHeroMission(bool combatEligible, bool holdsLongDefeat) =>
        combatEligible && holdsLongDefeat;

    internal static (bool Combat, bool LongDefeat) Read(Mission? mission)
    {
        if (mission == null)
            return (false, false);
        return (SignatureMissionGate.IsEligible(mission),
            mission.GetMissionBehavior<LongDefeatMissionLogic>() != null);
    }
}

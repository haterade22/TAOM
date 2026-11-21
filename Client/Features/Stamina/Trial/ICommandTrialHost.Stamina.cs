using TAOM.Features.Stamina.Domain;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

internal partial interface ICommandTrialHost
{
    /// <summary>
    /// The stamina layer now: its gate, how many of the soldiers on the field stamina tracks, whether it tracks the hero, the refresh queue, the strips and the run threshold.
    /// It reads the cells and the main agent: call it after the in-mission-tick marker.
    /// </summary>
    StaminaCensus ReadStamina();

    /// <summary>
    /// Deals the soldiers of one side into groups in turn: the player team's infantry on foot in two groups (<paramref name="enemies"/> false), or every enemy on foot in three.
    /// A soldier counts when his slot is his, he is active, he holds a stamina cell and he is not running away. Returns the size of the smallest group.
    /// It resets what each group has counted. It reads agents: call it after the in-mission-tick marker.
    /// </summary>
    int ChooseStaminaGroups(bool enemies);

    /// <summary>
    /// One read of a chosen group: the soldiers still in it, their speed, locks, attack chance and defensiveness, and the releases and defends counted since the groups were chosen.
    /// It reads agents: call it after the in-mission-tick marker.
    /// </summary>
    StaminaGroupSample SampleStaminaGroup(int group);

    /// <summary>
    /// Forces a band on the soldiers of a chosen group, with the guards of the stamina console command, and returns how many it forced.
    /// It only sets each cell's forced band; the stamina tick makes every engine write. It reads agents: call it after the in-mission-tick marker.
    /// </summary>
    int ForceStaminaGroup(int group, StaminaBand band);

    /// <summary>
    /// Gives every soldier back to the stamina rules: each cell's forced band is cleared. It reads no agent, so the failure path may call it from the screen tick.
    /// Returns how many forced cells it cleared, 0 when stamina has no runtime.
    /// </summary>
    int StaminaReleaseForced();

    /// <summary>The soldiers, riders included, who are alive and on the player's side (<paramref name="enemies"/> false) or on the other side. It reads agents: call it after the in-mission-tick marker.</summary>
    int CountStaminaSoldiers(bool enemies);
}

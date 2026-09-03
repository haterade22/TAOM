using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;

namespace TAOM.Features.PlayerSwitcher;

/// <summary>
/// Walks the narrative menu chain straight to the career menu when the player has already picked a
/// lord to take over.
/// </summary>
/// <remarks>
/// The six backstory menus (parent, childhood, education, youth, adulthood, age selection) grant
/// skills, attributes and traits to the character-creation hero. When a lord is selected that hero
/// is deleted at finalize and only the career is carried across, so those answers are discarded
/// either way. The career menu is the one genuine choice left, which is why the walk stops there
/// rather than running to the end of the stage.
///
/// Selecting before advancing is mandatory, not tidiness: TrySwitchToNextMenu reads
/// SelectedOptions[CurrentMenu] and would throw KeyNotFoundException on a menu nothing was chosen
/// for. Driving the same public transition a real click drives also leaves SelectedOptions fully
/// populated, so the review stage and the trait XP pass see the shape they expect.
///
/// Every failure is soft. Aborting mid-chain leaves the player in the ordinary backstory flow,
/// which is exactly the pre-fast-path experience and never a broken one.
/// </remarks>
public class NarrativeCareerFastPathService : INarrativeCareerFastPathService
{
    /// <summary>
    /// Bound on the walk. The real chain is six hops; the cap only exists so a modded or drifted
    /// chain that never reaches the career menu cannot spin.
    /// </summary>
    public const int MaxHops = 10;

    private readonly IPlayerSwitchSession _session;
    private readonly IModLogger _logger;

    public NarrativeCareerFastPathService(IPlayerSwitchSession session, IModLogger logger)
    {
        _session = session;
        _logger = logger;
    }

    public void SkipToCareerMenu(INarrativeStageAdapter stage)
    {
        if (stage == null)
            return;

        // The whole gate. An ordinary character creation never enters the walk.
        if (!_session.HasSelection)
            return;

        try
        {
            for (var hop = 0; hop < MaxHops; hop++)
            {
                if (stage.CurrentMenuId == CareerMenuService.CareerMenuId)
                {
                    LogArrival(hop);
                    return;
                }

                if (!stage.SelectFirstSuitableOption())
                {
                    _logger.LogWarning(
                        $"Player Switcher: '{stage.CurrentMenuId}' offered no option, so the backstory " +
                        "questions stay in place for this culture.");
                    return;
                }

                if (!stage.TryAdvance())
                {
                    _logger.LogWarning(
                        $"Player Switcher: could not advance past '{stage.CurrentMenuId}', so the " +
                        "backstory questions stay in place.");
                    return;
                }
            }

            // Reachable when the chain is exactly MaxHops long.
            if (stage.CurrentMenuId == CareerMenuService.CareerMenuId)
            {
                LogArrival(MaxHops);
                return;
            }

            _logger.LogWarning(
                $"Player Switcher: gave up looking for the career menu after {MaxHops} menus, " +
                $"stopping at '{stage.CurrentMenuId}'.");
        }
        catch (Exception ex)
        {
            // A broken skip must never stop a player creating a character.
            _logger.LogError($"Player Switcher: could not skip to the career menu: {ex}");
        }
    }

    private void LogArrival(int hops)
    {
        if (hops > 0)
            _logger.LogInfo($"Player Switcher: skipped {hops} backstory menus straight to the career choice");
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// The armoury at the director (phase4-plan.md Slice 5, spike S11): a gear set the run bought is put on the lord's own live body on the tick after the buy, the bot's errand to the armoury is walked, teleported and released
/// through the lord adapter, and the bot's four place actions are dispatched. Every adapter call is made from <c>Apply</c> or the bot's turn in <c>Tick</c>, on the main thread. A director with no lord adapter serves
/// neither and says so once.
/// </summary>
internal sealed partial class LongDefeatDirector
{
    private bool _errandOpen;
    private readonly HashSet<string> _placeRefusals = new();

    /// <summary>
    /// Puts the lord's merged kit on his own body (<c>swap</c> line). The run never hands a swap out on a tick that spawns, raises or remounts the lord: a set is bought between waves only, and a handed swap
    /// holds the wave's start one tick. The set and the way the buyer came are read from the last gear purchase of the run.
    /// </summary>
    private void SwapLordGear(LdLoadout gear)
    {
        var sets = _run.FortSummary().Purchases.Where(p => p.Kind == PurchaseKind.GearSet).ToList();
        var last = sets.LastOrDefault();
        var set = last?.Id ?? "none";
        if (_lord == null)
        {
            WarnNoLordAdapter("the gear swap");
            return;
        }

        var swap = _lord.SwapGear(gear);
        var reach = (last?.Reach ?? ErrandReach.Person).ToString().ToLowerInvariant();
        _logger.LogInfo($"{Tag}[Armoury] swap variant={swap.Variant} set={set} sets={sets.Count} reach={reach} done={Bit(swap.Done)} before={ArmourText(swap.Before)} after={ArmourText(swap.After)} sums={ArmourText(swap.Sums)} "
            + $"enc={F1(swap.Before.Encumbrance)}->{F1(swap.After.Encumbrance)} model_enc={F1(swap.Sums.Encumbrance)} health={F1(swap.Health)}/{F1(swap.HealthLimit)}->{F1(swap.HealthAfter)}/{F1(swap.HealthLimitAfter)} "
            + $"mounted={Bit(swap.Mounted)} mount_after={Bit(swap.MountAfter)} wield={(swap.Wielded.Length == 0 ? "none" : swap.Wielded)} kit_wrong={swap.KitWrong}"
            + (swap.Problem.Length == 0 ? "" : $" problem={swap.Problem}"));
    }

    private static string Bit(bool value) => value ? "1" : "0";

    private static string ArmourText(LdArmour a) => $"{F1(a.Head)}/{F1(a.Torso)}/{F1(a.Legs)}/{F1(a.Arms)}";

    /// <summary>The five driven armour values as the rise's line prints them: the four sums and the weight.</summary>
    private static string ArmourReadText(LdArmour a) => $"{ArmourText(a)}/{F1(a.Encumbrance)}";

    /// <summary>Serves the run's errand order: the walk's start, the teleport once the walk ran out of time, and the release.</summary>
    private void ServeErrand(ErrandOrder order)
    {
        if (_lord == null)
        {
            WarnNoLordAdapter("the bot's errand");
            return;
        }

        var place = order.Place;
        switch (order.Step)
        {
            case ErrandStep.Start:
            {
                var move = _lord.StartErrand(order.At!);
                _errandOpen = move.Done;
                _logger.LogInfo($"{Tag}[Errand] start place={place} at={PointText(order.At!.Point)} done={Bit(move.Done)} mounted={Bit(move.Mounted)} assignable={Bit(move.Assignable)} using={Bit(move.Using)} scripted={Bit(move.Scripted)} dist={F1(move.Distance)}"
                    + ProblemText(move));
                break;
            }

            case ErrandStep.Teleport:
            {
                var move = _lord.TeleportErrand(order.At!);
                _logger.LogInfo($"{Tag}[Errand] teleport place={place} done={Bit(move.Done)} mounted={Bit(move.Mounted)} using={Bit(move.Using)} scripted_before={Bit(move.Scripted)} dist_before={F1(move.Distance)}" + ProblemText(move));
                break;
            }

            default:
                ReleaseErrandWalk("end");
                break;
        }
    }

    private static string ProblemText(LdErrandMove move) => move.Problem.Length == 0 ? "" : " problem=" + move.Problem;

    /// <summary>
    /// Releases the lord's scripted walk if the director opened one, once. Called at the errand's end and when the run ends or is abandoned, because a run that has a result hands out no more orders and the lord
    /// would otherwise keep walking.
    /// </summary>
    private void ReleaseErrandWalk(string why)
    {
        if (_lord == null || !_errandOpen)
            return;

        _errandOpen = false;
        var move = _lord.EndErrand();
        _logger.LogInfo($"{Tag}[Errand] end why={why} released={Bit(move.Done)} mounted={Bit(move.Mounted)} using={Bit(move.Using)} scripted_after={Bit(move.Scripted)}" + ProblemText(move));
    }

    /// <summary>The bot's place actions: the errand, its end, a gear set and an engine. False for every other action, which <c>PlayShop</c> hands on.</summary>
    private bool PlayPlace(BotAction action)
    {
        switch (action.Kind)
        {
            case BotActionKind.Errand:
                if (action.OfferId != null && Enum.TryParse<LdPlaceKind>(action.OfferId, out var place))
                    NotePlaceRefusal("errand " + place, _run.TryStartErrand(place).ToString(), ErrandResult.Ok.ToString());
                return true;
            case BotActionKind.EndErrand:
                _run.EndErrand();
                return true;
            case BotActionKind.Gear:
                if (action.OfferId != null)
                    NotePlaceRefusal("gear " + action.OfferId, _run.TryBuyGear(action.OfferId).ToString(), LocationResult.Ok.ToString());
                return true;
            case BotActionKind.Engine:
                if (action.OfferId != null)
                    NotePlaceRefusal("engine " + action.OfferId, _run.TryBuyEngine(action.OfferId, action.Site).ToString(), LocationResult.Ok.ToString());
                return true;
            default:
                return false;
        }
    }

    /// <summary>A refusal of the bot's place action is logged once for each wave, phase, action and answer, so a refusal that repeats every tick does not fill the log.</summary>
    private void NotePlaceRefusal(string what, string result, string ok)
    {
        if (result != ok && _placeRefusals.Add($"{_run.Wave}:{_botPhase}:{what}:{result}"))
            _logger.LogInfo($"{Tag}[Errand] the bot's {what} was refused: {result}");
    }
}

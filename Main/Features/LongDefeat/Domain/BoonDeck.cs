using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>One draw of an offer: the cards shown, how many rows were eligible, how many of them were not in the draw before (the reroll's exclusion held when 3 or more), and the bank after it.</summary>
internal sealed record BoonDraw(IReadOnlyList<string> Ids, int Eligible, int Others, int BankAfter)
{
    /// <summary>The bank when the draw was made, before a reroll spent one: a reroll earned while the pick stayed open makes it differ from the previous draw's <see cref="BankAfter"/>.</summary>
    public int BankBefore { get; init; }
}

/// <summary>One pick of the run as the report lists it: the wave whose clear offered it, every draw (the first, then each reroll), the boon taken and the wave it acts on.</summary>
internal sealed class BoonPickRecord
{
    public BoonPickRecord(int pick, int wave)
    {
        Pick = pick;
        Wave = wave;
    }

    public int Pick { get; }

    public int Wave { get; }

    public List<BoonDraw> Draws { get; } = new();

    public string? Taken { get; set; }

    /// <summary>The wave number that starts next once the boon is taken: a boon taken in the intermission after wave 3 acts from wave 4.</summary>
    public int TakenBeforeWave { get; set; }

    /// <summary>True when no row was eligible, so the pick drew nothing.</summary>
    public bool Lapsed => Draws.Count == 0;
}

/// <summary>
/// The boon pick state of a run (phase2-plan.md section 2, "Boons"): a pick owed at every third cleared wave, one offer open at a time, a reroll
/// earned at every fifth cleared wave into a bank of three, and a draw of three weighted cards from <see cref="RunRandom"/> stream 2 at
/// <c>(pick, draw)</c> (the first pick is number 1, the first draw 0). Plain state, touched only on the main thread, like the run that owns it.
/// </summary>
internal sealed class BoonDeck
{
    public const int PickEvery = 3;
    public const int RerollEvery = 5;
    public const int MaxBank = 3;
    public const int CardCount = 3;

    private readonly BoonPlan _plan;
    private readonly ulong _seed;
    private readonly string _classId;
    private readonly HashSet<BoonCategory> _troops;
    private readonly List<BoonRow> _owned = new();
    private readonly List<BoonPickRecord> _picks = new();
    private IReadOnlyList<BoonRow> _offer = Array.Empty<BoonRow>();

    /// <param name="startRerolls">A test option: the bank at the start, held to <see cref="MaxBank"/>. A seeded reroll counts as earned.</param>
    /// <param name="classId">The lord's class; a class row is eligible only for it.</param>
    /// <param name="troopGroups">The troop categories (infantry, archers, cavalry) the garrison or shop holds a troop of; a row of another one is never eligible.</param>
    public BoonDeck(BoonPlan plan, ulong seed, int startRerolls, string classId = "", IReadOnlyCollection<BoonCategory>? troopGroups = null)
    {
        _plan = plan;
        _seed = seed;
        _classId = classId;
        _troops = new HashSet<BoonCategory>(troopGroups ?? Array.Empty<BoonCategory>());
        Banked = Math.Max(0, Math.Min(MaxBank, startRerolls));
        Earned = Banked;
    }

    /// <summary>Picks owed that no offer has been opened for yet (one offer is open at a time).</summary>
    public int Owed { get; private set; }

    public int Banked { get; private set; }

    public int Earned { get; private set; }

    public int Used { get; private set; }

    /// <summary>The cards open now; empty when no pick is open.</summary>
    public IReadOnlyList<BoonRow> Offer => _offer;

    public IReadOnlyList<BoonRow> Owned => _owned;

    public IReadOnlyList<BoonPickRecord> Picks => _picks;

    /// <summary>The rows a draw may use now: the eligible ones of the plan in file order, less those already owned.</summary>
    public IReadOnlyList<BoonRow> Eligible() => _plan.Rows.Where(r => IsEligible(r) && !IsOwned(r.Id)).ToList();

    /// <summary>A wave was cleared (<paramref name="wavesCleared"/> clears so far, <paramref name="wave"/> the wave just cleared): owes a pick at every third, earns a reroll at every fifth, opens an offer when one is owed and none is open.</summary>
    public void Cleared(int wavesCleared, int wave)
    {
        if (wavesCleared > 0 && wavesCleared % PickEvery == 0)
            Owed++;
        if (wavesCleared > 0 && wavesCleared % RerollEvery == 0 && Banked < MaxBank)
        {
            Banked++;
            Earned++;
        }

        OpenOwed(wave);
    }

    /// <summary>Takes a card of the open offer. False, changing nothing, when no offer is open or the card is not in it. <paramref name="wave"/> is the wave just cleared and <paramref name="nextWave"/> the one that starts next.</summary>
    public bool TryPick(string id, int wave, int nextWave)
    {
        var row = _offer.FirstOrDefault(r => r.Id == id);
        if (row == null)
            return false;

        _owned.Add(row);
        var record = _picks[_picks.Count - 1];
        record.Taken = row.Id;
        record.TakenBeforeWave = nextWave;
        _offer = Array.Empty<BoonRow>();
        OpenOwed(wave);
        return true;
    }

    /// <summary>Discards the open offer and draws a new one that leaves out the cards just shown. False when no offer is open or no reroll is banked.</summary>
    public bool TryReroll()
    {
        if (_offer.Count == 0 || Banked <= 0)
            return false;

        var bankBefore = Banked;
        Banked--;
        Used++;
        var record = _picks[_picks.Count - 1];
        _offer = Draw(record, record.Draws.Count, _offer.Select(r => r.Id).ToList(), bankBefore);
        return true;
    }

    private void OpenOwed(int wave)
    {
        while (_offer.Count == 0 && Owed > 0)
        {
            Owed--;
            var record = new BoonPickRecord(_picks.Count + 1, wave);
            _picks.Add(record);
            _offer = Draw(record, 0, Array.Empty<string>(), Banked);
        }
    }

    /// <summary>
    /// One draw. The pool is the eligible rows less those just shown, unless fewer than <see cref="CardCount"/> would remain, in which case nothing is left out.
    /// Each card: <c>r = Below(total weight)</c>, walk the rows in order to the one <c>r</c> falls in, take it and remove it. Fewer rows than cards offers what is left.
    /// </summary>
    private IReadOnlyList<BoonRow> Draw(BoonPickRecord record, int draw, IReadOnlyCollection<string> shown, int bankBefore)
    {
        var eligible = Eligible();
        var others = eligible.Where(r => !shown.Contains(r.Id)).ToList();
        var pool = (shown.Count > 0 && others.Count >= CardCount ? others : eligible).ToList();
        if (pool.Count == 0)
            return Array.Empty<BoonRow>();

        var random = RunRandom.For(_seed, RunRandom.OfferStream, record.Pick, draw);
        var cards = new List<BoonRow>();
        for (var n = Math.Min(CardCount, pool.Count); n > 0; n--)
        {
            var roll = random.Below(pool.Sum(_plan.WeightOf));
            var index = 0;
            while (index < pool.Count - 1 && roll >= _plan.WeightOf(pool[index]))
                roll -= _plan.WeightOf(pool[index++]);
            cards.Add(pool[index]);
            pool.RemoveAt(index);
        }

        record.Draws.Add(new BoonDraw(cards.Select(c => c.Id).ToList(), eligible.Count, others.Count, Banked) { BankBefore = bankBefore });
        return cards;
    }

    private bool IsEligible(BoonRow row) => row.Category switch
    {
        BoonCategory.Class => row.ClassId == _classId,
        BoonCategory.Infantry or BoonCategory.Archers or BoonCategory.Cavalry => _troops.Contains(row.Category),
        _ => true,
    };

    private bool IsOwned(string id) => _owned.Any(r => r.Id == id);
}

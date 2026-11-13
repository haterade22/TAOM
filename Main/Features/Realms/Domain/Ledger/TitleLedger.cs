using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Realms.Domain.Laws;

namespace TAOM.Features.Realms.Domain.Ledger;

/// <summary>
/// The changing state of the titles: de jure holders, claims, laws, pending successions, each title's chronicle and
/// the holders of offices.
/// Keys are title and hero string ids; no engine objects are kept, so the ledger survives saves and tests.
/// </summary>
public sealed class TitleLedger
{
    private readonly Dictionary<string, string> _holders = new();

    /// <summary><see cref="_holders"/> turned around: each holder's titles. Kept in step by <see cref="SetHolder"/>.</summary>
    private readonly Dictionary<string, HashSet<string>> _heldBy = new();

    private readonly Dictionary<string, List<TitleClaim>> _claims = new();
    private readonly Dictionary<string, TitleLaws> _laws = new();
    private readonly Dictionary<string, PendingSuccession> _pending = new();
    private readonly Dictionary<string, List<ChronicleEntry>> _chronicle = new();
    private readonly Dictionary<string, string> _offices = new();
    private readonly HashSet<string> _knownOffices = new(System.StringComparer.Ordinal);

    public TitleLaws DefaultLaws { get; set; } = TitleLaws.Default;

    public string? HolderOf(string titleId) => _holders.TryGetValue(titleId, out var hero) ? hero : null;

    /// <summary>Sets the de jure holder. Null vacates the title. A new holder's own claim on it is spent.</summary>
    public void SetHolder(string titleId, string? heroId)
    {
        if (_holders.TryGetValue(titleId, out var previous) && _heldBy.TryGetValue(previous, out var previousTitles))
        {
            previousTitles.Remove(titleId);
            if (previousTitles.Count == 0)
                _heldBy.Remove(previous);
        }

        if (heroId == null)
        {
            _holders.Remove(titleId);
            return;
        }

        _holders[titleId] = heroId;
        if (!_heldBy.TryGetValue(heroId, out var titles))
            _heldBy[heroId] = titles = new HashSet<string>(System.StringComparer.Ordinal);
        titles.Add(titleId);
        RemoveClaim(titleId, heroId);
    }

    /// <summary>The hero's titles by right, by id. Read from an index, since every lord party's name asks for it.</summary>
    public IReadOnlyList<string> TitlesHeldBy(string heroId) =>
        heroId != null && _heldBy.TryGetValue(heroId, out var titles) ? titles.OrderBy(id => id).ToList() : new List<string>();

    public IEnumerable<KeyValuePair<string, string>> Holders => _holders;

    public bool AddClaim(TitleClaim claim)
    {
        if (HolderOf(claim.TitleId) == claim.HeroId)
            return false;
        if (!_claims.TryGetValue(claim.TitleId, out var list))
            _claims[claim.TitleId] = list = new List<TitleClaim>();
        if (list.Any(c => c.HeroId == claim.HeroId))
            return false;
        list.Add(claim);
        return true;
    }

    public void RemoveClaim(string titleId, string heroId)
    {
        if (!_claims.TryGetValue(titleId, out var list))
            return;
        list.RemoveAll(c => c.HeroId == heroId);
        if (list.Count == 0)
            _claims.Remove(titleId);
    }

    public void RemoveAllClaimsOf(string heroId)
    {
        foreach (var titleId in _claims.Keys.ToList())
            RemoveClaim(titleId, heroId);
    }

    public IReadOnlyList<TitleClaim> ClaimsOn(string titleId) =>
        _claims.TryGetValue(titleId, out var list) ? list.ToList() : new List<TitleClaim>();

    public IReadOnlyList<TitleClaim> ClaimsOf(string heroId) =>
        _claims.Values.SelectMany(l => l).Where(c => c.HeroId == heroId).OrderBy(c => c.TitleId).ToList();

    public IEnumerable<TitleClaim> AllClaims => _claims.Values.SelectMany(l => l);

    /// <summary>Laws set on this exact title, or null when it follows its parents.</summary>
    public TitleLaws? LawsOn(string titleId) => _laws.TryGetValue(titleId, out var laws) ? laws : null;

    public void SetLaws(string titleId, TitleLaws? laws)
    {
        if (laws == null)
            _laws.Remove(titleId);
        else
            _laws[titleId] = laws;
    }

    public IEnumerable<KeyValuePair<string, TitleLaws>> Laws => _laws;

    public void AddPending(PendingSuccession pending) => _pending[pending.TitleId] = pending;

    public void RemovePending(string titleId) => _pending.Remove(titleId);

    public IReadOnlyList<PendingSuccession> Pending => _pending.Values.OrderBy(p => p.TitleId).ToList();

    public bool IsPending(string titleId) => _pending.ContainsKey(titleId);

    /// <summary>Adds a change of holder to the history of a title or office, dropping the oldest beyond <paramref name="limit"/>.</summary>
    public void Record(string id, ChronicleEntry entry, int limit)
    {
        if (limit <= 0)
            return;
        if (!_chronicle.TryGetValue(id, out var list))
            _chronicle[id] = list = new List<ChronicleEntry>();
        list.Add(entry);
        if (list.Count > limit)
            list.RemoveRange(0, list.Count - limit);
    }

    /// <summary>The history of a title or office, oldest first.</summary>
    public IReadOnlyList<ChronicleEntry> ChronicleOf(string id) =>
        _chronicle.TryGetValue(id, out var list) ? list.ToList() : new List<ChronicleEntry>();

    public IEnumerable<KeyValuePair<string, List<ChronicleEntry>>> Chronicles => _chronicle;

    public string? OfficeHolderOf(string officeId) => _offices.TryGetValue(officeId, out var hero) ? hero : null;

    /// <summary>Sets the holder of an office; null vacates it.</summary>
    public void SetOfficeHolder(string officeId, string? heroId)
    {
        if (heroId == null)
            _offices.Remove(officeId);
        else
            _offices[officeId] = heroId;
    }

    public IReadOnlyList<string> OfficesHeldBy(string heroId) =>
        _offices.Where(p => p.Value == heroId).Select(p => p.Key).OrderBy(id => id, System.StringComparer.Ordinal).ToList();

    public IEnumerable<KeyValuePair<string, string>> Offices => _offices;

    /// <summary>Offices this campaign has made already, held or vacant; a new office in the config is made once.</summary>
    public ISet<string> KnownOffices => _knownOffices;
}

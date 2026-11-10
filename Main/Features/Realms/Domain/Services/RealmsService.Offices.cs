using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Realms.Domain.Config;
using TAOM.Features.Realms.Domain.Generation;
using TAOM.Features.Realms.Domain.Ledger;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Features.Realms.Domain.Services;

/// <summary>
/// Offices of a realm (the Steward of Gondor, the Marshals of the Mark), defined in &lt;Offices&gt;. Each is held by one
/// hero of its realm at a time, appointed by the realm's ruler; it never passes by inheritance.
/// </summary>
public sealed partial class RealmsService
{
    public OfficeConfig? OfficeById(string officeId) => Config.Offices.FirstOrDefault(o => o.Id == officeId);

    /// <summary>The offices the hero holds, highest rank first.</summary>
    public IReadOnlyList<OfficeConfig> OfficesOf(string heroId) =>
        Ledger.OfficesHeldBy(heroId).Select(OfficeById).Where(o => o != null).Select(o => o!)
            .OrderByDescending(o => o.Rank).ThenBy(o => o.Id, StringComparer.Ordinal).ToList();

    public OfficeConfig? PrimaryOfficeOf(string heroId) => OfficesOf(heroId).FirstOrDefault();

    /// <summary>The offices of a realm, in the order of the config.</summary>
    public IReadOnlyList<OfficeConfig> OfficesOfRealm(string kingdomId) => Config.Offices.Where(o => o.KingdomId == kingdomId).ToList();

    /// <summary>
    /// Makes the offices in the config this campaign does not know yet (a new campaign, or a save from before they
    /// existed), appointing their initial holder when alive and serving the realm. Then runs <see cref="UpdateOffices"/>
    /// without candidates, so an office that follows the ruler starts with the ruler.
    /// </summary>
    public IReadOnlyList<TitleEvent> EnsureOffices(IOfficeWorld world)
    {
        var events = new List<TitleEvent>();
        foreach (var office in Config.Offices.Where(o => !Ledger.KnownOffices.Contains(o.Id)))
        {
            Ledger.KnownOffices.Add(office.Id);
            var initial = office.InitialHolderId;
            if (initial != null && world.IsAlive(initial) && world.KingdomOfHero(initial) == office.KingdomId
                && Ledger.OfficesHeldBy(initial).Count == 0)
                SetOffice(office.Id, initial, TitleEventReason.Start, events);
        }

        events.AddRange(UpdateOffices(world, null));
        return events;
    }

    /// <summary>
    /// The weekly look over the offices. An office that follows the ruler goes to the ruler while they do not hold the
    /// realm title. Otherwise a holder who died, left the realm or became its ruler loses it. Vacant offices are filled
    /// from <paramref name="candidates"/> (by realm, in the ruler's order of preference): the first alive, serving the
    /// realm, not its ruler and holding no other office. A realm without candidates (the player's) keeps them vacant.
    /// </summary>
    public IReadOnlyList<TitleEvent> UpdateOffices(IOfficeWorld world, IReadOnlyDictionary<string, IReadOnlyList<string>>? candidates)
    {
        var events = new List<TitleEvent>();
        foreach (var office in Config.Offices.Where(o => Ledger.KnownOffices.Contains(o.Id)))
        {
            var ruler = world.RulerOf(office.KingdomId);
            var holder = Ledger.OfficeHolderOf(office.Id);
            if (office.FollowsRuler && ruler != null && Ledger.HolderOf(TitleGenerator.KingdomPrefix + office.KingdomId) != ruler)
            {
                if (holder != ruler)
                    SetOffice(office.Id, ruler, TitleEventReason.RealmSuccession, events);
                continue;
            }

            if (holder != null && (!world.IsAlive(holder) || world.KingdomOfHero(holder) != office.KingdomId || holder == ruler))
            {
                SetOffice(office.Id, null, TitleEventReason.Dismissal, events);
                holder = null;
            }

            if (holder != null || candidates == null || !candidates.TryGetValue(office.KingdomId, out var list))
                continue;
            var pick = list.FirstOrDefault(c => CanHoldOffice(office, c, world));
            if (pick != null)
                SetOffice(office.Id, pick, TitleEventReason.Appointment, events);
        }

        return events;
    }

    /// <summary>Whether the hero could be appointed: alive, serving the office's realm, not its ruler, holding no office.</summary>
    public bool CanHoldOffice(OfficeConfig office, string heroId, IOfficeWorld world) =>
        world.IsAlive(heroId) && world.KingdomOfHero(heroId) == office.KingdomId && world.RulerOf(office.KingdomId) != heroId
        && Ledger.OfficesHeldBy(heroId).Count == 0;

    /// <summary>
    /// Whether the appointer may fill or change the office: they rule its realm, and the office is not one that follows
    /// the ruler while they lack the realm title (then it is theirs).
    /// </summary>
    public bool CanAppointTo(string appointerId, OfficeConfig office, IOfficeWorld world) =>
        world.RulerOf(office.KingdomId) == appointerId
        && !(office.FollowsRuler && Ledger.HolderOf(TitleGenerator.KingdomPrefix + office.KingdomId) != appointerId);

    /// <summary>The ruler appoints the hero, replacing any holder. Does nothing unless both checks allow it.</summary>
    public IReadOnlyList<TitleEvent> Appoint(string appointerId, string officeId, string heroId, IOfficeWorld world)
    {
        var events = new List<TitleEvent>();
        var office = OfficeById(officeId);
        if (office == null || !Ledger.KnownOffices.Contains(officeId) || !CanAppointTo(appointerId, office, world)
            || !CanHoldOffice(office, heroId, world))
            return events;
        SetOffice(officeId, heroId, TitleEventReason.Appointment, events);
        return events;
    }

    /// <summary>The ruler dismisses the office's holder.</summary>
    public IReadOnlyList<TitleEvent> Dismiss(string appointerId, string officeId, IOfficeWorld world)
    {
        var events = new List<TitleEvent>();
        var office = OfficeById(officeId);
        if (office == null || Ledger.OfficeHolderOf(officeId) == null || !CanAppointTo(appointerId, office, world))
            return events;
        SetOffice(officeId, null, TitleEventReason.Dismissal, events);
        return events;
    }

    /// <summary>A dead hero's offices fall vacant.</summary>
    private void VacateOfficesOf(string heroId, List<TitleEvent> events)
    {
        foreach (var officeId in Ledger.OfficesHeldBy(heroId).ToList())
            SetOffice(officeId, null, TitleEventReason.Vacated, events);
    }

    private void SetOffice(string officeId, string? heroId, TitleEventReason reason, List<TitleEvent> events)
    {
        var previous = Ledger.OfficeHolderOf(officeId);
        if (previous == heroId)
            return;
        Ledger.SetOfficeHolder(officeId, heroId);
        Ledger.Record(officeId, new ChronicleEntry(Clock(), reason, heroId, previous), Config.Feudal.ChronicleEntries);
        events.Add(new TitleEvent(TitleEventKind.OfficeChanged, reason, officeId, heroId, previous));
    }
}

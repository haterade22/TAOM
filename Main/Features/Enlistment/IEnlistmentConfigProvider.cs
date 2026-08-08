using System.Collections.Generic;

namespace TAOM.Features.Enlistment;

/// <summary>Core enlistment tunables. JSON-backed loading + validation lands with the content phase; until then compiled defaults.</summary>
public sealed class EnlistmentCoreConfig
{
    public double ContractDays { get; set; } = 365.0;

    /// <summary>
    /// Days of service owed before a commander will grant an honourable release. Deliberately
    /// NOT <see cref="ContractDays"/>: the contract is a year, so keying the release refusal to
    /// it would refuse every realistic request and leave desertion as the only exit — which is
    /// the bug reported on 2026-08-07 wearing a different hat. A term the player can actually
    /// serve is what makes "leave now and forfeit your pay" a choice rather than a trap.
    /// Degenerate values (NaN, infinity, &lt;= 0) grant release immediately; see
    /// <c>EnlistmentDialogGateService.EvaluateReleaseRequest</c>.
    /// </summary>
    public double MinimumServiceDays { get; set; } = 21.0;

    /// <summary>Days a commander may remain party-less (captured/disbanded) before an honorable auto-discharge.</summary>
    public double CommanderGraceDays { get; set; } = 7.0;

    /// <summary>
    /// Native menu ids rewritten to the service wait menu while EnlistedAttached. Seeded
    /// from the v1.4.7 EncounterGameMenuBehavior sweep (incl. the 1.4.x naval ids);
    /// fail-open — unknown menus flow through and are logged once per id instead.
    /// </summary>
    public List<string> RedirectMenuIds { get; set; } = new List<string>
    {
        "army_wait",
        "army_wait_at_settlement",
        "town_wait",
        "town_wait_menus",
        "village_wait_menus",
        "town_outside",
        "castle_outside",
        "join_encounter",
        "encounter",
        "port_menu",
        "naval_town_outside",
    };
}

public interface IEnlistmentConfigProvider
{
    EnlistmentCoreConfig GetConfig();
}

using System.Collections.Generic;

namespace TAOM.Features.Enlistment;

/// <summary>Core enlistment tunables. JSON-backed loading + validation lands with the content phase; until then compiled defaults.</summary>
public sealed class EnlistmentCoreConfig
{
    public double ContractDays { get; set; } = 365.0;

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

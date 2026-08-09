using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace TAOM.Tests.Features.CharacterCreation;

/// <summary>
/// Cross-references CC-selectable cultures against the four culture-scoped narrative menus.
/// The sibling of <see cref="CareerCultureCoverageTests"/>, which does the same for careers.
///
/// A culture with zero options renders an empty narrative page, and vanilla CC throws on
/// <c>SelectedOptions[CurrentMenu]</c> when the player tries to advance past it — so the culture
/// appears selectable and then dead-ends character creation (#111, open since 2026-05-07 pending a
/// design decision).
///
/// This exists because the failure keeps arriving through a different door. `shaghana` / `abanissa`
/// have no narrative options (#111), no eligible careers (review #24) and no enlistment rosters
/// (#431); `goblin` / `mistymountainorcs` had no careers and shipped with all 96 of their narrative
/// STRINGS unregistered (#432). The common cause is not carelessness — it is that every one of those
/// coverage tables was hand-maintained and written before those cultures existed. **A per-culture
/// invariant has to be driven off the culture list itself**, which is what this test does.
/// </summary>
[TestClass]
public class NarrativeCultureCoverageTests
{
    private static readonly string ModuleDataPath = Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..",
            "Main", "_Module", "ModuleData"));

    /// <summary>
    /// The culture-scoped menus. `childhood_menu.json` and `career_menu.json` are excluded because
    /// they carry no `culture_id` at all — every option is offered to everyone, so they cannot have
    /// a per-culture gap.
    /// </summary>
    private static readonly string[] CultureScopedMenus =
        { "parents_menu.json", "youth_menu.json", "education_menu.json", "adulthood_menu.json" };

    /// <summary>
    /// Cultures selectable in CC that knowingly have no narrative options. Removing a culture from
    /// this list is the last step of authoring its content; adding one requires an issue explaining
    /// why the culture is selectable without a completable flow.
    /// </summary>
    private static readonly HashSet<string> DocumentedExceptions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // #111 — selectable in cultures.json, zero options in all four menus, so picking either
            // one dead-ends character creation. Blocked on a design decision: author the content,
            // or drop them from cultures.json and keep them as NPC-only kingdoms.
            "shaghana",
            "abanissa",
        };

    private static List<string> LoadSelectableCultures()
    {
        var path = Path.Combine(ModuleDataPath, "charactercreation", "cultures.json");
        Assert.IsTrue(File.Exists(path), $"cultures.json not found at {path}");

        return JArray.Parse(File.ReadAllText(path))
            .Select(c => c.Value<string>("culture_id"))
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList()!;
    }

    [TestMethod]
    public void EverySelectableCulture_HasNarrativeOptionsInEveryMenu_OrIsDocumented()
    {
        var cultures = LoadSelectableCultures();

        var gaps = new List<string>();
        foreach (var menu in CultureScopedMenus)
        {
            var path = Path.Combine(ModuleDataPath, "charactercreation", menu);
            Assert.IsTrue(File.Exists(path), $"{menu} not found at {path}");

            var options = JArray.Parse(File.ReadAllText(path));
            foreach (var culture in cultures.Where(c => !DocumentedExceptions.Contains(c)))
            {
                if (!options.Any(o => string.Equals(o.Value<string>("culture_id"), culture,
                        StringComparison.OrdinalIgnoreCase)))
                    gaps.Add($"{menu}: {culture}");
            }
        }

        Assert.AreEqual(0, gaps.Count,
            "A CC-selectable culture with no options in a menu renders an empty page, and vanilla "
            + "throws on SelectedOptions[CurrentMenu] when the player advances — the culture is "
            + "offered and then dead-ends creation. Author options, or drop the culture from "
            + "cultures.json:\n  " + string.Join("\n  ", gaps));
    }

    [TestMethod]
    public void DocumentedExceptions_AreStillSelectableAndStillEmpty()
    {
        // A documented exception that has been fixed, or that no longer exists, is a stale
        // suppression — it silently widens the test's blind spot for whoever inherits it. Failing
        // loudly on a RESOLVED exception is the only thing that gets the entry deleted.
        var cultures = LoadSelectableCultures();
        var stale = new List<string>();

        foreach (var exception in DocumentedExceptions)
        {
            if (!cultures.Contains(exception, StringComparer.OrdinalIgnoreCase))
            {
                stale.Add($"{exception}: no longer selectable in cultures.json — delete the exception");
                continue;
            }

            var covered = CultureScopedMenus.Count(menu =>
                JArray.Parse(File.ReadAllText(Path.Combine(ModuleDataPath, "charactercreation", menu)))
                    .Any(o => string.Equals(o.Value<string>("culture_id"), exception,
                        StringComparison.OrdinalIgnoreCase)));

            if (covered == CultureScopedMenus.Length)
                stale.Add($"{exception}: now has options in all {covered} menus — delete the exception and close #111");
            else if (covered > 0)
                stale.Add($"{exception}: partially authored ({covered}/{CultureScopedMenus.Length} menus) — "
                    + "a partial flow still dead-ends; finish it or revert");
        }

        Assert.AreEqual(0, stale.Count,
            "Stale entries in DocumentedExceptions:\n  " + string.Join("\n  ", stale));
    }
}

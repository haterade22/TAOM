using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Core;

/// <summary>
/// No production code null-checks <c>PartyBase.MainParty</c>. The getter (v1.4.8 PartyBase.cs:208-218) returns
/// null without a campaign, but with one it dereferences <c>Campaign.Current.MainParty.Party</c>, so it throws
/// while <c>Campaign.MainParty</c> is null: the check throws in the one case it is written for, whichever way
/// round it is written. The messenger popup shipped that
/// check; the stored <c>Campaign.Current?.MainParty</c> is the safe test. The scan
/// covers both polarities, the reversed forms, the <c>is</c> patterns, <c>??</c> and <c>?.</c>, which guards only
/// the no-campaign case (read <c>Campaign.Current?.MainParty?.Party</c> instead), on the getter written directly
/// or inside any number of its own parentheses. A check through a local alias is not caught. Comments are blanked
/// first, string literals are not, so a string holding the check is reported (<see cref="RepoPaths.StripComments"/>).
/// </summary>
[TestClass]
public class MainPartyNullCheckScanTests
{
    // The getter, bare or inside balanced parentheses of its own (the balancing group `p` makes every opening
    // paren close before the operator). The lookbehind keeps a call argument such as
    // `Find (PartyBase.MainParty)?.X` out, whitespace or a generic argument list before the paren included: there
    // the check is on the call's result, not the getter. A keyword before the paren (`return (`) and a lambda
    // arrow (`=> (`) are not calls, so they stay in.
    private static readonly Regex NullCheck = new(
        @"(?:\bPartyBase\s*\.\s*MainParty"
        + @"|(?<!(?:[\])]|(?<!=)>|\b(?!(?:return|await|throw|case|when|else|in|is|and|or|not)\b)\w+)\s*)"
        + @"(?:(?<p>\()\s*)+PartyBase\s*\.\s*MainParty(?:\s*(?<-p>\)))+(?(p)(?!)))"
        + @"\s*(?:[!=]=\s*null\b|is\s+(?:not\s+)?null\b|\?\.|\?\?)"
        + @"|\bnull\s*[!=]=\s*(?:\(\s*)*PartyBase\s*\.\s*MainParty\b",
        RegexOptions.Compiled);

    [DataTestMethod]
    [DataRow("if (PartyBase.MainParty == null) return;")]
    [DataRow("PartyBase.MainParty != null && x")]
    [DataRow("if (null == PartyBase.MainParty) return;")]
    [DataRow("null != PartyBase.MainParty && x")]
    [DataRow("if (PartyBase.MainParty is null) return;")]
    [DataRow("PartyBase.MainParty is not null && x")]
    [DataRow("PartyBase . MainParty!=null")]
    [DataRow("return PartyBase.MainParty?.MapEvent != null;")]
    [DataRow("var roster = PartyBase.MainParty?.ItemRoster;")]
    [DataRow("PartyBase . MainParty ?. ItemRoster")]
    [DataRow("var roster = (PartyBase.MainParty)?.ItemRoster;")]
    [DataRow("if ((PartyBase.MainParty) == null) return;")]
    [DataRow("( PartyBase.MainParty ) != null && x")]
    [DataRow("if ((PartyBase.MainParty) is null) return;")]
    [DataRow("(PartyBase.MainParty) is not null && x")]
    [DataRow("if (null == (PartyBase.MainParty)) return;")]
    [DataRow("var party = PartyBase.MainParty ?? fallback;")]
    [DataRow("var party = (PartyBase.MainParty) ?? fallback;")]
    [DataRow("if (((PartyBase.MainParty)) == null) return;")]
    [DataRow("((PartyBase.MainParty)) != null && x")]
    [DataRow("if (null == ((PartyBase.MainParty))) return;")]
    [DataRow("null != ( ( PartyBase.MainParty ) ) && x")]
    [DataRow("if (((PartyBase.MainParty)) is null) return;")]
    [DataRow("var party = ((PartyBase.MainParty)) ?? fallback;")]
    [DataRow("var roster = ((PartyBase.MainParty))?.ItemRoster;")]
    [DataRow("return (PartyBase.MainParty)?.MapEvent;")]
    [DataRow("return ((PartyBase.MainParty)) == null;")]
    [DataRow("Func<bool> missing = () => (PartyBase.MainParty) == null;")]
    public void NullCheck_EveryFormOfTheComputedGetterCheck_IsCaught(string source)
    {
        Assert.IsTrue(NullCheck.IsMatch(RepoPaths.StripComments(source)), source);
    }

    [DataTestMethod]
    [DataRow("if (Campaign.Current?.MainParty == null) return;")]
    [DataRow("Campaign.Current?.MainParty != null && x")]
    [DataRow("var party = PartyBase.MainParty;")]
    [DataRow("PartyBase.MainParty.MobileParty")]
    [DataRow("// PartyBase.MainParty != null")]
    [DataRow("/* null == PartyBase.MainParty */ x")]
    [DataRow("x = y; // if (PartyBase.MainParty is not null)")]
    [DataRow("var roster = Campaign.Current?.MainParty?.Party?.ItemRoster;")]
    [DataRow("if (Describe(PartyBase.MainParty) == null) return;")]
    [DataRow("var roster = Find(PartyBase.MainParty)?.ItemRoster;")]
    [DataRow("var party = (PartyBase.MainParty);")]
    [DataRow("var roster = Find (PartyBase.MainParty)?.ItemRoster;")]
    [DataRow("var roster = Find((PartyBase.MainParty))?.ItemRoster;")]
    [DataRow("if (Describe ((PartyBase.MainParty)) == null) return;")]
    [DataRow("var roster = Find<Party> (PartyBase.MainParty)?.ItemRoster;")]
    [DataRow("var roster = Find\n    (PartyBase.MainParty) ?? fallback;")]
    public void NullCheck_TheStoredPartyOrACommentOrAPlainRead_IsNotCaught(string source)
    {
        Assert.IsFalse(NullCheck.IsMatch(RepoPaths.StripComments(source)), source);
    }

    /// <summary>
    /// The scan's known limit: a regex sees one expression, so a check through a local alias is not caught.
    /// The rule and lesson say so; review catches the alias, and the three found in #792 are pinned below.
    /// </summary>
    [TestMethod]
    public void NullCheck_ACheckThroughALocalAlias_IsAKnownGap()
    {
        const string source = "var party = PartyBase.MainParty;\nif (party == null) return;";
        Assert.IsFalse(NullCheck.IsMatch(RepoPaths.StripComments(source)), source);
    }

    [DataTestMethod]
    [DataRow("Main/Adapters/PlayerEquipmentAdapter.cs")]
    [DataRow("Main/Features/TroopWeight/Hooks/TroopWeightDisplayHook.cs")]
    [DataRow("Main/Features/Enlistment/Hooks/EnlistmentBattleBehavior.cs")]
    public void AliasedSites_ReadTheMainPartyThroughTheCampaign(string relativePath)
    {
        // Each null-checked a local copy of PartyBase.MainParty, the alias form the scan cannot see (#792).
        var path = RepoPaths.RepoPath(relativePath.Split('/'));
        Assert.IsFalse(RepoPaths.MentionsOutsideComments(path, "PartyBase.MainParty"),
            relativePath + " must read Campaign.Current?.MainParty?.Party, not the computed PartyBase.MainParty");
    }

    [TestMethod]
    public void ProductionSource_NeverNullChecksTheComputedPartyBaseMainParty()
    {
        var offenders = Directory.EnumerateFiles(RepoPaths.RepoPath("Main"), "*.cs", SearchOption.AllDirectories)
            .Where(p => RepoPaths.MentionsOutsideComments(p, "MainParty"))
            .Where(p => NullCheck.IsMatch(RepoPaths.StripComments(File.ReadAllText(p))))
            .Select(p => p.Substring(RepoPaths.RepoPath().Length).TrimStart('\\', '/'))
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "PartyBase.MainParty is a computed getter that dereferences Campaign.Current.MainParty, so it throws "
            + "while the main party is null instead of returning null; use `Campaign.Current?.MainParty == null`. "
            + "Offending files: " + string.Join(", ", offenders));
    }
}

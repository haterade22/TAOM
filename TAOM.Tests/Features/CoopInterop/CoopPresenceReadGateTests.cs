using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.CoopInterop;

/// <summary>
/// Fails a NEW read of co-op module PRESENCE (<c>ICoopPresenceProvider.IsCoopActive</c> or
/// <c>CoopPresence.IsActive</c>) outside the files below. Presence is true whenever the Coop module is
/// merely enabled, so it is correct only for a one-shot startup or session-launch decision. Anything that
/// mutates or decides shared world state picks a session-aware predicate by what it decides: see the
/// lesson "Presence is not authority" in <c>docs/reviews/lessons/campaign-mechanics.md</c>. A listed file
/// whose read is gone also fails, so the list cannot rot into a blanket pass.
/// </summary>
[TestClass]
public class CoopPresenceReadGateTests
{
    private static readonly Regex PresenceRead =
        new(@"\.\s*IsCoopActive\b|\bCoopPresence\s*\.\s*IsActive\b", RegexOptions.Compiled);

    /// <summary>Repo-relative file (forward slashes) to the reason its presence read is the right predicate.</summary>
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Main/Features/CareerSystem/CareerQuestService.cs"] = "session-launch term: a presence check beside the authority predicate",
        ["Main/Features/PlayerPossession/PlayerPossessionService.cs"] = "documented heir discriminator, not an authority decision",
        ["Main/Features/ShadowfaxScreens/Clan/ClanScreenFastPathService.cs"] = "client-side UI fast path choice",
        ["Client/ClientUiExtensions.cs"] = "one-shot UI registration at startup",
        ["Main/Features/FactionUI/Hooks/GauntletLayerLoadMoviePatch.cs"] = "the themed map bar's clock binds the time-control mixin ClientUiExtensions drops on presence, so the movie swap follows that registration",
        ["Main/Features/CoopInterop/CoopPresenceProvider.cs"] = "the provider itself",
        ["Main/Features/CoopInterop/CoopSessionProvider.cs"] = "the session probe that derives the session-aware predicates",
        ["Main/Features/CoopInterop/Hooks/CoopRecruitAttributionPatch.cs"] = "recruit attribution is only wanted while the Coop module is loaded",
        ["Main/Features/CoopInterop/Hooks/CoopUpgradeChargePatch.cs"] = "Prepare warns about a moved target only while the Coop module is loaded",
        ["Main/Features/MarshalsWord/Hooks/MarshalSpacingPatch.cs"] = "a patch-time decision: the spacing repair stays off wherever a second machine could compute another width",
    };

    private static List<string> FilesReadingPresence()
    {
        var repoDir = RepoPath();
        return CSharpFiles()
            .Where(f => MentionsPresenceOutsideComments(f))
            .Select(f => f.Substring(repoDir.Length).TrimStart('\\', '/').Replace('\\', '/'))
            .ToList();
    }

    private static bool MentionsPresenceOutsideComments(string path)
    {
        return ReadsPresence(File.ReadAllText(path));
    }

    /// <summary>The literal prefilter keeps the whole-tree scan cheap; it names no dot, because C# allows
    /// whitespace around one (<c>CoopPresence . IsActive</c>) and the regex decides.</summary>
    private static bool ReadsPresence(string text)
    {
        if (text.IndexOf("IsCoopActive", StringComparison.Ordinal) < 0
            && text.IndexOf("CoopPresence", StringComparison.Ordinal) < 0)
            return false;
        return PresenceRead.IsMatch(StripComments(text));
    }

    [TestMethod]
    public void ReadsPresence_LegalWhitespaceAroundTheDot_StillMatches()
    {
        Assert.IsTrue(ReadsPresence("var a = CoopPresence . IsActive;"));
        Assert.IsTrue(ReadsPresence("var a = CoopPresence\r\n    .IsActive;"));
        Assert.IsTrue(ReadsPresence("var a = presence. IsCoopActive;"));
        Assert.IsTrue(ReadsPresence("var a = presence\r\n    . IsCoopActive;"));
        Assert.IsTrue(ReadsPresence("var a = presence.IsCoopActive;"));
        Assert.IsTrue(ReadsPresence("var a = CoopPresence.IsActive;"));
    }

    [TestMethod]
    public void ReadsPresence_CommentsAndOtherMembers_DoNotMatch()
    {
        Assert.IsFalse(ReadsPresence("// presence . IsCoopActive"));
        Assert.IsFalse(ReadsPresence("/* CoopPresence . IsActive */"));
        Assert.IsFalse(ReadsPresence("var a = CoopPresence.IsActiveNow;"));
        Assert.IsFalse(ReadsPresence("var a = presence.IsCoopActiveLater;"));
        Assert.IsFalse(ReadsPresence("var a = OtherPresence.IsActive;"));
    }

    [TestMethod]
    public void PresenceReads_OutsideTheAllowlist_AreNone()
    {
        var readers = FilesReadingPresence();
        Assert.IsTrue(readers.Count > 0, "The scan found no presence read at all: the gate is not reading the tree.");

        var stray = readers.Where(r => !Allowed.ContainsKey(r)).ToList();

        Assert.AreEqual(0, stray.Count,
            "A co-op presence read is only right for a startup or session-launch decision. Pick the predicate "
            + "by what the code decides (docs/features/coop-interop.md, 'Which predicate to use'), or add the "
            + "file to Allowed with its reason. Found in: " + string.Join(", ", stray));
    }

    [TestMethod]
    public void Allowlist_EveryEntryStillReadsPresence()
    {
        var readers = new HashSet<string>(FilesReadingPresence(), StringComparer.OrdinalIgnoreCase);

        var stale = Allowed.Keys.Where(k => !readers.Contains(k)).ToList();

        Assert.AreEqual(0, stale.Count,
            "These allowlisted files no longer read presence: remove them from Allowed. " + string.Join(", ", stale));
    }
}

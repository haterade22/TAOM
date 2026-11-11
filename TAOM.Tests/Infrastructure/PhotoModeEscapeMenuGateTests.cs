using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// The singleplayer escape menu offers Photo Mode, which pauses the engine, and only the Photo Mode view's Escape handler turns it off
/// (v1.5.7: MissionGauntletSingleplayerEscapeMenu, MissionGauntletPhotoMode.OnEscape). So every TAOM <c>[ViewMethod]</c> list that has
/// that escape menu also lists <c>ViewCreator.CreatePhotoModeView()</c>, or constructs a view whose class overrides
/// <c>IsPhotoModeAllowed</c>. The Long Defeat (2026-11-04) and Story Missions (#1105, 2026-11-10) lists each copied a sibling and lost
/// this; lesson "A mode that copies a sibling's views must copy what they refuse" in docs/reviews/lessons/adapters-taleworlds-api.md.
/// </summary>
[TestClass]
public class PhotoModeEscapeMenuGateTests
{
    private const string EscapeMenu = "CreateMissionSingleplayerEscapeMenu";
    private static readonly Regex Constructed = new(@"\bnew\s+([A-Z]\w*)\s*\(", RegexOptions.Compiled);
    private static readonly Regex ClassName = new(@"\bclass\s+(\w+)", RegexOptions.Compiled);

    [TestMethod]
    public void EveryViewListWithTheEscapeMenu_HasThePhotoModeViewOrAViewThatRefusesIt()
    {
        // Filter on the raw text first, then strip comments from the few candidates. A file that names a token only in a comment passes this
        // filter, and the checks below, which read the stripped text, then drop it.
        var sources = RepoPaths.CSharpFiles()
            .Select(f => (File: f, Raw: File.ReadAllText(f)))
            .Where(s => s.Raw.Contains("IsPhotoModeAllowed") || s.Raw.Contains("[ViewCreatorModule]"))
            .ToDictionary(s => s.File, s => RepoPaths.StripComments(s.Raw));
        var refusers = new HashSet<string>(sources.Values
            .Where(text => text.Contains("override bool IsPhotoModeAllowed"))
            .SelectMany(text => ClassName.Matches(text).Cast<Match>().Select(m => m.Groups[1].Value)));

        var lists = sources
            .Where(s => s.Value.Contains("[ViewCreatorModule]"))
            .SelectMany(s => ViewMethodBodies(s.Value).Select(body => (File: s.Key, Body: body)))
            .Where(list => list.Body.Contains(EscapeMenu))
            .ToList();

        Assert.IsTrue(lists.Count >= 4, $"the scan found {lists.Count} view lists with the escape menu, expected at least 4; the gate stopped guarding");
        var missing = lists
            .Where(list => !list.Body.Contains("ViewCreator.CreatePhotoModeView()")
                && !Constructed.Matches(list.Body).Cast<Match>().Any(m => refusers.Contains(m.Groups[1].Value)))
            .Select(list => list.File)
            .ToList();
        Assert.AreEqual(0, missing.Count, "Photo Mode would pause the game with no way back in: " + string.Join(", ", missing)
            + ". Add ViewCreator.CreatePhotoModeView() to the list, or a view that overrides IsPhotoModeAllowed.");
    }

    /// <summary>Each <c>[ViewMethod]</c> method's text, from its attribute to the next attribute or the end of the file.</summary>
    private static IEnumerable<string> ViewMethodBodies(string source)
    {
        var starts = Regex.Matches(source, @"\[ViewMethod\b").Cast<Match>().Select(m => m.Index).ToList();
        for (var i = 0; i < starts.Count; i++)
            yield return source.Substring(starts[i], (i + 1 < starts.Count ? starts[i + 1] : source.Length) - starts[i]);
    }
}

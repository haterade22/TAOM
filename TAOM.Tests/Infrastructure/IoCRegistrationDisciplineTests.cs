using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Ratchet: no NEW eager <c>container.Resolve</c> inside a feature's <c>Register*Feature</c> body.
///
/// <para>An eager Resolve during registration materializes <c>IEnumerable&lt;T&gt;</c> contributor
/// injections at that instant, so any contributor a LATER feature registers is silently invisible.
/// That shipped as a round-A HIGH: FieldCamp's eager resolve baked its overlay-contributor
/// collection empty and Refuge's camp-block never fired (RCA
/// `rca-yotthani-camps-2026-08-23.md` Class 5). Patch-static initialisation belongs in the single
/// post-registration block <c>IoC.InitializePatchStatics</c>, which runs after the LAST feature
/// registers.</para>
///
/// <para>Pre-existing offenders are baselined below with a burn-down intent: they are scalar-only
/// resolves that predate the contributor seams and are safe today. Do not add to the list; move
/// initialisation to the post-registration block instead.</para>
/// </summary>
[TestClass]
public class IoCRegistrationDisciplineTests
{
    private static readonly HashSet<string> Baseline = new HashSet<string>(StringComparer.Ordinal)
    {
        // Scalar resolves predating the post-registration block; safe (no collection injection
        // on their resolve paths) but grandfathered, not endorsed. Verified by source scan
        // 2026-08-23; the stale-entry test below keeps this list honest.
        "BannerInjectionIoC.cs",
        "FactionMapIoC.cs",
        "HeroRaceIoC.cs",
    };

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    [TestMethod]
    public void NoNewFeatureIoC_ResolvesEagerlyDuringRegistration()
    {
        var offenders = new List<string>();
        var featureDir = Path.Combine(RepoRoot, @"Main\Features");

        foreach (var file in Directory.EnumerateFiles(featureDir, "*IoC.cs", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            var text = File.ReadAllText(file);

            // Only the registration body counts; InitializePatchStatics is the sanctioned home.
            var registerBody = ExtractRegisterBody(text);
            if (registerBody == null)
                continue;

            if (Regex.IsMatch(registerBody, @"container\s*\.\s*Resolve"))
            {
                if (Baseline.Contains(name))
                    continue;
                offenders.Add(name);
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "Eager container.Resolve inside a Register*Feature body. This materializes " +
            "IEnumerable<T> contributor injections before later features register (the Refuge " +
            "camp-block round-A HIGH). Move the initialisation into IoC.InitializePatchStatics:\n" +
            string.Join("\n", offenders));
    }

    [TestMethod]
    public void Baseline_HasNoStaleEntries()
    {
        var featureDir = Path.Combine(RepoRoot, @"Main\Features");
        var stale = new List<string>();

        foreach (var name in Baseline)
        {
            var matches = Directory.EnumerateFiles(featureDir, name, SearchOption.AllDirectories).ToList();
            if (matches.Count == 0)
            {
                stale.Add(name + " (file gone)");
                continue;
            }

            var stillOffends = matches.Any(f =>
            {
                var body = ExtractRegisterBody(File.ReadAllText(f));
                return body != null && Regex.IsMatch(body, @"container\s*\.\s*Resolve");
            });

            if (!stillOffends)
                stale.Add(name + " (clean now; remove from the baseline so it cannot regress)");
        }

        Assert.AreEqual(0, stale.Count,
            "Baseline ratchet drifted:\n" + string.Join("\n", stale));
    }

    /// <summary>The text of the Register*Feature method body, or null when the file has none.
    /// Brace-counting from the method header; good enough for house-style IoC files.</summary>
    private static string ExtractRegisterBody(string text)
    {
        var m = Regex.Match(text, @"public static void Register\w+\s*\([^)]*\)\s*\{");
        if (!m.Success)
            return null;

        int depth = 1, i = m.Index + m.Length;
        int start = i;
        while (i < text.Length && depth > 0)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}') depth--;
            i++;
        }

        return text.Substring(start, Math.Max(0, i - 1 - start));
    }
}

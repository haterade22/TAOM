using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CareerSystem.UI;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Every UIExtenderEx insert patch in the TAOM assembly (Prefabs2 only) states where its node lands. Three silent mistakes are
/// caught here: <c>InsertType.Child</c> inserts BEFORE the child at <c>Index</c> and Index defaults to 0, so a Child patch without
/// its own Index lands first; a Child insert goes into the selected node's own XML children and the template loads only its
/// <c>Children</c> element, so a Child XPath that does not end in <c>/Children</c> is silently dropped; and <c>Append</c>/<c>Prepend</c>
/// insert a SIBLING of the selected node, so an XPath ending in <c>/Children</c> puts the block next to the Children element.
/// Whether an Append beside a container was meant to go inside it is intent, and stays a reading check (lessons localization-ui.md).
/// </summary>
[TestClass]
public class PrefabInsertPositionTests
{
    [PrefabExtension("Probe", "descendant::Widget[@Id='A']/Children")]
    private sealed class ChildWithoutIndex : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Child;
    }

    [PrefabExtension("Probe", "descendant::Widget[@Id='A']")]
    private sealed class ChildOnAWidget : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Child;
        public override int Index => int.MaxValue;
    }

    [PrefabExtension("Probe", "descendant::Widget[@Id='A']/Children")]
    private sealed class AppendOnChildren : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Append;
    }

    [PrefabExtension("Probe", "descendant::Widget[@Id='A']/Children")]
    private sealed class ChildLastInChildren : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Child;
        public override int Index => int.MaxValue;
    }

    // The one TAOM assembly on 1.4.x. Types that reference an engine assembly the test host cannot load
    // come back null from a partial load; a patch class references only UIExtenderEx, so it always survives.
    private static IEnumerable<Type> InsertPatches() =>
        LoadableTypes(typeof(TAOM.IoC).Assembly)
            .Where(t => !t.IsAbstract && typeof(PrefabExtensionInsertPatch).IsAssignableFrom(t));

    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }

    private static IEnumerable<string> Problems(Type patch)
    {
        var instance = (PrefabExtensionInsertPatch)Activator.CreateInstance(patch)!;
        var xpaths = patch.GetCustomAttributes<PrefabExtensionAttribute>(inherit: false).Select(a => a.XPath ?? "").ToList();

        if (instance.Type == InsertType.Child)
        {
            var inheritsIndex = patch.GetProperty(nameof(PrefabExtensionInsertPatch.Index))!.DeclaringType == typeof(PrefabExtensionInsertPatch);
            if (inheritsIndex)
                yield return $"{patch.Name}: InsertType.Child without an Index override lands FIRST (Index defaults to 0). " +
                             "Fix: add `public override int Index => int.MaxValue;` for last, or `=> 0` for first, with a comment naming the sibling.";

            foreach (var xpath in xpaths.Where(x => !x.TrimEnd().EndsWith("/Children", StringComparison.Ordinal)))
                yield return $"{patch.Name}: InsertType.Child on \"{xpath}\" lands beside the Children element, which the template never loads. " +
                             "Fix: end the XPath in /Children.";
        }
        else if (instance.Type == InsertType.Append || instance.Type == InsertType.Prepend)
        {
            foreach (var xpath in xpaths.Where(x => x.TrimEnd().EndsWith("/Children", StringComparison.Ordinal)))
                yield return $"{patch.Name}: InsertType.{instance.Type} on \"{xpath}\" inserts a SIBLING of Children, not a child. " +
                             "Fix: keep the XPath on /Children and use InsertType.Child with an explicit Index, or select a child widget to insert beside.";
        }
    }

    [TestMethod]
    public void EveryInsertPatch_LandsWhereItsInsertTypeSays()
    {
        var patches = InsertPatches().ToList();
        CollectionAssert.Contains(patches, typeof(CareerEnergyBarPrefab), "the reflection is not reading the TAOM assembly");

        var problems = patches.SelectMany(Problems).ToList();

        Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));
    }

    [TestMethod]
    public void EachRule_FailsTheMistakeItNames()
    {
        StringAssert.Contains(Problems(typeof(ChildWithoutIndex)).Single(), "lands FIRST");
        StringAssert.Contains(Problems(typeof(ChildOnAWidget)).Single(), "lands beside the Children element");
        StringAssert.Contains(Problems(typeof(AppendOnChildren)).Single(), "inserts a SIBLING of Children");
        Assert.AreEqual(0, Problems(typeof(ChildLastInChildren)).Count());
    }
}

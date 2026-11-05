using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// A Gauntlet <c>HintWidget</c> has no hover of its own: it relays its parent widget's hover events (v1.4.8 HintWidget.cs:15,
/// :31-35), and hit-testing never picks a widget with <c>DoNotAcceptEvents="true"</c> (EventManager.cs:861, :897). A hint under
/// such a parent never shows. The trap shipped three times on the 1.5.x line (#857, #898, #910; Messengers, #898, is on both
/// lines), so this gate builds the XML of every in-code PrefabExtension in TAOM and fails on a hint whose parent refuses events.
/// A hint at the root of an insert has a vanilla parent and is skipped.
/// </summary>
[TestClass]
public class PrefabHintParentGateTests
{
    private const string DocumentAttribute = "PrefabExtensionXmlDocumentAttribute";

    private static IEnumerable<(Type Type, XmlDocument Document)> Documents()
    {
        foreach (var type in LoadableTypes(typeof(TAOM.IoC).Assembly))
        {
            var method = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.CustomAttributes.Any(a => a.AttributeType.Name == DocumentAttribute));
            if (method == null || type.IsAbstract)
                continue;
            var instance = Activator.CreateInstance(type, nonPublic: true)!;
            yield return (type, (XmlDocument)method.Invoke(instance, null)!);
        }
    }

    // Some TAOM types name game assemblies the test host may not load; the extensions themselves load without them, and
    // the count floor below fails the gate if they ever stop doing so.
    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
    }

    // Gauntlet nests a widget's children in a <Children> element, so a hint's widget parent is two levels up.
    private static XmlElement? WidgetParent(XmlElement hint)
    {
        var up = hint.ParentNode as XmlElement;
        if (up != null && up.Name == "Children")
            up = up.ParentNode as XmlElement;
        return up;
    }

    [TestMethod]
    public void TheGate_ScansEveryInCodePrefabExtension()
    {
        // Five on 1.4.x at planning: CareerButtonPrefab, CareerEnergyBarPrefab, MessengerEncyclopediaPrefabExtension,
        // SpecialResourceIconPrefab, PrefabInsertExtraFastForward.
        Assert.IsTrue(Documents().Count() >= 5, "fewer in-code prefab extensions than TAOM ships: the scan found nothing to check");
    }

    [TestMethod]
    public void NoHintWidget_SitsUnderAParentThatRefusesEvents()
    {
        var bad = new List<string>();
        foreach (var (type, document) in Documents())
        {
            foreach (var hint in document.GetElementsByTagName("HintWidget").Cast<XmlElement>())
            {
                var parent = WidgetParent(hint);
                if (parent != null && string.Equals(parent.GetAttribute("DoNotAcceptEvents"), "true", StringComparison.OrdinalIgnoreCase))
                    bad.Add($"{type.FullName}: HintWidget '{hint.GetAttribute("DataSource")}' under <{parent.Name} Id=\"{parent.GetAttribute("Id")}\">");
            }
        }

        Assert.AreEqual(0, bad.Count, "a hint under a parent with DoNotAcceptEvents=\"true\" never shows:\n" + string.Join("\n", bad));
    }
}

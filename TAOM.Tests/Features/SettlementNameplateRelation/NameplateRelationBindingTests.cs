using System;
using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TAOM.Features.SettlementNameplateRelation;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.SettlementNameplateRelation;

/// <summary>
/// Pins every engine member <see cref="TaomSettlementPlateWidget"/> and the Patch38 postfix rely on
/// against the installed engine (#591). The widget reads the item widget's AlphaFactor /
/// ColorFactor and the postfix reads SettlementNameplateWidget.RelationType / IsTracked; a rename
/// on the engine side would compile (the widget) or apply (the patch) and then silently do nothing.
/// </summary>
[TestClass]
public class NameplateRelationBindingTests
{
    private const string ItemWidgetTypeName =
        "TaleWorlds.MountAndBlade.GauntletUI.Widgets.Nameplate.SettlementNameplateItemWidget";
    private const string NameplateWidgetTypeName =
        "TaleWorlds.MountAndBlade.GauntletUI.Widgets.Nameplate.SettlementNameplateWidget";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static Type RequireType(string name)
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var type = AccessTools.TypeByName(name);
        Assert.IsNotNull(type, name + " did not resolve against the installed engine.");
        return type;
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SettlementNameplateItemWidget_ResolvesAsAWidget()
    {
        var type = RequireType(ItemWidgetTypeName);
        Assert.IsTrue(typeof(Widget).IsAssignableFrom(type), ItemWidgetTypeName + " is no longer a Widget.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Widget_AlphaFactorAndColorFactor_ArePublicWritableFloats()
    {
        foreach (var name in new[] { "AlphaFactor", "ColorFactor" })
        {
            var property = AccessTools.Property(typeof(Widget), name);
            Assert.IsNotNull(property, "Widget." + name + " is gone; the plate widget mirrors it.");
            Assert.AreEqual(typeof(float), property.PropertyType, "Widget." + name + " changed type.");
            Assert.IsTrue(property.GetSetMethod() != null, "Widget." + name + " lost its public setter.");
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SettlementNameplateWidget_RelationTypeAndIsTracked_ArePublic()
    {
        var type = RequireType(NameplateWidgetTypeName);

        var relation = AccessTools.Property(type, "RelationType");
        Assert.IsNotNull(relation, "RelationType is gone; the Patch38 relation floor reads it.");
        Assert.AreEqual(typeof(int), relation.PropertyType);

        var tracked = AccessTools.Property(type, "IsTracked");
        Assert.IsNotNull(tracked, "IsTracked is gone; the Patch38 relation floor reads it.");
        Assert.AreEqual(typeof(bool), tracked.PropertyType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SettlementNameplateWidget_DetermineTargetAlphaValue_ResolvesForPatch38()
    {
        var type = RequireType(NameplateWidgetTypeName);
        var method = AccessTools.Method(type, "DetermineTargetAlphaValue", Type.EmptyTypes);
        Assert.IsNotNull(method, "DetermineTargetAlphaValue did not resolve; Patch38 would fail to apply.");
        Assert.AreEqual(typeof(float), method.ReturnType);
    }

    [TestMethod]
    public void TextAndBannerBrushes_ExposeTheMembersTheWidgetWrites()
    {
        // TextWidget: Brush (clone, written) and ReadOnlyBrush (read without cloning).
        Assert.IsNotNull(AccessTools.Property(typeof(TextWidget), "Brush"));
        Assert.IsNotNull(AccessTools.Property(typeof(TextWidget), "ReadOnlyBrush"));
        // MaskedTextureWidget: Brush lives on TextureWidget, not BrushWidget.
        Assert.IsNotNull(AccessTools.Property(typeof(MaskedTextureWidget), "Brush"));
        Assert.IsNotNull(AccessTools.Property(typeof(Brush), "GlobalAlphaFactor"));
        Assert.IsNotNull(AccessTools.Property(typeof(Brush), "FontColor"));
    }

    [TestMethod]
    public void TaomSettlementPlateWidget_HasUIContextConstructorAndUniqueSimpleName()
    {
        // WidgetFactory instantiates by (UIContext) and keys _builtinTypes on the simple name.
        var ctor = typeof(TaomSettlementPlateWidget).GetConstructor(new[] { typeof(UIContext) });
        Assert.IsNotNull(ctor, "The engine needs a public (UIContext) constructor.");

        var collisions = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return Type.EmptyTypes; }
            })
            .Where(t => t.Name == nameof(TaomSettlementPlateWidget) && t != typeof(TaomSettlementPlateWidget))
            .ToList();
        Assert.AreEqual(0, collisions.Count,
            "Another loaded Widget shares the simple name: " + string.Join(", ", collisions.Select(t => t.FullName)));
    }
}

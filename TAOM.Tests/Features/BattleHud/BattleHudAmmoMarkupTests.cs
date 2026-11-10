using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Bannerlord.UIExtenderEx.Prefabs2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleHud.UI;
using TAOM.Features.CareerSystem.UI;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.BattleHud;

// Issue #1095: the ammo capsule's markup inside the injected HUD, and the native counter it replaces.
// The count and the low ammo alert stay native: the capsule hosts the engine's own AgentAmmoTextWidget
// bound to the VM's AmmoCount and IsAmmoCountAlertEnabled, and the native counter in the weapon column
// is switched off so the number never shows twice. What no test shows is the look (the capsule under the
// rim, the icon, the alert flash): that is the preview and the in-game check.
[TestClass]
public class BattleHudAmmoMarkupTests
{
    private const string NativeAgentStatus = @"Modules\Native\GUI\Prefabs\Mission\AgentStatus.xml";

    private static XmlElement HudMarkup() => new CareerEnergyBarPrefab().GetDocument().DocumentElement!;

    private static XmlElement Capsule()
    {
        var capsule = (XmlElement?)HudMarkup().SelectSingleNode("Children/Widget[@Id='TaomBattleHudAmmo']");
        Assert.IsNotNull(capsule, "the HUD has an ammo capsule");
        return capsule!;
    }

    private static XmlElement InCapsule(string xpath)
    {
        var element = (XmlElement?)Capsule().SelectSingleNode(xpath);
        Assert.IsNotNull(element, xpath);
        return element!;
    }

    private static float Number(XmlElement element, string attribute)
    {
        var text = element.GetAttribute(attribute);
        Assert.IsTrue(text.Length > 0, $"{element.Name} has no {attribute}");
        return float.Parse(text, CultureInfo.InvariantCulture);
    }

    private static string XPathOf(Type extension) =>
        CustomAttributeData.GetCustomAttributes(extension).Single(a => a.AttributeType.Name == "PrefabExtensionAttribute")
            .ConstructorArguments[1].Value!.ToString()!;

    private static string MovieOf(Type extension) =>
        CustomAttributeData.GetCustomAttributes(extension).Single(a => a.AttributeType.Name == "PrefabExtensionAttribute")
            .ConstructorArguments[0].Value!.ToString()!;

    // ── The capsule ────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Capsule_UsesTheRuntimeSpriteAtTheGeometrySize()
    {
        var capsule = Capsule();

        Assert.AreEqual("bh_ammo_capsule", capsule.GetAttribute("Sprite"));
        Assert.AreEqual(BattleHudGeometry.AmmoCapsuleWidth, Number(capsule, "SuggestedWidth"), 0.001f);
        Assert.AreEqual(BattleHudGeometry.AmmoCapsuleHeight, Number(capsule, "SuggestedHeight"), 0.001f);
        Assert.AreEqual("Fixed", capsule.GetAttribute("WidthSizePolicy"));
        Assert.AreEqual("Fixed", capsule.GetAttribute("HeightSizePolicy"));
    }

    [TestMethod]
    public void Capsule_HangsCentredBelowTheCircleForThePresentRings()
    {
        var capsule = Capsule();

        Assert.AreEqual("Center", capsule.GetAttribute("HorizontalAlignment"), "centred on 6 o'clock");
        Assert.AreEqual("Top", capsule.GetAttribute("VerticalAlignment"), "its offset is measured from the root's top");
        Assert.AreEqual("@HudAmmoTop", capsule.GetAttribute("PositionYOffset"), "it follows the ring count, not the full HUD's bottom");
        Assert.AreEqual("@HudShowAmmo", capsule.GetAttribute("IsVisible"));
    }

    [TestMethod]
    public void Capsule_DrawsBeforeTheRings_SoTheOuterRimLiesOverItsTop()
    {
        var children = ((XmlElement)HudMarkup().SelectSingleNode("Children")!).ChildNodes.OfType<XmlElement>().ToList();
        var capsule = children.FindIndex(e => e.GetAttribute("Id") == "TaomBattleHudAmmo");
        var rings = children.FindIndex(e => e.Name == "TaomBattleHudRingWidget");

        Assert.IsTrue(capsule >= 0 && rings >= 0);
        Assert.IsTrue(capsule < rings, "the capsule must draw under the ring widget's shell rim");
    }

    [TestMethod]
    public void Capsule_IsDisplayOnly()
    {
        foreach (var element in Capsule().SelectNodes("descendant-or-self::*")!.OfType<XmlElement>().Where(e => e.Name != "Children"))
            Assert.AreEqual("true", element.GetAttribute("DoNotAcceptEvents"), $"{element.Name} would take clicks from the scene");
    }

    // ── Icons ──────────────────────────────────────────────────────────────────────────────────

    [DataTestMethod]
    [DataRow("bh_ammo_quiver", "@HudAmmoIsQuiver")]
    [DataRow("bh_ammo_javelin", "@HudAmmoIsJavelin")]
    public void Icon_IsTheApprovedSpriteShownOnlyForItsWeapon(string sprite, string visibility)
    {
        var icon = InCapsule($"Children/Widget[@Sprite='{sprite}']");

        Assert.AreEqual(visibility, icon.GetAttribute("IsVisible"));
        Assert.AreEqual(BattleHudGeometry.AmmoIconSize, Number(icon, "SuggestedWidth"), 0.001f, "square art in a square box");
        Assert.AreEqual(BattleHudGeometry.AmmoIconSize, Number(icon, "SuggestedHeight"), 0.001f);
        Assert.AreEqual("Left", icon.GetAttribute("HorizontalAlignment"));
        Assert.AreEqual("Center", icon.GetAttribute("VerticalAlignment"));
        Assert.AreEqual(BattleHudGeometry.AmmoIconInset, Number(icon, "MarginLeft"), 0.001f);
    }

    [TestMethod]
    public void Capsule_ShowsOnlyTheTwoApprovedIcons()
    {
        var sprites = Capsule().SelectNodes("descendant::*[@Sprite]")!.OfType<XmlElement>()
            .Select(e => e.GetAttribute("Sprite")).OrderBy(s => s, StringComparer.Ordinal).ToArray();

        CollectionAssert.AreEqual(new[] { "bh_ammo_javelin", "bh_ammo_quiver" }, sprites, "no other art may label a weapon");
    }

    // ── The number: the engine's counter, bound to the native values ───────────────────────────

    [TestMethod]
    public void Counter_IsTheNativeAmmoWidgetBoundToTheNativeCountAndAlert()
    {
        var counter = InCapsule("Children/AgentAmmoTextWidget");

        Assert.AreEqual("@AmmoCount", counter.GetAttribute("IntText"), "the count is native, never recalculated");
        Assert.AreEqual("@IsAmmoCountAlertEnabled", counter.GetAttribute("IsAlertEnabled"), "the low ammo alert is native");
        Assert.AreEqual("AgentAmmoCount.Text", counter.GetAttribute("Brush"), "the native counter's brush");
        Assert.AreEqual("@HudAmmoNumberOffsetX", counter.GetAttribute("PositionXOffset"));
    }

    [TestMethod]
    public void Counter_IsCentredInItsAreaAtAReadableSize()
    {
        var counter = InCapsule("Children/AgentAmmoTextWidget");
        var font = Number(counter, "Brush.FontSize");

        Assert.AreEqual("Center", counter.GetAttribute("HorizontalAlignment"));
        Assert.AreEqual("Center", counter.GetAttribute("VerticalAlignment"));
        Assert.AreEqual("Center", counter.GetAttribute("Brush.TextHorizontalAlignment"));
        Assert.AreEqual("Center", counter.GetAttribute("Brush.TextVerticalAlignment"));
        Assert.AreEqual(BattleHudGeometry.AmmoNumberWidth, Number(counter, "SuggestedWidth"), 0.001f);
        Assert.AreEqual(BattleHudGeometry.AmmoCapsuleHeight, Number(counter, "SuggestedHeight"), 0.001f);
        Assert.IsTrue(font >= 22f && font <= 24f, $"font {font}");
        Assert.IsTrue(font <= BattleHudGeometry.AmmoCapsuleHeight, "the capsule holds its font");
    }

    [TestMethod]
    public void HudMarkup_HasExactlyOneAmmoCounter()
    {
        Assert.AreEqual(1, HudMarkup().SelectNodes("descendant::AgentAmmoTextWidget")!.Count);
    }

    [TestMethod]
    public void CapsuleBindings_EveryOneResolvesOnTheMixinOrTheNativeVm()
    {
        var mixin = typeof(MissionAgentStatusBattleHudMixin);
        var vm = mixin.BaseType!.GetGenericArguments().Single();
        var bindings = Capsule().SelectNodes("descendant-or-self::*/@*")!.OfType<XmlAttribute>()
            .Where(a => a.Value.StartsWith("@", StringComparison.Ordinal)).Select(a => a.Value.Substring(1)).Distinct().ToList();

        Assert.IsTrue(bindings.Count > 0, "the capsule binds something");
        foreach (var name in bindings)
        {
            var onMixin = mixin.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (onMixin != null)
            {
                Assert.IsTrue(CustomAttributeData.GetCustomAttributes(onMixin).Any(a => a.AttributeType == typeof(TaleWorlds.Library.DataSourceProperty)),
                    $"{name} on the mixin is not a [DataSourceProperty]");
                continue;
            }
            Assert.IsNotNull(vm.GetProperty(name, BindingFlags.Public | BindingFlags.Instance), $"@{name} binds to nothing on {vm.Name} or the mixin");
        }
    }

    [TestMethod]
    public void Mixin_HoldsNoMissionOrAgentHandle()
    {
        // The wielded weapon is read fresh each refresh; a held agent goes stale across deaths and missions.
        string[] handles = { "Agent", "Mission", "MissionWeapon", "MissionEquipment", "WeaponComponentData" };
        var fields = typeof(MissionAgentStatusBattleHudMixin)
            .GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        foreach (var field in fields)
            Assert.IsFalse(handles.Contains(field.FieldType.Name), $"{field.Name} holds a {field.FieldType.Name}");
    }

    // ── The native counter is switched off, not deleted, and the patch never hits the HUD's own ──

    [TestMethod]
    public void NativeCounterPatch_HidesTheNativeCounter()
    {
        var patch = (PrefabExtensionSetAttributePatch)Activator.CreateInstance(typeof(BattleHudHideNativeAmmoCounter), true)!;

        Assert.AreEqual("AgentStatus", MovieOf(typeof(BattleHudHideNativeAmmoCounter)));
        Assert.AreEqual("false", patch.Attributes.Single(a => a.Name == "IsVisible").Value, "constant false replaces @ShowAmmoCount");
    }

    [TestMethod]
    public void NativeCounterPatch_XPath_MissesTheHudsOwnCounter()
    {
        // UIExtenderEx resolves an XPath with SelectSingleNode, and patch order within a movie is not
        // pinned, so the path must exclude the HUD's counter even with the HUD already inserted.
        var doc = new XmlDocument();
        doc.LoadXml(@"<Prefab><Window><Widget VisualDefinition=""AgentStatus""><Children>
  <ListPanel MarginBottom=""118""><Children>
    <ImageIdentifierWidget DataSource=""{PrimaryWeapon}""><Children>
      <AgentAmmoTextWidget DataSource=""{..}"" IntText=""@AmmoCount"" IsVisible=""@ShowAmmoCount"" />
    </Children></ImageIdentifierWidget>
  </Children></ListPanel>
</Children></Widget></Window></Prefab>");
        var children = doc.SelectSingleNode("/Prefab/Window/Widget/Children")!;
        children.PrependChild(doc.ImportNode(HudMarkup(), true));

        var matches = doc.SelectNodes(XPathOf(typeof(BattleHudHideNativeAmmoCounter)))!.OfType<XmlElement>().ToList();

        Assert.AreEqual(1, matches.Count);
        Assert.IsNull(matches[0].SelectSingleNode("ancestor::Widget[@Id='TaomBattleHud']"), "the patch would hide the HUD's own counter");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void OnlyTheHidePatch_TouchesAnAmmoCounter()
    {
        // The old patch that moved the native counter beside the HUD must go: it would bring a second number back.
        // GetTypes on every TAOM assembly resolves module and engine references, so load them first.
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game install unavailable: " + string.Join("; ", GameAssemblies.Diagnostics));
        var touching = TaomAssemblies.All.SelectMany(a => a.GetTypes())
            .Where(t => CustomAttributeData.GetCustomAttributes(t)
                .Where(a => a.AttributeType.Name == "PrefabExtensionAttribute" && a.ConstructorArguments.Count > 1)
                .Any(a => Equals(a.ConstructorArguments[0].Value, "AgentStatus")
                    && (a.ConstructorArguments[1].Value as string ?? "").Contains("AgentAmmoTextWidget")))
            .ToList();

        CollectionAssert.AreEqual(new[] { typeof(BattleHudHideNativeAmmoCounter) }, touching);
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void NativeCounterPatch_XPath_MatchesTheInstalledCounterExactlyOnce()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game install unavailable: " + string.Join("; ", GameAssemblies.Diagnostics));
        var path = Path.Combine(GameAssemblies.GameDir, NativeAgentStatus);
        if (!File.Exists(path))
            Assert.Inconclusive("AgentStatus.xml not found at " + path);
        var doc = new XmlDocument();
        doc.Load(path);

        var matches = doc.SelectNodes(XPathOf(typeof(BattleHudHideNativeAmmoCounter)))!.OfType<XmlElement>().ToList();

        Assert.AreEqual(1, matches.Count);
        Assert.AreEqual("@AmmoCount", matches[0].GetAttribute("IntText"));
        Assert.AreEqual("@IsAmmoCountAlertEnabled", matches[0].GetAttribute("IsAlertEnabled"), "the native alert binding the capsule copies");
        Assert.AreEqual("AgentAmmoCount.Text", matches[0].GetAttribute("Brush"), "the native brush the capsule copies");
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.FieldCamp;

/// <summary>
/// The camp overlay's GauntletLayer order against the installed map layers (#704). ScreenManager sorts
/// screen and global layers by order and draws in that order, so a camp layer below the map bar is
/// painted over by it: the themed Camp icon and divider sit inside the map bar's area and vanished in
/// game. The camp layer must draw above the map bar and below map conversations (which must still
/// cover it). Every order is read from IL without running a view: the constants passed straight into
/// the one GauntletLayer constructor in each method. Relational on purpose, so an engine change to
/// either native order fails here instead of silently hiding or exposing the icon.
/// </summary>
[TestClass]
[TestCategory("RequiresGameIL")]
public class FieldCampLayerOrderTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static MethodBase RequireMethod(string typeName, string methodName)
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var type = AccessTools.TypeByName(typeName);
        Assert.IsNotNull(type, typeName + " did not resolve against the installed engine or TAOM.Client.");
        var method = AccessTools.Method(type, methodName);
        Assert.IsNotNull(method, $"{typeName}.{methodName} did not resolve.");
        return method;
    }

    /// <summary>The (name, order) of the single <c>new GauntletLayer(name, order[, bool])</c> in
    /// <paramref name="method"/>, read from the literals pushed immediately before it. Fails when there
    /// is no such construction, more than one, or an argument that is not a literal.</summary>
    private static (string Name, int Order) LayerOrder(MethodBase method)
    {
        var body = PatchProcessor.ReadMethodBody(method).ToList();
        var constructions = body.Select((pair, index) => (pair, index))
            .Where(x => x.pair.Key == OpCodes.Newobj && x.pair.Value is ConstructorInfo ctor
                        && ctor.DeclaringType?.Name == "GauntletLayer")
            .ToList();
        string where = $"{method.DeclaringType?.FullName}.{method.Name}";
        Assert.AreEqual(1, constructions.Count, $"{where}: expected exactly one GauntletLayer construction");

        var (newobj, at) = constructions[0];
        var parameters = ((ConstructorInfo)newobj.Value).GetParameters();
        Assert.IsTrue(parameters.Length >= 2 && parameters.Length <= 3
                      && parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == typeof(int)
                      && (parameters.Length == 2 || parameters[2].ParameterType == typeof(bool)),
            $"{where}: GauntletLayer constructor shape changed: ({string.Join(", ", parameters.Select(p => p.ParameterType.Name))})");

        int orderIndex = at - (parameters.Length - 1);
        int nameIndex = orderIndex - 1;
        Assert.IsTrue(nameIndex >= 0, $"{where}: GauntletLayer arguments are not in this method's IL");
        if (parameters.Length == 3)
            Assert.IsTrue(TryInt(body[at - 1], out _), $"{where}: the shouldClear argument is not a literal");
        Assert.IsTrue(TryInt(body[orderIndex], out int order), $"{where}: the layer order is not a literal ({body[orderIndex].Key})");
        Assert.AreEqual(OpCodes.Ldstr, body[nameIndex].Key, $"{where}: the layer name is not a literal");
        return ((string)body[nameIndex].Value, order);
    }

    private static bool TryInt(KeyValuePair<OpCode, object> pair, out int value)
    {
        var op = pair.Key;
        if (op == OpCodes.Ldc_I4) { value = (int)pair.Value; return true; }
        if (op == OpCodes.Ldc_I4_S) { value = System.Convert.ToInt32(pair.Value); return true; }
        var shortForms = new[]
        {
            OpCodes.Ldc_I4_M1, OpCodes.Ldc_I4_0, OpCodes.Ldc_I4_1, OpCodes.Ldc_I4_2, OpCodes.Ldc_I4_3,
            OpCodes.Ldc_I4_4, OpCodes.Ldc_I4_5, OpCodes.Ldc_I4_6, OpCodes.Ldc_I4_7, OpCodes.Ldc_I4_8,
        };
        int i = System.Array.IndexOf(shortForms, op);
        value = i - 1;
        return i >= 0;
    }

    private static (string Name, int Order) Camp() =>
        LayerOrder(RequireMethod("TAOM.Features.FieldCamp.UI.FieldCampMapView", "CreateLayout"));

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CampOverlayLayer_DrawsAboveTheMapBar()
    {
        var camp = Camp();
        var mapBar = LayerOrder(RequireMethod("SandBox.GauntletUI.Map.GauntletMapBarGlobalLayer", "Initialize"));

        Assert.IsTrue(camp.Order > mapBar.Order,
            $"camp layer '{camp.Name}' at {camp.Order} is drawn under map bar layer '{mapBar.Name}' at {mapBar.Order}; the Camp icon beside the clock is hidden");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CampOverlayLayer_DrawsBelowMapConversations()
    {
        var camp = Camp();
        var conversation = LayerOrder(RequireMethod("SandBox.GauntletUI.Map.GauntletMapConversationView", "CreateConversationView"));

        Assert.IsTrue(camp.Order < conversation.Order,
            $"camp layer '{camp.Name}' at {camp.Order} draws over map conversation layer '{conversation.Name}' at {conversation.Order}");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CampOverlayLayer_DoesNotShareTheMapIncidentOrder()
    {
        var camp = Camp();
        var incidents = LayerOrder(RequireMethod("SandBox.GauntletUI.Map.GauntletMapIncidentView", "CreateLayout"));

        Assert.AreNotEqual(incidents.Order, camp.Order,
            $"camp layer '{camp.Name}' shares order {camp.Order} with '{incidents.Name}', so the engine's tie-break (by layer id, not by intent) decides which draws on top");
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.Warg;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Warg;

[TestClass]
public class WargRageLogTests
{
    private static WargRageLog NewLog() => new(Substitute.For<IModLogger>(), () => 0.0);

    private static object Rider() => new();

    private static object Form() => new();

    private sealed class Scene
    {
        public double Now;
        public WargRageLog.RiderView View = new(true, true, true, false, 0f, 10f, 0f);
        public WargRageLog Log = null!;
        public IModLogger Logger = Substitute.For<IModLogger>();

        public static Scene Create()
        {
            var scene = new Scene();
            scene.Log = new WargRageLog(scene.Logger, () => scene.Now, (_, _) => scene.View);
            return scene;
        }
    }

    [TestCleanup]
    public void Clear()
    {
        var field = typeof(WargRageLog).GetField("_active", BindingFlags.NonPublic | BindingFlags.Static)!;
        WargRageLog.Deactivate((WargRageLog?)field.GetValue(null));
    }

    [TestMethod]
    public void Outcome_ClassifiesRiderAndFormation()
    {
        Assert.AreEqual("rider-inactive", WargRageLog.Outcome(false, true));
        Assert.AreEqual("rider-inactive", WargRageLog.Outcome(false, false));
        Assert.AreEqual("null-formation", WargRageLog.Outcome(true, false));
        Assert.AreEqual("restored", WargRageLog.Outcome(true, true));
    }

    [TestMethod]
    public void HasReturned_UsesTheLargerOf15AndHalfWidthPlus5_PlusTheFormationDepth()
    {
        Assert.IsTrue(WargRageLog.HasReturned(15f, 10f, 0f), "a narrow formation keeps the 15 m floor");
        Assert.IsFalse(WargRageLog.HasReturned(15.1f, 10f, 0f));
        Assert.IsTrue(WargRageLog.HasReturned(30f, 50f, 0f), "50 m wide: half the width plus 5 m is 30 m");
        Assert.IsFalse(WargRageLog.HasReturned(30.1f, 50f, 0f));
        Assert.IsTrue(WargRageLog.HasReturned(40f, 10f, 25f), "a rider at the rear of a 25 m deep formation is 25 m further back");
        Assert.IsFalse(WargRageLog.HasReturned(40.1f, 10f, 25f));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Active_IsNullUntilActivated_AndClearedByDeactivate()
    {
        GameAssemblies.EnsureLoaded();
        var log = NewLog();
        Assert.IsNull(WargRageLog.Active);

        WargRageLog.Activate(log, Mission.Current);
        Assert.AreSame(log, WargRageLog.Active, "Mission.Current is the activating mission");

        WargRageLog.Deactivate(NewLog());
        Assert.AreSame(log, WargRageLog.Active, "another log cannot deactivate this one");

        WargRageLog.Deactivate(log);
        Assert.IsNull(WargRageLog.Active);

        WargRageLog.Activate(log, new object());
        Assert.IsNull(WargRageLog.Active, "a log activated for another mission is not active here");
    }

    [TestMethod]
    public void Counts_StartAtZero()
    {
        var c = NewLog().Counts();

        Assert.AreEqual(0, c.Started + c.Restored + c.NullFormation + c.RiderInactive + c.Stranded + c.Returned + c.NotReturned + c.Superseded + c.Open);
        Assert.AreEqual(0f, c.MaxReturnSeconds);
    }

    [TestMethod]
    public void Ended_ARiderWithAFormation_IsRestored_AndIgnoredWithoutAnEpisode()
    {
        var scene = Scene.Create();
        var rider = Rider();
        scene.Log.Ended(rider, Form());
        Assert.AreEqual(0, scene.Log.Counts().Restored, "no open episode");

        scene.Log.Started(rider, false);
        scene.Log.Ended(rider, Form());

        var c = scene.Log.Counts();
        Assert.AreEqual(1, c.Started);
        Assert.AreEqual(1, c.Restored);
        Assert.AreEqual(1, c.Open);
    }

    [TestMethod]
    public void Ended_ARiderThatIsNoLongerActive_IsClosedAsRiderInactive()
    {
        var scene = Scene.Create();
        var rider = Rider();
        scene.Log.Started(rider, true);
        scene.View = new WargRageLog.RiderView(false, false, false, false, 0f, 0f, 0f);

        scene.Log.Ended(rider, Form());

        var c = scene.Log.Counts();
        Assert.AreEqual(1, c.RiderInactive);
        Assert.AreEqual(0, c.Restored);
        Assert.AreEqual(0, c.Open);
    }

    [TestMethod]
    public void Ended_WithNoFormationToGiveBack_IsClosedAsNullFormation()
    {
        var scene = Scene.Create();
        var rider = Rider();
        scene.Log.Started(rider, false);

        scene.Log.Ended(rider, null);

        var c = scene.Log.Counts();
        Assert.AreEqual(1, c.NullFormation);
        Assert.AreEqual(0, c.Restored);
        Assert.AreEqual(0, c.Open);
    }

    [TestMethod]
    public void Ended_ANullRider_IsIgnored()
    {
        var scene = Scene.Create();
        scene.Log.Started(Rider(), true);

        scene.Log.Ended(null, Form());

        Assert.AreEqual(1, scene.Log.Counts().Open);
        Assert.AreEqual(0, scene.Log.Counts().Restored);
    }

    [TestMethod]
    public void Started_ASecondRageOnARestoredRider_CountsTheFirstEpisodeAsSuperseded()
    {
        var scene = Scene.Create();
        var rider = Rider();
        scene.Log.Started(rider, true);
        scene.Log.Ended(rider, Form());

        scene.Log.Started(rider, true);

        var c = scene.Log.Counts();
        Assert.AreEqual(2, c.Started);
        Assert.AreEqual(1, c.Restored);
        Assert.AreEqual(1, c.Superseded);
        Assert.AreEqual(1, c.Open, "the new episode replaces the old one");

        scene.Log.Ended(rider, Form());
        Assert.AreEqual(2, scene.Log.Counts().Restored, "the second episode is restorable again");
        Assert.AreEqual(1, scene.Log.Counts().Superseded);
    }

    [TestMethod]
    public void Started_ASecondRageOnARiderStillAwaitingItsReset_IsNotSuperseded()
    {
        var scene = Scene.Create();
        var rider = Rider();
        scene.Log.Started(rider, true);

        scene.Log.Started(rider, true);

        Assert.AreEqual(0, scene.Log.Counts().Superseded);
        Assert.AreEqual(1, scene.Log.Counts().Open);
    }

    [TestMethod]
    public void Watch_ARestoredRiderBackInPlace_IsReturned_AndClosed()
    {
        var scene = Scene.Create();
        var rider = Rider();
        var form = Form();
        scene.Log.Started(rider, true);
        scene.Now = 10;
        scene.Log.Ended(rider, form);
        scene.View = new WargRageLog.RiderView(true, true, true, true, 3f, 10f, 0f);
        scene.Now = 14;

        scene.Log.Watch();

        var c = scene.Log.Counts();
        Assert.AreEqual(1, c.Returned);
        Assert.AreEqual(4f, c.MaxReturnSeconds, 0.001f);
        Assert.AreEqual(0, c.Open);
    }

    [TestMethod]
    public void Watch_ARiderAtTheRearOfADeepFormation_IsReturned()
    {
        var scene = Scene.Create();
        var rider = Rider();
        scene.Log.Started(rider, true);
        scene.Log.Ended(rider, Form());
        scene.View = new WargRageLog.RiderView(true, true, true, true, 38f, 10f, 25f);

        scene.Log.Watch();

        Assert.AreEqual(1, scene.Log.Counts().Returned);
    }

    [TestMethod]
    public void Watch_AWallClockPause_IsNotANotReturned_ButSixtyMissionSecondsAre()
    {
        var scene = Scene.Create();
        var rider = Rider();
        var form = Form();
        scene.Log.Started(rider, true);
        scene.Now = 10;
        scene.Log.Ended(rider, form);
        scene.View = new WargRageLog.RiderView(true, true, true, true, 200f, 10f, 0f);

        scene.Log.Watch();
        Assert.AreEqual(0, scene.Log.Counts().NotReturned, "mission time did not advance");

        scene.Now = 70;
        scene.Log.Watch();
        Assert.AreEqual(1, scene.Log.Counts().NotReturned);
        Assert.AreEqual(0, scene.Log.Counts().Open);
    }

    [TestMethod]
    public void Watch_ARestoredRiderInAnotherFormation_IsClosedSilently()
    {
        var scene = Scene.Create();
        var rider = Rider();
        scene.Log.Started(rider, true);
        scene.Log.Ended(rider, Form());
        scene.View = new WargRageLog.RiderView(true, true, true, false, 3f, 10f, 0f);

        scene.Log.Watch();

        var c = scene.Log.Counts();
        Assert.AreEqual(0, c.Returned);
        Assert.AreEqual(0, c.NotReturned);
        Assert.AreEqual(0, c.Open);
    }

    [TestMethod]
    public void Watch_AnUnrestoredRiderThatDied_IsClosedAsRiderInactive()
    {
        var scene = Scene.Create();
        scene.Log.Started(Rider(), false);
        scene.View = new WargRageLog.RiderView(false, false, false, false, 0f, 0f, 0f);

        scene.Log.Watch();

        var c = scene.Log.Counts();
        Assert.AreEqual(1, c.RiderInactive);
        Assert.AreEqual(0, c.Open);
    }

    [TestMethod]
    public void Watch_AnUnrestoredRiderOnFootWithNoFormation_IsStrandedOnce()
    {
        var scene = Scene.Create();
        scene.Log.Started(Rider(), false);
        scene.View = new WargRageLog.RiderView(true, false, false, false, 0f, 0f, 0f);

        scene.Log.Watch();
        scene.Log.Watch();

        var c = scene.Log.Counts();
        Assert.AreEqual(1, c.Stranded);
        Assert.AreEqual(0, c.Open);
    }

    [TestMethod]
    public void Watch_OneLookOnFoot_IsNotStrandedYet_AndALateResetRestoresIt()
    {
        var scene = Scene.Create();
        var rider = Rider();
        scene.Log.Started(rider, false);
        scene.View = new WargRageLog.RiderView(true, false, false, false, 0f, 0f, 0f);

        scene.Log.Watch();
        Assert.AreEqual(0, scene.Log.Counts().Stranded, "the warg-death reset can reach the rider a tick after the dismount");
        Assert.AreEqual(1, scene.Log.Counts().Open);

        scene.Log.Ended(rider, Form());
        scene.Log.Watch();

        var c = scene.Log.Counts();
        Assert.AreEqual(0, c.Stranded);
        Assert.AreEqual(1, c.Restored);
    }

    [TestMethod]
    public void Watch_AnUnrestoredRiderStillOnItsWarg_StaysOpen()
    {
        var scene = Scene.Create();
        scene.Log.Started(Rider(), false);

        scene.Log.Watch();

        var c = scene.Log.Counts();
        Assert.AreEqual(0, c.Stranded);
        Assert.AreEqual(0, c.RiderInactive);
        Assert.AreEqual(1, c.Open);
    }

    [TestMethod]
    public void Deactivate_AlsoForgetsTheMission()
    {
        var log = NewLog();
        var mission = new object();
        WargRageLog.Activate(log, mission);
        WargRageLog.Deactivate(log);
        WargRageLog.Activate(log, mission);
        WargRageLog.Deactivate(new WargRageLog(Substitute.For<IModLogger>(), () => 0.0));
        var field = typeof(WargRageLog).GetField("_activeMission", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.AreSame(mission, field.GetValue(null), "another log cannot clear it");

        WargRageLog.Deactivate(log);

        Assert.IsNull(field.GetValue(null));
    }

    [TestMethod]
    public void TreeEdits_CallTheLogOnlyThroughActive()
    {
        var prepare = Regex.Replace(RepoPaths.ReadSource("Main/Features/Warg/BehaviorTreeElements/PrepareOnFirstCall.cs", stripComments: true), @"\s+", " ");
        var clean = Regex.Replace(RepoPaths.ReadSource("Main/Features/Warg/BehaviorTreeElements/CleanIfEnemyDied.cs", stripComments: true), @"\s+", " ");

        var started = prepare.IndexOf("WargRageLog.Active?.Started(rider, rider.Formation != null); rider.Formation = null;", StringComparison.Ordinal);
        Assert.IsTrue(started >= 0, "the rage start is logged right before the formation is taken away");
        var ended = clean.IndexOf("ResetAiControlledAgent(Agent rider, Formation formation) { WargRageLog.Active?.Ended(rider, formation);", StringComparison.Ordinal);
        Assert.IsTrue(ended >= 0, "the reset logs first, before its early return");
        Assert.AreEqual(1, Regex.Matches(prepare, "WargRageLog").Count);
        Assert.AreEqual(1, Regex.Matches(clean, "WargRageLog").Count);
    }

    [TestMethod]
    public void TheLog_NamesNoEngineTypeInItsFieldsPropertiesOrSignatures()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var offenders = new List<string>();
        var types = new List<Type> { typeof(WargRageLog) };
        types.AddRange(typeof(WargRageLog).GetNestedTypes(all));

        foreach (var type in types)
        {
            foreach (var f in type.GetFields(all))
                Check(type, "field " + f.Name, f.FieldType, offenders);
            foreach (var p in type.GetProperties(all))
                Check(type, "property " + p.Name, p.PropertyType, offenders);
            foreach (var m in type.GetMethods(all))
            {
                Check(type, "return of " + m.Name, m.ReturnType, offenders);
                foreach (var parameter in m.GetParameters())
                    Check(type, m.Name + "(" + parameter.Name + ")", parameter.ParameterType, offenders);
            }

            foreach (var ctor in type.GetConstructors(all))
                foreach (var parameter in ctor.GetParameters())
                    Check(type, ".ctor(" + parameter.Name + ")", parameter.ParameterType, offenders);
        }

        Assert.AreEqual(0, offenders.Count, "ADR-007: engine types belong in an adapter, not a decision class: " + string.Join("; ", offenders));
    }

    private static void Check(Type owner, string what, Type type, List<string> offenders)
    {
        if (type.IsByRef || type.IsArray || type.IsPointer)
        {
            Check(owner, what, type.GetElementType()!, offenders);
            return;
        }

        if (type.Assembly.GetName().Name!.StartsWith("TaleWorlds", StringComparison.Ordinal))
            offenders.Add(owner.Name + " " + what + " is " + type.FullName);

        if (type.IsGenericType)
            foreach (var argument in type.GetGenericArguments())
                Check(owner, what, argument, offenders);
    }
}

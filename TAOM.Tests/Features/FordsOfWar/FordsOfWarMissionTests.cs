using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.MountAndBlade;
using TAOM.Features.FordsOfWar;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.FordsOfWar;

[TestClass]
[TestCategory("RequiresGame")]
public class FordsOfWarMissionTests
{
    private const BindingFlags Any = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void MissionName_IsThePinnedName()
    {
        Assert.AreEqual("TaomFordsOfWar", FordsOfWarMission.MissionName);
    }

    [TestMethod]
    public void IsActive_NullMission_IsFalse()
    {
        Assert.IsFalse(FordsOfWarMission.IsActive(null));
    }

    [TestMethod]
    public void IsActive_NoMissionState_IsFalse()
    {
        var mission = (Mission)FormatterServices.GetUninitializedObject(typeof(Mission));

        Assert.IsFalse(FordsOfWarMission.IsActive(mission));
    }

    [TestMethod]
    public void IsActive_StateNamedForFordsHoldingThisMission_IsTrue()
    {
        var mission = (Mission)FormatterServices.GetUninitializedObject(typeof(Mission));

        using (new FakeMissionState(mission, FordsOfWarMission.MissionName))
            Assert.IsTrue(FordsOfWarMission.IsActive(mission));
    }

    [TestMethod]
    public void IsActive_StateNamedForAnotherMission_IsFalse()
    {
        var mission = (Mission)FormatterServices.GetUninitializedObject(typeof(Mission));

        using (new FakeMissionState(mission, "CustomBattle"))
            Assert.IsFalse(FordsOfWarMission.IsActive(mission));
    }

    [TestMethod]
    public void IsActive_StateHoldingADifferentMission_IsFalse()
    {
        var mission = (Mission)FormatterServices.GetUninitializedObject(typeof(Mission));
        var other = (Mission)FormatterServices.GetUninitializedObject(typeof(Mission));

        using (new FakeMissionState(other, FordsOfWarMission.MissionName))
            Assert.IsFalse(FordsOfWarMission.IsActive(mission));
    }

    /// <summary>
    /// A real <c>MissionState</c> object with its auto-property fields written by reflection and put in the static
    /// <c>MissionState.Current</c>, then taken out again on dispose. The engine builds one only inside a game.
    /// </summary>
    internal sealed class FakeMissionState : System.IDisposable
    {
        private readonly FieldInfo _current = Field("<Current>k__BackingField");
        private readonly object? _previous;

        public FakeMissionState(Mission mission, string name)
        {
            var state = FormatterServices.GetUninitializedObject(typeof(MissionState));
            Field("<CurrentMission>k__BackingField").SetValue(state, mission);
            Field("<MissionName>k__BackingField").SetValue(state, name);
            _previous = _current.GetValue(null);
            _current.SetValue(null, state);
        }

        public void Dispose() => _current.SetValue(null, _previous);

        private static FieldInfo Field(string name) =>
            typeof(MissionState).GetField(name, Any)!;
    }
}

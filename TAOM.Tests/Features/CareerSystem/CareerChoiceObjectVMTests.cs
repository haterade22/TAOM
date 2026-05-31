using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CareerSystem.UI;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
public class CareerChoiceObjectVMTests
{
    private static CareerChoiceDefinition Choice() => new CareerChoiceDefinition(
        id: "c1", groupId: "g1", type: ChoiceType.Passive,
        description: "desc", iconSprite: "icon", passive: null, mutations: null);

    [TestMethod]
    public void IsUnavailable_NotTakenNotFree_ReturnsTrue()
    {
        var vm = new CareerChoiceObjectVM(Choice(), isTaken: false, isFreeToTake: false);
        Assert.IsTrue(vm.IsUnavailable);
        Assert.IsFalse(vm.IsTaken);
        Assert.IsFalse(vm.IsFreeToTake);
    }

    [TestMethod]
    public void IsUnavailable_Taken_ReturnsFalse()
    {
        var vm = new CareerChoiceObjectVM(Choice(), isTaken: true, isFreeToTake: false);
        Assert.IsFalse(vm.IsUnavailable);
    }

    [TestMethod]
    public void IsUnavailable_FreeToTake_ReturnsFalse()
    {
        var vm = new CareerChoiceObjectVM(Choice(), isTaken: false, isFreeToTake: true);
        Assert.IsTrue(vm.IsFreeToTake);
        Assert.IsFalse(vm.IsUnavailable);
    }

    [TestMethod]
    public void IsUnavailable_TakenAndFreeRequested_TreatedAsTakenNotUnavailable()
    {
        // ctor coerces isFreeToTake to (isFreeToTake && !isTaken); taken wins.
        var vm = new CareerChoiceObjectVM(Choice(), isTaken: true, isFreeToTake: true);
        Assert.IsTrue(vm.IsTaken);
        Assert.IsFalse(vm.IsFreeToTake);
        Assert.IsFalse(vm.IsUnavailable);
    }
}

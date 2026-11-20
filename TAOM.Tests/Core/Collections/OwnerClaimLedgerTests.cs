using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Core.Collections;

namespace TAOM.Tests.Core.Collections;

[TestClass]
public class OwnerClaimLedgerTests
{
    private const int Marshal = 1;
    private const int Stamina = 2;

    [TestMethod]
    public void Claim_FirstOwner_SaysSwitchOn()
    {
        var ledger = new OwnerClaimLedger<object>();

        Assert.IsTrue(ledger.Claim(new object(), Marshal));
    }

    [TestMethod]
    public void Claim_SecondOwnerOnTheSameKey_DoesNotSwitchOnAgain()
    {
        var ledger = new OwnerClaimLedger<object>();
        var soldier = new object();
        ledger.Claim(soldier, Marshal);

        Assert.IsFalse(ledger.Claim(soldier, Stamina));
        Assert.IsTrue(ledger.Holds(soldier, Marshal));
        Assert.IsTrue(ledger.Holds(soldier, Stamina));
    }

    [TestMethod]
    public void Claim_SameOwnerTwice_DoesNotSwitchOnAgain()
    {
        var ledger = new OwnerClaimLedger<object>();
        var soldier = new object();
        ledger.Claim(soldier, Stamina);

        Assert.IsFalse(ledger.Claim(soldier, Stamina));
    }

    [TestMethod]
    public void Release_OneOfTwoOwners_KeepsTheSwitchOn()
    {
        // The defect this type exists for: Marshal's Word let a soldier go and switched the callback off while
        // stamina still needed it.
        var ledger = new OwnerClaimLedger<object>();
        var soldier = new object();
        ledger.Claim(soldier, Marshal);
        ledger.Claim(soldier, Stamina);

        Assert.IsFalse(ledger.Release(soldier, Marshal));
        Assert.IsFalse(ledger.Holds(soldier, Marshal));
        Assert.IsTrue(ledger.Holds(soldier, Stamina));
    }

    [TestMethod]
    public void Release_LastOwner_SaysSwitchOffAndForgetsTheKey()
    {
        var ledger = new OwnerClaimLedger<object>();
        var soldier = new object();
        ledger.Claim(soldier, Marshal);
        ledger.Claim(soldier, Stamina);
        ledger.Release(soldier, Marshal);

        Assert.IsTrue(ledger.Release(soldier, Stamina));
        Assert.AreEqual(0, ledger.Count);
    }

    [TestMethod]
    public void Release_OwnerWithoutAClaim_ChangesNothing()
    {
        var ledger = new OwnerClaimLedger<object>();
        var soldier = new object();
        ledger.Claim(soldier, Marshal);

        Assert.IsFalse(ledger.Release(soldier, Stamina));
        Assert.IsFalse(ledger.Release(new object(), Marshal));
        Assert.IsTrue(ledger.Holds(soldier, Marshal));
    }

    [TestMethod]
    public void Keys_CompareByReference_NotByEquals()
    {
        var ledger = new OwnerClaimLedger<string>();
        var first = new string('a', 3);
        var second = new string('a', 3);
        ledger.Claim(first, Marshal);

        Assert.IsFalse(ledger.Holds(second, Marshal));
        Assert.IsTrue(ledger.Claim(second, Marshal));
    }

    [TestMethod]
    public void DropOwner_RemovesOnlyThatOwnersClaims()
    {
        var ledger = new OwnerClaimLedger<object>();
        var both = new object();
        var staminaOnly = new object();
        var marshalOnly = new object();
        ledger.Claim(both, Marshal);
        ledger.Claim(both, Stamina);
        ledger.Claim(staminaOnly, Stamina);
        ledger.Claim(marshalOnly, Marshal);

        ledger.DropOwner(Stamina);

        Assert.IsTrue(ledger.Holds(both, Marshal));
        Assert.IsFalse(ledger.Holds(both, Stamina));
        Assert.IsFalse(ledger.Holds(staminaOnly, Stamina));
        Assert.IsTrue(ledger.Holds(marshalOnly, Marshal));
        Assert.AreEqual(2, ledger.Count);
    }
}

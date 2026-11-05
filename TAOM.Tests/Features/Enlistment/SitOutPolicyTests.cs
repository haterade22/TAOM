using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.Enlistment;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// #830: the pure sit-out decision. The invariant under every row: a Refused verdict is only
/// possible while the Attack option is available, so the player never loses his last enabled way
/// out of an open encounter.
/// </summary>
[TestClass]
public class SitOutPolicyTests
{
    private const float Threshold = 0.85f;

    private static SitOutSnapshot Healthy(
        bool? attack = true, bool? siege = false, float? share = 0.5f, bool? wounded = false) =>
        new SitOutSnapshot(wounded, attack, siege, share);

    [TestMethod]
    public void Decide_Wounded_IsWoundedFree()
    {
        Assert.AreEqual(SitOutVerdict.WoundedFree, SitOutPolicy.Decide(Healthy(wounded: true), Threshold));
    }

    [TestMethod]
    public void Decide_WoundedWithEveryOtherFieldNull_IsStillWoundedFree()
    {
        var s = new SitOutSnapshot(true, null, null, null);
        Assert.AreEqual(SitOutVerdict.WoundedFree, SitOutPolicy.Decide(s, Threshold));
    }

    [TestMethod]
    public void Decide_WoundedInASiege_IsWoundedFree()
    {
        Assert.AreEqual(SitOutVerdict.WoundedFree, SitOutPolicy.Decide(Healthy(wounded: true, siege: true), Threshold));
    }

    [TestMethod]
    public void Decide_AttackUnavailable_IsForcedOpen()
    {
        Assert.AreEqual(SitOutVerdict.ForcedOpen, SitOutPolicy.Decide(Healthy(attack: false), Threshold));
    }

    [TestMethod]
    public void Decide_AttackUnavailableEvenInASiegeOrOverwhelming_IsForcedOpen()
    {
        Assert.AreEqual(SitOutVerdict.ForcedOpen, SitOutPolicy.Decide(Healthy(attack: false, siege: true, share: 0.99f), Threshold));
    }

    [TestMethod]
    public void Decide_Siege_IsRefusedSiege()
    {
        Assert.AreEqual(SitOutVerdict.RefusedSiege, SitOutPolicy.Decide(Healthy(siege: true, share: 0.99f), Threshold));
    }

    [DataTestMethod]
    [DataRow(0.0f, SitOutVerdict.RefusedField)]
    [DataRow(0.5f, SitOutVerdict.RefusedField)]
    [DataRow(0.84f, SitOutVerdict.RefusedField)]
    [DataRow(0.8499f, SitOutVerdict.RefusedField)]
    [DataRow(0.85f, SitOutVerdict.OverwhelmingCost)]
    [DataRow(0.8501f, SitOutVerdict.OverwhelmingCost)]
    [DataRow(0.95f, SitOutVerdict.OverwhelmingCost)]
    [DataRow(1.0f, SitOutVerdict.OverwhelmingCost)]
    public void Decide_FieldShare_SplitsAtTheThreshold(float share, SitOutVerdict expected)
    {
        Assert.AreEqual(expected, SitOutPolicy.Decide(Healthy(share: share), Threshold));
    }

    [TestMethod]
    public void Decide_NaNShare_IsNotOverwhelming()
    {
        var verdict = SitOutPolicy.Decide(Healthy(share: float.NaN), Threshold);
        Assert.AreEqual(SitOutVerdict.RefusedField, verdict, "a NaN share must never buy a free sit-out");
    }

    [TestMethod]
    public void Decide_NaNThreshold_IsNeverOverwhelming()
    {
        var verdict = SitOutPolicy.Decide(Healthy(share: 1f), float.NaN);
        Assert.AreEqual(SitOutVerdict.RefusedField, verdict);
    }

    [DataTestMethod]
    [DataRow(1.5f)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void Decide_ShareOutsideZeroToOne_IsRefusedField(float share)
    {
        // A share past 1 is a corrupt read, never a clear favourite.
        Assert.AreEqual(SitOutVerdict.RefusedField, SitOutPolicy.Decide(Healthy(share: share), Threshold));
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void Decide_AnyNeededFieldNull_IsUnchanged(int nullIndex)
    {
        // A healthy full snapshot reaches the share test, so every field is needed.
        bool? wounded = nullIndex == 0 ? null : false;
        bool? attack = nullIndex == 1 ? null : true;
        bool? siege = nullIndex == 2 ? null : false;
        float? share = nullIndex == 3 ? null : 0.5f;

        var verdict = SitOutPolicy.Decide(new SitOutSnapshot(wounded, attack, siege, share), Threshold);

        Assert.AreEqual(SitOutVerdict.Unchanged, verdict);
    }

    [TestMethod]
    public void Decide_DefaultSnapshot_IsUnchanged()
    {
        Assert.AreEqual(SitOutVerdict.Unchanged, SitOutPolicy.Decide(default, Threshold));
    }

    [TestMethod]
    public void Decide_EverySnapshot_RefusedImpliesAttackAvailableAndNeverReEnables()
    {
        bool?[] tri = { null, true, false };
        float?[] shares = { null, 0f, 0.5f, 0.8499f, 0.85f, 1f, float.NaN, float.PositiveInfinity };
        var checkedRows = 0;

        foreach (var wounded in tri)
        foreach (var attack in tri)
        foreach (var siege in tri)
        foreach (var share in shares)
        {
            var s = new SitOutSnapshot(wounded, attack, siege, share);
            var v = SitOutPolicy.Decide(s, Threshold);
            checkedRows++;

            var refused = v == SitOutVerdict.RefusedField || v == SitOutVerdict.RefusedSiege;
            if (refused)
                Assert.IsTrue(attack == true, $"refused without a proven-available Attack: {Describe(s)} -> {v}");
            if (attack == false)
                Assert.IsTrue(v == SitOutVerdict.ForcedOpen || v == SitOutVerdict.WoundedFree || v == SitOutVerdict.Unchanged,
                    $"Attack unavailable must never restrict Send troops: {Describe(s)} -> {v}");
            if (v == SitOutVerdict.OverwhelmingCost)
                Assert.IsTrue(wounded == false && attack == true && siege == false && share.HasValue && share.Value >= Threshold,
                    $"OverwhelmingCost outside its one legal row: {Describe(s)}");
        }

        Assert.AreEqual(3 * 3 * 3 * 8, checkedRows);
    }

    private static string Describe(SitOutSnapshot s) =>
        $"wounded={s.Wounded}, attack={s.AttackAvailable}, siege={s.IsSiege}, share={s.PlayerSideShare}";

    [TestMethod]
    public void Ledger_StartsEmpty_RecordsAndConsumeResets()
    {
        var ledger = new SitOutLedger();
        Assert.AreEqual(SitOutVerdict.Unchanged, ledger.Consume());

        ledger.Record(SitOutVerdict.OverwhelmingCost);
        Assert.AreEqual(SitOutVerdict.OverwhelmingCost, ledger.Consume());
        Assert.AreEqual(SitOutVerdict.Unchanged, ledger.Consume(), "Consume empties the ledger");
    }

    [TestMethod]
    public void Snapshot_IsAnImmutableStruct()
    {
        var s = new SitOutSnapshot(true, false, true, 0.25f);
        Assert.AreEqual(true, s.Wounded);
        Assert.AreEqual(false, s.AttackAvailable);
        Assert.AreEqual(true, s.IsSiege);
        Assert.AreEqual(0.25f, s.PlayerSideShare);
        Assert.IsTrue(typeof(SitOutSnapshot).IsValueType);
        Assert.IsFalse(Array.Exists(typeof(SitOutSnapshot).GetProperties(), p => p.CanWrite));
    }
}

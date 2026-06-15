# Codex Adversarial Review -- CulturalFeats party-culture NRE fix + chokepoint migration

You are an adversarial reviewer. Confirm or DISPUTE each finding with concrete evidence from the code below. Do not rubber-stamp; do not invent issues in vanilla-matching code. Use `--` not em-dashes.

## What changed (1 crash fix + 1 consistency migration, 5 C# files)

A campaign-map `NullReferenceException` crashed during `Army.OnSiegeStarted` strength calc. Root cause: `CultureFeatAdapter.ResolvePartyCulture` called `party.Culture`, and vanilla `PartyBase.Culture => MapFaction.Culture` has NO null guard -- it NREs inside the getter when `MapFaction` is null (a faction-less lord party). The `if (party.Culture != null)` guard was useless because the getter throws before returning.

Fix: rewrote `ResolvePartyCulture` as a null-safe `?.` chain. Then swept the four remaining inline Owner-only party-culture callers onto the same chokepoint so all 9 party-culture feat models resolve culture identically (vanilla `PartyBaseHelper.HasFeat` precedence: LeaderHero -> party.Culture(=MapFaction) -> Owner -> Settlement).

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar.
"rohan" is NOT a valid ID (Rohan uses "vlandia"). "dol_guldur" is NOT valid (use "dolguldur").
This change touches NO config IDs -- it is pure C# control-flow. ID cheatsheet included only for context.

## READ FIRST

- docs/reviews/rca-culturefeat-partyculture-nre-2026-06-15.md (the RCA -- root cause + preventive actions)
- docs/features/cultural-feats.md -> "Party-Culture Resolution -- the ResolvePartyCulture chokepoint" section
- Main/Features/CulturalFeats/CultureFeatAdapter.cs (the chokepoint -- ResolvePartyCulture + FromOrNull overloads)
- .claude/rules/adapters.md -> "computed getter throws BEFORE your != null guard" rule

## Known Suspects (CONFIRM or DISPUTE each, with evidence)

1. NULL-SAFETY COMPLETENESS. The new chain is `party.LeaderHero?.Culture ?? party.MapFaction?.Culture ?? party.Owner?.Culture ?? party.Settlement?.Culture`. Prove it cannot NRE for ANY PartyBase state. Note `PartyBase.Owner` (vanilla below) is itself a computed getter that derefs `Settlement.Owner` / `MobileParty.Owner` -- does `party.Owner` (NOT `party.Owner?...`) throw when reached on a faction-less mobile party? Does `party.LeaderHero` (= `MobileParty?.LeaderHero`) throw? Confirm every hop is safe, or find the hop that still throws.

2. PRECEDENCE EQUIVALENCE. Does the `??` chain exactly reproduce vanilla `PartyBaseHelper.HasFeat` order (LeaderHero -> party.Culture -> Owner -> Settlement)? `party.MapFaction?.Culture` is claimed to be the null-safe equivalent of `party.Culture` because `PartyBase.Culture => MapFaction.Culture`. Confirm or dispute that substitution is semantically identical when MapFaction is non-null.

3. RAID MIGRATION TYPE. `TaomRaidModel` now passes `attackerSide?.LeaderParty` directly to `FromOrNull(PartyBase?)`. Confirm `MapEventSide.LeaderParty` is a `PartyBase` (NOT a `MobileParty`). If it were `MobileParty`, this would bind the wrong overload or fail to compile. The vanilla signature is pasted below -- verify it.

4. BEHAVIOR-SHIFT SCOPE. The migration shifted ArmyManagement (influence award + cost), Raid (damage), and PartyWage line 49 from Owner-culture to LeaderHero-first. Confirm that (a) `TaomPartyWageModel` line 82 `garrisonCulture = mobileParty.CurrentSettlement.Owner?.Culture` was CORRECTLY left settlement-owner-scoped (garrison wage is paid by the fief owner, not the party), and (b) the two `_careerPassives.ApplyFactor(...Owner?.StringId...)` / `(...LeaderHero?.StringId...)` calls (per-hero passives, NOT culture feats) were correctly NOT migrated. Flag if either should also have changed, or if a party-culture site was missed.

5. NULL-RESULT CONSUMPTION. `ResolvePartyCulture` can now return null (faction-less party with null Owner and Settlement). `FromOrNull(null)` returns a null `ICultureFeatAdapter?`. Confirm every consumer -- `ICulturalFeatsService.Apply*Feats(ICultureFeatAdapter?, ...)` and the wage model's `ResolvePartyInputs(CultureObject?)` / `ResolveRohanMountedWageBonus(CultureObject?)` -- handles null without NRE and falls back to base behavior. The fix depends on "no culture -> skip feats" being supported.

6. REMAINING EXPOSURE. Grep all of Main/ for any OTHER unguarded call to a `PartyBase`'s `.Culture` (the throwing getter) that was NOT migrated. We claim ZERO remain. Disprove it if you can. (Settlement.Culture and Hero.Culture are plain FIELDS -- safe -- do not flag those.)

## DESIGN QUESTION (weigh, don't just confirm)

PartyWage line 49 is the weakest semantic case: it shifts "party wage culture" from owner-economic (the clan pays salaries) to leader-recruitment (LeaderHero-first). For the common case (party leader == owner) it is identical; only a cross-culture-led party differs. We chose consistency (all party-culture feats resolve identically + match vanilla HasFeat) over per-feat owner-scoping. Is there a concrete gameplay scenario where leader-first wage attribution is clearly WRONG, or is consistency the right call?

## THE DIFF (exactly what changed)

```diff
--- a/Main/Features/CulturalFeats/CultureFeatAdapter.cs
+++ b/Main/Features/CulturalFeats/CultureFeatAdapter.cs
@@ ResolvePartyCulture @@
-        if (party.LeaderHero != null)
-            return party.LeaderHero.Culture;
-        if (party.Culture != null)
-            return party.Culture;
-        if (party.Owner != null)
-            return party.Owner.Culture;
-        if (party.Settlement != null)
-            return party.Settlement.Culture;
-        return null;
+        return party.LeaderHero?.Culture
+            ?? party.MapFaction?.Culture
+            ?? party.Owner?.Culture
+            ?? party.Settlement?.Culture;

--- a/Main/Features/CulturalFeats/Models/TaomArmyManagementModel.cs
+++ b/...
-            CultureFeatAdapter.FromOrNull(armyMemberParty.Party?.Owner?.Culture),
+            CultureFeatAdapter.FromOrNull(armyMemberParty.Party),
-            CultureFeatAdapter.FromOrNull(armyLeaderParty.Party?.Owner?.Culture),
+            CultureFeatAdapter.FromOrNull(armyLeaderParty.Party),

--- a/Main/Features/CulturalFeats/Models/TaomBattleRewardModel.cs
+++ b/...
-        _feats.ApplyRenownFeats(CultureFeatAdapter.FromOrNull(winnerParty.Owner?.Culture ?? winnerParty.Culture), ref result);
+        _feats.ApplyRenownFeats(CultureFeatAdapter.FromOrNull(winnerParty), ref result);
         _careerPassives.ApplyFactor((winnerParty.Owner ?? winnerParty.LeaderHero)?.StringId, ref result, PassiveEffectType.BattleRenownGain);

--- a/Main/Features/CulturalFeats/Models/TaomRaidModel.cs
+++ b/...
-            CultureFeatAdapter.FromOrNull(attackerSide?.LeaderParty?.Owner?.Culture),
+            CultureFeatAdapter.FromOrNull(attackerSide?.LeaderParty),
         _careerPassives.ApplyFactor(attackerSide?.LeaderParty?.Owner?.StringId, ref result, PassiveEffectType.TroopDamage);

--- a/Main/Features/TroopProgression/Models/TaomPartyWageModel.cs
+++ b/...
-        var partyCulture = mobileParty.Party?.Owner?.Culture;
+        var partyCulture = CultureFeatAdapter.ResolvePartyCulture(mobileParty.Party);
   ... (garrison path unchanged):
         var garrisonCulture = mobileParty.CurrentSettlement.Owner?.Culture;
```

## VANILLA CODE (installed v1.4.6, authoritative)

PartyBase.cs (TaleWorlds.CampaignSystem.Party):
```csharp
public Hero LeaderHero => MobileParty?.LeaderHero;                 // line 206

public Hero Owner                                                  // lines 189-204
{
    get
    {
        Hero hero = _customOwner;
        if (hero == null)
        {
            if (!IsMobile)
                return Settlement.Owner;
            hero = MobileParty.Owner;
        }
        return hero;
    }
}

public IFaction MapFaction                                         // lines 236-250
{
    get
    {
        if (MobileParty != null) return MobileParty.MapFaction;
        if (Settlement != null)  return Settlement.MapFaction;
        return null;
    }
}

public CultureObject Culture => MapFaction.Culture;                // line 255 -- NO null guard (crash source)
```

Hero.Culture is a plain field: `[SaveableField(551)] public CultureObject Culture;` (Hero.cs:117).
Settlement.Culture is a plain field: `public CultureObject Culture;` (Settlement.cs:70).

PartyBaseHelper.HasFeat (Helpers, the precedence being mirrored), lines 373-396:
```csharp
public static bool HasFeat(PartyBase party, FeatObject feat)
{
    if (party == null) return false;
    if (party.LeaderHero != null) return party.LeaderHero.Culture.HasFeat(feat);
    if (party.Culture != null)    return party.Culture.HasFeat(feat);
    if (party.Owner != null)      return party.Owner.Culture.HasFeat(feat);
    if (party.Settlement != null) return party.Settlement.Culture.HasFeat(feat);
    return false;
}
```

MapEventSide.LeaderParty (TaleWorlds.CampaignSystem.MapEvents), line 94:
```csharp
[SaveableProperty(4)] public PartyBase LeaderParty { get; internal set; }
```

Base GameModel methods overridden (verify the override signatures match):
- DefaultArmyManagementCalculationModel.DailyBeingAtArmyInfluenceAward(MobileParty), .CalculatePartyInfluenceCost(MobileParty, MobileParty)
- DefaultRaidModel.CalculateHitDamage(MapEventSide, float)
- DefaultPartyWageModel.GetTotalWage(MobileParty, TroopRoster, bool)
- DefaultBattleRewardModel.CalculateRenownGain(PartyBase, float, float, float, bool)

## REQUIRED SECTIONS

1. KNOWN SUSPECTS -- CONFIRMED / DISPUTED verdict for each of the 6, with the exact code line that proves it.
2. NULL-SAFETY PROOF -- walk each hop of the new chain and state why it cannot throw (or where it can).
3. PRECEDENCE / BEHAVIOR analysis -- is the LeaderHero-first shift correct for army-influence, raid-damage, party-wage? Answer the DESIGN QUESTION.
4. COMPLETENESS -- did the migration miss any party-culture site, or over-migrate a settlement/per-hero site?
5. FINDINGS OR OBSERVATIONS -- any real bug, with file + line + fix. If none, say so explicitly.

## QUALITY GATES

- Decompile / reason against the vanilla code pasted above; do not assume.
- Distinguish computed getters (PartyBase.Culture, PartyBase.Owner -- can throw) from plain fields (Hero.Culture, Settlement.Culture -- safe).
- Do NOT flag vanilla-matching precedence as a bug -- mirroring HasFeat is the intent.
- "rohan"/"dol_guldur" are not valid IDs, but this change touches no IDs.

## Prior review lessons

SUCCESSES: vanilla decompilation caught missing gates; lifecycle tracing caught stale state; config ID cross-ref caught mismatches.
FAILURES to avoid: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as a bug; skipping hard sections.

Output your review below this line.

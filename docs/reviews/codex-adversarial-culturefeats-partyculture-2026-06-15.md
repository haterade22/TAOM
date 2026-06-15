OpenAI Codex v0.128.0 (research preview)
--------
workdir: C:\Users\mikew\source\repos\TAOM
model: gpt-5.5
provider: openai
approval: never
sandbox: workspace-write [workdir, /tmp, C:\Users\mikew\.codex\memories]
reasoning effort: xhigh
reasoning summaries: none
session id: 019ecc04-2144-7601-bbd7-4a2e63e3f1af
--------
user
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

ERROR: The process "36620" not found.
ERROR: The process "50896" not found.
codex
I’ll review this as a read-only pass: first the RCA/docs and changed files, then grep for remaining `PartyBase.Culture` exposure and verify the nullable chain against the pasted vanilla getter shapes.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "rg -n \"PartyBase|\\.Culture|ResolvePartyCulture|PartyBaseHelper|Party Culture|Party-Culture|computed getter\" docs/reviews/rca-culturefeat-partyculture-nre-2026-06-15.md docs/features/cultural-feats.md .claude/rules/adapters.md" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "rg -n \"ResolvePartyCulture|FromOrNull|Apply.*Feats|ResolvePartyInputs|ResolveRohanMountedWageBonus|Owner\\?\\.Culture|LeaderHero\\?\\.Culture|\\.Culture\" Main/Features/CulturalFeats Main/Features/TroopProgression -g \"*.cs\"" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git diff -- Main/Features/CulturalFeats/CultureFeatAdapter.cs Main/Features/CulturalFeats/Models/TaomArmyManagementModel.cs Main/Features/CulturalFeats/Models/TaomBattleRewardModel.cs Main/Features/CulturalFeats/Models/TaomRaidModel.cs Main/Features/TroopProgression/Models/TaomPartyWageModel.cs' in C:\Users\mikew\source\repos\TAOM
 succeeded in 590ms:
diff --git a/Main/Features/CulturalFeats/CultureFeatAdapter.cs b/Main/Features/CulturalFeats/CultureFeatAdapter.cs
index 5969396..5415c45 100644
--- a/Main/Features/CulturalFeats/CultureFeatAdapter.cs
+++ b/Main/Features/CulturalFeats/CultureFeatAdapter.cs
@@ -63,14 +63,13 @@ public sealed class CultureFeatAdapter : ICultureFeatAdapter
     {
         if (party == null)
             return null;
-        if (party.LeaderHero != null)
-            return party.LeaderHero.Culture;
-        if (party.Culture != null)
-            return party.Culture;
-        if (party.Owner != null)
-            return party.Owner.Culture;
-        if (party.Settlement != null)
-            return party.Settlement.Culture;
-        return null;
+        // party.Culture is `MapFaction.Culture` with no null guard — it NREs when
+        // MapFaction is null (faction-less lord party during army siege-start strength
+        // calc). Use the null-safe MapFaction?.Culture equivalent; every step is `?.`
+        // per .claude/rules/adapters.md (TaleWorlds getters crash before your null check).
+        return party.LeaderHero?.Culture
+            ?? party.MapFaction?.Culture
+            ?? party.Owner?.Culture
+            ?? party.Settlement?.Culture;
     }
 }
diff --git a/Main/Features/CulturalFeats/Models/TaomArmyManagementModel.cs b/Main/Features/CulturalFeats/Models/TaomArmyManagementModel.cs
index d06d66c..740fc5f 100644
--- a/Main/Features/CulturalFeats/Models/TaomArmyManagementModel.cs
+++ b/Main/Features/CulturalFeats/Models/TaomArmyManagementModel.cs
@@ -18,13 +18,17 @@ public class TaomArmyManagementModel : DefaultArmyManagementCalculationModel
         _feats = feats;
     }
 
+    // Resolve party culture via the shared CultureFeatAdapter.FromOrNull(PartyBase) chokepoint —
+    // vanilla PartyBaseHelper.HasFeat precedence (LeaderHero-first, MapFaction-aware), null-safe.
+    // Replaces the prior Owner-only inline that skipped LeaderHero.Culture (the Codex-review-43
+    // systemic gap the other party-culture models were already migrated for).
     public override float DailyBeingAtArmyInfluenceAward(MobileParty armyMemberParty)
         => _feats.ApplyArmyInfluenceAward(
-            CultureFeatAdapter.FromOrNull(armyMemberParty.Party?.Owner?.Culture),
+            CultureFeatAdapter.FromOrNull(armyMemberParty.Party),
             base.DailyBeingAtArmyInfluenceAward(armyMemberParty));
 
     public override int CalculatePartyInfluenceCost(MobileParty armyLeaderParty, MobileParty party)
         => _feats.ApplyArmyInfluenceCost(
-            CultureFeatAdapter.FromOrNull(armyLeaderParty.Party?.Owner?.Culture),
+            CultureFeatAdapter.FromOrNull(armyLeaderParty.Party),
             base.CalculatePartyInfluenceCost(armyLeaderParty, party));
 }
diff --git a/Main/Features/CulturalFeats/Models/TaomBattleRewardModel.cs b/Main/Features/CulturalFeats/Models/TaomBattleRewardModel.cs
index 8e3f34a..11c1db2 100644
--- a/Main/Features/CulturalFeats/Models/TaomBattleRewardModel.cs
+++ b/Main/Features/CulturalFeats/Models/TaomBattleRewardModel.cs
@@ -31,7 +31,10 @@ public class TaomBattleRewardModel : DefaultBattleRewardModel
             contributionShareOfWinnerParty,
             renownMultiplierForWinnerSide,
             includeDescriptions);
-        _feats.ApplyRenownFeats(CultureFeatAdapter.FromOrNull(winnerParty.Owner?.Culture ?? winnerParty.Culture), ref result);
+        // Vanilla PartyBaseHelper.HasFeat precedence via the shared helper. Replaces the prior
+        // `winnerParty.Owner?.Culture ?? winnerParty.Culture`: winnerParty.Culture is `MapFaction.Culture`
+        // and NREs when MapFaction is null, and the old order skipped LeaderHero.Culture (Codex review 43).
+        _feats.ApplyRenownFeats(CultureFeatAdapter.FromOrNull(winnerParty), ref result);
         _careerPassives.ApplyFactor((winnerParty.Owner ?? winnerParty.LeaderHero)?.StringId, ref result, PassiveEffectType.BattleRenownGain);
         return result;
     }
diff --git a/Main/Features/CulturalFeats/Models/TaomRaidModel.cs b/Main/Features/CulturalFeats/Models/TaomRaidModel.cs
index ed0fcd9..0d2673f 100644
--- a/Main/Features/CulturalFeats/Models/TaomRaidModel.cs
+++ b/Main/Features/CulturalFeats/Models/TaomRaidModel.cs
@@ -22,8 +22,13 @@ public class TaomRaidModel : DefaultRaidModel
         MapEventSide attackerSide, float settlementHitPoints)
     {
         var result = base.CalculateHitDamage(attackerSide, settlementHitPoints);
+        // Vanilla PartyBaseHelper.HasFeat precedence via the shared chokepoint (LeaderHero-first,
+        // null-safe). MapEventSide.LeaderParty is a PartyBase, so pass it straight to
+        // FromOrNull(PartyBase). Replaces the prior Owner-only inline that skipped LeaderHero.Culture
+        // (Codex review 43). The careerPassives call below keys on the owner Hero's StringId (a
+        // per-hero passive, not a culture feat) — intentionally left on Owner.
         _feats.ApplyRaidDamageFeats(
-            CultureFeatAdapter.FromOrNull(attackerSide?.LeaderParty?.Owner?.Culture),
+            CultureFeatAdapter.FromOrNull(attackerSide?.LeaderParty),
             ref result);
         _careerPassives.ApplyFactor(attackerSide?.LeaderParty?.Owner?.StringId, ref result, PassiveEffectType.TroopDamage);
         return result;
diff --git a/Main/Features/TroopProgression/Models/TaomPartyWageModel.cs b/Main/Features/TroopProgression/Models/TaomPartyWageModel.cs
index 53bfdd5..65e7bc5 100644
--- a/Main/Features/TroopProgression/Models/TaomPartyWageModel.cs
+++ b/Main/Features/TroopProgression/Models/TaomPartyWageModel.cs
@@ -46,7 +46,13 @@ public class TaomPartyWageModel : DefaultPartyWageModel
         var result = base.GetTotalWage(mobileParty, troopRoster, includeDescriptions);
 
         var garrisonInputs = ResolveGarrisonInputs(mobileParty);
-        var partyCulture = mobileParty.Party?.Owner?.Culture;
+        // Party wage feats key off the party's OWN culture — resolve via the shared
+        // CultureFeatAdapter.ResolvePartyCulture chokepoint (vanilla PartyBaseHelper.HasFeat
+        // precedence: LeaderHero-first, MapFaction-aware, null-safe), not the prior Owner-only
+        // inline. Behavior shift: a Rohan hero leading a Gundabad-owned party now pays Rohan wage
+        // rates, matching how every other party-culture feat resolves. Garrison wage (below) stays
+        // separate — it is settlement-owner-scoped, not party-scoped.
+        var partyCulture = CultureFeatAdapter.ResolvePartyCulture(mobileParty.Party);
         var partyInputs = ResolvePartyInputs(partyCulture);
         float rohanMountedWageBonus = ResolveRohanMountedWageBonus(partyCulture);
         float mountedWageShare = ComputeMountedWageShare(rohanMountedWageBonus, result.BaseNumber, troopRoster);
@@ -79,6 +85,9 @@ public class TaomPartyWageModel : DefaultPartyWageModel
         if (!mobileParty.IsGarrison || mobileParty.CurrentSettlement?.Town == null)
             return WageFeatInputs.None;
 
+        // Settlement-scoped by design: garrison wage feats key off the FIEF OWNER's culture (who
+        // pays the garrison), NOT the garrison party's own culture — so this deliberately does NOT
+        // route through ResolvePartyCulture. See docs/features/cultural-feats.md.
         var garrisonCulture = mobileParty.CurrentSettlement.Owner?.Culture;
         if (garrisonCulture == null)
             return WageFeatInputs.None;
warning: in the working copy of 'Main/Features/CulturalFeats/CultureFeatAdapter.cs', LF will be replaced by CRLF the next time Git touches it

2026-06-15T16:01:40.051108Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 2.3 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "ResolvePartyCulture|FromOrNull|Apply.*Feats|ResolvePartyInputs …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

2026-06-15T16:01:40.051106Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 2.3 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "PartyBase|\.Culture|ResolvePartyCulture|PartyBaseHelper|Party  …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 2334ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "PartyBase|\.Culture|ResolvePartyCulture|PartyBaseHelper|Party  …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 2334ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "ResolvePartyCulture|FromOrNull|Apply.*Feats|ResolvePartyInputs …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

codex
`rg` is not installed in this shell, so I’m switching to PowerShell `Select-String`/numbered `Get-Content`. The diff confirms only the five expected C# files are changed.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/CulturalFeats/Models/TaomArmyManagementModel.cs' | ForEach-Object { "'$i++; '"'{0}:{1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/CulturalFeats/CultureFeatAdapter.cs' | ForEach-Object { "'$i++; '"'{0}:{1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/CulturalFeats/Models/TaomBattleRewardModel.cs' | ForEach-Object { "'$i++; '"'{0}:{1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/CulturalFeats/Models/TaomRaidModel.cs' | ForEach-Object { "'$i++; '"'{0}:{1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 498ms:
1:using TaleWorlds.CampaignSystem.GameComponents;
2:using TaleWorlds.CampaignSystem.Party;
3:
4:namespace TAOM.Features.CulturalFeats.Models;
5:
6:/// <summary>
7:/// Thin GameModel boundary — converts <see cref="MobileParty"/> into the
8:/// adapter the <see cref="ICulturalFeatsService"/> needs, then delegates. Per
9:/// <c>gamemodels.md</c> rule 4: no inline if/foreach/switch/yield, no business
10:/// logic. Issues #144 / #176.
11:/// </summary>
12:public class TaomArmyManagementModel : DefaultArmyManagementCalculationModel
13:{
14:    private readonly ICulturalFeatsService _feats;
15:
16:    public TaomArmyManagementModel(ICulturalFeatsService feats)
17:    {
18:        _feats = feats;
19:    }
20:
21:    // Resolve party culture via the shared CultureFeatAdapter.FromOrNull(PartyBase) chokepoint —
22:    // vanilla PartyBaseHelper.HasFeat precedence (LeaderHero-first, MapFaction-aware), null-safe.
23:    // Replaces the prior Owner-only inline that skipped LeaderHero.Culture (the Codex-review-43
24:    // systemic gap the other party-culture models were already migrated for).
25:    public override float DailyBeingAtArmyInfluenceAward(MobileParty armyMemberParty)
26:        => _feats.ApplyArmyInfluenceAward(
27:            CultureFeatAdapter.FromOrNull(armyMemberParty.Party),
28:            base.DailyBeingAtArmyInfluenceAward(armyMemberParty));
29:
30:    public override int CalculatePartyInfluenceCost(MobileParty armyLeaderParty, MobileParty party)
31:        => _feats.ApplyArmyInfluenceCost(
32:            CultureFeatAdapter.FromOrNull(armyLeaderParty.Party),
33:            base.CalculatePartyInfluenceCost(armyLeaderParty, party));
34:}

 succeeded in 499ms:
1:using TaleWorlds.CampaignSystem;
2:using TaleWorlds.CampaignSystem.GameComponents;
3:using TaleWorlds.CampaignSystem.Party;
4:using TaleWorlds.Core;
5:using TAOM.Features.CareerSystem;
6:using TAOM.Features.CareerSystem.Domain;
7:
8:namespace TAOM.Features.CulturalFeats.Models;
9:
10:public class TaomBattleRewardModel : DefaultBattleRewardModel
11:{
12:    private readonly ICulturalFeatsService _feats;
13:    private readonly ICareerPassiveService _careerPassives;
14:
15:    public TaomBattleRewardModel(ICulturalFeatsService feats, ICareerPassiveService careerPassives)
16:    {
17:        _feats = feats;
18:        _careerPassives = careerPassives;
19:    }
20:
21:    public override ExplainedNumber CalculateRenownGain(
22:        PartyBase winnerParty,
23:        float renownValueOfBattleForWinnerSide,
24:        float contributionShareOfWinnerParty,
25:        float renownMultiplierForWinnerSide,
26:        bool includeDescriptions)
27:    {
28:        var result = base.CalculateRenownGain(
29:            winnerParty,
30:            renownValueOfBattleForWinnerSide,
31:            contributionShareOfWinnerParty,
32:            renownMultiplierForWinnerSide,
33:            includeDescriptions);
34:        // Vanilla PartyBaseHelper.HasFeat precedence via the shared helper. Replaces the prior
35:        // `winnerParty.Owner?.Culture ?? winnerParty.Culture`: winnerParty.Culture is `MapFaction.Culture`
36:        // and NREs when MapFaction is null, and the old order skipped LeaderHero.Culture (Codex review 43).
37:        _feats.ApplyRenownFeats(CultureFeatAdapter.FromOrNull(winnerParty), ref result);
38:        _careerPassives.ApplyFactor((winnerParty.Owner ?? winnerParty.LeaderHero)?.StringId, ref result, PassiveEffectType.BattleRenownGain);
39:        return result;
40:    }
41:}

 succeeded in 511ms:
1:using TaleWorlds.CampaignSystem;
2:using TaleWorlds.CampaignSystem.CharacterDevelopment;
3:using TaleWorlds.CampaignSystem.Party;
4:
5:namespace TAOM.Features.CulturalFeats;
6:
7:/// <summary>
8:/// Production wrapper around a sealed <see cref="CultureObject"/>. Constructed
9:/// at the boundary in each <c>Taom*Model</c> override; the underlying culture
10:/// reference is never exposed to the service. <c>HasFeat</c> lives on
11:/// <see cref="CultureObject"/> (Campaign system), not its
12:/// <c>BasicCultureObject</c> base in TaleWorlds.Core — keeping the type as
13:/// <c>CultureObject</c> matches how every model already pulls culture
14:/// (via <c>party.Owner.Culture</c>, <c>town.OwnerClan.Culture</c>, etc.).
15:/// </summary>
16:public sealed class CultureFeatAdapter : ICultureFeatAdapter
17:{
18:    private readonly CultureObject _culture;
19:
20:    public CultureFeatAdapter(CultureObject culture)
21:    {
22:        _culture = culture;
23:    }
24:
25:    public bool HasFeat(FeatObject feat)
26:    {
27:        if (_culture == null || feat == null)
28:            return false;
29:        return _culture.HasFeat(feat);
30:    }
31:
32:    /// <summary>
33:    /// Convenience boundary helper: returns null when <paramref name="culture"/>
34:    /// is null so the model overrides can keep their "no culture → skip"
35:    /// short-circuit at a single point.
36:    /// </summary>
37:    public static ICultureFeatAdapter? FromOrNull(CultureObject? culture)
38:        => culture == null ? null : new CultureFeatAdapter(culture);
39:
40:    /// <summary>
41:    /// Boundary helper — resolves the culture used for cultural-feat checks
42:    /// with the same precedence as vanilla <c>PartyBaseHelper.HasFeat</c>:
43:    /// leader hero → party → owner → settlement. Wraps the result in an
44:    /// adapter, returning null when no culture can be resolved. Shared by
45:    /// every GameModel that does culture-keyed feat dispatch on a party
46:    /// (<c>TaomPartySpeedModel</c>, <c>TaomPartySizeModel</c>, …) so the
47:    /// precedence lives in one place — fixes the consistency gap Codex
48:    /// review 43 caught and prevents future drift.
49:    /// </summary>
50:    public static ICultureFeatAdapter? FromOrNull(PartyBase? party)
51:        => FromOrNull(ResolvePartyCulture(party));
52:
53:    /// <summary>
54:    /// Boundary helper — same vanilla <c>PartyBaseHelper.HasFeat</c> precedence
55:    /// as <see cref="FromOrNull(PartyBase?)"/> but returns the raw
56:    /// <see cref="CultureObject"/> for callers that need the engine type for
57:    /// a non-feat purpose (e.g. <c>culture.StringId</c> lookup against a
58:    /// per-culture config). Use the adapter overload when you only need
59:    /// <c>HasFeat</c>; use this when you need a real culture handle. Returns
60:    /// null when no culture can be resolved.
61:    /// </summary>
62:    public static CultureObject? ResolvePartyCulture(PartyBase? party)
63:    {
64:        if (party == null)
65:            return null;
66:        // party.Culture is `MapFaction.Culture` with no null guard — it NREs when
67:        // MapFaction is null (faction-less lord party during army siege-start strength
68:        // calc). Use the null-safe MapFaction?.Culture equivalent; every step is `?.`
69:        // per .claude/rules/adapters.md (TaleWorlds getters crash before your null check).
70:        return party.LeaderHero?.Culture
71:            ?? party.MapFaction?.Culture
72:            ?? party.Owner?.Culture
73:            ?? party.Settlement?.Culture;
74:    }
75:}

 succeeded in 495ms:
1:using TaleWorlds.CampaignSystem;
2:using TaleWorlds.CampaignSystem.GameComponents;
3:using TaleWorlds.CampaignSystem.MapEvents;
4:using TaleWorlds.Core;
5:using TAOM.Features.CareerSystem;
6:using TAOM.Features.CareerSystem.Domain;
7:
8:namespace TAOM.Features.CulturalFeats.Models;
9:
10:public class TaomRaidModel : DefaultRaidModel
11:{
12:    private readonly ICulturalFeatsService _feats;
13:    private readonly ICareerPassiveService _careerPassives;
14:
15:    public TaomRaidModel(ICulturalFeatsService feats, ICareerPassiveService careerPassives)
16:    {
17:        _feats = feats;
18:        _careerPassives = careerPassives;
19:    }
20:
21:    public override ExplainedNumber CalculateHitDamage(
22:        MapEventSide attackerSide, float settlementHitPoints)
23:    {
24:        var result = base.CalculateHitDamage(attackerSide, settlementHitPoints);
25:        // Vanilla PartyBaseHelper.HasFeat precedence via the shared chokepoint (LeaderHero-first,
26:        // null-safe). MapEventSide.LeaderParty is a PartyBase, so pass it straight to
27:        // FromOrNull(PartyBase). Replaces the prior Owner-only inline that skipped LeaderHero.Culture
28:        // (Codex review 43). The careerPassives call below keys on the owner Hero's StringId (a
29:        // per-hero passive, not a culture feat) — intentionally left on Owner.
30:        _feats.ApplyRaidDamageFeats(
31:            CultureFeatAdapter.FromOrNull(attackerSide?.LeaderParty),
32:            ref result);
33:        _careerPassives.ApplyFactor(attackerSide?.LeaderParty?.Owner?.StringId, ref result, PassiveEffectType.TroopDamage);
34:        return result;
35:    }
36:}

codex
I’ve got the exact changed lines now. Next I’m checking the wage model and service null-handling, plus a whole-`Main` search for `PartyBase` culture getter exposure rather than just changed files.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/TroopProgression/Models/TaomPartyWageModel.cs' | ForEach-Object { "'$i++; '"'{0}:{1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/CulturalFeats/ICulturalFeatsService.cs' | ForEach-Object { "'$i++; '"'{0}:{1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/CulturalFeats/CulturalFeatsService.cs' | ForEach-Object { "'$i++; if ($i -le 420) { '"'{0}:{1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main -Recurse -Include *.cs | Select-String -Pattern '\\.Culture' | ForEach-Object { '{0}:{1}:{2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 508ms:
1:using TaleWorlds.CampaignSystem;
2:using TaleWorlds.Core;
3:
4:namespace TAOM.Features.CulturalFeats;
5:
6:/// <summary>
7:/// Centralises the per-feat dispatch logic for the 16 <c>Taom*Model</c>
8:/// overrides in <see cref="Models"/>. Each method takes a boundary-converted
9:/// <see cref="ICultureFeatAdapter"/> (or null when the source has no owning
10:/// culture) and an in-flight <see cref="ExplainedNumber"/> by-ref, applies any
11:/// matching cultural feats to that result, and returns.
12:///
13:/// Career-passive integration is intentionally NOT part of this service — those
14:/// remain owned by <c>ICareerPassiveService</c> and the model overrides still
15:/// call them directly at the boundary. The single-responsibility line:
16:/// <i>cultural feats only</i>.
17:///
18:/// All methods are no-ops when <paramref name="culture"/> is null. Issues #144,
19:/// #176.
20:/// </summary>
21:public interface ICulturalFeatsService
22:{
23:    // ── ArmyManagement ──────────────────────────────────────────────────
24:    /// <summary>Applies Rivendell + Gondor army-influence-award factors (additive).</summary>
25:    float ApplyArmyInfluenceAward(ICultureFeatAdapter? culture, float baseAward);
26:
27:    /// <summary>Applies Rivendell, Gundabad, Dol Guldur, Mordor army-influence-COST factors (additive).</summary>
28:    int ApplyArmyInfluenceCost(ICultureFeatAdapter? culture, int baseCost);
29:
30:    // ── PartySpeed ──────────────────────────────────────────────────────
31:    /// <summary>
32:    /// Applies the per-culture terrain movement-speed feats whose terrain matches
33:    /// <paramref name="terrain"/> (forest/snow/steppe/desert/plain/swamp), plus the
34:    /// Mordor night-speed feat when <paramref name="isNight"/>. Flat <c>AddFactor</c>
35:    /// of each matching feat's <c>EffectBonus</c>; no-op for <see cref="TerrainKind.None"/>.
36:    /// </summary>
37:    void ApplyTerrainSpeedFeats(ICultureFeatAdapter? culture, TerrainKind terrain, bool isNight, ref ExplainedNumber result);
38:
39:    /// <summary>Applies the Rohan infantry-speed penalty when &gt;50% of the party is infantry.</summary>
40:    void ApplyRohanInfantryPenalty(ICultureFeatAdapter? culture, int mountedCount, int totalCount, ref ExplainedNumber result);
41:
42:    // ── SettlementProsperity ───────────────────────────────────────────
43:    /// <summary>Applies Rivendell/Mirkwood/Gondor hearth-growth factors. Skipped when current change is negative.</summary>
44:    void ApplyHearthGrowthFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);
45:
46:    // ── SettlementMilitia ──────────────────────────────────────────────
47:    /// <summary>Applies Mirkwood/Dol Guldur veteran-militia spawn-chance bonuses (additive).</summary>
48:    void ApplyVeteranMilitiaFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);
49:
50:    // ── BuildingConstruction ───────────────────────────────────────────
51:    /// <summary>Applies Erebor/Lothlorien/Dol Guldur/Isengard construction-speed factors.</summary>
52:    void ApplyConstructionSpeedFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);
53:
54:    // ── VillageProduction ──────────────────────────────────────────────
55:    /// <summary>Applies Erebor production + Gundabad/Mordor grain-production factors.</summary>
56:    void ApplyVillageProductionFeats(ICultureFeatAdapter? culture, bool isGrain, ref ExplainedNumber result);
57:
58:    // ── Caravan ────────────────────────────────────────────────────────
59:    /// <summary>Applies the Umbar cheaper-caravans factor to a base cost. Rounds to int via MathF.Round-equivalent banker rounding semantics matching the original model.</summary>
60:    int ApplyCaravanCost(ICultureFeatAdapter? culture, int baseCost);
61:
62:    // ── BattleReward ───────────────────────────────────────────────────
63:    /// <summary>Applies the Umbar renown factor.</summary>
64:    void ApplyRenownFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);
65:
66:    // ── PartyTroopUpgrade ──────────────────────────────────────────────
67:    /// <summary>Applies the Isengard/Rohan mounted-upgrade-cost factors. No-op when troop is not mounted.</summary>
68:    void ApplyTroopUpgradeFeats(ICultureFeatAdapter? culture, bool isMounted, ref ExplainedNumber result);
69:
70:    // ── PartySize ──────────────────────────────────────────────────────
71:    /// <summary>Applies Mordor/Gundabad/Dol Guldur/Isengard/Gondor + Dunland/Rhun/Harad party-size factors.</summary>
72:    void ApplyPartySizeFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);
73:
74:    // ── VolunteerRespawn ──────────────────────────────────────────────
75:    /// <summary>Applies Dunland/Gundabad/Dol Guldur/Mordor village volunteer-respawn-rate factors. Called once per notable per daily tick — caller must clamp to [0,1] (probability semantics).</summary>
76:    void ApplyVolunteerRespawnFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);
77:
78:    // ── NotableSpawn ──────────────────────────────────────────────────
79:    /// <summary>
80:    /// Returns the notable target for the settlement, applied per-occupation. Town
81:    /// occupations (Merchant / Artisan / GangLeader) sum flat <c>Add</c> values from
82:    /// per-(culture, occupation) feats — supports the asymmetric Isengard/Dol Guldur
83:    /// gang-leader-heavy distributions where a single uniform multiplier can't express
84:    /// "small Merchants, huge Gang Leaders." Village occupations (RuralNotable /
85:    /// Headman) keep the legacy uniform per-(culture, village) <c>AddFactor</c> with
86:    /// ceiling rounding. Returns <paramref name="baseCount"/> when the culture has no
87:    /// matching feat, <paramref name="baseCount"/> &lt;= 0, or
88:    /// <paramref name="occupation"/> is <see cref="NotableOccupationKind.Other"/>.
89:    /// </summary>
90:    int ApplyNotableCountFeat(ICultureFeatAdapter? culture, NotableOccupationKind occupation, int baseCount);
91:
92:    // ── FoodConsumption ────────────────────────────────────────────────
93:    /// <summary>Applies Rivendell/Mirkwood/Lothlorien/Dol Guldur food-consumption factors.</summary>
94:    void ApplyFoodConsumptionFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);
95:
96:    // ── SettlementLoyalty ──────────────────────────────────────────────
97:    /// <summary>Applies Gondor/Erebor/Lothlorien/Rivendell/Rohan loyalty bonuses (Add — not AddFactor).</summary>
98:    void ApplyLoyaltyFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);
99:
100:    // ── PartyMorale ────────────────────────────────────────────────────
101:    /// <summary>Applies Gondor/Rohan/Erebor/Mirkwood/Lothlorien morale bonuses (Add).</summary>
102:    void ApplyMoraleFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);
103:
104:    // ── Smithing ───────────────────────────────────────────────────────
105:    /// <summary>Applies Erebor/Isengard smithing-energy-cost factors.</summary>
106:    void ApplySmithingFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);
107:
108:    // ── ClanFinance (tariffs) ──────────────────────────────────────────
109:    /// <summary>Applies the Umbar tariff-income factor.</summary>
110:    void ApplyTariffIncomeFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);
111:
112:    // ── Raid ───────────────────────────────────────────────────────────
113:    /// <summary>Applies Mordor/Gundabad/Isengard raid-damage factors.</summary>
114:    void ApplyRaidDamageFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result);
115:}

 succeeded in 510ms:
1:using TaleWorlds.CampaignSystem;
2:using TaleWorlds.CampaignSystem.CharacterDevelopment;
3:using TaleWorlds.CampaignSystem.GameComponents;
4:using TaleWorlds.CampaignSystem.Party;
5:using TaleWorlds.CampaignSystem.Roster;
6:using TaleWorlds.Core;
7:using TaleWorlds.Localization;
8:using TAOM.Features.CareerSystem;
9:using TAOM.Features.CareerSystem.Domain;
10:using TAOM.Features.CulturalFeats;
11:
12:namespace TAOM.Features.TroopProgression.Models;
13:
14:public class TaomPartyWageModel : DefaultPartyWageModel
15:{
16:    private static TextObject? _cultureText;
17:    private static TextObject CultureText => _cultureText ??= GameTexts.FindText("str_culture");
18:
19:    private readonly ITroopCostService _costService;
20:    private readonly ICareerPassiveService _careerPassives;
21:    private readonly IWageModifierService _wageModifiers;
22:
23:    public TaomPartyWageModel(
24:        ITroopCostService costService,
25:        ICareerPassiveService careerPassives,
26:        IWageModifierService wageModifiers)
27:    {
28:        _costService = costService;
29:        _careerPassives = careerPassives;
30:        _wageModifiers = wageModifiers;
31:    }
32:
33:    public override int MaxWagePaymentLimit => 20000;
34:
35:    public override int GetCharacterWage(CharacterObject character)
36:    {
37:        int tier = character.Tier;
38:        bool isMounted = character.IsMounted;
39:        bool isMercenary = IsMercenaryOccupation(character.Occupation);
40:
41:        return _costService.GetCharacterWage(tier, isMounted, isMercenary);
42:    }
43:
44:    public override ExplainedNumber GetTotalWage(MobileParty mobileParty, TroopRoster troopRoster, bool includeDescriptions = false)
45:    {
46:        var result = base.GetTotalWage(mobileParty, troopRoster, includeDescriptions);
47:
48:        var garrisonInputs = ResolveGarrisonInputs(mobileParty);
49:        // Party wage feats key off the party's OWN culture — resolve via the shared
50:        // CultureFeatAdapter.ResolvePartyCulture chokepoint (vanilla PartyBaseHelper.HasFeat
51:        // precedence: LeaderHero-first, MapFaction-aware, null-safe), not the prior Owner-only
52:        // inline. Behavior shift: a Rohan hero leading a Gundabad-owned party now pays Rohan wage
53:        // rates, matching how every other party-culture feat resolves. Garrison wage (below) stays
54:        // separate — it is settlement-owner-scoped, not party-scoped.
55:        var partyCulture = CultureFeatAdapter.ResolvePartyCulture(mobileParty.Party);
56:        var partyInputs = ResolvePartyInputs(partyCulture);
57:        float rohanMountedWageBonus = ResolveRohanMountedWageBonus(partyCulture);
58:        float mountedWageShare = ComputeMountedWageShare(rohanMountedWageBonus, result.BaseNumber, troopRoster);
59:
60:        _wageModifiers.ApplyWageModifiers(
61:            ref result, garrisonInputs, partyInputs, rohanMountedWageBonus, mountedWageShare, CultureText);
62:
63:        _careerPassives.ApplyFactor(mobileParty.LeaderHero?.StringId, ref result, PassiveEffectType.TroopWages);
64:        return result;
65:    }
66:
67:    public override ExplainedNumber GetTroopRecruitmentCost(
68:        CharacterObject troop, Hero buyerHero, bool withoutItemCost = false)
69:    {
70:        var feats = ResolveMountedCostFeats(buyerHero?.Culture, troop.IsMounted);
71:        var buyerPerks = ResolveBuyerRecruitmentPerks(buyerHero, troop);
72:        int cost = _wageModifiers.CalculateRecruitmentCost(
73:            troop.Level,
74:            troop.IsMounted,
75:            IsMercenaryOccupation(troop.Occupation),
76:            withoutItemCost,
77:            feats,
78:            CultureText,
79:            buyerPerks);
80:        return new ExplainedNumber(cost, includeDescriptions: false);
81:    }
82:
83:    private static WageFeatInputs ResolveGarrisonInputs(MobileParty mobileParty)
84:    {
85:        if (!mobileParty.IsGarrison || mobileParty.CurrentSettlement?.Town == null)
86:            return WageFeatInputs.None;
87:
88:        // Settlement-scoped by design: garrison wage feats key off the FIEF OWNER's culture (who
89:        // pays the garrison), NOT the garrison party's own culture — so this deliberately does NOT
90:        // route through ResolvePartyCulture. See docs/features/cultural-feats.md.
91:        var garrisonCulture = mobileParty.CurrentSettlement.Owner?.Culture;
92:        if (garrisonCulture == null)
93:            return WageFeatInputs.None;
94:
95:        return new WageFeatInputs(
96:            isApplicable: true,
97:            ereborGarrisonBonus: BonusIfHas(garrisonCulture, TaomCulturalFeats.EreborGarrisonWageFeat),
98:            lothlorienGarrisonBonus: BonusIfHas(garrisonCulture, TaomCulturalFeats.LothlorienGarrisonWageFeat),
99:            isengardGarrisonBonus: BonusIfHas(garrisonCulture, TaomCulturalFeats.IsengardGarrisonWageFeat),
100:            gondorGarrisonBonus: BonusIfHas(garrisonCulture, TaomCulturalFeats.GondorGarrisonWageFeat));
101:    }
102:
103:    private static WageFeatInputs ResolvePartyInputs(CultureObject? partyCulture)
104:    {
105:        if (partyCulture == null)
106:            return WageFeatInputs.None;
107:
108:        return new WageFeatInputs(
109:            isApplicable: true,
110:            gundabadWageBonus: BonusIfHas(partyCulture, TaomCulturalFeats.GundabadWageFeat),
111:            umbarWageBonus: BonusIfHas(partyCulture, TaomCulturalFeats.UmbarWageFeat),
112:            mordorWageBonus: BonusIfHas(partyCulture, TaomCulturalFeats.MordorWageFeat));
113:    }
114:
115:    private static float ResolveRohanMountedWageBonus(CultureObject? partyCulture)
116:        => BonusIfHas(partyCulture, TaomCulturalFeats.RohanMountedWageFeat);
117:
118:    private float ComputeMountedWageShare(float rohanMountedWageBonus, float baseWageTotal, TroopRoster troopRoster)
119:    {
120:        if (rohanMountedWageBonus == 0f || troopRoster == null || baseWageTotal <= 0f)
121:            return 0f;
122:
123:        float mountedWageTotal = 0f;
124:        foreach (var element in troopRoster.GetTroopRoster())
125:        {
126:            if (element.Character?.IsMounted == true)
127:                mountedWageTotal += GetCharacterWage(element.Character) * element.Number;
128:        }
129:        return mountedWageTotal / baseWageTotal;
130:    }
131:
132:    private static MountedCostFeatInputs ResolveMountedCostFeats(CultureObject? buyerCulture, bool isMounted)
133:    {
134:        if (!isMounted || buyerCulture == null)
135:            return MountedCostFeatInputs.None;
136:
137:        return new MountedCostFeatInputs(
138:            isengardMountedCostBonus: BonusIfHas(buyerCulture, TaomCulturalFeats.IsengardCheaperRecruitsFeat),
139:            rohanMountedCostBonus: BonusIfHas(buyerCulture, TaomCulturalFeats.RohanMountedCostFeat));
140:    }
141:
142:    // Vanilla DefaultPartyWageModel.GetTroopRecruitmentCost applies the buyer hero's personal
143:    // skill-perk recruitment discounts (orthogonal to TAOM's extended cost table + cultural feats).
144:    // The full-replacement override dropped them; this restores them. KhuzaitRecruitUpgradeFeat is
145:    // intentionally NOT resolved here — TAOM replaces it with the Isengard/Rohan mounted-cost feats.
146:    private static RecruitmentPerkInputs ResolveBuyerRecruitmentPerks(Hero? buyerHero, CharacterObject troop)
147:    {
148:        if (buyerHero == null)
149:            return RecruitmentPerkInputs.None;
150:
151:        return new RecruitmentPerkInputs(
152:            hasBuyer: true,
153:            tierAtLeast2: troop.Tier >= 2,
154:            isInfantry: troop.IsInfantry,
155:            isRanged: troop.IsRanged,
156:            isPartyLeader: buyerHero.IsPartyLeader,
157:            isMercenary: IsMercenaryOccupation(troop.Occupation),
158:            headHunterBonus: SecondaryPerkBonus(buyerHero, DefaultPerks.Throwing.HeadHunter),
159:            chinkInTheArmorBonus: SecondaryPerkBonus(buyerHero, DefaultPerks.OneHanded.ChinkInTheArmor),
160:            showOfStrengthBonus: SecondaryPerkBonus(buyerHero, DefaultPerks.TwoHanded.ShowOfStrength),
161:            hardyFrontlineBonus: SecondaryPerkBonus(buyerHero, DefaultPerks.Polearm.HardyFrontline),
162:            renownedArcherBonus: SecondaryPerkBonus(buyerHero, DefaultPerks.Bow.RenownedArcher),
163:            piercerBonus: SecondaryPerkBonus(buyerHero, DefaultPerks.Crossbow.Piercer),
164:            frugalBonus: SecondaryPerkBonus(buyerHero, DefaultPerks.Steward.Frugal),
165:            swordForBarterBonus: PrimaryPerkBonus(buyerHero, DefaultPerks.Trade.SwordForBarter),
166:            slickNegotiatorBonus: PrimaryPerkBonus(buyerHero, DefaultPerks.Charm.SlickNegotiator));
167:    }
168:
169:    private static float SecondaryPerkBonus(Hero hero, PerkObject perk)
170:        => hero.GetPerkValue(perk) ? perk.SecondaryBonus : 0f;
171:
172:    private static float PrimaryPerkBonus(Hero hero, PerkObject perk)
173:        => hero.GetPerkValue(perk) ? perk.PrimaryBonus : 0f;
174:
175:    private static float BonusIfHas(CultureObject? culture, FeatObject feat)
176:        => culture?.HasFeat(feat) == true ? feat.EffectBonus : 0f;
177:
178:    private static bool IsMercenaryOccupation(Occupation occupation)
179:    {
180:        return occupation == Occupation.Mercenary
181:            || occupation == Occupation.Gangster
182:            || occupation == Occupation.CaravanGuard;
183:    }
184:}

 succeeded in 529ms:
1:using System;
2:using TaleWorlds.CampaignSystem;
3:using TaleWorlds.CampaignSystem.CharacterDevelopment;
4:using TaleWorlds.Core;
5:using TaleWorlds.Library;
6:using TaleWorlds.Localization;
7:
8:namespace TAOM.Features.CulturalFeats;
9:
10:/// <summary>
11:/// Concrete dispatch for <see cref="ICulturalFeatsService"/>. All methods are
12:/// pure (no side effects beyond mutating the by-ref <see cref="ExplainedNumber"/>),
13:/// no singleton lookups in the hot path, no per-call allocations beyond the
14:/// shared <see cref="CultureText"/>. Each method's structure mirrors the
15:/// original inline body of the corresponding <c>Taom*Model</c> override 1:1
16:/// to keep the behavior-preserving refactor reviewable line-by-line.
17:/// </summary>
18:public sealed class CulturalFeatsService : ICulturalFeatsService
19:{
20:    // Phase 9b #144 — preserved verbatim from the original models so the
21:    // ExplainedNumber description strings on the world-map tooltip stay
22:    // identical post-refactor. The `try` guard is for unit tests where the
23:    // TaleWorlds <c>GameTexts</c> static is uninitialised — the description
24:    // is a pure UI string and `Add`/`AddFactor` accept null descriptions.
25:    private static TextObject? _cultureText;
26:    private static bool _cultureTextResolved;
27:    private static TextObject? CultureText
28:    {
29:        get
30:        {
31:            if (_cultureTextResolved)
32:                return _cultureText;
33:            try { _cultureText = GameTexts.FindText("str_culture"); }
34:            catch { _cultureText = null; }
35:            _cultureTextResolved = true;
36:            return _cultureText;
37:        }
38:    }
39:
40:    // ── ArmyManagement ──────────────────────────────────────────────────
41:
42:    public float ApplyArmyInfluenceAward(ICultureFeatAdapter? culture, float baseAward)
43:    {
44:        if (culture == null)
45:            return baseAward;
46:
47:        float result = baseAward;
48:        if (culture.HasFeat(TaomCulturalFeats.RivendellArmyInfluenceFeat))
49:            result += baseAward * TaomCulturalFeats.RivendellArmyInfluenceFeat.EffectBonus;
50:        if (culture.HasFeat(TaomCulturalFeats.GondorArmyInfluenceFeat))
51:            result += baseAward * TaomCulturalFeats.GondorArmyInfluenceFeat.EffectBonus;
52:        return result;
53:    }
54:
55:    public int ApplyArmyInfluenceCost(ICultureFeatAdapter? culture, int baseCost)
56:    {
57:        if (culture == null)
58:            return baseCost;
59:
60:        float multiplier = 0f;
61:        if (culture.HasFeat(TaomCulturalFeats.RivendellArmyInfluenceCostFeat))
62:            multiplier += TaomCulturalFeats.RivendellArmyInfluenceCostFeat.EffectBonus;
63:        if (culture.HasFeat(TaomCulturalFeats.GundabadArmyInfluenceCostFeat))
64:            multiplier += TaomCulturalFeats.GundabadArmyInfluenceCostFeat.EffectBonus;
65:        if (culture.HasFeat(TaomCulturalFeats.MistyMountainOrcsArmyInfluenceCostFeat))
66:            multiplier += TaomCulturalFeats.MistyMountainOrcsArmyInfluenceCostFeat.EffectBonus;
67:        if (culture.HasFeat(TaomCulturalFeats.DolGuldurArmyInfluenceCostFeat))
68:            multiplier += TaomCulturalFeats.DolGuldurArmyInfluenceCostFeat.EffectBonus;
69:        if (culture.HasFeat(TaomCulturalFeats.MordorArmyInfluenceCostFeat))
70:            multiplier += TaomCulturalFeats.MordorArmyInfluenceCostFeat.EffectBonus;
71:        if (culture.HasFeat(TaomCulturalFeats.MirkwoodArmyInfluenceCostFeat))
72:            multiplier += TaomCulturalFeats.MirkwoodArmyInfluenceCostFeat.EffectBonus;
73:        if (culture.HasFeat(TaomCulturalFeats.HaradArmyInfluenceCostFeat))
74:            multiplier += TaomCulturalFeats.HaradArmyInfluenceCostFeat.EffectBonus;
75:
76:        return multiplier != 0f ? (int)(baseCost * (1f + multiplier)) : baseCost;
77:    }
78:
79:    // ── PartySpeed ──────────────────────────────────────────────────────
80:
81:    public void ApplyTerrainSpeedFeats(
82:        ICultureFeatAdapter? culture, TerrainKind terrain, bool isNight, ref ExplainedNumber result)
83:    {
84:        if (culture == null)
85:            return;
86:
87:        switch (terrain)
88:        {
89:            case TerrainKind.Forest:
90:                ApplyIfHas(culture, TaomCulturalFeats.MirkwoodForestSpeedFeat, ref result);
91:                ApplyIfHas(culture, TaomCulturalFeats.LothlorienForestSpeedFeat, ref result);
92:                ApplyIfHas(culture, TaomCulturalFeats.RivendellForestSpeedFeat, ref result);
93:                break;
94:            case TerrainKind.Snow:
95:                ApplyIfHas(culture, TaomCulturalFeats.EreborSnowSpeedFeat, ref result);
96:                ApplyIfHas(culture, TaomCulturalFeats.GundabadSnowSpeedFeat, ref result);
97:                ApplyIfHas(culture, TaomCulturalFeats.GoblinSnowSpeedFeat, ref result);
98:                ApplyIfHas(culture, TaomCulturalFeats.MistyMountainOrcsSnowSpeedFeat, ref result);
99:                break;
100:            case TerrainKind.Steppe:
101:                ApplyIfHas(culture, TaomCulturalFeats.KhandSteppeSpeedFeat, ref result);
102:                ApplyIfHas(culture, TaomCulturalFeats.RhunSteppeSpeedFeat, ref result);
103:                break;
104:            case TerrainKind.Desert:
105:                ApplyIfHas(culture, TaomCulturalFeats.UmbarDesertSpeedFeat, ref result);
106:                ApplyIfHas(culture, TaomCulturalFeats.HaradDesertSpeedFeat, ref result);
107:                ApplyIfHas(culture, TaomCulturalFeats.ShaghanaDesertSpeedFeat, ref result);
108:                ApplyIfHas(culture, TaomCulturalFeats.AbanissaDesertSpeedFeat, ref result);
109:                break;
110:            case TerrainKind.Plain:
111:                ApplyIfHas(culture, TaomCulturalFeats.MordorPlainSpeedFeat, ref result);
112:                ApplyIfHas(culture, TaomCulturalFeats.GondorPlainSpeedFeat, ref result);
113:                ApplyIfHas(culture, TaomCulturalFeats.RohanPlainSpeedFeat, ref result);
114:                ApplyIfHas(culture, TaomCulturalFeats.DalePlainSpeedFeat, ref result);
115:                ApplyIfHas(culture, TaomCulturalFeats.DunlandPlainSpeedFeat, ref result);
116:                ApplyIfHas(culture, TaomCulturalFeats.IsengardPlainSpeedFeat, ref result);
117:                break;
118:            case TerrainKind.Swamp:
119:                ApplyIfHas(culture, TaomCulturalFeats.MordorSwampSpeedFeat, ref result);
120:                ApplyIfHas(culture, TaomCulturalFeats.IsengardSwampSpeedFeat, ref result);
121:                break;
122:        }
123:
124:        if (isNight)
125:            ApplyIfHas(culture, TaomCulturalFeats.MordorNightSpeedFeat, ref result);
126:    }
127:
128:    private static void ApplyIfHas(ICultureFeatAdapter culture, FeatObject feat, ref ExplainedNumber result)
129:    {
130:        if (culture.HasFeat(feat))
131:            result.AddFactor(feat.EffectBonus, CultureText);
132:    }
133:
134:    public void ApplyRohanInfantryPenalty(
135:        ICultureFeatAdapter? culture, int mountedCount, int totalCount, ref ExplainedNumber result)
136:    {
137:        if (culture == null || totalCount <= 0)
138:            return;
139:        if (!culture.HasFeat(TaomCulturalFeats.RohanInfantrySpeedFeat))
140:            return;
141:        if (mountedCount * 2 < totalCount)
142:            result.AddFactor(TaomCulturalFeats.RohanInfantrySpeedFeat.EffectBonus, CultureText);
143:    }
144:
145:    // ── SettlementProsperity ───────────────────────────────────────────
146:
147:    public void ApplyHearthGrowthFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
148:    {
149:        if (culture == null)
150:            return;
151:
152:        // Match the original model's guard: only buff/nerf growth when result is still
153:        // non-negative. Negative results are skipped wholesale per pre-refactor behavior.
154:        if (culture.HasFeat(TaomCulturalFeats.RivendellHearthGrowthFeat) && result.ResultNumber >= 0f)
155:            result.AddFactor(TaomCulturalFeats.RivendellHearthGrowthFeat.EffectBonus, CultureText);
156:        if (culture.HasFeat(TaomCulturalFeats.MirkwoodHearthGrowthFeat) && result.ResultNumber >= 0f)
157:            result.AddFactor(TaomCulturalFeats.MirkwoodHearthGrowthFeat.EffectBonus, CultureText);
158:        if (culture.HasFeat(TaomCulturalFeats.GondorHearthGrowthFeat) && result.ResultNumber >= 0f)
159:            result.AddFactor(TaomCulturalFeats.GondorHearthGrowthFeat.EffectBonus, CultureText);
160:    }
161:
162:    // ── SettlementMilitia ──────────────────────────────────────────────
163:
164:    public void ApplyVeteranMilitiaFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
165:    {
166:        if (culture == null)
167:            return;
168:        if (culture.HasFeat(TaomCulturalFeats.MirkwoodMilitiaProductionFeat))
169:            result.Add(TaomCulturalFeats.MirkwoodMilitiaProductionFeat.EffectBonus);
170:        if (culture.HasFeat(TaomCulturalFeats.DolGuldurMilitiaProductionFeat))
171:            result.Add(TaomCulturalFeats.DolGuldurMilitiaProductionFeat.EffectBonus);
172:    }
173:
174:    // ── BuildingConstruction ───────────────────────────────────────────
175:
176:    public void ApplyConstructionSpeedFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
177:    {
178:        if (culture == null)
179:            return;
180:        if (culture.HasFeat(TaomCulturalFeats.EreborConstructionSpeedFeat))
181:            result.AddFactor(TaomCulturalFeats.EreborConstructionSpeedFeat.EffectBonus, CultureText);
182:        if (culture.HasFeat(TaomCulturalFeats.LothlorienConstructionSpeedFeat))
183:            result.AddFactor(TaomCulturalFeats.LothlorienConstructionSpeedFeat.EffectBonus, CultureText);
184:        if (culture.HasFeat(TaomCulturalFeats.DolGuldurConstructionSpeedFeat))
185:            result.AddFactor(TaomCulturalFeats.DolGuldurConstructionSpeedFeat.EffectBonus, CultureText);
186:        if (culture.HasFeat(TaomCulturalFeats.IsengardConstructionSpeedFeat))
187:            result.AddFactor(TaomCulturalFeats.IsengardConstructionSpeedFeat.EffectBonus, CultureText);
188:        if (culture.HasFeat(TaomCulturalFeats.MistyMountainOrcsConstructionSpeedFeat))
189:            result.AddFactor(TaomCulturalFeats.MistyMountainOrcsConstructionSpeedFeat.EffectBonus, CultureText);
190:    }
191:
192:    // ── VillageProduction ──────────────────────────────────────────────
193:
194:    public void ApplyVillageProductionFeats(ICultureFeatAdapter? culture, bool isGrain, ref ExplainedNumber result)
195:    {
196:        if (culture == null)
197:            return;
198:        if (culture.HasFeat(TaomCulturalFeats.EreborProductionFeat))
199:            result.AddFactor(TaomCulturalFeats.EreborProductionFeat.EffectBonus, CultureText);
200:        if (isGrain && culture.HasFeat(TaomCulturalFeats.GundabadGrainProductionFeat))
201:            result.AddFactor(TaomCulturalFeats.GundabadGrainProductionFeat.EffectBonus, CultureText);
202:        if (isGrain && culture.HasFeat(TaomCulturalFeats.MordorGrainProductionFeat))
203:            result.AddFactor(TaomCulturalFeats.MordorGrainProductionFeat.EffectBonus, CultureText);
204:    }
205:
206:    // ── Caravan ────────────────────────────────────────────────────────
207:
208:    public int ApplyCaravanCost(ICultureFeatAdapter? culture, int baseCost)
209:    {
210:        if (culture == null)
211:            return baseCost;
212:        if (culture.HasFeat(TaomCulturalFeats.UmbarCheaperCaravansFeat))
213:            return MathF.Round(baseCost * (1f + TaomCulturalFeats.UmbarCheaperCaravansFeat.EffectBonus));
214:        return baseCost;
215:    }
216:
217:    // ── BattleReward ───────────────────────────────────────────────────
218:
219:    public void ApplyRenownFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
220:    {
221:        if (culture == null)
222:            return;
223:        if (culture.HasFeat(TaomCulturalFeats.UmbarRenownFeat))
224:            result.AddFactor(TaomCulturalFeats.UmbarRenownFeat.EffectBonus, CultureText);
225:        if (culture.HasFeat(TaomCulturalFeats.DaleRenownFeat))
226:            result.AddFactor(TaomCulturalFeats.DaleRenownFeat.EffectBonus, CultureText);
227:        if (culture.HasFeat(TaomCulturalFeats.KhandRenownFeat))
228:            result.AddFactor(TaomCulturalFeats.KhandRenownFeat.EffectBonus, CultureText);
229:    }
230:
231:    // ── PartyTroopUpgrade ──────────────────────────────────────────────
232:
233:    public void ApplyTroopUpgradeFeats(ICultureFeatAdapter? culture, bool isMounted, ref ExplainedNumber result)
234:    {
235:        if (culture == null || !isMounted)
236:            return;
237:        if (culture.HasFeat(TaomCulturalFeats.IsengardCheaperRecruitsFeat))
238:            result.AddFactor(TaomCulturalFeats.IsengardCheaperRecruitsFeat.EffectBonus, CultureText);
239:        if (culture.HasFeat(TaomCulturalFeats.RohanMountedCostFeat))
240:            result.AddFactor(TaomCulturalFeats.RohanMountedCostFeat.EffectBonus, CultureText);
241:    }
242:
243:    // ── PartySize ──────────────────────────────────────────────────────
244:
245:    public void ApplyPartySizeFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
246:    {
247:        if (culture == null)
248:            return;
249:        if (culture.HasFeat(TaomCulturalFeats.MordorPartySizeFeat))
250:            result.AddFactor(TaomCulturalFeats.MordorPartySizeFeat.EffectBonus, CultureText);
251:        if (culture.HasFeat(TaomCulturalFeats.GundabadPartySizeFeat))
252:            result.AddFactor(TaomCulturalFeats.GundabadPartySizeFeat.EffectBonus, CultureText);
253:        if (culture.HasFeat(TaomCulturalFeats.GoblinPartySizeFeat))
254:            result.AddFactor(TaomCulturalFeats.GoblinPartySizeFeat.EffectBonus, CultureText);
255:        if (culture.HasFeat(TaomCulturalFeats.MistyMountainOrcsPartySizeFeat))
256:            result.AddFactor(TaomCulturalFeats.MistyMountainOrcsPartySizeFeat.EffectBonus, CultureText);
257:        if (culture.HasFeat(TaomCulturalFeats.DolGuldurPartySizeFeat))
258:            result.AddFactor(TaomCulturalFeats.DolGuldurPartySizeFeat.EffectBonus, CultureText);
259:        if (culture.HasFeat(TaomCulturalFeats.IsengardPartySizeFeat))
260:            result.AddFactor(TaomCulturalFeats.IsengardPartySizeFeat.EffectBonus, CultureText);
261:        if (culture.HasFeat(TaomCulturalFeats.GondorPartySizeFeat))
262:            result.AddFactor(TaomCulturalFeats.GondorPartySizeFeat.EffectBonus, CultureText);
263:        if (culture.HasFeat(TaomCulturalFeats.DunlandPartySizeFeat))
264:            result.AddFactor(TaomCulturalFeats.DunlandPartySizeFeat.EffectBonus, CultureText);
265:        if (culture.HasFeat(TaomCulturalFeats.RhunPartySizeFeat))
266:            result.AddFactor(TaomCulturalFeats.RhunPartySizeFeat.EffectBonus, CultureText);
267:        if (culture.HasFeat(TaomCulturalFeats.HaradPartySizeFeat))
268:            result.AddFactor(TaomCulturalFeats.HaradPartySizeFeat.EffectBonus, CultureText);
269:        if (culture.HasFeat(TaomCulturalFeats.KhandPartySizeFeat))
270:            result.AddFactor(TaomCulturalFeats.KhandPartySizeFeat.EffectBonus, CultureText);
271:    }
272:
273:    // ── VolunteerRespawn ──────────────────────────────────────────────
274:
275:    public void ApplyVolunteerRespawnFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
276:    {
277:        if (culture == null)
278:            return;
279:        if (culture.HasFeat(TaomCulturalFeats.DunlandVolunteerRateFeat))
280:            result.AddFactor(TaomCulturalFeats.DunlandVolunteerRateFeat.EffectBonus, CultureText);
281:        if (culture.HasFeat(TaomCulturalFeats.GundabadVolunteerRateFeat))
282:            result.AddFactor(TaomCulturalFeats.GundabadVolunteerRateFeat.EffectBonus, CultureText);
283:        if (culture.HasFeat(TaomCulturalFeats.GoblinVolunteerRateFeat))
284:            result.AddFactor(TaomCulturalFeats.GoblinVolunteerRateFeat.EffectBonus, CultureText);
285:        if (culture.HasFeat(TaomCulturalFeats.DolGuldurVolunteerRateFeat))
286:            result.AddFactor(TaomCulturalFeats.DolGuldurVolunteerRateFeat.EffectBonus, CultureText);
287:        if (culture.HasFeat(TaomCulturalFeats.MordorVolunteerRateFeat))
288:            result.AddFactor(TaomCulturalFeats.MordorVolunteerRateFeat.EffectBonus, CultureText);
289:        if (culture.HasFeat(TaomCulturalFeats.LothlorienVolunteerRateFeat))
290:            result.AddFactor(TaomCulturalFeats.LothlorienVolunteerRateFeat.EffectBonus, CultureText);
291:    }
292:
293:    // ── NotableSpawn ──────────────────────────────────────────────────
294:
295:    public int ApplyNotableCountFeat(ICultureFeatAdapter? culture, NotableOccupationKind occupation, int baseCount)
296:    {
297:        if (culture == null || baseCount <= 0)
298:            return baseCount;
299:
300:        // Town occupations use per-(culture, occupation) AdditionType.Add feats — supports the
301:        // asymmetric Isengard/Dol Guldur gang-leader-heavy distributions a uniform multiplier
302:        // couldn't express ("a few Merchants, many Gang Leaders").
303:        switch (occupation)
304:        {
305:            case NotableOccupationKind.Merchant:
306:            {
307:                int add = 0;
308:                if (culture.HasFeat(TaomCulturalFeats.IsengardNotableCountTownMerchantFeat))
309:                    add += (int)TaomCulturalFeats.IsengardNotableCountTownMerchantFeat.EffectBonus;
310:                if (culture.HasFeat(TaomCulturalFeats.DolGuldurNotableCountTownMerchantFeat))
311:                    add += (int)TaomCulturalFeats.DolGuldurNotableCountTownMerchantFeat.EffectBonus;
312:                return baseCount + add;
313:            }
314:            case NotableOccupationKind.Artisan:
315:            {
316:                int add = 0;
317:                if (culture.HasFeat(TaomCulturalFeats.IsengardNotableCountTownArtisanFeat))
318:                    add += (int)TaomCulturalFeats.IsengardNotableCountTownArtisanFeat.EffectBonus;
319:                if (culture.HasFeat(TaomCulturalFeats.DolGuldurNotableCountTownArtisanFeat))
320:                    add += (int)TaomCulturalFeats.DolGuldurNotableCountTownArtisanFeat.EffectBonus;
321:                if (culture.HasFeat(TaomCulturalFeats.GundabadNotableCountTownArtisanFeat))
322:                    add += (int)TaomCulturalFeats.GundabadNotableCountTownArtisanFeat.EffectBonus;
323:                return baseCount + add;
324:            }
325:            case NotableOccupationKind.GangLeader:
326:            {
327:                int add = 0;
328:                if (culture.HasFeat(TaomCulturalFeats.IsengardNotableCountTownGangLeaderFeat))
329:                    add += (int)TaomCulturalFeats.IsengardNotableCountTownGangLeaderFeat.EffectBonus;
330:                if (culture.HasFeat(TaomCulturalFeats.DolGuldurNotableCountTownGangLeaderFeat))
331:                    add += (int)TaomCulturalFeats.DolGuldurNotableCountTownGangLeaderFeat.EffectBonus;
332:                if (culture.HasFeat(TaomCulturalFeats.MordorNotableCountTownGangLeaderFeat))
333:                    add += (int)TaomCulturalFeats.MordorNotableCountTownGangLeaderFeat.EffectBonus;
334:                if (culture.HasFeat(TaomCulturalFeats.GundabadNotableCountTownGangLeaderFeat))
335:                    add += (int)TaomCulturalFeats.GundabadNotableCountTownGangLeaderFeat.EffectBonus;
336:                return baseCount + add;
337:            }
338:            case NotableOccupationKind.RuralNotable:
339:            case NotableOccupationKind.Headman:
340:            {
341:                // Village: legacy uniform per-(culture, village) AddFactor + ceiling. The 4 village
342:                // feats deliver the same +1/+1 distribution for all 4 cultures (the user's spec didn't
343:                // call for asymmetric village counts), so keeping the AddFactor shape is cleaner than
344:                // splitting them into per-occupation Add feats with identical values.
345:                float multiplier = 0f;
346:                if (culture.HasFeat(TaomCulturalFeats.IsengardNotableCountVillageFeat))
347:                    multiplier += TaomCulturalFeats.IsengardNotableCountVillageFeat.EffectBonus;
348:                if (culture.HasFeat(TaomCulturalFeats.DolGuldurNotableCountVillageFeat))
349:                    multiplier += TaomCulturalFeats.DolGuldurNotableCountVillageFeat.EffectBonus;
350:                if (culture.HasFeat(TaomCulturalFeats.MordorNotableCountVillageFeat))
351:                    multiplier += TaomCulturalFeats.MordorNotableCountVillageFeat.EffectBonus;
352:                if (culture.HasFeat(TaomCulturalFeats.GundabadNotableCountVillageFeat))
353:                    multiplier += TaomCulturalFeats.GundabadNotableCountVillageFeat.EffectBonus;
354:                if (multiplier <= 0f)
355:                    return baseCount;
356:                return (int)Math.Ceiling((double)baseCount * (1.0 + multiplier));
357:            }
358:            default:
359:                return baseCount;
360:        }
361:    }
362:
363:    // ── FoodConsumption ────────────────────────────────────────────────
364:
365:    public void ApplyFoodConsumptionFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
366:    {
367:        if (culture == null)
368:            return;
369:        if (culture.HasFeat(TaomCulturalFeats.RivendellFoodConsumptionFeat))
370:            result.AddFactor(TaomCulturalFeats.RivendellFoodConsumptionFeat.EffectBonus, CultureText);
371:        if (culture.HasFeat(TaomCulturalFeats.MirkwoodFoodConsumptionFeat))
372:            result.AddFactor(TaomCulturalFeats.MirkwoodFoodConsumptionFeat.EffectBonus, CultureText);
373:        if (culture.HasFeat(TaomCulturalFeats.LothlorienFoodConsumptionFeat))
374:            result.AddFactor(TaomCulturalFeats.LothlorienFoodConsumptionFeat.EffectBonus, CultureText);
375:        if (culture.HasFeat(TaomCulturalFeats.DolGuldurFoodConsumptionFeat))
376:            result.AddFactor(TaomCulturalFeats.DolGuldurFoodConsumptionFeat.EffectBonus, CultureText);
377:        if (culture.HasFeat(TaomCulturalFeats.GoblinFoodConsumptionFeat))
378:            result.AddFactor(TaomCulturalFeats.GoblinFoodConsumptionFeat.EffectBonus, CultureText);
379:        if (culture.HasFeat(TaomCulturalFeats.MistyMountainOrcsFoodConsumptionFeat))
380:            result.AddFactor(TaomCulturalFeats.MistyMountainOrcsFoodConsumptionFeat.EffectBonus, CultureText);
381:        if (culture.HasFeat(TaomCulturalFeats.UmbarFoodConsumptionFeat))
382:            result.AddFactor(TaomCulturalFeats.UmbarFoodConsumptionFeat.EffectBonus, CultureText);
383:        if (culture.HasFeat(TaomCulturalFeats.KhandFoodConsumptionFeat))
384:            result.AddFactor(TaomCulturalFeats.KhandFoodConsumptionFeat.EffectBonus, CultureText);
385:        if (culture.HasFeat(TaomCulturalFeats.HaradFoodConsumptionFeat))
386:            result.AddFactor(TaomCulturalFeats.HaradFoodConsumptionFeat.EffectBonus, CultureText);
387:    }
388:
389:    // ── SettlementLoyalty ──────────────────────────────────────────────
390:
391:    public void ApplyLoyaltyFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
392:    {
393:        if (culture == null)
394:            return;
395:        if (culture.HasFeat(TaomCulturalFeats.GondorLoyaltyFeat))
396:            result.Add(TaomCulturalFeats.GondorLoyaltyFeat.EffectBonus, CultureText);
397:        if (culture.HasFeat(TaomCulturalFeats.EreborLoyaltyFeat))
398:            result.Add(TaomCulturalFeats.EreborLoyaltyFeat.EffectBonus, CultureText);
399:        if (culture.HasFeat(TaomCulturalFeats.LothlorienLoyaltyFeat))
400:            result.Add(TaomCulturalFeats.LothlorienLoyaltyFeat.EffectBonus, CultureText);
401:        if (culture.HasFeat(TaomCulturalFeats.RivendellLoyaltyFeat))
402:            result.Add(TaomCulturalFeats.RivendellLoyaltyFeat.EffectBonus, CultureText);
403:        if (culture.HasFeat(TaomCulturalFeats.RohanLoyaltyFeat))
404:            result.Add(TaomCulturalFeats.RohanLoyaltyFeat.EffectBonus, CultureText);
405:        if (culture.HasFeat(TaomCulturalFeats.DaleLoyaltyFeat))
406:            result.Add(TaomCulturalFeats.DaleLoyaltyFeat.EffectBonus, CultureText);
407:        if (culture.HasFeat(TaomCulturalFeats.RhunLoyaltyFeat))
408:            result.Add(TaomCulturalFeats.RhunLoyaltyFeat.EffectBonus, CultureText);
409:    }
410:
411:    // ── PartyMorale ────────────────────────────────────────────────────
412:
413:    public void ApplyMoraleFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
414:    {
415:        if (culture == null)
416:            return;
417:        if (culture.HasFeat(TaomCulturalFeats.GondorMoraleFeat))
418:            result.Add(TaomCulturalFeats.GondorMoraleFeat.EffectBonus, CultureText);
419:        if (culture.HasFeat(TaomCulturalFeats.RohanMoraleFeat))
420:            result.Add(TaomCulturalFeats.RohanMoraleFeat.EffectBonus, CultureText);

 succeeded in 795ms:
Main\Adapters\CareerHeroAdapter.cs:19:public string CultureStringId => _hero?.Culture?.StringId;
Main\Adapters\ClanPopulationAdapter.cs:44:clan.Culture?.StringId ?? "",
Main\Adapters\ClanStartupAdapter.cs:26:clan.Culture?.StringId ?? ""));
Main\Adapters\CultureConversionAdapter.cs:35:=> Settlement.Find(settlementId)?.Culture?.StringId;
Main\Adapters\CultureConversionAdapter.cs:38:=> Settlement.Find(settlementId)?.OwnerClan?.Culture?.StringId;
Main\Adapters\CultureConversionAdapter.cs:78:settlement.Culture = culture;
Main\Adapters\EquipmentSnapshotAdapter.cs:50:string cultureId = character?.Culture?.StringId ?? "<noculture>";
Main\Adapters\ICultureConversionAdapter.cs:30:/// <summary>Sets <c>Settlement.Culture</c> to the resolved culture. Returns false if the id or culture can't be resolved.</summary>
Main\Adapters\IItemPoolAdapter.cs:2:using TAOM.Features.CultureMarketplace.Domain;
Main\Adapters\ItemPoolAdapter.cs:6:using TAOM.Features.CultureMarketplace.Domain;
Main\Adapters\ItemPoolAdapter.cs:75:var attribCulture = item.Culture?.StringId;
Main\Adapters\ItemPoolAdapter.cs:111:return PrefixMap[i].CultureId;
Main\Adapters\ObjectManagerAdapter.cs:94:CultureId = c.Culture?.StringId
Main\Adapters\RosterItemSnapshot.cs:5:/// TaleWorlds types per ADR-007. CultureStringId is `item.Culture?.StringId` at
Main\Adapters\StartupHeroAdapter.cs:19:hero.Culture?.StringId ?? "",
Main\Adapters\TownRosterAdapter.cs:21:return settlement?.OwnerClan?.Culture?.StringId;
Main\Adapters\TownRosterAdapter.cs:126:result.Add(new RosterItemSnapshot(item.StringId, item.Culture?.StringId, n));
Main\Adapters\VolunteerContextAdapter.cs:3:using TAOM.Features.CultureConversion;
Main\Adapters\VolunteerContextAdapter.cs:26:return new VolunteerContext(null, null, null, hero.Culture?.StringId);
Main\Adapters\VolunteerContextAdapter.cs:37:string cultureId = hero.Culture?.StringId;
Main\Adapters\VolunteerContextAdapter.cs:40:string ownerCultureId = settlement.OwnerClan?.Culture?.StringId;
Main\Adapters\VolunteerContextAdapter.cs:45:string settlementCultureId = settlement.Culture?.StringId;
Main\Features\Arena\Models\TaomTournamentModel.cs:49:var items = _service.BuildPrizePool(town?.Culture?.StringId, RegularMinTier, RegularMaxTier);
Main\Features\Arena\Models\TaomTournamentModel.cs:58:var items = _service.BuildPrizePool(town?.Culture?.StringId, EliteMinTier, float.MaxValue);
Main\Features\Arena\Models\TaomTournamentModel.cs:66:var dummyId = _service.ResolveDummyId(participant?.Culture?.StringId, null);
Main\Features\Arena\TournamentService.cs:54:if (item.Culture?.StringId != cultureId) continue;
Main\Features\BanditManagement\Hooks\Patch40_HideoutDescription.cs:41:var cultureId = settlement.Culture?.StringId;
Main\Features\BattleBalance\Models\TaomPartyHealingModel.cs:42:var culture = TAOM.Features.CulturalFeats.CultureFeatAdapter.ResolvePartyCulture(party);
Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:81:$"agent#{snapshot.AgentIndex} '{snapshot.AgentName}' char='{snapshot.CharacterId}' culture='{snapshot.CultureId}' slots={snapshot.Slots?.Count ?? 0}");
Main\Features\CareerSystem\CareerCampaignBehavior.cs:63:var cultureId = hero.Culture?.StringId;
Main\Features\CareerSystem\CareerRegistry.cs:82:_logger.LogDebug($"CareerSystem: IsEligible — hero culture='{hero.CultureStringId}' clanTier={hero.ClanTier} < minClanTier={career.MinClanTier} for career '{careerStringId}'");
Main\Features\CareerSystem\CareerRegistry.cs:88:var heroCulture = hero.CultureStringId;
Main\Features\CareerSystem\CareerRegistry.cs:105:_logger.LogDebug($"CareerSystem: IsEligible — hero culture='{hero.CultureStringId}' IS eligible for career '{careerStringId}'");
Main\Features\CareerSystem\CareerSwitchService.cs:49:_logger.LogDebug($"CareerSystem: CanSwitch — hero culture='{hero.CultureStringId}' career='{newCareerStringId}' result={eligible}");
Main\Features\CharacterCreation\Hooks\CharacterCreationCultureStageVM_OnCultureSelection_Patch.cs:18:/// TAOM's <c>FactionMap.CultureSettingService</c> invokes <c>SetSelectedCulture</c> first
Main\Features\CharacterCreation\Hooks\CharacterCreationCultureStageVM_OnCultureSelection_Patch.cs:31:if (selectedCulture?.Culture == null) return;
Main\Features\CharacterCreation\Hooks\CharacterCreationCultureStageVM_OnCultureSelection_Patch.cs:36:service?.ApplyForCulture(selectedCulture.Culture.StringId);
Main\Features\CharacterCreation\CharacterCreationContentService.cs:81:if (VanillaCultureIds.Contains(cultureData.CultureId))
Main\Features\CharacterCreation\CharacterCreationContentService.cs:84:var cultureObject = GetCultureObject(cultureData.CultureId);
Main\Features\CharacterCreation\CharacterCreationContentService.cs:87:_logger.LogWarning($"Culture '{cultureData.CultureId}' not found in MBObjectManager — skipping");
Main\Features\CharacterCreation\CharacterCreationContentService.cs:101:_logger.LogError($"Failed to register culture '{cultureData.CultureId}': {ex.Message}");
Main\Features\CharacterCreation\CharacterCreationContentService.cs:175:// BL's ApplyCulture() should have set Hero.Culture = SelectedCulture already.
Main\Features\CharacterCreation\CharacterCreationContentService.cs:177:var heroCultureBefore = Hero.MainHero?.Culture?.StringId ?? "null";
Main\Features\CharacterCreation\CharacterCreationContentService.cs:178:_logger.LogInfo($"CC Finalize: SelectedCulture='{selectedCulture.StringId}', Hero.Culture before='{heroCultureBefore}'");
Main\Features\CharacterCreation\CharacterCreationContentService.cs:180:if (Hero.MainHero != null && Hero.MainHero.Culture?.StringId != selectedCulture.StringId)
Main\Features\CharacterCreation\CharacterCreationContentService.cs:182:Hero.MainHero.Culture = selectedCulture;
Main\Features\CharacterCreation\CharacterCreationContentService.cs:183:_logger.LogInfo($"CC Finalize: Force-set Hero.Culture to '{selectedCulture.StringId}' (was '{heroCultureBefore}')");
Main\Features\CharacterCreation\CultureCreationDataProvider.cs:77:string.Equals(c.CultureId, cultureId, StringComparison.OrdinalIgnoreCase));
Main\Features\CharacterCreation\NarrativeDataProvider.cs:33:return all.Where(o => string.Equals(o.CultureId, cultureId, StringComparison.OrdinalIgnoreCase))
Main\Features\CharacterCreation\NarrativeMenuBuilder.cs:65:var cultureId = definition.CultureId;
Main\Features\CrashReport\Collectors\CampaignStateCollector.cs:75:string? culture = SafeStr(() => h.Culture?.StringId);
Main\Features\CrashReport\Collectors\CampaignStateCollector.cs:127:SettlementCulture: SafeStr(() => settlement.Culture?.StringId),
Main\Features\CrashReport\Rendering\PlainTextCrashReportRenderer.cs:167:sb.AppendLine($"              culture={h.CultureId ?? "?"}  clan={h.ClanId ?? "?"}  kingdom={h.KingdomId ?? "?"}");
Main\Features\CulturalFeats\Models\TaomArmyManagementModel.cs:23:// Replaces the prior Owner-only inline that skipped LeaderHero.Culture (the Codex-review-43
Main\Features\CulturalFeats\Models\TaomBattleRewardModel.cs:35:// `winnerParty.Owner?.Culture ?? winnerParty.Culture`: winnerParty.Culture is `MapFaction.Culture`
Main\Features\CulturalFeats\Models\TaomBattleRewardModel.cs:36:// and NREs when MapFaction is null, and the old order skipped LeaderHero.Culture (Codex review 43).
Main\Features\CulturalFeats\Models\TaomBuildingConstructionModel.cs:21:_feats.ApplyConstructionSpeedFeats(CultureFeatAdapter.FromOrNull(town.OwnerClan?.Culture), ref result);
Main\Features\CulturalFeats\Models\TaomCaravanModel.cs:17:CultureFeatAdapter.FromOrNull(CharacterObject.PlayerCharacter?.Culture),
Main\Features\CulturalFeats\Models\TaomClanFinanceModel.cs:21:_feats.ApplyTariffIncomeFeats(CultureFeatAdapter.FromOrNull(clan?.Culture), ref result);
Main\Features\CulturalFeats\Models\TaomNotableSpawnModel.cs:14:/// Keyed on <c>settlement.Culture</c> (settlement identity), NOT <c>OwnerClan.Culture</c>:
Main\Features\CulturalFeats\Models\TaomNotableSpawnModel.cs:35:var culture = CultureFeatAdapter.FromOrNull(settlement.Culture);
Main\Features\CulturalFeats\Models\TaomPartySizeModel.cs:26:// Replaces the prior `party.Owner?.Culture ?? party.Culture` which skipped LeaderHero.Culture
Main\Features\CulturalFeats\Models\TaomRaidModel.cs:27:// FromOrNull(PartyBase). Replaces the prior Owner-only inline that skipped LeaderHero.Culture
Main\Features\CulturalFeats\Models\TaomSettlementLoyaltyModel.cs:37:_feats.ApplyLoyaltyFeats(CultureFeatAdapter.FromOrNull(town.Owner?.Culture), ref result);
Main\Features\CulturalFeats\Models\TaomSettlementMilitiaModel.cs:19:_feats.ApplyVeteranMilitiaFeats(CultureFeatAdapter.FromOrNull(settlement.OwnerClan?.Culture), ref result);
Main\Features\CulturalFeats\Models\TaomSettlementProsperityModel.cs:20:_feats.ApplyHearthGrowthFeats(CultureFeatAdapter.FromOrNull(village.Settlement?.OwnerClan?.Culture), ref result);
Main\Features\CulturalFeats\Models\TaomSmithingModel.cs:37:_feats.ApplySmithingFeats(CultureFeatAdapter.FromOrNull(hero?.Culture), ref result);
Main\Features\CulturalFeats\Models\TaomVillageProductionModel.cs:21:CultureFeatAdapter.FromOrNull(village.Settlement?.OwnerClan?.Culture),
Main\Features\CulturalFeats\CultureFeatAdapter.cs:14:/// (via <c>party.Owner.Culture</c>, <c>town.OwnerClan.Culture</c>, etc.).
Main\Features\CulturalFeats\CultureFeatAdapter.cs:66:// party.Culture is `MapFaction.Culture` with no null guard — it NREs when
Main\Features\CulturalFeats\CultureFeatAdapter.cs:68:// calc). Use the null-safe MapFaction?.Culture equivalent; every step is `?.`
Main\Features\CulturalFeats\CultureFeatAdapter.cs:70:return party.LeaderHero?.Culture
Main\Features\CulturalFeats\CultureFeatAdapter.cs:71:?? party.MapFaction?.Culture
Main\Features\CulturalFeats\CultureFeatAdapter.cs:72:?? party.Owner?.Culture
Main\Features\CulturalFeats\CultureFeatAdapter.cs:73:?? party.Settlement?.Culture;
Main\Features\CultureConversion\Domain\SettlementConversionRecord.cs:3:namespace TAOM.Features.CultureConversion.Domain;
Main\Features\CultureConversion\Domain\SettlementConversionRecord.cs:17:/// <see cref="Settlement.Culture"/> is NOT a saveable engine field, so completed overrides are
Main\Features\CultureConversion\Hooks\CultureConversionBehavior.cs:7:namespace TAOM.Features.CultureConversion.Hooks;
Main\Features\CultureConversion\Hooks\CultureConversionBehavior.cs:18:/// Entity State Matrix (re-apply on load mutates Settlement.Culture, an idempotent field write — not a
Main\Features\CultureConversion\CultureConversionConfig.cs:1:namespace TAOM.Features.CultureConversion;
Main\Features\CultureConversion\CultureConversionConfigProvider.cs:8:namespace TAOM.Features.CultureConversion;
Main\Features\CultureConversion\CultureConversionIoC.cs:4:namespace TAOM.Features.CultureConversion;
Main\Features\CultureConversion\CultureConversionService.cs:5:using TAOM.Features.CultureConversion.Domain;
Main\Features\CultureConversion\CultureConversionService.cs:8:namespace TAOM.Features.CultureConversion;
Main\Features\CultureConversion\CultureConversionService.cs:18:///     <c>Settlement.Culture</c> + bound villages flip to the new culture, notable volunteer slots are
Main\Features\CultureConversion\CultureConversionService.cs:59:// First time we've touched this fief: Settlement.Culture is still the authored original.
Main\Features\CultureConversion\CultureConversionService.cs:138:// Settlement.Culture stayed at the XML original, so recruitment would take the converted
Main\Features\CultureConversion\CultureConversionSettingsProvider.cs:3:namespace TAOM.Features.CultureConversion;
Main\Features\CultureConversion\CultureConversionSettingsProvider.cs:26:SafeClampInt(TaomSettings.Instance?.CultureConversionHoldDays, _defaults.RequiredHoldDays, MinHoldDays, MaxHoldDays);
Main\Features\CultureConversion\CultureConversionSettingsProvider.cs:29:TaomSettings.Instance?.CultureConversionRequireStableLoyalty ?? _defaults.RequireStableLoyalty;
Main\Features\CultureConversion\CultureConversionStore.cs:3:using TAOM.Features.CultureConversion.Domain;
Main\Features\CultureConversion\CultureConversionStore.cs:5:namespace TAOM.Features.CultureConversion;
Main\Features\CultureConversion\ICultureConversionConfigProvider.cs:1:namespace TAOM.Features.CultureConversion;
Main\Features\CultureConversion\ICultureConversionService.cs:1:namespace TAOM.Features.CultureConversion;
Main\Features\CultureConversion\ICultureConversionService.cs:11:/// <summary>Re-applies completed culture overrides after a save load (Settlement.Culture is not engine-saved).</summary>
Main\Features\CultureConversion\ICultureConversionSettingsProvider.cs:1:namespace TAOM.Features.CultureConversion;
Main\Features\CultureConversion\ICultureConversionStore.cs:2:using TAOM.Features.CultureConversion.Domain;
Main\Features\CultureConversion\ICultureConversionStore.cs:4:namespace TAOM.Features.CultureConversion;
Main\Features\CultureMarketplace\Domain\CultureItemPool.cs:3:namespace TAOM.Features.CultureMarketplace.Domain;
Main\Features\CultureMarketplace\Domain\ItemPoolEntry.cs:1:namespace TAOM.Features.CultureMarketplace.Domain;
Main\Features\CultureMarketplace\Domain\ItemPoolItem.cs:1:namespace TAOM.Features.CultureMarketplace.Domain;
Main\Features\CultureMarketplace\Domain\MarketplaceConfigOverride.cs:3:namespace TAOM.Features.CultureMarketplace.Domain;
Main\Features\CultureMarketplace\Domain\MarketplaceTuning.cs:1:namespace TAOM.Features.CultureMarketplace.Domain;
Main\Features\CultureMarketplace\Domain\RoutedItem.cs:3:namespace TAOM.Features.CultureMarketplace.Domain;
Main\Features\CultureMarketplace\CultureItemPoolService.cs:6:using TAOM.Features.CultureMarketplace.Domain;
Main\Features\CultureMarketplace\CultureItemPoolService.cs:8:namespace TAOM.Features.CultureMarketplace;
Main\Features\CultureMarketplace\CultureItemPoolService.cs:72:foreach (var cId in routed.Cultures)
Main\Features\CultureMarketplace\CultureItemPoolService.cs:89:var cultureId = ClassifyEffectiveCulture(item.CultureId, item.PrefixCultureId);
Main\Features\CultureMarketplace\CultureItemPoolService.cs:96:if (string.IsNullOrEmpty(item.CultureId) && !string.IsNullOrEmpty(item.PrefixCultureId))
Main\Features\CultureMarketplace\CultureItemPoolService.cs:186:foreach (var rawCulture in kvp.Value.Cultures)
Main\Features\CultureMarketplace\CultureMarketplaceBehavior.cs:6:using TAOM.Features.CultureMarketplace.Domain;
Main\Features\CultureMarketplace\CultureMarketplaceBehavior.cs:8:namespace TAOM.Features.CultureMarketplace;
Main\Features\CultureMarketplace\CultureMarketplaceBehavior.cs:112:_logger.LogInfo($"[CultureMarketplace] Pool ready: {_poolService.CultureCount} cultures, {_poolService.TotalItemCount} items");
Main\Features\CultureMarketplace\CultureMarketplaceConfigProvider.cs:9:using TAOM.Features.CultureMarketplace.Domain;
Main\Features\CultureMarketplace\CultureMarketplaceConfigProvider.cs:11:namespace TAOM.Features.CultureMarketplace;
Main\Features\CultureMarketplace\CultureMarketplaceInjectionService.cs:4:using TAOM.Features.CultureMarketplace.Domain;
Main\Features\CultureMarketplace\CultureMarketplaceInjectionService.cs:6:namespace TAOM.Features.CultureMarketplace;
Main\Features\CultureMarketplace\CultureMarketplaceIoC.cs:3:using TAOM.Features.CultureMarketplace.Domain;
Main\Features\CultureMarketplace\CultureMarketplaceIoC.cs:5:namespace TAOM.Features.CultureMarketplace;
Main\Features\CultureMarketplace\CultureMarketplaceMaintenanceService.cs:6:namespace TAOM.Features.CultureMarketplace;
Main\Features\CultureMarketplace\CultureMarketplaceMaintenanceService.cs:76:var effective = _poolService.ClassifyEffectiveCulture(row.CultureStringId, prefixCultureId: null);
Main\Features\CultureMarketplace\ICultureItemPoolService.cs:2:using TAOM.Features.CultureMarketplace.Domain;
Main\Features\CultureMarketplace\ICultureItemPoolService.cs:4:namespace TAOM.Features.CultureMarketplace;
Main\Features\CultureMarketplace\ICultureMarketplaceConfigProvider.cs:2:using TAOM.Features.CultureMarketplace.Domain;
Main\Features\CultureMarketplace\ICultureMarketplaceConfigProvider.cs:4:namespace TAOM.Features.CultureMarketplace;
Main\Features\CultureMarketplace\ICultureMarketplaceInjectionService.cs:3:namespace TAOM.Features.CultureMarketplace;
Main\Features\CultureMarketplace\ICultureMarketplaceMaintenanceService.cs:3:namespace TAOM.Features.CultureMarketplace;
Main\Features\CustomBattles\CustomBattleService.cs:72:string.Equals(c.CultureId, factionId, StringComparison.OrdinalIgnoreCase))
Main\Features\Diplomacy\Models\TaomDiplomacyModel.cs:34:&& supporter.Culture?.HasFeat(TaomCulturalFeats.IsengardDecisionPenaltyFeat) == true)
Main\Features\FactionMap\Widgets\PolygonWidget.cs:1053:System.Globalization.CultureInfo.InvariantCulture, out float x) &&
Main\Features\FactionMap\Widgets\PolygonWidget.cs:1055:System.Globalization.CultureInfo.InvariantCulture, out float y))
Main\Features\FactionMap\CultureSettingService.cs:39:// CharacterObject.PlayerCharacter.Culture (== Hero.MainHero.Culture). Assign the chosen
Main\Features\FactionMap\CultureSettingService.cs:45:Hero.MainHero.Culture = culture;
Main\Features\InitialChildGeneration\InitialChildGenerationConfigProvider.cs:86:config.CultureOverrides.Add(new CultureOverride
Main\Features\InitialChildGeneration\InitialChildGenerationService.cs:77:|| config.ExcludedCultures.Contains(clan.CultureId, StringComparer.OrdinalIgnoreCase);
Main\Features\InitialChildGeneration\InitialChildGenerationService.cs:89:var cultureOverride = config.CultureOverrides
Main\Features\InitialChildGeneration\InitialChildGenerationService.cs:90:.FirstOrDefault(c => string.Equals(c.CultureId, clan.CultureId, StringComparison.OrdinalIgnoreCase));
Main\Features\MissionDiagnostic\MissionDiagnosticService.cs:63:heroLine = $"MainHero='{hero.Name}', culture='{hero.Culture?.StringId}', kingdom='{hero.Clan?.Kingdom?.StringId ?? "none"}'";
Main\Features\SettlementGuards\SettlementGuardService.cs:28:?? _config.GetByCultureId(context.CultureId);
Main\Features\SettlementGuards\SettlementGuardService.cs:39:$"culture={context.CultureId} spawnPoint={spawnPointTag} → {troopId}");
Main\Features\ShaderPrecompilation\ShaderPrecompilationService.cs:27:&& c.CultureId != null
Main\Features\ShaderPrecompilation\ShaderPrecompilationService.cs:28:&& validCultureIds.Contains(c.CultureId))
Main\Features\Siege\SiegeDefenseService.cs:219:snapshot[kvp.Key] = $"{evt.DefenderFactionId}|{remainingHours.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{(evt.PlayerAccepted ? 1 : 0)}|{(evt.RewardClaimed ? 1 : 0)}";
Main\Features\Siege\SiegeDefenseService.cs:235:System.Globalization.CultureInfo.InvariantCulture, out var remainingHours)) continue;
Main\Features\SpecialResources\Hooks\PartyCharacterVM_InitializeUpgrades_Patch.cs:29:var cultureId = Hero.MainHero?.Culture?.StringId;
Main\Features\SpecialResources\Hooks\PartyScreenLogic_AddCommand_Patch.cs:37:var cultureId = Hero.MainHero?.Culture?.StringId;
Main\Features\SpecialResources\UI\SpecialResourceMapBarMixin.cs:39:var cultureId = hero.Culture?.StringId;
Main\Features\SpecialResources\UI\SpecialResourceMapBarMixin.cs:81:var cultureId = hero.Culture?.StringId;
Main\Features\SpecialResources\UI\SpecialResourceSpriteWidget.cs:35:var cultureId = hero?.Culture?.StringId;
Main\Features\SpecialResources\SpecialResourcesBehavior.cs:413:cultureId = hero?.Culture?.StringId;
Main\Features\StartupResources\PlayerStartupGoldService.cs:39:var entry = config.CultureEntries.FirstOrDefault(
Main\Features\StartupResources\PlayerStartupGoldService.cs:40:e => string.Equals(e.CultureId, cultureId, StringComparison.OrdinalIgnoreCase));
Main\Features\StartupResources\StartupGoldService.cs:43:if (lookup.TryGetValue(hero.CultureId.ToLowerInvariant(), out var entry) && entry.Gold > 0)
Main\Features\StartupResources\StartupGoldService.cs:57:foreach (var entry in config.CultureEntries)
Main\Features\StartupResources\StartupGoldService.cs:59:if (!string.IsNullOrEmpty(entry.CultureId))
Main\Features\StartupResources\StartupGoldService.cs:60:lookup[entry.CultureId.ToLowerInvariant()] = entry;
Main\Features\StartupResources\StartupInfluenceService.cs:36:if (lookup.TryGetValue(clan.CultureId.ToLowerInvariant(), out var entry) && entry.Influence > 0f)
Main\Features\StartupResources\StartupInfluenceService.cs:50:foreach (var entry in config.CultureEntries)
Main\Features\StartupResources\StartupInfluenceService.cs:52:if (!string.IsNullOrEmpty(entry.CultureId))
Main\Features\StartupResources\StartupInfluenceService.cs:53:lookup[entry.CultureId.ToLowerInvariant()] = entry;
Main\Features\StartupResources\StartupResourcesConfigProvider.cs:56:config.CultureEntries.Add(new CultureResourceEntry
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:70:var feats = ResolveMountedCostFeats(buyerHero?.Culture, troop.IsMounted);
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:91:var garrisonCulture = mobileParty.CurrentSettlement.Owner?.Culture;
Main\Features\TroopProgression\Models\TaomVolunteerModel.cs:58:var culture = CultureFeatAdapter.FromOrNull(settlement?.OwnerClan?.Culture);
Main\Features\TroopProgression\VolunteerContext.cs:12:// The settlement's CURRENT culture id (Settlement.Culture.StringId) — reflects any CultureConversion
Main\Features\TroopProgression\VolunteerRecruitmentService.cs:442:// troops). SettlementCultureId == the converted Settlement.Culture; conversion is gated on
Main\Features\TroopProgression\VolunteerRecruitmentService.cs:466:?? ResolvePool(context.CultureId, CultureMap);
Main\IoC.cs:47:using TAOM.Features.CultureMarketplace;
Main\IoC.cs:48:using TAOM.Features.CultureConversion;
Main\SubModule.cs:487:campaignStarter.AddBehavior(new Features.CultureMarketplace.CultureMarketplaceBehavior(
Main\SubModule.cs:488:IoC.Resolve<Features.CultureMarketplace.ICultureItemPoolService>(),
Main\SubModule.cs:489:IoC.Resolve<Features.CultureMarketplace.ICultureMarketplaceInjectionService>(),
Main\SubModule.cs:490:IoC.Resolve<Features.CultureMarketplace.ICultureMarketplaceMaintenanceService>(),
Main\SubModule.cs:492:IoC.Resolve<Features.CultureMarketplace.Domain.MarketplaceTuning>(),
Main\SubModule.cs:505:campaignStarter.AddBehavior(new Features.CultureConversion.Hooks.CultureConversionBehavior(
Main\SubModule.cs:506:IoC.Resolve<Features.CultureConversion.ICultureConversionService>(),
Main\SubModule.cs:507:IoC.Resolve<Features.CultureConversion.ICultureConversionStore>(),

codex
The broad `.Culture` search is mostly hero/settlement/clan fields, but I’m narrowing the potentially dangerous cases to code that operates on `PartyBase` or `MobileParty.Party`. I’m also checking whether service methods consistently no-op on null adapters.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/CulturalFeats/CulturalFeatsService.cs' | ForEach-Object { "'$i++; if ($i -ge 411) { '"'{0}:{1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main -Recurse -Include *.cs | Select-String -Pattern 'Party\\.Culture|\\.Party\\?\\.Culture|\\.Party\\.Culture|PartyBase.*Culture|winnerParty\\.Culture|LeaderParty.*Culture|party\\.Culture|mobileParty\\.Party.*Culture|FromOrNull\\(["'^'"\\r\\n]*Party|ResolvePartyCulture' | ForEach-Object { '{0}:{1}:{2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main/Features -Recurse -Include *Model.cs | Select-String -Pattern 'FromOrNull\\(|ResolvePartyCulture|ApplyFactor\\(' | ForEach-Object { '{0}:{1}:{2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main -Recurse -Include *.cs | Select-String -Pattern 'PartyBase' | ForEach-Object { '{0}:{1}:{2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 588ms:
411:    // ── PartyMorale ────────────────────────────────────────────────────
412:
413:    public void ApplyMoraleFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
414:    {
415:        if (culture == null)
416:            return;
417:        if (culture.HasFeat(TaomCulturalFeats.GondorMoraleFeat))
418:            result.Add(TaomCulturalFeats.GondorMoraleFeat.EffectBonus, CultureText);
419:        if (culture.HasFeat(TaomCulturalFeats.RohanMoraleFeat))
420:            result.Add(TaomCulturalFeats.RohanMoraleFeat.EffectBonus, CultureText);
421:        if (culture.HasFeat(TaomCulturalFeats.EreborMoraleFeat))
422:            result.Add(TaomCulturalFeats.EreborMoraleFeat.EffectBonus, CultureText);
423:        if (culture.HasFeat(TaomCulturalFeats.MirkwoodMoraleFeat))
424:            result.Add(TaomCulturalFeats.MirkwoodMoraleFeat.EffectBonus, CultureText);
425:        if (culture.HasFeat(TaomCulturalFeats.LothlorienMoraleFeat))
426:            result.Add(TaomCulturalFeats.LothlorienMoraleFeat.EffectBonus, CultureText);
427:        if (culture.HasFeat(TaomCulturalFeats.HaradMoraleFeat))
428:            result.Add(TaomCulturalFeats.HaradMoraleFeat.EffectBonus, CultureText);
429:    }
430:
431:    // ── Smithing ───────────────────────────────────────────────────────
432:
433:    public void ApplySmithingFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
434:    {
435:        if (culture == null)
436:            return;
437:        if (culture.HasFeat(TaomCulturalFeats.EreborSmithingFeat))
438:            result.AddFactor(TaomCulturalFeats.EreborSmithingFeat.EffectBonus);
439:        if (culture.HasFeat(TaomCulturalFeats.IsengardSmithingFeat))
440:            result.AddFactor(TaomCulturalFeats.IsengardSmithingFeat.EffectBonus);
441:        if (culture.HasFeat(TaomCulturalFeats.MordorSmithingFeat))
442:            result.AddFactor(TaomCulturalFeats.MordorSmithingFeat.EffectBonus);
443:        if (culture.HasFeat(TaomCulturalFeats.GoblinSmithingFeat))
444:            result.AddFactor(TaomCulturalFeats.GoblinSmithingFeat.EffectBonus);
445:        if (culture.HasFeat(TaomCulturalFeats.MistyMountainOrcsSmithingFeat))
446:            result.AddFactor(TaomCulturalFeats.MistyMountainOrcsSmithingFeat.EffectBonus);
447:    }
448:
449:    // ── ClanFinance (tariffs) ──────────────────────────────────────────
450:
451:    public void ApplyTariffIncomeFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
452:    {
453:        if (culture == null)
454:            return;
455:        if (culture.HasFeat(TaomCulturalFeats.UmbarTariffIncomeFeat))
456:            result.AddFactor(TaomCulturalFeats.UmbarTariffIncomeFeat.EffectBonus, CultureText);
457:        if (culture.HasFeat(TaomCulturalFeats.EreborTariffIncomeFeat))
458:            result.AddFactor(TaomCulturalFeats.EreborTariffIncomeFeat.EffectBonus, CultureText);
459:        if (culture.HasFeat(TaomCulturalFeats.DaleTariffIncomeFeat))
460:            result.AddFactor(TaomCulturalFeats.DaleTariffIncomeFeat.EffectBonus, CultureText);
461:        if (culture.HasFeat(TaomCulturalFeats.KhandTariffIncomeFeat))
462:            result.AddFactor(TaomCulturalFeats.KhandTariffIncomeFeat.EffectBonus, CultureText);
463:    }
464:
465:    // ── Raid ───────────────────────────────────────────────────────────
466:
467:    public void ApplyRaidDamageFeats(ICultureFeatAdapter? culture, ref ExplainedNumber result)
468:    {
469:        if (culture == null)
470:            return;
471:        if (culture.HasFeat(TaomCulturalFeats.MordorRaidDamageFeat))
472:            result.AddFactor(TaomCulturalFeats.MordorRaidDamageFeat.EffectBonus, CultureText);
473:        if (culture.HasFeat(TaomCulturalFeats.GundabadRaidDamageFeat))
474:            result.AddFactor(TaomCulturalFeats.GundabadRaidDamageFeat.EffectBonus, CultureText);
475:        if (culture.HasFeat(TaomCulturalFeats.IsengardRaidDamageFeat))
476:            result.AddFactor(TaomCulturalFeats.IsengardRaidDamageFeat.EffectBonus, CultureText);
477:        if (culture.HasFeat(TaomCulturalFeats.UmbarRaidDamageFeat))
478:            result.AddFactor(TaomCulturalFeats.UmbarRaidDamageFeat.EffectBonus, CultureText);
479:        if (culture.HasFeat(TaomCulturalFeats.GoblinRaidDamageFeat))
480:            result.AddFactor(TaomCulturalFeats.GoblinRaidDamageFeat.EffectBonus, CultureText);
481:        if (culture.HasFeat(TaomCulturalFeats.MistyMountainOrcsRaidDamageFeat))
482:            result.AddFactor(TaomCulturalFeats.MistyMountainOrcsRaidDamageFeat.EffectBonus, CultureText);
483:        if (culture.HasFeat(TaomCulturalFeats.HaradRaidDamageFeat))
484:            result.AddFactor(TaomCulturalFeats.HaradRaidDamageFeat.EffectBonus, CultureText);
485:        if (culture.HasFeat(TaomCulturalFeats.RhunRaidDamageFeat))
486:            result.AddFactor(TaomCulturalFeats.RhunRaidDamageFeat.EffectBonus, CultureText);
487:    }
488:}

 succeeded in 714ms:
Main\Features\BattleBalance\Models\TaomPartyHealingModel.cs:42:var culture = TAOM.Features.CulturalFeats.CultureFeatAdapter.ResolvePartyCulture(party);
Main\Features\CareerSystem\Models\TaomInventoryCapacityModel.cs:26:_careerPassives.ApplyFactor(mobileParty?.LeaderHero?.StringId, ref result, PassiveEffectType.InventoryCapacity);
Main\Features\CareerSystem\Models\TaomMapVisibilityModel.cs:20:_careerPassives.ApplyFactor(party?.LeaderHero?.StringId, ref result, PassiveEffectType.PartySpottingRange);
Main\Features\CulturalFeats\Models\TaomArmyManagementModel.cs:21:// Resolve party culture via the shared CultureFeatAdapter.FromOrNull(PartyBase) chokepoint —
Main\Features\CulturalFeats\Models\TaomArmyManagementModel.cs:27:CultureFeatAdapter.FromOrNull(armyMemberParty.Party),
Main\Features\CulturalFeats\Models\TaomArmyManagementModel.cs:32:CultureFeatAdapter.FromOrNull(armyLeaderParty.Party),
Main\Features\CulturalFeats\Models\TaomBattleRewardModel.cs:37:_feats.ApplyRenownFeats(CultureFeatAdapter.FromOrNull(winnerParty), ref result);
Main\Features\CulturalFeats\Models\TaomBattleRewardModel.cs:38:_careerPassives.ApplyFactor((winnerParty.Owner ?? winnerParty.LeaderHero)?.StringId, ref result, PassiveEffectType.BattleRenownGain);
Main\Features\CulturalFeats\Models\TaomBuildingConstructionModel.cs:21:_feats.ApplyConstructionSpeedFeats(CultureFeatAdapter.FromOrNull(town.OwnerClan?.Culture), ref result);
Main\Features\CulturalFeats\Models\TaomCaravanModel.cs:17:CultureFeatAdapter.FromOrNull(CharacterObject.PlayerCharacter?.Culture),
Main\Features\CulturalFeats\Models\TaomClanFinanceModel.cs:21:_feats.ApplyTariffIncomeFeats(CultureFeatAdapter.FromOrNull(clan?.Culture), ref result);
Main\Features\CulturalFeats\Models\TaomFoodConsumptionModel.cs:22:_feats.ApplyFoodConsumptionFeats(CultureFeatAdapter.FromOrNull(party.Party), ref result);
Main\Features\CulturalFeats\Models\TaomNotableSpawnModel.cs:35:var culture = CultureFeatAdapter.FromOrNull(settlement.Culture);
Main\Features\CulturalFeats\Models\TaomPartyMoraleModel.cs:26:_feats.ApplyMoraleFeats(CultureFeatAdapter.FromOrNull(party.Party), ref result);
Main\Features\CulturalFeats\Models\TaomPartyMoraleModel.cs:27:_careerPassives.ApplyFactor(party.LeaderHero?.StringId, ref result, PassiveEffectType.TroopMorale);
Main\Features\CulturalFeats\Models\TaomPartySizeModel.cs:25:// Vanilla PartyBaseHelper.HasFeat precedence — see CultureFeatAdapter.FromOrNull(PartyBase).
Main\Features\CulturalFeats\Models\TaomPartySizeModel.cs:28:_feats.ApplyPartySizeFeats(CultureFeatAdapter.FromOrNull(party), ref result);
Main\Features\CulturalFeats\Models\TaomPartySpeedModel.cs:30:var culture = CultureFeatAdapter.FromOrNull(mobileParty.Party);
Main\Features\CulturalFeats\Models\TaomPartySpeedModel.cs:40:_careerPassives.ApplyFactor(mobileParty.LeaderHero?.StringId, ref result, PassiveEffectType.PartyMovementSpeed);
Main\Features\CulturalFeats\Models\TaomPartyTroopUpgradeModel.cs:28:CultureFeatAdapter.FromOrNull(party),
Main\Features\CulturalFeats\Models\TaomPartyTroopUpgradeModel.cs:31:_careerPassives.ApplyFactor((party.Owner ?? party.LeaderHero)?.StringId, ref result, PassiveEffectType.TroopUpgradeCost);
Main\Features\CulturalFeats\Models\TaomRaidModel.cs:27:// FromOrNull(PartyBase). Replaces the prior Owner-only inline that skipped LeaderHero.Culture
Main\Features\CulturalFeats\Models\TaomRaidModel.cs:31:CultureFeatAdapter.FromOrNull(attackerSide?.LeaderParty),
Main\Features\CulturalFeats\Models\TaomRaidModel.cs:33:_careerPassives.ApplyFactor(attackerSide?.LeaderParty?.Owner?.StringId, ref result, PassiveEffectType.TroopDamage);
Main\Features\CulturalFeats\Models\TaomSettlementLoyaltyModel.cs:37:_feats.ApplyLoyaltyFeats(CultureFeatAdapter.FromOrNull(town.Owner?.Culture), ref result);
Main\Features\CulturalFeats\Models\TaomSettlementMilitiaModel.cs:19:_feats.ApplyVeteranMilitiaFeats(CultureFeatAdapter.FromOrNull(settlement.OwnerClan?.Culture), ref result);
Main\Features\CulturalFeats\Models\TaomSettlementProsperityModel.cs:20:_feats.ApplyHearthGrowthFeats(CultureFeatAdapter.FromOrNull(village.Settlement?.OwnerClan?.Culture), ref result);
Main\Features\CulturalFeats\Models\TaomSmithingModel.cs:37:_feats.ApplySmithingFeats(CultureFeatAdapter.FromOrNull(hero?.Culture), ref result);
Main\Features\CulturalFeats\Models\TaomSmithingModel.cs:38:_careerPassives.ApplyFactor(hero?.StringId, ref result, PassiveEffectType.EnchantmentCostReduction);
Main\Features\CulturalFeats\Models\TaomVillageProductionModel.cs:21:CultureFeatAdapter.FromOrNull(village.Settlement?.OwnerClan?.Culture),
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:50:// CultureFeatAdapter.ResolvePartyCulture chokepoint (vanilla PartyBaseHelper.HasFeat
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:55:var partyCulture = CultureFeatAdapter.ResolvePartyCulture(mobileParty.Party);
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:63:_careerPassives.ApplyFactor(mobileParty.LeaderHero?.StringId, ref result, PassiveEffectType.TroopWages);
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:90:// route through ResolvePartyCulture. See docs/features/cultural-feats.md.
Main\Features\TroopProgression\Models\TaomVolunteerModel.cs:58:var culture = CultureFeatAdapter.FromOrNull(settlement?.OwnerClan?.Culture);

 succeeded in 895ms:
Main\Adapters\IPlayerEquipmentAdapter.cs:17:/// depositing each non-empty <c>EquipmentElement</c> into <c>PartyBase.MainParty.ItemRoster</c>
Main\Adapters\PlayerEquipmentAdapter.cs:16:var party = PartyBase.MainParty;
Main\Features\BannerColorPersistence\Hooks\MapConversationTableau_SpawnOpponentBodyguard_Patch.cs:56:new[] { typeof(CharacterObject), typeof(int), typeof(PartyBase) });
Main\Features\BannerColorPersistence\Hooks\PartyVM_RefreshCurrentCharacterInformation_Patch.cs:31:PartyBase? ownerParty = null;
Main\Features\BattleBalance\Models\TaomCombatSimulationModel.cs:17:CharacterObject strikedTroop, PartyBase strikerParty, PartyBase strikedParty,
Main\Features\BattleBalance\Models\TaomPartyHealingModel.cs:23:public override float GetSurvivalChance(PartyBase party, CharacterObject character,
Main\Features\BattleBalance\Models\TaomPartyHealingModel.cs:24:DamageTypes damageType, bool canDamageKillEvenIfBlunt, PartyBase enemyParty = null)
Main\Features\BattleBalance\Models\TaomPartyHealingModel.cs:39:// Vanilla PartyBaseHelper.HasFeat precedence — same fix family Codex 43 + 46
Main\Features\CareerSystem\Quests\CareerQuest.cs:161:private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
Main\Features\CareerSystem\Quests\CareerQuest.cs:163:if (capturer != PartyBase.MainParty || prisoner == null || !prisoner.IsLord) return;
Main\Features\CulturalFeats\Models\TaomArmyManagementModel.cs:21:// Resolve party culture via the shared CultureFeatAdapter.FromOrNull(PartyBase) chokepoint —
Main\Features\CulturalFeats\Models\TaomArmyManagementModel.cs:22:// vanilla PartyBaseHelper.HasFeat precedence (LeaderHero-first, MapFaction-aware), null-safe.
Main\Features\CulturalFeats\Models\TaomBattleRewardModel.cs:22:PartyBase winnerParty,
Main\Features\CulturalFeats\Models\TaomBattleRewardModel.cs:34:// Vanilla PartyBaseHelper.HasFeat precedence via the shared helper. Replaces the prior
Main\Features\CulturalFeats\Models\TaomFoodConsumptionModel.cs:21:// Vanilla PartyBaseHelper.HasFeat precedence via the shared helper.
Main\Features\CulturalFeats\Models\TaomPartyMoraleModel.cs:25:// Vanilla PartyBaseHelper.HasFeat precedence via the shared helper.
Main\Features\CulturalFeats\Models\TaomPartySizeModel.cs:22:PartyBase party, bool includeDescriptions = false)
Main\Features\CulturalFeats\Models\TaomPartySizeModel.cs:25:// Vanilla PartyBaseHelper.HasFeat precedence — see CultureFeatAdapter.FromOrNull(PartyBase).
Main\Features\CulturalFeats\Models\TaomPartyTroopUpgradeModel.cs:22:PartyBase party, CharacterObject characterObject, CharacterObject upgradeTarget)
Main\Features\CulturalFeats\Models\TaomPartyTroopUpgradeModel.cs:25:// Vanilla PartyBaseHelper.HasFeat precedence via the shared CultureFeatAdapter helper —
Main\Features\CulturalFeats\Models\TaomRaidModel.cs:25:// Vanilla PartyBaseHelper.HasFeat precedence via the shared chokepoint (LeaderHero-first,
Main\Features\CulturalFeats\Models\TaomRaidModel.cs:26:// null-safe). MapEventSide.LeaderParty is a PartyBase, so pass it straight to
Main\Features\CulturalFeats\Models\TaomRaidModel.cs:27:// FromOrNull(PartyBase). Replaces the prior Owner-only inline that skipped LeaderHero.Culture
Main\Features\CulturalFeats\CultureFeatAdapter.cs:42:/// with the same precedence as vanilla <c>PartyBaseHelper.HasFeat</c>:
Main\Features\CulturalFeats\CultureFeatAdapter.cs:50:public static ICultureFeatAdapter? FromOrNull(PartyBase? party)
Main\Features\CulturalFeats\CultureFeatAdapter.cs:54:/// Boundary helper — same vanilla <c>PartyBaseHelper.HasFeat</c> precedence
Main\Features\CulturalFeats\CultureFeatAdapter.cs:55:/// as <see cref="FromOrNull(PartyBase?)"/> but returns the raw
Main\Features\CulturalFeats\CultureFeatAdapter.cs:62:public static CultureObject? ResolvePartyCulture(PartyBase? party)
Main\Features\Messengers\MessengerCampaignBehavior.cs:352:var mainParty = PartyBase.MainParty;
Main\Features\Messengers\MessengerCampaignBehavior.cs:363:PartyBase targetParty;
Main\Features\Messengers\MessengerCampaignBehavior.cs:586:if (PartyBase.MainParty == null) return false;
Main\Features\Siege\Models\TaomSiegeEventModel.cs:21:public override IEnumerable<SiegeEngineType> GetAvailableDefenderSiegeEngines(PartyBase party)
Main\Features\Siege\Models\TaomSiegeEventModel.cs:23:// Phase 9b #134 P1 — `party.MobileParty` is null for garrison defenders (PartyBase.IsMobile=false).
Main\Features\SpecialResources\SpecialResourcesBehavior.cs:381:PartyBase leftOwner, TroopRoster leftMembers, TroopRoster leftPrisoners,
Main\Features\SpecialResources\SpecialResourcesBehavior.cs:382:PartyBase rightOwner, TroopRoster rightMembers, TroopRoster rightPrisoners,
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:50:// CultureFeatAdapter.ResolvePartyCulture chokepoint (vanilla PartyBaseHelper.HasFeat
Main\Features\TroopWeight\Hooks\CampaignUIHelper_GetPartyHealthTooltip_Patch.cs:18:public static void Postfix(PartyBase party, ref List<TooltipProperty> __result)
Main\Features\TroopWeight\Hooks\IOnCampaignUIHelperGetPartyHealthTooltip.cs:9:void OnGetPartyHealthTooltip(PartyBase party, ref List<TooltipProperty> __result);
Main\Features\TroopWeight\Hooks\IOnPartyBaseHelperGetPartySizeText.cs:6:public interface IOnPartyBaseHelperGetPartySizeText
Main\Features\TroopWeight\Hooks\IOnPartyBaseHelperGetPartySizeText.cs:8:void OnGetPartySizeText(PartyBase party, ref TextObject __result);
Main\Features\TroopWeight\Hooks\IOnPartyBaseNumberOfAllMembers.cs:5:public interface IOnPartyBaseNumberOfAllMembers
Main\Features\TroopWeight\Hooks\IOnPartyBaseNumberOfAllMembers.cs:7:void OnPartyBaseNumberOfAllMembers(PartyBase partyBase, ref int __result);
Main\Features\TroopWeight\Hooks\IOnPartyBaseNumberOfRegularMembers.cs:5:public interface IOnPartyBaseNumberOfRegularMembers
Main\Features\TroopWeight\Hooks\IOnPartyBaseNumberOfRegularMembers.cs:7:void OnPartyBaseNumberOfRegularMembers(PartyBase partyBase, ref int __result);
Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:6:[HarmonyPatch(typeof(PartyBase), nameof(PartyBase.NumberOfAllMembers), MethodType.Getter)]
Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:8:public static class PartyBase_NumberOfAllMembers_Patch
Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:10:private static IOnPartyBaseNumberOfAllMembers? _hook;
Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:12:public static void Initialize(IOnPartyBaseNumberOfAllMembers hook) => _hook = hook;
Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:15:public static void Postfix(PartyBase __instance, ref int __result)
Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:18:_hook?.OnPartyBaseNumberOfAllMembers(__instance, ref __result);
Main\Features\TroopWeight\Hooks\PartyBase_NumberOfRegularMembers_Patch.cs:6:[HarmonyPatch(typeof(PartyBase), nameof(PartyBase.NumberOfRegularMembers), MethodType.Getter)]
Main\Features\TroopWeight\Hooks\PartyBase_NumberOfRegularMembers_Patch.cs:8:public static class PartyBase_NumberOfRegularMembers_Patch
Main\Features\TroopWeight\Hooks\PartyBase_NumberOfRegularMembers_Patch.cs:10:private static IOnPartyBaseNumberOfRegularMembers? _hook;
Main\Features\TroopWeight\Hooks\PartyBase_NumberOfRegularMembers_Patch.cs:12:public static void Initialize(IOnPartyBaseNumberOfRegularMembers hook) => _hook = hook;
Main\Features\TroopWeight\Hooks\PartyBase_NumberOfRegularMembers_Patch.cs:15:public static void Postfix(PartyBase __instance, ref int __result)
Main\Features\TroopWeight\Hooks\PartyBase_NumberOfRegularMembers_Patch.cs:18:_hook?.OnPartyBaseNumberOfRegularMembers(__instance, ref __result);
Main\Features\TroopWeight\Hooks\PartyBaseHelper_GetPartySizeText_Patch.cs:9:// Targets the PartyBase overload only (there is also GetPartySizeText(int, int, bool)).
Main\Features\TroopWeight\Hooks\PartyBaseHelper_GetPartySizeText_Patch.cs:10:[HarmonyPatch(typeof(PartyBaseHelper), nameof(PartyBaseHelper.GetPartySizeText), new[] { typeof(PartyBase) })]
Main\Features\TroopWeight\Hooks\PartyBaseHelper_GetPartySizeText_Patch.cs:12:public static class PartyBaseHelper_GetPartySizeText_Patch
Main\Features\TroopWeight\Hooks\PartyBaseHelper_GetPartySizeText_Patch.cs:14:private static IOnPartyBaseHelperGetPartySizeText? _hook;
Main\Features\TroopWeight\Hooks\PartyBaseHelper_GetPartySizeText_Patch.cs:16:public static void Initialize(IOnPartyBaseHelperGetPartySizeText hook) => _hook = hook;
Main\Features\TroopWeight\Hooks\PartyBaseHelper_GetPartySizeText_Patch.cs:19:public static void Postfix(PartyBase party, ref TextObject __result)
Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:8:public class PartyBaseNumberOfAllMembersHook : IOnPartyBaseNumberOfAllMembers
Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:13:public PartyBaseNumberOfAllMembersHook(ITroopWeightService troopWeightService, IModLogger logger)
Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:18:public void OnPartyBaseNumberOfAllMembers(PartyBase partyBase, ref int __result)
Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:22:if (partyBase?.MemberRoster == null)
Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:25:int cacheKey = partyBase.GetHashCode();
Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:26:int currentVersion = partyBase.MemberRoster.VersionNo;
Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:35:var weightedCount = _troopWeightService.CalculateWeightedMemberCount(partyBase);
Main\Features\TroopWeight\Hooks\PartyBaseNumberOfRegularMembersHook.cs:8:public class PartyBaseNumberOfRegularMembersHook : IOnPartyBaseNumberOfRegularMembers
Main\Features\TroopWeight\Hooks\PartyBaseNumberOfRegularMembersHook.cs:13:public PartyBaseNumberOfRegularMembersHook(ITroopWeightService troopWeightService, IModLogger logger)
Main\Features\TroopWeight\Hooks\PartyBaseNumberOfRegularMembersHook.cs:18:public void OnPartyBaseNumberOfRegularMembers(PartyBase partyBase, ref int __result)
Main\Features\TroopWeight\Hooks\PartyBaseNumberOfRegularMembersHook.cs:22:if (partyBase?.MemberRoster == null)
Main\Features\TroopWeight\Hooks\PartyBaseNumberOfRegularMembersHook.cs:25:int cacheKey = partyBase.GetHashCode();
Main\Features\TroopWeight\Hooks\PartyBaseNumberOfRegularMembersHook.cs:26:int currentVersion = partyBase.MemberRoster.VersionNo;
Main\Features\TroopWeight\Hooks\PartyBaseNumberOfRegularMembersHook.cs:38:int rosterCount = partyBase.MemberRoster.Count;
Main\Features\TroopWeight\Hooks\PartyBaseNumberOfRegularMembersHook.cs:41:var element = partyBase.MemberRoster.GetElementCopyAtIndex(i);
Main\Features\TroopWeight\Hooks\RecruitmentVMRefreshPartyPropertiesHook.cs:37:var basePartyWeightedCount = PartyBase.MainParty?.NumberOfAllMembers ?? 0;
Main\Features\TroopWeight\Hooks\TroopWeightDisplayHook.cs:20:/// Display-only: we deliberately do NOT weight <c>PartyBase.NumberOfHealthyMembers</c> itself
Main\Features\TroopWeight\Hooks\TroopWeightDisplayHook.cs:28:IOnPartyBaseHelperGetPartySizeText
Main\Features\TroopWeight\Hooks\TroopWeightDisplayHook.cs:71:public void OnGetPartyHealthTooltip(PartyBase party, ref List<TooltipProperty> __result)
Main\Features\TroopWeight\Hooks\TroopWeightDisplayHook.cs:116:public void OnGetPartySizeText(PartyBase party, ref TextObject __result)
Main\Features\TroopWeight\Hooks\TroopWeightDisplayHook.cs:140:_logger.LogWarning($"[TroopWeight] PartyBaseHelper.GetPartySizeText hook error: {ex.GetType().Name}: {ex.Message}");
Main\Features\TroopWeight\Hooks\TroopWeightDisplayHook.cs:151:private void RewriteHealthTooltip(List<TooltipProperty> list, PartyBase party)
Main\Features\TroopWeight\ITroopWeightService.cs:12:float CalculateWeightedMemberCount(PartyBase party);
Main\Features\TroopWeight\ITroopWeightService.cs:30:/// PartyBase / TroopRoster) — the math it delegates to is.
Main\Features\TroopWeight\ITroopWeightService.cs:32:(int Healthy, int Wounded) GetWeightedHealthAndWounded(PartyBase party);
Main\Features\TroopWeight\TroopWeightIoC.cs:13:container.Register<IOnPartyBaseNumberOfAllMembers, PartyBaseNumberOfAllMembersHook>(Reuse.Singleton);
Main\Features\TroopWeight\TroopWeightIoC.cs:14:container.Register<IOnPartyBaseNumberOfRegularMembers, PartyBaseNumberOfRegularMembersHook>(Reuse.Singleton);
Main\Features\TroopWeight\TroopWeightIoC.cs:23:IOnPartyBaseNumberOfAllMembers allMembersHook,
Main\Features\TroopWeight\TroopWeightIoC.cs:24:IOnPartyBaseNumberOfRegularMembers regularMembersHook,
Main\Features\TroopWeight\TroopWeightIoC.cs:29:PartyBase_NumberOfAllMembers_Patch.Initialize(allMembersHook);
Main\Features\TroopWeight\TroopWeightIoC.cs:30:PartyBase_NumberOfRegularMembers_Patch.Initialize(regularMembersHook);
Main\Features\TroopWeight\TroopWeightIoC.cs:38:PartyBaseHelper_GetPartySizeText_Patch.Initialize(displayHook);
Main\Features\TroopWeight\TroopWeightService.cs:18:// is called on the nameplate path (PartyBaseHelper.GetPartySizeText) for every visible party each
Main\Features\TroopWeight\TroopWeightService.cs:21:private readonly ConditionalWeakTable<PartyBase, WeightedHealthBox> _healthCache = new();
Main\Features\TroopWeight\TroopWeightService.cs:51:public float CalculateWeightedMemberCount(PartyBase party)
Main\Features\TroopWeight\TroopWeightService.cs:127:public (int Healthy, int Wounded) GetWeightedHealthAndWounded(PartyBase party)
Main\SubModule.cs:180:IoC.Resolve<IOnPartyBaseNumberOfAllMembers>(),
Main\SubModule.cs:181:IoC.Resolve<IOnPartyBaseNumberOfRegularMembers>(),

 succeeded in 972ms:
Main\Features\BattleBalance\Models\TaomPartyHealingModel.cs:42:var culture = TAOM.Features.CulturalFeats.CultureFeatAdapter.ResolvePartyCulture(party);
Main\Features\CulturalFeats\Models\TaomArmyManagementModel.cs:21:// Resolve party culture via the shared CultureFeatAdapter.FromOrNull(PartyBase) chokepoint —
Main\Features\CulturalFeats\Models\TaomArmyManagementModel.cs:27:CultureFeatAdapter.FromOrNull(armyMemberParty.Party),
Main\Features\CulturalFeats\Models\TaomArmyManagementModel.cs:32:CultureFeatAdapter.FromOrNull(armyLeaderParty.Party),
Main\Features\CulturalFeats\Models\TaomBattleRewardModel.cs:35:// `winnerParty.Owner?.Culture ?? winnerParty.Culture`: winnerParty.Culture is `MapFaction.Culture`
Main\Features\CulturalFeats\Models\TaomBattleRewardModel.cs:37:_feats.ApplyRenownFeats(CultureFeatAdapter.FromOrNull(winnerParty), ref result);
Main\Features\CulturalFeats\Models\TaomFoodConsumptionModel.cs:22:_feats.ApplyFoodConsumptionFeats(CultureFeatAdapter.FromOrNull(party.Party), ref result);
Main\Features\CulturalFeats\Models\TaomPartyMoraleModel.cs:26:_feats.ApplyMoraleFeats(CultureFeatAdapter.FromOrNull(party.Party), ref result);
Main\Features\CulturalFeats\Models\TaomPartySizeModel.cs:25:// Vanilla PartyBaseHelper.HasFeat precedence — see CultureFeatAdapter.FromOrNull(PartyBase).
Main\Features\CulturalFeats\Models\TaomPartySizeModel.cs:26:// Replaces the prior `party.Owner?.Culture ?? party.Culture` which skipped LeaderHero.Culture
Main\Features\CulturalFeats\Models\TaomPartySizeModel.cs:28:_feats.ApplyPartySizeFeats(CultureFeatAdapter.FromOrNull(party), ref result);
Main\Features\CulturalFeats\Models\TaomPartySpeedModel.cs:30:var culture = CultureFeatAdapter.FromOrNull(mobileParty.Party);
Main\Features\CulturalFeats\Models\TaomPartyTroopUpgradeModel.cs:25:// Vanilla PartyBaseHelper.HasFeat precedence via the shared CultureFeatAdapter helper —
Main\Features\CulturalFeats\Models\TaomPartyTroopUpgradeModel.cs:28:CultureFeatAdapter.FromOrNull(party),
Main\Features\CulturalFeats\Models\TaomRaidModel.cs:27:// FromOrNull(PartyBase). Replaces the prior Owner-only inline that skipped LeaderHero.Culture
Main\Features\CulturalFeats\Models\TaomRaidModel.cs:31:CultureFeatAdapter.FromOrNull(attackerSide?.LeaderParty),
Main\Features\CulturalFeats\CultureFeatAdapter.cs:50:public static ICultureFeatAdapter? FromOrNull(PartyBase? party)
Main\Features\CulturalFeats\CultureFeatAdapter.cs:51:=> FromOrNull(ResolvePartyCulture(party));
Main\Features\CulturalFeats\CultureFeatAdapter.cs:55:/// as <see cref="FromOrNull(PartyBase?)"/> but returns the raw
Main\Features\CulturalFeats\CultureFeatAdapter.cs:62:public static CultureObject? ResolvePartyCulture(PartyBase? party)
Main\Features\CulturalFeats\CultureFeatAdapter.cs:66:// party.Culture is `MapFaction.Culture` with no null guard — it NREs when
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:50:// CultureFeatAdapter.ResolvePartyCulture chokepoint (vanilla PartyBaseHelper.HasFeat
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:55:var partyCulture = CultureFeatAdapter.ResolvePartyCulture(mobileParty.Party);
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:90:// route through ResolvePartyCulture. See docs/features/cultural-feats.md.

codex
So far, I see no live `party.Culture` call left; the only hits are comments plus party-cultural callers already using `FromOrNull(PartyBase)` or `ResolvePartyCulture`. I’m reading the documentation/RCA sections now to verify the intended scoping for wage and garrison behavior.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'.claude/rules/adapters.md' | ForEach-Object { "'$i++; if ($_ -match '"'computed getter|PartyBase|Culture|throw|throws|null guard|before') { '{0}:{1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'docs/features/cultural-feats.md' | ForEach-Object { "'$i++; if ($_ -match '"'Party-Culture|ResolvePartyCulture|party-culture|garrison|Party Wage|wage|career') { '{0}:{1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'docs/reviews/rca-culturefeat-partyculture-nre-2026-06-15.md' | ForEach-Object { "'$i++; '"'{0}:{1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Get-ChildItem -Path Main/Features/CulturalFeats/Models -Filter *.cs | ForEach-Object { $path=$_.FullName; Select-String -Path $path -Pattern '"'FromOrNull\\(|ResolvePartyCulture|Owner\\?\\.Culture|OwnerClan\\?\\.Culture|Settlement\\?\\.Culture|LeaderHero\\?\\.StringId|Owner\\?\\.StringId|ApplyFactor\\(' | ForEach-Object { '{0}:{1}:{2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 543ms:
1:# RCA — `CultureFeatAdapter.ResolvePartyCulture` NRE (PartyBase.Culture on a faction-less party)
2:
3:**Date:** 2026-06-15
4:**Feature:** CulturalFeats (party-culture feat resolution)
5:**Severity of shipped bug:** HIGH (campaign-map hard crash)
6:**Fix commit:** _pending_ (CultureFeatAdapter.cs + TaomBattleRewardModel.cs)
7:
8:## Top-line summary
9:
10:A `NullReferenceException` crashed the campaign map tick during `Army.OnSiegeStarted` → `IsWaitingForArmyMembers` → per-member-party `EstimatedStrength` → `GetPowerOfParty` → party `Morale`/`PartySizeLimit`. Two reported stack traces both terminated at [CultureFeatAdapter.cs](../../Main/Features/CulturalFeats/CultureFeatAdapter.cs) line 68, which called `party.Culture` directly.
11:
12:`PartyBase.Culture` is `MapFaction.Culture` with **no null guard** (PartyBase.cs:255; `MapFaction` returns null when both `MobileParty` and `Settlement` lack a faction, PartyBase.cs:236-250). The crashing party `lord_1_3_party_1` ("Gorwulf, The Boar") had `LeaderHero == null`, `MapFaction == null`, `Owner != null`. So the engine getter dereferenced a null `MapFaction` and threw **inside the getter** — the TAOM `if (party.Culture != null)` guard was useless because the getter throws *before* it can return.
13:
14:The fix routes all party-culture resolution through a single null-safe `?.` chain (`party.LeaderHero?.Culture ?? party.MapFaction?.Culture ?? party.Owner?.Culture ?? party.Settlement?.Culture`) and migrates the one other direct-getter caller (`TaomBattleRewardModel`) onto it.
15:
16:## Findings
17:
18:| # | Sev | Bug | Category | Why missed | Preventive action |
19:|---|-----|-----|----------|------------|-------------------|
20:| 1 | HIGH | `ResolvePartyCulture` called `party.Culture` (= `MapFaction.Culture`, unguarded) → NRE when `MapFaction == null` | Null-safety / computed-getter | The code copied vanilla `PartyBaseHelper.HasFeat`'s precedence *verbatim*, including its `if (party.Culture != null)` line — assuming vanilla's guard was sufficient. It is not: the guard reads a property whose getter throws. And the latent vanilla bug never fires in vanilla because vanilla never calls `HasFeat` per-party in the party-size/morale models. | Route every party-culture resolution through the single `ResolvePartyCulture` chokepoint; never call `PartyBase.Culture` directly. `.claude/rules/adapters.md` already mandates `?.` on computed getters — this is that rule applied to `PartyBase.Culture`. New feedback memory codifies the "vanilla helper called on more entities than vanilla does" angle. |
21:| 2 | LOW | `TaomBattleRewardModel` had the same `winnerParty.Culture` exposure as a `??` fallback, plus old `Owner ?? Culture` precedence skipping `LeaderHero` | Consistency / null-safety | It was the single remaining caller using the pre-chokepoint inline pattern (the gap Codex review 43 flagged for other models but didn't sweep here). | Migrated to `FromOrNull(winnerParty)`. Data-flow review confirmed no other inline `PartyBase.Culture` caller remains in `Main\`. |
22:
23:## Root-cause pattern
24:
25:**TAOM invokes vanilla resolution logic on a broader set of entities than vanilla itself does, surfacing latent vanilla NREs that vanilla never triggers.** Vanilla `PartyBaseHelper.HasFeat` calls `party.Culture`, but vanilla only calls `HasFeat` in narrow contexts. TAOM's GameModels (`TaomPartySizeModel`, `TaomPartyMoraleModel`, `TaomPartySpeedModel`, `TaomFoodConsumptionModel`, `TaomPartyTroopUpgradeModel`) call it on **every** party-size/morale/speed/food/upgrade calc — including faction-less lord parties mid-army-siege-start, which vanilla's code path never reaches. Copying vanilla's logic verbatim therefore inherited a latent crash that only TAOM's broader call frequency could trigger.
26:
27:This is the same shape as `feedback_ported_data_upstream_bugs_vanilla_baseline.md` (1-for-1 ports inherit upstream bugs) generalized from data to *control flow*: a verbatim logic copy inherits the original's unstated preconditions, and TAOM violates those preconditions by calling it more widely.
28:
29:## Why each deep-review agent's rule set is relevant
30:
31:This crash was found in production (live debug session), not by the review — the review was run on the *fix*. For the record, of the 5 agents:
32:
33:- **Agent 1 (Standards):** would not have caught the original — calling `party.Culture` is not an ADR violation; the boundary adapter is the correct place to touch sealed types.
34:- **Agent 2 (API Compat):** would catch it only if its prompt asked "does this computed getter throw internally?" — which it now does (it verified `Hero.Culture`/`Settlement.Culture` are safe fields but `PartyBase.Culture` is an unguarded computed getter). This is the agent best positioned to catch the *class* of bug going forward.
35:- **Agent 3 (Efficiency):** out of scope.
36:- **Agent 4 (Completeness):** out of scope (it checks tests/docs/issue, not getter safety).
37:- **Agent 5 (Data Flow):** the right agent — its "remaining direct `PartyBase.Culture` exposures across `Main\`" trace is exactly the sweep that confirms the blast radius is closed. Had this trace been run before the fix shipped originally, it would have flagged the unguarded call.
38:
39:## Preventive rule / memory
40:
41:- **`.claude/rules/adapters.md`** already mandated `?.` on computed properties; strengthened (2026-06-15) with the concrete named trap — `PartyBase.Culture => MapFaction.Culture`, the computed-getter-vs-plain-field distinction, and "route through one null-safe chokepoint, never inline" — so it fires for every future adapter edit. The original rule existed but wasn't applied when the verbatim vanilla precedence was copied.
42:- **New feedback memory** `feedback_taleworlds_computed_getter_nre_route_through_chokepoint.md` — codifies (a) the computed-getter-throws-before-null-check trap for `PartyBase.Culture` specifically, and (b) the systemic "TAOM calls a vanilla helper on more entities than vanilla does → latent vanilla NRE goes live" pattern, with the chokepoint-resolution fix.
43:- **Follow-on hardening (2026-06-15):** swept the three remaining Owner-only party-culture callers — `TaomArmyManagementModel` (influence award + cost), `TaomRaidModel` (raid damage), `TaomPartyWageModel` line 49 (party wage) — onto the `ResolvePartyCulture` chokepoint, so **all 9** party-culture feat models now resolve identically (LeaderHero-first, null-safe). They never hit the throwing `party.Culture` (they used `Owner?.Culture`, so no crash), but inline resolution left the door open for a future `?? party.Culture` fallback to reintroduce the NRE — uniformity closes that. Garrison wage (`TaomPartyWageModel` line 82, settlement-owner-scoped) and per-hero `StringId` passives are correctly excluded. Verified pre-edit (API type + adversarial semantics) and post-edit (diff-correctness + full uniformity sweep) by 2-agent workflows; build clean, 3169 tests green. Behavior shift documented in [`cultural-feats.md`](../features/cultural-feats.md).
44:
45:## Tests
46:
47:`ResolvePartyCulture(PartyBase)` is engine-boundary (sealed `PartyBase`, requires live `Campaign.Current`) — not unit-testable in the MSTest harness (ADR-008 "test via game"). Verified by: full build clean (0 errors, `-p:ModuleId=` to skip the game-folder copy while the game was running), 3169 tests green, and the pending in-game retest of the crashing save.

 succeeded in 550ms:
14:1. **Research first** — Decompile the TaleWorlds class with `ilspycmd` before creating the adapter interface
25:**A computed getter throws BEFORE your `!= null` guard can run — guard the inner object, not the result.** Decompile the getter body first: a property defined as `X => A.B` (e.g. `PartyBase.Culture => MapFaction.Culture`, PartyBase.cs:255) dereferences `A` with no guard, so `if (party.Culture != null)` NREs inside the getter when `MapFaction` is null. Write `party.MapFaction?.Culture` instead. Distinguish computed getters (throw) from plain `[SaveableField]` fields (`Hero.Culture`, `Settlement.Culture` — safe once the parent is non-null) by reading the member definition, not by assuming. When several models resolve the same value, funnel them through one null-safe chokepoint (e.g. `CultureFeatAdapter.ResolvePartyCulture`) and never resolve inline — a future inline `?? party.Culture` fallback silently reintroduces the crash. This shipped as a campaign-map NRE: issue #281, RCA `docs/reviews/rca-culturefeat-partyculture-nre-2026-06-15.md`. Note that copying a vanilla helper verbatim (`PartyBaseHelper.HasFeat`) inherits its unstated preconditions — TAOM hit the NRE because it calls the helper on far more parties than vanilla does.
41:**Rule:** Before calling a `(ItemObject, ...)` overload, search for the parallel `(EquipmentElement, ...)` form. If it exists, prefer it. Update the adapter's internal data to carry the full `EquipmentElement` (not bare `ItemObject` or `string` ID).

 succeeded in 570ms:
13:- **Without this feature:** Every culture plays identically in terms of garrison wages, army costs, production bonuses
39:### Party-Culture Resolution — the `ResolvePartyCulture` chokepoint
41:GameModels that key feats off a **party's** culture (PartySize, PartyMorale, PartySpeed, FoodConsumption, PartyTroopUpgrade, BattleReward) resolve culture through one boundary helper — `CultureFeatAdapter.ResolvePartyCulture(PartyBase)` and its `FromOrNull(PartyBase)` wrapper — never inline. It mirrors vanilla `PartyBaseHelper.HasFeat` precedence, every step null-safe:
52:**Uniformity (2026-06-15):** all **9** party-culture feat models route through this chokepoint — PartySize, PartyMorale, PartySpeed, FoodConsumption, PartyTroopUpgrade, BattleReward, and (swept 2026-06-15) ArmyManagement, Raid, and PartyWage (line 49). The last three previously resolved inline as `Owner?.Culture` — null-safe (never the throwing `party.Culture`, so no crash) but precedence-inconsistent, skipping `LeaderHero`. Migrating them makes every party-culture feat resolve identically and matches vanilla `HasFeat`; it also closes the door on a future `?? party.Culture` fallback silently reintroducing the NRE. *Behavior shift:* a hero of culture A leading a party owned by a culture-B clan now gets A's army-influence / raid-damage / wage feats (the leader's culture), not B's. **Deliberately excluded** because they are *not* party-scoped: garrison wage (`TaomPartyWageModel.ResolveGarrisonInputs`, keyed on the fief owner's culture), the settlement-owner feat models (`TaomSettlementLoyaltyModel`, `TaomBuildingConstructionModel`, `TaomSettlementMilitiaModel`), and per-hero `StringId` career passives.
62:  <feat id="taom_erebor_garrison_wage" />
75:| Erebor | `taom_erebor_garrison_wage` | -25% garrison wage | -0.25 | Yes |
92:| Lothlorien | `taom_lothlorien_garrison_wage` | -20% garrison wage | -0.2 | Yes |
98:| Isengard | `taom_isengard_garrison_wage` | -20% garrison wage | -0.2 | Yes |
106:| Gundabad | `taom_gundabad_wage` | +10% party wages | 0.1 | No |
111:| Umbar | `taom_umbar_wage` | +8% party wages | 0.08 | No |
118:| Gondor | `taom_gondor_garrison_wage` | -20% garrison wage | -0.2 | Yes |
126:| Mordor | `taom_mordor_wage` | +20% party wages | 0.2 | No |
130:| Rohan | `taom_rohan_mounted_wage` | -15% mounted troop wages | -0.15 | Yes |
240:| `Main/Features/TroopProgression/Models/TaomPartyWageModel.cs` | Wage/garrison feat checks (extended) |
289:**Deferred to Wave 1.5** (need conditional logic / a different model — E-class, not Q): Goblin Sunlight Aversion (daylight-conditional speed), Mirkwood Spider-Tainted Paths (non-forest-conditional speed), Rhûn Cavalry-Only (infantry-% conditional speed), Mirkwood Thranduil's Vaults (garrison wage lives in `TaomPartyWageModel`, not `CulturalFeatsService`).
310:7. **Update the CC faction-map page.** Edit the matching faction's entry in [`Main/_Module/ModuleData/factionmap/factions.json`](../../Main/_Module/ModuleData/factionmap/factions.json) — add the feat to `perks[]` (lore-named flagship positives), `bonuses[]` (concrete game-effect line with the correct `positive: true|false` flag), and `weaknesses[]` (if it's a real negative — Rohan infantry penalty, Isengard relationship +25%, Mordor wages +20%, etc.). Without this, the player's starting-culture page in CC silently lies about what shipped. Standing instruction per `feedback_faction_map_update_with_cultural_feats.md`. The JSON ↔ culture-StringId mapping is in that memory file (e.g., `stewardship_of_gondor` → `gondor`, `kingdom_of_rohan` → `vlandia`).

 succeeded in 538ms:
Main\Features\CulturalFeats\Models\TaomArmyManagementModel.cs:21:// Resolve party culture via the shared CultureFeatAdapter.FromOrNull(PartyBase) chokepoint —
Main\Features\CulturalFeats\Models\TaomArmyManagementModel.cs:27:CultureFeatAdapter.FromOrNull(armyMemberParty.Party),
Main\Features\CulturalFeats\Models\TaomArmyManagementModel.cs:32:CultureFeatAdapter.FromOrNull(armyLeaderParty.Party),
Main\Features\CulturalFeats\Models\TaomBattleRewardModel.cs:35:// `winnerParty.Owner?.Culture ?? winnerParty.Culture`: winnerParty.Culture is `MapFaction.Culture`
Main\Features\CulturalFeats\Models\TaomBattleRewardModel.cs:37:_feats.ApplyRenownFeats(CultureFeatAdapter.FromOrNull(winnerParty), ref result);
Main\Features\CulturalFeats\Models\TaomBattleRewardModel.cs:38:_careerPassives.ApplyFactor((winnerParty.Owner ?? winnerParty.LeaderHero)?.StringId, ref result, PassiveEffectType.BattleRenownGain);
Main\Features\CulturalFeats\Models\TaomBuildingConstructionModel.cs:21:_feats.ApplyConstructionSpeedFeats(CultureFeatAdapter.FromOrNull(town.OwnerClan?.Culture), ref result);
Main\Features\CulturalFeats\Models\TaomCaravanModel.cs:17:CultureFeatAdapter.FromOrNull(CharacterObject.PlayerCharacter?.Culture),
Main\Features\CulturalFeats\Models\TaomClanFinanceModel.cs:21:_feats.ApplyTariffIncomeFeats(CultureFeatAdapter.FromOrNull(clan?.Culture), ref result);
Main\Features\CulturalFeats\Models\TaomFoodConsumptionModel.cs:22:_feats.ApplyFoodConsumptionFeats(CultureFeatAdapter.FromOrNull(party.Party), ref result);
Main\Features\CulturalFeats\Models\TaomNotableSpawnModel.cs:35:var culture = CultureFeatAdapter.FromOrNull(settlement.Culture);
Main\Features\CulturalFeats\Models\TaomPartyMoraleModel.cs:26:_feats.ApplyMoraleFeats(CultureFeatAdapter.FromOrNull(party.Party), ref result);
Main\Features\CulturalFeats\Models\TaomPartyMoraleModel.cs:27:_careerPassives.ApplyFactor(party.LeaderHero?.StringId, ref result, PassiveEffectType.TroopMorale);
Main\Features\CulturalFeats\Models\TaomPartySizeModel.cs:25:// Vanilla PartyBaseHelper.HasFeat precedence — see CultureFeatAdapter.FromOrNull(PartyBase).
Main\Features\CulturalFeats\Models\TaomPartySizeModel.cs:26:// Replaces the prior `party.Owner?.Culture ?? party.Culture` which skipped LeaderHero.Culture
Main\Features\CulturalFeats\Models\TaomPartySizeModel.cs:28:_feats.ApplyPartySizeFeats(CultureFeatAdapter.FromOrNull(party), ref result);
Main\Features\CulturalFeats\Models\TaomPartySpeedModel.cs:30:var culture = CultureFeatAdapter.FromOrNull(mobileParty.Party);
Main\Features\CulturalFeats\Models\TaomPartySpeedModel.cs:40:_careerPassives.ApplyFactor(mobileParty.LeaderHero?.StringId, ref result, PassiveEffectType.PartyMovementSpeed);
Main\Features\CulturalFeats\Models\TaomPartyTroopUpgradeModel.cs:28:CultureFeatAdapter.FromOrNull(party),
Main\Features\CulturalFeats\Models\TaomPartyTroopUpgradeModel.cs:31:_careerPassives.ApplyFactor((party.Owner ?? party.LeaderHero)?.StringId, ref result, PassiveEffectType.TroopUpgradeCost);
Main\Features\CulturalFeats\Models\TaomRaidModel.cs:27:// FromOrNull(PartyBase). Replaces the prior Owner-only inline that skipped LeaderHero.Culture
Main\Features\CulturalFeats\Models\TaomRaidModel.cs:31:CultureFeatAdapter.FromOrNull(attackerSide?.LeaderParty),
Main\Features\CulturalFeats\Models\TaomRaidModel.cs:33:_careerPassives.ApplyFactor(attackerSide?.LeaderParty?.Owner?.StringId, ref result, PassiveEffectType.TroopDamage);
Main\Features\CulturalFeats\Models\TaomSettlementLoyaltyModel.cs:37:_feats.ApplyLoyaltyFeats(CultureFeatAdapter.FromOrNull(town.Owner?.Culture), ref result);
Main\Features\CulturalFeats\Models\TaomSettlementMilitiaModel.cs:19:_feats.ApplyVeteranMilitiaFeats(CultureFeatAdapter.FromOrNull(settlement.OwnerClan?.Culture), ref result);
Main\Features\CulturalFeats\Models\TaomSettlementProsperityModel.cs:20:_feats.ApplyHearthGrowthFeats(CultureFeatAdapter.FromOrNull(village.Settlement?.OwnerClan?.Culture), ref result);
Main\Features\CulturalFeats\Models\TaomSmithingModel.cs:37:_feats.ApplySmithingFeats(CultureFeatAdapter.FromOrNull(hero?.Culture), ref result);
Main\Features\CulturalFeats\Models\TaomSmithingModel.cs:38:_careerPassives.ApplyFactor(hero?.StringId, ref result, PassiveEffectType.EnchantmentCostReduction);
Main\Features\CulturalFeats\Models\TaomVillageProductionModel.cs:21:CultureFeatAdapter.FromOrNull(village.Settlement?.OwnerClan?.Culture),

codex
The docs explicitly call out the intended 9 party-culture models and exclusions. I’m doing one more pass over the sibling model bodies so the final verdict can distinguish missed party sites from intentionally settlement- or hero-scoped sites.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'docs/features/cultural-feats.md' | ForEach-Object { "'$i++; if ($i -ge 35 -and $i -le 58) { '"'{0}:{1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs' | ForEach-Object { "'$i++; '"'{0}:{1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/CulturalFeats/Models/TaomFoodConsumptionModel.cs' | ForEach-Object { "'$i++; '"'{0}:{1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/CulturalFeats/Models/TaomPartyMoraleModel.cs' | ForEach-Object { "'$i++; '"'{0}:{1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 520ms:
1:using TaleWorlds.CampaignSystem;
2:using TaleWorlds.CampaignSystem.GameComponents;
3:using TaleWorlds.CampaignSystem.Party;
4:using TaleWorlds.Core;
5:using TAOM.Features.CareerSystem;
6:using TAOM.Features.CareerSystem.Domain;
7:
8:namespace TAOM.Features.CulturalFeats.Models;
9:
10:public class TaomPartySizeModel : DefaultPartySizeLimitModel
11:{
12:    private readonly ICulturalFeatsService _feats;
13:    private readonly ICareerPassiveService _careerPassives;
14:
15:    public TaomPartySizeModel(ICulturalFeatsService feats, ICareerPassiveService careerPassives)
16:    {
17:        _feats = feats;
18:        _careerPassives = careerPassives;
19:    }
20:
21:    public override ExplainedNumber GetPartyMemberSizeLimit(
22:        PartyBase party, bool includeDescriptions = false)
23:    {
24:        var result = base.GetPartyMemberSizeLimit(party, includeDescriptions);
25:        // Vanilla PartyBaseHelper.HasFeat precedence — see CultureFeatAdapter.FromOrNull(PartyBase).
26:        // Replaces the prior `party.Owner?.Culture ?? party.Culture` which skipped LeaderHero.Culture
27:        // (Codex review 43 caught the same systemic gap in TaomPartySpeedModel).
28:        _feats.ApplyPartySizeFeats(CultureFeatAdapter.FromOrNull(party), ref result);
29:        // PartySize passives are authored as flat counts ("+2 party size"), so apply via ApplyFlat
30:        // (result.Add). ApplyFactor would treat magnitude=2 as +200% (x3 the base) — the "+2 -> +150"
31:        // bug. Culture party-size feats above remain factor-based (ApplyPartySizeFeats uses AddFactor).
32:        _careerPassives.ApplyFlat((party.Owner ?? party.LeaderHero)?.StringId, ref result, PassiveEffectType.PartySize);
33:        return result;
34:    }
35:}

 succeeded in 533ms:
35:        |
36:9 GameModel overrides (check culture.HasFeat() and apply bonuses)
37:```
38:
39:### Party-Culture Resolution — the `ResolvePartyCulture` chokepoint
40:
41:GameModels that key feats off a **party's** culture (PartySize, PartyMorale, PartySpeed, FoodConsumption, PartyTroopUpgrade, BattleReward) resolve culture through one boundary helper — `CultureFeatAdapter.ResolvePartyCulture(PartyBase)` and its `FromOrNull(PartyBase)` wrapper — never inline. It mirrors vanilla `PartyBaseHelper.HasFeat` precedence, every step null-safe:
42:
43:```csharp
44:party.LeaderHero?.Culture
45:    ?? party.MapFaction?.Culture   // null-safe stand-in for party.Culture
46:    ?? party.Owner?.Culture
47:    ?? party.Settlement?.Culture;
48:```
49:
50:**Why `party.MapFaction?.Culture`, never `party.Culture`:** `PartyBase.Culture` is `MapFaction.Culture` with no null guard (PartyBase.cs:255), and `MapFaction` is null for a faction-less party (e.g. an empty lord party during `Army.OnSiegeStarted` strength calc). `party.Culture` throws the NRE *inside its own getter*, so an `if (party.Culture != null)` guard can't help. This shipped as a campaign-map crash (issue #281, RCA [`rca-culturefeat-partyculture-nre-2026-06-15.md`](../reviews/rca-culturefeat-partyculture-nre-2026-06-15.md)); the chokepoint above is the fix. `Hero.Culture` and `Settlement.Culture` are plain fields, safe once the owner/settlement is non-null — so `Owner?.Culture` / `Settlement?.Culture` need no inner guard.
51:
52:**Uniformity (2026-06-15):** all **9** party-culture feat models route through this chokepoint — PartySize, PartyMorale, PartySpeed, FoodConsumption, PartyTroopUpgrade, BattleReward, and (swept 2026-06-15) ArmyManagement, Raid, and PartyWage (line 49). The last three previously resolved inline as `Owner?.Culture` — null-safe (never the throwing `party.Culture`, so no crash) but precedence-inconsistent, skipping `LeaderHero`. Migrating them makes every party-culture feat resolve identically and matches vanilla `HasFeat`; it also closes the door on a future `?? party.Culture` fallback silently reintroducing the NRE. *Behavior shift:* a hero of culture A leading a party owned by a culture-B clan now gets A's army-influence / raid-damage / wage feats (the leader's culture), not B's. **Deliberately excluded** because they are *not* party-scoped: garrison wage (`TaomPartyWageModel.ResolveGarrisonInputs`, keyed on the fief owner's culture), the settlement-owner feat models (`TaomSettlementLoyaltyModel`, `TaomBuildingConstructionModel`, `TaomSettlementMilitiaModel`), and per-hero `StringId` career passives.
53:
54:## Configuration
55:
56:### Config: `Main/_Module/ModuleData/taom_spcultures.xml`
57:
58:Each culture has a `<cultural_feats>` block with 3 feat IDs:

 succeeded in 526ms:
1:using TaleWorlds.CampaignSystem;
2:using TaleWorlds.CampaignSystem.GameComponents;
3:using TaleWorlds.CampaignSystem.Party;
4:using TaleWorlds.Core;
5:
6:namespace TAOM.Features.CulturalFeats.Models;
7:
8:public class TaomFoodConsumptionModel : DefaultMobilePartyFoodConsumptionModel
9:{
10:    private readonly ICulturalFeatsService _feats;
11:
12:    public TaomFoodConsumptionModel(ICulturalFeatsService feats)
13:    {
14:        _feats = feats;
15:    }
16:
17:    public override ExplainedNumber CalculateDailyFoodConsumptionf(
18:        MobileParty party, ExplainedNumber baseConsumption)
19:    {
20:        var result = base.CalculateDailyFoodConsumptionf(party, baseConsumption);
21:        // Vanilla PartyBaseHelper.HasFeat precedence via the shared helper.
22:        _feats.ApplyFoodConsumptionFeats(CultureFeatAdapter.FromOrNull(party.Party), ref result);
23:        return result;
24:    }
25:}

 succeeded in 472ms:
1:using TaleWorlds.CampaignSystem;
2:using TaleWorlds.CampaignSystem.GameComponents;
3:using TaleWorlds.CampaignSystem.Party;
4:using TaleWorlds.Core;
5:using TAOM.Features.CareerSystem;
6:using TAOM.Features.CareerSystem.Domain;
7:
8:namespace TAOM.Features.CulturalFeats.Models;
9:
10:public class TaomPartyMoraleModel : DefaultPartyMoraleModel
11:{
12:    private readonly ICulturalFeatsService _feats;
13:    private readonly ICareerPassiveService _careerPassives;
14:
15:    public TaomPartyMoraleModel(ICulturalFeatsService feats, ICareerPassiveService careerPassives)
16:    {
17:        _feats = feats;
18:        _careerPassives = careerPassives;
19:    }
20:
21:    public override ExplainedNumber GetEffectivePartyMorale(
22:        MobileParty party, bool includeDescription = false)
23:    {
24:        var result = base.GetEffectivePartyMorale(party, includeDescription);
25:        // Vanilla PartyBaseHelper.HasFeat precedence via the shared helper.
26:        _feats.ApplyMoraleFeats(CultureFeatAdapter.FromOrNull(party.Party), ref result);
27:        _careerPassives.ApplyFactor(party.LeaderHero?.StringId, ref result, PassiveEffectType.TroopMorale);
28:        return result;
29:    }
30:}

codex
The existing sibling models line up with the docs: party-scoped models use the chokepoint, while settlement-owner and per-hero paths stay separate. I’m checking the remaining two party-scoped siblings and compile/test status signals next.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs' | ForEach-Object { "'$i++; '"'{0}:{1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/CulturalFeats/Models/TaomPartyTroopUpgradeModel.cs' | ForEach-Object { "'$i++; '"'{0}:{1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/BattleBalance/Models/TaomPartyHealingModel.cs' | ForEach-Object { "'$i++; '"'{0}:{1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git status --short' in C:\Users\mikew\source\repos\TAOM
 succeeded in 376ms:
 M .claude/rules/adapters.md
 M CHANGELOG.md
 M Main/Features/AdvancedCombat/CustomAttacksUtils.cs
 M Main/Features/AdvancedCombat/SpatialGrid.cs
 M Main/Features/CulturalFeats/CultureFeatAdapter.cs
 M Main/Features/CulturalFeats/Models/TaomArmyManagementModel.cs
 M Main/Features/CulturalFeats/Models/TaomBattleRewardModel.cs
 M Main/Features/CulturalFeats/Models/TaomRaidModel.cs
 M Main/Features/Spider/BehaviorTreeElements/IBTSpiderBlackboard.cs
 M Main/Features/Spider/BehaviorTreeElements/OnSpiderDied.cs
 M Main/Features/Spider/BehaviorTreeElements/SpiderAttackActions.cs
 D Main/Features/Spider/BehaviorTreeElements/SpiderAttackTask.cs
 D Main/Features/Spider/BehaviorTreeElements/SpiderCanBiteDecorator.cs
 M Main/Features/Spider/ISpiderAttackService.cs
 M Main/Features/Spider/SpiderAttackService.cs
 M Main/Features/Spider/SpiderBehaviorTree.cs
 M Main/Features/Spider/SpiderConfig.cs
 M Main/Features/Spider/SpiderMissionBehavior.cs
 M Main/Features/TroopProgression/Models/TaomPartyWageModel.cs
 M Main/_Module/GUI/PreFabs/CareerSystem/AbilityHUD.xml
 M TAOM.Tests/Features/Spider/SpiderAttackServiceTests.cs
 M docs/features/cultural-feats.md
?? Main/Features/Spider/BehaviorTreeElements/SpiderAttackOffCooldownDecorator.cs
?? Main/Features/Spider/BehaviorTreeElements/SpiderAttackTaskBase.cs
?? Main/Features/Spider/BehaviorTreeElements/SpiderEngageDecorator.cs
?? Main/Features/Spider/SpiderAttackKind.cs
?? TAOM.Tests/Features/AdvancedCombat/CustomAttacksUtilsTests.cs
?? docs/reviews/codex-adversarial-culturefeats-partyculture-2026-06-15.md
?? docs/reviews/codex-adversarial-culturefeats-partyculture-2026-06-15.prompt.md
?? docs/reviews/rca-culturefeat-partyculture-nre-2026-06-15.md
?? docs/reviews/rca-spider-directional-attacks-2026-06-15.md
warning: could not open directory '.codex_tmp/tmp1fft9w7e/': Permission denied
warning: could not open directory '.codex_tmp/tmp1fyfj2_j/': Permission denied
warning: could not open directory '.codex_tmp/tmp29jamgyn/': Permission denied
warning: could not open directory '.codex_tmp/tmp2owwhlqy/': Permission denied
warning: could not open directory '.codex_tmp/tmp5516wbdv/': Permission denied
warning: could not open directory '.codex_tmp/tmp78qze76y/': Permission denied
warning: could not open directory '.codex_tmp/tmp8gds9xfp/': Permission denied
warning: could not open directory '.codex_tmp/tmpbwbm4o8d/': Permission denied
warning: could not open directory '.codex_tmp/tmpc85yzrbw/': Permission denied
warning: could not open directory '.codex_tmp/tmpi3maldm8/': Permission denied
warning: could not open directory '.codex_tmp/tmpkr2gpims/': Permission denied
warning: could not open directory '.codex_tmp/tmpktgkfgay/': Permission denied
warning: could not open directory '.codex_tmp/tmplcnrd81l/': Permission denied
warning: could not open directory '.codex_tmp/tmplorpih3w/': Permission denied
warning: could not open directory '.codex_tmp/tmpm39f2s15/': Permission denied
warning: could not open directory '.codex_tmp/tmpov4yy7nx/': Permission denied
warning: could not open directory '.codex_tmp/tmpsanxk9fp/': Permission denied
warning: could not open directory '.codex_tmp/tmpwy0s04pv/': Permission denied
warning: could not open directory '.codex_tmp/tmpyqy6lg5o/': Permission denied
warning: could not open directory '.codex_tmp2/tmpulsj5ei5/': Permission denied

 succeeded in 519ms:
1:using TaleWorlds.CampaignSystem;
2:using TaleWorlds.CampaignSystem.GameComponents;
3:using TaleWorlds.CampaignSystem.Party;
4:using TaleWorlds.Core;
5:using TAOM.Features.CareerSystem;
6:using TAOM.Features.CareerSystem.Domain;
7:
8:namespace TAOM.Features.CulturalFeats.Models;
9:
10:public class TaomPartyTroopUpgradeModel : DefaultPartyTroopUpgradeModel
11:{
12:    private readonly ICulturalFeatsService _feats;
13:    private readonly ICareerPassiveService _careerPassives;
14:
15:    public TaomPartyTroopUpgradeModel(ICulturalFeatsService feats, ICareerPassiveService careerPassives)
16:    {
17:        _feats = feats;
18:        _careerPassives = careerPassives;
19:    }
20:
21:    public override ExplainedNumber GetGoldCostForUpgrade(
22:        PartyBase party, CharacterObject characterObject, CharacterObject upgradeTarget)
23:    {
24:        var result = base.GetGoldCostForUpgrade(party, characterObject, upgradeTarget);
25:        // Vanilla PartyBaseHelper.HasFeat precedence via the shared CultureFeatAdapter helper —
26:        // same fix Codex 43 made to speed model and the 3-pack RCA applied to size model.
27:        _feats.ApplyTroopUpgradeFeats(
28:            CultureFeatAdapter.FromOrNull(party),
29:            characterObject.IsMounted,
30:            ref result);
31:        _careerPassives.ApplyFactor((party.Owner ?? party.LeaderHero)?.StringId, ref result, PassiveEffectType.TroopUpgradeCost);
32:        return result;
33:    }
34:}

 succeeded in 522ms:
1:using TaleWorlds.CampaignSystem;
2:using TaleWorlds.CampaignSystem.GameComponents;
3:using TaleWorlds.CampaignSystem.Party;
4:using TaleWorlds.CampaignSystem.Roster;
5:using TaleWorlds.Core;
6:using TAOM.Features.CareerSystem;
7:using TAOM.Features.CareerSystem.Domain;
8:
9:namespace TAOM.Features.CulturalFeats.Models;
10:
11:public class TaomPartySpeedModel : DefaultPartySpeedCalculatingModel
12:{
13:    private readonly ICulturalFeatsService _feats;
14:    private readonly ICareerPassiveService _careerPassives;
15:
16:    public TaomPartySpeedModel(ICulturalFeatsService feats, ICareerPassiveService careerPassives)
17:    {
18:        _feats = feats;
19:        _careerPassives = careerPassives;
20:    }
21:
22:    public override ExplainedNumber CalculateFinalSpeed(MobileParty mobileParty, ExplainedNumber finalSpeed)
23:    {
24:        var result = base.CalculateFinalSpeed(mobileParty, finalSpeed);
25:
26:        // Boundary: convert sealed TaleWorlds types to primitives + adapter, then delegate.
27:        // Phase 9b #135 P1 — `Campaign.Current` and `MapSceneWrapper` can both be null during
28:        // scene transitions; `?.` short-circuit yields a null TerrainType which MapTerrain maps
29:        // to TerrainKind.None so no terrain feat is applied.
30:        var culture = CultureFeatAdapter.FromOrNull(mobileParty.Party);
31:        var terrain = MapTerrain(
32:            Campaign.Current?.MapSceneWrapper?.GetFaceTerrainType(mobileParty.CurrentNavigationFace));
33:        // Match vanilla: the night movement penalty (which the Mordor night feat offsets) is
34:        // applied only when not at sea, so the offsetting bonus must be land-only too.
35:        var isNight = (Campaign.Current?.IsNight ?? false) && !mobileParty.IsCurrentlyAtSea;
36:        var (mountedCount, totalCount) = CountMountedAndTotal(mobileParty.MemberRoster);
37:
38:        _feats.ApplyTerrainSpeedFeats(culture, terrain, isNight, ref result);
39:        _feats.ApplyRohanInfantryPenalty(culture, mountedCount, totalCount, ref result);
40:        _careerPassives.ApplyFactor(mobileParty.LeaderHero?.StringId, ref result, PassiveEffectType.PartyMovementSpeed);
41:
42:        return result;
43:    }
44:
45:    /// <summary>
46:    /// Boundary helper — maps the sealed TaleWorlds <see cref="TerrainType"/> (nullable
47:    /// when the map scene is unavailable) to the TAOM-owned <see cref="TerrainKind"/> so
48:    /// the service stays free of engine types (ADR-007). <see cref="TerrainType.Dune"/>
49:    /// folds into <see cref="TerrainKind.Desert"/> to match vanilla's desert handling.
50:    /// Any unmapped terrain (water, mountain, etc.) and a null input map to
51:    /// <see cref="TerrainKind.None"/>.
52:    /// </summary>
53:    private static TerrainKind MapTerrain(TerrainType? terrain) => terrain switch
54:    {
55:        TerrainType.Plain => TerrainKind.Plain,
56:        TerrainType.Forest => TerrainKind.Forest,
57:        TerrainType.Swamp => TerrainKind.Swamp,
58:        TerrainType.Steppe => TerrainKind.Steppe,
59:        TerrainType.Desert => TerrainKind.Desert,
60:        TerrainType.Dune => TerrainKind.Desert,
61:        TerrainType.Snow => TerrainKind.Snow,
62:        _ => TerrainKind.None,
63:    };
64:
65:    /// <summary>
66:    /// Boundary helper — collapses a sealed <see cref="TroopRoster"/> down to the
67:    /// two primitives <see cref="ICulturalFeatsService.ApplyRohanInfantryPenalty"/>
68:    /// needs, keeping the service free of TaleWorlds types per ADR-007.
69:    /// </summary>
70:    private static (int mounted, int total) CountMountedAndTotal(TroopRoster roster)
71:    {
72:        int total = roster.TotalManCount;
73:        int mounted = 0;
74:        foreach (var element in roster.GetTroopRoster())
75:        {
76:            if (element.Character?.IsMounted == true)
77:                mounted += element.Number;
78:        }
79:        return (mounted, total);
80:    }
81:}

 succeeded in 512ms:
1:using System;
2:using TaleWorlds.CampaignSystem;
3:using TaleWorlds.CampaignSystem.GameComponents;
4:using TaleWorlds.CampaignSystem.Party;
5:using TaleWorlds.Core;
6:using TAOM.Features.CareerSystem;
7:using TAOM.Features.CareerSystem.Domain;
8:
9:namespace TAOM.Features.BattleBalance.Models;
10:
11:public class TaomPartyHealingModel : DefaultPartyHealingModel
12:{
13:    private readonly IBattleBalanceSettingsProvider _settings;
14:    private readonly IBattleBalanceConfigProvider _configProvider;
15:
16:    public TaomPartyHealingModel(IBattleBalanceSettingsProvider settings,
17:        IBattleBalanceConfigProvider configProvider)
18:    {
19:        _settings = settings;
20:        _configProvider = configProvider;
21:    }
22:
23:    public override float GetSurvivalChance(PartyBase party, CharacterObject character,
24:        DamageTypes damageType, bool canDamageKillEvenIfBlunt, PartyBase enemyParty = null)
25:    {
26:        float vanillaSurvival = base.GetSurvivalChance(
27:            party, character, damageType, canDamageKillEvenIfBlunt, enemyParty);
28:
29:        if (party == null)
30:            return vanillaSurvival;
31:
32:        float result = vanillaSurvival;
33:
34:        if (_settings.EnableCulturalSurvivalBonuses)
35:        {
36:            var config = _configProvider.GetConfig();
37:            if (config.CasualtyRatios.EnableCulturalSurvivalBonuses)
38:            {
39:                // Vanilla PartyBaseHelper.HasFeat precedence — same fix family Codex 43 + 46
40:                // applied to feat-keyed models. Per-culture survival bonus is a culture-keyed
41:                // config lookup; should use the same leader→party→owner→settlement walk.
42:                var culture = TAOM.Features.CulturalFeats.CultureFeatAdapter.ResolvePartyCulture(party);
43:                if (culture != null)
44:                {
45:                    float bonus = config.CasualtyRatios.GetCulturalSurvivalBonus(culture.StringId);
46:                    if (bonus != 0f)
47:                        result = ApplyCulturalSurvivalBonus(result, bonus);
48:                }
49:            }
50:        }
51:
52:        // Career passive: TroopRegeneration increases survival chance
53:        var hero = party.Owner ?? party.LeaderHero;
54:        if (hero != null)
55:        {
56:            var passiveService = IoC.Resolve<ICareerPassiveService>();
57:            if (passiveService != null)
58:            {
59:                float magnitude = passiveService.GetPassiveMagnitude(hero.StringId, PassiveEffectType.TroopRegeneration);
60:                if (magnitude != 0f)
61:                    result = Math.Min(1f, result * (1f + magnitude));
62:            }
63:        }
64:
65:        return result;
66:    }
67:
68:    internal static float ApplyCulturalSurvivalBonus(float vanillaSurvival, float culturalBonus)
69:    {
70:        if (culturalBonus == 0f)
71:            return vanillaSurvival;
72:
73:        float deathChance = 1f - vanillaSurvival;
74:        float newDeathChance = deathChance * (1f - culturalBonus);
75:        float result = 1f - newDeathChance;
76:        return Math.Max(0f, Math.Min(1f, result));
77:    }
78:}

codex
The working tree has unrelated Spider/AdvancedCombat changes, so I’m ignoring those for this review. To satisfy the vanilla-evidence rule, I’m checking the decompiled Bannerlord source on `E:\Decompiled_Bannerlord` for the exact getter and `MapEventSide.LeaderParty` signatures before I write findings.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Test-Path 'E:\\Decompiled_Bannerlord'" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter 'PartyBase.cs' -ErrorAction SilentlyContinue | Select-Object -First 10 -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter 'MapEventSide.cs' -ErrorAction SilentlyContinue | Select-Object -First 10 -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter 'PartyBaseHelper.cs' -ErrorAction SilentlyContinue | Select-Object -First 10 -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 492ms:
True

 succeeded in 645ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyBaseHelper.cs

 succeeded in 710ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.MapEvents\MapEventSide.cs

 succeeded in 710ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs

codex
The decompiled files are present locally. I’m pulling only the relevant snippets now: `PartyBase` getters, `PartyBaseHelper.HasFeat`, and `MapEventSide.LeaderParty`.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\PartyBase.cs' | ForEach-Object { "'$i++; if (($i -ge 180 -and $i -le 260)) { '"'{0}:{1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\Helpers\\PartyBaseHelper.cs' | ForEach-Object { "'$i++; if (($i -ge 365 -and $i -le 405)) { '"'{0}:{1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.MapEvents\\MapEventSide.cs' | ForEach-Object { "'$i++; if (($i -ge 85 -and $i -le 100)) { '"'{0}:{1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\PartyBase.cs' -Pattern 'public Hero LeaderHero|public Hero Owner|public IFaction MapFaction|public CultureObject Culture' -Context 0,15 | ForEach-Object { "'$_.LineNumber.ToString() + '"':' + "'$_.Line.Trim(); $_.Context.PostContext | ForEach-Object { '"'  ' + "'$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 515ms:
180:
181:	public float HealingRateForMemberRegulars => Campaign.Current.Models.PartyHealingModel.GetDailyHealingForRegulars(this, isPrisoner: false).ResultNumber;
182:
183:	public ExplainedNumber HealingRateForMemberRegularsExplained => Campaign.Current.Models.PartyHealingModel.GetDailyHealingForRegulars(this, isPrisoner: false, includeDescriptions: true);
184:
185:	public float HealingRateForMemberHeroes => Campaign.Current.Models.PartyHealingModel.GetDailyHealingHpForHeroes(this, isPrisoners: false).ResultNumber;
186:
187:	public ExplainedNumber HealingRateForMemberHeroesExplained => Campaign.Current.Models.PartyHealingModel.GetDailyHealingHpForHeroes(this, isPrisoners: false, includeDescriptions: true);
188:
189:	public Hero Owner
190:	{
191:		get
192:		{
193:			Hero hero = _customOwner;
194:			if (hero == null)
195:			{
196:				if (!IsMobile)
197:				{
198:					return Settlement.Owner;
199:				}
200:				hero = MobileParty.Owner;
201:			}
202:			return hero;
203:		}
204:	}
205:
206:	public Hero LeaderHero => MobileParty?.LeaderHero;
207:
208:	public static PartyBase MainParty
209:	{
210:		get
211:		{
212:			if (Campaign.Current == null)
213:			{
214:				return null;
215:			}
216:			return Campaign.Current.MainParty.Party;
217:		}
218:	}
219:
220:	public bool LevelMaskIsDirty { get; private set; }
221:
222:	public int Index
223:	{
224:		get
225:		{
226:			return _index;
227:		}
228:		private set
229:		{
230:			_index = value;
231:		}
232:	}
233:
234:	public bool IsValid => Index >= 0;
235:
236:	public IFaction MapFaction
237:	{
238:		get
239:		{
240:			if (MobileParty != null)
241:			{
242:				return MobileParty.MapFaction;
243:			}
244:			if (Settlement != null)
245:			{
246:				return Settlement.MapFaction;
247:			}
248:			return null;
249:		}
250:	}
251:
252:	[SaveableProperty(210)]
253:	public int RandomValue { get; private set; } = MBRandom.RandomInt(1, int.MaxValue);
254:
255:	public CultureObject Culture => MapFaction.Culture;
256:
257:	public Tuple<uint, uint> PrimaryColorPair
258:	{
259:		get
260:		{

 succeeded in 523ms:
365:			{
366:				num = item.EquipmentElement.GetModifiedMountSpeed(in EquipmentElement.Invalid);
367:				speedLimitationItem = item.EquipmentElement.Item;
368:			}
369:		}
370:		return num;
371:	}
372:
373:	public static bool HasFeat(PartyBase party, FeatObject feat)
374:	{
375:		if (party == null)
376:		{
377:			return false;
378:		}
379:		if (party.LeaderHero != null)
380:		{
381:			return party.LeaderHero.Culture.HasFeat(feat);
382:		}
383:		if (party.Culture != null)
384:		{
385:			return party.Culture.HasFeat(feat);
386:		}
387:		if (party.Owner != null)
388:		{
389:			return party.Owner.Culture.HasFeat(feat);
390:		}
391:		if (party.Settlement != null)
392:		{
393:			return party.Settlement.Culture.HasFeat(feat);
394:		}
395:		return false;
396:	}
397:}

 succeeded in 507ms:
85:	private CharacterObject _selectedSimulationTroop;
86:
87:	[CachedData]
88:	public MBList<Ship> SimulationShipList { get; private set; } = new MBList<Ship>();
89:
90:	[CachedData]
91:	public float WeightedShipCombatFactor { get; private set; }
92:
93:	[SaveableProperty(4)]
94:	public PartyBase LeaderParty { get; internal set; }
95:
96:	public MBReadOnlyList<MapEventParty> Parties => _battleParties;
97:
98:	[SaveableProperty(7)]
99:	public BattleSideEnum MissionSide { get; private set; }
100:

 succeeded in 476ms:
189:public Hero Owner
  	{
  		get
  		{
  			Hero hero = _customOwner;
  			if (hero == null)
  			{
  				if (!IsMobile)
  				{
  					return Settlement.Owner;
  				}
  				hero = MobileParty.Owner;
  			}
  			return hero;
  		}
  	}
206:public Hero LeaderHero => MobileParty?.LeaderHero;
  
  	public static PartyBase MainParty
  	{
  		get
  		{
  			if (Campaign.Current == null)
  			{
  				return null;
  			}
  			return Campaign.Current.MainParty.Party;
  		}
  	}
  
  	public bool LevelMaskIsDirty { get; private set; }
  
236:public IFaction MapFaction
  	{
  		get
  		{
  			if (MobileParty != null)
  			{
  				return MobileParty.MapFaction;
  			}
  			if (Settlement != null)
  			{
  				return Settlement.MapFaction;
  			}
  			return null;
  		}
  	}
  
255:public CultureObject Culture => MapFaction.Culture;
  
  	public Tuple<uint, uint> PrimaryColorPair
  	{
  		get
  		{
  			if (MapFaction == null)
  			{
  				return new Tuple<uint, uint>(4291609515u, 4291609515u);
  			}
  			return new Tuple<uint, uint>(MapFaction.Color, MapFaction.Color2);
  		}
  	}
  
  	[SaveableProperty(216)]
  	public TextObject CustomName { get; private set; }

codex
One subtle point is `party.Owner?.Culture`: the null-conditional protects the returned hero, but it still invokes the `Owner` getter. I’m verifying the `IsMobile`/`MobileParty` shape to see whether that getter can throw in the faction-less mobile-party case the fix targets.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\PartyBase.cs' -Pattern 'IsMobile|MobileParty|Settlement' | ForEach-Object { '{0}:{1}' -f "'$_.LineNumber, $_.Line.Trim() } | Select-Object -First 80' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\PartyBase.cs' -Pattern 'new PartyBase|Initialize|MobileParty =' -Context 2,4 | ForEach-Object { "'$_.LineNumber.ToString() + '"':' + "'$_.Line.Trim(); $_.Context.PreContext | ForEach-Object { '"'  pre ' + "'$_ }; $_.Context.PostContext | ForEach-Object { '"'  post ' + "'$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\PartyBase.cs' | ForEach-Object { "'$i++; if ($i -ge 1 -and $i -le 120) { '"'{0}:{1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\PartyBase.cs' -Pattern 'public bool IsMobile|public MobileParty MobileParty|public Settlement Settlement' -Context 0,8 | ForEach-Object { "'$_.LineNumber.ToString() + '"':' + "'$_.Line.Trim(); $_.Context.PostContext | ForEach-Object { '"'  ' + "'$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 566ms:
11:using TaleWorlds.CampaignSystem.Settlements;
74:if (!IsMobile)
76:return Settlement.Position;
78:return MobileParty.Position;
86:if (!IsMobile)
88:return Settlement.IsVisible;
90:return MobileParty.IsVisible;
98:if (!IsMobile)
100:return Settlement.IsActive;
102:return MobileParty.IsActive;
110:if (!IsMobile)
112:return Settlement.SiegeEvent;
114:return MobileParty.SiegeEvent;
119:public Settlement Settlement { get; private set; }
122:public MobileParty MobileParty { get; private set; }
124:public bool IsSettlement => Settlement != null;
126:public bool IsMobile => MobileParty != null;
141:if (!IsSettlement)
143:if (!IsMobile)
147:return MobileParty.Name;
149:return Settlement.Name;
179:public string Id => MobileParty?.StringId ?? Settlement.StringId;
196:if (!IsMobile)
198:return Settlement.Owner;
200:hero = MobileParty.Owner;
206:public Hero LeaderHero => MobileParty?.LeaderHero;
240:if (MobileParty != null)
242:return MobileParty.MapFaction;
244:if (Settlement != null)
246:return Settlement.MapFaction;
279:if (!IsMobile)
281:return Settlement.Banner;
283:return MobileParty.Banner;
301:if (value != null && IsMobile && MapEvent != null && MapEvent.DefenderSide.LeaderParty == this)
314:if (MobileParty == null)
318:if (IsActive && MobileParty.IsTransitionInProgress)
320:MobileParty.CancelNavigationTransition();
322:foreach (MobileParty attachedParty in MobileParty.AttachedParties)
445:if (MobileParty?.Army != null)
447:return MobileParty.Army.LeaderParty?.LeaderHero?.CharacterObject;
467:collectedObjects.Add(Settlement);
468:collectedObjects.Add(MobileParty);
476:internal static object AutoGeneratedGetMemberValueSettlement(object o)
478:return ((PartyBase)o).Settlement;
481:internal static object AutoGeneratedGetMemberValueMobileParty(object o)
483:return ((PartyBase)o).MobileParty;
586:if (IsSettlement && !TextObject.IsNullOrEmpty(CustomName))
588:CustomName.SetSettlementProperties(Settlement);
593:CampaignVec2 IInteractablePoint.GetInteractionPosition(MobileParty interactingParty)
595:if (IsMobile)
597:return MobileParty.Position;
599:if (IsSettlement)
603:return Settlement.GatePosition;
605:return Settlement.PortPosition;
610:bool IInteractablePoint.CanPartyInteract(MobileParty mobileParty, float dt)
613:if (IsMobile && (mobileParty.IsMainParty || !MobileParty.ShouldBeIgnored))
615:flag = mobileParty.IsCurrentlyAtSea == MobileParty.IsCurrentlyAtSea;
617:else if (IsSettlement)
619:flag = mobileParty.IsTargetingPort == mobileParty.IsCurrentlyAtSea && !mobileParty.IsTransitionInProgress;
623:GetEncounterTargetPoint(dt, mobileParty, out var targetPoint, out var neededMaximumDistanceForEncountering);
624:float length = (mobileParty.Position.ToVec2() - targetPoint.ToVec2()).Length;
625:flag = (mobileParty.BesiegedSettlement != null && mobileParty.BesiegedSettlement == mobileParty.TargetSettlement) || length < neededMaximumDistanceForEncountering;
630:void IInteractablePoint.OnPartyInteraction(MobileParty engagingParty)
632:if (IsMobile)
634:MobileParty.OnPartyInteraction(engagingParty);
636:else if (IsSettlement)
638:Settlement.OnPartyInteraction(engagingParty);
642:private static void GetEncounterTargetPoint(float dt, MobileParty mobileParty, out CampaignVec2 targetPoint, out float neededMaximumDistanceForEncountering)
645:float num = (mobileParty.IsCurrentlyAtSea ? encounterModel.NeededMaximumNavalDistanceForEncounteringMobileParty : encounterModel.NeededMaximumLandDistanceForEncounteringMobileParty);
646:if (mobileParty.Army != null)
648:float a = (mobileParty.IsCurrentlyAtSea ? encounterModel.MaximumAllowedNavalDistanceForEncounteringMobilePartyInArmy : encounterModel.MaximumAllowedLandDistanceForEncounteringMobilePartyInArmy);
649:neededMaximumDistanceForEncountering = TaleWorlds.Library.MathF.Clamp(num * TaleWorlds.Library.MathF.Sqrt(mobileParty.Army.LeaderParty.AttachedParties.Count + 1), TaleWorlds.Library.MathF.Max(num, dt * Campaign.Current.EstimatedMaximumLordPartySpeedExceptPlayer), TaleWorlds.Library.MathF.Max(a, dt * (Campaign.Current.EstimatedMaximumLordPartySpeedExceptPlayer + 0.01f)));
655:if (mobileParty.IsCurrentlyEngagingSettlement)
657:Settlement shortTermTargetSettlement = mobileParty.ShortTermTargetSettlement;
658:targetPoint = (mobileParty.IsTargetingPort ? shortTermTargetSettlement.PortPosition : shortTermTargetSettlement.GatePosition);
659:neededMaximumDistanceForEncountering = (shortTermTargetSettlement.IsTown ? encounterModel.NeededMaximumDistanceForEncounteringTown : encounterModel.NeededMaximumDistanceForEncounteringVillage);
660:if (mobileParty.IsTargetingPort)
662:SiegeEvent siegeEvent = shortTermTargetSettlement.SiegeEvent;
669:else if (mobileParty.Army != null && mobileParty.Army.LeaderParty != mobileParty && mobileParty.ShortTermTargetParty.MapEvent != null && mobileParty.ShortTermTargetParty.MapEvent == mobileParty.Army.LeaderParty.MapEvent && mobileParty.Army.LeaderParty.AttachedParties.Contains(mobileParty))
671:targetPoint = mobileParty.Position;

 succeeded in 558ms:
314:if (MobileParty == null)
  pre 				_mapEventSide.AddPartyInternal(this);
  pre 			}
  post 			{
  post 				return;
  post 			}
  post 			if (IsActive && MobileParty.IsTransitionInProgress)
912:MobileParty = mobileParty;
  pre 	{
  pre 		Index = Campaign.Current.GeneratePartyId(this);
  post 		Settlement = settlement;
  post 		ItemRoster = new ItemRoster();
  post 		MemberRoster = new TroopRoster(this);
  post 		PrisonRoster = new TroopRoster(this);
1062:MobileParty mobileParty = mapPoint as MobileParty;
  pre 		isInspected = false;
  pre 		isVisible = false;
  post 		if (mobileParty?.Army != null && mobileParty.Army.LeaderParty.AttachedParties.IndexOf(mobileParty) >= 0)
  post 		{
  post 			isVisible = mobileParty.Army.LeaderParty.IsVisible;
  post 			return;

 succeeded in 561ms:
1:using System;
2:using System.Collections.Generic;
3:using TaleWorlds.CampaignSystem.Actions;
4:using TaleWorlds.CampaignSystem.CharacterDevelopment;
5:using TaleWorlds.CampaignSystem.ComponentInterfaces;
6:using TaleWorlds.CampaignSystem.Extensions;
7:using TaleWorlds.CampaignSystem.Map;
8:using TaleWorlds.CampaignSystem.MapEvents;
9:using TaleWorlds.CampaignSystem.Naval;
10:using TaleWorlds.CampaignSystem.Roster;
11:using TaleWorlds.CampaignSystem.Settlements;
12:using TaleWorlds.CampaignSystem.Siege;
13:using TaleWorlds.Core;
14:using TaleWorlds.Library;
15:using TaleWorlds.Localization;
16:using TaleWorlds.SaveSystem;
17:using TaleWorlds.SaveSystem.Load;
18:
19:namespace TaleWorlds.CampaignSystem.Party;
20:
21:public sealed class PartyBase : IBattleCombatant, IRandomOwner, IInteractablePoint
22:{
23:	[SaveableField(15)]
24:	private int _remainingFoodPercentage;
25:
26:	[SaveableField(182)]
27:	private CampaignTime _lastEatingTime = CampaignTime.Now;
28:
29:	[SaveableField(8)]
30:	private Hero _customOwner;
31:
32:	[SaveableField(9)]
33:	private int _index;
34:
35:	[SaveableField(200)]
36:	private MapEventSide _mapEventSide;
37:
38:	[CachedData]
39:	private int _partyMemberSizeLastCheckVersion;
40:
41:	[CachedData]
42:	private int _cachedPartyMemberSizeLimit;
43:
44:	[CachedData]
45:	private int _prisonerSizeLastCheckVersion;
46:
47:	[CachedData]
48:	private int _cachedPrisonerSizeLimit;
49:
50:	[CachedData]
51:	private int _lastNumberOfMenWithHorseVersionNo;
52:
53:	[CachedData]
54:	private int _lastNumberOfMenPerTierVersionNo;
55:
56:	[SaveableField(17)]
57:	private int _numberOfMenWithHorse;
58:
59:	private int[] _numberOfHealthyMenPerTier;
60:
61:	[CachedData]
62:	private int _lastEstimatedStrengthVersionNo = -1;
63:
64:	[CachedData]
65:	private float _cachedEstimatedStrength;
66:
67:	[SaveableField(20)]
68:	private MBList<Ship> _ships = new MBList<Ship>();
69:
70:	public CampaignVec2 Position
71:	{
72:		get
73:		{
74:			if (!IsMobile)
75:			{
76:				return Settlement.Position;
77:			}
78:			return MobileParty.Position;
79:		}
80:	}
81:
82:	public bool IsVisible
83:	{
84:		get
85:		{
86:			if (!IsMobile)
87:			{
88:				return Settlement.IsVisible;
89:			}
90:			return MobileParty.IsVisible;
91:		}
92:	}
93:
94:	public bool IsActive
95:	{
96:		get
97:		{
98:			if (!IsMobile)
99:			{
100:				return Settlement.IsActive;
101:			}
102:			return MobileParty.IsActive;
103:		}
104:	}
105:
106:	public SiegeEvent SiegeEvent
107:	{
108:		get
109:		{
110:			if (!IsMobile)
111:			{
112:				return Settlement.SiegeEvent;
113:			}
114:			return MobileParty.SiegeEvent;
115:		}
116:	}
117:
118:	[SaveableProperty(1)]
119:	public Settlement Settlement { get; private set; }
120:

 succeeded in 501ms:
119:public Settlement Settlement { get; private set; }
  
  	[SaveableProperty(2)]
  	public MobileParty MobileParty { get; private set; }
  
  	public bool IsSettlement => Settlement != null;
  
  	public bool IsMobile => MobileParty != null;
  
122:public MobileParty MobileParty { get; private set; }
  
  	public bool IsSettlement => Settlement != null;
  
  	public bool IsMobile => MobileParty != null;
  
  	[SaveableProperty(3)]
  	public TroopRoster MemberRoster { get; private set; }
  
126:public bool IsMobile => MobileParty != null;
  
  	[SaveableProperty(3)]
  	public TroopRoster MemberRoster { get; private set; }
  
  	[SaveableProperty(4)]
  	public TroopRoster PrisonRoster { get; private set; }
  
  	[SaveableProperty(5)]

codex
`Owner` is safe for the reported faction-less mobile party because `IsMobile` is literally `MobileParty != null`, then the getter reads `MobileParty.Owner`. I’m checking construction/initialization now because the prompt asks “ANY PartyBase state,” and the one pathological case would be a `PartyBase` with neither `MobileParty` nor `Settlement`.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\PartyBase.cs' | ForEach-Object { "'$i++; if ($i -ge 880 -and $i -le 930) { '"'{0}:{1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\**\\*.cs' -Pattern 'new PartyBase\\(' -ErrorAction SilentlyContinue | Select-Object -First 50 | ForEach-Object { '{0}:{1}:{2}' -f "'$_.Path, $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\PartyBase.cs' -Pattern 'PartyBase\\(' -Context 2,12 | ForEach-Object { "'$_.LineNumber.ToString() + '"':' + "'$_.Line.Trim(); $_.Context.PreContext | ForEach-Object { '"'  pre ' + "'$_ }; $_.Context.PostContext | ForEach-Object { '"'  post ' + "'$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\**\\*.cs' -Pattern 'PartyBase\\.Initialize' -ErrorAction SilentlyContinue | Select-Object -First 50 | ForEach-Object { '{0}:{1}:{2}' -f "'$_.Path, $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 520ms:
880:	{
881:		float result = 0f;
882:		if (IsMobile)
883:		{
884:			BattleSideEnum side = BattleSideEnum.Defender;
885:			if (MapEvent != null)
886:			{
887:				side = Side;
888:			}
889:			result = Campaign.Current.Models.MilitaryPowerModel.GetPowerOfParty(this, side, MapEvent.PowerCalculationContext.Estimated);
890:		}
891:		return result;
892:	}
893:
894:	public float GetCustomStrength(BattleSideEnum side, MapEvent.PowerCalculationContext context)
895:	{
896:		return Campaign.Current.Models.MilitaryPowerModel.GetPowerOfParty(this, side, context);
897:	}
898:
899:	public PartyBase(MobileParty mobileParty)
900:		: this(mobileParty, null)
901:	{
902:	}
903:
904:	public PartyBase(Settlement settlement)
905:		: this(null, settlement)
906:	{
907:	}
908:
909:	private PartyBase(MobileParty mobileParty, Settlement settlement)
910:	{
911:		Index = Campaign.Current.GeneratePartyId(this);
912:		MobileParty = mobileParty;
913:		Settlement = settlement;
914:		ItemRoster = new ItemRoster();
915:		MemberRoster = new TroopRoster(this);
916:		PrisonRoster = new TroopRoster(this);
917:	}
918:
919:	internal void AddShipInternal(Ship ship)
920:	{
921:		_ships.Add(ship);
922:	}
923:
924:	internal void RemoveShipInternal(Ship ship)
925:	{
926:		_ships.Remove(ship);
927:	}
928:
929:	public int GetShipsVersion()
930:	{

 succeeded in 514ms:
456:internal static void AutoGeneratedStaticCollectObjectsPartyBase(object o, List<object> collectedObjects)
  pre 	public bool IsVisualDirty { get; private set; }
  pre 
  post 	{
  post 		((PartyBase)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
  post 	}
  post 
  post 	private void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
  post 	{
  post 		CampaignTime.AutoGeneratedStaticCollectObjectsCampaignTime(_lastEatingTime, collectedObjects);
  post 		collectedObjects.Add(_customOwner);
  post 		collectedObjects.Add(_mapEventSide);
  post 		collectedObjects.Add(_ships);
  post 		collectedObjects.Add(Settlement);
  post 		collectedObjects.Add(MobileParty);
899:public PartyBase(MobileParty mobileParty)
  pre 	}
  pre 
  post 		: this(mobileParty, null)
  post 	{
  post 	}
  post 
  post 	public PartyBase(Settlement settlement)
  post 		: this(null, settlement)
  post 	{
  post 	}
  post 
  post 	private PartyBase(MobileParty mobileParty, Settlement settlement)
  post 	{
  post 		Index = Campaign.Current.GeneratePartyId(this);
904:public PartyBase(Settlement settlement)
  pre 	}
  pre 
  post 		: this(null, settlement)
  post 	{
  post 	}
  post 
  post 	private PartyBase(MobileParty mobileParty, Settlement settlement)
  post 	{
  post 		Index = Campaign.Current.GeneratePartyId(this);
  post 		MobileParty = mobileParty;
  post 		Settlement = settlement;
  post 		ItemRoster = new ItemRoster();
  post 		MemberRoster = new TroopRoster(this);
  post 		PrisonRoster = new TroopRoster(this);
909:private PartyBase(MobileParty mobileParty, Settlement settlement)
  pre 	}
  pre 
  post 	{
  post 		Index = Campaign.Current.GeneratePartyId(this);
  post 		MobileParty = mobileParty;
  post 		Settlement = settlement;
  post 		ItemRoster = new ItemRoster();
  post 		MemberRoster = new TroopRoster(this);
  post 		PrisonRoster = new TroopRoster(this);
  post 	}
  post 
  post 	internal void AddShipInternal(Ship ship)
  post 	{
  post 		_ships.Add(ship);

 succeeded in 12280ms:
 succeeded in 12283ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1856:Party = new PartyBase(this);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:767:Party = new PartyBase(this);

codex
The only residual null-safety wrinkle is inside `MobileParty.MapFaction`/`Settlement.MapFaction`, since `party.MapFaction?.Culture` still invokes that getter first. I’m checking those getter bodies so the proof does not stop one level too early.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\MobileParty.cs' -Pattern 'public IFaction MapFaction|public Clan Owner|public Hero Owner|LeaderHero' -Context 0,20 | ForEach-Object { "'$_.LineNumber.ToString() + '"':' + "'$_.Line.Trim(); $_.Context.PostContext | ForEach-Object { '"'  ' + "'$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\MobileParty.cs' -Pattern 'MapFaction' | Select-Object -First 30 | ForEach-Object { '{0}:{1}' -f "'$_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Settlements\\Settlement.cs' -Pattern 'public IFaction MapFaction|public Hero Owner|public CultureObject Culture|OwnerClan' -Context 0,20 | Select-Object -First 8 | ForEach-Object { "'$_.LineNumber.ToString() + '"':' + "'$_.Line.Trim(); $_.Context.PostContext | ForEach-Object { '"'  ' + "'$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Settlements\\Settlement.cs' -Pattern 'MapFaction' | Select-Object -First 30 | ForEach-Object { '{0}:{1}' -f "'$_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 1145ms:
330:if (MapFaction != null)
332:return MapFaction.Banner;
1079:public IFaction MapFaction
1085:return ActualClan.MapFaction;
1091:return Party.Owner.MapFaction;
1095:return Party.Owner.HomeSettlement.MapFaction;
1099:return HomeSettlement.OwnerClan.MapFaction;
1103:return Party.Owner.MapFaction;
1107:return Party.Owner.MapFaction;
1109:return (LeaderHero != null) ? LeaderHero.MapFaction : null;
1113:return HomeSettlement.OwnerClan.MapFaction;
1115:return (LeaderHero != null) ? LeaderHero.MapFaction : null;
2068:flag = mobileParty.MapEvent.MapEventSettlement == mobileParty.CurrentSettlement && (mobileParty.MapEvent.AttackerSide.LeaderParty.MapFaction == engagingParty.MapFaction || mobileParty.MapEvent.DefenderSide.LeaderParty.MapFaction == engagingParty.MapFaction);
2271:if (MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.3.0") && IsActive && MapFaction == null)
2383:IFaction mapFaction = TargetParty.MapFaction;
2384:if (mapFaction == null || !mapFaction.IsAtWarWith(MapFaction))
2391:IFaction mapFaction2 = TargetSettlement.MapFaction;
2392:if (mapFaction2 == null || !mapFaction2.IsAtWarWith(MapFaction))
2400:if (shortTermTargetParty != null && shortTermTargetParty.MapFaction?.IsAtWarWith(MapFaction) == true)
2499:else if (ShortTermTargetParty != null && ShortTermTargetParty.MapFaction == MapFaction)
2531:textObject = ((!(Campaign.Current.Models.MapDistanceModel.GetDistance(this, TargetSettlement, IsTargetingPort, NavigationCapability, out estimatedLandRatio) > Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(NavigationType.All) * 0.5f)) ? ((!flag) ? new TextObject("{=yUVv3z5V}Patrolling around {TARGET_SETTLEMENT}.") : (TargetSettlement.MapFaction.IsAtWarWith(MapFaction) ? new TextObject("{=VQY0e6hF}Threatening the coastal waters off {TARGET_SETTLEMENT}.") : new TextObject("{=8qvUbTvW}Guarding the coastal waters off {TARGET_SETTLEMENT}."))) : ((!flag) ? new TextObject("{=MNoogAgk}Heading to patrol around {TARGET_SETTLEMENT}.") : (TargetSettlement.MapFaction.IsAtWarWith(MapFaction) ? new TextObject("{=YIKb1kob}Heading to the coastal waters off {TARGET_SETTLEMENT}.") : new TextObject("{=avhlH79s}Heading to patrol the coastal waters off {TARGET_SETTLEMENT}."))));
2550:textObject = ((ShortTermTargetParty != null && ShortTermTargetParty.MapFaction.IsAtWarWith(MapFaction)) ? new TextObject("{=NRpbagbZ}Running to {TARGET_PARTY}.") : new TextObject("{=EQHq3bHM}Travelling to {TARGET_PARTY}"));
3603:if (mobileParty.MapFaction.IsAtWarWith(MapFaction))

 succeeded in 1241ms:
292:public IFaction MapFaction => SettlementComponent?.MapFaction;
503:return Hideout.MapFaction as Clan;
892:if (engagingParty.ShortTermTargetSettlement == null || ((!engagingParty.IsCurrentlyAtSea || !engagingParty.IsTargetingPort) && (!engagingParty.ShortTermTargetSettlement.IsVillage || engagingParty.DefaultBehavior != AiBehavior.DefendSettlement) && ((engagingParty.ShortTermTargetSettlement.Party.SiegeEvent != null && engagingParty != MobileParty.MainParty && engagingParty.MapFaction != engagingParty.ShortTermTargetSettlement.SiegeEvent.BesiegerCamp.MapFaction) || (engagingParty.ShortTermTargetSettlement.Party.MapEvent != null && engagingParty != MobileParty.MainParty && engagingParty.MapFaction != engagingParty.ShortTermTargetSettlement.Party.MapEvent.AttackerSide.LeaderParty.MapFaction && (!engagingParty.ShortTermTargetSettlement.Party.MapEvent.IsSallyOut || engagingParty.MapFaction != engagingParty.ShortTermTargetSettlement.Party.MapEvent.DefenderSide.LeaderParty.MapFaction)))))

 succeeded in 1371ms:
70:public CultureObject Culture;
  
  	private TextObject _name;
  
  	[SaveableField(129)]
  	private MBList<Village> _boundVillages;
  
  	[SaveableField(131)]
  	private MobileParty _lastAttackerParty;
  
  	[SaveableField(148)]
  	private MBList<SiegeEvent.SiegeEngineMissile> _siegeEngineMissiles;
  
  	public Town Town;
  
  	public Village Village;
  
  	public Hideout Hideout;
  
  	[CachedData]
  	public MilitiaPartyComponent MilitiaPartyComponent;
107:public Hero Owner => OwnerClan.Leader;
  
  	public Banner Banner
  	{
  		get
  		{
  			if (Party.CustomBanner != null)
  			{
  				return Party.CustomBanner;
  			}
  			if (SettlementComponent != null && SettlementComponent.GetDefaultComponentBanner() != null)
  			{
  				return SettlementComponent.GetDefaultComponentBanner();
  			}
  			if (OwnerClan != null)
  			{
  				return OwnerClan.Banner;
  			}
  			return null;
  		}
  	}
121:if (OwnerClan != null)
  			{
  				return OwnerClan.Banner;
  			}
  			return null;
  		}
  	}
  
  	public bool IsVisible
  	{
  		get
  		{
  			return _isVisible;
  		}
  		set
  		{
  			if (_isVisible != value)
  			{
  				_isVisible = value;
  				Party.OnVisibilityChanged(value);
  			}
123:return OwnerClan.Banner;
  			}
  			return null;
  		}
  	}
  
  	public bool IsVisible
  	{
  		get
  		{
  			return _isVisible;
  		}
  		set
  		{
  			if (_isVisible != value)
  			{
  				_isVisible = value;
  				Party.OnVisibilityChanged(value);
  			}
  		}
  	}
292:public IFaction MapFaction => SettlementComponent?.MapFaction;
  
  	public TextObject Name
  	{
  		get
  		{
  			if (!TextObject.IsNullOrEmpty(Party.CustomName))
  			{
  				return Party.CustomName;
  			}
  			return _name;
  		}
  	}
  
  	public TextObject EncyclopediaText { get; private set; }
  
  	public string EncyclopediaLink => (Campaign.Current.EncyclopediaManager.GetIdentifier(typeof(Settlement)) + "-" + base.StringId) ?? "";
  
  	public TextObject EncyclopediaLinkWithName => HyperlinkTexts.GetSettlementHyperlinkText(EncyclopediaLink, Name);
  
  	[SaveableProperty(122)]
489:public Clan OwnerClan
  	{
  		get
  		{
  			if (Village != null)
  			{
  				return Village.Bound.OwnerClan;
  			}
  			if (Town != null)
  			{
  				return Town.OwnerClan;
  			}
  			if (IsHideout)
  			{
  				return Hideout.MapFaction as Clan;
  			}
  			return null;
  		}
  	}
  
  	internal static void AutoGeneratedStaticCollectObjectsSettlement(object o, List<object> collectedObjects)
495:return Village.Bound.OwnerClan;
  			}
  			if (Town != null)
  			{
  				return Town.OwnerClan;
  			}
  			if (IsHideout)
  			{
  				return Hideout.MapFaction as Clan;
  			}
  			return null;
  		}
  	}
  
  	internal static void AutoGeneratedStaticCollectObjectsSettlement(object o, List<object> collectedObjects)
  	{
  		((Settlement)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
  	}
  
  	protected override void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
  	{
499:return Town.OwnerClan;
  			}
  			if (IsHideout)
  			{
  				return Hideout.MapFaction as Clan;
  			}
  			return null;
  		}
  	}
  
  	internal static void AutoGeneratedStaticCollectObjectsSettlement(object o, List<object> collectedObjects)
  	{
  		((Settlement)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
  	}
  
  	protected override void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
  	{
  		base.AutoGeneratedInstanceCollectObjects(collectedObjects);
  		collectedObjects.Add(Stash);
  		collectedObjects.Add(_nextLocatable);
  		collectedObjects.Add(_settlementWallSectionHitPointsRatioList);

 succeeded in 1627ms:
428:if (IsLordParty && LeaderHero != null)
  			{
  				return LeaderHero.Gold;
  			}
  			return _partyTradeGold;
  		}
  		set
  		{
  			if (IsLordParty && LeaderHero != null)
  			{
  				LeaderHero.Gold = TaleWorlds.Library.MathF.Max(value, 0);
  			}
  			else
  			{
  				_partyTradeGold = TaleWorlds.Library.MathF.Max(value, 0);
  			}
  		}
  	}
  
  	[SaveableProperty(1957)]
  	public int PartyTradeTaxGold { get; private set; }
430:return LeaderHero.Gold;
  			}
  			return _partyTradeGold;
  		}
  		set
  		{
  			if (IsLordParty && LeaderHero != null)
  			{
  				LeaderHero.Gold = TaleWorlds.Library.MathF.Max(value, 0);
  			}
  			else
  			{
  				_partyTradeGold = TaleWorlds.Library.MathF.Max(value, 0);
  			}
  		}
  	}
  
  	[SaveableProperty(1957)]
  	public int PartyTradeTaxGold { get; private set; }
  
  	[SaveableProperty(1960)]
436:if (IsLordParty && LeaderHero != null)
  			{
  				LeaderHero.Gold = TaleWorlds.Library.MathF.Max(value, 0);
  			}
  			else
  			{
  				_partyTradeGold = TaleWorlds.Library.MathF.Max(value, 0);
  			}
  		}
  	}
  
  	[SaveableProperty(1957)]
  	public int PartyTradeTaxGold { get; private set; }
  
  	[SaveableProperty(1960)]
  	public CampaignTime StationaryStartTime { get; private set; }
  
  	[CachedData]
  	public int VersionNo { get; private set; }
  
  	[SaveableProperty(1080)]
438:LeaderHero.Gold = TaleWorlds.Library.MathF.Max(value, 0);
  			}
  			else
  			{
  				_partyTradeGold = TaleWorlds.Library.MathF.Max(value, 0);
  			}
  		}
  	}
  
  	[SaveableProperty(1957)]
  	public int PartyTradeTaxGold { get; private set; }
  
  	[SaveableProperty(1960)]
  	public CampaignTime StationaryStartTime { get; private set; }
  
  	[CachedData]
  	public int VersionNo { get; private set; }
  
  	[SaveableProperty(1080)]
  	public bool ShouldJoinPlayerBattles { get; set; }
  
775:public Hero LeaderHero => PartyComponent?.Leader;
  
  	[SaveableProperty(1070)]
  	private Hero Scout { get; set; }
  
  	[SaveableProperty(1072)]
  	private Hero Engineer { get; set; }
  
  	[SaveableProperty(1071)]
  	private Hero Quartermaster { get; set; }
  
  	[SaveableProperty(1073)]
  	private Hero Surgeon { get; set; }
  
  	[SaveableProperty(1076)]
  	private Hero FirstMate { get; set; }
  
  	[SaveableProperty(1077)]
  	private Hero Navigator { get; set; }
  
  	public Hero Owner => _partyComponent?.PartyOwner;
795:public Hero Owner => _partyComponent?.PartyOwner;
  
  	public Hero EffectiveScout
  	{
  		get
  		{
  			if (Scout == null || Scout.PartyBelongedTo != this)
  			{
  				return LeaderHero;
  			}
  			return Scout;
  		}
  	}
  
  	public Hero EffectiveQuartermaster
  	{
  		get
  		{
  			if (Quartermaster == null || Quartermaster.PartyBelongedTo != this)
  			{
  				return LeaderHero;
803:return LeaderHero;
  			}
  			return Scout;
  		}
  	}
  
  	public Hero EffectiveQuartermaster
  	{
  		get
  		{
  			if (Quartermaster == null || Quartermaster.PartyBelongedTo != this)
  			{
  				return LeaderHero;
  			}
  			return Quartermaster;
  		}
  	}
  
  	public Hero EffectiveEngineer
  	{
  		get
815:return LeaderHero;
  			}
  			return Quartermaster;
  		}
  	}
  
  	public Hero EffectiveEngineer
  	{
  		get
  		{
  			if (Engineer == null || Engineer.PartyBelongedTo != this)
  			{
  				return LeaderHero;
  			}
  			return Engineer;
  		}
  	}
  
  	public Hero EffectiveSurgeon
  	{
  		get
827:return LeaderHero;
  			}
  			return Engineer;
  		}
  	}
  
  	public Hero EffectiveSurgeon
  	{
  		get
  		{
  			if (Surgeon == null || Surgeon.PartyBelongedTo != this)
  			{
  				return LeaderHero;
  			}
  			return Surgeon;
  		}
  	}
  
  	public Hero EffectiveFirstMate
  	{
  		get
839:return LeaderHero;
  			}
  			return Surgeon;
  		}
  	}
  
  	public Hero EffectiveFirstMate
  	{
  		get
  		{
  			if (FirstMate == null || FirstMate.PartyBelongedTo != this)
  			{
  				return LeaderHero;
  			}
  			return FirstMate;
  		}
  	}
  
  	public Hero EffectiveNavigator
  	{
  		get
851:return LeaderHero;
  			}
  			return FirstMate;
  		}
  	}
  
  	public Hero EffectiveNavigator
  	{
  		get
  		{
  			if (Navigator == null || Navigator.PartyBelongedTo != this)
  			{
  				return LeaderHero;
  			}
  			return Navigator;
  		}
  	}
  
  	public float RecentEventsMorale
  	{
  		get
863:return LeaderHero;
  			}
  			return Navigator;
  		}
  	}
  
  	public float RecentEventsMorale
  	{
  		get
  		{
  			return _moraleDueToEvents;
  		}
  		set
  		{
  			_moraleDueToEvents = value;
  			if (_moraleDueToEvents < -100f)
  			{
  				_moraleDueToEvents = -100f;
  			}
  			else if (_moraleDueToEvents > 100f)
  			{
1079:public IFaction MapFaction
  	{
  		get
  		{
  			if (ActualClan != null)
  			{
  				return ActualClan.MapFaction;
  			}
  			if (Party.Owner != null)
  			{
  				if (Party.Owner == Hero.MainHero)
  				{
  					return Party.Owner.MapFaction;
  				}
  				if (Party.Owner.IsNotable)
  				{
  					return Party.Owner.HomeSettlement.MapFaction;
  				}
  				if ((IsMilitia || IsGarrison || IsVillager || IsPatrolParty) && HomeSettlement?.OwnerClan != null)
  				{
  					return HomeSettlement.OwnerClan.MapFaction;
1109:return (LeaderHero != null) ? LeaderHero.MapFaction : null;
  			}
  			if (HomeSettlement != null)
  			{
  				return HomeSettlement.OwnerClan.MapFaction;
  			}
  			return (LeaderHero != null) ? LeaderHero.MapFaction : null;
  		}
  	}
  
  	public TextObject ArmyName
  	{
  		get
  		{
  			if (Army == null || Army.LeaderParty != this)
  			{
  				return Name;
  			}
  			return Army.Name;
  		}
  	}
1115:return (LeaderHero != null) ? LeaderHero.MapFaction : null;
  		}
  	}
  
  	public TextObject ArmyName
  	{
  		get
  		{
  			if (Army == null || Army.LeaderParty != this)
  			{
  				return Name;
  			}
  			return Army.Name;
  		}
  	}
  
  	public SiegeEvent SiegeEvent => BesiegerCamp?.SiegeEvent;
  
  	public float Food => (float)Party.RemainingFoodPercentage * 0.01f + (float)TotalFoodAtInventory;
  
  	public int TotalFoodAtInventory => ItemRoster.TotalFood;
2258:if ((LeaderHero != null && this != MainParty && LeaderHero.PartyBelongedTo != this) || (MapEvent == null && base.StringId.Contains("troops_of_")))
  			{
  				DestroyPartyAction.Apply(null, this);
  			}
  			if (MapEvent == null && (base.StringId.Contains("troops_of_CharacterObject") || base.StringId.Contains("troops_of_TaleWorlds.CampaignSystem.CharacterObject")))
  			{
  				if (!IsActive)
  				{
  					IsActive = true;
  				}
  				DestroyPartyAction.Apply(null, this);
  			}
  		}
  		if (MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.3.0") && IsActive && MapFaction == null)
  		{
  			if (MapEvent != null)
  			{
  				MapEventSide = null;
  			}
  			RemoveParty();
  		}
2420:if (LeaderHero != null && CurrentSettlement != null && CurrentSettlement == LeaderHero.HomeSettlement)
  			{
  				LeaderHero.PassedTimeAtHomeSettlement++;
  			}
  			Anchor.HourlyTick();
  		}
  	}
  
  	public void MovePartyToTheClosestLand()
  	{
  		int[] invalidTerrainTypesForNavigationType = Campaign.Current.Models.PartyNavigationModel.GetInvalidTerrainTypesForNavigationType(NavigationType.All);
  		CampaignVec2 nearestFaceCenterForPositionWithPath = Campaign.Current.MapSceneWrapper.GetNearestFaceCenterForPositionWithPath(CurrentNavigationFace, targetIsLand: true, Campaign.MapDiagonal / 2f, invalidTerrainTypesForNavigationType);
  		SetNavigationModePoint(nearestFaceCenterForPositionWithPath);
  		SetMoveGoToPoint(nearestFaceCenterForPositionWithPath, NavigationType.All);
  	}
  
  	internal void DailyTick()
  	{
  		RecentEventsMorale -= RecentEventsMorale * 0.1f;
  		if (LeaderHero != null)
  		{
2422:LeaderHero.PassedTimeAtHomeSettlement++;
  			}
  			Anchor.HourlyTick();
  		}
  	}
  
  	public void MovePartyToTheClosestLand()
  	{
  		int[] invalidTerrainTypesForNavigationType = Campaign.Current.Models.PartyNavigationModel.GetInvalidTerrainTypesForNavigationType(NavigationType.All);
  		CampaignVec2 nearestFaceCenterForPositionWithPath = Campaign.Current.MapSceneWrapper.GetNearestFaceCenterForPositionWithPath(CurrentNavigationFace, targetIsLand: true, Campaign.MapDiagonal / 2f, invalidTerrainTypesForNavigationType);
  		SetNavigationModePoint(nearestFaceCenterForPositionWithPath);
  		SetMoveGoToPoint(nearestFaceCenterForPositionWithPath, NavigationType.All);
  	}
  
  	internal void DailyTick()
  	{
  		RecentEventsMorale -= RecentEventsMorale * 0.1f;
  		if (LeaderHero != null)
  		{
  			LeaderHero.PassedTimeAtHomeSettlement *= 0.9f;
  		}
2439:if (LeaderHero != null)
  		{
  			LeaderHero.PassedTimeAtHomeSettlement *= 0.9f;
  		}
  	}
  
  	public TextObject GetBehaviorText()
  	{
  		TextObject textObject = TextObject.GetEmpty();
  		if (Army != null && (AttachedTo != null || Army.LeaderParty == this) && !Army.LeaderParty.IsEngaging && !Army.LeaderParty.IsFleeing())
  		{
  			textObject = Army.GetLongTermBehaviorText();
  		}
  		if (textObject.IsEmpty())
  		{
  			float estimatedLandRatio;
  			if (DefaultBehavior == AiBehavior.Hold || ShortTermBehavior == AiBehavior.Hold || (IsMainParty && Campaign.Current.IsMainPartyWaiting))
  			{
  				textObject = ((!IsVillager || !HasNavalNavigationCapability) ? new TextObject("{=RClxLG6N}Holding.") : new TextObject("{=WYxUqYpu}Fishing."));
  			}
  			else if (ShortTermBehavior == AiBehavior.EngageParty && ShortTermTargetParty != null)
2441:LeaderHero.PassedTimeAtHomeSettlement *= 0.9f;
  		}
  	}
  
  	public TextObject GetBehaviorText()
  	{
  		TextObject textObject = TextObject.GetEmpty();
  		if (Army != null && (AttachedTo != null || Army.LeaderParty == this) && !Army.LeaderParty.IsEngaging && !Army.LeaderParty.IsFleeing())
  		{
  			textObject = Army.GetLongTermBehaviorText();
  		}
  		if (textObject.IsEmpty())
  		{
  			float estimatedLandRatio;
  			if (DefaultBehavior == AiBehavior.Hold || ShortTermBehavior == AiBehavior.Hold || (IsMainParty && Campaign.Current.IsMainPartyWaiting))
  			{
  				textObject = ((!IsVillager || !HasNavalNavigationCapability) ? new TextObject("{=RClxLG6N}Holding.") : new TextObject("{=WYxUqYpu}Fishing."));
  			}
  			else if (ShortTermBehavior == AiBehavior.EngageParty && ShortTermTargetParty != null)
  			{
  				textObject = new TextObject("{=5bzk75Ql}Engaging {TARGET_PARTY}.");
3050:Hero leaderHero = LeaderHero;
  		bool flag = !IsActive || leaderHero == null || leaderHero.PartyBelongedToAsPrisoner != null;
  		bool isNight = Campaign.Current.IsNight;
  		Vec2 vec = _lastWind;
  		if (IsCurrentlyAtSea)
  		{
  			vec = Campaign.Current.Models.MapWeatherModel.GetWindForPosition(Position);
  		}
  		if (_lastNavigationFace.FaceIndex == CurrentNavigationFace.FaceIndex && _partyLastCheckIsPrisoner == flag && _partyLastCheckAtNight == isNight && !(Math.Abs(_lastWind.RotationInRadians - vec.RotationInRadians) > 0.06f))
  		{
  			return Math.Abs(_lastWind.LengthSquared - vec.LengthSquared) > 0.0001f;
  		}
  		return true;
  	}
  
  	private bool IsBaseSpeedCacheInvalid()
  	{
  		UpdateCommonCacheVersions();
  		MapWeatherModel.WeatherEventEffectOnTerrain weatherEffectOnTerrainForPosition = Campaign.Current.Models.MapWeatherModel.GetWeatherEffectOnTerrainForPosition(Position.ToVec2());
  		if (_partyPureSpeedLastCheckVersion == GetVersionNoForBaseSpeedCalculation())
  		{
3051:bool flag = !IsActive || leaderHero == null || leaderHero.PartyBelongedToAsPrisoner != null;
  		bool isNight = Campaign.Current.IsNight;
  		Vec2 vec = _lastWind;
  		if (IsCurrentlyAtSea)
  		{
  			vec = Campaign.Current.Models.MapWeatherModel.GetWindForPosition(Position);
  		}
  		if (_lastNavigationFace.FaceIndex == CurrentNavigationFace.FaceIndex && _partyLastCheckIsPrisoner == flag && _partyLastCheckAtNight == isNight && !(Math.Abs(_lastWind.RotationInRadians - vec.RotationInRadians) > 0.06f))
  		{
  			return Math.Abs(_lastWind.LengthSquared - vec.LengthSquared) > 0.0001f;
  		}
  		return true;
  	}
  
  	private bool IsBaseSpeedCacheInvalid()
  	{
  		UpdateCommonCacheVersions();
  		MapWeatherModel.WeatherEventEffectOnTerrain weatherEffectOnTerrainForPosition = Campaign.Current.Models.MapWeatherModel.GetWeatherEffectOnTerrainForPosition(Position.ToVec2());
  		if (_partyPureSpeedLastCheckVersion == GetVersionNoForBaseSpeedCalculation())
  		{
  			return _lastWeatherTerrainEffect != weatherEffectOnTerrainForPosition;
3114:Hero leaderHero = LeaderHero;
  			bool partyLastCheckIsPrisoner = !IsActive || leaderHero == null || leaderHero.PartyBelongedToAsPrisoner != null;
  			bool isNight = Campaign.Current.IsNight;
  			if (IsCurrentlyAtSea)
  			{
  				_lastWind = Campaign.Current.Models.MapWeatherModel.GetWindForPosition(Position);
  			}
  			_lastNavigationFace = CurrentNavigationFace;
  			_partyLastCheckIsPrisoner = partyLastCheckIsPrisoner;
  			_partyLastCheckAtNight = isNight;
  			_lastCalculatedSpeed = Campaign.Current.Models.PartySpeedCalculatingModel.CalculateFinalSpeed(this, _lastCalculatedBaseSpeedExplained).ResultNumber;
  		}
  		return _lastCalculatedSpeed;
  	}
  
  	private bool IsWeightCacheInvalid()
  	{
  		UpdateCommonCacheVersions();
  		return _partyWeightLastCheckVersionNo != GetVersionNoForWeightCalculation();
  	}
  
3115:bool partyLastCheckIsPrisoner = !IsActive || leaderHero == null || leaderHero.PartyBelongedToAsPrisoner != null;
  			bool isNight = Campaign.Current.IsNight;
  			if (IsCurrentlyAtSea)
  			{
  				_lastWind = Campaign.Current.Models.MapWeatherModel.GetWindForPosition(Position);
  			}
  			_lastNavigationFace = CurrentNavigationFace;
  			_partyLastCheckIsPrisoner = partyLastCheckIsPrisoner;
  			_partyLastCheckAtNight = isNight;
  			_lastCalculatedSpeed = Campaign.Current.Models.PartySpeedCalculatingModel.CalculateFinalSpeed(this, _lastCalculatedBaseSpeedExplained).ResultNumber;
  		}
  		return _lastCalculatedSpeed;
  	}
  
  	private bool IsWeightCacheInvalid()
  	{
  		UpdateCommonCacheVersions();
  		return _partyWeightLastCheckVersionNo != GetVersionNoForWeightCalculation();
  	}
  
  	private void UpdateCommonCacheVersions()
3248:return LeaderHero?.GetPerkValue(perk) ?? false;
  		case PartyRole.ArmyCommander:
  			return Army?.LeaderParty?.LeaderHero?.GetPerkValue(perk) ?? false;
  		case PartyRole.PartyMember:
  			foreach (TroopRosterElement item in MemberRoster.GetTroopRoster())
  			{
  				if (item.Character.IsHero && item.Character.HeroObject.GetPerkValue(perk))
  				{
  					return true;
  				}
  			}
  			return false;
  		case PartyRole.Personal:
  			Debug.FailedAssert("personal perk is called in party", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Party\\MobileParty.cs", "HasPerk", 3195);
  			return LeaderHero?.GetPerkValue(perk) ?? false;
  		case PartyRole.ClanLeader:
  			if (LeaderHero != null)
  			{
  				return LeaderHero.Clan?.Leader?.GetPerkValue(perk) ?? false;
  			}
  			return false;
3250:return Army?.LeaderParty?.LeaderHero?.GetPerkValue(perk) ?? false;
  		case PartyRole.PartyMember:
  			foreach (TroopRosterElement item in MemberRoster.GetTroopRoster())
  			{
  				if (item.Character.IsHero && item.Character.HeroObject.GetPerkValue(perk))
  				{
  					return true;
  				}
  			}
  			return false;
  		case PartyRole.Personal:
  			Debug.FailedAssert("personal perk is called in party", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Party\\MobileParty.cs", "HasPerk", 3195);
  			return LeaderHero?.GetPerkValue(perk) ?? false;
  		case PartyRole.ClanLeader:
  			if (LeaderHero != null)
  			{
  				return LeaderHero.Clan?.Leader?.GetPerkValue(perk) ?? false;
  			}
  			return false;
  		default:
  			return false;
3262:return LeaderHero?.GetPerkValue(perk) ?? false;
  		case PartyRole.ClanLeader:
  			if (LeaderHero != null)
  			{
  				return LeaderHero.Clan?.Leader?.GetPerkValue(perk) ?? false;
  			}
  			return false;
  		default:
  			return false;
  		}
  	}
  
  	public void SetHeroPartyRole(Hero hero, PartyRole partyRole)
  	{
  		switch (partyRole)
  		{
  		case PartyRole.Surgeon:
  			SetPartySurgeon(hero);
  			break;
  		case PartyRole.Engineer:
  			SetPartyEngineer(hero);
3264:if (LeaderHero != null)
  			{
  				return LeaderHero.Clan?.Leader?.GetPerkValue(perk) ?? false;
  			}
  			return false;
  		default:
  			return false;
  		}
  	}
  
  	public void SetHeroPartyRole(Hero hero, PartyRole partyRole)
  	{
  		switch (partyRole)
  		{
  		case PartyRole.Surgeon:
  			SetPartySurgeon(hero);
  			break;
  		case PartyRole.Engineer:
  			SetPartyEngineer(hero);
  			break;
  		case PartyRole.Scout:
3266:return LeaderHero.Clan?.Leader?.GetPerkValue(perk) ?? false;
  			}
  			return false;
  		default:
  			return false;
  		}
  	}
  
  	public void SetHeroPartyRole(Hero hero, PartyRole partyRole)
  	{
  		switch (partyRole)
  		{
  		case PartyRole.Surgeon:
  			SetPartySurgeon(hero);
  			break;
  		case PartyRole.Engineer:
  			SetPartyEngineer(hero);
  			break;
  		case PartyRole.Scout:
  			SetPartyScout(hero);
  			break;
3438:PartyRole.PartyLeader => LeaderHero,
  			PartyRole.Surgeon => Surgeon, 
  			PartyRole.Engineer => Engineer, 
  			PartyRole.Quartermaster => Quartermaster, 
  			PartyRole.Scout => Scout, 
  			PartyRole.FirstMate => FirstMate, 
  			PartyRole.Navigator => Navigator, 
  			_ => null, 
  		};
  	}
  
  	public Hero GetEffectiveRoleHolder(PartyRole partyRole)
  	{
  		return partyRole switch
  		{
  			PartyRole.PartyLeader => LeaderHero, 
  			PartyRole.Surgeon => EffectiveSurgeon, 
  			PartyRole.Engineer => EffectiveEngineer, 
  			PartyRole.Quartermaster => EffectiveQuartermaster, 
  			PartyRole.Scout => EffectiveScout, 
  			PartyRole.FirstMate => EffectiveFirstMate, 
3453:PartyRole.PartyLeader => LeaderHero,
  			PartyRole.Surgeon => EffectiveSurgeon, 
  			PartyRole.Engineer => EffectiveEngineer, 
  			PartyRole.Quartermaster => EffectiveQuartermaster, 
  			PartyRole.Scout => EffectiveScout, 
  			PartyRole.FirstMate => EffectiveFirstMate, 
  			PartyRole.Navigator => EffectiveNavigator, 
  			_ => null, 
  		};
  	}
  
  	public int GetNumDaysForFoodToLast()
  	{
  		int totalFood = ItemRoster.TotalFood;
  		totalFood *= 100;
  		if (this == MainParty)
  		{
  			totalFood += Party.RemainingFoodPercentage;
  		}
  		return (int)((float)totalFood / (100f * (0f - FoodChange)));
  	}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\MobileParty.cs' | ForEach-Object { "'$i++; if ($i -ge 1075 -and $i -le 1118) { '"'{0}:{1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 1182ms:
1075:	public ItemRoster ItemRoster => Party.ItemRoster;
1076:
1077:	public bool IsMainParty => this == MainParty;
1078:
1079:	public IFaction MapFaction
1080:	{
1081:		get
1082:		{
1083:			if (ActualClan != null)
1084:			{
1085:				return ActualClan.MapFaction;
1086:			}
1087:			if (Party.Owner != null)
1088:			{
1089:				if (Party.Owner == Hero.MainHero)
1090:				{
1091:					return Party.Owner.MapFaction;
1092:				}
1093:				if (Party.Owner.IsNotable)
1094:				{
1095:					return Party.Owner.HomeSettlement.MapFaction;
1096:				}
1097:				if ((IsMilitia || IsGarrison || IsVillager || IsPatrolParty) && HomeSettlement?.OwnerClan != null)
1098:				{
1099:					return HomeSettlement.OwnerClan.MapFaction;
1100:				}
1101:				if (IsCaravan || IsBanditBossParty)
1102:				{
1103:					return Party.Owner.MapFaction;
1104:				}
1105:				if (_isCurrentlyUsedByAQuest && Party.Owner != null)
1106:				{
1107:					return Party.Owner.MapFaction;
1108:				}
1109:				return (LeaderHero != null) ? LeaderHero.MapFaction : null;
1110:			}
1111:			if (HomeSettlement != null)
1112:			{
1113:				return HomeSettlement.OwnerClan.MapFaction;
1114:			}
1115:			return (LeaderHero != null) ? LeaderHero.MapFaction : null;
1116:		}
1117:	}
1118:

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main -Recurse -Include *.cs | Select-String -Pattern 'PartyBaseHelper\\.HasFeat|HasFeat\\(' | ForEach-Object { '{0}:{1}:{2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 807ms:
Main\Features\BattleBalance\Models\TaomPartyHealingModel.cs:39:// Vanilla PartyBaseHelper.HasFeat precedence — same fix family Codex 43 + 46
Main\Features\CulturalFeats\Models\TaomArmyManagementModel.cs:22:// vanilla PartyBaseHelper.HasFeat precedence (LeaderHero-first, MapFaction-aware), null-safe.
Main\Features\CulturalFeats\Models\TaomBattleRewardModel.cs:34:// Vanilla PartyBaseHelper.HasFeat precedence via the shared helper. Replaces the prior
Main\Features\CulturalFeats\Models\TaomFoodConsumptionModel.cs:21:// Vanilla PartyBaseHelper.HasFeat precedence via the shared helper.
Main\Features\CulturalFeats\Models\TaomPartyMoraleModel.cs:25:// Vanilla PartyBaseHelper.HasFeat precedence via the shared helper.
Main\Features\CulturalFeats\Models\TaomPartySizeModel.cs:25:// Vanilla PartyBaseHelper.HasFeat precedence — see CultureFeatAdapter.FromOrNull(PartyBase).
Main\Features\CulturalFeats\Models\TaomPartyTroopUpgradeModel.cs:25:// Vanilla PartyBaseHelper.HasFeat precedence via the shared CultureFeatAdapter helper —
Main\Features\CulturalFeats\Models\TaomRaidModel.cs:25:// Vanilla PartyBaseHelper.HasFeat precedence via the shared chokepoint (LeaderHero-first,
Main\Features\CulturalFeats\CulturalFeatsService.cs:48:if (culture.HasFeat(TaomCulturalFeats.RivendellArmyInfluenceFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:50:if (culture.HasFeat(TaomCulturalFeats.GondorArmyInfluenceFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:61:if (culture.HasFeat(TaomCulturalFeats.RivendellArmyInfluenceCostFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:63:if (culture.HasFeat(TaomCulturalFeats.GundabadArmyInfluenceCostFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:65:if (culture.HasFeat(TaomCulturalFeats.MistyMountainOrcsArmyInfluenceCostFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:67:if (culture.HasFeat(TaomCulturalFeats.DolGuldurArmyInfluenceCostFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:69:if (culture.HasFeat(TaomCulturalFeats.MordorArmyInfluenceCostFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:71:if (culture.HasFeat(TaomCulturalFeats.MirkwoodArmyInfluenceCostFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:73:if (culture.HasFeat(TaomCulturalFeats.HaradArmyInfluenceCostFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:130:if (culture.HasFeat(feat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:139:if (!culture.HasFeat(TaomCulturalFeats.RohanInfantrySpeedFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:154:if (culture.HasFeat(TaomCulturalFeats.RivendellHearthGrowthFeat) && result.ResultNumber >= 0f)
Main\Features\CulturalFeats\CulturalFeatsService.cs:156:if (culture.HasFeat(TaomCulturalFeats.MirkwoodHearthGrowthFeat) && result.ResultNumber >= 0f)
Main\Features\CulturalFeats\CulturalFeatsService.cs:158:if (culture.HasFeat(TaomCulturalFeats.GondorHearthGrowthFeat) && result.ResultNumber >= 0f)
Main\Features\CulturalFeats\CulturalFeatsService.cs:168:if (culture.HasFeat(TaomCulturalFeats.MirkwoodMilitiaProductionFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:170:if (culture.HasFeat(TaomCulturalFeats.DolGuldurMilitiaProductionFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:180:if (culture.HasFeat(TaomCulturalFeats.EreborConstructionSpeedFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:182:if (culture.HasFeat(TaomCulturalFeats.LothlorienConstructionSpeedFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:184:if (culture.HasFeat(TaomCulturalFeats.DolGuldurConstructionSpeedFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:186:if (culture.HasFeat(TaomCulturalFeats.IsengardConstructionSpeedFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:188:if (culture.HasFeat(TaomCulturalFeats.MistyMountainOrcsConstructionSpeedFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:198:if (culture.HasFeat(TaomCulturalFeats.EreborProductionFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:200:if (isGrain && culture.HasFeat(TaomCulturalFeats.GundabadGrainProductionFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:202:if (isGrain && culture.HasFeat(TaomCulturalFeats.MordorGrainProductionFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:212:if (culture.HasFeat(TaomCulturalFeats.UmbarCheaperCaravansFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:223:if (culture.HasFeat(TaomCulturalFeats.UmbarRenownFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:225:if (culture.HasFeat(TaomCulturalFeats.DaleRenownFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:227:if (culture.HasFeat(TaomCulturalFeats.KhandRenownFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:237:if (culture.HasFeat(TaomCulturalFeats.IsengardCheaperRecruitsFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:239:if (culture.HasFeat(TaomCulturalFeats.RohanMountedCostFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:249:if (culture.HasFeat(TaomCulturalFeats.MordorPartySizeFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:251:if (culture.HasFeat(TaomCulturalFeats.GundabadPartySizeFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:253:if (culture.HasFeat(TaomCulturalFeats.GoblinPartySizeFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:255:if (culture.HasFeat(TaomCulturalFeats.MistyMountainOrcsPartySizeFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:257:if (culture.HasFeat(TaomCulturalFeats.DolGuldurPartySizeFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:259:if (culture.HasFeat(TaomCulturalFeats.IsengardPartySizeFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:261:if (culture.HasFeat(TaomCulturalFeats.GondorPartySizeFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:263:if (culture.HasFeat(TaomCulturalFeats.DunlandPartySizeFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:265:if (culture.HasFeat(TaomCulturalFeats.RhunPartySizeFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:267:if (culture.HasFeat(TaomCulturalFeats.HaradPartySizeFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:269:if (culture.HasFeat(TaomCulturalFeats.KhandPartySizeFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:279:if (culture.HasFeat(TaomCulturalFeats.DunlandVolunteerRateFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:281:if (culture.HasFeat(TaomCulturalFeats.GundabadVolunteerRateFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:283:if (culture.HasFeat(TaomCulturalFeats.GoblinVolunteerRateFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:285:if (culture.HasFeat(TaomCulturalFeats.DolGuldurVolunteerRateFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:287:if (culture.HasFeat(TaomCulturalFeats.MordorVolunteerRateFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:289:if (culture.HasFeat(TaomCulturalFeats.LothlorienVolunteerRateFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:308:if (culture.HasFeat(TaomCulturalFeats.IsengardNotableCountTownMerchantFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:310:if (culture.HasFeat(TaomCulturalFeats.DolGuldurNotableCountTownMerchantFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:317:if (culture.HasFeat(TaomCulturalFeats.IsengardNotableCountTownArtisanFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:319:if (culture.HasFeat(TaomCulturalFeats.DolGuldurNotableCountTownArtisanFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:321:if (culture.HasFeat(TaomCulturalFeats.GundabadNotableCountTownArtisanFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:328:if (culture.HasFeat(TaomCulturalFeats.IsengardNotableCountTownGangLeaderFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:330:if (culture.HasFeat(TaomCulturalFeats.DolGuldurNotableCountTownGangLeaderFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:332:if (culture.HasFeat(TaomCulturalFeats.MordorNotableCountTownGangLeaderFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:334:if (culture.HasFeat(TaomCulturalFeats.GundabadNotableCountTownGangLeaderFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:346:if (culture.HasFeat(TaomCulturalFeats.IsengardNotableCountVillageFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:348:if (culture.HasFeat(TaomCulturalFeats.DolGuldurNotableCountVillageFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:350:if (culture.HasFeat(TaomCulturalFeats.MordorNotableCountVillageFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:352:if (culture.HasFeat(TaomCulturalFeats.GundabadNotableCountVillageFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:369:if (culture.HasFeat(TaomCulturalFeats.RivendellFoodConsumptionFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:371:if (culture.HasFeat(TaomCulturalFeats.MirkwoodFoodConsumptionFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:373:if (culture.HasFeat(TaomCulturalFeats.LothlorienFoodConsumptionFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:375:if (culture.HasFeat(TaomCulturalFeats.DolGuldurFoodConsumptionFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:377:if (culture.HasFeat(TaomCulturalFeats.GoblinFoodConsumptionFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:379:if (culture.HasFeat(TaomCulturalFeats.MistyMountainOrcsFoodConsumptionFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:381:if (culture.HasFeat(TaomCulturalFeats.UmbarFoodConsumptionFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:383:if (culture.HasFeat(TaomCulturalFeats.KhandFoodConsumptionFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:385:if (culture.HasFeat(TaomCulturalFeats.HaradFoodConsumptionFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:395:if (culture.HasFeat(TaomCulturalFeats.GondorLoyaltyFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:397:if (culture.HasFeat(TaomCulturalFeats.EreborLoyaltyFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:399:if (culture.HasFeat(TaomCulturalFeats.LothlorienLoyaltyFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:401:if (culture.HasFeat(TaomCulturalFeats.RivendellLoyaltyFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:403:if (culture.HasFeat(TaomCulturalFeats.RohanLoyaltyFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:405:if (culture.HasFeat(TaomCulturalFeats.DaleLoyaltyFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:407:if (culture.HasFeat(TaomCulturalFeats.RhunLoyaltyFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:417:if (culture.HasFeat(TaomCulturalFeats.GondorMoraleFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:419:if (culture.HasFeat(TaomCulturalFeats.RohanMoraleFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:421:if (culture.HasFeat(TaomCulturalFeats.EreborMoraleFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:423:if (culture.HasFeat(TaomCulturalFeats.MirkwoodMoraleFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:425:if (culture.HasFeat(TaomCulturalFeats.LothlorienMoraleFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:427:if (culture.HasFeat(TaomCulturalFeats.HaradMoraleFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:437:if (culture.HasFeat(TaomCulturalFeats.EreborSmithingFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:439:if (culture.HasFeat(TaomCulturalFeats.IsengardSmithingFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:441:if (culture.HasFeat(TaomCulturalFeats.MordorSmithingFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:443:if (culture.HasFeat(TaomCulturalFeats.GoblinSmithingFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:445:if (culture.HasFeat(TaomCulturalFeats.MistyMountainOrcsSmithingFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:455:if (culture.HasFeat(TaomCulturalFeats.UmbarTariffIncomeFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:457:if (culture.HasFeat(TaomCulturalFeats.EreborTariffIncomeFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:459:if (culture.HasFeat(TaomCulturalFeats.DaleTariffIncomeFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:461:if (culture.HasFeat(TaomCulturalFeats.KhandTariffIncomeFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:471:if (culture.HasFeat(TaomCulturalFeats.MordorRaidDamageFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:473:if (culture.HasFeat(TaomCulturalFeats.GundabadRaidDamageFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:475:if (culture.HasFeat(TaomCulturalFeats.IsengardRaidDamageFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:477:if (culture.HasFeat(TaomCulturalFeats.UmbarRaidDamageFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:479:if (culture.HasFeat(TaomCulturalFeats.GoblinRaidDamageFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:481:if (culture.HasFeat(TaomCulturalFeats.MistyMountainOrcsRaidDamageFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:483:if (culture.HasFeat(TaomCulturalFeats.HaradRaidDamageFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:485:if (culture.HasFeat(TaomCulturalFeats.RhunRaidDamageFeat))
Main\Features\CulturalFeats\CultureFeatAdapter.cs:25:public bool HasFeat(FeatObject feat)
Main\Features\CulturalFeats\CultureFeatAdapter.cs:29:return _culture.HasFeat(feat);
Main\Features\CulturalFeats\CultureFeatAdapter.cs:42:/// with the same precedence as vanilla <c>PartyBaseHelper.HasFeat</c>:
Main\Features\CulturalFeats\CultureFeatAdapter.cs:54:/// Boundary helper — same vanilla <c>PartyBaseHelper.HasFeat</c> precedence
Main\Features\CulturalFeats\ICultureFeatAdapter.cs:20:bool HasFeat(FeatObject feat);
Main\Features\Diplomacy\Models\TaomDiplomacyModel.cs:34:&& supporter.Culture?.HasFeat(TaomCulturalFeats.IsengardDecisionPenaltyFeat) == true)
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:50:// CultureFeatAdapter.ResolvePartyCulture chokepoint (vanilla PartyBaseHelper.HasFeat
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:176:=> culture?.HasFeat(feat) == true ? feat.EffectBonus : 0f;
Main\Features\TroopProgression\IWageModifierService.cs:50:/// The model resolves <c>CultureObject.HasFeat(feat) ? feat.EffectBonus : 0f</c>

codex
I’m verifying the overridden GameModel signatures from the decompiled base classes as a final API-compat check. That covers both the raid `LeaderParty` type and the changed method calls compiling against the expected Bannerlord API.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\Campaign' -Recurse -Filter 'DefaultArmyManagementCalculationModel.cs' -ErrorAction SilentlyContinue | Select-Object -First 5 -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\Campaign' -Recurse -Filter 'DefaultPartyWageModel.cs' -ErrorAction SilentlyContinue | Select-Object -First 5 -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\Campaign' -Recurse -Filter 'DefaultRaidModel.cs' -ErrorAction SilentlyContinue | Select-Object -First 5 -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\Campaign' -Recurse -Filter 'DefaultBattleRewardModel.cs' -ErrorAction SilentlyContinue | Select-Object -First 5 -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 557ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultArmyManagementCalculationModel.cs

 succeeded in 555ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultRaidModel.cs

 succeeded in 585ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPartyWageModel.cs

 succeeded in 568ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultBattleRewardModel.cs

codex
The base model files are present; I’m extracting just their method declarations and any base calls relevant to null behavior.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultArmyManagementCalculationModel.cs' -Pattern 'DailyBeingAtArmyInfluenceAward|CalculatePartyInfluenceCost' -Context 0,8 | ForEach-Object { "'$_.LineNumber.ToString() + '"':' + "'$_.Line.Trim(); $_.Context.PostContext | ForEach-Object { '"'  ' + "'$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultRaidModel.cs' -Pattern 'CalculateHitDamage' -Context 0,8 | ForEach-Object { "'$_.LineNumber.ToString() + '"':' + "'$_.Line.Trim(); $_.Context.PostContext | ForEach-Object { '"'  ' + "'$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultPartyWageModel.cs' -Pattern 'GetTotalWage' -Context 0,8 | ForEach-Object { "'$_.LineNumber.ToString() + '"':' + "'$_.Line.Trim(); $_.Context.PostContext | ForEach-Object { '"'  ' + "'$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultBattleRewardModel.cs' -Pattern 'CalculateRenownGain' -Context 0,8 | ForEach-Object { "'$_.LineNumber.ToString() + '"':' + "'$_.Line.Trim(); $_.Context.PostContext | ForEach-Object { '"'  ' + "'$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 590ms:
46:public override ExplainedNumber CalculateHitDamage(MapEventSide attackerSide, float settlementHitPoints)
  	{
  		float num = (MathF.Sqrt(attackerSide.TroopCount) + 5f) / 900f;
  		ExplainedNumber result = new ExplainedNumber(num * (float)CampaignTime.DeltaTime.ToHours);
  		foreach (MapEventParty party in attackerSide.Parties)
  		{
  			if (party.Party.MobileParty?.LeaderHero != null && party.Party.MobileParty.LeaderHero.GetPerkValue(DefaultPerks.Roguery.NoRestForTheWicked))
  			{
  				result.AddFactor(DefaultPerks.Roguery.NoRestForTheWicked.SecondaryBonus);

 succeeded in 591ms:
48:public override float DailyBeingAtArmyInfluenceAward(MobileParty armyMemberParty)
  	{
  		float num = (armyMemberParty.Party.EstimatedStrength + 20f) / 200f;
  		if (PartyBaseHelper.HasFeat(armyMemberParty.Party, DefaultCulturalFeats.EmpireArmyInfluenceFeat))
  		{
  			num += num * DefaultCulturalFeats.EmpireArmyInfluenceFeat.EffectBonus;
  		}
  		return num;
  	}
58:public override int CalculatePartyInfluenceCost(MobileParty armyLeaderParty, MobileParty party)
  	{
  		if (armyLeaderParty.LeaderHero != null && party.LeaderHero != null && armyLeaderParty.LeaderHero.Clan == party.LeaderHero.Clan)
  		{
  			return 0;
  		}
  		float num = armyLeaderParty.LeaderHero.GetRelation(party.LeaderHero);
  		float partySizeScore = GetPartySizeScore(party);
  		float b = MathF.Round(party.Party.EstimatedStrength);
156:int num = Campaign.Current.Models.ArmyManagementCalculationModel.CalculatePartyInfluenceCost(mobileParty, mobileParty2);
  					float estimatedStrength = mobileParty2.Party.EstimatedStrength;
  					float num2 = 1f - (float)mobileParty2.Party.MemberRoster.TotalWounded / (float)mobileParty2.Party.MemberRoster.TotalManCount;
  					float item = estimatedStrength / ((float)num + 0.1f) * num2;
  					list.Add((mobileParty2, item, num));
  				}
  			}
  			list = list.OrderByQ(((MobileParty, float, int) x) => x.Item2).ToListQ();
  			int count = kingdom.WarPartyComponents.Count;
212:num += CalculatePartyInfluenceCost(army.LeaderParty, item);
  		}
  		ExplainedNumber explainedNumber = new ExplainedNumber(num);
  		if (army.LeaderParty.MapFaction.IsKingdomFaction && ((Kingdom)army.LeaderParty.MapFaction).ActivePolicies.Contains(DefaultPolicies.RoyalCommissions))
  		{
  			explainedNumber.AddFactor(-0.3f, DefaultPolicies.RoyalCommissions.Name);
  		}
  		if (army.LeaderParty.LeaderHero.GetPerkValue(DefaultPerks.Tactics.Encirclement))
  		{

 succeeded in 569ms:
48:public override ExplainedNumber CalculateRenownGain(PartyBase winnerParty, float renownValueOfBattleForWinnerSide, float contributionShareOfWinnerParty, float renownMultiplierForWinnerSide, bool includeDescriptions)
  	{
  		ExplainedNumber stat = new ExplainedNumber(contributionShareOfWinnerParty * renownValueOfBattleForWinnerSide * renownMultiplierForWinnerSide, includeDescriptions);
  		if (winnerParty.IsMobile)
  		{
  			if (winnerParty.MobileParty.HasPerk(DefaultPerks.Throwing.LongReach, checkSecondaryRole: true))
  			{
  				PerkHelper.AddPerkBonusForParty(DefaultPerks.Throwing.LongReach, winnerParty.MobileParty, isPrimaryBonus: false, ref stat);
  			}

 succeeded in 584ms:
43:public override ExplainedNumber GetTotalWage(MobileParty mobileParty, TroopRoster troopRoster, bool includeDescriptions = false)
  	{
  		int num = 0;
  		int num2 = 0;
  		int num3 = 0;
  		int num4 = 0;
  		int num5 = 0;
  		int num6 = 0;
  		bool flag = !mobileParty.HasPerk(DefaultPerks.Steward.AidCorps);

codex
A separate risk surfaced while checking base signatures: some vanilla base methods themselves call `PartyBaseHelper.HasFeat`, which still contains the throwing `party.Culture` getter. I’m tracing the overridden base methods now because a TAOM wrapper cannot prevent an NRE that happens inside `base.*` before the TAOM feat code runs.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\Campaign' -Recurse -Filter 'DefaultPartySizeLimitModel.cs' -ErrorAction SilentlyContinue | Select-Object -First 5 -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\Campaign' -Recurse -Filter 'DefaultPartyMoraleModel.cs' -ErrorAction SilentlyContinue | Select-Object -First 5 -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\Campaign' -Recurse -Filter 'DefaultMobilePartyFoodConsumptionModel.cs' -ErrorAction SilentlyContinue | Select-Object -First 5 -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\Campaign' -Recurse -Filter 'DefaultPartySpeedCalculatingModel.cs' -ErrorAction SilentlyContinue | Select-Object -First 5 -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 468ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPartySizeLimitModel.cs

 succeeded in 481ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPartyMoraleModel.cs

 succeeded in 477ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMobilePartyFoodConsumptionModel.cs

 succeeded in 464ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPartySpeedCalculatingModel.cs

codex
I found at least one vanilla base method (`DefaultArmyManagementCalculationModel.DailyBeingAtArmyInfluenceAward`) calling `PartyBaseHelper.HasFeat` directly. I’m checking whether the specific crash-fix models also call such base code, because that would be a real remaining exposure even with the new chokepoint.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultPartySizeLimitModel.cs' -Pattern 'GetPartyMemberSizeLimit|PartyBaseHelper\\.HasFeat|\\.Culture|HasFeat' -Context 0,6 | ForEach-Object { "'$_.LineNumber.ToString() + '"':' + "'$_.Line.Trim(); $_.Context.PostContext | ForEach-Object { '"'  ' + "'$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultPartyMoraleModel.cs' -Pattern 'GetEffectivePartyMorale|PartyBaseHelper\\.HasFeat|\\.Culture|HasFeat' -Context 0,6 | ForEach-Object { "'$_.LineNumber.ToString() + '"':' + "'$_.Line.Trim(); $_.Context.PostContext | ForEach-Object { '"'  ' + "'$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultMobilePartyFoodConsumptionModel.cs' -Pattern 'CalculateDailyFoodConsumptionf|PartyBaseHelper\\.HasFeat|\\.Culture|HasFeat' -Context 0,6 | ForEach-Object { "'$_.LineNumber.ToString() + '"':' + "'$_.Line.Trim(); $_.Context.PostContext | ForEach-Object { '"'  ' + "'$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultPartySpeedCalculatingModel.cs' -Pattern 'CalculateFinalSpeed|PartyBaseHelper\\.HasFeat|\\.Culture|HasFeat' -Context 0,6 | ForEach-Object { "'$_.LineNumber.ToString() + '"':' + "'$_.Line.Trim(); $_.Context.PostContext | ForEach-Object { '"'  ' + "'$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 510ms:
81:public override ExplainedNumber GetPartyMemberSizeLimit(PartyBase party, bool includeDescriptions = false)
  	{
  		ExplainedNumber result = new ExplainedNumber(0f, includeDescriptions);
  		if (party.IsMobile)
  		{
  			if (party.MobileParty.IsGarrison)
  			{

 succeeded in 489ms:
215:public override ExplainedNumber GetEffectivePartyMorale(MobileParty mobileParty, bool includeDescription = false)
  	{
  		ExplainedNumber bonus = new ExplainedNumber(50f, includeDescription);
  		bonus.Add(mobileParty.RecentEventsMorale, _recentEventsText);
  		GetMoraleEffectsFromSkill(mobileParty, ref bonus);
  		if (mobileParty.IsMilitia || mobileParty.IsGarrison)
  		{

 succeeded in 488ms:
25:public override ExplainedNumber CalculateDailyFoodConsumptionf(MobileParty party, ExplainedNumber baseConsumption)
  	{
  		CalculatePerkEffects(party, ref baseConsumption);
  		baseConsumption.LimitMax(-0.01f);
  		return baseConsumption;
  	}
  
37:if (party.MemberRoster.GetCharacterAtIndex(i).Culture.IsBandit)
  			{
  				num += party.MemberRoster.GetElementNumber(i);
  			}
  		}
  		for (int j = 0; j < party.PrisonRoster.Count; j++)
  		{
44:if (party.PrisonRoster.GetCharacterAtIndex(j).Culture.IsBandit)
  			{
  				num += party.PrisonRoster.GetElementNumber(j);
  			}
  		}
  		if (party.LeaderHero != null && party.LeaderHero.GetPerkValue(DefaultPerks.Roguery.Promises) && num > 0)
  		{

 succeeded in 462ms:
256:public override ExplainedNumber CalculateFinalSpeed(MobileParty mobileParty, ExplainedNumber finalSpeed)
  	{
  		if (mobileParty.IsCustomParty && !((CustomPartyComponent)mobileParty.PartyComponent).BaseSpeed.ApproximatelyEqualsTo(0f))
  		{
  			finalSpeed = new ExplainedNumber(((CustomPartyComponent)mobileParty.PartyComponent).BaseSpeed);
  		}
  		TerrainType faceTerrainType = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(mobileParty.CurrentNavigationFace);
279:if (PartyBaseHelper.HasFeat(mobileParty.Party, DefaultCulturalFeats.BattanianForestSpeedFeat))
  			{
  				float value2 = DefaultCulturalFeats.BattanianForestSpeedFeat.EffectBonus * 0.3f;
  				finalSpeed.AddFactor(value2, _culture);
  			}
  		}
  		else if (!mobileParty.IsCurrentlyAtSea && (faceTerrainType == TerrainType.Water || faceTerrainType == TerrainType.River || faceTerrainType == TerrainType.UnderBridge || faceTerrainType == TerrainType.Bridge || faceTerrainType == TerrainType.Fording))
295:if (!PartyBaseHelper.HasFeat(mobileParty.Party, DefaultCulturalFeats.AseraiDesertFeat))
  				{
  					finalSpeed.AddFactor(-0.1f, _desert);
  				}
  				if (effectiveScout != null && effectiveScout.GetPerkValue(DefaultPerks.Scouting.DesertBorn))
  				{
  					finalSpeed.AddFactor(DefaultPerks.Scouting.DesertBorn.PrimaryBonus, DefaultPerks.Scouting.DesertBorn.Name);

codex
The base speed model definitely has vanilla `PartyBaseHelper.HasFeat` calls before TAOM’s added cultural feats. I’m expanding the size/morale snippets because the first grep only showed method starts; if they also use `HasFeat`, the fix may leave the original crash path exposed.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultPartySizeLimitModel.cs' -Pattern 'PartyBaseHelper\\.HasFeat|HasFeat\\(' | ForEach-Object { '{0}:{1}' -f "'$_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultPartyMoraleModel.cs' -Pattern 'PartyBaseHelper\\.HasFeat|HasFeat\\(' | ForEach-Object { '{0}:{1}' -f "'$_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultBattleRewardModel.cs' -Pattern 'PartyBaseHelper\\.HasFeat|HasFeat\\(' | ForEach-Object { '{0}:{1}' -f "'$_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultPartyTroopUpgradeModel.cs' -Pattern 'PartyBaseHelper\\.HasFeat|HasFeat\\(' -ErrorAction SilentlyContinue | ForEach-Object { '{0}:{1}' -f "'$_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 481ms:
 succeeded in 464ms:
 succeeded in 474ms:
65:if (PartyBaseHelper.HasFeat(winnerParty, DefaultCulturalFeats.VlandianRenownMercenaryFeat))

 succeeded in 466ms:
91:if (characterObject.IsMounted && PartyBaseHelper.HasFeat(party, DefaultCulturalFeats.KhuzaitRecruitUpgradeFeat))

codex
Base `DefaultBattleRewardModel` and `DefaultPartyTroopUpgradeModel` also call `PartyBaseHelper.HasFeat`; `DefaultPartySpeedCalculatingModel` and `DefaultArmyManagementCalculationModel.DailyBeingAtArmyInfluenceAward` do too. I’m checking the exact ordering in TAOM wrappers to see which of those can still throw before or independent of the new helper.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultPartyTroopUpgradeModel.cs' | ForEach-Object { "'$i++; if ($i -ge 80 -and $i -le 100) { '"'{0}:{1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultBattleRewardModel.cs' | ForEach-Object { "'$i++; if ($i -ge 48 -and $i -le 72) { '"'{0}:{1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultPartySpeedCalculatingModel.cs' | ForEach-Object { "'$i++; if ($i -ge 268 -and $i -le 300) { '"'{0}:{1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultArmyManagementCalculationModel.cs' | ForEach-Object { "'$i++; if ($i -ge 48 -and $i -le 66) { '"'{0}:{1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 505ms:
48:	public override ExplainedNumber CalculateRenownGain(PartyBase winnerParty, float renownValueOfBattleForWinnerSide, float contributionShareOfWinnerParty, float renownMultiplierForWinnerSide, bool includeDescriptions)
49:	{
50:		ExplainedNumber stat = new ExplainedNumber(contributionShareOfWinnerParty * renownValueOfBattleForWinnerSide * renownMultiplierForWinnerSide, includeDescriptions);
51:		if (winnerParty.IsMobile)
52:		{
53:			if (winnerParty.MobileParty.HasPerk(DefaultPerks.Throwing.LongReach, checkSecondaryRole: true))
54:			{
55:				PerkHelper.AddPerkBonusForParty(DefaultPerks.Throwing.LongReach, winnerParty.MobileParty, isPrimaryBonus: false, ref stat);
56:			}
57:			if (winnerParty.MobileParty.HasPerk(DefaultPerks.Charm.PublicSpeaker))
58:			{
59:				stat.AddFactor(DefaultPerks.Charm.PublicSpeaker.PrimaryBonus, DefaultPerks.Charm.PublicSpeaker.Name);
60:			}
61:			if (winnerParty.LeaderHero != null)
62:			{
63:				PerkHelper.AddPerkBonusForCharacter(DefaultPerks.Leadership.FamousCommander, winnerParty.LeaderHero.CharacterObject, isPrimaryBonus: true, ref stat, winnerParty.MobileParty.IsCurrentlyAtSea);
64:			}
65:			if (PartyBaseHelper.HasFeat(winnerParty, DefaultCulturalFeats.VlandianRenownMercenaryFeat))
66:			{
67:				stat.AddFactor(DefaultCulturalFeats.VlandianRenownMercenaryFeat.EffectBonus, GameTexts.FindText("str_culture"));
68:			}
69:		}
70:		return stat;
71:	}
72:

 succeeded in 515ms:
80:		int roundedResultNumber2 = partyWageModel.GetTroopRecruitmentCost(characterObject, null, withoutItemCost: true).RoundedResultNumber;
81:		bool flag = characterObject.Occupation == Occupation.Mercenary || characterObject.Occupation == Occupation.Gangster;
82:		ExplainedNumber stat = new ExplainedNumber((float)(roundedResultNumber - roundedResultNumber2) / ((!flag) ? 2f : 3f));
83:		if (party.MobileParty.HasPerk(DefaultPerks.Steward.SoundReserves))
84:		{
85:			PerkHelper.AddPerkBonusForParty(DefaultPerks.Steward.SoundReserves, party.MobileParty, isPrimaryBonus: true, ref stat);
86:		}
87:		if (characterObject.IsRanged && party.MobileParty.HasPerk(DefaultPerks.Bow.RenownedArcher, checkSecondaryRole: true))
88:		{
89:			PerkHelper.AddPerkBonusForParty(DefaultPerks.Bow.RenownedArcher, party.MobileParty, isPrimaryBonus: false, ref stat);
90:		}
91:		if (characterObject.IsMounted && PartyBaseHelper.HasFeat(party, DefaultCulturalFeats.KhuzaitRecruitUpgradeFeat))
92:		{
93:			stat.AddFactor(DefaultCulturalFeats.KhuzaitRecruitUpgradeFeat.EffectBonus, GameTexts.FindText("str_culture"));
94:		}
95:		if (flag && party.MobileParty.HasPerk(DefaultPerks.Steward.Contractors))
96:		{
97:			PerkHelper.AddPerkBonusForParty(DefaultPerks.Steward.Contractors, party.MobileParty, isPrimaryBonus: true, ref stat);
98:		}
99:		return stat;
100:	}

 succeeded in 487ms:
268:			{
269:				for (int i = 0; i < mobileParty.MemberRoster.Count; i++)
270:				{
271:					if (!mobileParty.MemberRoster.GetCharacterAtIndex(i).IsMounted)
272:					{
273:						num += (float)mobileParty.MemberRoster.GetElementNumber(i);
274:					}
275:				}
276:			}
277:			float value = ((num / (float)mobileParty.MemberRoster.TotalManCount >= 0.75f) ? (-0.3f * (0f - DefaultPerks.Scouting.ForestKin.PrimaryBonus)) : (-0.3f));
278:			finalSpeed.AddFactor(value, _movingInForest);
279:			if (PartyBaseHelper.HasFeat(mobileParty.Party, DefaultCulturalFeats.BattanianForestSpeedFeat))
280:			{
281:				float value2 = DefaultCulturalFeats.BattanianForestSpeedFeat.EffectBonus * 0.3f;
282:				finalSpeed.AddFactor(value2, _culture);
283:			}
284:		}
285:		else if (!mobileParty.IsCurrentlyAtSea && (faceTerrainType == TerrainType.Water || faceTerrainType == TerrainType.River || faceTerrainType == TerrainType.UnderBridge || faceTerrainType == TerrainType.Bridge || faceTerrainType == TerrainType.Fording))
286:		{
287:			finalSpeed.AddFactor(-0.3f, _fordEffect);
288:		}
289:		else
290:		{
291:			switch (faceTerrainType)
292:			{
293:			case TerrainType.Desert:
294:			case TerrainType.Dune:
295:				if (!PartyBaseHelper.HasFeat(mobileParty.Party, DefaultCulturalFeats.AseraiDesertFeat))
296:				{
297:					finalSpeed.AddFactor(-0.1f, _desert);
298:				}
299:				if (effectiveScout != null && effectiveScout.GetPerkValue(DefaultPerks.Scouting.DesertBorn))
300:				{

 succeeded in 504ms:
48:	public override float DailyBeingAtArmyInfluenceAward(MobileParty armyMemberParty)
49:	{
50:		float num = (armyMemberParty.Party.EstimatedStrength + 20f) / 200f;
51:		if (PartyBaseHelper.HasFeat(armyMemberParty.Party, DefaultCulturalFeats.EmpireArmyInfluenceFeat))
52:		{
53:			num += num * DefaultCulturalFeats.EmpireArmyInfluenceFeat.EffectBonus;
54:		}
55:		return num;
56:	}
57:
58:	public override int CalculatePartyInfluenceCost(MobileParty armyLeaderParty, MobileParty party)
59:	{
60:		if (armyLeaderParty.LeaderHero != null && party.LeaderHero != null && armyLeaderParty.LeaderHero.Clan == party.LeaderHero.Clan)
61:		{
62:			return 0;
63:		}
64:		float num = armyLeaderParty.LeaderHero.GetRelation(party.LeaderHero);
65:		float partySizeScore = GetPartySizeScore(party);
66:		float b = MathF.Round(party.Party.EstimatedStrength);

codex
For null-result consumption, the service methods all have direct null no-op guards, and wage resolves null to `WageFeatInputs.None`. I’m checking the wage modifier service implementation once so the `None` path is actually inert rather than only nominally safe.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/TroopProgression/WageModifierService.cs' | ForEach-Object { "'$i++; '"'{0}:{1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/TroopProgression/WageFeatInputs.cs' | ForEach-Object { "'$i++; '"'{0}:{1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main/Features/TroopProgression -Filter '*Wage*' | Select-Object -ExpandProperty Name" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main/Features/TroopProgression -Recurse -Include *.cs | Select-String -Pattern 'record.*WageFeatInputs|class WageFeatInputs|struct WageFeatInputs|WageFeatInputs.None|ApplyWageModifiers' | ForEach-Object { '{0}:{1}:{2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 483ms:
1:using TaleWorlds.CampaignSystem;
2:using TaleWorlds.Localization;
3:
4:namespace TAOM.Features.TroopProgression;
5:
6:/// <summary>
7:/// Concrete <see cref="IWageModifierService"/>. Pure — no TaleWorlds sealed-type
8:/// references in method bodies (only primitives + <c>ExplainedNumber</c>/<c>TextObject</c>
9:/// from <c>TaleWorlds.Core</c> which are open structs/types). Fully unit-testable
10:/// without a campaign session.
11:/// </summary>
12:public class WageModifierService : IWageModifierService
13:{
14:    private const int HighLevelHorseCost = 500;
15:    private const int LowLevelHorseCost = 150;
16:    private const int HighLevelHorseThreshold = 26;
17:    private readonly ITroopCostService _costService;
18:
19:    public WageModifierService(ITroopCostService costService)
20:    {
21:        _costService = costService;
22:    }
23:
24:    public void ApplyWageModifiers(
25:        ref ExplainedNumber result,
26:        in WageFeatInputs garrison,
27:        in WageFeatInputs party,
28:        float rohanMountedWageBonus,
29:        float mountedWageShare,
30:        TextObject? cultureText)
31:    {
32:        if (garrison.IsApplicable)
33:        {
34:            AddNonZero(ref result, garrison.EreborGarrisonBonus, cultureText);
35:            AddNonZero(ref result, garrison.LothlorienGarrisonBonus, cultureText);
36:            AddNonZero(ref result, garrison.IsengardGarrisonBonus, cultureText);
37:            AddNonZero(ref result, garrison.GondorGarrisonBonus, cultureText);
38:        }
39:
40:        if (party.IsApplicable)
41:        {
42:            AddNonZero(ref result, party.GundabadWageBonus, cultureText);
43:            AddNonZero(ref result, party.UmbarWageBonus, cultureText);
44:            AddNonZero(ref result, party.MordorWageBonus, cultureText);
45:
46:            // Rohan mounted-wage feat — scaled by mounted wage share (matches vanilla pattern).
47:            // Bonus is signed (negative for reduction). Skip cleanly if either side is zero.
48:            if (rohanMountedWageBonus != 0f && mountedWageShare > 0f)
49:                result.AddFactor(rohanMountedWageBonus * mountedWageShare, cultureText);
50:        }
51:    }
52:
53:    public int CalculateRecruitmentCost(
54:        int level,
55:        bool isMounted,
56:        bool isMercenary,
57:        bool withoutItemCost,
58:        in MountedCostFeatInputs mountedCostFeats,
59:        TextObject? cultureText,
60:        in RecruitmentPerkInputs buyerPerks = default)
61:    {
62:        int baseCost = _costService.GetTroopRecruitmentCost(level, isMercenary);
63:        var result = new ExplainedNumber(baseCost, includeDescriptions: false);
64:
65:        if (!withoutItemCost && isMounted)
66:            result.Add(CalculateHorseCost(level), null);
67:
68:        if (isMounted)
69:        {
70:            AddNonZero(ref result, mountedCostFeats.IsengardMountedCostBonus, cultureText);
71:            AddNonZero(ref result, mountedCostFeats.RohanMountedCostBonus, cultureText);
72:        }
73:
74:        // Vanilla buyer-hero recruitment-cost perk discounts. AddFactor accumulates linearly
75:        // (SumOfFactors += value), so a single summed apply is identical to vanilla's sequential
76:        // AddFactor calls. LimitMin(1f) mirrors vanilla's clamp inside the buyerHero != null block.
77:        if (buyerPerks.HasBuyer)
78:        {
79:            result.AddFactor(SumBuyerPerkFactors(in buyerPerks));
80:            result.LimitMin(1f);
81:        }
82:
83:        return (int)result.ResultNumber;
84:    }
85:
86:    private static float SumBuyerPerkFactors(in RecruitmentPerkInputs p)
87:    {
88:        float sum = 0f;
89:
90:        if (p.TierAtLeast2)
91:            sum += p.HeadHunterBonus;
92:
93:        if (p.IsInfantry)
94:        {
95:            sum += p.ChinkInTheArmorBonus;
96:            sum += p.ShowOfStrengthBonus;
97:            sum += p.HardyFrontlineBonus;
98:        }
99:        else if (p.IsRanged)
100:        {
101:            sum += p.RenownedArcherBonus;
102:            sum += p.PiercerBonus;
103:        }
104:
105:        if (p.IsPartyLeader)
106:            sum += p.FrugalBonus;
107:
108:        if (p.IsMercenary)
109:        {
110:            sum += p.SwordForBarterBonus;
111:            sum += p.SlickNegotiatorBonus;
112:        }
113:
114:        return sum;
115:    }
116:
117:    public int CalculateHorseCost(int troopLevel)
118:        => troopLevel >= HighLevelHorseThreshold ? HighLevelHorseCost : LowLevelHorseCost;
119:
120:    private static void AddNonZero(ref ExplainedNumber result, float bonus, TextObject? cultureText)
121:    {
122:        if (bonus != 0f)
123:            result.AddFactor(bonus, cultureText);
124:    }
125:}

 succeeded in 446ms:
IWageModifierService.cs
WageModifierService.cs

2026-06-15T16:07:59.096605Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.5 seconds
Output:
[31;1mGet-Content: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m $i=0; [36;1mGet-Content 'Main/Features/TroopProgression/WageFeatInputs.cs'[0m  …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot find path 'C:\Users\mikew\source\repos\TAOM\Main\Features\TroopProgression\WageFeatInputs.cs' because it does not exist.[0m

 exited 1 in 504ms:
[31;1mGet-Content: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m $i=0; [36;1mGet-Content 'Main/Features/TroopProgression/WageFeatInputs.cs'[0m  …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot find path 'C:\Users\mikew\source\repos\TAOM\Main\Features\TroopProgression\WageFeatInputs.cs' because it does not exist.[0m

 succeeded in 468ms:
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:60:_wageModifiers.ApplyWageModifiers(
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:86:return WageFeatInputs.None;
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:93:return WageFeatInputs.None;
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:106:return WageFeatInputs.None;
Main\Features\TroopProgression\IWageModifierService.cs:21:void ApplyWageModifiers(
Main\Features\TroopProgression\IWageModifierService.cs:54:public readonly struct WageFeatInputs
Main\Features\TroopProgression\IWageModifierService.cs:85:public static WageFeatInputs None => new WageFeatInputs(isApplicable: false);
Main\Features\TroopProgression\WageModifierService.cs:24:public void ApplyWageModifiers(

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/TroopProgression/IWageModifierService.cs' | ForEach-Object { "'$i++; if ($i -ge 1 -and $i -le 90) { '"'{0}:{1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 466ms:
1:using TaleWorlds.CampaignSystem;
2:using TaleWorlds.Localization;
3:
4:namespace TAOM.Features.TroopProgression;
5:
6:/// <summary>
7:/// Pure decision functions for party-wage and troop-recruitment cost modifiers,
8:/// extracted from <c>TaomPartyWageModel</c> per gamemodels.md rule 4 (thin override
9:/// body). The service operates on primitives only — the model is responsible for
10:/// translating sealed TaleWorlds types (CultureObject, FeatObject, CharacterObject)
11:/// at the boundary and resolving <c>HasFeat</c> + <c>EffectBonus</c> into the
12:/// supplied structs.
13:/// </summary>
14:public interface IWageModifierService
15:{
16:    /// <summary>
17:    /// Apply garrison-wage feats (when the party is an actual garrison) and party-wage
18:    /// feats (always for the owner culture, including the Rohan mounted-wage share)
19:    /// to <paramref name="result"/>.
20:    /// </summary>
21:    void ApplyWageModifiers(
22:        ref ExplainedNumber result,
23:        in WageFeatInputs garrison,
24:        in WageFeatInputs party,
25:        float rohanMountedWageBonus,
26:        float mountedWageShare,
27:        TextObject? cultureText);
28:
29:    /// <summary>
30:    /// Compute the full troop-recruitment cost including horse-cost (when mounted +
31:    /// !withoutItemCost), the Isengard/Rohan mounted-cost feats, and the buyer hero's
32:    /// vanilla recruitment-cost perk discounts. Returns the final <c>ResultNumber</c> as int.
33:    /// </summary>
34:    int CalculateRecruitmentCost(
35:        int level,
36:        bool isMounted,
37:        bool isMercenary,
38:        bool withoutItemCost,
39:        in MountedCostFeatInputs mountedCostFeats,
40:        TextObject? cultureText,
41:        in RecruitmentPerkInputs buyerPerks = default);
42:
43:    /// <summary>Horse-cost lookup: 500 for level &gt;= 26, else 150.</summary>
44:    int CalculateHorseCost(int troopLevel);
45:}
46:
47:/// <summary>
48:/// Pre-resolved feat-bonus inputs for a single AddFactor pass. Each pair is
49:/// (apply, bonus): when <c>apply</c> is true the bonus is applied with AddFactor.
50:/// The model resolves <c>CultureObject.HasFeat(feat) ? feat.EffectBonus : 0f</c>
51:/// into these primitives at the boundary, keeping the service free of TaleWorlds
52:/// sealed types per ADR-007.
53:/// </summary>
54:public readonly struct WageFeatInputs
55:{
56:    public readonly bool IsApplicable;
57:    public readonly float EreborGarrisonBonus;
58:    public readonly float LothlorienGarrisonBonus;
59:    public readonly float IsengardGarrisonBonus;
60:    public readonly float GondorGarrisonBonus;
61:    public readonly float GundabadWageBonus;
62:    public readonly float UmbarWageBonus;
63:    public readonly float MordorWageBonus;
64:
65:    public WageFeatInputs(
66:        bool isApplicable,
67:        float ereborGarrisonBonus = 0f,
68:        float lothlorienGarrisonBonus = 0f,
69:        float isengardGarrisonBonus = 0f,
70:        float gondorGarrisonBonus = 0f,
71:        float gundabadWageBonus = 0f,
72:        float umbarWageBonus = 0f,
73:        float mordorWageBonus = 0f)
74:    {
75:        IsApplicable = isApplicable;
76:        EreborGarrisonBonus = ereborGarrisonBonus;
77:        LothlorienGarrisonBonus = lothlorienGarrisonBonus;
78:        IsengardGarrisonBonus = isengardGarrisonBonus;
79:        GondorGarrisonBonus = gondorGarrisonBonus;
80:        GundabadWageBonus = gundabadWageBonus;
81:        UmbarWageBonus = umbarWageBonus;
82:        MordorWageBonus = mordorWageBonus;
83:    }
84:
85:    public static WageFeatInputs None => new WageFeatInputs(isApplicable: false);
86:}
87:
88:/// <summary>
89:/// Pre-resolved mounted-recruit-cost feat bonuses. Zero means the feat does not apply
90:/// (either the buyer's culture lacks the feat, or the troop is not mounted).

codex
**Known Suspects**
1. DISPUTED as worded. For valid engine mobile parties, the chain is safe, including the faction-less mobile-party case. But not for literally “ANY PartyBase state”: `party.Owner?.Culture` still invokes computed `PartyBase.Owner`, and vanilla `Owner` derefs `Settlement.Owner` when `IsMobile == false`. Evidence: `CultureFeatAdapter.cs:70-73`; decompiled `PartyBase.cs:189-200`, `:126`.
2. CONFIRMED. Vanilla order is `LeaderHero -> party.Culture -> Owner -> Settlement` in `PartyBaseHelper.HasFeat` (`PartyBaseHelper.cs:373-395`). `party.MapFaction?.Culture` is the null-safe equivalent of `party.Culture` when `MapFaction` is non-null because `PartyBase.Culture => MapFaction.Culture` (`PartyBase.cs:255`).
3. CONFIRMED. `MapEventSide.LeaderParty` is `PartyBase` (`MapEventSide.cs:93-94`), so `FromOrNull(attackerSide?.LeaderParty)` binds the `PartyBase?` overload.
4. CONFIRMED. `TaomPartyWageModel.cs:91` correctly keeps garrison culture settlement-owner-scoped. Per-hero passives at `TaomBattleRewardModel.cs:38`, `TaomRaidModel.cs:33`, and `TaomPartyWageModel.cs:63` are not culture-feat sites and were correctly not migrated.
5. CONFIRMED. Cultural-feat service methods no-op on null, e.g. `CulturalFeatsService.cs:42-45`, `:55-58`, `:219-222`, `:467-470`. Wage null paths return `WageFeatInputs.None` (`TaomPartyWageModel.cs:103-116`; `IWageModifierService.cs:85`), and `BonusIfHas` is null-safe (`TaomPartyWageModel.cs:175-176`).
6. DISPUTED for overall remaining crash exposure. Direct `party.Culture` calls in `Main/` are clean, but several TAOM overrides still call vanilla base methods that invoke `PartyBaseHelper.HasFeat`, which still calls throwing `party.Culture`.

**Findings**
[HIGH] Main/Features/CulturalFeats/Models/TaomArmyManagementModel.cs:28 — Null-safety / base call — `base.DailyBeingAtArmyInfluenceAward` still calls vanilla `PartyBaseHelper.HasFeat(armyMemberParty.Party, ...)` (`DefaultArmyManagementCalculationModel.cs:48-55`), which reaches `party.Culture` (`PartyBaseHelper.cs:379-385`; `PartyBase.cs:255`) and can still NRE on the same `LeaderHero == null && MapFaction == null` shape. Fix: replace the base call with a local equivalent that uses the null-safe resolver for the vanilla Empire feat check.

[HIGH] Main/Features/CulturalFeats/Models/TaomBattleRewardModel.cs:28 — Null-safety / base call — `base.CalculateRenownGain` still calls `PartyBaseHelper.HasFeat(winnerParty, DefaultCulturalFeats.VlandianRenownMercenaryFeat)` (`DefaultBattleRewardModel.cs:48-68`) before TAOM reaches `CultureFeatAdapter.FromOrNull(winnerParty)` at line 37. Fix: inline the vanilla calculation and route that vanilla feat check through the same null-safe party-culture resolver.

[MEDIUM] Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs:24 — Remaining exposure / sibling model — `base.CalculateFinalSpeed` still calls `PartyBaseHelper.HasFeat(mobileParty.Party, ...)` for forest/desert cultural effects (`DefaultPartySpeedCalculatingModel.cs:279-295`). This is pre-existing, but it contradicts the “all party-culture feat models are protected by the chokepoint” closure claim. Fix: audit/copy the vanilla speed cultural checks with null-safe resolution.

[MEDIUM] Main/Features/CulturalFeats/Models/TaomPartyTroopUpgradeModel.cs:24 — Remaining exposure / sibling model — `base.GetGoldCostForUpgrade` still calls `PartyBaseHelper.HasFeat(party, DefaultCulturalFeats.KhuzaitRecruitUpgradeFeat)` (`DefaultPartyTroopUpgradeModel.cs:91-93`). Fix: preserve vanilla behavior with a null-safe resolver before applying TAOM upgrade feats.

[LOW] Main/Features/TroopProgression/IWageModifierService.cs:17 — Documentation drift — comment still says party-wage feats are “always for the owner culture,” but `TaomPartyWageModel.cs:55` now resolves party wage culture via `ResolvePartyCulture`. Fix: update the comment to leader-first party-culture resolution, with garrison remaining fief-owner-scoped.

**Design Question**
Leader-first party wage attribution is defensible. A cross-culture leader/owner party is the only behavior shift, and vanilla party feat semantics consistently prefer `LeaderHero` before party faction/owner. If the design were explicitly “the clan treasury pays wages,” owner-culture would be defensible, but that would make wage the odd party-culture model out. Consistency is the better call here.

CRITICAL: 0 | HIGH: 2 | MEDIUM: 2 | LOW: 1  
VERDICT: ISSUES FOUND
SUCCESS: The process with PID 15492 (child process of PID 29168) has been terminated.
SUCCESS: The process with PID 29168 (child process of PID 44084) has been terminated.
SUCCESS: The process with PID 44084 (child process of PID 59124) has been terminated.
SUCCESS: The process with PID 59124 (child process of PID 19628) has been terminated.
tokens used
132,360
**Known Suspects**
1. DISPUTED as worded. For valid engine mobile parties, the chain is safe, including the faction-less mobile-party case. But not for literally “ANY PartyBase state”: `party.Owner?.Culture` still invokes computed `PartyBase.Owner`, and vanilla `Owner` derefs `Settlement.Owner` when `IsMobile == false`. Evidence: `CultureFeatAdapter.cs:70-73`; decompiled `PartyBase.cs:189-200`, `:126`.
2. CONFIRMED. Vanilla order is `LeaderHero -> party.Culture -> Owner -> Settlement` in `PartyBaseHelper.HasFeat` (`PartyBaseHelper.cs:373-395`). `party.MapFaction?.Culture` is the null-safe equivalent of `party.Culture` when `MapFaction` is non-null because `PartyBase.Culture => MapFaction.Culture` (`PartyBase.cs:255`).
3. CONFIRMED. `MapEventSide.LeaderParty` is `PartyBase` (`MapEventSide.cs:93-94`), so `FromOrNull(attackerSide?.LeaderParty)` binds the `PartyBase?` overload.
4. CONFIRMED. `TaomPartyWageModel.cs:91` correctly keeps garrison culture settlement-owner-scoped. Per-hero passives at `TaomBattleRewardModel.cs:38`, `TaomRaidModel.cs:33`, and `TaomPartyWageModel.cs:63` are not culture-feat sites and were correctly not migrated.
5. CONFIRMED. Cultural-feat service methods no-op on null, e.g. `CulturalFeatsService.cs:42-45`, `:55-58`, `:219-222`, `:467-470`. Wage null paths return `WageFeatInputs.None` (`TaomPartyWageModel.cs:103-116`; `IWageModifierService.cs:85`), and `BonusIfHas` is null-safe (`TaomPartyWageModel.cs:175-176`).
6. DISPUTED for overall remaining crash exposure. Direct `party.Culture` calls in `Main/` are clean, but several TAOM overrides still call vanilla base methods that invoke `PartyBaseHelper.HasFeat`, which still calls throwing `party.Culture`.

**Findings**
[HIGH] Main/Features/CulturalFeats/Models/TaomArmyManagementModel.cs:28 — Null-safety / base call — `base.DailyBeingAtArmyInfluenceAward` still calls vanilla `PartyBaseHelper.HasFeat(armyMemberParty.Party, ...)` (`DefaultArmyManagementCalculationModel.cs:48-55`), which reaches `party.Culture` (`PartyBaseHelper.cs:379-385`; `PartyBase.cs:255`) and can still NRE on the same `LeaderHero == null && MapFaction == null` shape. Fix: replace the base call with a local equivalent that uses the null-safe resolver for the vanilla Empire feat check.

[HIGH] Main/Features/CulturalFeats/Models/TaomBattleRewardModel.cs:28 — Null-safety / base call — `base.CalculateRenownGain` still calls `PartyBaseHelper.HasFeat(winnerParty, DefaultCulturalFeats.VlandianRenownMercenaryFeat)` (`DefaultBattleRewardModel.cs:48-68`) before TAOM reaches `CultureFeatAdapter.FromOrNull(winnerParty)` at line 37. Fix: inline the vanilla calculation and route that vanilla feat check through the same null-safe party-culture resolver.

[MEDIUM] Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs:24 — Remaining exposure / sibling model — `base.CalculateFinalSpeed` still calls `PartyBaseHelper.HasFeat(mobileParty.Party, ...)` for forest/desert cultural effects (`DefaultPartySpeedCalculatingModel.cs:279-295`). This is pre-existing, but it contradicts the “all party-culture feat models are protected by the chokepoint” closure claim. Fix: audit/copy the vanilla speed cultural checks with null-safe resolution.

[MEDIUM] Main/Features/CulturalFeats/Models/TaomPartyTroopUpgradeModel.cs:24 — Remaining exposure / sibling model — `base.GetGoldCostForUpgrade` still calls `PartyBaseHelper.HasFeat(party, DefaultCulturalFeats.KhuzaitRecruitUpgradeFeat)` (`DefaultPartyTroopUpgradeModel.cs:91-93`). Fix: preserve vanilla behavior with a null-safe resolver before applying TAOM upgrade feats.

[LOW] Main/Features/TroopProgression/IWageModifierService.cs:17 — Documentation drift — comment still says party-wage feats are “always for the owner culture,” but `TaomPartyWageModel.cs:55` now resolves party wage culture via `ResolvePartyCulture`. Fix: update the comment to leader-first party-culture resolution, with garrison remaining fief-owner-scoped.

**Design Question**
Leader-first party wage attribution is defensible. A cross-culture leader/owner party is the only behavior shift, and vanilla party feat semantics consistently prefer `LeaderHero` before party faction/owner. If the design were explicitly “the clan treasury pays wages,” owner-culture would be defensible, but that would make wage the odd party-culture model out. Consistency is the better call here.

CRITICAL: 0 | HIGH: 2 | MEDIUM: 2 | LOW: 1  
VERDICT: ISSUES FOUND

TAOM is a Lord of the Rings total-conversion mod for Mount & Blade II: Bannerlord (.NET Framework 4.7.2). You are reviewing a small, isolated change on branch bannerlord-1.5.x, uncommitted in the working tree.

## What changed

The tournament bet cap is an MCM (Mod Configuration Menu) setting. Its default was 0 (meaning unlimited, resolved by TournamentRewardRules.MaximumBet to a 50,000,000 safety ceiling). The maintainer asked to change the default to 15,000 while keeping the player's ability to set it back to 0 for unlimited, or raise it to the slider's max of 1,000,000.

MCM persists each setting by property name and loads the saved value over the compiled default on every subsequent run. A bare default-value edit under the same property name would therefore never reach any player who already has TAOM installed (every existing player already has 0 saved under the old name). The project's own documented rule for this (`.claude/rules/config-providers.md` "Doc requirement", mirrored in `docs/ai-includes/orientation.md` trap index as "Persisted MCM defaults") is: rename the property so the new default reaches existing installs too, and have the HintText tell a player who already saved a value that it was reset.

4 files changed, no other files touched:

```
diff --git a/Main/Features/TaomSettings.cs b/Main/Features/TaomSettings.cs
index e2302e268..f47f48a6b 100644
--- a/Main/Features/TaomSettings.cs
+++ b/Main/Features/TaomSettings.cs
@@ -178,10 +178,12 @@ public class TaomSettings : AttributeGlobalSettings<TaomSettings>

     // --- Tournaments ---

+    // Renamed from TournamentMaxBetPerRound (Mike, 2026-11-10): default goes from 0 (unlimited) to 15,000, and
+    // MCM loads a saved value by name, so a player who ever saved any value under the old name would have kept it.
     [SettingPropertyGroup("Tournaments")]
     [SettingPropertyInteger("Max Bet Per Round", 0, TAOM.Features.TournamentRewards.TournamentRewardRules.MaxBetSetting, Order = 0, RequireRestart = false,
-        HintText = "The most you may bet in each tournament round. 0 = unlimited (up to your purse). Roguery's Deep Pockets doubles any other value. Vanilla is 150. Default: 0.")]
-    public int TournamentMaxBetPerRound { get; set; } = 0;
+        HintText = "The most you may bet in each tournament round. 0 = unlimited (up to your purse); raise this slider for a bigger cap, or back to 0 to remove it. Roguery's Deep Pockets doubles any other value. Vanilla is 150. Default: 15,000. Saved values from before 2026-11-10 were reset to this default; if you had chosen your own, set it again.")]
+    public int TournamentBetCapPerRound { get; set; } = 15_000;

     [SettingPropertyGroup("Tournaments")]
     [SettingPropertyFloatingInteger("Renown Multiplier", 0.0f, TAOM.Features.TournamentRewards.TournamentRewardRules.MaxMultiplier, "#0.00", Order = 1, RequireRestart = false,
diff --git a/Main/Features/TournamentRewards/TournamentRewardsSettingsProvider.cs b/Main/Features/TournamentRewards/TournamentRewardsSettingsProvider.cs
index d83791aa4..09917774b 100644
--- a/Main/Features/TournamentRewards/TournamentRewardsSettingsProvider.cs
+++ b/Main/Features/TournamentRewards/TournamentRewardsSettingsProvider.cs
@@ -29,7 +29,7 @@ public sealed class TournamentRewardsSettingsProvider : ITournamentRewardsSettin
     private TaomSettings? Settings => _settings ?? TaomSettings.Instance;

     public int MaxBetPerRound =>
-        SettingClamp.Clamp(Settings?.TournamentMaxBetPerRound, 0, 0, TournamentRewardRules.MaxBetSetting);
+        SettingClamp.Clamp(Settings?.TournamentBetCapPerRound, 15_000, 0, TournamentRewardRules.MaxBetSetting);

     public float RenownMultiplier =>
         SettingClamp.Clamp(Settings?.TournamentRenownMultiplier, 1f, 0f, TournamentRewardRules.MaxMultiplier);
diff --git a/TAOM.Tests/Features/TournamentRewards/TournamentRewardsSettingsProviderTests.cs b/TAOM.Tests/Features/TournamentRewards/TournamentRewardsSettingsProviderTests.cs
index 04ce242f1..1ba68fba2 100644
--- a/TAOM.Tests/Features/TournamentRewards/TournamentRewardsSettingsProviderTests.cs
+++ b/TAOM.Tests/Features/TournamentRewards/TournamentRewardsSettingsProviderTests.cs
@@ -22,17 +22,17 @@ public class TournamentRewardsSettingsProviderTests
         var provider = new TournamentRewardsSettingsProvider();
         var compiled = new TaomSettings();

-        Assert.AreEqual(compiled.TournamentMaxBetPerRound, provider.MaxBetPerRound);
+        Assert.AreEqual(compiled.TournamentBetCapPerRound, provider.MaxBetPerRound);
         Assert.AreEqual(compiled.TournamentRenownMultiplier, provider.RenownMultiplier);
         Assert.AreEqual(compiled.TournamentInfluenceMultiplier, provider.InfluenceMultiplier);
     }

     [TestMethod]
-    public void CompiledDefaults_AreUnlimitedBetsAndTheDesignedMultipliers()
+    public void CompiledDefaults_AreTheDesignedBetCapAndMultipliers()
     {
         var compiled = new TaomSettings();

-        Assert.AreEqual(0, compiled.TournamentMaxBetPerRound, "0 = unlimited");
+        Assert.AreEqual(15_000, compiled.TournamentBetCapPerRound, "15,000; 0 would mean unlimited");
         Assert.AreEqual(1.0f, compiled.TournamentRenownMultiplier);
         Assert.AreEqual(1.0f, compiled.TournamentInfluenceMultiplier);
     }
@@ -60,7 +60,7 @@ public class TournamentRewardsSettingsProviderTests
     [TestMethod]
     public void MaxBetSlider_SpansExactlyTheProvidersClamp()
     {
-        var (min, max) = SliderRange(nameof(TaomSettings.TournamentMaxBetPerRound));
+        var (min, max) = SliderRange(nameof(TaomSettings.TournamentBetCapPerRound));

         Assert.AreEqual(0.0, min);
         Assert.AreEqual((double)TournamentRewardRules.MaxBetSetting, max);
@@ -102,7 +102,7 @@ public class TournamentRewardsSettingsProviderTests
     [DataRow(500, 500)]
     public void MaxBetPerRound_McmValueOutsideTheSlider_IsClamped(int mcm, int expected)
     {
-        var provider = new TournamentRewardsSettingsProvider(new TaomSettings { TournamentMaxBetPerRound = mcm });
+        var provider = new TournamentRewardsSettingsProvider(new TaomSettings { TournamentBetCapPerRound = mcm });

         Assert.AreEqual(expected, provider.MaxBetPerRound);
     }
diff --git a/docs/features/tournament-rewards.md b/docs/features/tournament-rewards.md
index 39cd443e7..86db4386e 100644
--- a/docs/features/tournament-rewards.md
+++ b/docs/features/tournament-rewards.md
@@ -2,8 +2,8 @@

 ## Overview

-Tournaments pay more, and the player chooses how. A **bet cap** set in MCM (unlimited by default) replaces
-vanilla's 150 per round. A winner's **renown and influence** grow with the lords and heroes in the field and with
+Tournaments pay more, and the player chooses how. A **bet cap** set in MCM (15,000 by default, or unlimited)
+replaces vanilla's 150 per round. A winner's **renown and influence** grow with the lords and heroes in the field and with
 the winner's culture. At **Join**, the player picks one of up to **three prizes** and the **combat skill** the
 tournament trains, earning experience in it for every round won. Designed with Mike on 2026-10-02.

@@ -105,14 +105,15 @@ Patch96 join choices -> TournamentJoinService -> ITournamentJoinAdapter (pri

 | Setting | Default | Meaning |
 |---|---|---|
-| Max Bet Per Round | 0 | 0 = unlimited (up to the purse); vanilla is 150 |
+| Max Bet Per Round | 15,000 | 0 = unlimited (up to the purse); vanilla is 150 |
 | Renown Multiplier | 1.00 | scales every winner's renown |
 | Influence Multiplier | 1.00 | scales every winner's influence in their own kingdom's towns |
 | Keep Enemy Sides Out | on | owned by the Arena feature: see [tournament-alignment-filter.md](tournament-alignment-filter.md) |
 | Tidings of the Lists | on | owned by Settlement Services: see [settlement-services.md](settlement-services.md) |

 MCM keeps a value once saved, so a changed default never reaches an existing install: rename the property to
-change one ([orientation trap](../ai-includes/orientation.md), "Persisted MCM defaults").
+change one ([orientation trap](../ai-includes/orientation.md), "Persisted MCM defaults"). The bet cap did this on
+2026-11-10: `TournamentMaxBetPerRound` (default 0) became `TournamentBetCapPerRound` (default 15,000).

 The multipliers run from 0 to 5 (`TournamentRewardRules.MaxMultiplier`) and the bet cap from 0 to 1,000,000. MCM's
 slider enforces that range but its settings file loader does not, so `TournamentRewardsSettingsProvider` clamps a
@@ -202,6 +203,9 @@ row. A bad value reverts with a warning; read once per process.

 ## Changelog

+- 2026-11-10: default bet cap per round changed from 0 (unlimited) to 15,000 (Mike); the player may still raise
+  the MCM slider to 1,000,000 or set it back to 0 for unlimited. The MCM property was renamed
+  `TournamentMaxBetPerRound` to `TournamentBetCapPerRound` so the new default reaches existing installs too.
 - 2026-10-05: checked against Bannerlord v1.5.4: every engine member it overrides, patches, reflects on or subscribes
   to is unchanged from v1.5.3 (decompile diff of both builds, and the strict binding gate passes on v1.5.4), and so is
   the engine code behind every behaviour this doc cites. No code change.
```

Neither Main/Features/TournamentRewards/Hooks/Patch96_TournamentRewards.cs nor Main/Features/TournamentRewards/TournamentRewardRules.cs was touched. `TournamentRewardRules.MaximumBet(int vanillaResult, int configuredCap)` (unchanged) is: `if (configuredCap <= 0) return BetCeiling(50_000_000); var perkFactor = Max(1, vanillaResult / 150); return Min(BetCeiling, configuredCap * perkFactor);`

`ITournamentRewardsSettingsProvider.MaxBetPerRound` (the interface member name) was NOT renamed, only the backing MCM property `TaomSettings.TournamentMaxBetPerRound` -> `TaomSettings.TournamentBetCapPerRound`.

Already confirmed by the implementing session (re-verify these claims, do not take them on faith):
- Full repo grep for `TournamentMaxBetPerRound` finds it only in the rename comment (TaomSettings.cs) and the changelog/doc line above (tournament-rewards.md) -- no other `.cs`, test, or shipped-config file still references the old name.
- `dotnet test` filtered to `TournamentRewards`: 165/165 passed, including the updated assertions above.
- Full suite (`dotnet test TAOM.Tests`): 38,028/38,037 passed, 0 failed, 9 skipped (unrelated to this change).
- `python tools/check_mcm_default_flips.py`: OK, no MCM default changed under an unchanged name (compares the working tree against every reachable release tag back to v2.0.x) -- confirms this rename does not collide with any value ever shipped under the new name `TournamentBetCapPerRound` on an earlier tag.
- `python tools/lint_docs.py --fail-on-drift`: 0 config-example drift, 0 em/en dashes in new lines, 0 version mismatches.
- `python tools/check_provenance.py`: 0 misses.

## Known Suspects -- CONFIRM or DISPUTE each with evidence (file:line or decompile)

1. **Fallback-vs-compiled-default drift.** `TournamentRewardsSettingsProvider.MaxBetPerRound` now hardcodes `15_000` as its no-MCM fallback (used when `TaomSettings.Instance` is null, e.g. a unit test or a context where MCM hasn't initialized). Confirm this literal is kept in lockstep with `TaomSettings.TournamentBetCapPerRound`'s compiled default only by convention/test (`Fallbacks_MatchTheCompiledDefaults`), not by a shared constant -- so a future edit to one without the other silently reintroduces exactly the bug this change just fixed (the fallback going stale was in fact the first failure this session hit and had to fix). Is a shared named constant (e.g. on `TournamentRewardRules`) warranted here instead of two independent literals, or is the existing test sufficient given the `config-providers.md` precedent (other renamed settings in this file use the same two-literal pattern)?

2. **MCM clamp bounds unchanged.** The `[SettingPropertyInteger]` attribute's min/max (`0`, `TournamentRewardRules.MaxBetSetting` = 1,000,000) are unchanged; only the compiled default literal moved from the property's `= 0` to `= 15_000` inside that unchanged range. Confirm 15,000 is within [0, 1,000,000] (trivially true) and that no other code path (Patch96, TournamentRewardRules, the arena/prize system) reads `TournamentMaxBetPerRound` reflectively (by string name, not `nameof`) in a way the renamed property would silently miss -- e.g. grep for `"TournamentMaxBetPerRound"` as a bare string literal anywhere (not `nameof`), and for any `GetProperty("TournamentMaxBetPerRound")`-style reflection.

3. **Deep Pockets perk interaction at the new default.** HintText says "Roguery's Deep Pockets doubles any other value." At the new default of 15,000 (previously this sentence only mattered for a player-chosen non-zero value, since 0 was default and meant unlimited), confirm `TournamentRewardRules.MaximumBet`'s `perkFactor = Max(1, vanillaResult/150)` still produces the doubling Deep Pockets implies when `vanillaResult` is vanilla's own perk-adjusted 300 (150*2): `perkFactor = 300/150 = 2`, so `15_000 * 2 = 30_000`, clamped to the 50,000,000 ceiling -- i.e. unaffected by this change, but confirm no off-by-one or truncation issue specific to 15,000 vs the old 0 path (the 0 path takes an entirely different branch, `configuredCap <= 0`, so it never exercised the perk-multiply branch at all; the rename exercises it for every player who does not change the slider, which is new exposure, not new code).

4. **MCM UI text mismatch at default.** The feature doc (`docs/features/tournament-rewards.md` "The 50,000,000 ceiling") says vanilla's own bet-screen text ("you can wager {MAX_AMOUNT} each time") shows 50,000,000 when the setting is at its default. That sentence was written when the default was 0 (unlimited -> 50M ceiling). At the NEW default of 15,000, vanilla's own text would instead show 15,000 (or 30,000 with Deep Pockets) as the per-round limit, not 50,000,000. Confirm docs/features/tournament-rewards.md was NOT updated to fix this now-stale sentence (the diff above only touches the Overview, the Configuration table, the rename note and the changelog -- the "50,000,000 ceiling" section's prose is unchanged) -- this is a plausible doc-drift MEDIUM: the default changed but a prose sentence describing behavior "at the default" was not re-checked against the new default.

5. **Comment/HintText date claim.** The rename comment and HintText both assert "Saved values from before 2026-11-10 were reset to this default" / "(Mike, 2026-11-10)". Confirm this is an accurate, verifiable claim given the diff is uncommitted at review time (i.e. the actual commit date may differ from 2026-11-10) -- if the commit lands on a different date, is the hardcoded date in the HintText (player-facing text) and the code comment still correct, or should it reference something more durable (a version number, an issue number) the way the precedent rename (`BanditPartiesPerHideoutCap`, "#1017") did? The bandit precedent cited an issue number; this rename cites only a date and "(Mike, ...)" with no issue number -- is that a meaningful inconsistency with house style for this category of change?

6. **Test coverage for the fallback default itself.** `MaxBetPerRound_McmValueOutsideTheSlider_IsClamped` tests out-of-range MCM values (`int.MaxValue`, `-5`, `500`) against the provider's CLAMP bounds, but does any test exercise the provider's constructor-with-no-settings path (`new TournamentRewardsSettingsProvider()`) against a scenario where `TaomSettings.Instance` is genuinely null (not just the compiled-defaults-equality test, which uses a fresh `TaomSettings` instance, not a null `Instance`)? If `TaomSettings.Instance` can be null in a real game context (not just unit tests), confirm the `15_000` fallback path is actually reachable and tested in that specific state, not just inferred from the compiled-default-equality test.

## What NOT to flag

- Don't re-litigate the rename-vs-flip decision itself; it is mandated by this repo's own `config-providers.md` rule and confirmed correct by `check_mcm_default_flips.py` passing clean against all 27 reachable release tags.
- Don't propose localizing the HintText strings; MCM HintText in this codebase is plain English (not `{=KEY}`-wrapped), consistent with every other setting in `TaomSettings.cs`.
- Don't flag the unrelated files showing as modified/untracked elsewhere in the working tree (a separate retopology-toolkit effort, review docs for other features); they are explicitly out of scope for this review and will not be committed with this change.

Report using your standard P1/P2/P3 severity format, citing exact `file:line` for every claim, and flag disagreement with the "Already confirmed" list above explicitly if you find any of it to be wrong.

# Adversarial review — TAOM co-op interop (#370)

You are an independent adversarial reviewer. Assume the changeset is wrong and try to prove it.
Repo root is the working directory. All changes are UNCOMMITTED — use
`git diff` and `git ls-files --others --exclude-standard` to see them. Target engine is
**Bannerlord v1.4.7**; verify every TaleWorlds signature against the INSTALLED DLLs at
`E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/`, never from
memory. A decompiled cache exists at `C:/Users/mikew/.taom-src/v1.4.7/`.

## What this changeset does

TAOM is a Bannerlord total conversion. Two third-party co-op mods can now run alongside it —
BannerlordTogether (launcher id `BannerlordTogether`) and BannerlordCoop (launcher id `Coop`). Both
are host-authoritative: one peer simulates, the others follow.

The changeset makes TAOM stand down from decisions that could evaluate differently on two peers,
because the failure mode is not a crash — it is two campaigns that silently disagree and two saves
that are both wrong.

Two distinct gates exist and conflating them is a likely source of bugs:

- `ICoopPresenceProvider.IsCoopActive` — **process-constant**: "is a co-op module enabled". Backed
  by `CoopPresence`, which reads the launcher module list by reflection and **fails closed**.
- `ICoopSessionProvider.IsAuthority` — **session-varying**: "do I own the simulation". Backed by
  reflection into BannerlordCoop. **Fails OPEN to singleplayer** (`!sessionActive || isServer`).

Read `docs/features/coop-interop.md` and `docs/features/bannerlord-together-compat.md` first — they
state the intended contract. Read `docs/reviews/rca-coop-veto-surface-2026-08-01.md`: an internal
review already found that the first pass gated one call site of a rule that had four.

## Focus areas, in priority order

1. **Did the veto fix miss any remaining path?** The rules are `IDiplomacyService.IsWarAllowed`,
   `IWarOfTheRingService.ShouldBlockPeace`, `IDiplomacyService.IsAllianceDecisionAllowed`. Four
   consumers are now gated (`AllianceActionHook`, `PeaceActionHook`,
   `TaomKingdomDecisionPermissionModel`, `TaomDiplomacyModel`). **Find a fifth**, or any other TAOM
   code path that can veto/alter a replicated campaign mutation using state a co-op host does not
   replicate. TAOM `SyncData` keys are the smoking gun — grep for `dataStore.SyncData`.

2. **The siege split.** `SiegeDefenseService.OnHourlyTick` was split into `OnHourlyTickShared`
   (authority-only) and `OnHourlyTickLocalPlayer` (every peer). Attack it: does the "local" half
   mutate anything shared? It sets `RewardClaimed` on an entry in `_activeEvents`, which is
   serialised into the `_taom_siege_active_events` save key. Is the claim that each peer keeps its
   own TAOM `SyncData` after join actually true, and does the split hold if it is not? Can a client
   double-claim, or claim a reward it did not earn? What happens to entries the client never expires?

3. **Gate-type confusion.** Every site using `IsCoopActive` where `IsAuthority` was correct, or vice
   versa. Consider especially: `IsAuthority` fails OPEN, so using it where BannerlordTogether (not
   Coop) is the active mod yields `true` on BOTH peers — is any gate relying on it in a way that
   silently does nothing under BT?

4. **UI-registration timing.** `Main/SubModule.cs` → `RegisterUiExtensions` calls
   `CoopPresence.Refresh()` then reads `IsActive`, during Main's `OnSubModuleLoad`. Is
   `TaleWorlds.ModuleManager.ModuleHelper.GetActiveModules()` actually populated at that point on
   v1.4.7? Decompile the launcher/module load path and answer definitively. If it is not, the
   `[CoopSuppressedUi]` suppression silently never fires. This is the single item the team could not
   settle from source.

5. **`CoopUiRegistrationPolicy`** re-implements UIExtenderEx's own type scan. Can it ever select a
   different set than `UIExtender.Register(Assembly)` would, for a type that matters? The vendored
   source is at `Dependencies/.vendor-source/Bannerlord.UIExtenderEx-2.13.2/`.

6. **`CoopPresencePolicy` / `coop-force-active.flag`.** The flag must only ever ADD presence, never
   remove it. Prove or disprove. Check the interaction with `coop-modules.txt` union-only parsing.

7. **The regression test itself.** `TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs`
   scans source text to enforce that veto sites consider co-op. Find inputs where its regexes
   produce a false negative — a real ungated consumer it would not catch. It already had two such
   bugs (reflection missing engine-coupled types; file-name keying collapsing three classes in one
   file). Assume there is a third.

## Rules

- Verify before asserting. Cite `file:line` and paste the code you are judging.
- Distinguish CONFIRMED (you read the code and it is wrong) from SUSPECTED.
- Severity P1/P2/P3, and say what the player-visible symptom is.
- Solo play must be byte-identical to before this changeset. Any solo regression is automatically
  P1 — check that specifically, it is the project's hard constraint.
- Do not propose refactors for taste. Only defects, with the minimum fix.
- If you find nothing in a focus area, say so explicitly rather than padding.

Output: findings ordered by severity, then a one-paragraph verdict on whether this is safe to
commit given that NO live two-peer session has ever been run.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->

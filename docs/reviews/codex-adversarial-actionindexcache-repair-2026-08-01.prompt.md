# Adversarial review: ActionIndexCache static-index repair (commit 19ec0e1e)

You are reviewing a fix that writes to VANILLA ENGINE STATIC FIELDS by reflection. Getting this
wrong corrupts animation state for every character in the game, silently. Be maximally adversarial.
Assume the fix is wrong until the code proves otherwise.

## The bug being fixed

Players reported characters rendering flat on their back (skeleton bind pose) in every UI tableau --
Character Customization, the inventory doll, the encyclopedia. All races. New campaigns. INTERMITTENT
per launch. Never reproduced on the dev machine.

Diagnosed mechanism: `TaleWorlds.MountAndBlade.ActionIndexCache` declares 215 `public static readonly
ActionIndexCache` fields populated by an EXPLICIT static constructor, each via `Create(name)` ->
`MBAnimation.GetActionCodeWithName(name)`. An explicit cctor means the type is NOT `beforefieldinit`,
so ANY static member access (field read OR the `Create` method) forces the whole table to initialise
at that instant. If that happens before the engine has loaded action types, every index bakes to -1
for the process lifetime. The fields are `readonly`, so the cctor never re-runs.

Vanilla `CharacterTableau.GetIdleAction()` returns `ActionIndexCache.act_inventory_idle_start` when
`_idleAction == act_none` (the default). `SetAction(-1)` is a no-op -> bind pose.

## The fix

`Main/Features/HeroRace/ActionIndexCacheRepair.cs` enumerates those static fields, and for each one
that reads -1, re-resolves it via a live `MBAnimation.GetActionCodeWithName` lookup and writes the
value back with `FieldInfo.SetValue`.

## READ FIRST

- `Main/Features/HeroRace/ActionIndexCacheRepair.cs` -- the fix. Read every line.
- `Main/Features/HeroRace/Diagnostics/TableauDiagnostics.cs` -- instrumentation + throttling
- `Main/Features/HeroRace/Hooks/CharacterTableau_RefreshCharacterTableau_Patch.cs` -- primary call site
- `Main/Features/HeroRace/CharacterSpawnerService.cs` -- secondary call site
- `Main/SubModule.cs` -- search for `ActionIndexCacheRepair` and `previewCategory`
- `TAOM.Tests/Features/HeroRace/ActionIndexCacheRepairTests.cs`
- `docs/reviews/rca-prone-character-tableau-2026-07-31.md` -- the addendum section

## VANILLA CODE -- decompile and paste these as code blocks in your review

Installed DLLs are authoritative. `ActionIndexCache`, `MBAnimation`, `MBActionSet`, `MBGlobals` are in
`E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll`.
`CharacterTableau`, `CharacterSpawner`, `BodyGeneratorView` are NOT there -- they live in
`E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Native/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.View.dll`.
Cached decompiles: `C:/Users/mikew/.taom-src/v1.4.7/`.

1. `ActionIndexCache` -- the full static constructor, the private `(string)` ctor, `Create`, `GetName`, `Index`.
2. `MBAnimation.GetActionCodeWithName` and `GetNumActionCodes`.
3. `CharacterTableau.GetIdleAction` and every call site of it inside `CharacterTableau`.
4. `MBGlobals.GetActionSet` and `MBActionSet.GetActionSet` -- confirm which throws and which does not.

## KNOWN SUSPECTS -- CONFIRM or DISPUTE each, with evidence

**S1. The round-trip check may silently disable the entire repair.**
Before writing, the code does:
```
var candidate = ActionIndexCache.Create(actionName);
string roundTrip = candidate.GetName();
if (candidate.Index < 0 || !string.Equals(roundTrip, actionName, StringComparison.Ordinal)) -> skip
```
`GetName()` calls `MBAPI.IMBAnimation.GetActionNameWithCode(Index)`. If the native layer returns a
name that differs in ANY way from the input (different case, an alias, a canonical form, a trailing
suffix, or the FIRST name registered for an index shared by several aliases), then the equality fails
for EVERY field and the repair writes NOTHING while reporting "name-mismatched". That would be a
silent total failure of the fix. Determine what `GetActionNameWithCode` actually returns relative to
the name passed to `GetActionCodeWithName`. If you cannot prove they round-trip exactly, say so
loudly -- this is the single highest-risk line in the changeset.

**S2. `FieldInfo.SetValue` on an `initonly` static field may not work, or may not be observed.**
Target is `net472`. Confirm: (a) does the CLR permit it, (b) is the write visible to code that
already JITted a read of that field, (c) does anything about `ActionIndexCache` being a
`readonly struct` change the answer. The code re-reads after writing to detect silent refusal --
verify that self-check is actually capable of detecting failure.

**S3. Failure now causes an unbounded retry loop on a per-refresh path.**
`RepairFields` sets `_completed = true` only on a clean pass and returns `false` otherwise. The
primary call site is a Harmony PREFIX on `CharacterTableau.RefreshCharacterTableau`. If any field
write fails permanently, does the full 215-field reflection scan re-run on every single tableau
refresh, forever? Assess the cost and whether the earlier "latch on failure" behaviour was actually
safer. Propose the correct middle ground.

**S4. The gate can be defeated by its own probe.**
`TryEnsureRepaired` gates on `MBAnimation.GetNumActionCodes()` and
`MBAnimation.GetActionCodeWithName("act_inventory_idle_start")`. Verify `MBAnimation` genuinely does
not touch `ActionIndexCache` for a NON-EMPTY name (it reads `ActionIndexCache.act_none.Index` on the
empty branch). Also: is `MBAnimation` itself free of a static constructor that could touch
`ActionIndexCache`? If the gate can initialise the type, the fix CAUSES the bug it repairs.

**S5. Call-site ordering and re-entrancy.**
The repair runs as the FIRST statement of the Patch2 prefix, before its own try/catch. Confirm
`TryEnsureRepaired` cannot throw. Also check `CharacterTableau_FirstTimeInit_Patch`. Can the repair
be re-entered from a nested tableau refresh while holding `_gate`? Is the lock usage correct
(`_completed` written inside `RepairFields` under a different lock acquisition than the check)?

**S6. Repairing a field the engine intends to be -1.**
`act_none` is skipped by name. Are there OTHER fields legitimately negative? If a name exists in the
engine but maps to a DIFFERENT field, the repair writes a wrong animation index into a vanilla static
-- a silent, non-crashing corruption. Prove the field-name-to-action-name mapping is safe, or find a
counterexample. Note the code already special-cases `act_raid_jump -> act_raid_jump_1`.

## ALSO CHECK

- `TableauDiagnostics`: `Log`, `LogAlways`, `LogDeduped` share the `_seen` dictionary keyspace while
  `LogError` uses `_seenErrors`. Can a caller-supplied key collide with another and silently suppress
  output? Is `MaxTotalLines` now genuinely enforced across all four emitters?
- Does any diagnostic path still touch `ActionIndexCache` statics before the gate has passed?
  `ProbeActionIndexHealth` is public and ungated -- can it be reached early?
- `ProbeActionSets` enumerates every race x 3 suffixes at `OnGameInitializationFinished`. With
  `MBActionSet.GetActionSet` (non-throwing) does it still fire engine asserts or exceptions?
- Thread safety: are tableau refreshes guaranteed main-thread? If not, what breaks?
- Is the fix even necessary at the Patch2 site given `SubModule.OnGameInitializationFinished` already
  attempts it? Enumerate the orderings where each is load-bearing.

## REQUIRED SECTIONS IN YOUR OUTPUT

1. VANILLA CODE -- the four decompiles above, as code blocks.
2. KNOWN SUSPECTS -- S1..S6, each CONFIRMED or DISPUTED with evidence.
3. FINDINGS -- each with severity (P1/P2/P3), file:line, concrete failure scenario, and fix.
4. WHAT WOULD MAKE THIS SAFER -- if you think the whole approach is wrong, say so and propose the
   alternative. A narrower fix that only repairs the handful of actions the tableau path actually
   reads is a legitimate answer -- argue for or against it.
5. OBSERVATIONS -- anything else.

## QUALITY GATES

- Do not flag code that matches vanilla behaviour as a bug.
- Verify "missing" claims by grepping before asserting absence.
- If you cannot decompile something, say UNVERIFIED rather than guessing.
- Prior Codex failure modes on this project: assuming `empire` = Rohan (it is Dunland), flagging
  vanilla-matching code as bugs, and skipping the hard sections. Do not skip S1 or S2 -- they are the
  reason this review was commissioned.

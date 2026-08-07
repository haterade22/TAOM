# Adversarial review — Patch69 tournament winner-panel guard + female-dwarf mesh fix

You are an independent adversarial reviewer for TAOM, a Mount & Blade II: Bannerlord **v1.4.7**
total-conversion mod. Your job is to find defects, not to agree. Assume the author was confident and
wrong. Prior reviews on this project have a ~95% precision rate; the misses are always the thing
nobody thought to check.

Repo root is the working directory. Engine DLLs:
`E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/`
and module DLLs under `.../Modules/<Module>/bin/Win64_Shipping_Client/`.
Decompile with `pwsh tools/taom-src.ps1 path <FullTypeName>` (prints an absolute path to cached
decompiled source). **The dump at `E:\Decompiled_Bannerlord\` may lag — installed DLLs are
authoritative for any signature claim.**

## What changed (review these)

New:
- `Main/Features/Arena/TournamentEntrant.cs`
- `Main/Features/Arena/ITournamentRosterGuardService.cs`
- `Main/Features/Arena/TournamentRosterGuardService.cs`
- `Main/Features/Arena/Hooks/Patch69_TournamentRosterGuard.cs`
- `Main/Features/Arena/Hooks/Patch69_TournamentEndGuard.cs`
- `TAOM.Tests/Features/Arena/TournamentRosterGuardServiceTests.cs`

Modified:
- `Main/Features/Arena/ArenaIoC.cs`
- `Main/SubModule.cs` (Patch69 registration + unload reset)
- `Main/Features/MissionDiagnostic/{IMissionDiagnosticService,MissionDiagnosticService}.cs`,
  `Main/Features/MissionDiagnostic/Hooks/MissionDiagnosticBehavior.cs`
- `Main/Features/HeroRace/Hooks/ActionSetCode_GenerateActionSetNameWithSuffix_Patch.cs`
- `tools/validate_mesh_refs.py` + `tools/tests/test_validate_mesh_refs.py`
- `docs/reference/lotrlome-armory-snapshot/skins.xml` (one attribute)

**Out of scope — do not review, another dev's in-flight work:** anything under
`Main/Features/Enlistment/`, `Main/Adapters/{EncounterAdapter,IEncounterAdapter}.cs`,
`TAOM.Tests/Features/Enlistment/`, `docs/features/enlistment.md`,
`docs/reviews/lessons/testing-qa.md`.

## The claims under test

A player crash bundle (`d7d9f7d3`, Erebor, TAOM v2.0.18.0, engine v1.4.7.117484) shows a
`TargetInvocationException` → inner `NullReferenceException` at
`SandBox.ViewModelCollection.Tournament.TournamentVM.OnTournamentEnd()`, reached from
`ExecuteSkipAllRounds()` → `TournamentBehavior.EndCurrentMatch(false)`. The player was NOT a
participant.

The author asserts:

1. **`OnTournamentEnd` has two unguarded dereferences** — `hero.MapFaction.Color` on the hero branch
   and `character.Culture.Color` on the troop branch — and `Hero.MapFaction` returns null for a
   clanless, non-special hero with neither a home settlement nor a party.
2. **The roster must never shrink.** `FightTournamentGame.GetParticipantCharacters` pads to exactly
   `MaximumParticipantCount` (16); `TournamentBehavior.CreateParticipants` fills a fixed 16-slot
   array; `FillParticipants` passes every slot to `TournamentRound.AddParticipant` →
   `TournamentMatch.AddParticipant`, which dereferences `participant.Team` with **no null check**.
   So removing an entrant would trade an end-of-tournament NRE for an entry-time NRE. Hence the
   guard SUBSTITUTES (`culture.EliteBasicTroop ?? culture.BasicTroop`) rather than removing.
3. **Two further null sites in `OnTournamentEnd` remain reachable but unreproducible** —
   `TournamentParticipantVM.Refresh(null, …)` sets `Participant = null` but never resets `IsValid`
   to false, and `GetParticipants()` filters on `IsValid`. The author could not produce that state
   from a full 16-entrant bracket, so it is logged (bracket dump) rather than guarded.

**Verify or refute each of these three against the installed engine. Quote decompiled code.**
If any is wrong, the design is wrong — say so explicitly and loudly.

## Highest-value questions (the author flagged these as the likeliest misses)

1. **Save/load.** Does `TournamentBehavior` persist `_participants` (`[SaveableField]` /
   `SaveableTypeDefiner`)? If a tournament survives a save/load, is `GetParticipantCharacters`
   re-called — or are unguarded participants restored, bypassing the guard entirely? This is the
   single most likely hole.
2. **Other producers.** Is `GetParticipantCharacters` genuinely the only path that populates
   tournament participants? Check `TournamentGame` subclasses and any `TournamentModel` path.
3. **`GetMenuText` side effect.** In v1.4.7 `FightTournamentGame.GetMenuText` calls
   `GetParticipantCharacters(..., includePlayer: false).Count(p => p.IsHero)`. The postfix runs
   there too and substitutes heroes with troops — does that change the "N lords are competing"
   menu text the player sees, and how often is `GetMenuText` called? Is substituting on that call
   harmless, wrong, or a performance problem?
4. **Complete dereference enumeration.** Enumerate EVERY dereference in `OnTournamentEnd` (all
   branches, including the `else` at the bottom that walks Round1/2/3). State for each: guarded by
   Patch69 / null-safe by construction / STILL EXPOSED. The author claims `WinnerBanner`'s
   `BannerImageIdentifier` ctor is null-safe — verify.
5. **Finalizer semantics.** `Patch69_TournamentEndGuard` returns `null` to swallow. Confirm that is
   correct Harmony finalizer semantics for a void method, that it cannot mask an unrelated
   exception class that should propagate, and that leaving the VM half-updated cannot soft-lock the
   tournament screen (can the player still exit?). Note TAOM's `PatchShield` also installs a
   finalizer on patched methods and its `ShouldSwallow` only eats
   MissingMethod/MissingField/TypeLoad — check for finalizer-ordering interactions.
6. **`EliteBasicTroop`/`BasicTroop` nullability.** Can either be null for any TAOM culture? Check
   `Main/_Module/ModuleData/taom_spcultures.xml` — do all 22 cultures declare both? What happens on
   a culture that declares neither (the guard's fail-safe leaves the entrant in place — is that the
   right call)?
7. **The data fix.** `skins.xml` adult female dwarf: `underwear_bottom_mesh` changed
   `sk_dwarf_underwear_female` → `sk_dwarf_underwear_female_a`. **Independently verify** that the
   bare name is not a shipped mesh and the `_a` form is, using a trailing-token-boundary search over
   `Modules/LOTRLOME_Armory/AssetPackages/*.tpac` (the bare name occurs as a PREFIX of the `_a` form,
   which is exactly why a substring search reports a false present). If this is wrong, the fix is
   wrong. Also: only the tracked snapshot is in git — the live install file was edited too. Is
   editing a dependency module's live file the right mechanism here? Read
   `docs/reference/lotrlome-armory-snapshot/README.md` first, which documents that workflow.

## Known suspects for this codebase

- Harmony `[HarmonyPatchCategory]` present but no matching `_harmony.PatchCategory("…")` in
  `SubModule.cs` = silently dead patch. Verify both categories, exact case.
- Apply timing: a category applied in a campaign-phase batch is dead for anything reachable from a
  pre-campaign screen. Confirm a tournament cannot be reached earlier.
- Service-locator use (`IoC.Resolve<T>()`) outside boundary classes; missing lazy `??=`; static
  caches not cleared on module unload.
- NaN/null polarity on decision gates.
- Interface signature changes leaving an unupdated implementation or caller
  (`IMissionDiagnosticService.LogActionSetSeen` gained three parameters).
- Strings interpolated BEFORE a dedup early-out on a hot path.
- ADR-007: services must not touch sealed TaleWorlds types. Is `TournamentEntrant` a legitimate
  boundary DTO, or a fig leaf?

## Output format

For each finding:

```
[P1 CRITICAL | P2 HIGH | P3 MEDIUM | P4 LOW] <one-line title>
  File:line
  What: <the defect>
  Why it's wrong: <evidence — quote decompiled engine source or TAOM source>
  Repro/Impact: <concrete failure scenario>
  Fix: <minimal change>
```

Then:
- **Verdict on each of the three numbered claims:** CONFIRMED / REFUTED, with evidence.
- **Summary:** N findings by severity.
- If you find nothing at a severity, say so explicitly rather than padding.

Prefer a small number of well-evidenced findings over a long speculative list. An unverified claim
must be labelled UNVERIFIED, never given a severity.

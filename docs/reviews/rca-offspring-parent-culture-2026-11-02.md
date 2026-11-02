# RCA: childbirth NRE from a hollow parent character (2026-11-02)

**Status:** the throw at `CharacterObject.cs:150` is repaired, and so is the next throw on the same parent. The
state that fits the crash has a complete engine mechanism, but the event that first made this save's character
hollow is UNVERIFIED. Nothing has run in game.

**Trigger:** #1076, player crash bundle `3fbdc0c5` (TAOM v2.0.36, build `6589bdd11`, Bannerlord v1.5.6.125053,
Simplified Chinese, saved campaign `RysRLzlFP8Xm` loaded from `saveauto2`).

```
System.NullReferenceException
  at CharacterObject.get_StealthEquipments()
  at Hero.SetInitialValuesFromCharacter(CharacterObject characterObject)
  at Hero..ctor(String stringId, CharacterObject characterObject, CampaignTime birthDay, CampaignTime deathDay)
  at HeroCreator.CreateHero(CharacterObject character, Boolean useCharacterAsTemplate, ...)
  at HeroCreator.DeliverOffSpring_Patch1(Hero mother, Hero father, Boolean isOffspringFemale)
  at PregnancyCampaignBehavior.CheckOffspringToDeliver(Pregnancy pregnancy)
```

The last engine log line, 0.3 s before the capture, is `creating hero from template with id: lord_MM15_6`.

## What is proven

- `CharacterObject.CreateFrom` calls `BasicCharacterObject.FillFrom`, which copies the raw `_culture`
  (`BasicCharacterObject.cs:195`).
- On a hero, `CharacterObject.Culture` returns `HeroObject.Culture` (`CharacterObject.cs:264-273`), so nothing shows
  a null raw value until a clone inherits it.
- `CharacterObject` persists only `_heroObject` and `_originCharacter`. The raw culture exists only after XML
  `Deserialize` or `InitializeHeroBasicCharacterOnAfterLoad`.
- `lord_MM15_6` is in `characters/lords.xml` at the crash build, with `culture="Culture.mistymountainorcs"`.
- `rgl_log.txt` has no `Null object reference found` line, so `UnregisterNonReadyObjects` removed nothing at this load.

## What is inferred

The null is `Culture` at `CharacterObject.cs:150`, by elimination:

- `IsHero` is false on a clone, because `HeroObject` is set after `SetInitialValuesFromCharacter` (`Hero.cs:1310-1311`).
- The same `AllEquipments.WhereQ` call succeeded for `BattleEquipments` at `Hero.cs:2321`.
- A null list element would throw in a lambda frame, and the top frame is the getter.
- `lord_MM5_6` has the same culture and the same equipment templates, and its birth succeeded 33 s earlier, so
  the culture's stealth roster is healthy.

No line in the bundle states the field's value. Codex's read-only probe against the installed DLLs adds strong support:
`report.json` records `IlOffset` 124 for the getter, and the probe reproduced offset 124 with a null raw culture and no
throw with a non-null one (CLR 4, Windows PowerShell 5). This is its run, not repeated here.

## The hollow character

An IL scan of all 325 DLLs under the game's `bin` and `Modules/*/bin` folders (read-only, by the engine lens) found
these writers of the raw culture: `Deserialize:489`, `InitializeHeroBasicCharacterOnAfterLoad:305` (load only),
`FillFrom:195` (new clones only), and two co-op paths that do not write null. `CharacterObject.set_Culture` has no
caller. So nothing in the scanned set clears the field during a session. The scan cannot see a `FieldInfo.SetValue`
loop, and it read DualWield v0.9.14, not the player's v0.10.5.0 (UNVERIFIED).

A complete engine path produces a hollow character instead:

1. At an earlier load, XML does not reach the hero's character.
2. `Campaign.cs:1457` calls `UnregisterNonReadyObjects`, which logs `Null object reference found` and unregisters
   it. `OnUnregistered` sets `IsRegistered` to false, and nothing repoints the hero.
3. `IsRegistered` is `[SaveableProperty(3)]` (`MBObjectBase.cs:23-25`), so the save keeps it.
4. At the next load, `OnBeforeLoad` registers the object only `if (IsRegistered)` (`MBObjectBase.cs:72-76`).
5. `MBObjectManager.cs:1388` finds no object of that id, creates a new one and upgrades that twin from XML. The hero
   keeps the hollow one, with no log line at this load.

A hollow character fits the frame: every read before `Hero.cs:2323` is safe on it. It also fits the silence: the
sweep in Patch83 and the unregister step both walk registered objects only.

**UNVERIFIED:** what made this save's character hollow. The bundle holds one session's log, and `lord_MM15_6` has been
in `lords.xml` since `8c338501b` (2026-06-23). `MBObjectManager.LoadXML` swallows every exception (`:790-796`), so a
throw in `Deserialize` would leave the rest of a file unread; that is one candidate.

## What was refuted

- **A second definition without a `culture` attribute** (`BasicCharacterObject.cs:489` writes null): the repo defines
  the id once, and `DualWield_TAOM` v0.1.3 does not name it (read from a local copy of that version, not the player's).
- **An id removed at this load:** the id is defined, and this session's log has no unregister line. An unregister at an
  earlier load is the mechanism above.
- **Memory pressure:** 33% commit headroom in the report.
- **A TAOM writer of the field:** no hit in `Main/**/*.cs` for a `_culture` write or a culture write through
  `BasicCharacterObject`.

## Fix

`Patch83_OffspringParentRepair`, a Prefix on `DeliverOffSpring`, repairs each hollow parent:

- **With a healthy registered character of the same id:** it sets `_originCharacter` to that twin and calls the
  engine's `InitializeHeroCharacterOnAfterLoad`. This copies culture, body range, race, skills and equipment together.
  The field is saved, so for a hero in the hero lists `SandBoxManager.cs:401-403` repeats the copy on every later load,
  and the child's origin is the twin too. **Trade-off:** this writes one saved field, on a character that is already
  broken, and pulls the twin into the save, as any XML template used as an origin is. The hero reads as generated for
  `IsOriginalCharacter` and the load copy. It stays unregistered, so `Hero.FindFirst`, `Hero.FindAll` and
  `CharacterObject.All` still do not see it.
- **With no twin:** it takes the saved `Hero.Culture`, then the clan, and fills the other null fields inert. A hero with
  neither source is reported and left alone. No culture is invented.

The gate is the hollow state, not the culture alone: Coop's culture getter postfix writes on a read, so on a co-op host a
culture-only gate would skip a parent whose body range is still null. The `PreAfterLoad` postfix logs a census and writes
nothing itself. [Feature doc](../features/stale-character-repair.md).

**Reading the census line:**

| Facts | Means |
|---|---|
| `original=True`, `registeredSameInstance=False`, `registeredCulture=<an id>`, `bodyNull=True` | hollow, with a twin: the mechanism above |
| `original=True`, `registeredSameInstance=False`, `registeredCulture=none` | the id is gone from ModuleData (#670) |
| `original=False`, `origin=<id>`, `registeredCulture=<an id>` | a generated child of a hollow root |
| `original=False`, `origin=<id>`, `registeredCulture=none` | a generated hero whose origin's id is gone (#670) |
| `original=True`, `registeredSameInstance=True`, `ready=True`, `bodyNull=False` | XML that lacks the attribute |
| No line at load, then a repair at a birth | a writer after load |

## Review findings (lens 5 Data flow, lens 2 Engine compatibility, lens 4 Completeness, lens 6 Design)

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | The first repair restored the culture only. On a hollow parent, `HeroInitializationArgs` then threw at `CharacterHelper.cs:48`, after `AddHero`, leaving a half-built child in a saved list once per day | Partial repair of a restored object | The class comment of `StaleCharacterRepairService` says a stale object is null in several places at once and that repairing one field moves the crash. The author read it and still treated culture as the only null, because the stack names only that | Trace the object past the first throw to the end of the method; lesson below |
| 2 | HIGH | The adapter's decisions never executed in a test: dropping the base cast would have left the crash in place with no line. The service tests substitute the whole adapter | Adapter with logic tested as a thin wrapper | "Adapters via service tests" assumes thin wrappers, and the feature doc said behaviour needs a live campaign | `HeroCharacterStateAdapterTests` on uninitialized objects; the doc claim corrected |
| 3 | MED | The check wrote `pass` before the birth ran, and the title said "does not crash", so a crash read as PASS. A first Postfix fix still let one finished birth hide a crash from a second | Verdict proves less than it claims | The author pinned the check to the repair, not to the outcome | A void Finalizer writes pass or fail; `HollowParent1076Tests` runs the real manifest |
| 4 | MED | A culture-only gate skips a hollow parent on a co-op host, because Coop's getter postfix writes the culture on a read | Gate on one symptom of a state | The author recorded the Coop write for the census only, though the Prefix read the same member | Gate on `IsCharacterHollow` |
| 5 | MED | Neither hook had a behaviour test, and the trap row broke the doc budget gate | Entry points left to an in-game test that cannot run yet | "Entry points: test via game" does not cover a game test still owed | `Patch83HooksHarmonyTests`; the trap moved to `adapters.md` |
| 6 | LOW | `nameNull` read `CharacterObject.Name`, which answers from the hero: the same trap as the bug. The census looked up the character's own id, so a generated child read as "id gone" | Shadowed getter; two lookups for one fact | The author applied the base-type read to culture only | `basicNameNull`; one `FindRegisteredTwin` |
| 7 | LOW | A thrown repair wrote no line, and a lost binding wrote `pass`, so both read like a healthy game | Verdict proves less than it claims | The author treated the catch as the end of the path | `Fail` in the catch; `BindingsResolved` gates the no-twin path |
| 8 | LOW | Wrong reasons in comments and docs: "a value cleared during a session", "a parent outside the hero lists", a wrong caller claim, and "writes nothing" under co-op | Claim not checked against writers and callers | The author wrote them from the plan, not from the IL | Corrected here and in the comment |

**Repeat offender:** finding 1 is the shape that `docs/features/stale-character-repair.md` "Why the repair covers four
fields, not one" records from September, in the same feature.

**Not applied:**

- **Repair at load instead of at the birth (lens 2, lens 6):** it would delete the patch and heal every read, but it is a
  saved-field write at load on every hollow hero in a working save, and the origin is unverified. Revisit under #1076 once
  the steps below have run and field logs show no repair without a census line.
- **A separate patch category (lens 2):** a failed apply would also cost the load sweep, but only on engine drift, which
  `Patch83HooksHarmonyTests` and the binding tests catch first and `TryPatchCategory` logs. Splitting adds a `SubModule`
  line for a small win.
- **A rendered-line test with a real `OutcomeLog` (lens 4):** `HollowParent1076Tests` uses hand-written lines. The format
  is `OutcomeLog`'s own and has its own tests.

## Codex review of `1165a00de`

One `gpt-6-astra` pass at `ultra`, approved by Mike for this commit: `docs/reviews/raw/codex-adversarial-hollow-parent-2026-11-02.md`.
It found no healthy hero the gate misreads and no serialization problem from the shared `StringId`.

| # | Codex | Verified | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|---|
| F1 | HIGH | MEDIUM | Adoption copied the twin's race over the race `RacePersistenceService` had restored on a registered generated hero, and the next save captured the wrong one. It needs a hollow origin and a deliberately changed race | Stale state / lifecycle | Lens 5 saw this edge and accepted it as too small to guard. The author treated the engine's copy as all or nothing and did not ask which copied field another system owns | The race is kept for a registered character; regression test; lesson below |
| O3 | MEDIUM observation | LOW | A missing-API exception PatchShield swallows before the Finalizer leaves a null child, and `child=none` read as a passed birth | Logic error | The author wrote "none" as a display fallback, and it reached the pass branch | A birth with no child writes fail; regression test |
| F2 | MEDIUM | LOW, not fixed | `parent-birth` is not paired per birth, so a hard kill inside a second repaired birth in one process reads as PASS | Verdict proves less than it claims | One session verdict covers many births | Simplicity criterion: a managed throw already writes a fail, and pairing needs a per-attempt id. The limit is written in the manifest |

Not a defect: a competing prefix could skip this repair (O3), and a reused id could select another character (O2). Neither has a known case.

**Not re-reviewed:** a CRITICAL or HIGH fix would normally get a further Codex pass, but Mike approved one pass, so the two
fixes above are proven by their tests and `/verify`.

## Proof still owed (#1076)

The player's save is not available (Mike, 2026-11-02), so #1076 closes with `triage-needs-ingame`. The state can be
built without that save:

1. Load a save with one lord removed from a copy of `lords.xml`, and expect the unregister line.
2. Save, restore the XML and load again. Expect no unregister line, and a census line with
   `registeredSameInstance=False bodyNull=True`.
3. Force a birth from that lord (a due pregnancy, or the engine's `CampaignCheats.GenerateChild`). Expect both `[Check]` lines in
   `tools/ingame_checks/1076.toml`, and no crash.

The earlier childbirth crash in [rca-shield-rethrow-stack-2026-09-22.md](rca-shield-rethrow-stack-2026-09-22.md)
(bundle `2d446100`, v1.4.8) threw a different exception type after `lord_AB8_1` and `lord_6_23_8`. A shared cause is
a guess.

## Prevention

Two lessons in `docs/reviews/lessons/state-lifecycle-save.md`: a hero's getter hides a null raw field, and a repair of a
restored object must follow that object to the end of the method it protects.

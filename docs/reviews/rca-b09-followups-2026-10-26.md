# RCA: B09 follow-ups, dead hero races, prefab brushes, mixin lookups (#1011, #1005, #1006, #1013, 2026-10-26)

## Top-line

Three follow-ups from the #907 review were fixed together: dead heroes lost their race on every load (#1011), nine brush
names in TAOM's own GUI prefabs were defined by no module (#1005), and the Lines of Descent mixin resolved its logger on
the failure path (#1006). The tier 1 review (the #1011 fix changes what the save holds) ran Codex at `ultra` and three
Claude lenses (data flow, engine compatibility, XML). None found a CRITICAL or HIGH, so no second wave ran. Together they
confirmed 3 MEDIUM and 10 LOW findings, and Codex found one older defect in another feature (#1013). All were fixed in the
same session except the items under "Not applied".

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | The fix read `Hero.AllAliveHeroes` plus `Hero.DeadOrDisabledHeroes`, but the engine also resets dead heroes it has removed from that list (`CampaignObjectManager.UnregisterDeadHero`: dead wanderers after 40 days, a wanderer killed before the player met them, dead notables after 7 days, a destroyed rebel clan), through a third loop in `SandBoxManager.InitializeCharactersAfterLoad` (`:413-426`). Those heroes still reverted (lens 2). | Entity set read from the first two loops only | The plan and the build read the two named loops of the reset and stopped; the third loop works on characters, not hero lists. TAOM already knew the engine unregisters dead heroes (`MessengerCampaignBehavior.cs:612-617`). | One source, `Hero.FindAll`, which scans `Campaign.Characters`, the list the third loop covers. Lesson in `state-lifecycle-save.md`. |
| 2 | MEDIUM | No test failed if the adapter went back to living heroes only: the seven new service tests fed a fake roster to a service whose diff was a rename, so they passed on the unfixed code (lens 5, Codex). | Test pins the wrong layer | The fix lived in a thin adapter, which TAOM does not unit-test, and the RED step was only a compile error from the rename. | A source pin on the adapter, shown red with the old enumeration and green with the fix. Lesson in `testing-qa.md`. |
| 3 | MEDIUM | `EventXml.xml`, a prefab nothing loads (no C#, no prefab tag, no DLL string), was repointed to a defined brush instead of deleted (lens 7, lens 5). | Dead data kept alive | The brief said "if unused, still fix it". | Deleted. |
| 4 | LOW | The new prefab gate used the C# string-literal regex on XML: it missed single-quoted attributes and `Constant BrushName` values, and it read brush names inside comments (Codex, lens 7). | Gate reuses a parser built for another input | The regex was written for brush names inside C# string literals. | The gate parses each prefab with `XDocument`. |
| 5 | LOW | The allowlist called `MarinerTroopIcon` a naval brush; it is the Id of a child widget that `MarinerTroopBrush` names (lens 7, lens 2). | Attribute name taken as type | The gate decides "brush" from the attribute name. | The gate skips the `MarinerTroopBrush` attribute (a widget reference) instead of allowing the value. |
| 6 | LOW | The adapter de-duplicated by StringId for a hero "in both lists", a state the engine never produces (lens 5, lens 2). | Guard for an impossible state | Written defensively without reading `CampaignObjectManager.HeroStateChanged`. | Removed with the single source of finding 1. |
| 7 | LOW | `GetHeroRace` read living heroes while `SetHeroRace` wrote dead ones too (lens 5, Codex). | Read and write on different sets | Only the two methods the persistence service calls were widened. | All three read `Campaign.Characters` through the engine's `Hero.FindAll` and `Hero.FindFirst`. |
| 8 | LOW | The custom battle slider knob kept `ButtonBrush1` where vanilla uses `CustomBattle.Slider.Handle`; with the track now drawn, the button art sat on it (lens 7). | Partial vanilla parity | The brief named only the undefined names. | Vanilla's brush. |
| 9 | LOW | A `Brush.FontSize="18"` override on the Presets button was added on a guess; the brush's own 24 fits the button (lens 7 measured it). | Unmeasured override | Width not measured. | Removed. |
| 10 | LOW | Three feature docs still named the old brushes, one of them telling authors to use an undefined name the new gate fails (lens 5). | Docs not swept with the rename | Only the rule files were updated. | Updated. |
| 11 | LOW | Rule and doc text described the gate's module model as the engine's (it loads every launched module), told authors to grep two modules, and the new lesson stated an in-game effect as observed (lens 2, lens 5, lens 7). | Engine claims from the design | Written before the engine read. | Corrected. |
| 12 | LOW | #1006 fixed one of four container lookups the rule forbids in the Lines of Descent mixins: the click handlers and the clan row's refresh still resolved services (lens 5). | Fix scoped to the reported line | The issue named one line. | Every lookup moved to the constructors, with a source pin. |
| 13 | LOW | (older, #1013) The optional troop census wrote races on session launch, before the race restore on the same event (Codex). | Event order | Listeners run newest-first and the census registered later. | The census writes on the after-session-launched event. |

**Not applied:**
- The restore calls `SetHeroRace` once per hero whose race differs, and each call scans `Campaign.Characters` (troops
  included) until it finds the hero, so restoring R heroes among C characters costs O(R x C) at load (Codex, lens 5).
  The 30 kept debug logs on the desktop hold 3 restore lines, each "Restored race for 0 heroes"; an index waits for a
  measurement (`simplicity-criterion.md`).
- Follow-up closed 2026-11-04 (103636439 removed the prefab) (no issue: a dead attribute in a prefab outside this change; correcting its casing would shrink the text to 6,
  so it needs Mike's look): `Main/_Module/GUI/PreFabs/PreviewPanel.xml:16` sets `Brush.Fontsize`, which the engine
  ignores because property lookup is case-sensitive.
- UNVERIFIED (co-op, lens 2 and lens 5): the co-op module syncs `BasicCharacterObject.Race`, so a client now logs one sync
  error per restored dead hero. Check the count in the next co-op load test.

**Fix review.** The fixes changed save-path behaviour, so Codex (`high`) and one convergence reviewer re-read them. Neither
found a CRITICAL or HIGH. They found 1 MEDIUM and 8 LOW, all in the tests and text that came with the fixes, all fixed:
the #1011 lesson title restated the overturned two-list design (MEDIUM); the gate's widget-reference exception matched a
value, not the attribute; the rule overstated what the PrefabExtension scan checks; the new census subscription had no
pin, so deleting it kept every test green; the adapter pin rejected the engine's equivalent `Hero.FindFirst`; one older
mixin test duplicated the new one; and four doc and comment lines kept stale wording (a count nothing computes, the
"alive/dead union", "as humans", a fixed item under "Known").

## Root-cause pattern

Findings 1, 6 and 7 share one cause: the hero set was taken from the named lists in the engine method and not from the
objects the method actually touches. Findings 3, 8 and 12 share another: each fix covered exactly the lines the issue
named, not the same defect next to them.

## Why each reviewer caught or missed what it did

- **Engine compatibility (2):** read the whole reset method and the unregister path, so it found the third loop (1).
- **Data flow (5):** traced every caller and test, and found the missing pin (2), the asymmetry (7), the stale docs (10)
  and the remaining lookups (12).
- **XML (7):** measured the brushes, sprites and text widths, and found the dead prefab (3), the knob (8) and the font (9).
- **Codex (`ultra`):** refuted the save-size and side-effect suspects with engine evidence, and found the gate gap (4) and
  the older census defect (13); it did not read the third loop.

## Feedback memories to codify

None. The lessons carry the rules.

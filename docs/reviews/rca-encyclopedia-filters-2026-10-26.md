# RCA: encyclopedia filters and hero page layout (#907, 2026-10-26)

## Top-line

The tier 1 review of the encyclopedia work (a Race filter and a race-aware Age filter on the hero list
through one Harmony postfix, the Family block moved above Allies on the hero page, and a "Lvl: N, Skills:"
heading from a view-model mixin) ran Codex at `ultra` and three Claude lenses (data flow, engine
compatibility, XML). None found a CRITICAL or HIGH, so no second wave ran. Together they confirmed 1 MEDIUM
and 9 LOW findings. Every one was fixed in the same session except one design choice, kept on purpose. The
page padding finding came from three reviewers independently.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | The Age filter sorted heroes the player has not met by their real age, although vanilla hides an unmet hero's age on the list and the page (`DefaultEncyclopediaHeroPage` comparers, `EncyclopediaHeroPageVM`) (lens 5). | Information leak through a new reader | The brief listed the age read and its edge cases (dead, future birthday) but not the visibility rule that vanilla applies to the same value. TAOM's "Show all encyclopedia characters" (on by default) hides the leak in normal play. | The Age predicate asks the information restriction model first; tests for a known and an unknown hero. Lesson in `adapters-taleworlds-api.md`. |
| 2 | LOW | The feature doc said the level heading shows for every hero of age; vanilla shows the skills heading only when the hero has a skill of 50 or more (`HasAnySkills`) (lens 5). | Doc claim from the design | The mixin sets the text for every hero; the visibility binding is vanilla's, on another property. | Doc and in-game checklist corrected. |
| 3 | LOW | Nothing checked that every race the game installs is in exactly one filter group; a new Armory race would fall into no Race item silently (lens 5, lens 7). | Data gate missing | The shipped-config test checked the JSON against itself, not against the installed `skins.xml` files. | A `LiveInstall` test reads Native and Armory `skins.xml` and asserts each race is in exactly one group. |
| 4 | LOW | The move left the relations column without the bottom margin that vanilla's last block (FamilyGrid, 50) carried, so the Enemies grid touched the page end (lens 5, lens 7, lens 2). | Layout attribute owned by position | The margin belonged to "the last block", but it was written on the Family grid and moved with it. | `MarginBottom="50"` on `EnemiesGridParent`, pinned in the layout test. Lesson in `localization-ui.md`. |
| 5 | LOW | The Lines of Descent prefab extension cited a hero page line number that the move made stale, and its comment and doc said the page "is not edited" (lens 7, lens 2). | Cross-feature reference by line number | Line numbers in comments do not move with the file. | The comment cites the `ScopeID` only; wording now says Lines of Descent does not edit the page. |
| 6 | LOW | The blank line before `</strings>` in `taom_module_strings.xml` was removed by hand, although the harvest tool keeps it on purpose, so the next harvest makes a whitespace diff (lens 7). | Hand edit against a generator | The blank line looked like noise. The same hand edit was made in #905 and #906. | Restored. Lesson in `localization-ui.md`. |
| 7 | LOW | The level heading mixin resolved its logger from the container inside the catch on every failed refresh (Codex). | Hot-path resolve | Copied from the Lines of Descent mixin. | The logger is captured in the constructor; a source pin. |
| 8 | LOW | The doc said vanilla never shows the civilian outfit on the hero page; vanilla shows it for notables and for noncombatant heroes who lead no party (lens 2). | Doc claim from the design | Written from memory of the page, not from `EncyclopediaHeroPageVM`. | Doc corrected. |
| 9 | LOW | The binding snapshot `patch-targets.md` did not list the new patch, so the binding gate could not see its target (lens 2). | Generated doc not regenerated | The snapshot is regenerated only on engine bumps by habit. | Regenerated with `tools/snapshot_api_surface.ps1`. |
| 10 | LOW | The patch comment, the registry and the doc said a throw leaves "vanilla's" result; another postfix can run first, so it is the incoming result. The registry entry lacked the PatchShield note the comment pointed to (lens 5, lens 2). | Imprecise contract wording | The co-op module's postfix on the same method was found late in the review. | Wording fixed in all three places; the registry entry gained the note. |

**Kept by design (lens 5):** a filter predicate that throws logs once and returns true, so a broken hero
stays visible in the list instead of vanishing. The brief chose visibility over precision.

Follow-up closed 2026-11-04 (#1006) (no issue: a one-line LOW in a merged file; filing it needs Mike's word): the Lines of Descent
mixin `EncyclopediaHeroFamilyTreeMixin.cs` resolves its logger inside its catch, the same shape as finding 7.

Follow-up closed 2026-11-04 (#1011) (no issue: older than this change, in race assignment; filing it needs Mike's word): Codex found
that a generated hero whose race differs from its template, and who then dies, can reload with the template
race, because the engine reinitializes dead template heroes on load (`SandBoxManager.cs:393-410`). The Race
filter then lists that dead hero under the template's race. UNVERIFIED in game.

## Root-cause pattern

Findings 1, 4 and 8 share one shape: a value or attribute was reasoned about where TAOM first met it, not
where vanilla gives it meaning. The age has a visibility rule in vanilla, the bottom margin belongs to the
column's end, and the outfit rule lives in the view model. Findings 5, 6 and 9 are references that a change
does not update by itself (a line number, a generator's output, a generated snapshot).

## Why each reviewer caught or missed what it did

- **Codex (`ultra`):** refuted the timing, ordering and server suspects with engine evidence and found only
  the logger resolve (7); it did not compare the Age read with vanilla's visibility rule.
- **Data flow (5):** caught the leak (1) by tracing who may see the age, and the doc and gate gaps (2, 3).
- **Engine compatibility (2):** caught the wrong doc claims (8), the stale snapshot (9) and the wording (10).
- **XML (7):** caught the margin (4), the stale line reference (5) and the generator blank line (6).
- The builder's tests missed all of these because the adapter fake had no information model, the layout
  test pinned order but not the end margin, and no test read the installed races.

## Feedback memories to codify

None. The lessons above carry the rules.

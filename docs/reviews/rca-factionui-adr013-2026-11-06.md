# RCA: FactionUI move behind the ADR-013 client seam (#1090, 2026-11-06)

## Top-line

`/deep-review` at tier 1 on commit `0ca5364de` (branch `fix/factionui-adr013`), which moved the faction UI merge's
19 campaign and encyclopedia files from core `TAOM.dll` into `TAOM.Client` and cleared the 15 gates that merge
`0f00a8e70` left failing. Reviewers: Codex at `ultra`, Lens 5 Data flow, Lens 2 Engine compatibility, Lens 7 XML. No
CRITICAL or HIGH, so no second wave. Three MEDIUM and four LOW findings were fixed before the merge; four LOW items are
deferred below. Codex found no defect. Nothing was run in game.

## Findings

| # | Sev | Finding | Found by | Fix and proof |
|---|---|---|---|---|
| F1 | MED | The two tree mixins passed a possibly null value through `OnPropertyChangedWithValue(object, string)`, and the new comments said UIExtenderEx 2.13.3 forwards it unchanged. On v1.5.6 its delegate lookup is ambiguous across the eight `ViewModel` overloads and comes back null, so a null value reaches `value.GetType()` (`BaseViewModelMixin<T>` line 176) and throws out of the page's `Refresh`. No engine caller refreshes a live hero page twice today, so no crash was seen. | Lens 2 | Both mixins call `OnPropertyChanged(name)`, and Gauntlet re-reads the getter; comments corrected. Filtered suite green; the in-game clear of the binding is owed. |
| F2 | MED | `EncyclopediaListScreenVM.Label` built `{=taom_ency_filters}`, `{=taom_ency_reset}` and `{=taom_ency_open_entry}` by string concatenation. The harvester and `UnregisteredLocalizationKeyBaselineTests` match only literal `{=taom_...}` text, so no string table held the keys and every language showed English. | Lens 5 | Literal `TextObject` keys, `Label` deleted, harvested, seeded in the 12 languages. |
| F3 | MED | `taom_fui_ency_bookmark` and `taom_fui_ency_local_information` have no consumer; the branch seeded 24 language rows for them, which a paid translator pass would have translated. | Lens 7, Lens 5 | English and language rows deleted. |
| F4 | LOW | `CharacterScreenVM` built its level label as `"Level " + n`, beside a commit that registers `taom_hero_level` for the same text. | Lens 5 | Uses `taom_hero_level`. |
| F5 | LOW | `NullCampaignPresentationAdapter`'s doc promised "no themed campaign screens"; core's `FrontEndMovieService` still swaps the prefab, whose campaign widgets then load as plain widgets. | Lens 5 | Doc states the real degraded mode, which `ClientComposition` already logs. |
| F6 | LOW | The `TAOM.EncyclopediaTroopNames` literal in `SubModule.cs` and the client constant were not pinned; an unknown category applies nothing without a failure. | Lens 5 | `PatchCategory_IsTheLiteralSubModuleApplies`. |
| F7 | LOW | `CampaignTimeHintPatch` had no Harmony patch registry entry, so a crash triage grep missed it. | Lens 5 | Registry section added. |
| F8 | LOW | `TAOMMapBar.xml` kept an empty `Variables` and eleven unused `Constants` (they resolve only inside their own prefab). | Lens 7 | Deleted. |

## Root-cause pattern

F2 and F3 come from the same gap: the localization gates read keys from literal text, so a key built at runtime is
invisible, and a registered key that nothing reads is never flagged. The merge added both shapes and the gates passed.
F1 is a comment written from a library's annotation, not from its code on the installed engine.

## Why each reviewer missed what it missed

- **Codex** checked the nine suspects in the brief and confirmed the adapter timing and the cast; the brief asked about
  the mixins' scope, not their null path, and nothing about runtime-built keys.
- **Lens 7** read the string tables and found F3; it did not read the C# that builds keys.
- **Lens 2** decompiled UIExtenderEx and found F1; string tables were outside its lens.
- **Lens 5** traced each binding and key to its consumer and found F2 to F7.

## Deferred

- FOLLOW-UP (no issue: pre-existing, no player effect today; Mike to decide): the reverse field-camp check
  `Prefab_EveryBinding_ExistsOnTheOverlayVm` reads only the vanilla prefab, not `FactionUI/TAOMFieldCampOverlay.xml`.
- FOLLOW-UP (no issue: pre-existing, once per process at most): when a themed build fails, the finalizer reloads vanilla
  but passes the wrapper view model to `OnLoaded`; `EncyclopediaListScreenVM` then never unsubscribes its native events.
- FOLLOW-UP (no issue: pre-existing): 47 by-name reads and commands in `CharacterScreenVM` and `EncyclopediaListScreenVM`
  have no row in a binding gate; all resolve on v1.5.6 (Lens 2 F2).
- FOLLOW-UP (no issue: tooling): `tools/data_gates.py` runs one `PrefabAttributeNameBindingTests` method for `gui-tests`,
  not the class.
- UNVERIFIED: every themed screen in game (map bar, hero page with and without family, unit page, list, character
  screen), and a co-op client.

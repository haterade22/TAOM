# RCA: portrait clan colours (#993) and refuge clan rows (#994), 2026-10-24

## Top-line

Two bugs found by code reading during the Shadowfax Screens review were confirmed on the installed
v1.5.5 DLLs and fixed with tests in one session; a tier-1 `/deep-review` (Data flow and Engine
compatibility; Codex not run, no paid word) found no HIGH and no incompatible API, and its MEDIUM
and LOW findings were fixed before commit. Neither fix has been seen in game.

- **#993:** all five BannerColorPersistence portrait patches wrote `CharacterCode.Color1/Color2`
  after `CharacterCode.CreateFrom` had built the `Code` string. `Code` has a private setter and is
  the only thing `CharacterImageIdentifier` keeps and the renderer parses
  (`CharacterImageTextureProvider.OnCreateImageWithId` calls `CreateFrom(id)`), so no clan colour
  ever reached a portrait since the feature shipped. Fixed with one helper,
  `ClanColoredCharacterCode.Recolor`, that rebuilds the code through the 8-argument `CreateFrom`.
  Per the feature's rules (Mike, 2026-10-24) every hero's portrait now shows his clan's colours,
  AI lords included, matching their battlefield armour.
- **#994:** Refuge decorated its clan-screen rows ("(being raised)", hidden wage slider) only at list
  build; each row's `RefreshValues` then ran `UpdateProperties` and reset the name. Fixed with a
  postfix on `ClanPartyItemWithPartyVM.UpdateProperties`.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | HIGH (feature dead) | Five portrait patches set colours on a built `CharacterCode`; nothing rendered them (#993). | Write nothing consumes (repeat) | The authors read `Color1`/`Color2` as live properties and never traced the object to the renderer's input; the feature doc even prescribed "mutate `__result` directly, no `CreateFrom` roundtrip needed". No test or in-game check compared a portrait. | Rebuild helper; the doc recipe corrected; a source-scan gate fails any `.Color1 =` / `.Color2 =` in the Hooks folder outside the helper; lesson in `localization-ui.md`. |
| 2 | MEDIUM | Refuge rows decorated at list build lost the suffix at the row's own refresh (#994). | Decoration on the wrong lifecycle step | The patch comment called the loss "benign" for a language change; nobody checked that the clan screen refreshes every row on every open. | Postfix on the row refresh; lesson in `localization-ui.md`. |
| 3 | MEDIUM | The first fix patched only `OrderOfBattleHeroItemVM.RefreshInformation`; the hero pool is built by the `(Agent)` constructor, which never calls it, so captains and pool heroes would show different colours. | Incomplete writer list | The fix followed the existing patch's target instead of listing every place the engine builds that portrait. | Constructor added as a second target; covered by the lesson (list every builder of the value). |
| 4 | MEDIUM | `PartyWageSubTitleText` is bound by no v1.5.5 prefab (verified by search of every installed module and the repo), so Refuge's "honest 0 wage line" never showed. | Write nothing consumes (repeat) | Same as 1: the property was written without checking a widget binds it. | Writes deleted; docs say only what renders. |
| 5 | LOW | Docs and comments said `Side` was unset when `GetCharacterCode` ran; it is a readonly field set first, only `IsPrisoner` lags. | Engine claim | Reasoned from the symptom, not the constructor order. | Corrected. |
| 6 | LOW | Registry Patch75 text named the abstract base type and "ready" refuges only. | Doc accuracy | Not updated with the v1.5 row type. | Corrected. |
| 7 | LOW | The binding pin skipped two members `Recolor` reads (`BodyProperties` field, `IsEmpty`). | Test gap | Pinned by memory of the call, not by reading it. | Added. |
| 8 | LOW | A new refuge lookup duplicated `IRefugeBook.GetByPartyId`. | Simplicity | Reuse rung 2 not checked. | Replaced by the existing lookup. |
| 9 | LOW | `OnPartySelection` (looked up by name) had no reflection-catalogue row. | Catalogue (pre-existing) | No gate enforces rows. | Row and DataRow added. |

Follow-up closed 2026-11-04 (8dbdfeb12) (no issue: needs a `Main/SubModule.cs` edit, the single-owner file): `ClanPartyItemVM_GetCharacterCode_Patch` can never change a result (heroes are recoloured inside `CampaignUIHelper.GetCharacterCode`, non-heroes get no clan info); delete it with its `SubModule.cs` initialize line when that file is free.

## Root-cause pattern: a value written where nothing reads it

Findings 1 and 4 are the same class as the nameplate redesign that lost relation colour for five
months (#591, `.claude/rules/gui-ui.md` "A clone can also stop consuming an engine signal"): code
writes a value that the renderer never reads, and nothing fails. Here it shipped twice in one feature
and once in another. The prevention is the same in every case: before writing a UI value, follow it to
the widget binding or the renderer's input on the installed version, and pin that chain with a test or
an in-game check.

## Why each lens caught or missed these

- **Data flow:** found 3 (by listing every builder of the hero item), the AI-lord reach, and the doc
  recipe; it did not search prefabs for the wage binding.
- **Engine compatibility:** found 3 and 4 (by searching every installed prefab), 5, 6 and 7, and
  confirmed every signature.
- **Not run (tier 1 runs only these two):** Standards, Efficiency, Completeness, Design. Finding 8 was
  raised by Data flow.

## Feedback memories to codify

None; the lesson goes to `docs/reviews/lessons/localization-ui.md`.

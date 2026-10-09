# RCA: Fiefs button on the campaign map bar (#789, 2026-10-09)

## Top-line

The fief hub (Patch36) opened only with F6. The fief-management port had brought in `FiefManagementNavItemVM` for a
"Fiefs (F6)" map-bar entry, but nothing created or bound it. #789 adds the entry: a postfix on
`MapNavigationHandler.OnCreateElements` (the seam NavalDLC uses for its Manage Fleet button) appends
`TaomFiefsNavigationElement`, the bar renders it from its bound list, and brush layers `taom_fiefs` give it a
background and an icon. F6 and the button share one opener (`FiefHubOpener`) and one map gate (`MapMenuGate`), which
the field camp's "Make camp" button now uses too. The unusable view model is deleted.

The deep review ran seven lenses (Standards, Engine compatibility, Data flow, XML; Efficiency, Completeness, Design).
It found no CRITICAL or HIGH defect and no engine incompatibility (21 usages verified on the installed v1.5.5; the seam
is identical in the v1.5.4 dump). Every finding was checked before it was fixed.

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | The element resolved its services itself instead of taking them by constructor | Service locator in TAOM-built code | The element was treated like an engine-built boundary | Constructor injection from the postfix factory; a pin test fails if `IoC.Resolve` returns |
| 2 | MED | `IFiefHubOpener` had one implementation and no fake | Structure not needed | Interface written by habit | Deleted; the concrete opener is registered |
| 3 | MED | The per-frame poll began with an uncached MCM lookup (csharp-architecture item 9) | Hot-path settings | The provider predates the new per-frame reader | Cached accessor; rows in `CampaignHotPathSettingsProvidersTests` |
| 4 | MED | The DI graph was checked by searching source text | REPEAT of `lessons/build-tooling-workflow.md` (source-text wiring) | Copied the feature's older test style | Real-container `Validate` test |
| 5 | LOW | The guard list existed twice (fief hub and field camp) and could drift; a click could read a one-frame-stale `IsInMenu`; F6 opened the hub with the escape menu open; vanilla's own map-action check (prisoner, encounter, raft or ferry) was missing | REPEAT of "parity claimed but never enumerated" (`lessons/harmony-il.md`) | The guards were copied from F6, not from vanilla's full set | One `MapMenuGate` with the menu-context, escape-menu and `CampaignUIHelper.GetMapScreenActionIsEnabledWithReason` checks; a source gate keeps the list in one file |
| 6 | LOW | A failed element build was swallowed with no log | Silent failure | A defensive catch inside the pure helper | The helper has no catch; the postfix's log-once catch reports it |
| 7 | LOW | Doc claims written from intent: "Fiefs (F6)" (it reads "Fiefs [F6]"), "no localized key for F6", "resolves IoC lazily", "cannot drift", stale comments in five files, an empty Issue section | Doc claims | Written before the engine read | Each corrected |
| 8 | LOW | Five tests repeated stronger gates or pinned engine members TAOM never calls; two enlistment states had no test row; a null guard for a dependency that is always registered | Test hygiene | | Deleted, added and removed as listed |

## Behaviour change to note

The shared gate also refuses the field camp's "Make camp" button in the newly gated states: the frame after a menu
opens, the escape menu, and vanilla's map-action refusals (prisoner, encounter, raft or ferry).

## Owed

In-game: the button's look after Kingdom's 90 px background, every permission state, F6 parity, NavalDLC off and on,
a Coop launch, gamepad focus (checklist in `docs/features/fief-management.md`). The two new strings
(`taom_fief_nav_disabled`, `taom_fief_nav_busy`) need the paid translation run (#782). `/engine-bump`: built and
tested on v1.5.5 against the v1.5.4 pin.

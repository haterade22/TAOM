# RCA: the map bar Extra Fast Forward button never applied the multiplier (#574, 2026-09-12)

**Top line.** The button shipped on 2026-04-05 bound to vanilla's `ExecuteTimeControlChange(2)`,
which sets the time mode and never writes `Campaign.SpeedUpMultiplier`, the only value
`Campaign.TickMapTime` scales by. The May audit (#168, closed 2026-05-14) found exactly this and
chose "Option A: mode only, redundant with vanilla, tracked as a future enhancement", writing that
into the mixin as a comment. The 2026-08-22 keybind refactor of the same feature passed a five-agent
deep review and a Codex pass without the button coming up, because neither review's scope included
it. On 2026-09-12 it arrived as a player report ("the slider does nothing"), with a second player
saying it worked for them (the E key writes the multiplier; that player had also moved Turbo to
Left Control, so a bare Ctrl press ran at the turbo slider). Fix: both map bar fast-forward buttons
now route through `ITimeAccelerationService`; the lit state refreshes per frame; the provider floors
extra at fast. The fix's own deep review found no defect in the change.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH (player-facing, 5 months) | The Extra Fast Forward button was vanilla fast-forward under a different tooltip; the MCM slider changed nothing a click could reach | UI wiring / spec | The button was authored by copying vanilla's FastForward button XML and changing its id and tooltip, so its handler was vanilla's. The multiplier lived in a service the prefab never reached. #168 confirmed the gap and chose the redundant option; the "known limitation" framing made a defect read as a design decision, and it survived two later reviews scoped to their own diffs. | Lesson in `docs/reviews/lessons/localization-ui.md`: a TAOM control binds a TAOM command that applies the mod's state and then delegates to vanilla, never vanilla's command directly; for every `Command.Click` in a TAOM prefab, trace the handler to a mod-owned write. Deep-review Agent 5 gains that trace (item 8b). |
| 2 | MED | Sticky multiplier: after one E press the engine kept the extra value and vanilla's FastForward button (mode only) ran at it until Space restored the normal value | Shared-state consistency | The writers of one shared engine property (key path, vanilla button, turbo save and restore, `Campaign.OnLoad`) were never enumerated together. Service tests mock the adapter, so the shared property was invisible to them. | Enumerate every writer of a shared engine property when adding one. Data-flow trace 4 of this review did so explicitly and walked four scenarios. |
| 3 | LOW | The button's lit state hooked `RefreshValues`, which `MapTimeControlVM` calls only from its constructor and on a gamepad state change, so it never followed a key press | UI refresh | Assumed a method named Refresh runs per frame; nobody grepped the VM for its call sites. | Before choosing a mixin refresh method, grep the VM for that method's call sites. `Tick` is per frame on this VM. |
| 4 | LOW | `docs/reference/feature-map.md` had no TimeAcceleration row (since the 2026-07-18 extraction) | Docs | The feature predates the map and the extraction listed features by hand. | Fixed in this change; the completeness agent now checks the map. |
| 5 | Disputed | Efficiency agent rated per-frame `TaomSettings.Instance` and three `Campaign.Current` reads HIGH while calling its own cost claim unverified | Review quality | Cost asserted from an assumed lock. Verified against the installed binaries: `Campaign.Current` is an auto-property; MCM's `GlobalSettings<T>.Instance` is two lock-free `ConcurrentDictionary` reads plus a `foreach` over a handful of settings containers; the service reads the setting only while a fast-forward mode is active, at most once per frame. | No code change. Agent 3's own rule already says an unverified cost is reported UNVERIFIED, never HIGH; the rating contradicted it. |
| 6 | LOW | `EnterFastForward` lacked the campaign-inactive and wait-menu tests `EnterExtraFastForward` had | Test symmetry | Both commands share one private method; the second got the guards' coverage by proximity. | Two tests added (64 in the filter). |
| 7 | LOW, pre-existing, deferred | `Campaign.OnLoad` resets `SpeedUpMultiplier` to 4 on every load regardless of the configured Fast Forward Multiplier; the vanilla "3" key (`MapTimeFastForward`, mode only) then fast-forwards at 4 until Space or a map bar button writes the configured value | Engine reset | Out of #574's scope; the key path had the same exposure before. | Recorded in the feature doc and CHANGELOG as a known limitation. Optional follow-up: write the configured value on `OnGameLoaded`. |
| 8 | LOW, pre-existing, deferred | No floor between the turbo and extra multipliers | Settings ordering | Out of scope. | Recorded in the feature doc. |

## Root-cause pattern: a comment laundered a defect into a design decision

The 2026-08-22 RCA for this same feature named its pattern "I verified the shape of things, not the
path they travel". Finding 1 is the same failure one layer up. The button's XML matched vanilla's
shape, the mixin bound a property, the tests were green, and a code comment said the redundancy was
known. Nobody followed a click from `Command.Click` to `SpeedUpMultiplier`, and once the comment
existed there was a written reason not to. A "known limitation" on a control whose tooltip promises
the missing behaviour is an open bug with a label on it, and the label is what let it ship for five
months.

Finding 2 is the shared-state cousin: two writers of one engine property, each correct on its own
path, never reconciled because the tests never saw the property.

## Why each agent of the 2026-08-22 review missed finding 1

The five agents and the Codex pass reviewed the keybind diff (`MapInputAdapter`,
`TaomTimeControlHotKeyCategory`, `TimeAccelerationService.OnTick`, strings). The button lives in
`TimeAccelerationPrefab.cs` and `TimeAccelerationMixin.cs`, which that diff did not touch.

- Standards: no changed file contained the binding.
- Compatibility: verified the engine members the diff used; `ExecuteTimeControlChange` was not among them.
- Efficiency: no per-frame path in the diff involved the button.
- Completeness: the feature doc described the button as "a visible Extra Fast-Forward button on the
  MapBar", which is true, and the doc's own Changelog did not say it applied a multiplier.
- Data flow: traces data declared in the changed files; the prefab's `Command.Click` was not one.

Generalisation: a user-facing control adjacent to the changed code inherits no review. Agent 5's new
item 8b traces every `Command.Click` in a TAOM prefab regardless of whether that file changed.

## Why the fix's own review found nothing in the change

All five agents reported on the new code: 34 engine and UIExtenderEx usages verified against the
installed 1.4.8 binaries (including that UIExtenderEx's attribute patch overwrites an existing
`Command.Click` and that its method wrapper forwards `GetParameters`, so vanilla's parameter
conversion reaches the mixin's `int`), eight data flows connected with the four multiplier writers
walked, standards clean, the completeness gap in finding 4 fixed, and the efficiency HIGHs downgraded
on evidence (finding 5).

## Feedback memories to codify

None new. The lesson entry in `docs/reviews/lessons/localization-ui.md` carries the rule; the
feature memory `features/time-acceleration.md` records the state and the owed in-game smokes, which
are the only thing left that static review cannot supply: the button visibly faster than vanilla's
and following the slider, vanilla's button restoring normal speed, E then Space then Space, turbo
restore, wait-menu parity, both buttons lit during extra, and #504's still-open Options checklist.

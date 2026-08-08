# CHANGELOG — TAOM (Tales From the Age of Men)

> **Archive:** entries before 2026-07-01 live in [`docs/changelog-archive/CHANGELOG-2026-H1.md`](docs/changelog-archive/CHANGELOG-2026-H1.md) (rolled 2026-07-12; cadence: each Jan 1 / Jul 1 — keep the current half-year here, roll the rest).

## 2026-08-08

### docs: bring every documentation surface in line with the tracker sweep

The triage closed 81 issues, which falsified a status claim wherever one was written down. Swept
four surfaces against the authoritative closed/open lists rather than against each doc's own prose:
13 feature docs carrying `**Status:** Open` for an issue that is closed; the auto-memory tree, where
MEMORY.md and three feature files still listed `gh issue close #N` obligations that were discharged
(the index is now 61 lines / 12 KB, shorter than before, since the work was removal); `TRACKING.md`,
which records #210's closure as `obsolete-premise` without ticking a single S6–S12 box that was never
actually run; and `docs/audits/README.md` + `docs/INDEX.md`, from which `docs/audits/` had been
unreachable entirely.

Six lessons appended to `lessons/build-tooling-workflow.md` (75 → 79) and `lessons/xslt-moduledata.md`
(20 → 22), counts re-derived by counting `^### ` headings rather than trusting the index. The two that
cost the most time: a regex of `<tag ` misses `<tag\n  attr=…`, which undercounted an action set as
134 entries when it holds 4,842 and nearly shipped as a public claim; and `grep -r` over a live
ModuleData directory reads `.bak*` files the engine never loads. Both times the shallow grep was wrong
and the careful verdict was right, which is itself the lesson.

`REVIEW-LOG.md`'s header claimed `COMPLETE: 25/25 features reviewed, 2026-04-05/06` while its rows ran
to #408; it now describes what the file holds, counted. That file also has mixed line endings in its
committed blob — 1,924 CRLF against 51 lone-LF — so an ordinary editor write turns a one-line change
into a 3,848-line whitespace diff. The header edit was rebuilt from HEAD's exact bytes to keep it at
`1 1`. Normalising the file wants its own whitespace-only commit.

Two CLAUDE.md Traps rows: the engine's 131,072-entity `rglConcurrentQueue` assert is global across
loaded modules while `tools/check_prefab_budget.py` counts only `TAOM_Map/Prefabs`, so it prints `OK`
at 99% of the ceiling (130,151 measured, ~921 spare, #359); and a fix landed in `LOTRLOME_Armory` or
`TAOM_Map` is real but unversioned, so a module reinstall silently reverts it — which is why the
in-repo validator gate matters more than the external edit. `check_prefab_budget.py` was undocumented
and now has a `tools/README.md` row carrying that caveat.

Not-tested: nothing in the game. Documentation only; `python tools/lint_docs.py --summary` reports 0
findings across all eight checks.

### perf(ui): 4.8 GB of resident texture memory freed by sizing art to what the screen shows

Two sets of images were authored at source resolutions that no widget could ever display, and both
were paid for permanently rather than while visible.

**Banner icons (L1) — 2,624 MB → 128 MB.** 369 sigils authored at 1024×1024 packed 9-per-sheet into
41 atlases of 4096² uncompressed RGBA8, and the category carries `<AlwaysLoad/>`, so the engine held
all of it from startup to exit — at the menu, on the map, in every battle — whether or not the
banner editor was ever opened. The widest widget that consumes them is 110 Gauntlet design units;
Gauntlet scales by screen *height* against a 1080 reference, so on the 5120×1440 test display they
render at 147 px and at 4K height 220 px. Rebaked at 256², which covers both: 41 sheets → **2**.
Vanilla authors its own equivalents at 100×100. Quality improves rather than degrades — the old path
minified 1024 → 147 px with no mipmaps. Confirmed in game after the restart.

**Faction culture art (L3a) — 2,474 MB → 78 MB decoded, 932 MB → 32 MB on disk.** 34 files were
5504×3072 and one 6227×11825, rendering into a 429×240 design-unit panel — a 36× area oversample —
while 36 sibling files already sat at ≤340×200. Capped at 1024 on the long side, preserving aspect.
The saving is a **worst case, not a flat win**: these load per faction viewed during character
creation and are never released, so a player who loads a save pays nothing and one who browsed all
72 paid the full amount.

`region_*` map art was deliberately left alone. `PolygonWidget.cs:323` sizes those as
`_bboxW * parentW` against a `StretchToParent` container, so they scale with the whole map surface
rather than a fixed panel — capping them at 1024 would visibly soften the culture-stage map.

Also documented a trap that cost time here: `GUI/SpriteData/FactionMap/` sits under the compiled
sprite directory but is **not** part of the sprite system. Those PNGs are read at runtime by
`EngineTexture.LoadTextureFromPath`, appear in no manifest, and are declared in no category — so
they need no bake and no restart, while the banner icons need both.

Not-tested: memory savings are computed from source dimensions and the atlas packer's arithmetic
(`SpriteSheetCount` 41→2 verified in both manifests), not from a before/after `[MemSample]` pair.
Research: `BannerEditor.xml`, `CharacterCreationCultureStage.xml`, `PolygonWidget.cs`,
`FactionImageWidget.cs`, `TAOMSpriteData.xml`.

### fix(docs): RuntimeDataCache is read by the shipping client — the 42 GB is not free to drop

Open question Q1 of the native-commit audit is settled, and against the hoped-for answer. A Procmon
capture of the shipping client (launch → menu → one battle) recorded **23,329 RuntimeDataCache
operations: 13,795 `ReadFile` across 5,036 distinct `.rdc` files, and zero writes** — TAOM_Map
13,577, LOTRLOME_Armory 7,658, TAOM 1,801, and `Alliance.Wargs` 293, a module the install-weight
ledger did not list. So RDC cannot be excluded from a release on the strength of "no vanilla module
ships one"; the rename A/B is still needed to decide whether the client *needs* it.

943 of the lookups returned `NAME NOT FOUND`, so the client already tolerates missing entries and
will most likely degrade rather than break — but with zero writes observed there is no evidence it
regenerates, which would make the slow path permanent for anyone who shipped without it.

A method note worth more than the result: the first capture produced zero RDC rows because it was
started and stopped without the game ever running, which is indistinguishable from "editor-only" if
you read the row count alone. Corroborate that Bannerlord actually appears in the capture before
reading a zero as an answer.

### fix(diagnostics): the 11.9-second battle-load gap was attributed, and it moved the problem

The gap markers added yesterday produced their first live reading, and it relocated the cost. The
window they were built to explain is now **2.5 s**, of which all of TAOM's behaviour registration is
**55 ms**. `polls=28` over `waitMs=903` — ~32 ms per poll — showed genuine multi-frame streaming
with a healthy main thread, refuting the blocking-native-spin hypothesis that would otherwise have
been the obvious thing to chase.

The dominant cost is **19.5 s after loading finishes**, 70% of a 27.8 s load, on a battle with
`agents=0`. The single `[MemSample]` inside that window explains it: `memLoad=97%`,
`availPhysMB=1468`, and the working set collapsing `7430 → 5351 → 2447 MB` as the OS evicted pages.
**Load time and memory pressure are one problem**, which is precisely what stamping `MemStats()` on
those markers was designed to decide. `[MemSample]` also captured a menu baseline (`privMB=9238`)
for the first time — that station did not exist before the sampler was moved.

Caveat recorded rather than glossed: ~95 GB of the 108 GB system commit was not Bannerlord, so a
clean-boot repeat is owed before attributing the 19.5 s wholly to TAOM.

### chore(triage): 147 open issues checked against HEAD — 81 closed, 60 kept, 6 escalated

The tracker had stopped being a queue and become a work log. Most open issues were past-tense
engineering reports written when the work was planned or already done, so for a large share the
question was never "is this broken?" but "did anyone press close?" All 147 were checked against
`828bf941`: 81 closed with a comment citing the evidence, 52 kept open with a status comment, 21
labelled without a comment, 6 escalated as needing a decision. Full per-issue evidence in
[`docs/audits/issue-triage-2026-08-08.md`](docs/audits/issue-triage-2026-08-08.md).

Seven of those closures came from a second pass that corrected the first one's test. Eleven issues
had been held open as `blocked-external` because their fix lives in `LOTRLOME_Armory` or `TAOM_Map`,
which this repo does not track — but "this repo cannot ship it" answers a different question from
"does the fix exist". Checked against the installed modules, seven were already applied: #352 (both
`body_name` typos corrected), #364 (`family_type="1"` on the caparison), #300 (`as_dwarf_warrior` at
4,842 actions), #390 (packages repacked 2026-08-07, `validate_mesh_refs.py` 3959/3959 clean), #338
(`town_LN1` down to one `map_siege_ram`, all 221 fortifications on the identical shape), #342 (the
Mordor cap idempotent, zero inversions across 20 slot-by-tier cells) and #358 (106/106 item defs, all
present in `pack5.tpac`). Four remain genuinely external: #398 and #385 need asset re-exports, #62
needs a change inside BannerlordTogether, and #359 turned out to have regressed — the prefab split
shipped and was then reverted, and `check_prefab_budget.py` reports `OK` at 99% of the engine's hard
cap because it counts one module where the assert is global.

Agents produced verdicts and never called a `gh` write command — every mutation came from one paced,
ledgered script driven by a verdict file that was read first, so a confident wrong answer could not
reach the tracker on its own. Eighteen cluster agents triaged; twelve more were paired to *failure
modes* rather than to issues, handed each claim without the first pass's evidence and told to refute
it. That produced 174 independent claim-checks over 76 proposed closures and killed two of them
(#285, #370), both for the same reason: an author-declared exit criterion that the first pass had
overridden. Thirty-seven more closures carry a caveat in their closing comment.

Six traps decided more verdicts than any amount of reading would have. The two CHANGELOG files are
newest-first, so a grep's first hit is the *older* entry — #82's archive line says "port all 7 native
hooks **+ activate**" and a later line disables it at the wiring level, and closing on the first hit
buries a parked feature whose issue is the park's only durable record. Three issues' fixes live in
`LOTRLOME_Armory`, which this repo cannot ship, so merging here delivers nothing to a player.
`culture="Culture.rohan"` returns zero rows because Rohan's 68 NPCs carry `Culture.vlandia`. PRs #80,
#271 and #403 are closed-unmerged, so a reference to one is evidence work did *not* ship. #275 reads
as fully shipped and its entire implementation sits on an unmerged branch. And a shipped fix is not a
solved problem: #317 landed, was celebrated, and a later entry records that it fixed the wrong half of
the loop.

Comments went only where something had actually changed. An issue whose situation had not moved got a
`triage-*` label instead — "nothing has changed since January" is a notification with no payload, and
the 32 issues touched in the last week already carry a human comment more current than triage could
write. That turned ~90 near-identical public comments into 52 that each state a delta.

Not-tested: nothing in the game. This is a tracker operation, no mod code changed.

### feat(coopinterop): report the settings that can diverge two co-op peers

TAOM's MCM settings live in a per-user file outside the save, no co-op mod syncs them, and nothing
on the wire corrects a mismatch — so two peers holding different values quietly compute different
battle autocalc, bandit density, AI army targeting and desertion rates. `SettingsFingerprint` hashes
the 112 simulation-relevant settings (of 167 across four MCM classes) and writes one short code per
MCM group to each peer's log, under the same co-op gate as the Harmony census. Solo sessions compute
nothing. It reports; peers still do not exchange the codes, which is the remaining step and needs
two machines to verify.

A per-group code rather than one number, because a single global hash answers "something differs"
and sends a player through 112 checkboxes, where "Battle Tactics differs" is one screen. Values go
through `InvariantCulture`, or two peers with identical settings would hash differently on any
machine with a comma decimal separator.

**Three corrections applied on merge.**

The pinned counts were measured at the fork point and nine settings had landed since — Enlistment's
two and Battlefield Promotions' seven. `TaomSettings` is 153, not 144. Both new `*Diagnostics`
toggles are classified as instrumentation (each is read by one provider that gates a trace, while
its feature switch stays relevant — the log/gameplay pair is exactly the trap that list exists for),
and `RebuildDistanceCacheAction` gets a fourth exclusion category: a `[SettingPropertyButton]` is a
`System.Action` that satisfies the get/set test and was being counted as a setting two peers could
hold differently. They cannot; one of them presses it. Final split: 167 / 112 / 55, re-pinned from a
measured run rather than arithmetic.

The read guard covered the get but not the render, and the render reflects too. MCM's
`Dropdown<T>.SelectedValue` is an unchecked `List<T>` indexer over a persisted `SelectedIndex`, and
`CaravanWarTradePolicy` is a live `Dropdown<string>` on no exclusion list — an index left stale by a
removed option threw from inside the render and took down the entire fingerprint, reducing the
feature to a warning line. Now one property costs one line, which is what the guard already claimed.
Pinned by a test confirmed to fail with the guard narrowed back.

Floats rendered through `"R"`, which .NET Framework does not round-trip reliably — and
`Directory.Build.props` pins `net472`. Now `"G9"` and `"G17"`. This one cannot raise a false alarm,
since both peers run the same framework; it can only let two genuinely different values collapse
onto the same text, which is a *missed* divergence — the thing the feature exists to catch.

### fix(tools): the GameModel catalogues drifted, and nothing could say so (#417)

Two files catalogue TAOM's GameModel overrides and both were wrong. 47 classes exist.
`.claude/rules/gamemodels.md` listed 35 and claimed 34 in two places; `docs/reference/gamemodel-registry.md`
listed 41. Neither had a checker, so neither could be wrong out loud.

The rules file is the one that costs something: it carries `paths: Main/Features/**/Models/*.cs`, so
that table *is* the briefing the next person gets when they edit a model. `TaomMapVisibilityModel`
returns a per-party spotting range — exactly the shape rule 9 (cross-entity propagation, written
after the NavalTravel #296 RCA) exists for — and it was not in the table, so the rule written for it
never reached it. Same for five others.

`check_model_registry` in `lint_docs.py` now reports unlisted models, phantom rows naming a class
that no longer exists, and prose counts that disagree with reality. It also reports finding nothing
under `Main/` as a failure, because a broken checker that reads clean is worse than no checker.

Merged from a fork, so the CHANGELOG gate in `check-changelog-changed.sh` never ran for it — this
entry is that gate applied after the fact. Break-tested before merging: renaming a real model
produced both `unlisted-model` and `phantom-model`, and corrupting the count produced `model-count`.

### fix(tools): a version mention on a line was silencing a real claim on the same line

`check_stale_versions` walks `STALE_VERSION_PATTERNS` in order and, when a match failed either the
negation guard or the present-tense-marker guard, `break`-ed out of the whole list. Both guards
judge *that version match*, not the line — so once #410 appended 1.4.5 and 1.4.6 after 1.3.15, a
line carrying a historical mention and a live claim had the claim retired by the mention.

`Historical: v1.3.15 shipped. The current target is Bannerlord 1.4.5.` reported nothing. Neither did
`Built for ~1.2.12, NOT 1.4.5. The current target is 1.4.6.` Both now report. The pin guard keeps
its `break` — naming the pin is a property of the sentence, so one contrast covers every version in
it.

This is the failure mode worth more than a false positive: a check that stops reporting says nothing
about having stopped. Three regression tests, each confirmed to fail with the `continue` reverted.

### docs(nativeskinfixes): the shipped signatures are v1.4.6 provenance, not a live binding

`native-skin-fixes.md` claimed all 7 signatures were "statically verified against the installed
v1.4.6 `TaleWorlds.Native.dll`". The installed engine is v1.4.7. #410 rewrote the heading one line
above this into provenance and left the stronger, present-tense claim standing — where the widened
checker could not see it, because `installed` is not one of its markers.

Adding that marker was tried and deliberately not shipped. It reports 8 findings, all genuine, but
`reflection-sites.md:66` ("all 32 resolve against installed v1.4.5") and `battle-scenes.md:7` are
binding-verification claims that want re-deriving against the pin, not rewording — `/verify-bindings`
work, not prose. Shipping the marker first would invite the wrong fix. The reasoning sits at the
marker site so the next reader does not re-derive it.

### docs(tools): three counts in the game-dir paragraph, corrected by counting

`tools/README.md`'s `BANNERLORD_GAME_DIR` paragraph advertises counted numbers, and all three were
off after #416 landed. Counting the actual imports: 22 tools resolve through `tools/_gamedir.py`
(not 21), 25 resolve the variable themselves (22 via the helper plus the three that keep inline
resolution), and 29 honour it once the four that inherit by import are added. The categories were
right — `analyze_armor_balance.py`, `apply_settlement_buildings.py`, `derive_armor_tiers.py` and
`generate_enlistment_rosters.py` name the variable only in help and error text, and genuinely get
their resolution from a tool they import.

### docs(claude): four rules from a night two sessions shared one working tree

The existing multi-session section covered the unpopped auto-stash. It did not cover what that stash
leaves behind, or the three ways the recovery itself can lose work — all four hit in one session:

- A stale auto-stash can leave a path `UU` with **everything already staged**, so a file gets edited
  with live `<<<<<<<` markers in it and a plain `git commit` sweeps both sessions into one commit.
- `git show stash@{0}:<f>` emits LF against a CRLF worktree, so a raw `diff` reports every line
  changed. That reads as total divergence; `--strip-trailing-cr` showed the stash was a strict
  subset and nothing had been lost.
- `--amend` run after another session commits rewrites **their** commit, not yours.
- An unstaged edit is fair game for the other session's next git operation — a CHANGELOG paragraph
  survived a reset and then vanished before it could be staged.

Also records the carve-out actually used: when the user asks for both sessions' work to land, commit
theirs as its own attributed commit rather than folding it into yours, and treat a CHANGELOG entry
whose code is absent as the tell that a change committed in halves.

### docs(localization): the enlistment strings file has 76 keys, not 36

`localization-map.md` recorded the count from when the file was created. Enlistment and Battlefield
Promotions have both grown since: 66 `taom_enlist_*` plus 10 `taom_fc_*`. The "source-only" half of
the note was still true — no language has a `std_taom_enlistment_strings_*.xml` — and that gap now
has its own issue (#418) rather than living as a parenthetical in a reference table. The note also
records why the gap degrades gracefully: both features build text as `{=key}fallback`, so an
untranslated key renders English rather than blank.

### chore(battleload): land the code for an entry that was already written

`feat(battleload): split the 11.9-second load gap into three named buckets, and add engine memory
attribution` was committed as a CHANGELOG entry while its implementation stayed uncommitted in a
shared working tree — so `HEAD` documented a feature whose code was not there. The code lands here
unchanged: the `EngineMemoryStatsReader` / `MemoryProbeReportFormatter` pair, the two
`MissionState` load-phase patches, the `Cheats/` folder, the `BattleLoadPhase` split, and the
`triage_battle_load.py` work, with their tests (C# suite 6052 green, 92 python tests green).

The split happened because a `pull --rebase` auto-stashed a tree two sessions were working in and
its restore left `CHANGELOG.md` conflicted, so the documentation half of the change committed and
the code half did not. `stash@{0}` was verified to be a strict subset of the working tree before
anything was staged, and is kept as a safety net rather than dropped. This is the second incident
of the class recorded in this file; the discipline that prevents it is in CLAUDE.md under
"Multi-session git safety".
### fix(cultures): an Uruk-hai lord with a Calradian face, and a carve-out named for the wrong reason

Two corrections applied while merging #412.

**Isengard's two new lord templates carried `BodyProperty.fighter_empire` on `race="uruk_hai"`.** In
the PR whose thesis is that hero templates must follow the culture, the one culture where the fix
stopped a layer short — it matched the 72 sibling entries in `npcs_isengard.xml`, which are
themselves the copy-paste artifact being fixed. Now `fighter_uruk_hai`, which exists at
`TAOM_bodyproperties.xml:301` and is what shipped data pairs with that race: 29 of the 30 troops
using it in `troops_isengard.xml` are `race="uruk_hai"`. All 14 cultures were checked; Isengard was
the only genuine mismatch. The four whose declared default is the undefined
`default_character_creation_body_property_empire` keep the sibling-matched value, which is the
better of the two available answers.

**The test's `LandlessCultures` exemption is now `HideoutOnlyCultures`.** The eight are not
landless — they own 159 settlements between them and every one is a `<Hideout>`. The exemption
survives, but for a reason nothing had written down: a hideout never revolts, so
`RebellionsCampaignBehavior` cannot reach them, and TAOM ships 208 wanderers across 18 cultures with
none in these eight, so no companion can carry one into `CompanionRolesCampaignBehavior`. Both
halves were counted, not assumed. The comment now states them, states what would void them (give one
a town, or ship a wanderer of one, and the list becomes an empty `MBReadOnlyList` for
`GetRandomElement` to index into), and cross-references `Patch65_LandlessCultureSpawnGuard` — which
uses "landless" for a different question and keeps no list at all.

### fix(cultures): every culture's lord templates were Dunlendings

A player granted a fief to a dwarf companion in Ered Luin and got a clan called Firebeard —
correctly Erebor — whose two new members were named Graldor and Dunric. Both names come out of
`taom_xslt_strings.xml`: `aom_dunland_male_name_29` and `aom_dunland_male_name_2`.

All fourteen of TAOM's own cultures shipped the vanilla `empire` block verbatim for
`<lord_templates>` **and** `<rebellion_hero_templates>` — the same sixteen
`spc_legion_of_the_betrayed` / `spc_hidden_hand` / `spc_embers_of_flame` / `spc_eleftheroi`
leaders, every one of them `culture="Culture.empire"` in `SandBox/ModuleData/spspecialcharacters.xml`.
`spcultures.xslt` renames empire to the Dunlendings, so those two lists resolved to Dunlendings
for Erebor, Rivendell, Gondor, Mordor and everywhere else. `notable_templates` had been done
properly per culture; these two were missed.

The engine never gets a second chance to correct it. `HeroCreator.CreateSpecialHero` leaves
`Culture` unset, so `InitializeHeroFromSettings` falls through to
`DefaultHeroCreationModel.GetCulture`, which returns
`hero.CharacterObject.OriginalCharacter.Culture` — the template's culture. The born settlement is
passed in and ignored. Whatever the template is, the hero is.

Two code paths read the lists, and both are player-visible:

- `CompanionRolesCampaignBehavior.SpawnNewHeroesForNewCompanionClan` builds two lords from
  `companionHero.Culture.LordTemplates` every time a companion is granted a fief.
- `RebellionsCampaignBehavior` builds rebel leaders from `settlement.Culture.RebelliousHeroTemplates`,
  so a rising in a dwarf or Gondorian town was led by Dunlendings too.

Each culture now has `spc_<culture>_lord_1` and `spc_<culture>_lord_2` in its own
`characters/npcs_<culture>.xml`, carrying the culture's race and `BodyProperty.fighter_<culture>`
face template, split by trait profile the way the headman templates are — bold versus cautious.
The equipment was already written: `equipmentsets/taom_lord_template_equipment.xml` has held
`taom_<culture>_lord_battle_male` and `_civilian_male` for all twenty cultures, reachable until now
only through the engine's culture-and-flag search for child and coming-of-age equipment. The new
templates reference those rosters directly.

`CultureLordTemplateTests` pins the invariant: every template a culture lists must be defined by
TAOM and carry that culture, and neither list may be empty — `MBReadOnlyList.GetRandomElement`
indexes straight into the list and throws on an empty one. Landless cultures are exempt; neither
code path reaches them.

Heroes already in a save keep the culture they were built with. This changes what spawns from here
on, not what has spawned.

## 2026-08-07

### fix(tools): a degraded validate_moduledata run stops reporting PASS as a pass (#404)

`validate_moduledata.py` took its Modules folder from a literal, so on a wrong root the registry came
back with 0 items and 0 NPCCharacters, `BROKEN_ITEM_REF` and `BROKEN_TROOP_REF` could not fire, and
the run still printed `PASS` and exited 0. It now resolves the folder through `_gamedir.game_modules`
(aliased on import, since `main()` binds a local of the same name), and a run whose root is missing
returns 2 rather than 0. The commit hook already fails open on anything that is not 1, so nothing
starts blocking that did not block before.

Measured on this machine: with the root right, `Registry: 5,952 items, 5,005 NPCCharacters` and
`PASS`, exit 0. With it wrong, `Registry: 0 items, 0 NPCCharacters`, `PASS`, and now exit 2.

The hook itself needed no change: `BANNERLORD_GAME_DIR` is a persistent user variable, so every
process it spawns inherits it.

### feat(tools): one place answers "where is the install", and a wrong root says so (#404)

Completes #404 on top of the writers. `tools/_gamedir.py` holds `game_dir(default)`,
`game_modules(default)` and `ensure_exists(path, what=...)`; 21 tools resolve through it.

`os.environ.get("BANNERLORD_GAME_DIR", default)` — the shape #401 established and seven tools still
carried — returns `""` for an exported-but-blank variable, and `Path("")` is `.`, so the tool reports
every file missing instead of the root being wrong. `game_dir` treats blank as unset. The two tests
covering that case fail against the old shape, which is how we know they test something:
`'' != 'E:\\Steam\\...'`.

Point 4 has an inverse the issue does not name, and it is the more expensive half: a wrong root that
makes a tool invent a catastrophe rather than hide one. Three reference validators build their
registry from the install and treat "not defined" as "broken", so an absent root registers nothing
and condemns everything:

| tool | wrong root | true answer |
|---|---|---|
| `audit_item_refs.py` | `Total broken refs: 35443 sites across 2897 item IDs`, exit 0 | `0 sites across 0 item IDs` |
| `validate_all_troop_refs.py` | `FAIL: 1488 armor refs do not resolve`, exit 1 | `PASS` |
| `validate_gondor_refs.py` | `FAIL: missing references will cause underwear bug in-game`, exit 1 | `PASS` |

The two that exit 1 are worse than the one that exits 0: exit 1 means "the data is broken", and
`validate_all_troop_refs.py` is the underwear-bug gate `tools/README.md` points at. A fabricated
failure there costs as much as a missed one. All three now exit 2 naming the folder they could not
read. (`validate_mesh_refs.py` shares the shape but not the bug — its root is absent here too and it
already exits 2 saying so; it takes `--items` / `--game` rather than the variable.)

`ensure_exists` went to the four places measured to end on a clean-looking result against a root that
is not there. All four exited 0 before; they exit 2 with the path named now. `rollback_erebor_iron_misfile.py`
printed five `SKIP: <file> not found` lines and `Total removed: 0`. `cleanup_deleted_gondor_armor.py`
printed `Grand total: 0`. `remap_stale_scene_names.py` printed `0 of 13 remaps match the live file`,
which reads as "already done". `rebuild_translation_files.py` was the worst of them: `mkdir(parents=True)`
built the whole `Modules/LOTRLOME_Armory/ModuleData/Languages/<lang>` chain under the bogus root,
found no `loc_*.xml` to read, and still finished on `Done.`

`taom_mcp_server.py` now derives its Modules folder from `BANNERLORD_GAME_DIR` and keeps
`BANNERLORD_GAME_MODULES` as an explicit override, so one variable is enough for a correct setup —
a server answering `item_exists` against the previous install is hard to notice. `tools/.env.example`
gains both `BANNERLORD_*` names.

`dump_settlement_buildings.py`, `rebalance_armor.py` and `rebalance_settlement_prosperity.py` were
left on their own inline resolution on purpose: they already use the `or` form, so they do not have
the blank-variable defect, and two of them define a local `game_dir()` the import would shadow. The
win would have been uniformity alone, against renames in files this work does not otherwise touch.

Six of the ten from the previous entry kept their inline `or` until this pass converted them too —
two forms inside one change is the drift the issue is about. Their output is unchanged: the six that
gained nothing but the helper are byte-identical to the previous version with the variable unset, and
the four that gained `ensure_exists` change exactly where they were meant to. Python suite: 400 tests,
up from 387, green on 3.14.

Running it first on 3.11 gave 3 errors in `test_translate_batching`, which looked pre-existing and
unrelated — it is both, but the reason is worth recording. `Path.read_text(newline=)` landed in 3.13,
so the suite is red on any older interpreter, and `python` on PATH here is 3.11.9 while the `py`
launcher defaults to 3.14. Four tools use the same keyword outside the tests
(`generate_lord_template_equipment.py`, two under `oneoff/`), so the floor for parts of `tools/` is
genuinely 3.13, while the docs still say 3.9+ / 3.10+ / 3.11+ in different places. Not this change's
to fix; noted so the next person does not read the red as breakage.

### fix(tools): the ten tools that write into the game install now honour BANNERLORD_GAME_DIR (#404)

#401 fixed the five diagnostics that only read. The tools that *write* are the sharper edge: with the
literal wrong they do not fail, they edit whichever install
`E:\Steam\steamapps\common\Mount & Blade II Bannerlord` happens to name. Seven of them write under
`TAOM_Map`, where the repo copy is a stale shadow and the live module is authoritative, so a write to
the wrong root is silent.

Each now resolves `os.environ.get("BANNERLORD_GAME_DIR") or <existing literal>` and derives its path
from that. The `or` form rather than #401's `get(VAR, default)` because an exported-but-blank variable
returns `""` and `Path("")` is `.` — #404 point 4. The literal stays as the fallback, so behaviour is
unchanged where the variable is unset.

The read/write split in the issue is 9/4; resolving every write call to its target root makes it
10/3. `clan_registry.py` only reads the game (vanilla `spclans.xml`, line 87) and writes to
`docs/reviews/_clan_registry.json` inside the repo, so it moves out of the writers.
`rebuild_translation_files.py` and `remap_stale_scene_names.py`, both listed as readers, move in:
the first writes `loc_settlements.xml` and the Armory `loc_*.xml` files straight into the install with
no `--apply` gate, the second rewrites the live `settlements.xml` under `--apply`. Of the four listed
readers only `audit_item_refs.py` (no write calls at all) and `apply_erebor_equipment_sweep.py` (reads
the install, writes `Main/_Module/.../troops_erebor.xml` in the repo) actually are ones.

Verified against a throwaway `Modules` tree holding copies of the two affected `ModuleData` folders
(515 files), with an MD5 manifest of the real install taken before and after every round: unchanged
throughout. Output is identical to the previous version both unset and with the variable
set to the tool's own literal — the only diffs were traceback line numbers, shifted by the added
lines. The control run matters more than the pass: with the fix reverted and the variable pointing at
the throwaway tree, none of the ten touch it; with the fix in place, seven write there (the other
three no-op for their own reasons — idempotent, nothing to roll back, and assets absent from this
install).

One pre-existing behaviour left alone as out of scope for this change: pointed at a root that does not
exist, `rebuild_translation_files.py` creates the whole
`Modules/LOTRLOME_Armory/ModuleData/Languages/<lang>` chain there and exits 0.
`rollback_erebor_iron_misfile.py` and `cleanup_deleted_gondor_armor.py` also exit 0 on a wrong root
while doing nothing. That is the case #404 point 4 wants a shared helper for.

### fix(troopweight): shed-on-upgrade was deleting every settlement's militia down to ~20

A player reported that every settlement held between 20 and 26 militia and stayed there, while the
settlement overlay kept showing a healthy `+5/day`. Both numbers were honest. The model really did
compute +5, and the militia really was being removed again the same day.

`Patch17_TroopWeight`'s shed-on-upgrade postfix hangs off
`PartyUpgraderCampaignBehavior.UpgradeReadyTroops`, which vanilla drives from `DailyTickPartyEvent` —
and `CampaignPeriodicEventManager` initialises that ticker over `MobileParty.All`, not over lord
parties. Militia parties are in `MobileParty.All`, so the postfix ran on all of them. A militia party
has no `LeaderHero`, so `DefaultPartySizeLimitModel.CalculateMobilePartyMemberSizeLimit` skips the
leader/clan/steward branch and returns its flat `ExplainedNumber(20f, ...)` base; `TaomPartySizeModel`
layers the culture factors on top, which is where 20 / 21 / 24 / 26 come from. Militia troops are
unlisted in `troop_weights.xml` so they weigh 1.0, `weighted == raw`, and `PlanShed` trimmed the raw
headcount straight down to that number. `Town.DailyTick` added `MilitiaChange`; the shed took it back
before the day was out.

The hook's own one-shot diagnostic had been recording it in session logs since 2026-08-03:

```
Shed 2 bodies from 'Militia of Orthanc'        (weighted 26 > limit 24)
Shed 8 bodies from 'Militia of Caras Galadhon' (weighted 28 > limit 20)
Shed 1 bodies from 'Militia of Bar-noss'       (weighted 21 > limit 20)
```

Militia settles where `MilitiaChange` reaches zero, at `40 × (2 + prosperity/1000 + buildings)`, so
Minas Tirith at 5303 prosperity should hold around 266 and was holding 26 — a tenth of its defenders,
in every fief on the map. `OnUpgradeReadyTroops` now returns early when `party.LeaderHero` is null,
beside the `MainParty` / `!IsActive` guards it already mirrors from vanilla.

That closes two quieter cases with it. Bandit parties are leaderless and drew the same flat 20, which
undid `Patch39_BanditPartySize` — that scales bandit spawn rosters toward their template maximum, up
to 50, and the next daily tick cut them back. Garrison parties were being trimmed to
`CalculateGarrisonPartySizeLimit`, which vanilla computes but never enforces as a hard cap; this
feature's own doc already called garrisons "not party-size-budgeted" when it deferred weighting their
tooltip. A settlement's own `PartyBase` reaches the hook through `MapEventEnded` and scores 0 from
`GetPartyMemberSizeLimit`, which would have made every non-hero in it sheddable.

Lord parties, the case the shed exists for, are untouched — the same logs show `Calemir's Party` still
trimming 234 bodies at weighted 465 against limit 231.

Verified: `dotnet test TAOM.Tests -c Release` returns an identical pass/fail set before and after the
guard, and `Main/TAOM.csproj` builds clean. Not verified in-game — recovery from 20 back to
equilibrium is roughly 40 game days, so expect a climb rather than a jump.
### fix(fieldcommission): promotions asked once, remembered a refusal, and stopped following you between campaigns

Players reported trouble with promoted companions. A 28-agent adversarial pass over the feature and
the 1.4.7 decompile could **not** reproduce either reported symptom — it confirmed the opposite for
the one that was checkable: a promoted companion does take the `MainPartyCompanion` branch of
`DefaultHeroAgentLocationModel.GetLocationForHero`, and `AddCompanionAction` sets `CompanionOf`, so
`Hero.Clan`, `MapFaction` and `IsPlayerCompanion` all resolve. They are talkable. What the pass did
find was a pile of real defects around the offer flow, and the most likely thing a player would
describe as "I can't deal with my promoted companions" is the first one below.

**One won battle could raise dozens of prompts, and there was no cap.** `EndBattle` queued
`min(rosterCount, merit / threshold)` offers for *every* troop type that scored a kill, and the tick
pump shows each one as a separate modal inquiry with the game paused. A large winning battle with a
mixed party therefore produced a queue of dialogs with no way to dismiss them in bulk. The donor mod
capped this at one per battle; the port dropped the cap. `maxOffersPerBattle` (default 1) restores
it, capping the battle rather than the troop, and the scan deliberately keeps running once the budget
is spent so later troop types still bank their kills.

**Declining did nothing.** Merit is never spent on a refusal — correct, and a deliberate fix to a
donor bug — but merit only ever grows and the queue condition is just `bank >= threshold`, so the
same soldier was proposed again after every won battle, forever. A per-troop decline mark now
requires another full threshold before that type may ask again. It costs the soldiers nothing, and
accepting clears it. The mark travels with the merit: when a stack upgrades and its bank moves to the
heir, the mark goes too, or the old type could never be offered again.

**The offer queue outlived the campaign.** It lives on a `Reuse.Singleton` service, is not persisted,
and was cleared nowhere — so an offer still queued when you left a campaign surfaced in the next save
loaded in the same session, proposing a soldier you do not have. The "an offer is on screen" latch
had the same lifetime and only ever came down inside an inquiry callback, so any path that skipped
one wedged the pump for the rest of the process. Both are now cleared at every session boundary,
ahead of the load guard, while the persisted bank is left alone.

**A promotion could conjure a companion from nobody.** `CreateCompanionFromTroop` never checked the
troop was still in the roster, so if the last of that type died between the offer and the answer, the
hero was created anyway, merit was deducted, and `RemoveOneFromRoster` returned `false` into a
discarded variable. The precondition is back where the donor had it, and the decrement result is
logged if it ever fails.

**The promoted-hero list emptied itself on every load.** `IsHeroAliveAndValid` resolved through
`MBObjectManager.GetObject<Hero>`, which cannot resolve these heroes at all —
`CampaignObjectManager.AddHero` hand-assigns `hero.Id` and never calls `MBObjectManager.RegisterObject`,
so every hero `HeroCreator` builds at runtime looks invalid to that lookup. The load-time prune
therefore dropped all of them. Now `Hero.AllAliveHeroes`, matching every other adapter in the repo.

Also: born settlement falls back through the player's settlement, then a town of the soldier's own
culture, then any town, instead of leaving it null on the world map for the engine to pick at random;
troops with no civilian equipment set (about 60, including every Dale troop) wear their own battle
kit in town instead of vanilla's Calradian peasant tunic; focus points are clamped to
`MaxFocusPerSkill`; upgrade-target walks skip heroes so merit cannot be parked under a hero id and
stranded; and the offer pump waits while the player is a prisoner.

**Not found, and worth stating plainly:** no cause for "no dialogue" or "crash on interaction" was
proven in this feature. Eleven further hypotheses were refuted against the decompile. The new
`[FieldCommission]` diagnostic trace (MCM: Battlefield Promotions → Promotion Diagnostics, off by
default) exists so the next report arrives with the answer in the log rather than another round trip.

### feat(fieldcommission): an MCM group, and an off switch that actually turns it off

"Battlefield Promotions" (GroupOrder 43): a master toggle plus offers-per-battle, fair-fight ratio,
merit per kill, merit to promote, companions-over-the-limit, and the diagnostics switch. Off is fully
inert — no merit accrues, no offer is queued or shown — and fully reversible: banked merit stays in
the save and companions already promoted are untouched.

`FieldCommissionSettingsProvider` implements `IFieldCommissionConfigProvider` and decorates the JSON
provider rather than exposing a parallel scalar surface like TAOM's other settings providers. Two
reasons, both load-bearing: every consumer already reads several fields off one config object, so no
constructor changes; and `GetConfig()` runs on the campaign tick, so the merged config is cached
against a `FieldCommissionMcmSnapshot` value-equality key and allocates nothing while the sliders are
still. Registration goes through `RegisterDelegate` with the dependency spelled out, so the decorator
cannot resolve to itself.

Every compiled MCM default has to equal its `field_commission_config.json` counterpart — a player
without MCM reads the JSON, a player with MCM at default reads the literal, and those two must
describe the same game. A test fails the build if they drift, which is the kind of split that is
otherwise invisible because the test host never has MCM loaded.

### fix(enlistment): serving no longer makes you MORE likely to die

The most serious thing the *Serve as Soldier* comparison surfaced, and it was silent.

`DefaultPartyHealingModel.GetSurvivalChance` reads every survival bonus off the party it is PASSED —
`AddSurgeonSurvivalBonus(mobileParty, …)`, the `PhysicianOfPeople` perk, and
`HasPerk(Medicine.CheatDeath, checkSecondaryRole: true)`. An enlisted player's party is one hero,
parked and hidden, with no surgeon and no perks. So a soldier going down in his commander's battle
was rolled as if nobody were there to save him — **more likely to die than the same character
freelancing**, with nothing on screen to say so.

The roll now resolves against the commander's party: the company that is actually tending you. It is
narrow by construction — the character must be the player, the party must be the player's own, and
service must be live, with any failure returning the original party untouched, and it can only ever
substitute a party (never hand back something worse). SAS needed a Harmony patch plus a
`[ThreadStatic]` flag and a `PartyBase.get_MapEvent` postfix to achieve this; TAOM already owns
`TaomPartyHealingModel`, so it is eleven lines in a GameModel and no new patch surface.

**Healing now scales, because a flat rate made two systems inert.** +11/day regardless of Medicine
meant a physician and a farmhand recovered at identical speed — so the skill did nothing in service,
and neither did the surgeon duty. Now `11 + Medicine/10`, doubled while the commander's column is
resting in a settlement (read from the COMMANDER, since the column is what rests and a following
player is wherever it is).

For scale: SAS pays ~28.8 HP/day in the field and ~86.4 resting. TAOM at Medicine 100 now gives 21,
or 42 resting — deliberately below SAS, because TAOM also feeds the player and SAS does not (it has
no food handling at all and simply out-heals the -19/day starvation branch).

Suite 6089 green.

Research: DefaultPartyHealingModel.GetSurvivalChance bonus sources
### fix(enlistment): wait-menu options grey out with a reason instead of vanishing

From a comparative read of the *Serve as Soldier* mod, whose changelog records this as a fix for a
problem they SHIPPED and then hit in play:

> changed the travelling menu options to always display, but become greyed out if they can't be
> performed; should stop aggravation from menu options constantly shifting position, particularly at
> higher game speeds.

TAOM had the same bug. "Speak with your commander" returned `false` when unavailable, which REMOVES
the row — so every option below it slid up under the player's cursor, worst at high game speed where
availability flips constantly. It now stays in place, greys out, and says why: not mid-battle, not
while away on orders, not when the commander is unreachable, not from off the map.

A disabled option that explains itself teaches the rule. A vanished one just looks broken.

`MenuCallbackArgs.IsEnabled` and `Tooltip` verified as public fields on installed 1.4.7 before relying
on them. Five reason strings registered and translated into all 12 languages (165 keys each,
verified per language rather than assumed).

Suite 6087 green.

Research: MenuCallbackArgs.IsEnabled/Tooltip
### feat(enlistment): the company looks after its own — food, morale and a surgeon

Extends today's starvation fix into the whole bargain of enlisting: while you serve, your keep is
the commander's problem, not yours.

| Upkeep | Why |
|---|---|
| Rations topped to a 3-day floor | `DefaultPartyHealingModel` returns **-19 HP/day** for a starving hero with no settlement. A one-man party parked in a field runs out, and a player hit 19% HP with no recovery. |
| Morale raised to 40 (never lowered) | Below 25 an attached party counts as "low morale" in `CalculateCohesionChangeInternal` and drags the whole army's cohesion. This is upkeep for the COMMANDER as much as comfort for the player. |
| Surgeon heals +11 HP/day | Matches vanilla's mobile-party baseline, so serving is never worse for your health than marching alone. Explicit rather than a side effect, because the engine's healing path was never written for a hidden, inactive, one-man party. |

All three are gated on actually being enlisted — guarded in the service rather than trusting the
caller, since every one of them grants a resource and a discharged player drawing rations from a
lord they no longer serve is a supply exploit nobody would notice. A test caught exactly that.

Morale RAISES only. A player who has earned high morale is never pulled down to the floor.

### The influence question, answered

Ahead of real army membership: an enlisted player joining the commander's army costs the commander
**nothing in influence**. `Army.OnAddPartyInternal` guards the charge with
`mobileParty != MobileParty.MainParty`, so the call-to-arms cost is skipped outright for the player
— the same-clan exemption that makes companion parties free never even needs to fire.

The real cost is **cohesion**, and two of its four drivers are things we can remove: starving
parties and low-morale parties. Both are now handled above. What remains is structural — a
one-man party always trips the "10 or fewer healthy members" term, and every party costs one point
of party-count — for roughly -0.375 cohesion/day on an AI-led army. Documented rather than
accepted silently; a join gate that skips armies already near the dispersion threshold is the
next step if that proves to matter in play.

**Unverified:** `DailyBeingAtArmyInfluenceAward` computes an influence award for army members, but
its consumer is not in the CampaignSystem decompile. If it pays out, serving earns influence that
partly offsets the cohesion drag — not relied upon until confirmed.

Suite 6087 green.

Research: Army.OnAddPartyInternal main-party guard, CalculateCohesionChangeInternal drivers
### fix(enlistment): the commander feeds his soldiers — enlisted players were starving

Reported in-game at 19% HP and not recovering. `DefaultPartyHealingModel` explains it exactly: a
mobile party heals its heroes **+11 HP/day**, *but*
`if (party.IsStarving && CurrentSettlement == null) return -19f` — a starving hero **loses** 19 HP a
day. An enlisted player is a single hero parked in the field for days with whatever food they
happened to be carrying. When it runs out they stop healing and start dying.

Mechanically the player is not in the commander's party, so the baggage train that feeds every
other soldier in the column does not feed them. The daily tick now tops their rations back up to a
three-day floor. A floor rather than a handout, so it cannot be farmed as a supply source, and a
discharge leaves a couple of days' food rather than a full larder.

This was a KNOWN unresolved question, not a surprise: `docs/features/enlistment.md`'s in-game
checklist listed *"food/wage/morale ticks for inactive MainParty"* as owed and never run. It has
now been run, by a player, at 19% health.

Suite 6084 green.

Research: DefaultPartyHealingModel.GetDailyHealingHpForHeroes starving branch
### fix(enlistment): stop teleporting the player between towns when the commander is in an army

Reported in-game as "still being left behind". The log said otherwise — `SYNC ok (drift 0.00)` on
every one of 126 syncs, so the player was sitting exactly on the commander's position the whole
time. What was actually happening was worse: **the player was being teleported between three
different towns, seconds apart** — `FOLLOW town_EW1` → `EXIT` → `FOLLOW town_EW2` → `EXIT` →
`FOLLOW town_EW3` → `EXIT`, repeatedly.

`CommanderSnapshot` was reading settlement identity from two different sources:
`partyIsInSettlement` from the commander's **party**, but `settlementId` / `settlementName` /
`settlementMenuId` from the **hero**. `Hero.CurrentSettlement` resolves through `PartyBelongedTo`,
so the moment a commander joins an ARMY it reports the ARMY's settlement while his own party is
elsewhere. `Assess` then followed the player into the hero's town, compared the two ids on the next
tick, found them different, ordered an exit — and repeated. All four fields now come from the party,
which is the thing that has a position and therefore the thing being followed.

**The diagnostic built to answer this question was itself blind.** `GetPresence` takes
`commanderHeroId` with a default of `null`, and `distanceToCommander` is hard-wired to `-1` when it
is absent — but the reconciler called the parameterless overload, so `distToCommander=?` printed on
every line of the first live session. It now passes the id, and the commander's settlement joins the
trace, so the next report of this shape is a grep rather than an investigation.

Two regression tests: one pins that every settlement field in `GetSnapshot` reads the party and
never `hero.CurrentSettlement`; one pins that following into the commander's settlement TERMINATES
rather than oscillating.

Also: the wait menu's lowest trust band said *"in poor odour"* — a real English idiom, and a poor
choice for a game UI. Now "badly thought of".

Suite 6081 green.

Research: Hero.CurrentSettlement resolves via PartyBelongedTo
### fix(enlistment): put "ask to be released" last, where a terminal action belongs

First live look at the finished wait menu (2026-08-08) confirmed three batches at once — the player
standing INSIDE Minas Tirith rather than outside its gate, the status text naming the settlement
instead of repeating one sentence for the whole term, and the conversation option reaching the
quartermaster, who had never issued a single kit to a player because nothing could open a
conversation with the commander.

It also showed an ordering mistake: *Ask to be released from service* sat SECOND, directly above the
two options a serving soldier uses constantly, and it carries the back-arrow icon — which reads as
"back", not "end my career". Moved to last, where vanilla puts its own leave option, with the
reason recorded beside it so it does not get tidied back.
### feat(localization): enlistment ships in 12 languages, including the 84 keys a grep cannot find

The enlistment strings were registered but not in the pipeline: `taom_enlistment_strings.xml` was
absent from both `generate_translation_template.py` and `translate_with_claude.py`, and
`LanguageDataXmlTests` pinned 10 language files per directory. `/localize` would have translated
nothing here — 66 keys shipping as English fallbacks in all 12 languages, silently, with no error.

**The half that mattered more was invisible.** `InteractiveDutyPresenter` builds its keys at RUNTIME
from data-row ids (`"taom_enlist_duty_" + id + "_" + suffix`), so the literal `{=key}` grep that
discovers every other TAOM string finds none of them — 14 duty/incident rows × 6 suffixes = 84 keys
that could never have been translated by hand-maintenance. `tools/generate_enlistment_duty_strings.py`
generates them, lifting the English from the three places it actually lives: the `DutyCopy`
dictionary in C#, `Humanize(option.Key)` over the JSON option keys, and the two shared result
toasts. Idempotent, so hand-tuned copy survives a re-run.

**160 keys × 12 languages, verified per language rather than assumed** — entry count and
translated-vs-still-English counted against the English source. All 12 at 160/160.

An ordering trap, hit and now documented in the generator and the translator guide:
`generate_translation_template.py --apply` **overwrites** each per-language file with a fresh
English template. Registering the 84 duty keys after translating meant regenerating to pick them
up, which blanked all 12 files. The cache made recovery free (re-run, everything returns from
cache, $0) but the correct order — register every key, THEN generate, THEN translate — saves the
round trip.

Also worth knowing: the translator exits 1 if ANY entry fails validation. Four pre-existing
vanilla-derived strings with nested gender conditionals (`{?TARGET_HERO.GENDER}lady{?}lord{\?}`)
fail on every language that has not cached them, so a non-zero exit is not automatically a problem
with your own strings. Zero enlistment entries failed.

$2.91, 1,980 LLM translations. Suite 6079 green.
### docs(enlistment): record the review pass, the surfaces, and what is still owed

`docs/features/enlistment.md` now carries the 2026-08-08 review pass — the four terminal defects
with the engine facts that make each terminal, not just the fix — plus a table of every
player-facing surface and where it lives, and why `Presentation/` exists separately from `Hooks/`.

Six lessons appended across four categories, and the index recounted: **368**, up from a printed
352 that had drifted. The counts were re-derived with `grep -c '^### '` rather than incremented,
which is what the index's own note says to do.

The lessons worth reading even if you never touch this feature:

- **A plausible engine root cause is still a hypothesis.** Every link in the frozen-menu-text chain
  was true and the conclusion was backwards — the comparison it blamed returns `0 != null`, which is
  always TRUE, so the menu was re-rendering every frame rather than never. Verify the conclusion,
  not just the links, and compile the language rule when one is load-bearing.
- **`grep -c` exits 1 on zero matches**, so `build | grep -c error && next-step` silently skips the
  next step on a CLEAN build. Check exit codes; a grep's silence is not evidence.
- **Never `pull --rebase` over another session's uncommitted work** — and when you must, snapshot
  hashes first, `apply` (never `pop`), and verify. The autostash did not pop here, which is the same
  failure that lost work the previous morning, arriving through the flag meant to prevent it.
- **Twelve defects, zero caught by 668 green tests.** A mock at the adapter boundary is a decision
  to stop testing at exactly the seam where engine-contract bugs live.

CLAUDE.md's Enlisted-service trap row now routes two soft-lock symptoms (stuck in a settlement, a
battle that never resolves) to their causes, within the 400-char row budget. The feature-map row
and the testing section list what has and has not run in a live game — the seven in-game cases
owed are each a state a test structurally cannot reach.
### fix(enlistment): two terminal soft-locks, a frozen battle, and a latch that never recovered

A five-agent deep review plus an adversarial Codex pass over batches 0–10. Twelve findings, all
fixed. The two that mattered most were both terminal and both survived save/reload.

**Discharge could strand the player inside a settlement, permanently.** Placement was decided
purely from where the COMMANDER is, and never asked where the PLAYER is — so a discharge while the
player stood in a town and the commander did not (dead, in the field, in a hideout) skipped the
settlement branch entirely and then closed the wait menu. That leaves `CurrentSettlement` set with
no menu at all, and it does not recover: `DoUpdatePosition` refuses to move a party in that state,
`CheckExitingSettlementParallel` explicitly skips the main party, and the menu the engine
re-pushes for a fortification is `town_outside`, whose Leave calls `PlayerEncounter.Finish()` —
which returns immediately when `Current` is null and never reaches its own `LeaveSettlement()`.
For a village the engine pushes nothing. Every recovery loop in the feature early-returns on
`NotEnlisted`, which the record now says.

**A save taken mid-battle could freeze that battle forever.** `EnlistedBattle` is coerced to
`EnlistedAttached` at save time, so on reload the state says "attached" while the engine still has
the player in the map event and restores `GameMenuId = "encounter"` — which the menu redirect then
swallowed. `MapEventManager.Tick` deliberately skips the player's own map event; it advances only
through `PlayerEncounter.Update`, driven from that menu. Battle menus are now exempt from redirect
whenever the player is actually in a map event.

**A duty starting from inside a settlement made the player invisible for days.** The exit path
ended in a park — correct while following the column, catastrophic on a duty, because nothing
un-hides a party while the state is `EnlistedDetachedOnDuty` (`Assess` returns Blocked for it, so
neither the reconciler nor the pump ever restores presence). Duties now have their own exit that
ends visible.

**The wait-menu guard could switch itself off for the rest of the process.** The back-off check sat
ABOVE both of its reset sites, so once three transient failures hit the cap neither reset was
reachable again — on a singleton, across re-enlistment and across campaigns.

**The real-time budget was frame-rate dependent.** The wait-menu tick is a FRAME tick, and it was
feeding the pump a constant 1/30s per callback — fabricating ~4.8 seconds of budget per real
second at 144 fps, running the "4 Hz" expensive tier at ~19 Hz and the status board, which uses
the explicitly forbidden `GetSnapshot`, at ~2.4 Hz. It now measures real elapsed time, clamped so
a stall cannot burst the budget.

Also: a NaN `CommanderGraceDays` made `nowDays >= GraceEndsAtDay` false forever, so grace never
expired and the player sat in `CommanderUnavailable` with no auto-discharge — the sixth instance of
that bug class, caught before shipping this time. A commander-party handle cached across a game
load matched by StringId and drove the position sync from a destroyed campaign's party. Deferred
duty callbacks granted rewards without re-checking authority or enlistment. `RestorePresence`'s
result was discarded before encounter work that its own comment called a hard precondition.
`StaleBeforeCommanderBattle` was defined but never called, so a leftover encounter burned the
immediate join. And the promotion toast still printed a raw enum name.

Suite 668 green.

Research: MobileParty.DoUpdatePosition, CheckExitingSettlementParallel, MapEventManager.Tick, GameMenuManager.OnFrameTick
### refactor(enlistment): put the presentation layer where it belongs, and stop duplicating strings

A standards pass found six entry points at or over ADR-002's 150-line ceiling — three of them
introduced by this arc, including one *created* while splitting a different 218-line violation.

The root cause for two of them was placement, not size: `EnlistmentWaitMenuPresenter` and
`ServiceStatusTextWriter` are registered singleton services, not entry points, and were sitting in
`Hooks/` where the ceiling applies. They now live in `Presentation/`, which resolves both the
misfiling and the apparent breach in one move. `EnlistmentMenuBehavior` (162) shed its option list
to `EnlistmentWaitMenuOptions` — the behavior's job is lifecycle, the option list is a separate and
growing concern.

`ServiceVocabulary` now owns every enum→player-words mapping. Section names had been written twice
— once in the reassignment dialog, once in the status board — as identical ladders over the same
localization keys. Two copies of one key set do not stay in sync, and the failure is silent: the
dialog says "baggage train", the board says something else, and nothing errors.

Four entry points brought under the ceiling. Two remain over and both pre-date this arc:
`EnlistmentMeritMissionBehavior` (163, and it holds a 44-line geometric scoring algorithm inline
that should be a testable engine) and `EnlistmentBattleBehavior` (157).

Enlistment suite 659 green.
### fix(enlistment): register the 40 strings that would have shipped untranslatable

An audit of every `{=key}` literal and every `IInquiryAdapter` key/fallback pair against
`taom_enlistment_strings.xml` found 36 keys used in code and registered nowhere — the entire
release/desertion flow, the status board, rank and section names, trust bands, and both new menu
options. A key that is never registered cannot be translated into any of the 12 languages; it ships
as its English fallback forever, silently, with no error anywhere.

Also fixed a live English-language defect this audit turned up. Merit bands carry a `GradeKey` that
is an internal id, and it was being substituted into the after-battle toast raw — so the player
read *"The sergeants noted your conduct: distinguished"*, lower-cased engine data dropped into the
middle of a sentence. Four localized grades now, with an unknown key falling back to the mildest
real grade rather than echoing the id.

62 enlistment keys registered, XML validated.
### feat(enlistment): a wait menu that tells you what is happening, and two edges that beat the tick

Batches 9 and 10. The wait menu showed one sentence — *"You serve in X's company"* — unchanged from
oath to discharge. It now shows where the column is and by name, your rank and section, days
served, standing, any pay owed, and your current orders.

**The planned root cause for this was wrong, and verifying it saved the work.** The design said
`GameMenuVM.IsMenuTextChanged` compares `Attributes`, that a global text variable never lands
there, that the text was therefore structurally frozen, and that the fix was
`args.MenuContext.GameMenu.GetText().SetTextVariable(...)`. Read against the installed v1.4.7, the
method compares `_menuTextAttributes.Count` (an `int`) with `_menuText?.Attributes?.Count` (an
`int?`). Our menu text is built by `new TextObject(text)` with no attributes, so that comparison is
`0 != null` — always TRUE under C#'s lifted equality, confirmed by compiling and running it rather
than reasoning about it. The menu re-renders every frame and re-resolves the global each time.

Nothing was frozen. `RefreshWaitText` simply ran once at menu init and its only token was the
commander's name, which does not change all term — nothing new was ever computed. So the fix is
cadence and varying content, and the proposed plumbing would have been real code aimed at a bug
this menu does not have.

The board rebuilds every 2 seconds and pushes only when the value actually differs. That equality
IS the throttle, so a field omitted from `Equals` is a status line whose changes never reach the
screen — one `DataRow` per field guards it.

Batch 10: the reconciler gained a re-entrancy guard, and it is not theoretical. Settlement
following made the reconciler call `LeaveSettlementAction`, which dispatches `OnSettlementLeft` —
the very edge this batch subscribes. Without the guard it re-enters itself mid-pass, on a record it
is halfway through mutating.

Two re-attach edges subscribed, not the donor's six. The pump already re-derives everything within
250 ms, so an edge earns its place only by firing where the pump is silent — and
`CampaignEvents.TickEvent` does not run inside encounter or settlement menus or a map conversation.
A commander walking out of a town while the player sits in a menu is exactly that gap. The other
four only re-arm a flag whose own retry budget is an hour anyway; declined deliberately, and
recorded as a decision rather than an oversight.

Near-miss worth recording: the status budget was first written outside the pump's NaN guard, one
missing pair of braces from being the sixth shipped instance of that bug class. A NaN `dt` would
have wedged the board permanently.

Enlistment suite 659 green.

Research: GameMenuVM.IsMenuTextChanged, MBTextManager vs TextObject.SetTextVariable, CampaignEvents.OnSettlementLeftEvent
### feat(enlistment): make the enlisted dialog surface reachable, and add a master off switch

Batch 8. Reassignment, the quartermaster and in-person discharge were all shipped, all tested, and
all unreachable — nothing in the feature could open a conversation with your commander, so no
player had ever seen any of them. The wait menu now has *Speak with your commander* and *Ask your
sergeant for work*.

`isLeave` stays false on both, and that is load-bearing rather than tidy:
`GameMenu.RunMenuOptionConsequence` calls `EndWait()` **before** the consequence when an option is
marked `isLeave` on a wait menu, and `EndWait` sets `IsWaitActive = false` and
`TimeControlMode = Stop` — tearing down the tick that drives our position sync before the
conversation ever opened. It would also make the option a candidate for the Escape slot.

`ConversationManager.OpenMapConversation` opens with
`(GameStateManager.Current?.ActiveState as MapState).OnMapConversationStarts(...)` — an `as` cast
dereferenced with no null check, so it throws when the state manager is null, when the state stack
is empty, or when the active state is anything other than a MapState. Three routes to one NRE,
none guarded; `CampaignMapConversation.OpenConversation` adds a fourth on `Campaign.Current`. The
adapter carries that guard itself, because one guard in one place is the only version of it that
cannot be forgotten at a new call site.

`TalkToCommander` re-checks its own gate. A menu option's condition and its consequence are a
frame apart, and a commander battle starting in that gap is exactly when acting on the stale
answer costs the player the fight.

Asking for work shares ONE offer path with the daily tick and the same rotation cadence — asking
cannot conjure work the rotation would not have given, so the option cannot be farmed.

**MCM: Enlistment can now be switched off entirely.** Off removes the enlist option, and anyone
already serving is released honourably on the next tick rather than left parked. That release is
not politeness: an enlisted player is hidden and inactive, and the code that restores them is the
code being switched off, so halting in place would strand them invisible on the map with no menu —
a soft-lock produced by a settings toggle. Fails open when MCM is absent.

**Reassignment fix (reported in-game):** there was no cavalry option. Each option is hidden when it
is your current role, and nothing told you what that was — so a player already in the horse saw
three choices and concluded cavalry was missing. The commander now answers first and names your
section. That also restores the player → NPC → player alternation every working dialog chain in
this feature has and reassignment did not.

**Two defects of my own, found by audit and fixed here:** `EnlistmentDialogBehavior` had reached
218 lines against ADR-002's 150 ceiling after Batch 6 — the release chain is now its own behavior;
and `taom_enlist_release_desert` was being used for two different English strings, which would
have translated to whichever one happened to be registered, in both places.

Suite 5977 green.

Research: ConversationManager.OpenMapConversation, GameMenu.RunMenuOptionConsequence, ConversationCharacterData ctor
### feat(enlistment): follow the column into town instead of standing outside the gate

Batch 7. When the commander's column entered a settlement the player stayed parked outside it,
invisible, for the entire stop — `MoveIntoSettlement`/`LeaveSettlement` existed on the adapter and
`CommanderSnapshot` carried the settlement fields, but nothing had ever called them. Two new
attachment verdicts wire them up: follow him in, and leave when he leaves.

**The player is placed inside, but held in the TAOM wait menu throughout** — never handed to
vanilla town flow. Actually *using* the town while enlisted is a different feature: every
in-settlement affordance routes through `PlayerEncounter.LocationEncounter`, and a live
`PlayerEncounter` while `EnlistedAttached` is precisely what the reconciler's sweeper destroys.
That needs its own state. `town`/`castle`/`village` join the redirect list, gated on
`EnlistedAttached` — so discharge restores normal town access with no teardown, and duties, which
run detached, can still send the player into a settlement to do a thing.

**Exit is checked ABOVE the battle branch, and that ordering is the point.** Joining a map event
while `CurrentSettlement` still points at another settlement puts the party in two places at once,
and `MapEvent.AddInvolvedPartyInternal` rewrites a siege ASSAULT to `SiegeOutside` off exactly that
field for a joining defender — turning an assault on the walls into a field fight for everyone in
it. Being inside the settlement the commander is DEFENDING is the correct state and is exempt,
which is why this batch improves siege joins rather than risking them.

`FollowCommanderIntoSettlement` is one transaction — presence, move, menu — and rolls back out if
the menu will not open. A settlement placement without our menu IS the donor's crash state:
`game_menu_settlement_wait_on_init` opens by dereferencing `PlayerEncounter.EncounterSettlement`.

Two exemptions ship alongside, or a correct settlement stop reads as a fault every hour: the
reconciler skips both the position sync and the "NOT parked" warning while the player is inside a
settlement (they are deliberately active and visible, and the engine owns placement), and
`OnWaitMenuInit` stops unconditionally re-parking — which would have yanked the player straight
back out of the town they just followed him into, on the very menu the follow asserts.
`_AttachedNotParkedOutsideSettlement_StillWarns` proves the exemption did not swallow the real
anomaly.

Two gaps found while wiring rather than from the plan: the maintenance pump gated its wait-menu
assertion on `LooksParked` alone, so it would have abandoned the menu for the whole settlement
stop; and a duty starting while inside the commander's town left the player detached but still
held at the gate, since a detached state is `Blocked` in `Assess` and never reaches the exit
verdict.

Suite 5946 green.

Research: MapEvent.AddInvolvedPartyInternal siege rewrite, EnterSettlementAction.ApplyForParty pushes no menu
### feat(enlistment): asking to leave is a choice you can see the price of

Batch 6. "Ask to be released" used to discharge you the instant you clicked it, with no
confirmation and no statement of cost. It now runs through a gate with three answers, and the
refusals carry the number that makes them actionable: *"{DAYS} more days are owed. Leaving now is
desertion: you forfeit the pay still owed to you."* — with a real integer, in the popup and in the
commander's own dialog line.

The refusal is keyed to a new `MinimumServiceDays` (21), deliberately NOT to `ContractDays`. The
contract is a year, so keying it there would refuse every realistic request and leave desertion as
the only exit — the bug fixed in Batch 1 wearing a different hat. A term you can actually serve is
what makes this a choice instead of a trap.

Desertion now has exactly one producer: a branch the player picked after being told what it costs.
Nothing classifies a leave as desertion behind their back.

Every degenerate input falls OPEN to granted — no start day recorded, a NaN clock, a poisoned
`MinimumServiceDays`. The failure this gate can cause is a player locked in service with no exit,
which is strictly worse than releasing someone a day early, so it is built to let people go.

The confirmation callback re-checks co-op authority. It runs on a LATER frame than the menu option
that already checked, so that check does not cover the moment the discharge actually runs.

`EnlistmentMenuBehavior` sheds two constructor dependencies as the decision moves out of it, and
stops calling `ExitToLast` — `DischargeService` owns the menu exit since Batch 5, and calling it
here too would stop campaign time on the refusal paths, where nothing was discharged at all.

Suite 5917 green.
### feat(battleload): split the 11.9-second load gap into three named buckets, and add engine memory attribution

`MissionInitialize → MissionAfterStartBegin` held 11.9 seconds of a measured 29-second battle load
with no instrumentation inside it — the largest unattributed span in the lifecycle. The engine
decomposes it into exactly three things (`MissionState.cs:221-350`), and three new markers name them:
`MissionInitializeDone` brackets the native `MBAPI.IMBMission.InitializeMission` call, and
`FinishMissionLoadingBegin`/`Done` bracket the private `MissionState.FinishMissionLoading`. There is
now no unattributed span between `MissionInitialize` and `BattlePlayable`.

The async wait in the middle is measured by a **counter hook that never logs**. `MissionState.TickLoading`
runs once per frame during a load, so at 60 fps a 12-second wait is ~720 calls; its prefix does one
`Interlocked.Increment` and the result rides a single `polls=`/`waitMs=` token pair on
`FinishMissionLoadingBegin`. That pair separates two diagnoses that were previously indistinguishable:
`polls=1` with a large `waitMs` means the main thread was **blocked inside one frame** (a native spin,
the #352 `WaitForMeshesToBeLoaded` shape), while `polls ≈ waitMs/16` means genuine async streaming
with a healthy main thread. `polls=0` means the binding failed — not that there was no wait, and the
feature doc says so. All three markers carry the `MemStats()` tokens, so the `privMB` curve *across*
the stall answers whether the load stall and the process-commit growth are one problem or two.

Also added `taom.print_memory [label] [gpu]` (Tier A, cheat gate) over
`TaleWorlds.Engine.Utilities`' memory statics, for the per-station rows of the commit-attribution
matrix in `docs/investigations/native-commit-audit-2026-08.md`, whose runbook this fills in.
`GetMemoryUsageOfCategory(int)` is deliberately **not** called: there is no category-count or
category-name API in either build and the index goes straight to native unvalidated, so a blind walk
is an access-violation risk inside a diagnostic. The two statistics strings get read first, and a
numeric probe gets built only if they turn out not to carry the breakdown. `MemoryPressureSampler.Start()`
moved to `OnBeforeInitialModuleScreenSetAsRoot` so a **main-menu** `[MemSample]` baseline exists —
the A-vs-B menu delta is the audit's decisive number, and no console command can produce it
(`RunAnywhere` still answers "Campaign was not started." before a game loads).

`tools/triage_battle_load.py` learned the three markers, which fixed a wrong verdict as well as
adding a report section. Its `classify()` knew only `MissionInitialize`/`BattleSceneSelected` as
`SCENE` and let every other phase fall through to `PRE_SCENE`, so a log ending at
`MissionInitializeDone` — a freeze in the native async load wait — was reported as *"froze very early
… before scene selection"*, aiming triage at the opposite end of the lifecycle. The two mid-window
markers now map to `SCENE` and `FinishMissionLoadingDone` to `POST_EQUIP` (loaded, first
`OnMissionTick` never came); the verdict kinds and exit codes are unchanged. A `Load timing` section
plus a `timings` JSON key report the six buckets the Phase-2 runbook records, the dominant one, the
`polls=`/`waitMs=` pair and the `privMB` trajectory. Spans are **gaps between markers, never
absolute `t=+` values** — the stopwatch is `IsRunning`-guarded, so a chained second mission inherits
the first's origin and only gaps stay comparable — and an unmeasured value is absent rather than
zero (`waitMs=<not observed>`, `?`), mirroring the C# side's refusal to fabricate one. Per RCA gap
#8 (a trailing token broke a near-identical regex on 2026-08-02, and the recorded mitigation is a
fixture rather than reasoning about the regex) every new phase is pinned with and without the
`MemStats()` suffix, and the `polls=87 waitMs=1449` / `polls=87` literals are pinned as twins of
`BattleLoadDiagnosticsService.FormatFinishWaitDetail`.

Two pre-existing defects fixed along the way, both load-bearing for the measurement:

- **The stopwatch never ran in a custom battle.** It was started only from `LogEncounterStart` /
  `ResetLifecycle`, both reachable only from the campaign-only `PlayerEncounter_Start_Patch` — so
  every `[BattleLoad]` line in a custom battle read `t=+0ms`, in the exact station the matrix uses.
  The existing `IsRunning`-guarded restart idiom now also runs from `LogMissionOpenNew` (the
  universal funnel) and `LogMissionInitialize`, leaving campaign deltas unchanged.
- **`BattleLoadPhaseBehavior` was registered only while the toggle was on**, despite being the
  loading window's only closer against an opener in `Mission.Initialize`'s prefix. The 2026-07-06 RCA
  deferred this as "the same synchronous call chain"; the bucket measurement disproves that — the two
  evaluations are separated by a tick boundary and ~11.9 s, and a toggle flipped inside that window
  latched the window open until the next `Mission.Initialize`, after which the stall watchdog fired
  at 300 s and wrote a spurious bundle.

**Known limitation:** the `t=+` origin is now inherited by a second mission in the same process, so
absolutes grow across chained missions. Gaps stay valid — read deltas, never absolutes.

Patch43 went 14 → 17 hooks (four private engine methods bound by string; the category's apply is
already try/catch-guarded). 44 tests added, all verified able to fail by mutating the three
load-bearing rules they guard.

Not-tested: the Harmony prefix/postfix bodies and the four `TaleWorlds.Engine.Utilities` statics
(native — everything downstream of them was extracted into a pure formatter precisely so it could be).
Research: `MissionState.TickLoading` / `FinishMissionLoading` / `Mission.Initialize` /
`TaleWorlds.Engine.Utilities`, all against the installed v1.4.7.

### fix(tools): five audits honour BANNERLORD_GAME_DIR instead of one machine's install path (#400, #401)

`audit_battle_scenes`, `audit_mount_parity`, `audit_scene_names`, `validate_all_troop_refs` and
`validate_gondor_refs` resolved the game from a literal `E:\Steam\steamapps\common\Mount & Blade II
Bannerlord` and took no path flag, so on any other install they exited on the first missing path — not
degraded, unrunnable. Each now reads `BANNERLORD_GAME_DIR`, the variable `README.md` already lists as a
prerequisite and `setup-dev-env.ps1` already sets, keeping the literal as the fallback. The shape is
copied from `verify_mount_assets.py` rather than invented.

Verified in both directions before merge: all five produce byte-identical output to the previous
versions with the variable set and with it unset, and all five change behaviour when pointed at a
nonexistent root — so the variable is load-bearing, not decorative.

Thirteen top-level `tools/*.py` still carry the literal with no override at all, nine of which write, and
the knob has drifted into four different variable names — filed as #404. This closes the five that could
not run at all.

Credit: found and fixed by @davrodwconnections in #401, verified independently here before merge.

### fix(namedcompanions): give named companions a home settlement so MapFaction resolves

A player reported the Erebor tournament crashing over and over. Crash bundle `d7d9f7d3`
(TAOM v2.0.18.0, Bannerlord v1.4.7.117484) is a `NullReferenceException` in
`SandBox.ViewModelCollection.Tournament.TournamentVM.OnTournamentEnd`, reached from
`ExecuteSkipRound` → `TournamentBehavior.SkipMatch` → `EndCurrentMatch` → the `TournamentEnd` event.
The line is `TournamentWinner.Character.ArmorColor1 = heroObject.MapFaction.Color` — the winner's
`MapFaction` is null.

**Named companions have no map faction at all, and never have.** `Hero.MapFaction` returns null when
`Clan`, `HomeSettlement` and `PartyBelongedTo` are all null, and all eighteen named companions hit
every branch:

- `characters/heroes.xml` gives each of them `faction="Faction.neutral"`, and `Hero.Deserialize`
  reads the reference then does `if (clan.StringId != "neutral") this.Clan = clan;` — so `Clan`
  stays null by design.
- `Hero.Deserialize` never assigns `BornSettlement`; only `HeroCreator.InitializeHeroFromSettings`
  does, via `HeroCreationModel.GetBornSettlement`. XML heroes never go through it.
  `Hero.UpdateHomeSettlement` then falls through governor / spouse / children / parents / siblings /
  clan / `CompanionOf` / notable and lands on `_bornSettlement` — null.
- A companion waiting in a tavern has `StayingInSettlement`, not `PartyBelongedTo`.

`NamedCompanionAdapter.PlaceInSettlement` only called `EnterSettlementAction.ApplyForCharacterOnly`,
which sets `StayingInSettlement` and nothing else, so placement never closed the gap.

**Why Erebor, and why the tournament.** `FightTournamentGame.CanNpcJoinTournament` admits
`hero.IsLord || hero.IsWanderer`, and named companions are `occupation="Wanderer"` sitting in
`settlement.HeroesWithoutParty`, so they enter. Their skills run 250-350, so they win. Four of the
eighteen — Gimli, Thanor, Whitegoat and Hatérade — spawn in `town_E1`, more than any other town, so
Erebor produced a clanless winner nearly every time. `TournamentManager.GivePrizeToWinner` guards
this two frames earlier (`if (winner.Clan != null)`); the view model does not.

`INamedCompanionAdapter.EnsureHomeSettlement` sets `BornSettlement` to the companion's configured
spawn settlement when it is null — the same field `HeroCreator` fills for every runtime-created
wanderer. `SpawnCompanions` calls it before placing; `EnsureCompanionsPlaced` calls it **ahead of the
placement-state guards**, because a companion in an existing save is already placed, recruited,
imprisoned or a fugitive and every one of those guards short-circuits. `_bornSettlement` is
`[SaveableField(630)]`, so the repair persists and runs once per save.

Not a tournament fix. The null was reachable from anything that dereferences `MapFaction` on these
heroes; the tournament is just where it landed hardest.
### fix(tools): lint_docs reports only real rot in the version and link checks (#397, #399)

`python tools/lint_docs.py` reported 43 findings on a fresh clone and not one was rot: 29 stale
version refs, all legitimate historical references, and 14 dead links, all pointing into the
gitignored `docs/reviews/raw/`. A check that is wrong every time is a check nobody reads.

The stale-version model flips from "a version string older than the pin is rot" to "a version string
**presented as the current target** is rot" — the definition `agent-operating-manual.md` already
states. Three refinements carry the rest: the present-tense test runs on the line with inline code
stripped (so `` `CampaignTime.Now` `` in an API-break note is an identifier, not a claim), a line that
also names the pin is contrasting two versions rather than calling the old one current, and
`docs/adrs/` joins the exempt prefixes — which the comment above that tuple already claimed was the
case, so `adrs/010`, the ADR that recorded this very problem, had been reporting itself as rot. Dead
links are skipped by **target** under `docs/reviews/raw/`, not by linking file, so `REVIEW-LOG.md`'s
several hundred other links stay covered.

43 → 0 with no doc content edited; `tools/lint_docs.py` and its tests are the only files touched.
9 tests added, 23 total. **The trade:** detection now requires marker-word phrasing, so present-tense
rot worded as "built for" / "requires" / a bare "Engine:" label is missed — filed as #405 with the
measured list. `> **Target: Bannerlord …**`, the CLAUDE.md header shape, is still caught, and
`check_version_consistency` checks that line against the pin independently.

External contribution from @davrodwconnections, review table from @Sternab. Every number was
re-measured here before merge.

Docs brought in line with the new semantics: `.claude/skills/lint-docs/SKILL.md` (checks 1-2 rewritten,
the blind spots stated where a reader of a clean run will see them), `docs/ai-includes/agent-operating-manual.md`
(the staleness rule is broader than the checker that enforces it — v1.4.5/v1.4.6 are not matched at
all), `tools/README.md` (new **Docs & knowledge base** section — `lint_docs.py` had never been listed),
4 lessons in `docs/reviews/lessons/build-tooling-workflow.md`, a lookup row in
`docs/reference/doc-lookup.md`, and `docs/INDEX.md`'s Conventions block — which still described the
linter, the backlinks footers and `docs/raw/` as unshipped future phases when all three have been in
place for months. New **`docs/features/doc-health-linter.md`** is now the reference the other five
point at — the tool had no feature doc while all three sibling validators did.

Writing it corrected a miscount the skill had carried since the config-drift checks landed: the
linter runs **seven** checks, not six (the CLAUDE.md/AGENTS.md eager-load budget was never listed),
and **three** of them gate a commit, not two. `docs/adrs/010` deliberately left
alone: it is a point-in-time record, and it is now exempt from the check rather than rewritten to
satisfy it. Lesson-index counts re-derived while there — 352 across 13 categories, five of which had
drifted low.

### fix(dwarf): female dwarves crashed on sight — one wrong mesh name (#403)

> "every instance that I attempt to find a female dwarf in a settlement/battle/tournament, if my
> camera looks at them it crashes my game, but it will not crash if I interact with them in dialogue"

A native access violation with no managed exception, so no crash bundle was ever produced:
`0xC0000005` READ at `TaleWorlds.Native.dll+0x58232C`, faulting address `0x24C` — a 16-bit index
read at a null base, i.e. geometry that failed to resolve. Reported independently by two players.

The adult female dwarf skin asked for `underwear_bottom_mesh="sk_dwarf_underwear_female"`. The
armory ships **`sk_dwarf_underwear_female_a`**; the bare name occurs only as a prefix of it, never as
a complete resource. Two facts make it fit the symptom exactly: it is the only unresolved mesh
reference in the whole 89-reference file, and the adult female dwarf is the **only** dwarf skin with
a non-empty `underwear_bottom_mesh` (males and all four child entries are `""`). Underwear draws only
when clothing leaves the slot visible, which is why keeps and battles crashed while dialogue — the
facegen path — did not.

Fixed in the live `LOTRLOME_Armory/ModuleData/skins.xml` **and** the tracked snapshot (one attribute,
both copies, line endings preserved; live backup at `skins.xml.bak-dwarf-female-underwear`).
Credit: the defect and its evidence were found by @Sternab in #403; every claim was independently
re-verified here before the edit landed.

### feat(tools): audit skin mesh references so this class of crash is caught, not stumbled on

`python tools/validate_mesh_refs.py --no-rgl-log` resolves every mesh name in a `skins.xml` against the meshes
actually shipped in the asset packages and loose asset trees, and exits non-zero on any that does not
resolve. This is the second time a single wrong mesh string in that file has cost a debugging session
(the first: the elf female skins, 2026-07-23), and both times the game gave no managed signal.

The check that matters is the **trailing token boundary** — `sk_dwarf_underwear_female` really is
present in the packages, as a prefix, so a substring search reports it fine and the bug survives.
Scans 251 packages / 50 GB in seconds by handing one pattern file to `grep`'s DFA. Verified both
ways: red on the pre-fix backup, green on all 89 references across all 12 races after the fix.

### fix(arena): guard the tournament winner panel against a null faction or culture (#407)

Separate defect, found while triaging the same reporter's crash bundle `d7d9f7d3` (Erebor, TAOM
v2.0.18.0). Clicking "Skip All Rounds" threw a `NullReferenceException` out of vanilla
`TournamentVM.OnTournamentEnd`, surfacing as a `TargetInvocationException` from
`ViewModel.ExecuteCommand`. Nothing in TAOM patches that method — the winner panel reads
`hero.MapFaction.Color` for a hero winner and `character.Culture.Color` for a troop winner, neither
guarded, and `Hero.MapFaction` genuinely returns null for a clanless, non-special hero with no home
settlement and no party.

`Patch69_TournamentRosterGuard` substitutes such entrants at `GetParticipantCharacters` with the
culture's elite/basic troop — vanilla's own padding choice. It **substitutes and never removes**, and
that is load-bearing: vanilla pads the roster to exactly `MaximumParticipantCount` (16) and
`TournamentMatch.AddParticipant` dereferences `participant.Team` with no null check, so a short
roster is its own NRE during bracket construction. Trading one crash for another is not a fix.

`Patch69_TournamentEndGuard` is a finalizer on `OnTournamentEnd` that dumps the entire bracket —
every round, match, team and slot with `IsValid`, participant nullity, clan, MapFaction and culture —
then swallows, so the screen degrades instead of dying mid-input. Two further null sites in that
method remain reachable in principle (`TournamentParticipantVM.Refresh(null, …)` nulls `Participant`
without ever resetting `IsValid`); they could not be reproduced from a full bracket, so they are
logged rather than guarded. If a bundle arrives carrying that dump, the slot is named outright.

**Not-tested:** Harmony invocation and the in-game path; the eligibility logic has 13 unit tests.

### fix(diagnostics): the action-set census now says which dwarf, and why

The reporter's log showed `ActionSet 'as_human_warrior' used by race='dwarf'` with no way to act on
it. The census now keys on sex as well as race — the entire male/female divergence lives in
`ActionSetCode.GenerateActionSetNameWithSuffix`, and folding the sexes together hid exactly the class
of report we were chasing — and records the character id and resolved Monster id. TAOM's own override
emits `as_human<suffix>` when the Monster is null, and that fallback is now logged (deduped per
sex+suffix) instead of silently turning a non-human into a human.

### feat(enlistment): a real-time service loop, and the edge that sees a commander join a running fight

Batch 3 — the foundational one. The donor drove its entire service loop from a ~4 Hz tick; the
rewrite drove it from one event edge plus an hourly reconcile. That single difference is behind
most of the reported symptoms.

**The edge that could not exist.** `CampaignEventDispatcher.OnMapEventStarted` is dispatched exactly
once, as the last statement of `MapEvent.Initialize` — it announces battle CREATION only. Every way
the commander joins an ALREADY-RUNNING fight was structurally invisible. `OnPartyAddedToMapEventEvent`,
dispatched from `MapEvent.AddInvolvedPartyInternal`, sees exactly that, and neither TAOM nor the
donor subscribed to it. It fires for every party joining every battle in the world, so the handler
is one bool and one ordinal compare against a cached id, with no logging on the non-match path.

**`ServiceMaintenanceService`** is the continuous half of the loop, with an explicit ownership split:
the hourly reconciler remains the ONLY terminal authority (discharge, grace, captivity, the stranded-
encounter sweep), and the pump asserts continuous invariants while making exactly ONE state
transition — breaking a stale `EnlistedBattle` latch that would otherwise block every future join.
A reflection test asserts the pump cannot even *inject* `IDischargeService`.

Two sources (campaign tick, wait-menu tick) share ONE budget, so adding the second cannot double the
work rate, and a cheap tier (cached position sync, zero lookups) runs every pass while the expensive
tier is throttled to one `Find<Hero>` at 4 Hz. The NaN gate is a positive requirement, because a
single NaN `dt` would otherwise poison the accumulator permanently — NaN propagates through every
later addition and every comparison against it is false.

Two contracts are written into the class docs so they are not re-litigated: the pump structurally
**cannot** reach `encounter` / `town` / `castle` or a map conversation, because the menu system sets
`TimeControlMode = Stop` and `Campaign.Tick()` gates its dispatcher on `_dt > 0f`; and the
`EnlistedAttached` gate on the menu assertion is load-bearing, not defensive, since `encounter` and
`join_encounter` are both in the redirect list.

The join retry budget is persisted (`pendingAttach`, `nextAttachHour`) and SHARED with the hourly
path. It is stored in campaign hours; the reconciler is handed days and converts. That equivalence
has its own assertion rather than a comment — if it ever drifts, the budget check silently
suppresses hourly recovery entirely and restores exactly the never-joins-a-battle bug.

Suite 5793 green (+42).

Research: CampaignEvents.TickEvent/OnPartyAddedToMapEventEvent, MapEvent.AddInvolvedPartyInternal dispatch, Campaign.Tick _dt gating, GameMenu TimeControlMode
### perf(enlistment): a per-tick adapter surface that does not scan the world

Batch 2 of the remediation plan. Behaviour-neutral groundwork for the real-time service pump —
nothing here is player-visible, and that is the point: the pump is unaffordable until the reads it
depends on stop being linear scans.

`CampaignObjectManager.Find<T>` is unindexed. `Find<Hero>` walks the dead-or-disabled list and then
every living hero; `Find<MobileParty>` walks every mobile party in the world — lords, villagers,
caravans, militia, garrisons, bandits. Every existing enlistment read paid one, and `GetSnapshot`
additionally rendered `hero.Name.ToString()` and walked Culture / Clan.MapFaction / CurrentSettlement
for fields no tick consumer reads.

New `CommanderTickSnapshot` + `GetTickSnapshot` make exactly ONE `Find<Hero>` and allocate nothing;
`SyncPositionCached` costs zero lookups on the steady path, revalidating a cached party handle O(1)
against the party id from the same pass. The rule is now stated in the XML docs of all three:
**a pump may make at most one `Find<Hero>` per pass and zero `Find<MobileParty>`**, and `GetSnapshot`
is marked forbidden on any per-frame path.

Map events have no engine identity to compare — `MapEvent` derives from `MBObjectBase` but every
instance is `new`'d and never registered, so `StringId`/`Id` are unset. The adapter mints a token
from a one-slot reference cache so the pump can tell "a different battle" from "the same battle".

Also hardens the park/restore paths with a shared `ClearArmyAttachment`, carved out so an army the
player LEADS is never cleared (that would disband it under its members), and pins the
never-attach decision with a source-scanning test so it cannot be re-derived.

Suite 5751 green (+20).

Research: CampaignObjectManager.Find<T> registered types, MapEvent/MBObjectBase identity
### fix(enlistment): one policy decides whether an encounter is ours to close

Batch 4. Five places finished a `PlayerEncounter`, each with its own ad-hoc reasoning or none.
They now route through a single PURE policy — no adapters, no logger, no engine types — with five
intents and two universal rules, so a decision that either destroys the player's own business or
strands them permanently is testable as a table.

Rule R1 is the one that matters: if the player is in their OWN map event, no intent finishes
anything — not even a discharge deletes a battle they are fighting.

A settlement encounter has no encountered MOBILE party. That fact separates "visiting a town" from
"meeting a lord", and it is why swearing an oath in a keep no longer ends the town visit.

**`Finish`'s default parameter is deleted.** The engine defaults to `true`; ours defaulted to
`false`, an inverted polarity every call site silently inherited. `PlayerEncounter.Finish` only
calls `LeaveSettlement()` when the flag is true, so `false` left `CurrentSettlement` set — and
`EncounterManager` refuses every main-party encounter while a party is inside a settlement. That is
a second, independent contributor to "cannot click a lord", distinct from the stuck-encounter bug.

`EnsureEncounterAgainst` also stops force-clearing blind; it refuses with a warning instead. That
surfaces as `could not join commander battle` lines in play — correct, not a new fault.

Research: PlayerEncounter.Finish forcePlayerOutFromSettlement, EncounterManager settlement gate

### fix(enlistment): every discharge hands the player back usable (INV-D1)

Batch 5, closing the frozen-menu soft-lock. `Execute` is a fixed-order pipeline and every step is
ordered for a reason now written beside it: presence before the army detach (`SetAttachedToInternal`
only tears down the inherited `MapEventSide` while active); detach before the encounter work
(`PlayerEncounter.Finish` branches on `Army`/`AttachedTo`); subscribers before placement (the
content layer cancels the active duty in its handler, while placement dispatches
`OnSettlementEntered`, which the duty runtime treats as a COMPLETION trigger — reversed, a
cancelled duty would complete itself).

The player is released into the commander's settlement with the right menu, with a mandatory
rollback: if the menu will not open we leave the settlement rather than strand them inside one.
`ExitToLast` is gated on actually being on the wait menu, because it sets `TimeControlMode = Stop`
unconditionally before delegating to a null-guarded manager.

The alarm stops crying wolf: `EncountersBlocked` gained the settlement clause it was missing from
`EncounterManager`'s real gate, a release inside a town is now a WARNING rather than an ERROR, and
`distToCommander` finally prints a number because the commander id is captured before `Reset()`.

Suite 5849 green.

Research: GameMenu.ExitToLast TimeControlMode, MobileParty.SetAttachedToInternal, PlayerEncounter.Finish branches

### feat(enlistment): put the `[EnlistDiag]` trace behind a switch the player owns

`[EnlistDiag]` was **4,856 of 6,001 lines (81%)** of one 32-minute log, 3,674 of them from the single
"map event started, commander NOT in it" line. `FileLogger` has no level filter, so the earlier DEBUG
downgrades changed only durability — every line was still built, timestamped and written. The gate had
to go at the call site; `FileLogger` itself is untouched and its durability contract is unchanged.

`TaomSettings.EnableEnlistmentDiagnostics` (MCM `Enlistment/Diagnostics`, no restart) now gates the
routine trace — TICK, SYNC ok, PARK ok, RESTORE ok and the per-map-event line, together ~93.5% of
measured volume across three files. Gating the *statement* rather than the level is what stops
`CountInvolved`'s full `InvolvedParties` walk from running at all when it is off; `FindCommanderPartyIdIn`
still enumerates once above the gate because it is load-bearing, so this removes one of two walks, not
both.

Gated lines emit at **INFO, not DEBUG**: DEBUG is the async queue a hard native CTD discards, which
would lose exactly the trace someone switched on to catch a crash.

**The toggle gates volume, not the tag.** Faults keep the `[EnlistDiag]` prefix and log regardless —
park/sync/restore failures, the drift warning, the stranded-`PlayerEncounter` sweep, and every
`DischargeService` line including `LEFT THE PLAYER UNABLE TO START ENCOUNTERS`. Turning it off does not
produce zero `[EnlistDiag]` lines, and the MCM hint says so.

The hazard was never the logging. In `ReconcileAttached` the log sits interleaved with the encounter
self-heal, the re-park and the position sync, so an `if (!enabled) return;` would have disabled the
enlistment self-heal for anyone who turned the toggle off. Six tests run the reconciler with it off and
assert each mutation still happens; a source scan rejects `IsEnabled) return` across all three gated
files. Verified by mutation: converting the gate to an early return reddens 22 tests, including all six.

Not-tested: MobilePartyAttachmentAdapter and EnlistmentBattleBehavior gate bodies (static
`MobileParty.MainParty`, sealed `MapEvent`) — pinned by source scan instead.

### fix(enlistment): settlements were never resolvable, and party attachment is settled as a no

`CampaignObjectManager.Find<Settlement>` returns null **unconditionally** on 1.4.7. The manager
registers exactly five object types — MobileParty, two Hero lists, Clan, Kingdom — and `Find<T>`
only descends into a bucket when `typeof(T)` matches one, so a Settlement lookup can never hit.
Two adapters used it. `MoveIntoSettlement` could never have worked (nothing called it, so nothing
surfaced it), and `DutyWorldAdapter` silently failed every spawned hunt duty — including after the
fix earlier the same day, which corrected WHICH settlement to anchor on while still resolving it
through the broken lookup. Both now use `Settlement.Find`, the form already used elsewhere in the
repo. A source-level binding test fails the build if `Find<Settlement>` reappears in an adapter,
because no runtime test can catch a lookup that throws nothing and always returns null.

**Party attachment is rejected, with evidence.** The natural design — set
`MobileParty.MainParty.AttachedTo` and let the engine carry the player along — is a guaranteed
campaign crash in its naive form: `DefaultEncounterGameMenuModel.GetGenericStateMenu()` dereferences
`mainParty.Army.LeaderParty` unguarded inside `if (mainParty.AttachedTo != null)`, and
`Campaign.Tick()` calls it on every tick while on the open map. Attaching *with* an army means
genuinely joining it (kingdom membership, army UI, undoing our own join-army suppression), and even
then a parked attached party fails `MapEvent.CanPartyJoinBattle` for every AI lord trying to
reinforce either side. It would also not have fixed the position drift, which is a tick-ordering
problem. Recorded in `docs/features/enlistment.md` so it is not re-derived.

Also lands the sequenced remediation plan: 44 specs across six design clusters, each adversarially
reviewed (44 kept, 0 dropped), ordered into 13 file-disjoint batches —
`docs/reviews/enlistment-remediation-plan-2026-08-07.md`.

Suite 5731 green.

Research: CampaignObjectManager.Find<T>/registered types, Settlement.Find, DefaultEncounterGameMenuModel.GetGenericStateMenu, Campaign.Tick
Rejected: MobileParty.AttachedTo as the park mechanism — unguarded Army dereference on every map tick
### fix(enlistment): stop calling an honourable release desertion, and stop stranding the player (#406)

Three confirmed findings from the donor diff, chosen because no architectural decision changes
whether they are right.

**A granted release is no longer desertion.** `ClassifyLeaveReason` returned `Desertion` whenever
the player left before `ContractEndDay`, and the contract defaults to **365 days** — so every
realistic exit was desertion, which forfeits the player's arrears. Asking your lord for leave and
being given it was being treated as betrayal, and it cost you wages he already owed you. The donor
has no desertion concept for discharge at all. `Desertion` is kept for a genuinely unilateral
abandonment path, which no dialog currently offers.

**Discharge now leaves the wait menu on every path.** Only the menu-button route called
`ExitToLast`, so any AUTOMATIC discharge — commander dies, grace expires, contract ends, identity
mismatch — left the player in a menu whose options were gone, with no Escape. The donor names this
exact symptom in its own source: "left the player frozen in the wait menu with an orphaned camera".

**The oath no longer destroys encounters that are not ours.** The `PlayerEncounter.Finish` added
earlier today fired blind, so swearing an oath while visiting a settlement, mid-fight, or in another
conversation tore down the player's own encounter. New `IEncounterAdapter.IsEncounterOwnedBy` gates
it on three conditions the donor also checks: no conversation in progress, an encountered MOBILE
party exists (a settlement visit has none — that check is what protects town visits), and it is the
commander's. Regression test asserts we leave a foreign encounter alone.

Suite 5730 green.

Research: ConversationManager.IsConversationInProgress, PlayerEncounter.EncounteredMobileParty
Not-tested: in-game discharge-then-interact, oath sworn inside a settlement
### docs(claude): require committing before a rebase, after a session lost another's work

`git pull --rebase` auto-stashes the WHOLE working tree, including files a concurrent session is
mid-edit on. If the stash is not restored afterwards, those files silently revert to HEAD with no
error, no conflict, and nothing in the log.

2026-08-07: a session did exactly that, committed over the top, and left `stash@{0}: colleague
in-flight enlistment/tools work (auto-stashed by Claude for rebase)`. The owning session next saw
its own source reverted to the pre-fix version and came within one command of committing the
original bug over its own fix, with a message describing a fix that was no longer present. It was
caught only because the reverted file still contained a symbol that had been deleted hours earlier.

Five rules in CLAUDE.md > Commits: commit before rebasing; check `git stash list` after any rebase
and restore what it took; recover with `apply` not `pop` so the stash survives as a safety net;
verify your own markers are in HEAD (`git grep <marker> HEAD`) after any stash event, because a
tree that compiles is not proof your change is still in it; and stage explicit paths rather than
`git add -A`, since shared files like CHANGELOG.md routinely hold two sessions' edits.
### fix(enlistment): the service loop actually works in a live game (#406)

Play-testing the shipped feature found it broken in ways no unit test could see. Instrumenting
the live session — rather than reading more code — found the cause of three reports at once.

**A `PlayerEncounter` stayed open for the entire term of service.** The oath is sworn inside a
conversation, and a conversation runs inside a `PlayerEncounter`; we parked the player's party but
never closed it. Measured at `playerEncounter=True` on **93 of 93** hourly ticks. `EncounterManager`
refuses every main-party encounter while that is set, so the player was engine-blocked from
interacting with anything for the whole term — and the state survived into discharge, which is why
lords could not be clicked after leaving service. The donor closes it at exactly this point
(`FinalizeEnlistmentConversation`: finish, then attach); the rewrite had dropped that ordering.
Now closed at swear-in and at discharge, with an hourly self-heal so saves already stuck recover.

**Nothing re-opened the wait menu after a battle.** The only re-assert was `OnConversationEnded`,
which never fires on the battle path, so the player came out of a fight parked and menuless on the
open map — reported as "after battle I was left behind and the option menu isn't here". Both
battle return paths now re-assert it.

**Hunt duties could never start in the field.** The spawn anchored on the commander's *current*
settlement, which is empty whenever the column is marching, so every `recon_sweep` failed with
`SpawnLooterParty: settlement=''`. Falls back to the nearest friendly settlement.

**`enlistment_config.json` was silently discarding every retune.** Newtonsoft's default
`ObjectCreationHandling.Auto` *appends* to a collection that already has an initializer, so a valid
4-entry rank table deserialized to 8, failed the "exactly 4" check, and reverted to compiled
defaults with only a warning. Fixed with `Replace` on both loaders.

Diagnostics were also recalibrated against the live log: the drift warning fired on essentially
every sync (291 of one session's 299 warnings) because ordinary inter-tick drift is ~1.8 and the
threshold was 1.0; a per-world-map-event line added another 3674. A diagnostic that fires
constantly is indistinguishable from no diagnostic.

**Confirmed working in live play:** field-battle join (instant), siege-assault join, hourly-recovery
join when the immediate edge is missed, return to parked service, and config load. Suite 5729 green.

Still unverified in game: a battle with the commander inside an army, discharge-then-click-a-lord,
and save-load mid-service. Recorded in `docs/features/enlistment.md`.

Research: PlayerEncounter/EncounterManager gating, MapEvent.AddInvolvedPartyInternal, GameMenu.SwitchToMenu vs ActivateGameMenu
Not-tested: army-battle join, post-discharge interaction, save-load mid-service
### fix(enlistment): the enlisted player can actually join the commander's battles (#406)

A player enlisted under a Mirkwood lord and never joined a single fight — the lord marched in an
army, stormed a town and fought in the field while the player's party trailed alongside, then left
the army and appeared unable to start battles at all. One bug wearing three faces: **the join never
worked, under any circumstance.** Army membership and the siege were coincidences.

The gate was `MapEvent.CanPartyJoinBattle`, used as the join precondition. It is not a mechanical
check — it requires **every party on the opposing side to be at war with the joining party's
`MapFaction`**, i.e. "may this free agent lawfully intervene in someone else's war?". Enlistment
deliberately leaves the player in their own clan, and a typical player is at war with nobody, so it
returned `false` for every battle and the service logged "staying parked" and went home. The donor
mod hit the same wall and worked around it by explicitly tolerating a false result while parked.

Four more defects sat behind it on the same path, each independently sufficient to break the feature:

- **The recovery path was a dead event.** `IEnlistmentReconciler.BattleJoinRequested` — raised hourly
  when the commander is fighting and the player is not — had no subscriber anywhere. The single
  `MapEventStarted` edge was the only chance to ever join, with no retry after a miss.
- **Sieges could never be seeded.** The encounter was built from the `MapEventStarted` arguments via
  `MobileParty` ids; a settlement defender has no `MobileParty`, so the id was null, the encounter
  was skipped, and `JoinBattle` then threw on a null `PlayerEncounter.Current`. Now seeded from
  `MapEvent.AttackerSide/DefenderSide.LeaderParty`, which are `PartyBase` and cover settlements.
- **A failed join reported success.** `PlayerEncounter.JoinBattleInternal` silently calls `Finish()`
  when `EncounteredBattle` is null, so a non-throwing call proved nothing. The adapter now verifies
  `MainParty.MapEvent != null`. The false success left state at `EnlistedBattle`, which blocked
  every subsequent battle at the entry guard.
- **Rollback leaked the encounter.** `EncounterManager` gates the main party on
  `PlayerEncounter.Current == null`, so an orphan blocked all future encounters — and a `MapEventSide`
  acquired without a live encounter freezes that map event permanently (`MapEventManager.Tick` skips
  the player's event; only `PlayerEncounter.Update` advances it), stranding the commander with
  `MapEventSide != null` and unable to start anything. That is the reported tail state.

Also: presence is now restored and position synced *before* any encounter work (the engine skips
inactive parties in encounter detection, so checking joinability while parked was chicken-and-egg);
the hourly reconciler no longer demotes `EnlistedBattle` while an encounter is open, which could
undo a good join; and every silent early-return on the path now logs which branch it took.

`/deep-review` then caught a **sixth defect, introduced by the fix itself**: the return of
`SwitchTo("encounter")` was discarded. A verified join gives MainParty a real `MapEventSide`, so a
failed menu switch freezes the map event — and the hourly recovery goes blind to it, because the
player genuinely IS in a map event and `Assess` reports `Attached` rather than `BattleJoinRequired`.
The same review pass also replaced a per-map-event `GetSnapshot` (which walked
Culture/Clan/MapFaction/Settlement and allocated through `Name.ToString()` for a caller that reads
only the party id) with a new `ICommanderLordAdapter.GetPartyId`.

An independent Codex pass then found **two P1s the five Claude agents had just cleared**, one of
them in the fix above:

- `GameMenu.SwitchToMenu` silently no-ops when there is no current menu context (it only
  `Debug.FailedAssert`s, inert in release), so checking `SwitchTo`'s return still reported success
  for a menu that never opened — the freeze survived the fix written to prevent it. Replaced with
  `IGameMenuAdapter.EnsureMenuOpen`, which switches, falls back to activating, and verifies against
  the observable `CurrentMenuId` rather than a returned bool.
- `LeaveSettlementIfUnderSiege` ran before *every* join. `MapEvent.AddInvolvedPartyInternal`
  rewrites a siege **assault** to `SiegeOutside` when a defender joins with `CurrentSettlement ==
  null`, so leaving first would have converted the battle type for every participant — an assault
  on the walls becoming a field fight outside them. Now attacker-only.

Tests: one regression test per defect, plus a DryIoc `Validate()` guard on the two constructors on
this path — the original bug compiled and passed every test because `IEncounterAdapter` was mocked,
so the engine precondition was never exercised. Suite 5713 green.

Known limitation: a joinable battle that repeatedly fails to seed retries hourly with no bound, and
each retry's `PlayerEncounter.Finish` pops the player's open menu via `ExitToLast`. Deferred with
reasoning to #408 — adding per-battle retry state to a just-restructured path is riskier than the
narrow exposure until in-game verification shows whether join failures recur at all.

RCA: `docs/reviews/rca-enlistment-battle-join-2026-08-07.md`

**Not yet verified in a live game.** The in-game gate list in `docs/features/enlistment.md` now leads
with this path and names the log line for each distinct failure branch.

Research: MapEvent.CanPartyJoinBattle, PlayerEncounter.JoinBattleInternal/RestartPlayerEncounter,
EncounterManager.HandleEncounterForMobileParty, MapEventManager.Tick
Not-tested: live encounter join (requires a running campaign)

## 2026-08-06

### feat(diagnostics): attribute town-gold drains and name why each caravan is parked (#391)

A player reported caravans sitting inside Minas Tirith while the merchant held 173 denars, and read
the two as cause and effect. Reading v1.4.7 shows both are real problems, the causal link runs the
other way, and neither is observable in-game — so this ships the instruments rather than a guess.

**The town's regen is not the fault.** `GetTownGoldChange` mean-reverts toward `base + Prosperity×12`
at 25% of the deficit, once per day. At TAOM's shipped base of 25000 and Minas Tirith's prosperity of
4345 the target is ~77,000, so at 173 gold the mint owes **~19,242/day**. Roughly 19k/day is leaving.
#317 raised the ceiling and never touched the drain — and the unbounded one is
`SellGoodsForTradeAction` (:52-57), which buys `min(qty, town.Gold / price)` across a villager's whole
roster with no reserve and can legally spend a town to zero daily.

**Town gold does not park a caravan.** Nothing in the caravan decision path reads `Town.Gold`; its
only effect is clamping sale volume to `town.Gold / itemPrice`, which at 173 denars silently rounds
to zero. What it does instead is *starve* them into the parked state: `HourlyTickParty` calls
`BuyGoods` before `ThinkNextDestination` with no broke-check, so a caravan that cannot sell spends
its purse anyway, drops to `PartyTradeGold ≈ 0`, and zeroes the buy half of its trade score for every
town in the world. With no cargo either, every candidate scores exactly 0, the strictly-greater-than-
zero test never passes, no AI action is set, and it is parked permanently — with no fallback, because
every generic AI behavior early-returns on `IsCaravan`.

`Patch68_EconomyDiagnostics` — one Prefix/Postfix recorder on `SettlementComponent.ChangeGold` plus
four outermost-wins flow-tag pairs. `ChangeGold` is the pool's **sole** mutator (private setter), so
one recorder sees 100% of flows and no site can be missed; it reads the before/after delta rather
than the argument because the engine hard-floors at zero and would otherwise hide that clamp.

`taom.print_town_ledger [town]` reports movement by day and by flow, naming the largest **drain**
rather than the largest flow — the mint is usually the biggest number and is income.
`taom.print_caravans [settlement]` reports which gate holds each caravan, with a histogram
(five caravans reading `Alerted` says "battle nearby"; no single line does).

Also recorded: the wounded ladder's thresholds are written `(double)num > 0.4` but `num` is a
`float`, and widening puts the nearest float to 0.4 at `0.40000000596…` — above the double. Every
band boundary is therefore **inclusive**, so exactly 40% wounded means *never leaves*. Verified
against IEEE-754 semantics, not assumed, and pinned by a data-driven test.

Also refuted, because it was the leading hypothesis: caravans are not a net drain. Selling to a town
pays out, but buying from one credits the **full** price and only skims the tax back.

Read-only, no gameplay change. Binding tests pin all five targets, two of them private
`ItemConsumptionBehavior` methods where a rename would silently attribute the town's whole income to
`Other` — plausible-looking and useless, which is worse than a crash. Feature doc:
`docs/features/economy-diagnostics.md`.

### fix(review): deep review of the career effect work — 1 CRITICAL, 1 HIGH fixed (#394, #395)

Six-agent deep review (5 core + the mandated tooling agent, since the changeset adds a file-writing
Python tool). Standards PASS, API compatibility 6/6 verified against the installed v1.4.7 DLLs,
Completeness COMPLETE, Data Flow 38 flows / 0 gaps. Two findings, both fixed here.

**CRITICAL — `tools/retune_career_health.py` was not idempotent for `troopdamage`.** The per-pip
"already retuned" guard keys on the OLD magnitude, which only works while a mapping's keys and
values are disjoint. `health` (25-100 → 5-10) is disjoint; `troopdamage` (0.03-0.20 → 0.02-0.08)
overlaps on {0.03, 0.05, 0.06, 0.08}, so an already-converted 0.05 was indistinguishable from a raw
0.05 and a second `--apply` would have silently re-shifted **71 of 105** pips across magnitude,
description, source string, 12 language files and 12 caches. The dry-run had been printing "71 pips
found" in plain text. Replaced with a file-level `already_applied()` guard — decidable where per-pip
is not — plus an `overlapping_values()` warning and 14 unit tests that pin idempotency for every
profile off the config table, so a new profile cannot skip the check. The tool now exits with
ALREADY APPLIED for both effects and writes nothing.

**HIGH — hot-path allocation this work created.** `CareerPassiveService.GetPassiveMagnitude`
interpolated a `LogDebug` string on every non-zero lookup. Interpolation is eager and `FileLogger`
has no level gate, so each call cost the message string plus a `DateTime.Now.ToString`, a second
interpolation and an unbounded queue push — regardless of whether anyone reads DEBUG. Harmless while
lookups were rare; this changeset made the method hot from both ends (`MaxHitpoints` on every agent
spawn, tooltip and heal tick; `TroopDamage` per blow for every non-hero troop whose leader holds the
pip). Line removed — `RefreshCache` still logs each hero's cached passives, so nothing diagnostic is
lost, and the crash-triage log stops being buried during battles.

Also corrected: four documents asserted "re-running is a no-op" on the strength of the unsound
per-pip reasoning, and the tool's lack of `.bak` writes is now a recorded deviation (every target is
git-tracked, unlike the sibling scripts that mutate the live game install) rather than a silent one.

RCA: `docs/reviews/rca-career-effect-layer-audit-2026-08-06.md`.

### fix(career): troop-damage pips only made villages burn faster (#395)

Second instance of the max-health bug class, found by re-auditing every career effect with the lens
that actually catches it: not *"is anything reading this"* but *"is it read where the description
promises"*. `PassiveEffectType.TroopDamage` — 105 pips, the second-most-used effect in the system —
promises combat: *"+5% troop damage"*, *"Your troops smash through enemy lines with brutal
efficiency"*. Its only consumer was `TaomRaidModel.CalculateHitDamage`, which vanilla defines as
`(√TroopCount + 5)/900 × deltaHours` and `RaidEventComponent` accumulates into `_nextSettlementDamage`.
That is village raid progress. Troops hit exactly as hard as they always did, in every battle.

Wired as the exact mirror of `TroopResistance`, which was already doing the defensive half correctly:
`GetAttackerTroopLeaderHeroId` resolves the attacker's party leader — off the RIDER's origin for a
mount hit, since a struck mount's own `Origin` is null, the same trap the victim-side helper already
documents — and `CalculateDamageAmplification` applies the leader's `TroopDamage` to their non-hero
troops' hits. Not mask-gated: the shipped pips carry no `attack_type_mask`, so it is a flat army-wide
multiplier. The hero and troop paths are mutually exclusive, pinned by a test.

The raid consumer is deliberately **kept**. Raid progress is a fair reading of "troop damage", it is
behaviour players already have, and the two are different systems — not a double-count.

`PassiveEffectConsumers` now records both misses in its header. Membership in that set is necessary,
never sufficient: it answers "is anything reading it", and it stayed green through both bugs.

### balance(career): troop-damage pips cut from 3-20% to a 2-8% band (#395)

Turning 105 pips on at their authored magnitudes would have been a large, sudden combat swing — and
those magnitudes were authored while the effect did nothing in battle, exactly the situation the
max-health pips were in. An army-wide multiplier compounds across every soldier, so the band now sits
below the hero's own `Damage` pips (5-18%) rather than above them.

| old | 3% | 5% | 6% | 8% | 10% | 12% | 15% | 20% |
|-----|----|----|----|----|-----|-----|-----|-----|
| new | 2% | 3% | 3% | 4% | 5%  | 6%  | 7%  | 8%  |

Per tier: t1 2-3%, t2 3-6%, t3 5-8%.

`tools/retune_career_health.py` grew an `--effect` flag and an `EFFECTS` profile table rather than
being copied — the four-surface machinery is identical, only the type, mapping, wording anchor and
number scale differ. `scale` is the new piece: Health authors a flat count and prints it as-is,
TroopDamage authors a fraction and prints a percentage, so `0.05` must render as "5%".

Verified: 105 pips rewritten, 105 descriptions + 105 source strings + 1,260 translations + 1,260
cache entries synced, 0 description/magnitude mismatches, 0 cache/language mismatches, every other
effect type's range byte-identical, symmetric 6,455/6,455 diff with the `\r\r\n` line endings intact,
`validate_moduledata.py` PASS, suite 5594 green (+4 tests).

Not-tested: in-game feel of army-wide troop damage.
Research: DefaultRaidModel.CalculateHitDamage, RaidEventComponent, AttackInformation attacker-side origins

### balance(career): max-health pips cut from 25-100 HP to a 5-10 band (#394)

Direct consequence of the fix below. While `Health` only moved a mission agent's health bar, +75 was
a battle-only number nobody read on the character sheet. Now that it is the campaign max HP, +75 on
vanilla's flat 100 base is a +75% swing that also raises the daily heal cap and the wounded threshold.
All 165 pips drop into a 5-10 band.

Mapping (maintainer-chosen "even spread" — every authored rank distinction survives, and the curve is
non-decreasing so a bigger old pip is never a smaller new one):

| old | 25 | 30 | 35 | 40 | 45 | 50 | 60 | 75 | 100 |
|-----|----|----|----|----|----|----|----|----|-----|
| new | 5  | 6  | 6  | 7  | 7  | 8  | 8  | 9  | 10  |

Per-tier result: root/t1 5-7, t2 6-8, t3 8-10.

`tools/retune_career_health.py` rewrites every surface the number appears on, so none of them states
a value the effect does not deliver: the `magnitude=`/`value=` on each Health `<PassiveEffect>` (both
authoring schemas), the English default in `description="{=key}..."` plus its source string in
`taom_career_strings.xml`, the 1,968 translated strings across all 12 languages, and 1,967
`tools/translation_cache/<lang>.json` entries.

That last pass is load-bearing, not housekeeping. `translate_with_claude.py` looks the cache up by
`string_id` alone and never checks that the English source still matches, so leaving it stale means
the next `/localize` run serves "+75 points de vie maximum." back into all 12 language files and
silently undoes the retune — a delayed fuse rather than a visible failure.

Touching the translations diverges from `retune_phantom_descriptions.py`, which deferred to
`translate_with_claude.py`. That script changed the text's shape (flat count → percentage, needing
per-language re-wording); this one changes a digit, and a digit is a digit in every language. Each
translated health string was verified to carry exactly one number, so the swap is unambiguous, keeps
the hand-translated PL wording, and needs no re-translation run. Re-running is a no-op — enforced by a
file-level `already_applied()` guard, NOT by keying on the old magnitude, which the deep review proved
unsound for any mapping whose old and new value sets overlap (see the #391-class note below).

One pip is deliberately untouched: `far_harad_halftroll_root` reads "Vile Brutality grants massive
health bonus" with no number, so there was nothing to sync (its magnitude still moved 40 → 7).

Verified: 165 pips rewritten, 164 descriptions + 164 source strings + 1,968 translations + 1,967
cache entries synced, 0 description/magnitude mismatches, 0 cache/language mismatches, every other
effect type's magnitude range byte-identical, all 14 XML files well-formed, 12 caches valid JSON,
`validate_moduledata.py` PASS, suite 5590 green. The data diff is symmetric (3,935 insertions /
3,935 deletions) — the tool uses the binary round-trip `tools/README.md` mandates, because these
language files are committed with `\r\r\n` and text-mode I/O turns the edit into a 6,180-line
whole-file rewrite (lesson recorded in `docs/reviews/lessons/build-tooling-workflow.md`).

Not-tested: in-game feel of the new band.

### fix(career): the max-health pip only applied in battle (#394)

A "+75 max health" career choice left the character screen reading `Max. Hit Points 100 / Base +100`.
`PassiveEffectType.Health` had exactly one consumer — `TaomAgentStatCalculateModel.GetEffectiveMaxHealth`,
in the **mission** `AgentStatCalculateModel` slot. The tooltip, `Hero.MaxHitPoints`, the daily heal cap
and the wounded threshold all read `CharacterStatsModel.MaxHitpoints` instead, and `TaomCharacterStatsModel`
overrode only `MaxCharacterTier`. The pip worked in battle and was invisible + inert on the campaign layer.

`TaomCharacterStatsModel` now takes `ICareerPassiveService` and applies `Health` via `ApplyFlat` on
`MaxHitpoints`, so the tooltip shows a named career row beside `Base +100`.

The hero add was **moved, not copied**: `SandboxAgentStatCalculateModel.GetEffectiveMaxHealth` opens with
`if (agent.IsHero) return agent.Character.MaxHitPoints();`, which resolves to that same model, so the
mission path inherits the bonus for free. `CareerAgentStatService.ApplyMaxHealthPassives` became
`ApplyMountHealthPassives` (mount-only) — keeping both adds would have made a +75 pip +150 in battle
(100 → 175 → 250). A unit test pins that the agent-stat path never reads `Health` again, and a binding
test pins the override plus its v1.4.7 signature (`CharacterObject`, `bool includeDescriptions` — not a
`StatExplainer`); the generic GameModel gates cannot catch its removal because `MaxCharacterTier` alone
keeps "declares an override" green.

Audited every other career effect while here: the 22 other shipped passive types all have a live read
site at a layer matching their wording with correct magnitude units, and all 302 keystone mutations
resolve (3 properties, all float on `AbilityTemplateData`; all 50 target ids match a template).
`Health` was the only broken one. `PassiveEffectConsumers` now documents why its phantom gate stayed
green — it answers "is anything reading it", not "on the layer the player sees".

Not-tested: in-game verification of the tooltip and the in-battle health cap.
Research: DefaultCharacterStatsModel.MaxHitpoints, SandboxAgentStatCalculateModel.GetEffectiveMaxHealth, Agent.BaseHealthLimit
Save-compat: No new fields — the bonus is computed from existing career choice data on every read.

### feat(career): per-tier diamond metals — bronze, silver, gold (#388)

Tier 1 bronze → tier 2 silver → tier 3 gold, so the choice tree reads as a progression at a
glance instead of three identical rows. Each tier owns its own three border colours (dim =
locked, mid = available, bright = taken glow) and they are deliberately not shared, so changing
one tier's metal means editing three values; the hex table lives in a comment above the body
block in `CareerScreen.xml`. The taken glow is shown by `TaomCareerDiamondWidget` rather than by
a binding, so per-tier glow needed no widget change.

The first silver ramp was then brightened: `#AEB6BE` for the available state is a mid grey, and
because the border sprite is tinted **multiplicatively**, a mid-tone tint darkens the sprite
instead of highlighting it — tier 2 read as washed out rather than as the step between bronze
and gold. Now `#6E767E / #E8EFF7 / #FFFFFF`, making a taken tier-2 diamond the brightest rim on
the screen.

Also fixed en route: an XML comment containing `--`, which the spec forbids and which stopped
the prefab parsing. Caught by the parse check before it reached the install — the same trap the
deep-review skill's "XML Parse Smoke Test" exists for.

Not-tested: in-game render of the three palettes.

### fix(career): diamond screen layout rework + ultrawide centring (#388)

Two follow-ups after seeing the first diamond pass in-game.

**Layout rework.** The career info was a full-height left column, which ate the width the grid
needed; it is now a header bar across the top (name + description | portrait | ability icon,
name and effect lines). Diamonds went 90px → 70px, the boxed group panels are gone entirely
(a group is now just its lore name above its row on the open background), and each diamond
carries the bronze border sprite tinted by state — dim when locked, bronze when available,
bright gold when taken. The rank title and its "Requires Level N" moved onto one line above
the groups, where they no longer collide with group names, and Free Points / Active Effects
became a proper right-hand column. The hover tooltip is anchored above its diamond and renders
late so it draws over neighbouring rows instead of under their chrome.

Removing the +/− button column left no way to refund a pick, so the diamond click became a
toggle (`ExecuteToggleChoice`): take an untaken choice, refund a taken one. Both directions
were already guarded — free points, tier locks and keystone exclusivity going in, a no-op on an
untaken id coming out — and a regression test pins it, because without it every taken choice is
stranded. `CareerNodePanel`'s VisualDefinition went with the boxed panel it sized.

**Ultrawide centring.** On a 32:9 monitor the screen came apart: the header bar stretched into
dead space, its ability description drifted past centre and clipped mid-sentence, and the
Active Effects column stranded far from the grid. Cause was `StretchToParent` plus margins on
the outer container — the layout was as wide as the monitor, so every fixed-width child
clustered left and the rest was void. Widths tuned against a 16:9 canvas had silently stopped
meaning anything. The container is now Fixed 1720 and centred, sized to hold the body
(1330 + 20 + 340) and the header (700 + 290 + 24 + 92 + 16 + 560), so it reads as one centred
block at any aspect ratio.

Suite 5566 green. Not-tested: in-game render at ultrawide — needs a reopen.

### fix(diagnostics): deep review of the #389 instrumentation — 8 findings, all fixed

Six agents (Standards, Compatibility, Efficiency, Completeness, Data Flow, Tooling Correctness) over
Patch67, its tests, and the two scripts. Compatibility verified 47/48 engine members with zero
incompatibilities; Completeness returned clean.

- **HIGH — per-frame allocation.** `BuildKey` rebuilt a key string on every rendered frame per live
  tableau, *before* the early-out. An earlier adversarial pass had cleared the steady-state path by
  checking everything that ran AFTER the early return and never asking what ran before it. Replaced
  with `TableauCensusSession`: a `ConditionalWeakTable` keyed on the tableau instance, so the
  steady-state path is a weak lookup plus one ordinal compare. Note the efficiency agent's proposed
  fix (a plain `Dictionary<CharacterTableau, string>`) would have pinned engine objects for the
  process lifetime — an agent's prescription needs verifying separately from its diagnosis.
- **GAP — tracked slots leaked on teardown.** `Forget` fired only from `SetRace`; nothing hooked
  `OnFinalize`, so closed tableaus held slots and the census went quiet after 64 characters. Added
  `CharacterTableau_OnFinalize_ResidencyReset_Patch`; the binding test now pins `OnFinalize`.
- **MED — identity collisions.** `RuntimeHelpers.GetHashCode` is reused after GC, so a recycled hash
  plus a repeat troop could silently suppress a new tableau's census. Now a monotonic serial that is
  never reissued.
- **MED — file layout.** Split two patch classes out of one file per the `{TargetClass}{TargetMethod}Patch.cs`
  convention. The census logic moved behind an `object`-keyed seam, which also made it unit-testable —
  8 new tests, including reference-equality assertions that prove the no-allocation guarantee.
- **Tooling (3 findings) in `fix_uruk_hai_hands_teamcolor.py`:** the forbidden mixed `utf-8` text
  read + text write (**third instance** of a lesson dating to 2026-05-28), a dormant branch splicing
  bare `
` into CRLF files, and substring rather than exact-token matching when deriving the target
  set. All fixed; the live edit was re-verified byte-identical afterwards (CRLF and item counts
  unchanged, dry run still reports 92 meshes / 0 pending changes).

**The repeat offender got a scope fix, not another lesson.** The XML I/O convention lives in
`tools/README.md`, which nothing auto-loads, and `moduledata-validation.md` was paths-scoped to *repo*
ModuleData — so authoring a script that edits the game install loaded no rule at all. That rule now
loads on `tools/**/*.py` and `tools/**/*.ps1` with both sanctioned idioms inline. A blocking lint was
evaluated and rejected: 92 of 124 XML-writing scripts trip a naive heuristic, so a gate would fail on
pre-existing debt.

Suite 5680 green; 95 BindingVerification tests pass against the installed engine. Findings table,
root-cause pattern and per-agent miss analysis in
`docs/reviews/rca-isengard-black-tableau-2026-08-06.md`; Review 82b in `docs/reviews/REVIEW-LOG.md`.

### fix(render): Isengard Uruk-Hai black silhouettes — FIXED and confirmed in-game (#389)

`m_uruk_hai_hands_a1` is authored to require the shader flag `use_double_colormap_with_mask_texture`.
The only thing that adds it is `AgentVisuals.AddTeamColorToMesh`, which the engine calls **only when
the item carries `UseTeamColor="true"`** — it does `GetMaterial().CreateCopy()` then
`AddMaterialShaderFlag(...)`. Isengard uruk armour was `UseTeamColor="false"`, so the material rendered
with the mask path unbound: black.

**CONFIRMED IN-GAME 2026-08-06.** `[Isengard] Uruk-Hai Swordman` renders with full armour detail and
the White Hand of Saruman legible on helmet and chest — that white-on-dark insignia is the
double-colormap mask path itself working. Verified in the log rather than only on screen: the post-fix
census shows every affected troop (`urukhai_fighter`, `_warrior`, `_swordman`, `_feller`, `_spearman`,
`_pavise`, `_nazg_hai`) now binding `m_uruk_hai_hands_a1(copy)` at `0x4C0090`, with **zero** remaining
occurrences of the raw `0x480090`. Exactly the transition predicted, on exactly the troops named.

Measured by the Patch67 render census, same material on both sides:

| item flag | material as bound | shader flags | result |
|---|---|---|---|
| `false` (uruk armour) | `m_uruk_hai_hands_a1` | `0x480090` | **black** |
| `true` (orc armour) | `m_uruk_hai_hands_a1(copy)` | `0x4C0090` | correct |

Delta is exactly `0x40000`. Correlation held across all 11 censused troops: black iff carrying the raw
variant. `urukhai_recruit` renders because its tunic/gloves/shoes never pull in the hands sub-mesh, and
the Isengard orc troops render because their items *are* team-coloured — using the very same uruk hands
material. **The absence of team colour was the bug, not its presence** — this investigation had that
arrow backwards twice.

- **Test fix applied to the live dependency module:** `UseTeamColor="true"` on the 79 Isengard items
  whose mesh binds that material (43 head, 12 arm, 10 body, 8 shoulder, 6 leg). 13 already had it.
  Reversible via `tools/oneoff/fix_uruk_hai_hands_teamcolor.py --revert`; target set is derived live
  from the armory's own `shader_compile_report.log`, never a hardcoded id list.
- 158-line diff, CRLF preserved byte-for-byte, item counts unchanged, XML well-formed,
  `validate_moduledata.py` PASS. Backups are `.bak-teamcolor` — **not** `.xml`, since these folders are
  globbed and an `.xml` backup injects duplicate ids. Needs a full game restart to test.
- Recorded in the armory snapshot README as an APPLIED EDIT: an Armory update will silently revert it.
  The cleaner upstream fix is that hand/glove sub-meshes should not be bundled into helmet, bracer,
  greave and pauldron MetaMeshes at all.

### docs(review): #389 investigation record — the discriminator is the helmet, not the race

Reporter observed that `urukhai_recruit` renders correctly. It is race `uruk_hai`, uses
`BodyProperty.fighter_uruk_hai`, wears an `sk_uruk_hai_*` body mesh — and is the only Uruk-Hai troop
with **no Head item**. That single observation exonerates the race, the body-property template, the
race base-body meshes and the whole `sk_uruk_hai_*` body-armour family at once. Every hypothesis from
the first day was scoped to the race because the *report* was phrased in race terms; the roster had
contained the disproof the whole time.

Current lead: every `sk_uruk_hai_helmet_*` MetaMesh bundles glove and hand sub-meshes
(`m_uruk_hai_gloves_a1` + the 888-shader-variant skin material `m_uruk_hai_hands_a1`) alongside its
helmet material, where the working `sk_gn_orc_mrd_helmet_light_a` control carries exactly one. Not
proven — the warrior's `_a4` helmet lacks the skin material yet is still black, and it does not
obviously explain the *whole* figure going black. All referenced materials and textures exist, so this
is a structural/authoring anomaly, not a missing asset.

Also settled this session, with evidence: resource residency is dead as a mechanism (measured
`agentLoading=0 mountLoading=0 agentVisible=True` on the black troop), cloth/team colour is dead
(byte-identical `0xFF2B2B2B` between the fine orc and the black uruk), and the engine's content-warning
channel was **completely empty** during a session where the bug was on screen.

- **Census now reads the SKELETON.** `AgentVisuals` attaches skin and armour through `GetSkeleton()`,
  so `GameEntity.MultiMeshComponentCount` is 0 on a character tableau — the first revision reported
  `metaMeshCount=0` for every character *including ones that render correctly*, which only a
  known-good control made legible as an instrument fault.
- New: [`docs/reviews/rca-isengard-black-tableau-2026-08-06.md`](docs/reviews/rca-isengard-black-tableau-2026-08-06.md)
  (open investigation, 12 eliminations with evidence). Five lessons appended across
  `animation-skeleton`, `data-content-cultures`, `testing-qa` and `harmony-il`; index recounted
  320 -> 337 (several categories had drifted independently). Armory snapshot README gained a
  "Known asset defects" section covering the helmet bundling, #390's 42 missing meta-meshes, and the
  dual-tree trap.
- Corrected two career entries below that cited **#390** — that number is the meta-mesh content issue;
  they belong to the #388 career arc.

### feat(diagnostics): Patch67 dumps a per-troop render census for the black-silhouette bug (#389)

The investigation had no instrument — the encyclopedia render path emits nothing, so the only evidence
was a human comparing pixels. `Patch67_TableauResidencyDiag` dumps one census per character on the
frame its visual becomes ready: mesh names, material names, shader names and flags, per-mesh
`Mesh.Color`/`Color2`, and the bound diffuse texture. Diff `urukhai_fighter` against
`isengard_orc_ravager` and the difference names the cause.

**The first version of this was aimed at the wrong thing, and adversarial review caught it before it
shipped.** It reported "loading counter never cleared ⇒ black silhouette". Against the v1.4.7
decompile that is impossible: `RefreshCharacterTableau` hides the refreshed buffer outright, and
`OnTick` makes a visual visible only once *both* loading counters clear — so a character whose
counters never clear renders **blank, not black**. For a symptom of "correct geometry, black pixels"
the counters must already have cleared, meaning the instrument could only ever have logged "fine".
Compounding it, armour is race-independent (`AddArmorMultiMeshesToAgentEntity` never reads `RaceData`),
so no per-race residency story explains a black helmet or shield at all. The counters are now used
only as a *timing trigger* for the census, and the registry entry carries a do-not-reinstate note.

Three defects the same review caught, all now fixed and pinned by tests: the counter reads 0 both
before the first refresh arms it and after loading completes, so treating 0 as "ready" reported
characters that had nothing attached (the sentinel collision `.claude/rules/harmony-patches.md`
already documents — requiring an observed non-zero first); only the agent counter was checked when
vanilla gates on the mount counter too; and capacity exhaustion went silently dark, so a missing troop
was indistinguishable from a clean one. An inverted boolean label (`bodyPropsDefault` printing the
negation of what it said) went with them.

- **Its own category, not folded into Patch2.** The #389 experiment disables `Patch2_RefreshTableau` +
  `Patch3_SetRace` to test whether TAOM's own patches cause the fault; the instrument must survive it.
- Keyed on `{instanceHash}/{_charStringId}` — the troop id makes the comparison readable, the instance
  hash stops the tick budget accumulating across every screen that ever showed that troop.
- Trigger logic is a pure `TableauResidencyTracker` (17 tests, TDD) so the patch stays thin; the
  per-frame postfix injects only two counters and the char id, with everything else read through cached
  reflection on the single reporting frame. Every field it touches plus both targets are pinned by
  `Patch67TableauResidencyBindingTests` — a renamed injected field makes Harmony throw, a renamed
  reflected field silently degrades every value to `<unreadable>`. Suite 5588 green.

**Caught in-game, not by the suite: the patch used three underscores where Harmony needs four.**
Harmony strips exactly three from an injected parameter and looks the remainder up as a field, so
`___agentVisualLoadingCounter` asked for a field named `agentVisualLoadingCounter` and the whole
category threw `ArgumentException("No such field")` at apply time. It passed its own four binding
tests and the full 5588-test suite, because `AccessTools.Field(type, "_agentVisualLoadingCounter")`
resolves regardless of how the *parameter* is spelled. The isolated preview batch logs a category
failure rather than crashing, so the only symptom was a patch that never ran — found by reading the
live `taom_debug_*.log` after an in-game repro. New repo-wide gate
`TAOM.Tests/Migration/HarmonyFieldInjectionNamingTests.cs` scans every `[HarmonyPatch]` class, strips
exactly three underscores from each `___` parameter and asserts the remainder resolves on the target,
suggesting the four-underscore form; verified red-then-green by reintroducing the defect. Lesson
recorded in `docs/reviews/lessons/harmony-il.md`. Suite 5589 green.

Scaffolding — delete the category once #389 is root-caused.

### fix(docs): `-p:DisableModuleCopy=true` does not actually disable the module copy

Verified against `bannerlord.buildresources` 1.1.0.129: only the `PostBuildCopyToModules` wrapper is
gated on the flag, while `CopyBinariesWindows` and `CopyModule` each carry their own
`AfterTargets="PostBuildEvent"` with conditions that omit it. So every "safe" agent build has been
writing to `<game>\Modules\` regardless — invisible with the game closed, a hard build failure with it
running (it holds `0Harmony.dll`). Five docs assert the flag works. The operating manual now carries
the caveat and the actual workaround (`-p:ModuleId=`), which skips all three copy targets while
leaving references intact.

### fix(tools): `taom-src` can now reach engine types that ship inside a module (#389 fallout)

`pwsh tools/taom-src.ps1 path <Type>` searched only `<game>\bin\Win64_Shipping_Client` and threw
"not found in any DLL" for every type living under a module. That is not an edge case:
`TaleWorlds.MountAndBlade.View.dll` — owner of `CharacterTableau`, `BasicCharacterTableau` and
`AgentVisuals`, i.e. the entire tableau/encyclopedia render path — exists **only** in
`Modules\Native\bin\` and is absent from `bin\` outright. So does every `SandBox.*` type. Every agent
investigating #389 hit the same wall and fell back to raw `ilspycmd`.

Search order is now the primary bin dir, then each `Modules\<Name>\bin\Win64_Shipping_Client` holding
DLLs — modules shipping `TaleWorlds.*` assemblies before third-party ones, so an engine type never
loses a probe race to a mod vendoring the same type name. 18 dirs on this install.

- `dll-index.json` now records a **full path**; a bare filename cannot address a module-dir assembly.
  Legacy bare-filename entries still resolve (verified against the pre-existing `TaleWorlds.Library`
  entry), so the cache does not need clearing.
- Verified across all six paths: module-dir type, non-`TaleWorlds`-prefixed module type
  (`SandBox.ViewModelCollection`), fresh primary-bin namespace probe, legacy index entry, cache-hit
  short-circuit, and that the cached output is real C#.

### fix(docs): armory snapshot was described as byte-identical to live; it is not

`docs/reference/lotrlome-armory-snapshot/README.md` claimed `skins.xml` and `monsters.xml` were
"verified byte-identical to live". Both are stored CRLF here against LF live — `skins.xml` is
5,899,564 bytes vs 5,678,589, `monsters.xml` 69,267 vs 67,296. They are identical **after newline
normalisation** (md5 equal via `tr -d '\r'`, checked this session). A hash check taken at face value
would have reported spurious drift and triggered a pointless re-snapshot.

### docs: two defects filed from the #389 investigation

- **#389** — Isengard `uruk_hai`/`berserker` render as black silhouettes in the encyclopedia tableau.
  Root cause **not** determined; 12 candidates refuted with evidence, 2 hypotheses standing, and a
  3-factor in-game experiment matrix (race × body-property × armour family) recorded to settle it.
- **#390** — 42 crafting-piece/shield meta-meshes referenced by `LOTRLOME_Armory` but absent from the
  install (18 uruk weapon parts, 18 Mordor weapon parts, 5 Mordor shields, 1 Gondor shield). Long-
  shipping, invisible because they are `CONTENT WARNING`s rather than errors.

### feat(career): diamond career screen with per-choice icons and Active Effects (#388)

Replaces the May pip-strip layout after comparing both in-game. Each choice is now a diamond
carrying an icon — five per group, two groups per tier — with a persistent Active Effects
column showing taken keystones (gold) above passive totals summed per effect type, so two +5%
Damage picks read as one "+10% damage". Rank titles and lore group names are kept.

Ported as TAOM-owned code from the reference module's prototype, dropping ~800 of its ~940
lines: its state patch Harmony-scraped our own ViewModel from outside and its effects panel
polled a static because it could not bind — from inside, the VM simply publishes the lists,
accumulated during the group walk that already runs rather than a second registry pass.

- **No sprite bake needed.** The per-choice sprites authored in `taom_career_choices.xml`
  (`career_choice_*`) were never drawn — zero PNGs, zero atlas entries — which is why
  `IconSprite` was dead data bound by no prefab. Diamonds use already-baked banner icons:
  keystones show their career's own sigil (#380), passives an icon for their effect type.
- **Percent vs flat is keyed on effect type, not magnitude size** — the reference's
  "under 1 means percent" rule prints a 1.0 magnitude (100%) as "+1" and a +1 companion limit
  as "+100%". TAOM authors damage/ammo/speed as fractions and health/party size as counts.
- Custom widget registration turns out to be automatic: the engine collects every `Widget`
  subclass from any loaded assembly referencing GauntletUI. The `Taom` prefix on the widget is
  load-bearing — the factory keys on the simple type name across assemblies, ignoring
  namespace, so an unprefixed name would collide with the reference module's class.

Prefab bindings machine-checked against the ViewModel members (zero unbound). Suite 5565 green.

Not-tested: in-game render — static review cannot certify that a sprite draws or that a layout
lands where intended.

## 2026-08-05

### feat(diagnostics): [MemSample] memory telemetry + minidump triage — a tester's OOM CTD now self-identifies from the log (#385, #386, #387)

A tester CTD (16 GB machine, mid-battle) took a 1.3 GB dump parse to attribute: `0xC0000005`
near-null read at `TaleWorlds.Native.dll+0x58232c` — the facegen static-morph path on an engine
worker thread (null morph-index array), with the process at **20.3 GB commit** (15.65 GB private;
managed heap only 654 MB) and system commit at 23 GB of a 31.6 GB limit. Filed as #385; not the
#360 signature (the tester's v2.0.15 already contains Patch63). Three shipped responses:

- **`[MemSample]` telemetry (#386):** `MemoryPressureSampler` writes a durable INFO line every 30 s
  (MCM 10–120 s) — `privMB/wsMB/heapMB/sysCommitUsedMB/sysCommitLimitMB/availPhysMB/memLoad` — plus
  a session-capacity one-shot and a latched `WARN LOW COMMIT HEADROOM` (< 2 GB or < 10 % of limit,
  512 MB re-arm hysteresis). Reader is direct P/Invoke (`GlobalMemoryStatusEx` +
  `GetProcessMemoryInfo`): measured 84 µs vs 7,711 µs for net472 `Process.PrivateMemorySize64`.
  Gates on its OWN MCM toggle, not the master (phase logging off must not kill crash forensics).
  Phase lines `EncounterStart/MissionInitialize/BattlePlayable/ExitBegin/MapResumed` gained
  ` privMB= wsMB=` tokens (`GcStats()`→`MemStats()`). 26 new C# tests; full suite 5,546 green.
- **Triage tool:** `tools/triage_battle_load.py` parses `[MemSample]`, renders a Memory-trend
  section and a `MEMORY PRESSURE` note ("the phase verdict above may be a symptom, not the
  cause"); verdict lattice/exit codes unchanged; old logs byte-identical; `--mem-threshold-mb`;
  `--json` gains a `memory` block. Twin literal tests pin the C#↔Python line contract. 23 new tests.
- **`native_crash_triage.py --dump` (#387):** decodes TW CrashDumper minidumps directly from a
  player bundle — exception, faulting module+RVA, commit split (image/private/mapped), and a
  return-address stack scan (with the observed per-thread `Rva=0` MemoryList fallback) — then
  chains into the existing RVA disassembly pipeline. Smoke-verified against the #385 dump
  (reproduces module+RVA, thread, 20.33 GB split, and both confirmed stack frames). 14 new tests;
  `/native-crash-triage` skill documents the no-Event-Log path.

Audit side: `docs/investigations/native-commit-audit-2026-08.md` written (Phase 0 ledger) — static
inventory puts ~2.7–4.5 GB of the private commit on statically-verified levers (41 always-loaded
4096² banner-icon atlases from 369 icons authored at 1024²; FactionMap textures loaded and never
freed; RuntimeDataCache readability is the decisive open question, runbook Phases 1–2).

Deep review (6 agents): 2 HIGH fixed same-session — the C#↔Python percent-threshold mirrors
disagreed in the ~1 MB band below the 10% line (integer-floor vs exact comparison; boundary pins
added both sides), and truncated dumps crashed `--dump` with a traceback (`struct.error` is not a
`ValueError`; broadened catches + 8 negative/degradation tests). Post-fix: C# 5,550 / Python 361
green. RCA: `docs/reviews/rca-memsample-telemetry-2026-08-05.md`.

### feat(career): career UX arc — 3 runtime fixes + 5 features from a vetted external submission (#377–#384)

An external contributor delivered a working reference module (`TAOM-Career-UX-Upstream-2026-08-05`)
prototyping a career-UX overhaul, with an upstream plan written in terms of TAOM's own files.
Adopted via `/adopt-external`: security pass clean (zero network/exec/exfil; DLL never run),
three verification agents checked every load-bearing claim against TAOM source, the 1.4.7
decompile, and ModuleData before anything was built. Full verdict + findings:
`~/.claude/plans/review-this-and-identify-bubbly-moon.md`. Suite 5502 green.

- **#377 ability runtime:** buff-tracker entries are contribution-counted and retire on the last
  restore (pre-fix a zeroed entry lingered all mission — "buff present" was meaningless);
  `CareerAbility` gained the active window (`BeginActiveWindow` fed the same mutation-extended
  duration that schedules the restores); a controlled-agent identity gate stops the HUD showing —
  and a V-press silently WASTING — the hero's ability while the player controls a soldier
  (co-op / after-wounded).
- **#378 button feel:** career button got an `Id`, a real Brush (Gauntlet derives press/hover
  states ONLY from a Brush — a bare `Sprite=` has none, decompile-verified), and a click sound.
  CareerScreen's `+`/`−` buttons had the same defect — same treatment, no new art (per-style
  ColorFactor on the existing sprites).
- **#379 unspent-points badge** on the character-screen button — service-level computation
  (`ICareerRegistry.GetUnspentPoints`, single-sourced with the career screen), deliberately
  independent of career-screen state.
- **#380 per-career keystone glyphs:** data-driven `keystone_icon` on all 50 `<Career>` rows
  (contributor's 49-entry banner-icon table, verified id-by-id + sprite-registered; +1 for the
  disabled `far_harad_halftroll`), rendered as a tinted medallion on keystone nodes. NO culture
  fallback by design (the reference's fallback regressed Rivendell to its live clan sigil).
  Art curation pass deferred — caveats in #380.
- **#381 keystone branch exclusivity (deliberate design adoption):** one keystone per tier;
  a completed tier-3 group reopens all; taken stones are grandfathered + refundable. Extracted
  to `KeystoneExclusivityRule` — the same rule now also DIMS closed keystones instead of
  silently eating the click. (Core one-per-tier predated this arc; exemption + display gating
  are new. The reference module carried this rule UNDOCUMENTED — flagged, then adopted on
  explicit decision.)
- **#382 energy bar:** the 130×166 square ability panel (which sat on the tournament prize lane)
  is retired — controller/VM/prefab deleted. Replaced by a slim bar docked in the AgentStatus
  cluster via TAOM's first mission-screen PrefabExtension (container-safety verified against the
  installed DLLs: the view does zero child traversal) + a `[ViewModelMixin("Tick")]` on
  `MissionAgentStatusVM`, using the native ShieldHealthBar brushes and a derived refill rescale
  that kills the measured empty→40% snap. Key chip reads the bound key from the input adapter.
- **#383 damage attribution:** combat-log `+N from ability` per qualifying hit, computed from
  TAOM's own applied buff state (never reflection; exact while the ability buff is the sole
  multiplicative term). Utility abilities say so once per activation instead of staying silent.
  `OnScoreHit` signature pinned against the installed v1.4.7 DLL first (two `in` params silently
  no-op a wrong override).
- **#384 diagnostics:** `taom.print_hud_layout` — on-demand, bounded widget-tree dump with real
  rectangles (the reference's layout dumps found three bugs code reading did not), with its
  collapsed-tree warning ported.

Rejected from the submission: the module itself (prefab shadow-clones, blanked AbilityHUD, an
undisclosed full CareerScreen rewrite), the hardcoded-8s wall-clock window, static hand-off
state, the sibling-widget button hack, the unbounded logger, and a private-member Harmony
binding. Kill-switch audit found 2 of its 6 advertised flags dead/fictional.

**Deep review (5 agents) + fixes, same session** (RCA: `docs/reviews/rca-career-ux-arc-2026-08-05.md`):
the compatibility agent verified the entire energy-bar architecture against the installed DLLs
(movie datasource chain, UIExtenderEx filename-keyed prefab patching, InsertType semantics) and
caught two silent unregistered-brush references; the data-flow agent caught a hero-death path
that cleared ally buffs without recomputing their engine-side stats. Fixed: ally refresh via
`GetBuffedAllyIndices` snapshot + `FindAgentWithIndex` loop; badge brush registered as
`CareerSystem.Badge.Text`; the three copies of the career-hero identity predicate extracted to
`CareerHeroIdentityGate` (one had already drifted); `OnScoreHit` body extracted to
`AbilityDamageAttributionReporter` (ADR-002 thinning); a real-XML invariant test now pins
worst-case cooldown ≥ ability duration (the 5s floor sits below the 8–10s durations — currently
unreachable, future retunes fail CI). `gui-ui.md` sprite-verification rule widened to Brush names.
Suite 5506 green after fixes.

Known limitation: `CharacterDeveloper.SkillNameText`/`.DescriptionText` (CareerScreen.xml ×14,
pre-existing since May) and `ButtonBrush1.Text` (PresetsOverlay.xml) are unregistered brush names
rendering with default styling — deliberately deferred (the career screen was visually approved
in that state); cleanup needs its own in-game pass. RCA finding #3.

**Codex adversarial pass (gpt-5.5 xhigh): 0 P1 / 2 P2; all six architecture Known Suspects
DISPUTED with decompiled evidence** — independently confirming the energy-bar design (Review 81,
`docs/reviews/raw/codex-adversarial-career-ux-arc-2026-08-05.md`). Fixed: `isSiegeEngineHit` now
filtered in `OnScoreHit` (siege missiles arrive with the operating player as affector; attributing
ability bonus there would be a false claim), and activation is blocked while the active window is
live (`IsAbilityActive` gate) — Codex proved the overlap reachable via olog_hai's Duration
mutations (16s window vs 15s cooldown floor), a case both the Claude data-flow agent and this
session's own invariant test missed by summing only CooldownReduction mutations; that test was
replaced by the structural gate + its unit tests. Codex's mount→rider normalization suggestion was
DISPUTED (its quoted vanilla code does not exist in the installed Mission.cs). Suite 5509 green.

Not-tested: in-game render/behavior (bar visuals, medallions, badge, sounds, attribution lines —
static review cannot certify a sprite renders); smoke checklist in the plan file + #377–#384.
Owed: 12-language `/localize` for the two new strings (English registered; translation gated on
`ANTHROPIC_API_KEY`), keystone art curation.

### chore(repo): leftover sweep — executed

A 15-agent verified sweep (7 finder modalities, adversarial verification per finding, completeness
critic) found 90 leftovers; the confirmed set is executed in the commits that follow. This entry
is built incrementally per commit.

**RuntimeDataCache untracked and removed (5.1 GB).** 116 engine-generated `.rdc`/`.rdc.rtemp`
files (18 of them interrupted-write temp files, one 67 MB) had been committed by a game→repo sync
(last touch 2026-07-07). Bannerlord regenerates the cache in the game module folder; nothing in
the build references the repo copy (grep over build.ps1/csproj/props: zero hits). Removed from
index and disk; `Main/_Module/RuntimeDataCache/` gitignored. The `__pycache__`/`*.pyc` ignore
also went repo-global (was tools/-scoped) and `.taom-src/` is now ignored (the live cache is
`~/.taom-src/`). The `.git` store still holds the historical blobs (~3.2 GiB) — reclaiming that
needs a history rewrite, deliberately not done.

**Tracked junk removed** (each verified unreferenced by the sweep's adversarial pass):
`_mine.patch` (merge-conflict scratch that rode into a docs commit), `.taom-src/` (v1.3.15
decompile cache accidentally committed — the live cache is `~/.taom-src/`), the repo's only
tracked `.pyc` (source never committed), four vestigial `.gitkeep` files in directories holding
15–1,376 tracked files, `taom_career_starting_equipment.xml.bak-startergear` (tool backup whose
suffix evaded the `*.bak` ignore; rollback lives in git history), four spent `issue-body-*.md`
staging files (all four verified as real GitHub issues — #110/#111/#115/#117), the v1.3.12
map-marker research note (zero inbound references, three engine versions stale), and
`MORNING-QUEUE.md` (the one creature-pipeline morning file with no live reference — the other
three are append targets of the active autonomous-loop prompt and STAY).

**Gitignored disk cruft deleted (no history impact, ~625 MB):** `tools/runes/preview/` (602 MB
of preview renders — user call; `raw_ai/` inputs + pipeline scripts untouched), the four
extracted `.vendor-source/` trees (~65 MB — the six pinned tarballs stay and re-extract
losslessly), stale `Dependencies/bin/x64/` + the orphaned `Dependencies/ThirdParty/NativeSkinFixes/`
artifact dir, both TestResults dirs, two empty root dirs (`BehaviorTreeWrapper/`, `.localappdata/`),
the stale `/freeze` boundary file (pointed at the pre-move repo path), a dead-PID
`scheduled_tasks.lock`, the diverged education-templates `.bak`, the April `SP.zip` localization
snapshot, and the NativeSkinFixes `.exp`/`.lib` linker byproducts — the `.pdb` STAYS (only
symbolization of the exact shipped parked binary). The one disk-only April codex `.log` moved
into `docs/reviews/raw/` (the sanctioned transcript home) instead of dying in a tracked dir it
was never committed to.

**Dead links 11 → 0; stale pointers fixed.** The five howdah links were one directory level
short; the ten pre-move-memory-slug links (two RCAs, faction-map.md, and four in live
`/deep-review` skill text) de-linked to plain citations with lessons-file pointers;
`/author-armor` + `/new-culture` no longer send readers to a CLAUDE.md table that moved to
`armory-guide.md` in July; `/freeze`'s example and the ARP-retarget exec path (doc + script
header) and the substance-painter project key lose the dead pre-move repo path;
`native-skin-fixes.md` no longer claims "TAOM is on v1.4.5"; AGENTS.md's Harmony table gets the
header row whose absence broke its markdown rendering.

**Doc graph reconnected.** The two orphaned-but-live feature docs (blow-diagnostics,
clan-heraldry) plus `SAVE-REPAIR-GUIDE.md`, `scene-entities.md`, and the `docs/archive/` README
now have INDEX.md rows (orphan_features 2 → 0); the erebor kitbash `runes.md` joins its README's
coverage table; the completed `SESSION-S5a-S5b-PROMPT.md` migration prompt moved to
`docs/archive/` with its artifact-class siblings.

**FactionMap duplicate sprites: 61 files / 545.7 MB deleted (1.0 GB → 412 MB).** An id-flow audit
proved the deletion safe in the strongest form available: the image id travels VERBATIM from
`factions.json`/`regions.json` through `FactionConfigProvider` → `FactionSelectionService` →
`FactionImageWidget` — never composed from campaign objects, never serialized — so the reference
universe is closed. Every deleted file is a byte-identical duplicate of a kept sibling AND has
zero literal hits across 3,552 repo text files, both staging JSONs, the CC prefab's RegionName
set, the map-assembly tools, and the live deployed factions.json (repo/live parity verified).
Each of the 54 duplicate groups keeps ≥1 copy. Missing ids fail SOFT (`_loadFailed` + log, no
crash) — already the live condition for 27 referenced ids that never had art (pre-existing gap,
noted, untouched). Eight `REMOVED_REGIONS`-era unique-content files kept (sole copies of art —
archival call, not a dedup). The borderline `banner_kingdom_of_gondor.png` went too: its only
reference is a string assertion in a mocked test that opens no file, and the id is unreachable
at runtime.

**tools/ triage.** Five scripts hardcoded the dead pre-move repo path and could not run: four
fixed to script-relative paths (verified resolving — `assign_lord_equipment`,
`generate_batch2_wanderers`, `extract_wanderers`, `Generate-SceneEntitiesDoc.ps1`);
`assign_xslt_lord_equipment.py` turned out dead beyond a path fix — its `splords.xslt` target
never existed in this repo and its sections are hardcoded line ranges — so it retired to
`tools/oneoff/` and left the README. Nine finished one-offs total moved to `tools/oneoff/` per
the convention (equipment-type 1.4.3 migration pair, roster-suggestion + culture-coverage pair,
bandit-hideout pair, Rhun settlement owners, career-name singularizer, the xslt equipment tool) —
each verified: zero README mentions (except the retired one), last touched 2.5+ months ago,
referenced only by history/archive docs. Five of those historical references were markdown LINKS
that broke on the move — caught by the lint gate, repointed to `tools/oneoff/`.

**Linter exemptions + settings.** `REVIEW-LOG`, `audit-*`, and `docs/reviews/lessons/` joined the
stale-version exemptions (they quote old versions as historical record, same class as `rca-*`) —
stale-version noise 62 → 29, and the remainder is deliberate history (native-skin-fixes' pinned
v1.3.15 RVAs, port narratives, the ADR-010 context). settings.local.json drops the
never-again-matchable `C:/non/existent/path` test rule and a doubled-slash duplicate read grant;
`/security-scan` clean at HIGH+ after the edit.

**Git objects.** Deleted: `bannerlord-1.3.15` + `fix/troll-guard-exclusion` (local refs, fully
merged + pushed — remote archives remain) and `verify-improve` (pure merge scaffolding over the
kept `impl-*` branches); pruned the gone `origin/fix/career-screen-collapse-on-levelup` tracking
ref; dropped the career-passive stash (strict subset of commit 9034e5dc) and the deleted-worktree
stash (May permissions delta). **KEPT pending user review:** the taom-troopweight stash — its
untracked component holds three OffspringDiagnostics files that exist nowhere in git history, and
its Patch49 number now collides with trunk's `Patch49_ArmyGatheringNreGuard`. Also kept:
`feature/dependencies-audit` + `fix/lotr-issues-sandbox-suppression` (unmerged unique work — one
commit holds a 12-language translation-backlog emit), `origin/bannerlord-1.3.15` (archive),
collaborator branches, the NativeSkinFixes `.pdb`, the 8 `REMOVED_REGIONS`-era unique-art files,
and `docs/issues-drafts/` (live generator + erratum content not on GitHub — the sweep's verifier
refuted its deletion).

**Verification:** full suite green after everything — build 0 errors, tests 5,448 passed / 0
failed / 2 skipped (same counts as pre-sweep); `lint_docs --fail-on-drift` rc=0 with dead_links
0, orphan_features 0; backlinks re-swept. Working-tree recovery this sweep: ~5.1 GB
(RuntimeDataCache) + 545.7 MB (FactionMap dupes) tracked, ~690 MB gitignored disk.

### refactor(harness): eager-context diet round 2 — CLAUDE.md prune + always-load rules + hook fixes

Second-round diet of the eager context load (round 1: 174 KB → 91 KB → 42 KB, July 2026).
CLAUDE.md had regrown to 44,681 B — over its own 44,000 B warn threshold — and the always-load
rules (48,794 B) had quietly become a bigger eager block than CLAUDE.md itself. This entry is
built incrementally across the diet's commits (the July 18 pass shipped without one and its
rationale nearly vanished).

**Phase 0 — the warn threshold was hard-gating commits.** `lint_docs.py --fail-on-drift` failed
on ANY budget finding, including `size-warn` — contrary to the `CLAUDE_MD_WARN_BYTES` constant's
own "report-only early warning" comment. In practice 44,000 B, not 46,000 B, was the commit
gate, and AGENTS.md (39,441 B) sat 559 B from tripping the same way. Warn findings now report
without gating; only hard violations (size / table-row / prose-line) block. Also corrected the
`check-doc-config-drift.sh` deny message, which still claimed a "60KB" cap (real: 46 KB).
Measured before/after: rc=1 → rc=0 with the size-warn still printed. A prior audit's claim that
CLAUDE.md L166 breaks the 400-char row cap was false — 400 chars exactly (408 *bytes*; the
auditor counted bytes, em-dashes are 3 in UTF-8).

**Scoped Rules → `docs/reference/rules-catalog.md`.** The 22-row rule table moved out verbatim
(nothing deleted — relocated). The 7 always-load rows were the worst offenders: they summarized
rules whose *full text* is already in context every session, a pure double-charge. CLAUDE.md
keeps the `paths:`-convention note + a stub.

**Working Discipline → `.claude/rules/working-discipline.md`.** Fork discipline, autonomous-loop
stewardship, the TodoWrite bar, inline-hook activation, and edit-scope discipline existed nowhere
but CLAUDE.md — moved verbatim into a new always-load rule (grep-verified: 0 of 20 moved lines
missing). Eager-budget-neutral, but CLAUDE.md stays an index. Three referrers updated
(evidence-over-claims, autonomous-loop-prompt, karpathy-autoresearch notes); the heading survives
in CLAUDE.md as a pointer.

**MCP Servers → `docs/reference/mcp-servers.md`.** The server table, full research lookup-order,
dual-build layout, and configuration moved out; the `imagine` server — enabled in `.mcp.json`
since it was added but absent from the CLAUDE.md table — is now documented. CLAUDE.md keeps the
Usage Guide (which tool for which task) + a 4-line lookup-order summary, the one eager copy of
what was a 4-way duplicate (research-guide, engine-and-toolchain, taom-src SKILL, operating
manual). The new doc also carries a plugin-overlap routing table (deep-review vs code-review,
deslop vs the now-disabled code-simplifier).

**Custom Agents collapsed; Skill Routing trimmed.** The 5-row agent table duplicated the agents'
eagerly-loaded descriptions — replaced by one paragraph that keeps the 6-point briefing convention
imperative (subagents don't inherit CLAUDE.md; the full checklist lives in
agent-operating-manual.md + agent-teams.md "Case studies"). Skill Routing dropped the
per-row "what it does" halves (each skill's eager description already says it), keeping triggers
and confidence gates; the Soft-suggest and Never-auto-invoke tables became compact lists with the
same semantics.

**Final thinning + factual fixes.** Doc Lookup → 4-line pointer (the 10 inline rows all
grep-verified present in `doc-lookup.md`, whose provenance note now says no rows stay inline);
Documentation Requirements + GitHub Issue & KB Requirements merged into one block under the
load-bearing "Documentation Requirements (MANDATORY)" heading (issue-body templates verified in
`completion-workflow.md:14-27`); Equipment & Armory → 6 lines (canonical-folder example,
`validate_all_troop_refs.py`, and shield-bone rules verified in `armory-guide.md` /
`armory-shield-audit.md`). Corrected two stale counts: "42 skills load eagerly" → 40 of 42
(two are `disable-model-invocation`), "25 hooks" → 24 registrations across 9 events.

**Always-load rules: provenance stripped to `docs/reference/rule-provenance.md`.** The "Why this
rule exists" / "Relationship to other rules" / "Source" sections of five always-load rules
(evidence-over-claims, simplicity-criterion, think-before-coding, response-style, ai-prose-style)
are import history and cross-rule commentary — zero runtime behavior, paid on every turn. Moved
verbatim; each rule keeps a one-line pointer (simplicity-criterion also keeps its don't-conflate
guard inline). The five identical ~6-line "no `paths:` intentionally" comment blocks collapsed to
one line each.

**harness-facts' parallel-agent sections → agent-teams.md "Case studies" — a section that did not
exist.** Three harness-facts pointers (and CLAUDE.md's briefing item 6) cited agent-teams.md
"Case studies"; agent-teams.md had no such heading. The move creates it: build-watcher cascade,
parallel-builder-brief seam rule, and worktree isolation now live there verbatim, turning three
dangling references into real ones. harness-facts keeps the operative one-liner (worktree
isolation for single-owner files, one pinned solution per shared sub-problem) + a pointer;
"Last verified" bumped to 2026-08-05.

**`response-style.md` + `ai-prose-style.md` merged into `output-style.md`.** The two rules split
one concern (chat replies vs written artifacts), cross-referenced each other four times, and
re-listed the same banned phrases. Now one rule: Part 1 (reply openings + confidence tags),
Part 2 (AI-prose tells + the TAOM house-style carve-out) — operative text verbatim, one
banned-phrase deferral, sources already in the provenance doc. Referrers updated (`/humanizer`
×5, CLAUDE.md soft-suggest, rules-catalog, rule-provenance headings). Size-neutral (+0.4 KB) —
the projected saving landed in the provenance strip; the merge's win is one banned-phrase list
and one less cross-referenced file.

**`check-changelog-updated.sh` finally mutes.** The last Stop hook without muting re-injected its
~45-word reminder on every turn of any session with dirty source and no CHANGELOG edit (~3.5K
tokens over a long session). It predated the hook-authoring "mirror the sibling's FULL convention
set" rule. Now mirrors `check-verification-evidence.sh`: `.changelog-reminded` one-shot marker,
cleared when CHANGELOG becomes dirty/staged or the streak ends. Tested live: reminds once,
silent on repeat, re-arms on revert.

**Post-compaction rehydration pointed at 5-month-stale memory — root cause: a silently-ignored
settings key.** `settings.json` set `"autoMemoryDirectory": ".claude/memory"`, but the key
accepts ONLY absolute or `~/`-prefixed paths (doc-verified: code.claude.com settings + memory
docs) — a relative value is silently ignored and the harness uses the default
`~/.claude/projects/e--repos-TAOM/memory/`. So the live memory accrued at the default path while
the tracked `.claude/memory/` copy froze in March, and `post-compact.sh` told every
post-compaction session to rehydrate from the frozen copy. Fixed three ways: the invalid key
removed; `post-compact.sh` now derives the live path (slug derivation + basename fallback,
fail-open) — live test resolves the real 58-line index; the tracked copy deleted after verifying
all six of its facts exist in live memory or repo rules (the live `user-profile.md` even carries
a `<!-- source: user_profile.md -->` ingest marker from the June reorg). Fact table updated in
`harness-facts.md` FIRST per its own §1 rule. `/security-scan` clean at HIGH+ after the settings
edit (1 pre-existing MED: unpinned `npx -y` filesystem MCP).

**hooks-catalog.md re-synced with reality.** `session-stop.sh` was listed under Stop but has been
wired to SessionEnd since 2026-07-18; `notify-test-results.sh` (wired in settings.json) had no
row; the `/freeze` skill-inline hook is now noted; `suggest-compact.sh`'s boundary-aware half and
the new muting/live-memory behaviors are documented; header carries the audited count (24
scripts / 24 registrations / 9 events).

**Cruft + plugin cleanup.** Deleted (gitignored, no history impact): the 2.8 MB pre-rotation
`session-log.md.1`, `agent-audit.log.1`, and a June 12 `/context-save` snapshot whose state lives
in the `improve` feature memory. The "empty" stale pre-move project slug dir
(`.claude/projects/c--Users-mikew-source-repos-TAOM/`) turned out to hold one TRACKED memory file
the audit missed — `feedback_sprite_atlas_cleanup.md`, whose move-between-atlas-categories /
delete-install-PNGs / duplicate-key-crash fact existed nowhere else. Recovered into
`docs/reviews/lessons/localization-ui.md` (now 15 lessons) before deleting the dir.
`code-simplifier` plugin disabled (user decision — `/deslop` covers it; `code-review` stays for
`/code-review ultra`); settings.local.json dropped 4 dead one-off `sed` rules, the
old-repo-path `cp` rule, and the orphaned claude-code-templates marketplace directory grant.
`/security-scan` clean at HIGH+ after both settings edits.

**context-budget scan.sh: plugins were invisible; the MCP table was stale.** New `scan_plugins()`
reads `enabledPlugins` and measures each plugin's command/skill descriptions from the local
plugin cache (7 plugins, ~259 tok — previously uncounted; a trailing-`\r` from Windows python on
pipes initially broke every cache lookup). `SERVER_TOOLS` gains `taom-moduledata` (EXACT 9,
counted from `taom_mcp_server.py`) and `imagine` (HEURISTIC ~5, when-authed). The report header
no longer hardcodes "Claude Opus 4.7" as the context model. The memory locator needed no fix —
the July audit's failure prediction assumed the fallback path; the derived slug hits directly.

**Baseline re-recorded** (`docs/context-budget-baseline.md`): eager-excl-MCP **~26.1K → ~16.8K
tokens (−35%)**. CLAUDE.md 4,717 tok / 28.4 KB (the July doc still said 14,173 / 91 KB — 3×
overstated after two undocumented restructures); always-load rules 43.5 KB incl. the CLAUDE.md
move-in; plugins measured for the first time (7, ~259 tok). MCP deferral behind ToolSearch
re-confirmed empirically this session.

**Net effect (measured):** CLAUDE.md 44,681 → 28,384 B (−36.5%, fourth restructure: 174 → 91 →
42 → 28 KB); always-load rules' original content 48,794 → 38,871 B (−20%); the every-turn
CHANGELOG nag muted (~3.5K tok/long session); post-compaction rehydration reads live memory
instead of a March snapshot; one unmigrated lesson recovered from a dir two audits called empty.
Three new reference docs (rules-catalog, mcp-servers, rule-provenance), one new always-load rule
(working-discipline), one merged rule (output-style), two rules deleted. Every move
grep-verified against its destination before the source was deleted (Track C4/C5); `--fail-on-drift`
rc=0 and dead-links held at 11 after every commit. Caps stay 46K/44K by user decision — regrowth
is watched, not gated, below 44 KB.

### fix(enlistment): four seam bugs an independent review found after ours passed

A Codex adversarial pass over the finished enlistment work came back 0 P1 / 4 P2, and every one
of the four sat at a seam that no single-file read covers — which is the argument for running an
independent pass at all, since ours had just finished clean.

An honorable discharge **erased the wages it was meant to settle**: the pipeline clears the
service record before raising its ended-event, so the final settlement paid a null hero id and
the gold adapter silently no-oped on the failed lookup. A player who served through a stretch
where the commander could not pay lost every deferred coin at the exact moment they were owed it.

Food deliveries **completed for free**. The count read `ItemRoster.TotalFood`, which folds
livestock in via `HorseComponent.MeatCount`, while the consume step only removed `IsFood` stacks —
so a player driving cattle satisfied the requirement and handed over nothing. The count now ranges
over exactly what consumption can take, and consumption reports what it actually moved so
completion keys on the handover rather than on the promise.

The wait-menu leave option had **no co-op authority gate**, unlike every other discharge path; a
client could clear its own service while the host stayed enlisted. And **NaN campaign days failed
open in the duty scheduler** — cooldown comparisons went false, offering duties straight through
the cooldown, while the expiry check went false forever, stranding a duty and the party it spawned.

Also this session: the merit sampler now scores whether you fought the way your assignment asks
(archers hold an 18-50m line, cavalry work the flanks, support stays with the commander, infantry
holds formation *and* stays in contact) — that config knob previously could never fire. And the
equipment issue-ledger moved into the save, so a full game restart no longer re-allows a free kit
draw per rank.

Research: ItemRoster.TotalFood vs Item.IsFood (installed 1.4.7 via taom-src)
Not-tested: in-game delivery with livestock aboard; merit scoring across a real battle
Save-compat: two additive keys in the existing `_taom_enlistment_content` section

## 2026-08-04

### feat(validate): the mesh validator can now ask which packaged art has no item

Players reported that not all the Gondor helmets are in the game — specifically that they cannot
buy them. The obvious suspect was an XML gap: helmet meshes shipped in `LOTRLOME_Armory`'s asset
packages with no matching `<Item>` entry. `validate_mesh_refs.py` could not answer that, because
all three of its tiers ask the same direction — *does every referenced mesh exist?* — and the
question here is the reverse.

`--unreferenced` runs that reverse pass over the Tier B present-set, with `--prefix` to narrow the
candidate side to one culture. It emits `UNREFERENCED_MESH` at INFO, so the exit code is unchanged
and the mode can't turn an audit into a red build. Two details are load-bearing rather than
cosmetic. `_slim` female body variants are suppressed when their base mesh is referenced — the
engine resolves them by appending the suffix, so they never appear in item XML, and without the
suppression the Gondor set alone reports 100 false positives. And matching is case-insensitive
here, unlike Tier B's exact lookup: in this direction casing drift should make a mesh look
referenced, never manufacture an orphan.

The audit falsified the hypothesis. Gondor is **0 orphans of 418 packaged meshes** — 318
referenced, 100 `_slim` — and no Gondor item references a mesh that isn't packaged, across all
five armor slots. Helmets are 109 meshes to 109 references, exactly 1:1. There are 0 duplicate
item ids among the 3297 armory items, every item is `is_merchandise="true"`, and the six
`gondor/` files all load via the directory-path `XmlNode`.

The real cause is reachability, not data. `CultureMarketplace` injects 6 items per town per day
from a flat pool holding the whole Gondor catalogue, so any one specific helmet is roughly a 1%
daily chance in a given town — the "probabilistic design vs deterministic expectation" gap that
feature's own RCA already named. Levers exist (`min_stock` routing, a weight boost) and none were
pulled; this pass is report-only.

Armory-wide the same scan finds 549 unreferenced meshes, none of them Gondor armor. 276 are
`clo_*` cloth variants (201 with an item-referenced base, so likely a naming convention like
`_slim` — unconfirmed, do not act on it yet) and 69 are `sm_*` crafting pieces, including the
Gondor poleaxe that was deliberately removed after it crashed new-campaign load. Also corrected
`armory-guide.md`, which listed 5 of the 17 Gondor region prefixes. +13 tests (33 → 46).

Audit: `docs/reviews/audit-gondor-armory-2026-08-04.md`.

Not-tested: the marketplace explanation is read from source and config, not from a live session —
the confirming evidence is the `[CultureMarketplace] gondor: N items` boot log line.

### feat(enlistment): core service loop for serving in a lord's party (#375, checkpoint 1 of N)

The Serve-as-Soldier rewrite's foundation: a persisted 8-state machine (petition → oath →
attached service, with battle, captivity, commander-grace and discharge states) replaces the
donor mod's visibility-flags-as-state hack. Party presence flags are now OUTPUTS asserted from
state; a single fixed-order discharge pipeline is the only exit and unconditionally restores the
party BEFORE clearing the record — the donor's commission-exit softlock (record cleared, party
hidden forever) is unrepresentable, and `DischargePipelineInvariantTests` pins that for all 8
discharge reasons. A captured commander now grants a 7-day grace window with the player freed to
roam, replacing the donor's split policy where the wait menu discharged instantly but the daily
tick didn't.

Ships as: 4 new adapters (commander snapshot, party attachment/presence, encounter, game menu —
positions are `CampaignVec2` on 1.4.7, camera-follow lives on `PartyBase`), the hourly reconciler
as sole terminal authority, an Entity-State-Matrix load normalizer (never leaves an ownerless
hidden MainParty — including rescuing foreign saves), the service wait menu with a config-driven
fail-open redirect guard, battle interception that joins the commander's side (state flips before
any menu push, so the guard can't eat battle menus), and `Patch66_Enlistment`: a
`GameMenuManager.SetNextMenu` prefix, a `MapState.EnterMenuMode` recovery postfix, and four
`LordConversationsCampaignBehavior` condition suppressions — all six targets pinned by
binding-drift tests against the installed DLLs. The donor's `MapState.OnMapConversationOver`
patch is replaced by the `ConversationEnded` campaign event.

158 feature tests; suite 5030 green. Not yet player-reachable — the enlist dialog chain is the
next checkpoint. Deep-review findings (7, all fixed in-session):
`docs/reviews/rca-enlistment-core-2026-08-04.md`.

Research: GameMenuManager.SetNextMenu, MapState, LordConversationsCampaignBehavior,
PlayerEncounter, MobileParty, CampaignEvents (installed 1.4.7 via taom-src)
Not-tested: Harmony patch invocation + wait-menu rendering (requires live game)
Save-compat: New feature — primitive-dict SyncData under `_taom_enlistment`, versioned, no
SaveableTypeDefiner; absent keys load as not-enlisted.

### feat(validate): dwarves can no longer be authored as cavalry

The ask was to check the lords XML and make every dwarven lord Infantry or Ranged. The audit says
they already are: all 36 Erebor lords are `default_group="Infantry"`, none of their equipment
rosters carries a mount, `lords.xslt` never touches a dwarf, and across every installed module all
185 `race="dwarf"` characters come out Infantry or Ranged. Nothing to fix — so the work became
making it stay that way.

`MOUNTED_DWARF` fires on a dwarf tagged `Cavalry`/`HorseArcher`, and on a dwarf who can reach a
`slot="Horse"` item through their own inline roster or a standalone roster they name. Both halves
are load-bearing, which decompiling v1.4.7 settled: `CharacterObject.GetFormationClass()` overrides
the base and, when `IsHero`, ignores `default_group` entirely — it reads `BattleEquipment` and
returns Cavalry for any `HasHorseComponent` item in the Horse slot. So `default_group="Infantry"` on
a lord holding a horse buys nothing, and an enum-only check would have waved him through. For a
troop the enum *is* the formation, and for a hero it still drives the party-screen icon, so both
remain worth checking.

This is the data-layer half of an invariant TAOM already enforced at runtime in exactly one place:
`Patch46_TournamentDwarfDismount` strips Horse and HorseHarness from dwarf tournament participants
because the dwarf skeleton's rider bone is misaligned and a mounted dwarf spawns inside the horse
mesh.

Player character-creation and career-starter rosters are deliberately out of scope — no
`NPCCharacter` references them, and all 12 custom cultures ship the same sumpter-horse template, so
gating them would flag shared vanilla parity rather than a dwarf defect.

Also dropped `"erebor"` from `HORSE_CULTURES` in `tools/fix_lord_cultures_and_mounts.py`. That
script is already dead — it points `SPLORDS_XSLT` at a `splords.xslt` that does not exist and
crashes in step 1 — but repairing it later would have injected `Item.charger` into the Erebor lord
battle rosters and recreated the exact defect this check now blocks.

Research: `CharacterObject.GetFormationClass`, `BasicCharacterObject.Deserialize`, `EquipmentIndex`
Not-tested: nothing — the check is covered by 11 unit tests plus a seeded-defect control run

### fix(campaign): a culture with no settlements crashed the daily clan tick

Crash report `099f650c` — `InvalidOperationException: Sequence contains no matching element`, out
of `Campaign.Tick`, with no TAOM patch anywhere on the stack. Vanilla
`HeroSpawnCampaignBehavior.SpawnLordParty` ends with
`Settlement.All.First(x => x.Culture == hero.Culture)` — a `First`, not a `FirstOrDefault` — reached
whenever the hero's faction has no `InitialHomeSettlement`. That is safe in Calradia because every
culture owns land. It is not safe here: `TAOM_Map/settlements.xslt` deletes every vanilla
settlement, and the 988 replacements covered 27 of the 38 defined cultures.

`Patch65_LandlessCultureSpawnGuard` repairs the precondition rather than reimplementing the method
— it hands the faction a home settlement so vanilla takes the branch above the throwing line, and
everything downstream stays vanilla. The anchor is picked deterministically (hero home → born
settlement → clan leader's settlement → nearest non-hostile → nearest), and
`Clan.InitialHomeSettlement` is a `[SaveableProperty]`, so it is one write per broken faction
rather than a per-tick patch-up. An `InvalidOperationException`-only finalizer backstops anything
that cannot be anchored; the caller already null-checks the result, so the lord raises no party
that day instead of ending the campaign.

The other half of the trigger is a faction with no initial home settlement, which a mod creates by
calling `Clan.CreateClan` without `SetInitialHomeSettlement`. `Warlord` v1.1.6.1 is the only
clan-creating mod in the reporter's load order, but it is not installed here and was never
decompiled — that attribution is unconfirmed, and the guard does not depend on it. Issue #374.

### fix(khand): the Variags owned the map's Khand and none of its culture

`battania` is TAOM's **Variag** culture — 7.3 KB of culture XSLT, 26 notable templates, 68
`*_khand` NPCCharacter bindings, a kingdom, 8 clans, 18 TAOM-authored lords. The settlements were never migrated
with it. All 27 K-series settlements stayed `Culture.khuzait` while the Variag clans held ten of
them, Sturlurtsa Khand included, so Khand produced Easterling notables, volunteers, guards and
marketplace stock and the Variag culture owned nothing. `CultureConversion` could not repair it:
its timers are seeded by `OnSettlementOwnerChangedEvent`, so a fief whose culture never matched its
owner from day 1 is never enqueued — 90 in-game days in the crash log, zero conversion lines.

26 settlements retagged (`castle_K4` is `clan_khuzait_1`'s and stays Easterling). That woke the
Variag culture's troop bindings, which were still vanilla precisely because nothing had ever
carried the culture — left alone it would have garrisoned Khand with Calradian
`battanian_militia_*`, ids TAOM redefines nowhere. They now point at the Rhun roster, which is what
those settlements produced before the retag. `CultureMap["battania"]` was added for the same
reason: the volunteer cascade ends at the culture pool and the K-series has no per-settlement
pools, so the retag would otherwise have silently dropped Khand's volunteers. It also makes Variag
a valid conversion target for the first time — `HasCulturePool` gates that, and the test that
documented the gap has been retired.

Applies to EXISTING SAVES too, not just new campaigns. `Settlement.Culture` is a bare
`public CultureObject Culture;` (v1.4.7 `Settlement.cs:70`) with no `[SaveableField]`, has no entry
in `AutoGeneratedSaveManager`'s Settlement member set, and is re-read from XML on every load
(`Settlement.cs:961`) — which is precisely why TAOM's CultureConversion has to re-apply converted
cultures on load. Contrast `Hero.Culture`, also a bare public field, which DOES get an
auto-generated accessor and so does persist. Every player loading a save after this update gets
Variag Khand immediately.

### feat(validate): the culture check that would have caught it before it shipped

`LANDLESS_CULTURE` — every culture on a `Lord`-occupation character, a `<Faction>` or a `<Kingdom>`
must own at least one settlement, or be listed in `_LANDLESS_BY_DESIGN` with a reason. The
settlement registry honours an unconditional `<xsl:template match="Settlement"/>` strip, which is
the part that matters: counting the vanilla settlements TAOM_Map deletes would have reported every
culture as landed while the game crashed. 18 errors before the retag, PASS after.

## 2026-08-03

### docs: bring the knowledge base up to the multiplayer changeset

32 files. The load-bearing part is not the feature docs — it is that three of the changes taught
rules that generalize past the bug that produced them, and those went into the places a future
session actually reads.

`vanilla-data-comparison.md` gained two failure shapes its stale-name framing did not cover: data
that is structurally illegal in a way one engine build tolerates and another refuses to boot on, and
data that is well-formed yet unreachable relative to the rest of the set. It also records that
`generate_race_civilian_action_sets.py` is **not** the suspect for the orphaned elements — grepping
for a generator is the obvious first move and it accuses the wrong component, which is exactly the
mistake made while fixing this. `engine-bump` now documents `audit_action_set_parity.py` as two
independent gates, because a bump is precisely when a byte-identical file one build accepted gets
rejected by the next. `moduledata-validation.md` records that the armory `action_sets.xml` sits
outside both the validator and the commit hook.

`AGENTS.md` gained the two patterns a Codex review would otherwise flag as bugs: `PlayerPossession`
gating a hero mutation on co-op *presence* rather than authority (it is the heir-succession
discriminator, not a world-state decision), and `DedicatedServerProvider` reading its own binaries
folder rather than co-op role (a client-hosted host reports `IsServer` while being a real player).

Also corrected two things this changeset would otherwise have left wrong: the War of the Ring doc
now states that the field report's "only the authority's MainHero can satisfy it" premise was
already inaccurate — battles were satisfiable by any party in that hero's *kingdom* — and CLAUDE.md's
own documentation rule no longer tells new features to add a Key Paths row, a section that has held
no per-feature rows since the Tier-2 restructure. The 13 per-category lesson counts were re-derived
by counting headings rather than trusting the index; the true total is 302, not the 291 and 243 two
places claimed.

Linter: orphan feature docs 3 → 2 (`player-possession.md` is now linked), no new dead links,
CLAUDE.md still inside its eager-load budget at 44.0 KB.

### fix(armory): 168 stray `<action>` elements crashed every dedicated server

`LOTRLOME_Armory/ModuleData/action_sets.xml` carried 168 `<action>` elements parented by
`<action_sets>` instead of an `<action_set>`. Twelve `as_<race>_female_villager_in_aserai_tavern`
sets had been authored SELF-CLOSING, which orphaned the 14 female-conversation overrides that belong
inside each — vanilla's own `as_human_female_villager_in_aserai_tavern` nests exactly those 14, in
that order, and all twelve TAOM groups matched it byte for byte.

Build 1.4.7.117484 tolerates the malformed file, so nothing surfaced in play. Build 117131 — which
TaleWorlds' dedicated-server engine ships — throws `KeyNotFoundException` in
`MBObjectManager.MergeElements` at schema path `/action_sets/action` and dies on boot, which is why
every server operator had to run the single-player module order to get one started.

Fixed in the live file and the tracked snapshot together (`tools/oneoff/fix_orphaned_tavern_conversation_actions.py`);
all 34,247 action elements and 1,226 action_sets are preserved, only their parentage changed.
`tools/audit_action_set_parity.py` now fails on any root-level `<action>`, verified in both
directions against the pre-fix file.

### fix(herorace): a one-race host used to humanise every hero in the world

A co-op host running without TAOM's modules has one race in its FaceGen, so every hero there reads
back as 0. `CaptureHeroRaces` wrote that as `legend="human"` + `{all heroes: 0}`, the map rode the
host→client save transfer, and `RestoreHeroRaces` on a full 15-race client resolved "human" to a
perfectly valid id and force-set every hero in the world to it. No per-value validation could catch
it — each individual value was well-formed; the race COUNT was the only tell. Capture now refuses a
table below two races and keeps whatever map it already had.

Second half of the same failure: capture ran only on `OnBeforeSaveEvent`, which the host→client save
transfer never raises, so a joiner received a world with no race data at all. It now also runs at
session launch — after the restore, an ordering the tests pin, because capturing first would
snapshot every hero at their raw XML race and overwrite the map the restore is about to apply.

### feat(possession): re-apply what character creation granted to the hero you actually get

Every multiplayer base discards the character-creation hero at the join hand-off and substitutes a
host-authored one. TAOM's CC grants all ran against the discarded hero, so joiners arrived with the
wrong race and none of their culture's package — a Mirkwood player got the native 1000 gold instead
of 1000+4000, with the +4000 grant visible in the client log immediately before the hero it applied
to ceased to exist.

`Main/Features/PlayerPossession/` records the choices at CC-finalize, watches for
`Hero.MainHero` becoming a different hero, and re-invokes the existing grant paths — `SetHeroRace`,
`GrantPlayerStartupGold`, `OnCareerSelected`, `InitializeHero` — against the right one. No co-op
assembly is referenced: the detection is pure engine state, so it works the same under BannerlordCoop,
Bannerlord Together, or something that does not exist yet.

The guards matter more than the feature. `Hero.MainHero` ALSO changes in single-player on heir
succession, so a naive version would hand every heir a fresh starting package. It is gated on co-op
presence (solo never reaches it), consumed once, and marked per hero in `SyncData` so a reconnect
cannot re-grant. Each grant is independently guarded — a joiner losing their career because the gold
grant threw would be worse than the bug being fixed.

### fix(specres): you had to COMMAND the winning side to earn anything

The earn gate asked whether the player is the winning side's `LeaderParty.LeaderHero`. Join any
lord's army and you are not — so in ordinary single-player, every victory fought under someone
else's command paid zero. Multiplayer made it total rather than different: under a client/server
split no player leads the authoritative side either, which is how a session with 33 fought missions
produced one `MapEventEnded`, with `state=None`.

Now keyed on `MapEvent.PlayerSide == MapEvent.WinningSide` — participation, not command — extracted
into `SpecialResourceEarnPolicy` so the AI-led-army case is pinned by a test that fails on the old
logic.

Separately, a dedicated server no longer credits `Hero.MainHero`. That hero is the idle world-gen
character the campaign was created around, and the server was banking prisoner and raid income
against it while the remote players who fought those battles earned nothing. Detected from the
binaries folder this assembly loaded from, which is a fact about the process rather than a guess:
notably NOT from co-op role, because a client-hosted session's host also reports `IsServer` and is a
real player who must keep earning.

### fix(emissary): the shop no longer takes payment for troops that evaporate

On a non-authoritative peer the special-resource charge persisted while the purchased elite went
into a client-side roster the next resync overwrote — pay real, get phantom. The sale is now declined
before the charge, with a message saying why. Granting it properly needs a message TAOM cannot send
without a compile-time dependency on one specific co-op mod, so declining is the honest behaviour
rather than a placeholder.

### fix(momentum): capturing a fief inside an ally's army counted for nothing

`SiegeOutcomeSnapshot.PlayerInvolved` required the player's own party to BE the captor, while the
battle snapshot beside it already counted any party in the player's kingdom. So the normal way a
vassal takes a settlement — inside someone else's army — recorded no player event, and the War of
the Ring victory requirement quietly failed to advance. Sieges now use the same test as battles.

This is the single-player half. Crediting remote players in OTHER kingdoms still needs a co-op seam
TAOM does not have, and is not claimed as fixed.

### feat(build): dedicated servers get TAOM binaries without a hand-copy

Neither module shipped a `Win64_Shipping_Server` folder, so a dedicated server logged
`Cannot find: ...\TAOM\bin\Win64_Shipping_Server\TAOM.dll` and then ran a vanilla simulation over
TAOM's map — no race capture, no War of the Ring, no campaign systems at all. Both csprojs now mirror
the assembled client folder to it, the same way `Main/TAOM.csproj` already mirrors to
`Win64_Shipping_wEditor`. Verified: 10 files for TAOM, 42 for TAOM.Dependencies, covering every DLL
server operators were copying by hand.

The two opt-out flags that recipe also needed are already obsolete — PatchShield skips install
outright under co-op presence, and SaveShield rethrows save-load faults there instead of swallowing.

### feat(devconsole): `taom.audit_settlement_entrances` finds unreachable settlement gates

Three settlement destinations were reported as wedging AI parties: `town_MM2`'s gate,
`hideout_desert_7`, and `castle_village_MM1_2`. All three coordinates match the live map data
exactly, and none of the faces is off-mesh — `PathFaceRecord.IsValid()` is true for every one, which
is why nothing cheaper than an island comparison detects them. They sit on navmesh islands the rest
of the map cannot path to, so every AI tick targeting them fails its path query and the engine says
so only through a repeating assert.

The command walks every settlement's entrance, derives the main landmass from the island index most
settlements agree on, and reports each disagreement with a replacement coordinate from
`GetAccessiblePointNearPosition` — the engine's own navmesh answering, not a guess. Needs one
in-game run to produce the numbers; applying them is a separate step against the live
`TAOM_Map/ModuleData/settlements.xml`.

### fix(mainmenu): 4,803 log lines per headless boot

`CustomizeMenu` runs on every screen-root set and warned unconditionally when an option was missing.
A dedicated server sets that root thousands of times per boot with StoryMode and SandBox absent, so
both warnings fired every time. The warnings are now once per option and the applied-line once per
session; the customization itself still runs every call, because the engine can rebuild the
initial-state options between sets and skipping it would silently drop the rename on a real client.

### docs(armory): the shield grip split is fine, two "typos" in it are load-bearing

Audited all 226 shields in `LOTRAOM_shields.xml` against vanilla 1.4.7 after the `hand_shield` /
`shield` mix looked suspect. The mix is not the problem: the invariant that matters —
`hand_shield` ⇒ `ForceAttachOffHandPrimaryItemBone`, `shield` ⇒ `ForceAttachOffHandSecondaryItemBone`
— holds 226/226, matching vanilla's 135/135 across `shields.xml`, `tournament_weapons.xml` and
`mpitems.xml`. Every TAOM race animates both grips (`audit_action_set_parity.py`: 0 gaps over 1304
humanoid sets), and `weapon_class` is uninformative because vanilla ships no `SmallShield` either.

What does diverge is shape versus grip: 56 kite-holstered shields are held centre-grip where vanilla
does that for three. That costs block coverage — `hand_shield` caps the cross-body arc at ±0.55 on
foot and horseback, `shield` at ±0.8/±0.6. Which of the 56 are wrong is a visual call, so
[`docs/reference/armory-shield-audit.md`](docs/reference/armory-shield-audit.md) tables them by mesh
family and changes none.

Two entries in that file look like plain typos and must stay. `wm_isengard_shield_a04` references
`bo_capwm_isengard_shield_a02_clean` with the underscore missing — but the asset is packaged under
that exact misspelling and the corrected spelling exists in no `.tpac`, so correcting the XML would
manufacture the missing-collision-body hang from #352. `gond_shld4` uses its full body as its own
capsule because no `bo_cap_wm_gondor_shield_a` was ever built. Both are now recorded as
deliberate-looking-wrong, with the TOC evidence.

One real fix: `wm_boromir_shield` was borrowing a round Rohirrim shield's capsule while its own
`bo_cap_wm_boromir_shield` sat packaged and unwired, so its block geometry did not track its mesh.
That file is outside any git repo — the audit doc is the only record if a dependency refresh reverts it.

The generalizable half is now a lesson in
[`lessons/data-content-cultures.md`](docs/reviews/lessons/data-content-cultures.md) and a second
warning box in [`mesh-ref-validation.md`](docs/features/mesh-ref-validation.md). #352 was a good
ref/asset pair broken on the *ref* side, and stating it that way trains "malformed name ⇒ fix the
name." It runs both directions, so a `validate_mesh_refs.py` PASS on a name that looks wrong is
positive evidence the name is right — only `MISSING_BODY` names are safe to rewrite. The
usage↔offhand-bone invariant is now stated in CLAUDE.md and `armory-guide.md` where authoring
actually happens.

### perf(diagnostics): 93% of the log was two blocks saying the same thing

The clean tournament repro (`taom_debug_2026-08-03_12-08-46.log`) came to 644 KB / 4,536 lines for
37 minutes of play. Reading it by tag: the `[BattleLoad]` equipment dump was 1,146 `slot=` lines
describing **18 distinct loadouts**, and `[CultureMarketplace]` was 1,687 lines running at a
sustained ~30/min with no ceiling. That matters because this file's job right now is crash forensics
from user machines, and an unbounded steady-state stream buries the evidence a reporter uploads.

The dump was per-agent but its content is per-loadout — 429 arena spectators drawn from 9 character
kits. Each distinct loadout is now dumped once and later agents carry a `loadout=#N` token. The key
is the rendered rows plus `race`/`monster`/`actionSet`, deliberately **not** the character id: it
has to mean "what the engine is about to assemble", so a mid-load `MatchEquipment` rewrite surfaces
as a new id with a fresh dump instead of being swallowed by an earlier twin. Crash durability
improves rather than degrades — a deduped agent's block was flushed far earlier in the load, behind
hundreds of subsequent synchronous INFO writes.

`[CultureMarketplace]` now rolls a whole in-game day into one line. The 2026-07-27 gate was correct
and still not enough. Two things survive the roll-up that the per-town line could not give you: the
foreign-strip total across every town, and a picks-vs-injections divergence that previously meant
diffing two tokens by eye across the whole session.

**All three per-agent phase stamps are unchanged.** `AgentEquipOk` without `AgentBuildDone` is what
separates an equip-phase death from a `Mission.BuildAgent` native-tail death — the distinction both
open tournament CTDs turn on — and the measured cost is 145 ms for 429 agents.

Constraint: `triage_battle_load.py` attaches slot dumps to the immediately-preceding
`AgentEquipBegin`, so the dedupe would have left its `EQUIPMENT` verdict with no suspects at all. It
now resolves `loadout=#N` back to the block that carries it, clearing the map at `MissionInitialize`
because ids restart per load. Fixing that surfaced a live bug alongside it: a lazy
`culture='(.*?)'` had been swallowing the `race`/`monster`/`actionSet` tokens added the day before,
reporting the whole run as the town's culture.

Measured by replaying the real log through the shipped key rather than estimated from it: the slot
block goes 186,345 B → 8,432 B against 5,148 B of added tokens, and the projected file is
644,215 B → 240,832 B (63 %). The first pass wrote "11 lines" here, from the count of distinct
*rows*; a loadout is a *set* of rows, and the real figure is 18 loadouts / 52 lines.

Not-tested: the in-game numbers (the above is a replay of an old-format log, not a fresh run).

### feat(validate): the two ref classes nothing was checking

The 2026-08-02 crash log carried three `Null object reference found with ID` lines that
`validate_moduledata.py` had no chance of catching. Two independent gaps.

`BodyProperty.*` was cross-checked by nothing — the feature doc listed it under "out of scope for
v1". It is now a `BROKEN_BODY_PROPERTY_REF` ref kind, resolved against a registry built from the
four authoritative `*_bodyproperties.xml` files (121 ids: 30 TAOM, 91 vanilla). An explicit file
list, not a directory walk, for the same reason cultures use one — a walk sweeps config files that
reuse the element shape.

The sweep also only ever walked TAOM's own ModuleData, while 28 of the 33 bad refs sat on
`LOTRLOME_Armory` item files. TAOM authors into that module directly, so it is TAOM's to keep
correct even though it lives outside this repo and outside git. `Validator` now takes
`extra_ref_roots` and sweeps it for **cross-references only** — the schema contracts (duplicate ids,
civilian `equipmentType`, enums) describe TAOM's files and are not applied to a foreign module.

Proven against the real registries rather than fixtures: reintroducing both original defects in a
temp tree yields `UNKNOWN_CULTURE` at `LOTRLOME_Armory/LOTRLOME_items/rhun/head_armors.xml:2` and
`BROKEN_BODY_PROPERTY_REF` at `characters/lords.xml:3`.

### fix(validate): a missing Armory folder used to print PASS

Found by the deep review, in the change directly above. The new sweep filtered its roots with
`if Path(r).exists()` and reported only the ones it found. A renamed or missing `LOTRLOME_Armory`
therefore dropped the entire Armory sweep silently and the run still printed `PASS` — output
identical to a genuinely clean run, and a silent revert to the exact under-coverage state the sweep
was built to end. `Validator` now records `missing_ref_roots` and the CLI says so loudly.

Same class one layer up: no registry had a size floor, so a renamed vanilla `*_bodyproperties.xml`
would shrink the set with no tell — to zero it trips the empty-registry guard and skips the check
entirely (quiet), partway it floods false positives (loud). Both now warn.

`tools/audit_mount_parity.py` had the same shape. If the chariot clip path resolved to nothing, the
`quad_movement` probe tested `bound[r] in inv` first and so reported nothing regardless of ground
truth — a false clean on the one check guarding the shipped gait-clip AV. Section F now refuses to
report target-resolution or tagging results at all when the inventory is empty.

Also from the review: `_read`/`_read_stripped` moved to `utf-8-sig` per the repo's own XML I/O
convention, since the Armory sweep newly routes 382 files through them and 2 carry a BOM.

### fix(missiondiag): the divide-by-zero was attributed to the wrong getter

The API-compatibility pass decompiled installed v1.4.7 and caught that the code comments, the
investigation doc and yesterday's CHANGELOG entry all named `GetDayOfSeason` / `TimeTicksPerDay` as
the throw site. `CampaignTime.ToString()` evaluates `GetYear` first, so `TimeTicksPerYear` is what
actually divides by zero. That was inferred from grepping for a division instead of reading the
method — the conclusion held, the cited mechanism did not.

The same pass established two things worth more than the correction. The window is structural, not a
race: `Campaign.OnInitialize` invokes `GameManager.OnGameStart` — which calls TAOM's
`SubModule.OnGameStart` — three lines before `CampaignTime.Initialize()`. And `Campaign.GameStarted`
is already `true` there on a save-load but `false` on a new campaign, which is why the existing guard
never closed the window and why this only ever surfaced on save-load.

Known limitation: `MissionDiagnosticService.LogCampaignContext` has no unit test. It reads
`Campaign.Current` statics that cannot be substituted, and the service has never had a test file.
The pure formatter it delegates to has 6.

RCA: `docs/reviews/rca-validator-silent-scope-2026-08-03.md`.

## 2026-08-03

### diag(siege): make an unusable rock pile say why

The reporter later clarified the failure happened on a **vanilla** siege map while defending, on
foot. That rules out the scene-data gap found yesterday — vanilla town and castle scenes carry 3–21
working piles, `boulder` resolves, and the audit finds no dead item id in any of them.

Three more candidates went the same way, each on evidence rather than reasoning. TAOM's
`BehaviorTreeMissionLogic.OnObjectUsed` is a pure notifier and blocks nothing. The boulder pickup
animations exist for both humanoid roots that matter: `act_pickup_boulder_begin`/`_end` are declared
in Native's `as_human_warrior` and LOTRLOME's `as_dwarf_warrior`, and every LOTR race derives from
one of them. LOTRLOME's duplicate `as_human_warrior` looked alarming until its contents turned out
to be 47 spider-rider actions — an additive fragment relying on merge-by-union, not a replacement.

What remains needs runtime state, so `SiegePropDiagnostics` reads it. Every candidate cause in the
differential fails silently — the engine writes nothing when a prop is unusable, and the interaction
system quietly keeps focus on the machine root, where `StonePile.GetDescriptionText` returns null for
anything not tagged `ammopickup`. Blank prompt, dead key, empty log.

The behavior sweeps `Mission.MissionObjects` — not `GetActiveEntitiesWithScriptComponentOfType`,
which omits `SetDisabled` objects and would hide one of the causes being tested for — and records
per prop: whether `GivenItemID` resolves, ammo remaining, machine disabled/deactivated state,
standing-point and `ammopickup`-point counts, how many points are deactivated, occupied, or disabled
for the player, and the decisive `GetValidVacantReachableStandingPointForAgent(Agent.Main).IsValid`
verdict, plus the nearest point's distance and ground-height delta. Classification into one of
thirteen named causes is pure logic over primitive snapshots, so it is unit-tested without a Mission.

Two false positives are designed out, both from the barrel/pile asymmetry: only `StonePile` requires
`ammopickup`-tagged points and a resolvable item, because `AmmoBarrelBase` iterates every standing
point and hands out no item at all — vanilla's `arrow_barrel` prefab tags nothing. Flagging either on
a barrel would have buried the real fault. The geometry gates are written as positive requirements so
a NaN from the engine fails them instead of passing.

Off by default under `Battle Tactics/Siege Prop Diagnostics`. Diagnostic only — it changes no
gameplay.

## 2026-08-02

### tools(siege): find the sieges where the rock piles are scenery

A player reported not being able to interact with throwable rock piles or arrow baskets in sieges,
and that the AI never threw rocks either. `tools/audit_siege_props.py` answers that statically, for
every town and castle at once, without launching the game.

A prop only works if it carries the engine script — `StonePile`, `ArrowBarrel`, `JavelinBarrel`. A
scene can hold dozens of pile-shaped meshes that render identically and do nothing, and the engine
says nothing about it: `MissionMainAgentInteractionComponent.FocusTick` finds no usable standing
point, focus falls back to the machine root, and `StonePile.GetDescriptionText` returns null for any
entity not tagged `ammopickup`. Blank prompt, dead interact key, no log line.

The count has to be per entity, not per string match. A scene entity can reference the prefab *and*
re-declare the script inline to override a variable, so counting the two forms separately
double-counts — that mistake produced a wrong pile count during the investigation before the tool
existed. The "looks like a usable pile" mesh set is derived from the prefabs that actually carry
`StonePile` rather than hand-written, which is what keeps scenery rubble (`stone_pile_desert_*`,
`stone_pile_wall_*`) and siege-engine debris out of the count.

Result across the 221 towns and castles in the live `TAOM_Map` settlements file: 206 have usable rock
piles, **15 have none**, and all 15 are on TAOM-authored scenes. Ten are Gondor castles sharing
`taom_gondor_castle_002`/`_003`, which have no rock piles and no barrels at all. Helm's Deep is the
one that reproduces the report exactly — 8 `stone_pile_a` meshes that look usable, zero that are.

The tool also checks every `GivenItemID` against the item registry, because an id that does not
resolve leaves `_givenItem` null and `StandingPointWithWeaponRequirement.IsDisabledForAgent` then
falls through to `return true`, silently disabling the pile for player and AI alike. No audited
settlement hits that. Vanilla's `stone_pile_l_usable` prefab does — it asks for `boulder_carry`,
which is defined nowhere in the install — but no town or castle scene references that prefab.

### perf(battle-load): a race lookup that cloned an array per agent (#372)

Deep review of the agent-build instrumentation, six agents. Two findings, both mine.

`FaceGen.GetRaceNames()` returns `(string[])_raceNamesArray.Clone()` — a fresh fifteen-element array
on every call. The adapter called it once per agent to read a single element, so an arena load with
648 agents allocated 648 arrays and discarded 648. `GetBaseMonsterNameFromRace(int)` sits next to it
in the same class, indexes the same array, and allocates nothing. Neither name says which is which;
only the bodies do, and I used the one whose name matched what I wanted.

The second was a comment: I labelled `Mission.cs:4041` "action-set resolution", but
`SetActionChannel(0, GetCurrentAction(0))` plays an action on channel 0 and resolves no action set.
The wrong label had already been copied into the feature doc.

A third finding was raised as HIGH and is wrong. The claim was 1944 synchronous disk flushes per
load, "2-20+ seconds", with a recommendation to move `AgentBuildDone` to DEBUG. `_logFile` is a
`StreamWriter`; its `Flush()` reaches the OS file cache and never calls `FlushFileBuffers`. Measured
on the live log, 1287 durable stamps took 145 ms — about half a percent of a 9.3 second load. Taking
the advice would have moved a crash-localisation stamp onto the async queue that a native crash
drops, which is the one thing it must never be.

Worth keeping: the agent that decompiled found a real defect, and the agent that reasoned from
plausibility deferred the real one and invented the other. `/deep-review`'s efficiency agent now
carries the instruction to decompile an engine method before costing it, and to report an unverified
cost as UNVERIFIED rather than HIGH.

RCA: `docs/reviews/rca-battleload-agentbuild-2026-08-03.md`

### diag(battle-load): the agent-build tail was never stamped (#372)

A player crashed to desktop entering a tournament in the Dunland town of Carreglyn. The log ends on
`AgentEquipOk agent#0 'Musician'` and nothing follows — no watchdog line either, so the process died
outright rather than hanging.

That tail was as far as the log could go, and the reason is a gap in our own instrumentation.
`AgentEquipOk` brackets one call, `Agent.EquipItemsFromSpawnEquipment`. `Mission.BuildAgent` then does
six more things to the same agent — `InitializeAgentRecord`, `BatchLastLodMeshes`,
`PreloadForRendering`, `SetActionChannel`, `InitializeComponents`, `_activeAgents.Add` — all native,
none stamped. A death in there and a death between two agents produce byte-identical logs.
`AgentBuildDone` is a postfix on `BuildAgent`, so they no longer do.

The `AgentEquipBegin` line now also carries `race=`, `monster=` and `actionSet=`. Those have to ride
the line written *before* the engine touches the agent, because a mismatch between them is the shape
that access-violates in native mesh assembly with nothing logged, and a stamp that fires afterwards
is worthless for a crash.

`from=` is the part that came directly from this report. The agent was `char='musician_dunland'`, and
no known code path puts a musician in a `TournamentFight`: the mission's 13 behaviors carry no
`MissionAgentHandler`, and `FightTournamentGame.GetParticipantCharacters` picks only heroes, tier 3–5
garrison troops, and `BasicTroop` upgrade targets. There was exactly one mission in the whole session,
so it is not a leftover from a town scene. The loadout dump could not name what built it; a bounded
managed stack can. Bounded to `Agent.Index <= 2` — the capture is not free and the answer is only
interesting at the head of the spawn sequence.

Root cause is still open. This is the instrumentation that will let the next log settle it.

Research: Mission.BuildAgent, FightTournamentGame.GetParticipantCharacters, TournamentMissionStarter
Not-tested: Harmony patch invocation and the live stack capture (require a running game)

### fix(battle-load): from= named our own prefix and nothing else (#372)

The first run proved the stamps and broke the token. 648 agents, every `AgentEquipBegin` matched by
an `AgentEquipOk` and an `AgentBuildDone` — and three `from=` lines that said only this:

```
from=Agent_..._Patch.CaptureSpawnOrigin <- Agent_..._Patch.Prefix
     <- .TaleWorlds.MountAndBlade.Agent.EquipItemsFromSpawnEquipment_Patch2
     <- .TaleWorlds.MountAndBlade.Mission.BuildAgent_Patch1
```

Four frames, four noise. The patch built each frame from `DeclaringType.Name` while the formatter
filtered on namespaces, so every filter was dead on arrival; our own prefix and Harmony's generated
wrappers then ate the whole budget and pushed the real caller off the end.

Full names now, and the `_PatchN` wrappers are normalised rather than skipped — a wrapper *replaces*
the frame it stands for on the stack, so dropping it would lose the method the token exists to name.
Consecutive duplicates collapse, since the wrapper and the original both appear. Budget raised to
six frames because Harmony adds one per patched method in the chain.

Worth stating plainly: had this worked on the first run it would have printed
`MissionAudienceHandler.SpawnAudienceAgents` and ended a day of speculation about how a
`musician_dunland` agent reached a tournament. It is an arena spectator — the audience is a weighted
draw over the settlement culture's location characters, and `Culture.Musician` sits in it at 0.1.
The agent was never strange; three analyses called it impossible because all three read the
13-behavior `InitializeMissionBehaviorsDelegate` instead of the 65-behavior list the mission actually
runs, which `MissionDiagnosticBehavior` had already dumped into the same log.

Research: MissionAudienceHandler.SpawnAudienceAgents (SandBox.View), Harmony wrapper frame naming

### perf(coopinterop): stop shielding a co-op mod's patch surface

Co-op works. A player got TAOM and BannerlordCoop running together, hit a frame-rate collapse,
profiled it, and traced it to TAOM's own PatchShield. They were right, and the fix is the one they
suggested: skip `PatchShield.Install()` when a co-op module is active.

The mechanism is #331 again. A shield finalizer binds `__originalMethod`, so Harmony's generated
wrapper pays a `GetMethodFromHandle` plus a try/catch on every call. In #331 that turned a
millisecond tournament teardown into a two-minute freeze. Here it lands somewhere worse: Coop's
AutoSync transpiles every declared method and constructor of 43 campaign types, and its `PatchAll`
runs on connect — before TAOM's late pass — so PatchShield wrapped that entire surface. Those methods
are the campaign hot path, which is why the symptom was frame rate rather than a single stall.

Widening the existing namespace denylist would have been the wrong lever, because it is not
co-op-scoped: excluding `TaleWorlds.CampaignSystem` would stop shielding nearly everything in solo
play too.

This gives up the swallow half — surviving a missing method or field after an engine bump. Under
co-op that is the right trade, and the same one SaveShield already makes when it rethrows save
faults: a visible crash beats two campaigns drifting apart in silence. The unpatch half was already
withheld. A player who has the co-op module enabled but plays solo also loses the shield for no
benefit; that is unavoidable, since install runs before any session exists.

Worth recording that this is the second time this cost has bitten, in a form the first fix's wording
did not cover. #331 treated it as a property of specific hot namespaces and answered with a curated
denylist. It is really a property of how many methods *anyone* has patched — a mod that transpiles
whole types multiplies the tax without TAOM changing a line. The lesson is generalised rather than
the list extended.

Also retires the "end-to-end co-op UNVERIFIED, do not tell players it works" caveat the docs have
carried since yesterday. Stated precisely: the pair runs and is playable. Nobody has audited object
sets between peers, and the client-side culture-conversion crash chain and career-quest creation path
are still unproven.



### diag(chariot): the chariot got the mount audit it was never in

A player reported constant crashes fighting Rhûn as dwarves and sent a debug log plus an rgl log.
Neither contains a root cause: the rgl log stops mid-line and the process is gone. That is a native
fault, and the reading is not a guess — `FileLogger` drains INFO/WARNING/ERROR synchronously with a
flush precisely so the tail survives an AV, so the 42 seconds of TAOM silence before the process
died is evidence rather than a lost buffer. No managed exception, no crash bundle, and the `Patch63`
banner-bearer guard never fired. Timeline puts the fault ~41s after the AI battle plan, at first
melee contact, not at spawn.

Rhûn's one non-vanilla field unit is the war chariot, so it drew the attention — and
`tools/audit_mount_parity.py` turned out to exclude it (`MOUNTS = ["spider", "warg", "elephant",
"mumakil"]`), which is the follow-up `chariot.md` has listed as pending since June. Section F now
audits it against the vanilla horse, re-derived from the deployed artifacts rather than from the
doc's claims about them. It comes back clean: all 24 `animation=` targets resolve, and all 10
`monster_usage_movements` clips carry `quad_movement`, including the two gait clips whose missing
tag was the latent AV that doc flagged as never in-game verified.

So the chariot is not exonerated, but it is no longer the obvious suspect, and two theories died on
controls rather than on argument. A first draft of the tag check also swept the upper-body table and
flagged seven clips — every one a `*_head` look overlay, which must NOT carry the tag. And the
chariot Monster ships no `fall_blow_damage_bone` and no ragdoll corpse bones, which reads as damning
until you notice the shipped, battle-proven warg lacks exactly the same ones. Recorded as
deliberately-not-fixed: changing it now would be a blind retry.

Root cause is still open, pending artifacts only the reporter has — a crash bundle, and the Windows
fault offset across two or three crashes, which distinguishes one crash site from several. The
request and a chariot-in/chariot-out custom-battle repro protocol are written up in
`docs/reviews/investigation-rhun-dwarf-ctd-2026-08-02.md`.

### fix(moduledata): three IDs the engine was resolving to null

The same rgl log carried `Null object reference found with ID: fighter_umbar / rhun / rohan` —
`MBObjectManager.UnregisterNonReadyObjects` reporting references that were never defined. All three
were typos against an overwhelming local majority: `BodyProperty.fighter_umbar` on 5 Umbar lords
(defined nowhere; the other 102 lords in the file use `fighter_haradrim`), `Culture.rhun` on 22
Armory head armors (609 items in the same folder already say `khuzait`), and `Culture.rohan` on 6
horse armors. `.claude/rules/xml-data.md` names those last two as the canonical culture mistake.

Cosmetic in effect — null face templates and null item cultures, not the crash. Two of the files
live in `LOTRLOME_Armory`, which is not a git repo, so the originals are saved beside them as
`*.bak-dangling-culture-20260802`.

Not fixed here, and worth knowing: `validate_moduledata.py` has an `UNKNOWN_CULTURE` check, but its
registry scope is TAOM's own ModuleData and it never sweeps `culture=` on Armory item files — where
28 of the 33 bad refs lived. `BodyProperty.*` is not cross-checked by anything.

### fix(missiondiag): every crash report was missing the in-game day

`Campaign: <time read failed: DivideByZeroException>`. The session snapshot runs before
`Campaign.Models` is built, so `CampaignTime.ToString()` hits `GetYear`, which integer-divides by a
`TimeTicksPerYear` that is still zero. The guard worked, so this never broke anything — it just
quietly cost us the campaign day on every crash report, which is what you correlate a save against.

`MapTimeTracker` and `NumTicks` are both `internal`, so there is no earlier readable source and no
fallback to write. The fix is a second emission instead: `LogMissionStartSnapshot` now logs the
campaign context as well, where models are always up, so any crash inside a mission carries the
date. The guard logic moved into a pure `CampaignContextFormatter` with 6 tests pinning the part
that actually matters — the time and hero halves are guarded independently, so one failing never
blanks the other.

Suite 4773 green.

## 2026-08-01

### fix(coopinterop): TAOM was telling players to disable TaleWorlds.Core

`SaveDefinerCollisionGuard` opened every collected user log with a fatal-sounding save-id collision
between `SaveableCoreTypeDefiner` and `SaveableObjectSystemTypeDefiner`, and the advice *"Disable one
of them."* Both are vanilla engine types, in a game that starts fine.

The rule the check asserts is false. It grouped `SaveableTypeDefiner` subclasses by base id and
treated a shared base as proof of collision, but the engine registers on `_saveBaseId + saveId`
(`AddClassDefinition`, verified on installed v1.4.7) — so a shared base is legal whenever the
per-type offsets differ. Enumerating every definer in the v1.4.7 dump gives 67 distinct base ids and
exactly one duplicated pair: the two above, which sit in different assemblies and therefore took the
cross-assembly branch, the one carrying the strongest wording and an instruction to disable
something.

Groups made up entirely of game-shipped assemblies are now dropped — there is nothing a player can do
about them. What survives logs a WARNING that the shared range *may* collide and names the first two
things to try disabling, rather than an ERROR asserting the game *will* fail. Four tests added, two
of which fail against the old code, built from the real vanilla pair rather than synthetic records —
the previous seven all passed because every fixture obeyed the same assumption the production code
made, so they confirmed the code matched the theory while the theory was wrong.

Reading the true ids would mean invoking each definer's `Define*` virtuals against a synthetic
`DefinitionContext`: running arbitrary third-party code speculatively at startup, bound to engine
internals that drift per version, for a diagnostic that can never beat the engine's own throw moments
later. Deliberately not done. If the heuristic misfires again it should be deleted, not deepened.

Why it survived two reports is worth recording: it was correctly identified as not caused by whoever
noticed it, and nothing routes an unowned, non-crashing, cosmetic-looking log line to anybody. The
cost stayed invisible because it was cosmetic — no crash, no failing test — while it sat at the top
of every support log discrediting every other line the same guard prints, including the real
collision it exists to catch.

RCA: `docs/reviews/rca-savedefiner-false-positive-2026-08-01.md`.

Not-tested: the guard's live output on an install that has a genuine cross-mod collision.
Research: `SaveableTypeDefiner.AddClassDefinition` (v1.4.7); all 67 vanilla definer base ids.

### fix(coopinterop): presence is not authority

A Codex adversarial pass over the interop layer below returned four HIGH findings. It disputed all
four suspects aimed at the layer itself — the fail-open direction, the reflection binder's locking,
the assembly-redirect deletion and the detection-coupling tests all held under attack. The problems
were around it.

The widest one: eight diplomacy and time-acceleration sites gated on module **presence**. That is
process-constant — true whenever the Coop module is merely enabled — so TAOM's War of the Ring rules
quietly switched off for a solo player who happened to have it installed, and for the co-op host,
which is exactly the peer that should be enforcing them.

The obvious fix is wrong too, and the code already said so in a comment: swapping to `IsAuthority`
breaks BannerlordTogether, because that predicate fails open and reports every BT peer authoritative,
so nothing gates at all. Presence gets the solo and host cases wrong; authority gets the
BannerlordTogether case wrong. The predicate that works keys on whether the host/client probe
actually **resolved**, which answers all five cases, and each is now a test.

Siege rewards leaked the other way. The shared/local split landed earlier so a client could still be
rewarded for a siege it defended, but the grant set a flag the save serializes — so a client claiming
its own reward wrote per-peer state into the host's save record. Clients now record the claim in
process-local state, behind a named predicate rather than an inline negation, because the inline form
is what shipped wrong.

Two smaller ones. A client could send a messenger, pay for it, and never see it arrive, since
delivery is host-side. And culture conversion's owner-change handler stayed ungated on the reasoning
that queuing a pending timer is harmless — it is not: the store is save-backed and the daily
processor that would mature those records is itself gated, so a client accumulates conversions
nothing ever services. Writing that justification into the docs had made it look considered.

Three of the eight findings needed correction before use: one quoted code that does not exist, one
was already fixed, and one proposed a fix that would have caused a different regression. Verifying
each against source before implementing is what caught them.

Career-quest creation on a client stays open. `QuestBase` sets its id in the constructor, which Coop
suppresses, but gating it removes career quests from clients entirely — that is a design decision,
not a mechanical fix. Recorded in the RCA rather than dropped.

### feat(coopinterop): TAOM can see BannerlordCoop at all, and stands down on a client

BannerlordTogether and BannerlordCoop are different projects. The interop layer shipped yesterday was
built for the first; the mod players are installing is the second, whose launcher id is the bare
string `Coop`. `CoopPresence` matches ids by exact equality, so nothing about `BannerlordTogether`
matched it — meaning every shield TAOM built was inert against the co-op mod actually in use.
PatchShield went on stripping foreign Harmony patches, SaveShield went on swallowing save faults, and
the census never wrote a line.

Adding one id is most of the fix, but it exposed two things the id alone does not solve.

The `AssemblyResolve` redirect matches on simple name and throws the requested version away, which is
safe only while our copy is the newest in the process. Coop ships five of those names higher —
`Serilog` 4.2 against our 2.0, `System.Runtime.CompilerServices.Unsafe` 6.0 against our 4.0, and
three more measured directly rather than assumed. Keeping them meant handing Coop's callers an
assembly a decade older than the one they compiled against. All five are gone; the BUTR stack that
motivated the shim stays. Neither a version comparison nor a co-op gate would work here: Coop loads
one of them by bare partial name, and the handler installs from a static constructor long before any
module probe can run.

The second is that Coop does not stop a client ticking the campaign. Its patches block the per-entity
tickers, so `DailyTickSettlementEvent` never reaches a client — but the global `DailyTickEvent` and
`HourlyTickEvent` fire on both peers, and Coop's own defence is a hand-written allowlist over named
vanilla types with no hook a third-party mod can register into. Seven TAOM behaviours now consult a
new `ICoopSessionProvider` and defer to the host: culture conversion, race aging, both War of the Ring
behaviours, messengers, siege defence, and castle recruitment's load-time notable top-up. The
authority decision is a two-line policy split out from its reflection binder so it can be tested
without a running game, and it fails open to singleplayer — a false negative there would quietly
disable TAOM for solo players, which is worse than the divergence it guards against.

Career quests were audited and deliberately left running on both peers: they key off `Hero.MainHero`,
which is a legitimately different hero for each player, and gating them would delete the feature for
clients.

Culture conversion is the one worth calling out. On a client its store holds the same pending records
as the host, because the client loaded the host's save — so it matures the same conversions locally
and replaces notables through `HeroCreator.CreateNotable`. Coop suppresses the `MBObjectBase.StringId`
setter on a client, leaving the id null for a `Dictionary` lookup that does not guard against one.
Every link in that chain was read in source; none of it has been reproduced in-game, and the code
comments say so. It is gated regardless, because the alternative outcome is a notable the host never
created.

Also settled, and worth not re-deriving: there is no save-definer collision (Coop sits ~682 million
ids away), Coop registers no GameModels so TAOM's ~30 overrides survive untouched, and network
identity is keyed on `StringId` built per-peer — so TAOM's XML content resolves on both sides with no
work at all. The analysis behind all of it, including the integration surface for syncing TAOM's own
state later, is written up rather than left in a transcript.

Not done, and listed in the feature doc rather than implied: no MCM settings parity across peers, no
ModuleData content hash, no replication of TAOM's own campaign state after the join baseline, and no
dedicated-server support. End-to-end co-op remains unverified.

### fix(build): make a mismatched TAOM/Dependencies pair impossible to ship silently

The version mismatch behind the first half of #371 could not be detected by anyone. Both assemblies
carried frozen versions on every build ever produced - TAOM 2.0.0.0, TAOM.Dependencies 0.1.0.0 - and
the module `<Version>` in `SubModule.xml` was equally static, so .NET bound any pair without
complaint and failed later at the member level. `Main/_Module/SubModule.xml` did not declare
TAOM.Dependencies as a dependency at all, so the launcher had nothing to check either. The only thing
distinguishing a current DLL from a two-week-old one was its file timestamp, which does not survive a
zip and a download.

Three changes close that. Each assembly now carries a per-build `InformationalVersion` stamp, and
both modules log theirs at startup with a verdict - a pairing more than an hour apart is reported as
a mismatch naming #371, which is what the shipped 07-31/07-17 pair would have produced. TAOM now
declares `<DependedModule Id="TAOM.Dependencies" />`, the element the vanilla launcher actually
parses, so a missing or wrong-ordered Dependencies is blocked at the launcher instead of surfacing as
bind-posed characters. And the Dependencies module version moved to v2.0.6 with a matching pin in
TAOM's metadata block, so BUTR/BLSE launchers can enforce it too.

`AssemblyVersion` is deliberately left fixed - changing it alters binding identity for no benefit
here, while `InformationalVersion` is free-form and costs nothing.

One thing worth recording, because the tests did not catch it: the stamp parser was written against
the format the code assumed and passed its unit tests, then failed against every real assembly -
`Bannerlord.BuildResources` appends its own commit-SHA suffix, so the timestamp is not at the end of
the string. Caught by running the parser against the actual built DLLs rather than a literal. There
is now a test using the real emitted string verbatim.

### chore(herorace): retire the tableau investigation scaffolding

The per-race action-set probe, the environment dump and the action-index health probe did their job -
they identified the `ActionIndexCache` static-initialiser race - and are removed (293 lines). What
stays is what the repair and the tableau patches need to state in one line whether a session hit the
fault: the repair's own verdict, and the error paths that fire only when a preview actually resolves
badly. The rest comes out once #371 is confirmed closed in the wild.

### fix(herorace): characters no longer render lying flat in every UI tableau

The real cause of the "bendy man" reports, and it was not the DLL version mismatch closed in #371 —
that finding was correct but never explained why the fault came and went between launches.

`ActionIndexCache` holds 215 static action indices filled by an **explicit** static constructor. That
detail is the whole bug: an explicit cctor means the type is not `beforefieldinit`, so the first touch
of *any* static member — a field read or the `Create` method — bakes the entire table at that instant.
Touch it before the engine has loaded action types and all 215 indices become `-1` for the rest of the
process, permanently, because the fields are readonly and the constructor never runs twice. Vanilla
`CharacterTableau.GetIdleAction()` returns one of those statics, so `SetAction(-1)` does nothing and
the skeleton stays in its bind pose. That explains every part of the report the previous theory
couldn't: all races, because it is global; intermittent per launch, because it is a load-order race;
never on the dev machine, because the timing differs. A community member had independently worked
around it by overwriting the action with a live lookup, which only helps if the baked value is unusable.

`ActionIndexCacheRepair` re-resolves poisoned indices from live lookups and writes them back. It is
gated on `MBAnimation` — a different type, with no static constructor — so it cannot trigger the
initialisation it exists to detect, and it does nothing at all when the indices are healthy. It runs
first thing in the tableau refresh and first-time-init prefixes, so the same refresh consumes the
corrected value and an already-open inventory doll fixes itself.

Two engine facts made this delicate. Field names are not reliably the action names — v1.4.7 has one
divergence, `act_raid_jump = Create("act_raid_jump_1")`, found by diffing all 214 constructor call
sites against their fields — so every write is round-trip verified against the engine and a name that
cannot be proven is left at `-1` rather than guessed. And `MBGlobals.GetActionSet` throws on a miss
rather than returning an invalid set, which had made several "invalid set" branches unreachable and
fired an engine assert per miss during the startup probe; the diagnostics use the non-throwing
`MBActionSet.GetActionSet` instead.

The instrumentation from 2026-07-31 stays, now correctly polarised: it previously compared against one
of four failure markers, so the poisoned-index case logged as INFO — the exact state it was written to
catch. Deferred-path logging is deduplicated after a review caught the `ae2ed426` log-flood pattern
being reintroduced on a per-tableau path.

RCA: `docs/reviews/rca-prone-character-tableau-2026-07-31.md` (addendum).
Research: `ActionIndexCache` cctor, `MBAnimation.GetActionCodeWithName`, `CharacterTableau.GetIdleAction`
Not-tested: reflection writes to initonly statics and tableau rendering both need a live game

### fix(coopinterop): stop TAOM vetoing decisions the co-op host already made (#370)

A source review of BannerlordTogether a0.5.3.2 — run under permission from Hobohoppy, one of its
creators — found that TAOM's `Priority.High` diplomacy prefixes were half a fix.

The ordering does what the previous entry claimed in one direction: when the host originates a war,
TAOM evaluates before BT and blocks it, so BT never broadcasts and both peers agree. The other
direction was never considered. BT's client suppresses locally-originated wars and forwards intent to
the host; when the host's decision comes back, BT re-applies it behind an `IsApplyingSync` guard —
and TAOM's prefix, running *ahead* of BT's at priority 600 against 400, re-evaluated `IsWarAllowed`
locally and could return false. Host at war, client at peace. Not a crash: two campaigns that
disagree, and the disagreement is invisible until the saves are irreconcilable.

How much the verdicts can differ depends on which veto, and the three are not alike. The peace veto
reads `IsWarOfTheRingActive` → `WarOfTheRingService.CurrentPhase`, which is persisted as the
`WarOfTheRing_CurrentPhase` `SyncData` key — TAOM campaign-behavior state BT knows nothing about and
never replicates, so peers genuinely drift apart. The war and alliance-end vetoes read only static
shipped config (`_permanentRelationships`, the alignment table), so identical installs compute
identical answers and they diverge only when peers run mismatched TAOM versions or edited configs.
That is an ordinary co-op scenario rather than an exotic one, and a veto that applies on one machine
but not the other is the same failure whatever made the answers differ — so all three are gated.

So under co-op TAOM now defers: war declaration, peace, and alliance-end vetoes all return false
immediately when a co-op module is active. One peer's ruleset has to win and TAOM cannot know which
one the session agreed on, so it yields the whole decision rather than applying its rules to half of
it. Gated on TAOM's own `CoopPresence` through a new `ICoopPresenceProvider` seam, not on BT's
private `IsApplyingSync` field — reflecting into another mod's internals breaks on their next build.
`AddAllianceDecision` is deliberately left alone: it dedups against replicated vanilla state, so both
peers compute the same answer, and gating it would bring back the decision-queue saturation it
exists to prevent.

Also suppressed TAOM's time-acceleration UI under co-op. BT prefixes the `Campaign.TimeControlMode`
setter and overwrites the assigned value outright, which is correct — a host-authoritative mod should
own the clock — but TAOM kept advertising control it no longer had: the injected `MapBar`
fast-forward button still rendered and still took clicks while doing nothing. The five prefab
extensions and the `MapTimeControlVM` mixin now carry `[CoopSuppressedUi]`, filtered out at
registration. It has to happen at registration because that is a one-shot in `OnSubModuleLoad` — a
runtime check inside a mixin cannot un-inject a widget that is already built.

Three things were checked and found already safe, recorded so nobody re-litigates them: BT declares
no `SaveableTypeDefiner` at all, so TAOM's four base ids — including the 726900601 that deliberately
collides with an upstream mod — are unthreatened by it; every vanilla model method BT patches that
TAOM also overrides calls `base`, so BT's patches still fire through TAOM's subclass; and the two
weather-bounds guards are complementary in either patch order, costing at worst a cosmetic
difference on out-of-bounds positions.

**Solo players pay nothing for any of this, and that is structural rather than careful.**
`CoopPresence` fails closed — an unreadable module list means false, so unmodded behaviour is the
default rather than a branch someone has to get right. UI registration now takes the original
`extender.Register(assembly)` call verbatim when no co-op module is present, so our own type
collection can never affect a solo player's UI; the filtered path exists only for sessions that
actually have a co-op mod loaded. The diplomacy gate is one bool read on a cold path, deliberately
uncached because `Refresh()` re-probes during startup and a cached read would trade staleness risk
for nothing measurable.

A separate compat module was considered and rejected on evidence, not preference:
`UIExtender.Deregister()` guards on `Instances[_moduleName] == this`, so another module holds a
different instance and literally cannot touch TAOM's UI registration — and its `OnSubModuleLoad`
runs after TAOM's anyway, too late for a one-shot. It could only reach the diplomacy half via
`Harmony.Unpatch`, which goes stale silently when TAOM's patch layout moves.

Two additions keep this from rotting. `coop-force-active.flag` in the TAOM.Dependencies directory
forces co-op mode on when detection fails — a renamed BT build or an unknown fork — matching the
existing `patchshield-disabled.flag` idiom rather than an MCM setting, since MCM persists a saved
value over a changed compiled default. It only ever adds presence, never removes it. And
`CoopVetoClassificationTests` scans `Main/` for every class declaring a bool-returning prefix and
fails the build unless each carries a written disposition: 32 classified, three gated. That test
found four prefixes nobody knew were there — it caught its own first two implementations being
wrong, once by reflecting over the assembly (four engine-coupled classes came back null in the test
host) and once by keying on file rather than class (one file holds three patch classes).

The force-flag decision itself was then extracted into `CoopPresencePolicy` — pure, no I/O, 13
tests — because the first version shipped the branch inside `CoopPresence`, which is static and does
file plus reflection I/O and therefore has never had a single unit test. Adding an untested branch
to the one class already flagged for having no tests is how the next gap gets made. The policy pins
both invariants that matter: an empty module list means *unknown* and fails closed, and the flag
only ever adds presence — there is deliberately no way to force co-op **off**, since a stray file
that did so would resurrect the divergence this whole entry is about.

**A `/deep-review` then found that the above was itself half a fix**, three times over. The vetoes
were gated at the Harmony prefixes; the same three rules are also consumed by
`TaomKingdomDecisionPermissionModel` (three overrides the engine reaches from
`DeclareWarDecision.IsAllowed()` — a different call site entirely) and by
`TaomDiplomacyModel.IsAtConstantWar`. Both were left enforcing, including the `ShouldBlockPeace`
path this entry already identifies as the *confirmed* divergence. And suppressing the
time-acceleration button did not suppress the mechanic: E / Space / Ctrl+Space reach
`TimeAccelerationService` directly and write `Campaign.SpeedUpMultiplier`, a different property from
`TimeControlMode` and one nothing is known to intercept.

All four sites are now gated, and the UI-registration read — the only `CoopPresence` consumer that
decided from the earlier, explicitly-uncertain probe rather than a live read — re-probes first and
logs on both branches, so a co-op session whose detection ran late is distinguishable from a genuine
solo one.

The durable fix is not the four gates, it is the test: `CoopVetoClassificationTests` now scans the
**whole source tree** for consumers of the three divergence-prone rules and fails the build on any
that reads no co-op flag. The review agent that found the first missed the second, because
`TaomDiplomacyModel` was not in the changeset and the agent's scope was the diff — a review scoped
to a diff cannot find a bug whose evidence sits outside it. RCA:
`docs/reviews/rca-coop-veto-surface-2026-08-01.md`.

**Three Codex passes then found nine more (3 P1, 5 P2, 1 P3)**, and the pass aimed at the
BannerlordCoop authority layer — code no review had covered — produced the most. The fifth veto path
was real: `DiplomacyBehavior.OnSessionLaunched` calls `EnforcePermanentAlliances`, which reaches
`MakePeace`/`StartAlliance` from TAOM config on every peer, and the scan test written for exactly
this bug class could not see it because that path consults no predicate — it mutates directly. The
scan now covers direct mutators. `CareerQuestCampaignBehavior`'s dedup was scanning the global
`QuestManager`, so the host's active quest blocked a client from ever being offered one; the doc had
justified leaving that behaviour ungated as "keyed entirely on `Hero.MainHero`", which was false when
written and had been restated twice before anyone checked. The momentum reconcile mutated state above
its own authority gate. And the siege split from earlier in this entry was itself wrong — a client
would have claimed a reward it never earned, because `PlayerAccepted`/`RewardClaimed` are host-owned
at join — so it was reverted and rebuilt with per-peer claim state.

Two findings are recorded and NOT fixed, because both are feature changes rather than gate
placement: a co-op client can still pay for a messenger that never arrives (`PendingMessenger` has no
owner field, and the arrival path mutates shared state so it cannot simply be ungated), and
`new CareerQuest` on a client is `MBObjectBase` construction in a live campaign. Both are in
`docs/features/coop-interop.md`.

They share a shape worth naming, because it is the *inverse* of what this whole layer was built to
prevent: the gate stops a client corrupting shared state, but the entry point in front of it stays
client-reachable, so nobody desyncs — the client just starts something it can never finish, and in
the messenger case pays for it. Every audit so far asked "can a client corrupt shared state?"; none
asked "can a client begin a flow the gate later refuses to complete?"

One question got settled instead of hedged: two passes independently decompiled v1.4.7's
`Module.Initialize` and confirmed `ModuleHelper`'s module list is populated before `LoadSubModules`
invokes `OnSubModuleLoad`. The UI-registration read is reliable, the caution in the code was
superstition, and the redundant re-probe it motivated is gone.

The boot matrix has still never been run — BT is not installed. Everything above is reasoning from
source plus 4737 green tests, not from a session.

Not-tested: live co-op behaviour of any kind — the diplomacy deferral and the UI suppression both
need two peers to demonstrate.
Research: BT `SuppressClientDeclareWarPatch`, `TimeControlModePatch`, `ClientWeatherPositionCrashGuardPatch`.

### docs(harmony-registry): `Patch0_BattleScenes` was marked DISABLED for two months while running

The registry said "Battle scenes (DISABLED)"; `SubModule.cs` has applied it unconditionally since the
2026-06-01 re-enable. Also added the missing `Patch63_BlowDiagnostics` section and documented that
`Patch63` prefixes two distinct category strings — Harmony keys on the full string so both apply
correctly, but the number is not a unique key and a careless rename would break one.

### feat(devconsole): damage_agent and requeue_settlement, plus six review fixes (#369)

`taom.damage_agent <amount> [name]` applies raw HP damage, mirroring the engine's own
`KillAgentCheat` blow construction with a configurable magnitude. Its usage string and its output
both state what it CANNOT do: a synthetic blow goes straight to `HandleBlow`, downstream of where
`DecideAgentShrugOffBlow` runs, so shrug-off, unstoppable and knockdown models never fire. It is for
HP attrition and death thresholds; testing the damage models needs a real weapon hit.

`taom.requeue_settlement <settlement>` fires the owner-changed path twice — the capture-then-grant
double-fire #333 was about — and reports whether the conversion hold timer moved. It refuses
settlements with no existing conversion record, because there the first fire would ARM a persisted
timer that a later daily tick completes into a real culture flip, which is a Tier C mutation from a
command sold as a read-mostly regression guard.

The review over the full twelve-command feature found no HIGH issues but six worth fixing. Two are
the same failure in different clothes. `damage_agent` omitted `GameNetwork.IsClientOrReplay` from its
guard while the class comment claimed to model the engine's guard "including its mission-mode guard"
— and `IsReplay` is not multiplayer-only, it covers the singleplayer replay viewer. That is the third
round running in which a comment asserted a relationship that was intended rather than read. The
other: `(int)amount` had no upper bound, so a large finite input would leave `BaseMagnitude` huge
while `InflictedDamage` went degenerate and read downstream as healing — the float-to-int cast class
that has now shipped five times in TAOM, caught at a sixth site before it shipped.

Also fixed: `print_town_economy` re-derived the equilibrium target from raw prosperity while the
service sanitizes non-finite prosperity to zero internally, so a corrupt save would have printed a
finite daily change beside `target=NaN` in the same block.

**Tests:** 4732 passing. RCA addendum 2: `docs/reviews/rca-devconsole-phase0-2026-07-31.md`.

Research: Mission.KillAgentCheat, AttackCollisionData.GetAttackCollisionDataForDebugPurpose, Blow, GameNetwork.IsClientOrReplay, CampaignCheats.TryGetObject, CultureConversionService
Constraint: two of FormatRequeue's four branches are unreachable from the live command — both fires are synchronous, so the guard closes after whichever fires first. They stay to catch a future service regression.
Not-tested: AgentDamageCheats.ApplyBlow has no unit coverage — sealed engine types, no adapter seam; its correctness rests on an argument-by-argument decompile diff
Save-compat: no new save fields

### feat(devconsole): mission spawning, agent inspection, and the battle-scene lookup (#369)

`taom.spawn_troops <troopId> <count> [enemy|ally]` is the one that matters. Vanilla ships 80
`campaign.*` and 10 `mission.*` console commands and not one of them spawns anything, so composing a
specific fight means Custom Battle — which hands you a culture's default roster, not "twenty dwarves
and one mumakil" — or steering a campaign into the right encounter. It uses the mission gate rather
than the campaign gate, so it works inside a custom battle, which is where creatures and mounts
actually get tested.

The spawn path had one crash-class trap and it is guarded. `Mission.SpawnTroop` reads
`agentTeam.Color` with no null check, and `GetAgentTeam` returns `PlayerEnemyTeam` unguarded — which
is null in town and village missions. Without pre-resolving the team, the command would hard-crash
the game rather than print a sentence. Two smaller ones came out of the same read: `SimpleAgentOrigin`
hard-casts to `CharacterObject` (so the troop is resolved as one, not as a `BasicCharacterObject`),
and `initialDirection.Value` is dereferenced unconditionally once a position is supplied. Every
bool/int argument is passed by name, because `isAlarmed`/`wieldInitialWeapons` and
`formationTroopCount`/`formationTroopIndex` are adjacent same-typed pairs where a positional swap
compiles silently and misbehaves at runtime.

`taom.print_agent_info` is the other half of that loop: spawn a creature, then read back its race,
monster, action set, skeleton, mount and rider instead of guessing from the model. `taom.print_battle_scene`
answers which battle terrain a fight at the party's position would load — and its loudest output is
the empty one, because a map index no scene declares means a fallback or an assert at battle start,
which is how an engine bump breaks TAOM's map data silently. `taom.print_mission_scene` rounds it out.

A three-agent review over the batch found no crash-class defect but eight things worth fixing, and
two are worth naming. `AgentSnapshot.Health` defaulted to `0f` on a failed read — and zero is a
plausible health value, so a throwing read would have rendered a healthy agent as `hp=0.0/100.0` and
read as dead. It is nullable now. And `MomentumCheats` duplicated the save serialization while its own
comment claimed it shared it; that is the previous RCA's root pattern recurring a day later, so the
fix was to extract `BuildSavePayload` and make the claim structurally true rather than to correct the
sentence.

**Tests:** 4683 passing, 30 new. RCA addendum: `docs/reviews/rca-devconsole-phase0-2026-07-31.md`.

Research: Mission.SpawnTroop, Mission.GetAgentTeam, SimpleAgentOrigin, MBObjectManager.GetObject, MBActionSet, EquipmentIndex, GameSceneDataManager
Constraint: `spawn_troops X N ally` in a town spawns onto the player's own team rather than refusing — GetAgentTeam falls back to PlayerTeam when PlayerAllyTeam is null, and only the enemy path can return null
Not-tested: the command entry points need a live mission; their formatters and the snapshot builder's fallbacks are covered
Save-compat: no new save fields

## 2026-07-31

### diag(herorace): light up the character-preview path after a bug only players could see

Players reported their character rendering flat on its back — Character Customization, the inventory
doll, the encyclopedia — for every race, on new campaigns. It did not reproduce here, which turned
out to be the whole story rather than an inconvenience.

The probable cause is a version mismatch: users ran a current `TAOM.dll` against a stale
`TAOM.Dependencies.dll`. `Main/TAOM.csproj` resolves HarmonyLib and UIExtenderEx *through* that
assembly, so an old pairing fails at the member level while patches are being applied; the HeroRace
preview patches never attach, the tableau falls back to vanilla human resolution, and the skeleton
renders in its bind pose, which in Bannerlord is a body lying down. Shipping a rebuilt `TAOM.dll`
alone did not fix it. Shipping both modules did.

Nothing could have caught that. `TAOM.Dependencies` has declared `v2.0.5` on every release, both
assemblies carry frozen versions (`0.1.0.0` and `2.0.0.0`) on every build ever produced so .NET binds
any pair without complaint, and `Main/_Module/SubModule.xml` declares no dependency on
`TAOM.Dependencies` at all, so the launcher has nothing to check. The only evidence distinguishing a
current DLL from a two-week-old one was its file timestamp, which does not survive a download.

Two users' `diag.log` files cleared every layer already instrumented — engine version, PatchShield,
race registration, duplicate BUTR assemblies — and then stopped, because the preview path emitted
nothing whatsoever. `TableauDiagnostics` closes that: per-category patch results, an environment dump
naming the loaded identity and path of every BUTR assembly (flagging duplicates), a one-shot probe of
the engine's action-set count and every race's `_facegen`/`_warrior` resolution with skeleton and idle
clip, plus the Character Customization screen's, the tableau's and the spawner's own resolutions. One
line per distinct situation, errors deduplicated by message, capped at 600 — about 90 lines in a
healthy session, against the 6.4 MB log a previous unthrottled attempt produced.

Two defects were fixed on the way. The seven patch categories owning the preview path were applied as
consecutive unguarded statements, so the first to throw silently prevented the rest — a state no
shipped log could distinguish from success; each is now isolated and reports its outcome. Five `catch`
blocks that swallowed exceptions without a trace now say so. Separately, the release payload was
shipping this machine's `diag.log`, `failed-mods-catalog.txt` and `last-good-modlist.txt`, which is
why both users' logs began with sessions dating to May; they are removed.

Still open: the bug was reported as intermittent per launch, and a version mismatch should be
deterministic, so the fix is not confirmed until an affected user reports several consecutive clean
relaunches. `CharacterTableau.GetIdleAction()` poses the doll with `act_inventory_idle_start` while
Patch2 injects `as_<race>_warrior` — a zero-action stub for uruk — and the snapshot README records
that the engine does not fall through `base_set` for `act_inventory_*`. A set can be valid and still
bind no clip. The new `idleStart-anim=` field answers that on the next launch.

RCA: `docs/reviews/rca-prone-character-tableau-2026-07-31.md`.
Not-tested: patch application and tableau rendering both require a live game.

### chore(armory-snapshot): re-sync `action_sets.xml`, 390 lines behind the live file

The committed mirror had drifted since the 2026-06-25 partial patch, and the missing region was the
spider-rider partial redefinition of `as_human_warrior` at the top of the file — the block carrying
the `LOAD-ORDER CRITICAL` comment, added during the June mount work. Any audit run against the mirror
was auditing data the game never loads. Re-snapshotted (+402 lines, verified byte-identical to live);
`monsters.xml` and `skins.xml` were already identical and left alone. Both
`audit_action_set_parity.py` (0 humanoid gaps across 1304 sets) and
`audit_civilian_action_set_coverage.py` (13 settlement races at full coverage) pass against the live
files.

### fix(armor): a legendary tier-2 pauldron no longer out-armors a tier-6 one

A player screenshot showed `Legendary [Gondor] Anorien Infantry Pauldron I` — tier 2, 2,929 gold —
displaying Body 20 / Arm 20 and beating `[Arnor] Noble Elite Pauldrons` at tier 6 and 55,634 gold.

The rebalance curve was not the problem. Bannerlord item modifiers add a **flat** armor bonus
(`legendary_plate` is +12) and the engine applies it independently to every nonzero armor stat, each
guarded by `num > 0`. Capes carry two such stats, so one roll lands twice: 9/9 became 21/21. The
shoulder curve meanwhile spanned 16 points across all six tiers — less than a single roll — so the
tier ladder had no way to survive contact with the loot system. Nothing in the tooling could see it
either: `_get_primary_stat` returned only `body_armor` for shoulders, so cape arm armor was invisible
to every analyzer, and no check compared two items at all.

The curve is now modifier-aware. For every slot, governed stat, tier pair two apart and cultural
protection value, `base[n]` must exceed `base[n-2]` plus that tier's legendary bonus plus the variant
cap. Beating the adjacent tier on a lucky roll is intended and still happens; leaping three tiers does
not. Shoulders widened to 2/5/9/13/19/25 body and 0/3/6/11/17/22 arm, and their loot rolls cap at
`chain` (+9) rather than `plate` (+12), because the native deltas were sized for 30-60 armor chest
pieces, not 2-25 armor capes. `material_type` is untouched — it drives hit sounds, and the engine reads
it separately from the loot table. Civilian cape arm armor is exactly 0, which the `num > 0` guard
makes modifier-immune and which also matches vanilla, where 39 of 53 capes carry no arm armor at all.

Roman-numeral variants turned out to add up to +17 straight into a stat, uncapped, which would have
eaten margins that run as thin as +1; they are now capped. Three lord rows moved to break exact ties
with a `plate` roll (body leg 34→36, arm 32→34, leg 40→42) as constants only — re-stating those slots
would have touched 1,706 of 2,450 items, and the roster source maps `lord` to `elite` regardless.

Applied to the live armory: 1,143 loot-table retags and 933 material retags across all cultures,
parse-verified to have moved zero armor or weight values, then the shoulder curve for gondor, rohan,
arnor, dale, mordor, dunland, mercenary, mirkwood, rivendell and rhun. Keyword tiering was rejected —
it agrees with the roster map on only 71.5% of shoulders. Running the same check over the pre-fix
backup tree with the pre-fix constants puts shoulder inversions at 129 → 57 and all-slot at 716 → 574,
with curve violations 11 → 0. The reported pair now reads legendary Gondor 17/14 against plain
Arnor 21/18.

Two buckets are deliberately left: 31 inversions in the six cultures whose ceilings sit far above the
curve (Isengard heavy pauldrons at 38 body against a ~21 elite target) belong to the cross-culture
normalization PR, and 26 involve items no troop wears, where keyword tiering would take
`roh_nbl_gorg_tst_6` from 25/25 to 3/2. Both need per-item intent rather than a mechanical pass.

Guards added so this cannot regress: `check_curve_invariant()` is a pure function of the constants and
exits non-zero, `_check_tier_inversions()` reports the item-level version against the live tree, and
`test_armor_curve_invariant.py` pins the curve, the native ladder against the shipped
`item_modifiers.xml`, and the four per-culture generators — all of which still held the pre-fix
shoulder table and would have silently reverted the fix on their next run.

`--materials-only` and `--keep-materials` are new. The latter matters: freezing materials used to be
implied by `--no-lower-armor`, which is how roughly a thousand items kept a `plate` loot table they
never earned.

Rolled modifiers persist in saves by `StringId`, so an already-owned legendary cape keeps its old roll
until re-looted. Cape prices move on the next launch, since `Value` is recomputed at deserialize.

Not-tested: in-game verification (armor loads only at a full application restart).
Research: `ItemModifier.ModifyArmor`, `EquipmentElement.GetModified*Armor`, `DefaultItemValueModel.CalculateArmorTier`.

### feat(coopinterop): stop TAOM sabotaging a co-op mod, and make divergence visible (#370)

Reviewed the BannerlordTogether a0.5.3.2 package to see what TAOM would need to change so players
can run the mod together. It targets v1.4.7 — TAOM's own pin — where the April 2026 pass had only
seen v0.2.2 and stopped at a startup CTD.

The package ships `AI_USAGE_POLICY_DO_NOT_DECOMPILE.txt` and a proprietary licence forbidding any
person or automated system from analyzing its binaries. Nothing here reads them. What TAOM needs to
know about another mod's patches comes instead from HarmonyLib's own public runtime registry, which
reports owner ids and patched-method metadata for every mod in the process — enough to answer which
methods both mods patch and where two transpilers meet, without touching their assembly.

The real finding was on our side. TAOM has grown two AppDomain-wide shields since April, and both do
exactly the wrong thing under host-authoritative co-op. `PatchShield` strips a non-allowlisted
Harmony owner's patches after a missing-API exception; in singleplayer that turns a crash into a
survivable degradation, but with a co-op layer it removes one peer's sync patch and produces no
crash at all — just two campaigns quietly diverging, which corrupts both saves and cannot be
diagnosed from a log. `SaveShield` swallows everything on the save-load chain, so a failed load
becomes a partially deserialised campaign that the host then replicates as authoritative. Both now
invert when a co-op module is active: the unpatch is withheld (and logged as "would unpatch"), and
`SAVE-LOAD` rethrows while `MISSION-INIT` keeps swallowing, since a mission fault is local. The
`saveshield-swallow-disabled.flag` still dominates, and now records the failure on the rethrow path
too — that is precisely when the catalog entry is wanted.

`CoopPresence` decides all of it from the launcher's active-module list, matched against a shipped
`coop-modules.txt`. Parsing is union-only by construction: the file can add ids but can never remove
a compiled default, because that list also feeds PatchShield's protected-owner allowlist and a bad
edit must not be able to unprotect the BUTR/MCM stack.

Load order is pinned rather than left to the launcher, for a sharper reason than patch ordering:
BannerlordTogether ships its own `0Harmony.dll` at the same 2.4.2.0 TAOM deploys, and HarmonyLib's
patch registry is per-assembly-instance static state. If its copy wins the AppDomain slot,
`GetAllPatchedMethods()` from ours cannot see its patches at all — which would blind both PatchShield
and the census. The census logs how many `0Harmony` assemblies are loaded so that failure is
visible rather than silent.

Also added the save-definer preflight. The engine registers every `SaveableTypeDefiner` into a
dictionary keyed by save id and throws in `Module.Initialize` on a duplicate, naming neither mod —
and TAOM deliberately reuses an upstream mod's base id so CompanionTactics saves import, which makes
that a guaranteed unattributable crash for anyone running both. It now reports both assemblies by
name before the engine gets there.

One determinism fix fell out of reading the engine: `MBRandom` draws from
`Game.Current.RandomGenerator`, which is state on the saved `Game` root, and the engine ships a
separate `NondeterministicRandom` for values that must not touch it. TAOM's character-tableau mount
mesh key and elephant trample animation variant were spending campaign RNG on cosmetics. Both moved.
Correct in singleplayer regardless of co-op; the trample damage roll stays where it was.

The April verdict on the `DefaultClanFinanceModel..cctor()` startup crash survives re-verification,
and one plausible fix is now ruled out in writing: the type's static field initializers call
`Game.Current.GameTextManager.FindText`, so the NRE is on `Game.Current` and a guard on
`GameTexts.FindText` — a different static — intercepts nothing. Whether it still reproduces on
a0.5.3.2 is unknown; the zero-code boot matrix in the feature doc answers that before anything else
is built.

A 7-dimension deep review with adversarial verification then caught 18 real defects in the above, all
fixed here; RCA at `docs/reviews/rca-coop-interop-2026-07-31.md`. Four were consequential enough to
call out. The save-definer preflight ran too late to ever fire: `Module.Initialize` builds the save
definition context immediately after loading submodules, long before
`OnBeforeInitialModuleScreenSetAsRoot`, so on the one boot where a collision existed the engine had
already thrown — moved to `OnSubModuleLoad`. PatchShield and SaveShield both attach finalizers to the
save chain, and Harmony gives every finalizer on a method one shared exception slot, so PatchShield's
unconditional swallow silently overrode the new co-op rethrow; PatchShield now skips SaveShield's
targets. The protected-owner allowlist entry `Bannerlord.UIExtenderEx` matches neither id UIExtenderEx
actually registers (the real ones read `bannerlord.uiextender.ex`), so the rescue path could have
stripped TAOM's own UI mixins — a pre-existing gap inherited when the list moved into
`PatchShieldPolicy`. And `<DependedModuleMetadata>` turns out to be a BUTR launcher extension the
engine never parses, so the Main-side load-order pin established nothing until it gained a real
`<ModulesToLoadAfterThis>`.

Two doc overclaims of my own are also corrected: the feature doc said version parity was "enforced by
the build stamp" (it is logged, not enforced) and that a unit test pinned the no-decompile boundary
(it pins the census model, not the writer).

`Dependencies/Foundation/{CoopPresence,CoopModuleList,PatchShieldPolicy,SaveShieldPolicy}.cs`,
`Main/Features/CoopInterop/**`, both `SubModule.xml` manifests, `coop-modules.txt`. Suite 4617 green.

Not-tested: shield behaviour under a live co-op session, the census against a second Harmony
instance, and the boot matrix itself — all require a running game and a second player.
Research: TaleWorlds.Core.MBRandom, Game.RandomGenerator, GameTexts.FindText,
DefaultClanFinanceModel static initializers, SaveableTypeDefiner._saveBaseId (installed v1.4.7).

### feat(devconsole): the first six read-only `taom.print_*` commands (#369)

Phase 1 of the console suite — the half that replaces the most manual play time. Every one is Tier A
(read-only, cheat gate only) and every one collapses a check that currently costs an in-game session.

`taom.print_momentum` is the best of them. The momentum save payload corrupts the save when a single
SyncData string crosses the engine's int16 archive-entry length, and confirming the chunker holds
means playing a campaign to roughly day 50 with active wars and then saving — the failure only shows
on the *next* load. The command prints byte count, chunk count and headroom on demand. Its
`WOULD CORRUPT` branch is unreachable today by construction and is pinned anyway, because a future
bump to `MaxChunkChars` is exactly the change that makes it reachable.

`taom.print_party_size` prints the whole weight-deflation chain the #337 rework made invisible. The
load-bearing detail is that a `penalty=0` reading says *why*: `ComputeSizePenalty` returns 0 both when
the party is light and when `baseLimit < 2` — the guard added after a NaN-poisoned cast defeated the
clamp — and rendering those two identically would hide the exact failure the guard exists to catch.

`taom.print_town_economy` prints TAOM's and vanilla's daily gold change side by side, which is #317's
real question. The vanilla column costs nothing: `ComputeTownGoldChange` with a null config computes
with the vanilla constants, so the report cannot drift from the engine's own numbers the way a
duplicated formula would. It refuses castles rather than computing a misleading figure — the engine
only ticks `Town.AllTowns`, so a castle never reaches that model.

`taom.print_patches` reconstructs the declared-vs-applied Harmony registry with no new bookkeeping in
`SubModule`: declared categories from reflecting the attribute, applied ones from walking
`GetAllPatchedMethods` back to each patch method's declaring type. It deliberately does not treat
NOT APPLIED as a fault — manual patches carry no category and some categories are never registered on
purpose — because a report that cried wolf would be ignored within a week.

`taom.print_special_resources` and `taom.print_races` round it out. The former is deliberately not
`GrantAmount(..., 0f)`: that would look like a free read but clamps and writes back, so on a save
whose balance predates a lowered cap a command named "print" would silently mutate it. The latter
validates the race id before the lookup, because `GetRaceNameFromId` coerces unknown ids to `"human"`
and a diagnostic that lies is worse than no diagnostic.

The assembly-wide binding tests picked up all six with zero new wiring and confirmed unique names,
correct shape, the naming convention, and the no-campaign gate — the Phase 0 contract doing its job.

**Tests:** 32 new formatter tests. Three semantic guards (degenerate-vs-light party limit, the
zero-rate divide-by-zero, tier-0 rendering) were verified RED by defect injection, and each failed on
exactly its own test.

Not-tested: the command entry points need a live campaign; the formatters they delegate to are fully covered
Save-compat: no new save fields

### feat(devconsole): the shared contract every `taom.*` command will route through (#369)

TAOM has one console command (`taom.add_special_resources`, #365) and a backlog of features whose
smoke tests cost minutes to hours of manual setup — starting a siege and surviving a reinforcement
wave, playing ~50 in-game days until the momentum payload crosses 32 KB. TOR_Core's
`TORConsoleCommands.cs` prompted a broader suite. This is Phase 0: the contract, not the commands.

Reading the engine first changed what the contract had to do. `CollectCommandLineFunctions` calls
`Delegate.CreateDelegate` with no try/catch, and that call sits behind an `[EngineCallback]` invoked
from `TaleWorlds.Native.dll` through a bare `[MonoPInvokeCallback]` — there is no
`AppDomain.UnhandledException` handler anywhere in the decompiled managed tree, and
`Utilities.AddCommandLineFunction` only runs after the collect returns. So a malformed command is
plausibly a startup hazard, not a degraded console. Dispatch has the same shape, which makes a
read-only command that walks `Settlement.All` no safer than a mutating one.

`TaomConsole` is the answer: cheat gate, then help, then the body inside a catch-all, with the
failure printed rather than thrown across the boundary. Three gates rather than one, because
`CampaignCheats.CheckCheatUsage` demands a campaign and that would lock mission commands out of
custom battles — TAOM's main venue for testing creatures and mounts. `DevConsoleArgs` holds the
parsing that would otherwise be re-decided per command: invariant culture, and `NaN`/`Infinity`
rejected rather than clamped (`float.TryParse` accepts both, and that bug class has shipped five
times).

`DevConsoleDiscoveryAudit` settles a question carried as "[Likely]" since #365. Whether the engine's
discovery pass reaches the TAOM assembly is not decidable offline — the call site is native, and
module load order is data-dependent — but `HasFunctionForCommand` is public, so TAOM asks at startup.
It queries a vanilla control command alongside our own; without that control, "our command is
missing" cannot distinguish "too early" from "dropped", and those need opposite reactions. It is also
the only way a silent duplicate-name drop becomes visible on a player's machine.

Naming settled from evidence rather than taste: across all 130 vanilla commands, `print_` appears 9
times and `dump_` zero, so `print_` is the read-only verb and `dump_`/`list_`/`get_` fail the build.
Console output stays raw English — vanilla's `CampaignCheats` strings are English consts that TAOM
returns verbatim, so localizing ours would give a mixed-language console.

The binding tests moved out of the SpecialResources folder, where they were misfiled as one feature's
concern despite being an assembly-wide invariant, and gained three checks: unique qualified names,
the naming convention, and invoking every command with no campaign. That last one proves in a single
assertion that the gate exists, runs before any campaign access, does not throw, and that no
declaring type touches engine state from a static initializer — the pattern TOR's command class uses
and we deliberately do not.

**Files:** `Main/Features/DevConsole/{TaomConsole,DevConsoleGuard,DevConsoleArgs,DevConsoleDiscoveryAudit}.cs`,
`Main/SubModule.cs`, `Main/Features/SpecialResources/Cheats/SpecialResourceCheats.cs`,
`TAOM.Tests/Features/DevConsole/*`, `docs/features/dev-console.md`,
`docs/features/special-resources.md`, `docs/reference/feature-map.md`, `CLAUDE.md`.

`/deep-review` (5 agents) found no functional bug in the C#, and API compatibility re-verified all 8
engine signatures against the installed v1.4.7 DLLs rather than the dump. What it did find was a
cluster of claims stated more confidently than the evidence supported — five of nine findings sat in
comments and the feature doc. One mattered: the vanilla-command inventory had been grepped across the
whole decompile tree, which carries a dual shipping/editor build, so `mission.list_agent_ids`
(editor-only, zero occurrences in the shipping client) was written into the doc's "already exists, do
not reimplement" list, and the `mission.*` group was undercounted as 6 against a real 10. Fixed, with
the trap now named in the doc and the lesson appended to `lessons/adapters-taleworlds-api.md`.

Also from the review: `TaomConsole` null-coalesced the body result but returned `usage` raw on the
help branch, so a command authored with a null usage string would return null across the native
boundary — every return path from the shell is now defended identically. `DevConsoleArgs.TryParseSide`
was deleted; it had zero callers, written ahead of a `spawn_troops` command that is deferred, and the
"pin shared sub-problems once" rule it was justified by applies to sub-problems appearing in two or
more builder briefs, not one. The discovery audit now caches its attributed-command scan, so a build
declaring zero commands no longer re-reflects over every type on each return to the main menu.

Parse-error wording changed as a side effect of the migration: the messages now name the offending
input (`Please enter a number — 'abc' is not a number.`) instead of a bare sentence. Nothing asserted
the old strings.

**Tests:** 24 new — 5 binding-contract, 14 parser, 5 audit-verdict — and the full suite green. The
five binding guards and the four audit verdicts were each verified RED by injecting their defects
before acceptance. RCA: `docs/reviews/rca-devconsole-phase0-2026-07-31.md`.

Research: `CommandLineFunctionality`, `ManagedExtensions`, `EngineCallbacksGenerated`, `CampaignCheats`, `Game`, `Mission`
Constraint: the native call site for `CollectCommandLineFunctions` is not decompilable — discovery timing can only be answered at runtime, hence the startup audit
Rejected: an IL scan asserting every command routes through `TaomConsole` — the invoke-with-no-campaign test gets ~90% of the enforcement for ~5% of the machinery
Rejected: porting TOR's `trigger_fatal_crash` — in a released mod it would pollute `CrashReport`'s signal with reports indistinguishable from real ones
Not-tested: the gates themselves (need a live `Game`) and the audit's engine-querying half; the in-game discovery check at the main menu is still owed
Save-compat: no new save fields

## 2026-07-30

### fix(lotrissues): the deliver-captives quests counted a troop type TAOM barely has (#368)

A player carrying 20 prisoners could not hand any of them to Pelendur of Anduinbrethil for **Hands
for the Mines** — the turn-in option stayed greyed out. Both `DeliverPersonnel` configs counted a
prisoner only when its `Occupation` was `Bandit`, and TAOM declares that occupation on **8 troops in
the entire mod**, all hideout bosses. Every bandit-culture rank-and-file (`dunland_peasant`,
`balcoth_volunteer`, `harad_levy`) is `occupation="Soldier"`, because those are the same entries the
regular Dunland/Rhûn/Harad factions recruit from. Only vanilla looters ever satisfied the filter.

Culture was no help either — the bandit cultures point at troops owned by *regular* cultures, so a
roster entry carries no trace of the party it came from. The rule is now **any non-hero prisoner**,
and it lives in `ILotrIssueService` as `CountDeliverableCaptives` + `PlanCaptiveHandover` over a
TaleWorlds-free `LotrCaptiveStack`, sharing one predicate so the turn-in gate and the removal can't
drift apart the way the two duplicated loops could. Heroes stay excluded: pulling one off the roster
directly would skip `EndCaptivityAction`. The handover passes no `woundedCount` on purpose —
`AddToCountsAtIndex` already clamps `WoundedNumber` to the new `Number`.

Quest text drops the bandit-only promise it was making, in the two configs and in the feature doc,
which still described the mechanic as "hand over N bandit prisoners". Existing saves heal on the next
`Refresh()`; no saveable field moved. +13 service tests (21 → 34), suite 4542 green.

Deep-review found no code defects across five agents; the compatibility pass verified 16 API touch
points against the installed v1.4.7 DLLs and established that vanilla's own `TroopRoster.RemoveTroop`
omits `woundedCount` exactly as this does. RCA — including why a right-for-vanilla predicate in a
test-exempt layer survives 4500 tests, five agents and a Codex pass —
[`rca-lotr-issues-deliver-personnel-2026-07-30.md`](docs/reviews/rca-lotr-issues-deliver-personnel-2026-07-30.md).

Owed: the 12 translated string files still read "bandit captives" — `translate_with_claude.py` needs
`ANTHROPIC_API_KEY`, unset here, and a changed default on an existing key needs its cache entry
purged first (the cache is keyed by string id, not by source text).

### feat(gondor): Lond Cirion barracks, and panels gripped by their structural body

`lond_cirion_barracks_01` — 18 x 6 m, two 3 m storeys, 26.57 gable, 79,480 tris (22,236 / 7,138 /
3,562). Parade front to the south with a double-door entry, a deliberately blank north service
wall, ten buttresses on the bay joints, an external stair to an upper door, decks at both storeys.

Two new part families, two new traps, both caught by rays rather than by eye. The **stair runs
backwards** if placed unrotated: its high tread is authored at local -Y (y -3.02, z 3.01) and its
foot at y 0, so the first build had it climbing away from the building — the down-ray profile read
0.6 m at the wall rising to 3.0 m three metres out. It now descends monotonically from the door to
grade in both mesh and collision. The **buttress** hangs its body off a mounting plane at y 0 and
needs the same origin anchoring as the eave strips; measured at +0.51 m projection on both faces.

Also fixed a latent defect the barracks exposed on all three buildings: parts were gripped by
their whole group's bounding box, but each panel type carries different decorative sub-objects, so
their structural wall planes landed millimetres apart — measured, two facade planes 5 mm apart
carrying 111.9 and 39.9 m2. Panels are now gripped by `<prefix>.wall` (else the `<prefix>` object),
collapsing both onto one plane carrying exactly their sum, 151.8 m2. Invisible in a render;
z-fights in-game.

Not a defect, recorded so it is not chased again: ~25 black patches on the barracks facade are a
preview artifact. No kit texture resolves headless (every material reports `file_exists=False`), so
Blender falls back per material and `gondor_bricks_small_a_normal_mat` falls back to near-black. A
1,633-ray facade sweep stopped 1,422 rays at the wall and every one of the 211 that passed through
was a door or window aperture.

### fix(erebor): lift the Iron Hills noble crossbow line above the regulars (#366)

`iron_hills_noble_scout`, `_sharpshooter` and `_veteran_sharpshooter` carried exactly the same
Crossbow value as their `ironpass_*` counterparts at every tier — 130 / 170 / 205 in both lines —
so the noble branch had no advantage in the one skill it exists for. It was already ahead on
One-Handed (+5), Polearm (+10), Bow (+5) and Riding (+10–15), which is what made the Crossbow tie
read as an oversight rather than a design choice.

Now 175 / 225 / 275, about 1.34× the regular line at each tier so the gap holds all the way up
rather than appearing only at the top. The `ironpass_*` line is untouched — nothing gets weaker.

These three values are off-formula. `tools/rebalance_troops.py` derives Crossbow from level and
`CULTURAL_MODS['iron_hills']` alone, and its `--dry-run` wanted all three back at 130 / 170 / 205 —
so the ids are now in `SKIP_TROOP_IDS` and recorded in `docs/features/troop-skill-balance.md`.
Without that the next `--apply` would have reverted this silently.

Worth recording, because these are deliberate spikes rather than values fitted under an existing
ceiling: each of the three becomes the highest-skilled troop at its level, and the ladder now has
inversions across levels. The level-21 Scout at 175 out-shoots every level-26 archer in the file
(Bow 170); the level-26 Sharpshooter at 225 beats the level-31 `ironpass_sharpshooter` at 205; and
at level 31 the noble sits 70 points above its same-level, same-weapon `ironpass` peer, against a
previous level-31 ceiling of 260 (`erebor_reg_mattock_warrior`, TwoHanded).

Save-compat: skill-only, no save migration. Troop skills live on the shared `CharacterObject`,
which is rebuilt from XML at every launch, so existing parties pick the new values up on next load.
### feat(erebor): spread unused dwarf armor and 2H weapons across troop rosters (#367)

Of 432 dwarf items in `LOTRLOME_Armory`, 127 were referenced nowhere in ModuleData. Most of that is
reserved by design — 52 clan/lord colourways, 33 spare pieces of Dáin's personal set, tournament and
lord-tier gear — leaving 38 genuinely dead line-troop items. Separately, 11 of the 27 two-handed
weapons sat on exactly one troop each while the two spears carried 71 of the 1,368 battle-roster
slots, and individual troops repeated the same helmet or axe across several of their own rosters,
wasting the randomisation those rosters exist to provide.

`tools/apply_erebor_equipment_sweep.py` rewrites duplicates in place: where a troop uses the same
item in the same slot in more than one of its battle rosters, the repeat becomes a sibling item —
same Armory stem or same visual family, comparable armour value, preferring whatever is used least
across the file. 200 substitutions, 15 of the 38 dead items now in use, two-handers with two or
fewer references down from 16 to 15.

Only the second and later occurrence within a troop is ever rewritten, so the displaced item always
survives in that troop and nothing can fall out of use: 305 dwarf items referenced before, 320 after,
zero lost. Roster counts, troop ids, levels, skills and upgrade targets are untouched — 60 troops,
223 rosters, 1,434 equipment slots before and after, and no roster gained a duplicate id.

Most of the work here was in constraining the selection rule, because the naive version was wrong in
four separate ways that all looked fine on a first read. Weapons and shields keep their stats outside
`<Armor>`, so the armour-value check cannot police them and a tower shield was being swapped for a
leather one. Pauldrons with and without a cloak share the Cape slot, so crossing them silently
stripped a troop's cloak. Ranged gear is tier-ordered on purpose — the sweep wanted to hand the
level-21 arbalest a 130-damage crossbow while the level-31 sharpshooter kept the 120, the inversion
`.claude/rules/troops.md` records from the Dale yew-bow case — so bows, crossbows, arrows and bolts
are excluded outright.

The fourth was the one that mattered. "Prefer whatever is used least" reaches straight for end-tier
exclusives, because being end-tier is *why* they were rare: an early run put the level-46 royal
warden's cuirass on a level-11 recruit, gave a level-21 Longbeard the same 2.96-damage axe as the
level-46 Royal Warden, and handed the level-36 Mountain Guard a 20-unit stub blade while a level-21
crossbowman's sidearm got the 43-unit one. 25 items dropped ten or more levels. Crafted melee weapons
have no `<Weapon>` element at all — reach and damage live on the blade crafting piece — so nothing in
the item was being compared. The rule now derives each item's tier floor from the lowest-level troop
already trusted with it, refuses to place anything below that floor, holds weapon reach and damage
within 10%, and requires armour `material_type` to match so mail cannot become plate at equal armour
value. No item's minimum wearer level drops anywhere in the final diff.

Recorded in `docs/features/troop-tree-revamp.md` (Change History + script table) so the next session
opening the Erebor tree finds the sweep and its two traps without re-deriving them.

That tier floor is why only 15 of the 38 dead items were placed rather than 33: the rest are genuine
end-tier gear whose only home is the `iron_hills_noble_*` line, and those 13 troops ship a single
battle roster apiece, so there is no duplicate to displace without adding rosters. `sm_iron_shield_b_gold`
has no stem sibling at all, and `sm_dwarf_iron_hammer_e` has 43 units of reach against 18–24 for
every other dwarf hammer — placing it is a weapon-balance decision about who should carry a
long-reach hammer, not a variety swap, so it is left for hand-authoring.

Not-tested: in-game render. ModuleData edits do not load until a full game restart, so the new
helmets, bracers and shields still need a visual check on a fresh launch.
Constraint: dwarf 2H weapons are `<CraftedItem>`s — reusing shipped ids only. Authoring new ones
crashes new-campaign load on uncompiled meshes (commit 436a1d05, the reverted Gondor poleaxe).
Save-compat: equipment-only, no troop id / roster count / upgrade_target change.
### feat(specialresources): `taom.add_special_resources` console cheat (#365)

Testing anything downstream of the resource economy — elite upgrades, the recruit gate, the Elite
Emissary, tier thresholds, deficit desertion — meant grinding battles or editing a save, because
there was no gold-cheat equivalent for War Spoils / Gems / Castar / Marks.

`taom.add_special_resources [amount]` (default 1000, negatives deduct and floor at 0) adds to
whichever resource the player's kingdom→culture resolves to. It clamps to that resource's XML `cap`
like every legitimate earn path and echoes the real before→after, so a clamp is never silent.

This is TAOM's first console command. `CommandLineFunctionality` reflects over every loaded assembly
that references `TaleWorlds.Library`, so the attribute alone is the wiring — nothing was added to
`SubModule.cs` or `IoC.cs`. The command class stays a thin entry point: it parses the console text
(rejecting `NaN`/`Infinity`, which `float.TryParse` accepts) and delegates to the new
`ISpecialResourceService.GrantAmount`, which reuses the existing private `AddCapped`. The grant tests
run against a real `SpecialResourceStorageService` rather than a substitute, since the floor-at-0
clamp belongs to storage and the cap clamp to the service.

The deep review then caught the command lying about its own effect: the clamp report keyed on the
sign of the request, so a save whose balance predates a lowered cap clamped on a *negative* grant
(550 − 10 → 500, not 540) and reported "(cap 500)" as though nothing happened — and the floor-at-0
was never reported at all. Both now report from the unclamped result, and the formatter was pulled
out of the `Campaign.Current`-dependent static so all six branches are covered. `ConsoleCommandBindingTests`
pins the engine reflection contract, because the engine's discovery loop calls `Delegate.CreateDelegate`
unguarded — a malformed TAOM command would abort discovery for every other command, vanilla's included.
17 tests total; every guard verified RED by injecting its defect before acceptance.

Known limitation: `CollectCommandLineFunctions` is invoked from `TaleWorlds.Native.dll`, so whether
discovery runs before or after TAOM's assembly loads is inferred, not proven. At the main menu,
`taom.add_special_resources` returning "Campaign was not started." confirms discovery; "Could not
find the command" means it never saw the TAOM assembly.

Research: `TaleWorlds.Library.CommandLineFunctionality`, `CampaignCheats.CheckCheatUsage/CheckHelp/CheckParameters`
Save-compat: No new save fields — the balance rides the existing `_taom_specialResources` SyncData.
Not-tested: The static console method itself (needs `Campaign.Current` + cheat mode).

## 2026-07-29

### feat(gondor): two pilot Lond Cirion buildings composed from the Gondor part families

New [`blender_assemble_lond_cirion_buildings.py`](tools/oneoff/blender_assemble_lond_cirion_buildings.py)
→ `blockout/lond_cirion_buildings_a.fbx`: `lond_cirion_house_01` (6×6 m, two 3 m storeys, 45° gable,
41,240 tris + lod3/lod6/bo) and `lond_cirion_house_02` (12×6 m arched hall, 26.57° gable, 31,122).
This is the *forward* composition path — a signature matcher proved shipped Gondor buildings are
merged component meshes, so the artists' recipes cannot be recovered (0/26 matches).

Part families are trappier than whole kit pieces, and a 16-agent audit (4 finder dimensions → 40
findings → 12 adversarial verifiers, 9 confirmed / 3 refuted) found why. A part is a *prefix* over
sub-objects, tiers assemble per sub-object, and the anchor is the group bounding box — so a
`.decalleak` card hanging 1.474 m below `gondor_wall_trim_6m_a` hijacked the anchor and threw
house_02's cornice 1.54 m up to ridge level, hiding the roof; and the 45° gable's solid tympanum
plate, named `.wall.lod` with no plain `.wall` sibling (artist typo), was dropped by the LOD filter,
leaving both gable ends 50% open and see-through. The user caught both in-editor first.
Also fixed: eave strips pinned by authored origin, gables seated by plate apex, ridge caps, trims
flush on the wall centreline (restoring the 3 m module grid), LOD ladder extended to
`.lod3`/`.lod6`, floors from the previously un-catalogued `gondor_ground_straight_a`, and a 3 m
below-grade skirt matching 9 of 10 shipped buildings.

Verified on the exported FBX rather than the log: `tris == expected_sum` on all 8 tiers (the
assembler now asserts it), gable rays 21/21 and 15/15 blocked, floor rays 121/121 and 253/253,
z_min −3.000 everywhere, coincident triangles 56/32 (panel end-caps, not a doubled skirt). The three
refuted findings are recorded as deliberate deviations in
[`docs/kitbash/lond-cirion-buildings.md`](docs/kitbash/lond-cirion-buildings.md) — notably that our
non-identity export transform is fine (the gatehouse carries it and imports correctly), and that
trims stay flush per the user's editor check over a verifier's cornice-practice argument.

### fix(diplomacy): War of the Ring phases pushed to Day 30 / Day 44

Isengard and Dunland attacked Rohan on **Day 2** and the whole map went to war on **Day 14** — the
War of the Ring was over the player before a campaign had any shape. Phase 1 now fires on **Day
30**, Phase 2 (all `Hostile` pairs at war, peace permanently blocked) on **Day 44**, leaving a
month of peace to establish yourself and a two-week Rohan/Isengard-only window before the world
ignites.

The days resolve through four sources and all four were set, since a disagreement between them is
invisible until a player without MCM loads the mod: shipped `diplomacy/war_of_the_ring.json`, the
`TaomSettings` MCM defaults (`Phase1TriggerDay` / `Phase2TriggerDay`, hint text included),
`TaomSettingsProvider`'s null-instance fallbacks, and `WarOfTheRingConfig`'s compiled defaults. No
logic changed — `WarOfTheRingService.CheckPhaseTransition` already read the values.

`WarOfTheRingConfig.Phase2` no longer inherits `PhaseConfig`'s default day. It shared Phase 1's
value, and `ValidateConfig` only reverts Phase 2 when it is *strictly* below Phase 1, so equal days
passed validation and would have fired `IsengardWar` and `FullWar` on the same tick had the JSON
gone missing. `testMode` (1/3) is untouched.

**Existing players keep Day 2 / Day 14.** MCM persists `TaomSettings` to
`Configs/ModSettings/Global/TAOM/`, so the new defaults only reach fresh installs — anyone who has
already launched the mod must move the sliders or delete that folder. Phase state is also saved
(`WarOfTheRing_CurrentPhase`), so an in-progress campaign resumes at whatever phase it reached; the
retune is visible on a new campaign.

Docs corrected while there: `war-of-the-ring.md` had documented the MCM defaults as 30/45 since the
Day-2 retune on 2026-05-22, describing days the mod never shipped, and its in-game test steps cited
the pre-1/3 `testMode` values. `diplomacy.md` still claimed both days were 1.

### fix(diplomacy): phase days now clamped so the Isengard war can't be skipped

`CheckPhaseTransition`'s two guards are sequential `if`s over an in-place `CurrentPhase` mutation, so
any equal or inverted `(phase1Day, phase2Day)` pair ran **both** transitions inside one call — Rohan
was attacked and the entire hostile tier went to full war on the same tick, with `IsengardWar` never
observable to the map meter, the momentum service, or the save. Both transitions logged normally, so
nothing looked wrong.

Three of the four value sources could produce such a pair. `ValidateConfig` guarded only the JSON
pair and used a strictly-`<` check, so equal days passed; it never inspected `testMode` at all; and
the MCM sliders — the path that governs in-game — were validated by nothing. Phase 1 = 100 with
Phase 2 = 44 is reachable with two individually-legal slider values. MCM 5.12.1 does not help here:
`[SettingPropertyInteger]` bounds are UI-only metadata and `BaseSettingsJsonConverter.ReadJson`
assigns with no range check, so a stale settings file bypasses even the slider range.

`GetEffectivePhaseDays` now clamps `phase1 = max(1, phase1); phase2 = max(phase1 + 1, phase2)` after
source selection — one clamp at the fan-in covers all four sources, where per-source validation
would drift. `ValidateConfig` keeps its own pass, tightened to `<=` and extended to `testMode`,
because a silent clamp cannot warn an author that their edited JSON is wrong.

24 tests added. The MCM branch of `GetEffectivePhaseDays` previously had **zero** coverage — every
test pinned `IsAvailable = false` in `Setup()`, which is how this survived behind a healthy-looking
25-test count. `WarOfTheRingConfigProviderTests` is new (none existed). `WarOfTheRingShippedConfigTests`
pins the shipped JSON so the doc/code drift this review found is caught by the suite next time.

Also: the Test Mode tooltip promised "(2/5 days)" while the shipped JSON has been 1/3 since
2026-05-22 and JSON wins for every key it contains — tooltip and `TestModeConfig` defaults both now
read 1/3. RCA: `docs/reviews/rca-wotr-phase-ordering-2026-07-30.md`.

### fix(gondor): Riding Caparison was unequippable — harness had no family_type (#364)

`[Gondor] Riding Caparison` (`starter_cavalry_gondor_horse_armor_a`) refused to go into the
mount-armor slot with **no error message**, leaving the red `No Saddle!` warning up. Its `<Armor>`
element carried no `family_type`, which `ArmorComponent.Deserialize` defaults to 0 — the *human*
family — while every horse is `Monster.horse`, `family_type="1"`. `SPInventoryVM.
IsItemEquipmentPossible` compares the two and returns false silently (v1.4.7 `:4112`); the same
comparison at `:3923` strips a pre-placed one, which is why the Gondor cavalry career start
(whose XML roster bypasses the UI gate) lost the harness on the first inventory transfer.

**The fixed file is OUTSIDE this repo and untracked**, so it is recorded here to survive an Armory
reinstall: `Modules\LOTRLOME_Armory\ModuleData\LOTRLOME_items\LOTRAOM_horses.xml`, the
`starter_cavalry_gondor_horse_armor_a` block gained `family_type="1"`, `mane_cover_type="all"`,
and `<Flags Civilian="true"/>` — parity with `gondor_horse_armor_1`, which uses the identical
`lrd_horse_armour_2` mesh. The missing Civilian flag was a second defect (it also barred the item
from civilian equipment mode, `:4042`); it was the only harness in the Armory lacking either.

Prevention, since 86 harness ids are un-auditable by eye: `tools/taom_schema.py` gained
`MISSING_HARNESS_FAMILY_TYPE` (a `Type="HorseHarness"` with no `<Armor family_type>`) and
`HARNESS_FAMILY_MISMATCH` (a `Horse` + `HorseHarness` pair in one `EquipmentSet`/troop
`EquipmentRoster` whose family types disagree), backed by `harness_family_types` /
`mount_family_types` registries. Mount-side family type resolves from the monsters XML following
`base_monster` — `HorseComponent.Deserialize` never reads `family_type` off `<Horse>`, so those
attributes are dead data — and monster ids declared with conflicting values across modules
(ADOD_Beasts vs Native/LOTRLOME) resolve to unknown rather than emitting a false mismatch.
8 new tests (231 in `tools/tests`); `validate_moduledata.py` PASS. Restoring the defect in-memory
against the real registry reproduces exactly one ERROR, so the rule would have caught the 2026-05-21
authoring miss. In-game verification still owed — item XML is read at process launch, so it needs a
full game restart, not a campaign reload.

Research: SPInventoryVM.IsItemEquipmentPossible, ArmorComponent.Deserialize, HorseComponent.Deserialize
Not-tested: in-game equip (needs a game restart)
### fix(gondor): gatehouse tower crowns chamfered, wing root merlons dropped

Two in-editor findings on the hybrid gatehouse. (1) tower_l3_b's rim corners ship as LOW caps
(m3/m6/m9/m12, z 18..21 vs the edge segments' 18..22.9), leaving a notch at every crown corner —
each is now bridged diagonally with the tower's own tall m1 segment: the chamfer line between
edge-segment ends, (4.5, 7.67)→(7.67, 4.5), is 4.48 m and m1 is 4.5 m, an exact fit; 8 chamfers
across both towers turn the crowns into full-height octagons. (2) The l1_d wings' root-end
merlon pair (m1/m13, world y −9.3..−5.3) crosses the rear tower's south wall at −8.1 — half of
each fin poked through the tower's interior floor, half clipped out of the wall face (user
screenshots); the pair is dropped from both wings. Tri deltas exact on all four tiers
(base +8,144 = 8×2,036 added − 4×2,036 removed; bo +1,080; lod3 +3,040; lod6 +1,512).
Follow-up (same day): the low caps carry their own short merlons, so cap + chamfer doubled the
corner merlons — the caps are now dropped from the tower part set (8 × 732 tris off the base
tier, again exact on all tiers); each corner has only the single tall diagonal run.
Follow-up 2: the real "double merlons" the user kept seeing along the span-facing edges were a
**floating arc of the OLD octagons' crown ring** — the octagons reached inboard to |x| 6.95,
BIG_CUT starts at 7.9, so a ~1 m sliver of crown merlons + machicolation survived at z 26..32
beside each new tower. New `SPAN_CUT` (|x| 6.5..8.0, y ±8.2, z ≥ 19 — above the span parapets)
excises it: base −6,374 tris across the two junctions, all tiers proportional.
Follow-up 3: the octagons' **wall facet** survived the same way below — a full-height 0.5 m
sheet at |x| 7.4..7.9, z −15..12, standing detached off each new tower face (user editor check;
junction probe). New `PANEL_CUT` (|x| 7.35..8.0, z ≤ 12) removes it while keeping the span
deck + parapet ends (z 12.2..19 in the same x window) walkable; cut regions now carry an
explicit zmax. −1,332 tris per visual tier, exact.

### feat(gondor): gatehouse wings replaced with gondor_castle_wall_tower_l1_d kit instances

The user identified the gatehouse's merged wings as embedded copies of the kit piece
`gondor_castle_wall_tower_l1_d` — verified dimensionally: at T(±14.79, −15.8, 0) the authored
merlon band (|x| 3.485..4.67) lands on the measured wing parapet (18.28..19.46) and the authored
prow tip (−18.558) on the body's −34.36 tip, bo tiers agreeing to ~1 cm.
`blender_hybrid_gatehouse.py` now cuts the wings off whole (y −35..−8.2, which also removes the
front roofed towers standing on them) and places clean l1_d instances — full part set, 4 tiers ×
(main + 13 merlon addons). This supersedes the same-day donor-clone/bisect refill: the entire
surgical apparatus is deleted, since the kit piece brings its own merlons, slit windows,
stairwell pit, and machicolated prow. Composite bbox confirms placement (y-extent 41.03 ⇒ prow
tip exactly −34.36); renders show uniform merlon rhythm and no seams.

### fix(gondor): hybrid gatehouse front towers cut full-height, wing wall refilled from measurement

The first gatehouse build cut only the front towers' roofs (user editor check), and its refill
donor band sat inside the wing-end turret, stamping three extra crown copies up the wall. A
0.5 m y-bin probe of the wing measured the true tower octagon at y −24.0..−14.0 — the old cut
(−24.5..−15.5) left a 1 m full-height slice at its north edge — and the only clean wall band at
y −13.5..−9.5, **north** of the tower (the south band belongs to the turret).
`blender_hybrid_gatehouse.py` now cuts the measured extent, clones the north band southward in
4 m cells, and **bisects** faces at the gap planes, the donor edges, and each copy's cell
borders: the wall's lower body is big quads, so face-center tests alone leave intruding slabs,
drop band content whose parent quad is centred outside, and coplanar-double-stamp the run.
Verified on the exported FBX: parapet face bins continuous with exact 4 m periodicity through
the fill, lower-wall spike pattern matching the intact run, E/W symmetric; height 47.2 → 42.9 m
(the leftover slice was the old maximum). Renders + probe tables:
`E:\LOTRAOMAssets\_export\lond_cirion\gatehouse\`.

### fix(dialogue): rulers no longer introduce themselves with Calradian demonyms (#363)

Théoden greeted the player as *"king of the Vlandians"*. The noun comes from
`str_liege_title.<culture>` in vanilla `SandBox/ModuleData/comment_strings.xml`, picked by
conversation tag — `VlandianTag.IsApplicableTo` is `Culture.StringId == "vlandia"` — so none of
TAOM's renames (kingdom `name` / `short_name` / `title` / `ruler_title`, culture `name`) could
reach it, and `str_liege_title` had never been overridden in the repo. All six vanilla-renamed
kingdoms leaked, vassals included (`comment_strings.xslt:103` reuses `{LIEGE_TITLE}`).

`comment_strings.xslt` now rewrites all 12 `str_liege_title[_female]` strings: **King of the
Mark** (Rohan), **Brenin of Dunland**, **King of Dale**, **Taskral of Harad**, **Khudriag of
Khand**, **Loke-Kan of Rhun** — male forms reusing each kingdom's existing `ruler_title`.
Each template re-emits `node()`, because the vanilla `<tags>` child is what selects the
variation: `FindMatchingScore` gives a matching tag +1, a **tagless** variation 0, a
non-matching tag -2.1e9, so a tag-stripped string would match every culture at once and the six
titles would collide. 12 `TAOM_liege_*` keys registered in `taom_xslt_strings.xml` and stubbed
into the 12 language files (English until the LLM pass runs — `ANTHROPIC_API_KEY` isn't set in
this environment).

Verified by transforming vanilla `comment_strings.xml` offline: 326 string ids in, 326 out, the
12 targets carrying both the new text and their original tag. New `LiegeTitleOverrideTests`
(4 tests) guard the texts, the `node()` copy, the absence of Calradian names, and the loc-key
registration; suite 4488 green.

Two adjacent defects found and filed on #363, not fixed here: the 23 pre-existing overrides in
the same file drop their `<tags>` child the same way (nobles fall through to whichever variation
loads first), and TAOM's 16 LOTR kingdoms have no `str_liege_title` variation at all, so their
rulers say *"I am Thranduil, ."*

## 2026-07-29

### docs(gondor): Lond Cirion wall kit catalogued

New [`docs/kitbash/lond-cirion-walls.md`](docs/kitbash/lond-cirion-walls.md) consolidates the
kit's durable knowledge out of session memory into the repo: the 8-section table, measured
registration facts (L3 deck z=15, tower door geometry, the +5 corner raise, interior rotations,
material dedup, tuck/kink math), the three composition laws (chirality, refill, tri-sum
verification), the deliberate ring state (junction-only towers, open siege frontage), and the
add-a-section workflow. Kitbash README gains the kit row; tools/README's assembler row rewritten;
the pipeline doc gains the third acquisition path ("kit-composition — new pieces from measured
existing parts"). Next (approved): a Gondor **building parts catalog** + reverse-engineering one
shipped building toward composing new buildings the same way.

## 2026-07-28

### feat(gondor): Lond Cirion wall kit started — ploppable L-section with towers

First section of a city-wall kit for **Lond Cirion** (new coastal Gondor city concept), following
the `minas_tirith_wall_l0.fbx` template format: one kit FBX holding, per section, base mesh +
`.lod3` + `.lod6` + `bo_` collision twin, pivoted at the plop anchor.
`blockout/lond_cirion_wall_a.fbx` ships `lond_cirion_wall_01` — a symmetric L (~88 × 85 m):
6 × `gondor_castle_wall_20m_L1_A` modules and 5 towers (`gondor_castle_wall_tower_L1_A`), arms
of wall–wall–tower–wall–tower butt-joined in-line, corner tower rotated 45° so its machicolated
outer face bisects both outward directions (wall ends overlap into its footprint — standard kit
corner). Tiers: 122k / 50k / 16k tris + 7k bo. Assembled programmatically by
`tools/oneoff/blender_assemble_lond_cirion_wall.py` from piece bboxes reverse-engineered with the
new `tools/oneoff/blender_dump_fbx_inventory.py`; verified via four-angle Cycles renders
(corner-bisector symmetry, plan view, seam continuity). v1 simplifications: tower wood interiors
+ decal meshes skipped; the bo_ is single-slot `stone` (wood top platforms ride under stone
physics). Owed: Modding Kit import + in-scene snap test; next sections (straight run, gatehouse,
curved) reuse the same assembler.

**Editor-feedback fixes (same day):** (1) importing multiple source FBXs into one Blender scene
made later imports' shared-name materials `.001` duplicates — exported slots the editor can't
bind (rendered white); the assembler now remaps `.NNN` duplicates onto their base-named
materials before joining (re-export verified clean). (2) An interim fix shifted the L1 towers
+4.05 m so their off-axis door met the walkway; superseded the same day by (3).

**Rebuilt on the L3 wall kit (user direction — L1 towers don't sit flush):** section 01 now uses
`Scenes/Gondor/walls/` pieces: `gondor_castle_wall_20m_l3_a` (deck z=15, outer +Y, merlon
add-ons m1–m6) + `gondor_castle_wall_tower_l3_a` (10.8 m square, doors on BOTH ±X faces centred
at y=0 with threshold exactly z=15 — flush in-line pass-through by design) + 
`gondor_castle_wall_tower_l3_b` as the corner (14.2 m square, doors on the ADJACENT +X/−Y faces
— an authored corner tower; its doors sit at z=10, so it places at z=+5, which also aligns its
crown with tower a's). No rotation hacks, no Y-shifts: a player walks arm A's deck through the
corner tower onto arm B's. Tower interiors (floors + spiral stairs) now included — towers are
enterable. **Stairs-vs-doors follow-up:** angular occupancy measured at door height showed the
corner tower's stair flight wrapping its +X door and the diagonal between its two doors; its
interior (floor + stairs + bo) now rotates +90° so the flight sits on the two plain faces —
through-door renders confirm open passage at the corner and clean through-tunnel sightlines in
the in-line towers (whose stairs measured clear as authored and are unchanged).

**Stairwell fall-hazard fix (in-editor catch — "someone entering that door will fall down the
stairs"):** entry-cell measurement proved NO 90° interior rotation can make every door safe —
each interior carries two hazards 90° apart (descending stairwell + climbing flight), so with
doors 180° apart (in-line) or 90° apart (corner) one hazard always lands on a door; these
interiors are authored around one "stair door" per tower. Fix: keep the rotations that park the
FALL (not the chest-height flight) on one door and bridge that door's stairwell with a generated
deck-flush plate (descent spans 2.64–4.52 m in the door lane — holes.json; plate 2.3–4.9 m,
±1.2 m, 2 cm proud to avoid z-fighting, `gondor_tiles_a_dirty_mat`, matching collision in bo_).
Every door now walks flat; the up-flight to the tower top stays usable; only the decorative
below-deck rooms are sealed. Verified by through-door renders at both bridged doors.
*(Superseded same day: the bridge approach was replaced by the user's simpler direction — rotate
each interior so the stairs sit on the window wall, `tower_a` +90° / `tower_b` 180°, no bridges.)*

**Section 01 CONFIRMED WORKING in-editor (user, 2026-07-28)** — materials bind, towers flush,
doors on the walkway, both arms facing outward, stairs on the window side.

**Sections 02–05 added (same day):** the kit FBX now ships five ploppable sections —
**02 gatehouse straight** (78 m: tower–wall–gate–wall–tower around
`gondor_castle_gatehouse_l1_a`, whose deck tops at exactly 15.0 = the L3 wall deck despite the
l1 name; own merlons, no interior), **03 straight run** (50 m wall–tower–wall chaining piece),
**04/05 coastal kinks** (22.5° and 45° bends: walls headed east rotated ±half-angle so outer
faces stay consistent — no chirality trap — with the vertex tower hiding the bend, wall ends
tucked 14.2 m out so their merlon corners stay inside the tower shell). Faceted arcs from
chained kinks + straights replace true curved geometry (the `meshes/gondor_wall_*_curved` bits
are garden-wall scale, unusable at 15 m). The **backwards L needs no section** — the L has
identical arms, so it is achiral: every mirrored variant is an editor rotation of section 01.
Also fixed: previews rendered leftover source pieces z-fighting at the origin (export was always
clean — `use_selection`); per-section previews now isolate the section.

**Section 08 tower thinning + siege frontage (2026-07-29, user direction):** the ring keeps
towers only at **direction changes and junctions** — all ~15 rhythm/mid-run towers removed
(embedded-03 centres, wing and leg mids, closure-leg tower), each thinned run REFILLED with
evenly-pitched walls spanning the same endpoints (a bare removal pass shipped 10 m holes first —
caught in the top-down; slack now spreads as 2–3 m per-joint tucks). The closure's long leg
(~222 m) is **deliberately not built**: it stays open as the siege frontage for the engine's
breachable wall entities, framed by the 05 kink (west) and the north run's end (east). Standalone
sections 01–07 keep their original tower rhythm.

**Section 08 CIRCUIT CLOSED (2026-07-29):** a closure solver connects the two open ends — it
intersects the headland end-line (heading −22.5°) with the north run's approach line (−67.5°;
the difference is exactly one 45° kink), places the 05 kink at the vertex, and fills both legs
with walls + rhythm towers, spreading each leg's length remainder as extra tuck at every joint
(hidden in the piece overlaps — no custom pieces needed). Full top-down render verifies one
continuous, seamless ring: **the complete Lond Cirion wall circuit in a single plop**, 3.22M
tris base with LOD tiers. The east-side north run also gained the walker's **flip mode** the
same day (outer right-of-travel + left-turning kinks — a chain recipe needs sequence, heading,
AND chirality).

**Section 08 east-shore run (same day, user recipe "3, 4, 3, 3" from the east wing's end):**
the chain walker (now a reusable function; its first nested-closure version hit the classic
`+=`-rebinding UnboundLocalError — `.extend()` fixes it) continues east from the wing at
heading 0°, kinking 22.5° south-east after the first straight. Section 08: **741 × 519 m,
2.64M tris base** (exact embedded sum). Editor-staged bridge/ramp pieces at the run's end were
deliberately excluded per the user.

**Section 08 headland run (same day, user placement "4, 3, 3, 3, 3, 5"):** the chain continues
from the second sweep (heading 45°) through a generic **chain walker** in the assembler —
cursor + heading, kinks turn the heading right by their bend, every joint 0.1 m tucked — adding
a 22.5° kink, four 03 straights, and a 45° kink exiting heading −22.5° southeast. Section 08:
**553 × 476 m, 2.29M tris base** (exact sum of embedded pieces), full top-down render verifies
the continuous bay-hugging circuit.

**Section 08 north-coast extension (same day, user placement "3, 3, 7" beyond the sweep):** the
chain now continues from the first sweep's far end at heading 112.5° — two embedded 03 straights
up the shore, then a second sweep attached by its west end (same curl handedness) bending around
to heading 45° northeast. All joints keep the 0.1 m tuck, chain cursor computed symbolically.
Section 08 final: **553 × 361 m**, tiers 1.76M / 662k / 306k + 172k bo — the entire west-harbor
waterfront from the gate court to the northern headland in one plop.

**Section 08 — the full harbor front (same day; final recipe from the user: west of the court's
wing, "03 and then 07"):** gate court + an embedded full section 03 straight run + the coastal
sweep attached by its east end — `Rz(213.75°)` lands the sweep's last segment collinear with
the run AND flips its outer face south to match the wings; both joints carry the kit-standard
0.1 m tuck, transforms computed symbolically. 350 × 103 m, one plop for the entire waterfront;
tiers 1.27M / 486k / 234k + 126k bo. (First cut used a bare 20 m wall instead of the 03 — too
short; the 08 number is reused from the deleted sea-anchor.)

**Section 07 — coastal sweep (same day):** ~173 m arc: four 2-wall runs through three 22.5°
kink towers, 67.5° total curvature, merlons on the convex side — one plop covers the harbor
beach arc; chain with 03/04/05 to follow any shoreline. (Sections 08 sea-anchor and 09
generated-ramp were built the same day and **deleted at the user's direction** — not needed;
their code removed, recoverable from git history. Ray-cast finding kept for the record: the
kit's `gd_ramp_large_a1` is a dual-lane switchback to a 20 m platform, wrong rise for the 15 m
deck.) Kit FBX: 28 meshes, 7 sections.

**Section 06 — gate front (same day; v3 = the user's TRACED shape, a recessed gate court):**
wings run along the waterfront line (outer south) and turn north at corner towers (±63 m); a
leg (the section-01 arm rhythm ending in a tower) runs 86 m back from each corner; the gate
face — filler wall · tower · wall · GATE · wall · tower · filler wall — spans between the two
leg end-towers, set back from the waterfront over a walled court. Filler-wall decks dead-end on
the leg towers' plain faces (established pattern); corner doors serve wing+leg (left Rz180,
right Rz90). 302 × 95 m, tiers 870k / 334k / 159k + 86k bo. (v1 corners-outboard and v2
corners-inboard-collinear were both wrong readings of the arrangement — the user's red/green
trace + ground-level shot settled the true Z-stepped shape.)

**Arm-B facing fix (in-editor catch):** a fixed-handed wall piece cannot serve both arms of an
L by rotating with the arm direction — `Rz(−90)@T(s,0,0)` ran arm B south but pointed its merlon
face into the city (one arm read backwards vs the other). Correct transform: orient the piece
`Rz(+90)` (outer −X, consistent with arm A's +Y) and translate south in world space,
`T(0,−s)@Rz(90)`. Verified from the convex exterior: both arms now present identical
machicolated outer faces. Tiers: 338k / 130k / 63k tris + 34k bo (up from v1's 122k — interiors + merlons;
proper per-part LOD tiers ship). Verified: plan-view door tunnels + deck-level corner arches in
renders; 14 base-named material slots, no `.NNN`.

### feat(gondor): three harbor ships from Tripo AI models (1.9M→40k tris each)

Converted three Tripo ship FBXs into Gondor harbor props: `sm_gondor_ship_cog_001` (20 m),
`sm_gondor_ship_longship_001` (24 m), `sm_gondor_ship_war_001` (30 m, swan figurehead) — each
40k tris (from ~1.9M source) + `bo_` twin at 3k with `stone` physics, under
`AssetSources/Scenes/Gondor/ships/`, with `t_gondor_ship_<name>_{d,n,s}` at 2048² in
`Scenes/Gondor/ships/textures/`. The throne script was renamed+generalized to
`tools/oneoff/blender_prep_tripo_prop.py`: map auto-discovery from the `.fbm` dir,
`--scale-mode length` (longest horizontal extent rotated to +X — one ship was length-along-Y),
`--decimate-tris` with the UV layer stripped pre-collapse and the full-res duplicate kept as
bake source (true high-to-low bake, cage auto-scaled `max(0.02, 0.005×max_dim)`). Chart re-UV
at spread 75°: 108–171 islands per ship; fold-over 3.5–9.2%, concentrated in rigging/chain
cylinders — all three Cycles preview renders clean, which is the deciding check. Still owed:
Modding Kit import (textures → FBX → editor materials `t_gondor_ship_*`), in-editor
normal-direction check (`--flip-green` re-run if inverted), harbor scene placement.

## 2026-07-27

### fix(recruitment): every Gondor troop was already reachable — but one pool summed to 120%, and nothing checked

Audited `troops_gondor.xml` against the volunteer service to confirm every troop is obtainable in some
region, counting upgrade paths. **It is.** All **189** Gondor troops resolve: 181 reachable from a
`gondor.json` pool root through the upgrade closure, 8 intentionally excluded (4 settlement militia, 1
hideout boss, 3 tavern `*_merc`, matching `IsIntentionallyUnrecruited`). Zero orphans globally and zero
in each of the 26 regional id-prefixes. All 25 `is_basic_troop` Soldier roots are pooled, and all **93**
live `TAOM_Map` EW settlements are covered exactly once with no dead keys. Engine check: `MaxVolunteerTier
=> 6` does *not* clamp `GetBasicVolunteer` — per `RecruitmentCampaignBehavior:228,241` it only gates the
in-slot upgrade drift — so the level-51 Ithilien Ranger four pools list really does appear.

The audit turned up three defects behind that clean result.

**One group in 24 summed to 120%.** "Bar Melui" carried four Lossarnach regulars at 25 plus a 20% noble.
`PickWeighted` normalises cumulatively, so it never crashed — it just quietly delivered 20.8%/16.7%
instead of the design. **Rebalanced the whole culture rather than patching the one group:** the noble /
settlement-specific line now takes 20% and the regular line 80%, replacing the retired 60/40 split, across
**15 of the 24 groups** (the 9 single-line groups are untouched). The three Anórien capitals run 70/20/10,
the Ithilien Ranger holding its 10%. Dol Amroth was standardised rather than exempted — it inverted the
rule at 90/10, so **Swan Knights go from the dominant roll at `town_EW5` to a 1-in-5 chance**, the largest
gameplay change here. No troop id or settlement key was added or removed, so reachability is untouched.

**Nothing validated troop ids inside `gondor.json`.** The existing typo test reads only the C# maps — in the
test bin `AllPooledTroopIds()` never contains a JSON id — and the reachability guard drops unknown ids
through an `if (nodes.Contains(...))` filter. A misspelled id therefore passed every check while resolving
to null in-game and silently voiding its weight share: exactly
`rca-rhun-gondor-recruitment-2026-05-23.md` (`wain_cavalry` vs `wainrider_cavalry`), whose "add a
script-level check" follow-up was never built. Two tests now close it — one collecting JSON ids
**unfiltered**, one asserting every group totals 100 and no settlement is listed twice. Both were verified
by injecting the failure: the typo gate named `gondor_lam_swordsman`, the totals gate named all four Bar
Melui settlements at 120%.

**The C# fallback had drifted from the JSON it shadows.** `gondor.json` overwrites `SettlementMap` at
runtime, so `VolunteerRecruitmentService.Gondor.cs` is live only in degraded mode — and in the tests, which
means the suite was asserting behaviour the game never exhibits. The C#-only path stranded the entire
7-troop Ithil Guard line and pooled three ids (`anf_guardsman`, `mt_fountain_guard`, `ser_pikeman`) the JSON
never offered, while `castle_EW10` handed out Harondor troops where the JSON says Belfalas. All 27
towns/castles now mirror the JSON as smallest-integer ratios, plus the `town_ES2` Ithil Guard conditional.
A drift test compares normalised shares per settlement so the two can't silently separate again; it was
likewise proven by perturbing a weight. Villages stay unmirrored by design and inherit through
`BoundSettlementId`.

Suite green at **4482** passed / 0 failed.

Save-compat: weights only — no troop ids added, removed, or renamed.
Not-tested: in-game volunteer distribution (needs a new campaign; volunteer slots are `[SaveableProperty]`).

### chore(logging): a 4-hour session wrote 6.4 MB — cut it to ~1.2 MB, and fixed two bugs it was hiding

A crash-free 4h12m session produced a **47,365-line** `taom_debug_*.log`. The per-tick tracing added
during the #331/#339/#360 investigations has done its job, and it isn't free: `IModLogger.LogFilePath`
bundles this file into every crash ZIP, so the noise degrades the *next* real triage. Every line was
classified; four buckets account for the whole file.

**95.2% of it (45,080 lines) was one CultureMarketplace line whose gate had been inert since it
shipped.** `0abe1854` (2026-07-04) already fixed this once, replacing an unconditional per-tick log
with `if (added > 0 || topUp > 0 || removed > 0)`. The `removed > 0` term defeats it: `removed` counts
foreign items stripped from a town market, which is steady-state housekeeping, not an event — vanilla
restocks cross-cultural goods daily and the filter strips them again, forever. 44,553 of the 45,080
lines (98.8%) had a non-zero foreign count, and **37,512 (83.2%) were emitted for that term alone,
with nothing injected.** The gate now reads `added > 0 || topUp > 0`; the foreign count still prints
on every surviving line, so no visibility is lost. **45,080 → 7,568 lines.**

That the first trim under-delivered was invisible because it was verified against *pre-fix* data —
its own trailer says `Not-tested: log volume itself (verified via frequency analysis of the live 21MB
session log, not a unit test)`. **A volume gate validated against the log that motivated it is not
validated.** Checked before suppressing: removals per town per day held flat across the session
(3.54 → 3.59 → 3.63 → 3.57) and roster counts plateaued rather than diverging, so this is equilibrium
being narrated, not a runaway being hidden.

**CultureConversion** (1,697 → 565): the `already pending — timer continues` DEBUG existed to prove
the hold clock wasn't restarting across a **45**-day hold; the shipped default is now **1 day**, so
there is no clock left to protect — deleted (its guard and early-return stay). `queued for conversion`
now logs only when `requiredHoldDays > 1`; at the default it duplicates the `converted`/`restored`
INFO one campaign day later. Both terminal lines stay at INFO — they are real campaign events.

**Startup** (~350 → ~218): the 38 per-pair `Establishing initial alliance` lines (the summary below
them already reports created/already-allied/silent-noop, and failures are named individually by a
warning); the 49 `Parsed career` lines and one of the two identical load summaries; the 15 per-race
`Race ID N` lines folded into one joined line — the id→name *order* is engine-supplied and shifts
between builds, so it is kept, just not at 15 lines; the 3-line-per-menu character-creation narrative
logs collapsed to one line each; the `[Diplomacy] EndAlliance blocked` DEBUG twin, a guaranteed 1:1
duplicate of the INFO immediately above it; and the `[NativeSkinFixes] parked` line.

**Log retention.** Nothing ever pruned `Logs/` — debug logs accumulated for the life of the install.
`FileLogger` now keeps the 10 most recent `taom_debug_*.log` (constructor-parameterised, `<= 0`
disables). The prune runs before the new file is opened, matches `taom_debug_*.log` only — `Logs/` is
shared with crash bundles, the battle-load stall marker and shader sentinels — and is fail-safe at
both the pass and per-file level, because a logger that throws from its constructor takes startup with
it.

**Deliberately untouched:** no log-level filter. In this codebase level encodes *crash-durability*,
not importance — INFO/WARNING/ERROR flush synchronously and survive a native AV, DEBUG is async and
lost — and 92% of the 1,331 call sites are on the durable path. `[BattleLoad]` is unchanged:
`tools/triage_battle_load.py` parses its exact format and the tournament-exit hang is still
cause-unknown. `[MissionDiag]`'s 30 one-shot lines are the best evidence in the file for triaging
third-party-mod and version-drift crashes.

Projected: **47,365 → ~8,600 lines, 6.4 MB → ~1.2 MB.**

#### fix(race-age): heroes were announced dead a day before they died

222 distinct heroes produced 238 `died of old age` lines — 16 announced **twice**, one campaign day
apart, at the same age. The behavior logged *before* the kill, and `KillByOldAge` returned `void`, so
it could not report that nothing had happened. `KillCharacterAction.ApplyInternal` marks-and-defers
when the victim is in a `MapEvent`/`SiegeEvent` and returns without changing `HeroState`, so the hero
stayed alive, still matched the age check next tick, and was announced again. The engine's guard is
`&& victim.DeathMark == KillCharacterActionDetail.None`, so the second call always lands — which is
why every duplicate appeared exactly twice and never three times.

`IHeroAgeAdapter.KillByOldAge` now returns `bool` (re-reading `IsAlive`, a plain `HeroState`
comparison, after the action) and the behavior kills first and announces only on a confirmed death.
This is the correctness fix, not just a dedupe: the mod stops reporting deaths that did not happen —
the same deferral path also covers the player character and a disabled life/death cycle. +4 tests.

#### fix(race-age): validation was granting Sauron a human fertility window

`RaceAgeConfigProvider` read the authored `"fertilityEnd": 0` on `nazghul` / `saruman` / `sauron` as an
inverted range and **overwrote the pair with 18/45**, warning three times on every session start.
Those three are the only entries with a zero fertility window and all three carry `"immortal": true` —
the value is the deliberate "cannot reproduce" sentinel. The ordering check now skips immortal races.
Masked downstream by `TaomPregnancyModel`'s `IsImmortal` short-circuit, but `GetFertilityEndAge` is
public on `IRaceAgeService`, so any future consumer reading it without re-checking `IsImmortal` would
have seen 45. The pre-existing test used the exact shipped `nazghul` shape but asserted only `Immortal`
and `FertilityMod` — never `FertilityEnd` — which is why the overwrite went unseen. +3 tests.

Suite 4,474 green (was 4,462). **Not-tested:** the log volume itself — the projections are frequency
analysis of the live session log, and the real oracle is a comparable session re-measured in-game.

Constraint: `IModLogger` exposes no `IsDebugEnabled`, so gating stays at the call sites.
Save-compat: none — logging, validation and one adapter return type; no persisted state.

## 2026-07-26

### feat(ui): game-menu hyperlinks coloured by faction (Patch64) (#362)

Entering a town, the settlement, its lord and its kingdom rendered in vanilla's link colours —
`#aa7449` tan, `#76A5B5` pale blue, `#878CAB` grey-blue — chosen for Bannerlord's dark menu panel.
Against TAOM's parchment they scored under 3:1 and read as washed-out noise, and every settlement
looked the same regardless of who held it.

Links are now coloured by the culture of the object they point at: 20 cultures, each with a
hand-authored style in `GameMenu.InfoText`. Minas Morgul reads oxblood; after Gondor takes it the
place name stays Mordor-coloured while the new governor and his realm read steel-blue.

The colour could not come from data alone. `HyperlinkTexts` hardcodes one style name per link
*type*, and the rich-text parser accepts only a named style — there is no inline colour attribute
(`RichText.cs:486-516`), so `CultureObject.Color` cannot be read at runtime and injected. Patch64
therefore rewrites the style name in a prefix on `GameMenuVM.set_ContextText`, resolving each
link's href back through `MBObjectManager` the way `EncyclopediaManager.GoToLink` does. The seam
matters: `GetMenuText` returns the same cached `TextObject` reference every call and
`IsMenuTextChanged` compares by reference every frame, so a postfix there would rebuild the menu
text at frame rate. Patching `HyperlinkTexts` itself was rejected as process-global —
`GetStyleOrDefault` falls back silently, so a leaked style name would turn every hyperlink in the
game into plain body text with nothing in the log.

Palette colours are pinned to relative luminance 0.035–0.10: dark enough to read on the parchment,
light enough to stay distinct from the black body text. Hover and press darken rather than
brighten, the opposite of vanilla's convention, because the background is light. The coverage test
recomputes luminance from the shipped XML and fails the build on an out-of-window colour, and also
pins every emittable style name to the brush file so the two cannot drift.

Bandit cultures, other mods' cultures, unresolvable objects and the faction-less link types
(concept, unit, ship, generic) keep their vanilla style names — those styles are separately
retinted for the parchment in the same brush, so nothing falls back to unreadable.

Guard for the one hazard no offline test can see: a module later in load order replaces
`GUI/Brushes/GameMenu.xml` wholesale (`ResourceDepot` keys by path, last wins), and
`Modules/DOTS/GUI/Brushes/GameMenu.xml` is byte-identical to TAOM's. Before emitting a style the
rewriter asks the live brush whether it exists, falls back to vanilla if not, and logs once naming
the cause. TAOM must load after DOTS.

Deep-review found two real defects before commit, both fixed. The rewriter memoised its last
(input, output) pair, but the key was the menu **string** while the answer depends on the linked
objects' **culture** — identical text before and after a culture conversion, or across a load of a
different save, would have returned a stale colour silently. The memo was deleted rather than
invalidated: it guarded a path that runs once per menu open. Separately, the 21 retinted vanilla
`Link.*` fallback styles kept vanilla's dark `#111111FF` glow, because a style redefining an
*inherited* name does not regain the brush `Default` — `Style.FillFrom` assigns through property
setters that latch the changed-flag at clone time. Every style now states its glow explicitly, and
a test enforces it.

Suite 4462 green (+32). Docs: `docs/features/menu-link-colors.md`, Patch64 registry entry,
RCA `docs/reviews/rca-menu-link-colors-2026-07-26.md`.

Not-tested: brush rendering, hover/press states, and that the shipped brush file is the one
GauntletUI actually loaded — all require the running game.

### fix(cultures): town taverns sold Calradian mercenaries — culture-specific hire pools

Every one of the 14 town-owning cultures shipped vanilla's `<basic_mercenary_troops>` list verbatim
(`eastern_mercenary` / `western_mercenary` / `sword_sisters_sister_t3`, all `Culture.neutral_culture`),
so Minas Morgul's backstreets offered "Hired Pike" — `western_mercenary_t4`, reached because
`RecruitmentCampaignBehavior.UpdateCurrentMercenaryTroopAndCount` randomly walks the drawn troop's
`UpgradeTargets` after picking a root. The 30% `caravan_guard` branch was already correct per culture,
which is why the tavern looked right roughly 3 days in 10.

Each culture now hires its own. Added 21 `<source>_merc` troops — dedicated `occupation="Mercenary"`
copies of that culture's **rarest** recruitment-pool entries (lowest `VolunteerChance` weight in
`Main/Features/TroopProgression/RecruitmentPools/`), so the tavern sells the specialists notables
rarely offer: Mordor hires Black Uruk Grunts, Orc Impalers, Orc Hunters and Nurn Warg Tamers;
Isengard hires Uruk-Hai Warriors, Scouts and Orthanc Chosen; Umbar hires Adûnaim. Copies carry the
source's skills and equipment, and the copies exist precisely so the **originals stay `Soldier`** —
occupation drives ×2 recruitment cost and ×1.5 wage through `TroopCostService`, which would otherwise
have repriced those troops in notable recruitment and in every AI party fielding them. The copies are
leaves on purpose: an `<upgrade_targets>` entry would let the engine's walk drift the offer back onto
a normal line troop.

New `TavernMercenaryDataTests` pins all four invariants (TAOM-defined ids, Mercenary occupation, leaf
copies, sources are their pool's rarest). `VolunteerRecruitmentServiceTests`' reachability guard gained
a `*_merc` exemption alongside `*_militia_*` and `*_boss` — mercenaries are bought for gold, not
volunteered. Generator: `tools/oneoff/generate_tavern_mercenaries.py` (idempotent). Save-safe — new ids
are additive, though an existing save keeps its stored Calradian troop until that town's next reroll.
The new `{=aom_merc_*_name}` keys ship English-only, matching every other troop name in the mod.

### fix(armory): every mace head shipped a 0-damage thrust attack — `excluded_item_usage_features` pass

All 20 blade pieces in the Armory's `Mace` weapon description lacked
`excluded_item_usage_features="thrust"`, so the composed animation set was
`onehanded_block_shield_tipdraw_swing_thrust` — a set **with** thrust attacks — while their
`BladeData` declares only `<Swing>`, leaving `ThrustDamageType = DamageTypes.Invalid` and the factor
at 0 (`BladeData.cs:39` → `Crafting.cs:135`/`216`). Vanilla tags 30/30 of its own mace heads; TAOM
tagged 0/20. Affected 19 shipped `<CraftedItem>`s carried by ~60 troop entries across Mordor,
Isengard, Gundabad, Goblin, Misty Mountain Orcs, Dol Guldur and Rhûn, plus anything smithed from
those heads. Also removed a vestigial `<Thrust>` (Pierce 1.76) from
`wm_isengard_berserker_sword_a01_blade`, which already excluded `thrust` — vanilla ships zero blades
in that contradictory state, and the stat was advertising an attack the animation set cannot perform.
**Deliberately unchanged for vanilla parity:** the 10 fully-inert exclusions (vanilla ships 17 of its
own 93 — `mace_head_31`–`39` tag `thrust` while appearing only in `TwoHandedMace`) and the `widegrip`
spread, which picks a staff-vs-2H-sword animation family rather than a capability. Verified: 21
pieces changed and nothing else, 681 pieces before and after, byte integrity preserved (no BOM, LF,
non-ASCII runs identical); swing-only heads missing the exclusion 20 → 0, exclusion-vs-damage
contradictions 1 → 0, and a **cross-slot union audit** (exclusions are unioned across every piece in
a weapon, not applied per piece) enumerating 47 reachable combinations → 27 names → 0 unresolvable.
Save-safe: crafted items recompose from their piece list each load (`ItemObject.cs:469`), ids
unchanged. Docs: new reference [`docs/reference/item-usage-features.md`](docs/reference/item-usage-features.md)
(mechanism, token table, the per-family vanilla rule, the union-audit method), the swing-only axe-head
example in `weapon-creation-workflow.md` now carries a do-not-copy-to-a-mace-head callout,
`weapon-xml-pipeline.md` documents that the attribute passes through from the manifest but is never
inferred, RCA [`rca-crafting-usage-features-2026-07-26.md`](docs/reviews/rca-crafting-usage-features-2026-07-26.md),
lessons appended to `xslt-moduledata.md` + `build-tooling-workflow.md`, and the LESSONS-LEARNED
per-category counts re-synced (they had drifted 13 low; 242 actual). Lives in the untracked
`LOTRLOME_Armory` module, so this entry and the RCA are the durable record (data-fix precedent #213).
Owed: GitHub issue, and an in-game smoke — craft a Gundabad/Mordor mace and confirm the thrust
attack is gone.

## 2026-07-25

### fix(banner-bearers): siege CTD on reinforcement bearer spawn — 1H sidearm invariant + Patch63 guard (#360)

Crash bundle `67b75cb4` (defense of Glad Thaw, ~6 min in): `AccessViolationException` in
`Agent.GetWeaponEntityFromEquipmentSlot(ExtraWeaponSlot)` from the engine's
`BannerBearerLogic.SpawnBannerBearer` on the **reinforcement** path — a code path deployment
never runs (hence the fuse) that reads the new bearer's native slot-4 weapon entity with no
check. Likely trigger (guard-instrumented): Mirkwood/Isengard shipped **two-handed polearms**
as `banner_bearer_replacement_weapons` while every vanilla culture ships 1H swords and the
model applies no class filter — the `DropOnWeaponChange` banner plausibly drops during the
native 2H wield, emptying the slot. Three layers: **data** (mirkwood/mirkwood_stalkers keep
`mirkwood_sword_a01` only; isengard pikes → `isengard_1h_sword_a`/`isengard_berserker_sword`),
**test** (`BannerBearerReplacementWeaponDataTests` pins the 1H invariant against the installed
Armory — failed on all 5 polearm entries pre-fix), **code** (`Patch63_BannerBearerSpawnGuard`:
guarded prefix-replacement — toggle-folded eligibility gate that also closes the
troll-as-reinforcement-bearer gap, managed slot-4 check that logs the mechanism instead of
crashing, AV-only catch; fail-open on binding drift, pinned by `BannerBearersBindingTests`).
Deep-review caught the first-cut gate suppressing vanilla-armed formations' bearers with the
feature off (the 2026-07-16 regression class, now in prefix form) — fixed via
`IsReinforcementBearerAllowed` folding the master toggle in the service. Suite 4426 green.
RCA: `docs/reviews/rca-banner-bearers-reinforcement-av-2026-07-25.md`. Owed: in-game siege
smoke (Glad Thaw save past a reinforcement wave; any Patch63 ANOMALY log line confirms the
drop mechanism in the wild).

### feat(mordor): Witch-king throne prop for the Minas Morgul throne room

Converted a Tripo AI-generated throne FBX into the Mordor kit: `sm_mordor_mm_throne_001.fbx`
(2.5 m, 42.7k tris, `bo_` twin at 1.5k with `stone` physics) + `t_mordor_mm_throne_{d,n,s}` at
2048² in `AssetSources/Scenes/Mordor/`. The Tripo auto-UV atlas (298 fragmented islands) was
replaced with an **xatlas-style chart unwrap** — region-growing over face connectivity + small-
fragment merge, planar projection, texel-density equalisation — landing at **128 islands / 57%
UV utilisation / 1.4% fold-over** (probe showed Blender's Smart UV Project is unusable on dense
organic triangulation: 1,485–2,112 islands at 17–24%). All maps rebaked selected-to-active onto
the new layout from the Tripo originals, plus a fresh geometry AO bake (Tripo ships none);
Cycles preview render verified before conversion. New scripts `blender_prep_witchking_throne.py`
(headless, probe modes) + `convert_tripo_prop_textures.py` (single-set d/n/s packer; doubles as
the Substance Painter round-trip converter — workflow in its docstring). Still owed: Modding Kit
import (textures → FBX → editor material `t_mordor_mm_throne`), in-editor normal-direction check
(`--flip-green` re-run if relief reads inverted), scene placement.

### fix(gondor): defer the new poleaxe — crafted weapons hard-crash on uncompiled meshes

The `wm_gondor_poleaxe_a`/`_b` items crashed new-campaign load (`ItemObject.Deserialize` NRE on the
CraftedItem, confirmed via debugger — `this = {wm_gondor_poleaxe_a}`). Root cause: a crafted **weapon**
assembles its combined mesh + physics **at item-deserialization**, so its `sm_ar_art_poleaxe_*` piece
meshes being source-only (not yet in the compiled `AssetPackages/`) is a hard crash — not the
invisible-render that armor gets on a missing mesh. The XML was complete (weapon def + crafting pieces
+ vanilla `TwoHandedPolearm` template); this is purely a mesh-compile ordering issue. Removed the 2
items + 3 crafting pieces from the external Armory and reverted Osgiliath Guard / Dome Guard to their
vanilla pikes. **Re-add after the AssetPackages recompile.** Everything else this session (existing
compiled items) is unaffected.

### feat(gondor): region-specific shields across every troop line + lord equipment sets

Standardised Gondor shields by fief so units read at a glance. Rules: Anórien + all **unlisted** regions
use the greyscale team-coloured `wm_gondor_shield_a02`; Pinnath Gelin (incl. Arndir) `gond_shield_three_green`;
Dol Amroth the swan `gond_shield_two_swan`; Cair Andros `wm_gondor_shield_a_cair_andros`; Minas Ithil
`wm_gondor_shield_a_minas_ithil` (Watcher) / `wm_gondor_shield_d_new_minas_ithil` (Vet/Sgt/Capt); Ringló Vale
`gond_shield_four_mustard`; Belfalas + Anfalas (incl. Serelond, Lond-Galen) the generic a02. Unlisted regions
(Lossarnach, Pelargir, Minas Tirith, Osgiliath, Calembel) were forced to a02 per decision, dropping their old
thematic shields. **40 troops** swapped; the rest already matched. **Lord equipment sets follow the same
rules:** Dol Amroth templates → swan, Arndir → green, the named fief-lords by fief (Imrahil swan, Hirluin
green, Forlong/Angbor/Golasgil → a02); Boromir keeps his unique shield. Mesh-id → item-id resolved from the
shield defs (several differ). Equipment-only, save-safe (187 NPCCharacters unchanged); `validate_moduledata`
+ `validate_gondor_refs` PASS.

### feat(gondor): weapon standardisation pass + new craftable poleaxe

Standardised Gondor melee weapons by rule:
- **Spears:** foot-spears (generic `wm_gondor_spear` + vanilla `eastern`/`imperial`) → `wm_gondor_spear_b`
  (36 refs); Fountain + Citadel Guard get the higher-blade `wm_gondor_spear_a`, replacing their vanilla pikes.
  Pelagir spears, banner-bearer spears, and Swan Knight lances left intact; other pikes stay vanilla.
- **Swan Knight swords:** gold `wm_swan_knight_sworda` on the Swan Knight capstone + Imrahil (Dol Amroth lord);
  silver `swordb` on the lower swan-sword troops.
- **Belfalas:** the melee infantry that lacked a spear (recruit/footman/soldier) each gained a `wm_gondor_spear_b`.
- **Poleaxe (new):** authored 3 crafting pieces (`sm_ar_art_poleaxe_blade_a`/`_b`/`_handle_a`) + 2 items
  `wm_gondor_poleaxe_a`/`_b` — a **swinging** `TwoHandedPolearm` (Cut swing + Pierce thrust), stats mirrored
  from `dale_poleaxe`. Placed on Osgiliath Guard (`_a`) + Dome Guard (`_b`), replacing their pikes. Pieces +
  items live in the external `LOTRLOME_Armory` (not git-tracked, per the item-def convention) and need the
  `AssetPackages/` recompile to render; blade/handle lengths are approximated (tune reach in-game).

1H swords a01–a10 were already all in use (left as-is). Save-safe; `validate_moduledata` PASS (registry +2
items — the poleaxes resolve).

### fix(gondor): non-Harondor low troops wear Anórien helmets, not the Harondor light helm

Seven non-Harondor troops (L6 recruits/peasants/levy/volunteer/lumberman + the L11 `gondor_militia_archer`)
wore `sk_gd_har_inf_helmet_light_a`, which didn't read right on non-Harondor units. Swapped to the lightest
Anórien infantry helmet `sk_gd_ano_inf_helmet_med_a` (Anórien has no light tier); the 9 Harondor troops keep
their own helmets. Equipment-only, save-safe; validators PASS.

## 2026-07-24

### fix(gondor): Anórien pool — home 2 idle capstone pieces (Osgiliath bracer, Minas Ithil helmet)

Two clean fixes from the Anórien-complex armour audit (the `ano_`/`osg_`/`ith_` pool: 72 items, 27
native troops, 15 idle). Each homes a region's *own* idle piece onto a capstone that was under-equipped:
- **`sk_gd_osg_bracer_noble_elite_a` (27)** → Osgiliath **Guard** (T6, was `bracer_noble_med_a` 15) +
  **Dome Guard** (T7 capstone, was `heavy_a` 21). Picked so `med_a` (archer+infantry) and `heavy_a`
  (longbowman) both stay in use — no new idle; Longbowman is deliberately kept on `heavy_a` to preserve it.
- **`sk_gd_ith_noble_helmet_heavy_b`** → Minas Ithil **Captain** + **Sharpshooter** (T8), replacing
  equal-armour `ano_`/`osg_` fallbacks. All three top Ithil troops (Captain, Sharpshooter, Moon Guard)
  now wear Ithil's **own** helmet — the "Ithil uses its own armour" decision.

Anórien idle **15 → 13**. The remaining 13 are reserved/deferred by decision: the 7 plain
`ano_pauld_noble_*` + 3 plain `osg_pauld_inf_*` stay idle because the Ithil/Osgiliath lines wear the cape
variants (kept — Ithil keeps its own look); the 2 `ano_pauld_fount_*` are the Minas Tirith Fountain line's;
`osg_inf_chest_elite_a` is the osg lord chest (General Note 6). The Anórien Regular chest ladder was left
as-is (deliberate "regulars stay softer than nobles"). Save-safe (4 single-slot swaps, 187 NPCCharacters
unchanged); `validate_gondor_refs` + `validate_moduledata` PASS.

**Minas Tirith Fountain Guard → its own elite chest.** Audited the `sk_gd_mns_*` set (Minas Tirith's own
armour — 11 pieces, helmet + chest only; cape/gloves/leg have no `mns_` model so they fall back to `ano_`,
not `osg_` — no `gondor_mt_` troop wears any `osg_` piece). The Fountain Guard (single L46 capstone) wore
the *heavy* fount chest while its own **elite** chest `sk_gd_mns_fount_chest_elite_a` sat idle → swapped it
onto the Guard (its "one true variant"). Per the decision, the Fountain/Citadel Guard keep their own `mns_`
armour and the reserved osg lord chest was NOT used on them. The two idle masked-`_b` helmets
(`mns_fount_helmet_heavy_b`, `mns_noble_helmet_heavy_b`) were then homed as helmet `_a`/`_b` variant rosters
on the Fountain Guard + Veteran — so the `mns_` set is **10/11 used**, the lone idle piece being
`mns_fount_chest_heavy_a` (surplus; the single-unit Fountain line has no lower tier to wear it). Save-safe;
`validate_gondor_refs` + `validate_moduledata` PASS.

### feat(gondor): equip the Linhir spear line with its own armour — last greenfield line (#358)

The 5-troop Linhir line (`gondor_lin_noble` T3 → `gondor_lin_high_guard` T7, a spear/shield line)
fought in generic Anórien `sk_gd_ano_*`. Refit to its own `sk_gd_lin_*` set per the artist Armor Guide.
Unlike Lond-Galen, Linhir is a **near-complete self-modelled set** (22 items — helmet/chest/pauldron/
bracer/greave all native); the **only** fallback is the medium-helmet slot on T3/T4, which borrows
Dol-Amroth's `sk_gd_dol_helmet_med_a` (Linhir models no medium helmet — its own lowest head is
`helmet_heavy`). Each troop carries two variant rosters (bracer `_a`/`_b` on T3/T4, helmet `_a`/`_b`
on T5–T7). Weapons and civilian set preserved — armour-only refit.

**The guide is flagged DRAFT.** The mapping follows the draft table exactly and was independently
re-derived from the stat ladder (they agree). Two design calls: (1) the **lord/cape pieces stay off
the troops** per the spec's reservation — `sk_gd_lin_chest_elite_a` ("Linhir Lord Armour"),
`helmet_lord_a/b`, and the two `pauld_cape_noble_*` capes ("Linhir never wears the cape combo") — so
17 of 22 pieces are used and the 5 reserved sit idle by design; (2) the T7 High Guard's greaves cap at
`grvs_heavy` (28) because Linhir has no elite greave — a tier gap the draft leaves as-is.

Save-safe: equipment-only, no troop id / level / weapon / upgrade_target / structure change (187
NPCCharacters unchanged). All 17 placed ids + the `dol_helmet_med_a` fallback verified defined +
mesh-exact (`id == mesh`); `validate_gondor_refs` + `validate_moduledata` PASS. Adversarially verified
across three lenses — spec-parity and resolution/idle confirmed, save-safety confirmed for the five
`gondor_lin_` troops. **Linhir was the last greenfield Gondor noble line — every southern + capital
Gondor noble line is now equipped** (Minas Tirith `gondor_mt_` is a separate optional audit).
Not-tested: in-game render (gated on the `AssetPackages/` recompile, #358); the draft weapon guide
(sword + tower shield at T5–T7) is a separate weapon pass.

### feat(gondor): equip the Lond-Galen crossbow line with its own armour (#358)

The 5-troop Lond-Galen line (`gondor_lg_noble` T4 → `gondor_lg_haven_guard` T8 — a crossbow / pavise
line) fought entirely in generic Anórien `sk_gd_ano_*` even though its own `sk_gd_lon_*` helmets and
chests shipped in #358. Refit per the artist Armor Guide: Head/Body take the line's own `sk_gd_lon_*`
(helmet med→heavy→elite by tier; chest chainmail→med→heavy), and — per the guide — the light slots
fall back to **Serelond `sk_gd_sere_*`** (its sibling Noble line under Anfalas), **not** Anórien:
pauldron / bracer / greave scale light→elite across the tiers. Every `sere_` fallback piece is already
worn by Serelond troops, so all resolve + are mesh-exact. Each troop carries two variant rosters (the
helmet `_a`/`_b` mesh pair) so all six `lon_` helmets show in formation. Weapons and the civilian set
preserved exactly — armour-only refit.

`sk_gd_lon_chest_lord_a` stays off the troops: it's the line's lord chest (worn by 4 Lond-Galen lord
equipment sets), so the 10-piece `lon_` set is now fully used — **9 on troops, 1 on lords, 0 truly
idle**. The two Anfalas veterans keep their shared `lon_` gear (share, not move — per the decision).

Save-safe: equipment-only, no troop id / level / weapon / upgrade_target / structure change (187
NPCCharacters unchanged). All 19 armour ids verified defined + mesh-exact (`id == mesh`);
`validate_gondor_refs` + `validate_moduledata` PASS. Adversarially verified across three lenses
(spec-parity, save-safety, resolution/idle) — all three confirmed, no refutation. Remaining greenfield
Gondor noble line: **Linhir**. Not-tested: in-game render (gated on the `AssetPackages/` recompile, #358).

### feat(gondor): equip the Dol-Amroth / Swan Knight troop line with its new armour (#358)

The 11-troop Dol-Amroth line (`gondor_da_noble` → `gondor_da_swan_knight` T9 cavalry /
`gondor_da_swan_guard` T8 infantry) still fought in generic Anórien armour even though the
`sk_gd_dol_*` set shipped in #358. Refit every troop to its region armour per the artist's per-tier
guide: cavalry branch in cape-pauldrons, infantry in plain pauldrons, elite chest reserved for the T9
Swan Knight, and the **masked** elite helm (`cav_/inf_helmet_elite_b`) on the pinnacle Swan Knight /
Swan Guard. Weapons (swan lances/spears, numenorean 2H) and mounts preserved. All **17 battle rosters**
across the line converted — several troops carry 2–3 variant rosters, so a first-roster-only pass would
have left the alternates in old armour. Also authored the one modelled-but-un-authored Belfalas boot
(`sk_gd_bel_boots_a`) onto the T1 recruit.

**Applied via a targeted per-roster armour-slot swap, not the applier's whole-file `--apply`** — a
dry-run showed `apply_gondor_troop_revamp.py`'s `EQUIPMENT` dict has drifted from the live file
(**61/118** troops would be rewritten) and its `DELETE_IDS` would remove a troop, so a wholesale
`--apply` is currently unsafe. The 11 Dol-Amroth entries were still added to the dict and the
generator's `DA_INF_*`/`DA_CAV_*` stubs filled, so both tools record the intended loadout (drift-guard).

Save-safe: equipment-only, no troop id / upgrade_target / structure change (187 NPCCharacters
unchanged). All 87 `sk_gd_dol_*` / `sk_gd_bel_*` armour refs verified mesh-exact against the geo-tpac
TOCs; `validate_gondor_refs` PASS (0 missing), `validate_moduledata` PASS. Belfalas otherwise already
matched its Armor Guide; its 3 un-modelled cape pieces stay on an Anórien fallback — flagged for KEYforce.

Not-tested: in-game render — REQUIRES a full restart + battle spawn of the Dol-Amroth line to confirm
each tier is clothed (gated on the `AssetPackages/` recompile, #358).

### feat(gondor): equip the Arndir (Pinnath noble) + Blackroot Vale troop lines (#358)

Two more greenfield Gondor noble lines wired from generic Anórien to their region armour (same #358
follow-up as Dol-Amroth):
- **Arndir** (Pinnath Gelin noble) — 9 `gondor_arn_*` troops (T3 Noble → T8 Hill-Knight cavalry / T7
  Foot-Knight infantry) refit to `sk_gd_pin_noble_*` per the spec Armor Guide (cavalry cape-pauldrons,
  Hill-Knight the elite chest, Anórien noble bracer/greave fallback). Now uses **20 of 21** idle noble
  pieces — only `pin_nob_chest_elite_b` stays idle (spec-marked lord-only).
- **Blackroot Vale** — 7 `gondor_brv_*` archers (Bowman → Shadowbow) refit to `sk_gd_vale_*` (hoods on
  the scouts, plain capes → pauldron+cape on the rangers, `chest_heavy_c` archer-pad on the Shadowbow
  line, Anórien inf bracer/greave fallback). **All 20 vale pieces now used (0 idle).**

Variant rosters spread the a/b elite helms (hill_knight, foot_knight) and the medium cape-pauldron
(vet_archer). Weapons + mounts preserved; hill_knight's mount (which sat mis-placed *outside* its roster)
was restored *inside* both variant rosters. Save-safe — no troop id / structure change (187 NPCCharacters
unchanged); all 57 armour refs mesh-exact; `validate_gondor_refs` + `validate_moduledata` PASS. Remaining
greenfield Gondor noble lines: **Linhir, Lond-Galen**.

Not-tested: in-game render (restart + battle spawn), gated on the `AssetPackages/` recompile (#358).

### feat(gondor): Anfalas — wire the 3 idle armor pieces into variant rosters (#358)

Anfalas had 13 `sk_gd_anf_*` pieces, 10 in use, 3 idle. Homed via variant rosters:
`gondor_anf_cavalry` gains a `cav_helmet_heavy_b` variant (the masked heavy cav helm), and
`gondor_anf_infantry` gains a variant pairing the third heavy helm `inf_helmet_heavy_c` with the
second heavy chest `inf_chest_heavy_b`. **All 13 Anfalas pieces now used (0 idle).** `anf_cavalry`'s
mount stays a **single shared `<equipment slot="Horse">` outside** the rosters — the engine applies a
stray horse to every variant, so it is deliberately *not* duplicated into each roster (corrects the
earlier "horse must live inside each roster" assumption; the hill_knight bug was the mount being
*deleted* on rebuild, not its being outside). Equipment-only, save-safe (187 NPCCharacters unchanged);
both new refs mesh-exact; `validate_gondor_refs` + `validate_moduledata` PASS. Not-tested: in-game
render (gated on the `AssetPackages/` recompile, #358). The two Anfalas veterans keep their Lond-Galen
noble gear (`sk_gd_lon_*`) by design.

### feat(gondor): Lossarnach — wire the 2 idle armor pieces into variant rosters (#358)

Lossarnach had 24 of 26 `sk_gd_los_*` pieces in use; the two idle ones now have homes via variant
rosters: `gondor_loss_noble_captain` gains an `sk_gd_los_noble_helmet_elite_b` variant (2nd elite helm),
and `gondor_loss_vet_guard` (top regular axebearer) gains an `sk_gd_los_inf_chest_elite_a` variant
(stat 51, the elite regular chest the line never reached). **All 26 Lossarnach pieces now used.**
Equipment-only, save-safe (187 NPCCharacters unchanged); both pieces mesh-exact; `validate_gondor_refs`
+ `validate_moduledata` PASS. Not-tested: in-game render (gated on the `AssetPackages/` recompile, #358).

### feat(gondor): Lamedon troops use their full helmet + chest variety (#358)

The 5-troop Lamedon line (`gondor_lam_clansman` → `gondor_lam_hill_warden`) used only 5 of its 17
`sk_gd_lam_*` helmets and 5 of 7 chests. Added **variant battle rosters** (the engine random-picks one
per soldier) so **every Lamedon helmet and chestplate is now used**: footman spreads its 2 medium helms,
swordman the 3 plain heavies (+ the previously-idle `chest_med_b`), vet_swordman the 3 "gold" heavies,
and hill_warden the 4 elite + 4 lord helms (with the idle `chest_lord_a` on one variant). 17 rosters
total across the line. Equipment-only, save-safe — no troop id / upgrade_target / structure change (187
NPCCharacters unchanged); all helmet/chest refs mesh-exact; `validate_gondor_refs` + `validate_moduledata`
PASS.

**Balance flag:** the 4 `nob_helmet_lord_*` (head 40) + `chest_lord_a` (body 50) are lord-tier — placing
them on the common hill_warden (T6) makes its top variants Swan-Knight-tanky. Move them to Lamedon lords
instead if that reads too strong.

Not-tested: in-game render (restart + battle spawn), gated on the `AssetPackages/` recompile (#358).

### fix(editor): Modding Kit startup assert `rglConcurrentQueue.h:882` — prefab folder crossed the engine's 131,072-entity load cap

Root-caused by disassembling the wEditor `TaleWorlds.Native.dll` at the logged assert stack: editor
startup parallel-enqueues every `<game_entity>` from every loaded `Prefabs\*.xml` into a native queue
hard-capped at 131,072 items (4096 × 32; `cmp eax,0x20000` at RVA `0x7708F0`). `TAOM_Map\Prefabs\`
hit 132,378 entities (80.5 MB) after four imported packs landed 7/24; Ignore corrupts the queue
(permanent loading-screen hang), Abort crashes at a secondary site — the one WER records. Fix:
classified all 1,407 top-level prefabs by real scene usage (`references.txt` + `scene.xscene` union,
transitive closure, code-ref + vanilla-collision guards) and parked the 518 scene-unused prefabs
(92K entities) in `Prefabs_Unused\` with a review `_INVENTORY.md`; live folder now 41,355 entities
(~32% of cap) after restoring the `*kitbash*` working-palette libraries per review (standing
decision: kitbash files stay live even when scene-unused). New gate: `tools/check_prefab_budget.py`. Full RCA:
`docs/investigations/editor-rglconcurrentqueue-assert-2026-07.md`; lesson in
`docs/reviews/lessons/data-content-cultures.md`; assert-dialog protocol added to
`/native-crash-triage` Phase 1. Found: `icon_camera` in `Soisson_Prefabs_2.xml` shadows the Native
editor prefab (rename before re-enabling); imported packs carry Blender-default `cube.001` meshes
with missing materials (fix before re-enabling).

## 2026-07-23

### fix(elves): female elves render on the human female basemesh instead of the male elf basemesh

No dedicated female elf basemesh was ever authored, so every female `<skin>` in `<race id="elf">`
pointed all its body-mesh slots at the male `sk_elf_basemesh_a1_*` set — female elves rendered on a
male torso/shoulders/legs, wearing the **male** underwear mesh. The elf skins were derived from
vanilla human female (their `min_scale` values match exactly), so the gap was purely the swapped
meshes. Per user decision this is a **full** swap **including the head** (`face_meta_mesh` →
`head_female_a`): female elves now use the maturity-matched vanilla human female mesh set and no longer
carry the elf pointed-ear head.

Applied to the **adult / teenager / tween** female elf blocks (each mapped to its vanilla human female
equivalent — adult+teen use `body_female_a`, tween uses `body_female_a_kid` + `underwear_female_teen`);
the **toddler** female's stray `sk_elf_basemesh_a1_shoulders` remnant was also fixed. The `sauron` race
(an NPC-only verbatim elf clone, #321) is deliberately left untouched — no female sauron ever spawns.
This is LOTRLOME_Armory data, not TAOM-owned: the live `Modules/LOTRLOME_Armory/ModuleData/skins.xml`
(what loads) and the repo reference snapshot (`docs/reference/lotrlome-armory-snapshot/skins.xml`) were
both patched.

**The mesh swap alone was not enough.** Swapping `face_meta_mesh` left the `<face_textures>` still
pointing at the elf head *material* `m_elf_basemesh_a1_head` — in-game this rendered a correct female
body under a **garbled face** (smeared texture, mismatched patches, face/neck tone mismatch), because
the elf head material's UVs don't map onto `head_female_a`. The face assets are now aligned to vanilla
human female too: `<face_textures>` → `head_female_a/b/c/e` with `lod_material="head_female_a.lod"` and
vanilla's `color="0xFFCAD3E0"`, keeping the elf's wider `face_texture1..10` tag coverage so characters
whose body properties reference tags 5–10 still resolve. Two pre-existing defects fixed in the same
pass: `<mouth_textures>` referenced `m_dwarf_basemesh_mouth_a` (a **dwarf** mouth material) → now
`mouth_mat`; and `<eyebrow_meshes>` held a single `name=""` entry, meaning **female elves had no
eyebrows at all** → now the vanilla female set. Child + toddler females carried the same
human-mesh/elf-material mismatch before this session and are fixed too.

**Face tattoos were off by one.** With the face rendering correctly, the character-creation tattoo
picker showed a tattoo on the blank "no tattoo" slot. Vanilla's `<tattoo_materials>` begins with a
*nameless* `<tattoo_material>` carrying the `Cleanface` style tag — index 0 means "no tattoo".
LOTRLOME's elf list omits it and starts straight at `tattoo_female_a_mat`, so every index shifted
down one (elf had 33 entries where vanilla has 34, otherwise identical). Prepending the missing
Cleanface entry restores exact index parity. `zero_probability="85"` is deliberately left alone —
that governs how often randomly-generated elves get a tattoo, and is a LOTRLOME design choice, not
part of the indexing bug.

Verified at the XML level: both files parse well-formed; all 5 female maturities have zero
elf/dwarf asset references anywhere in the subtree and attributes **identical to vanilla human
female**; tattoo lists index-match vanilla 34/34. `/deep-review` confirmed at byte level that the
`<race id="elf">` block is the **only** region differing from the pre-change backup, and within it
only the 5 `gender="1"` skins — male elf skins, the whole sauron clone and all 12 other races are
byte-identical. Every one of the 89 asset names the female skins reference also resolves in
Native's own `skins.xml`.
**Not yet game-verified** — the face and eyebrows were confirmed in-game; the tattoo-index fix
still needs a shader-cache-sack clear and a look at the character-creation tattoo picker.

Save-compat: no migration needed — every list grew or held, so no saved index can fall out of
range. Cosmetic drift only: saved female elves gain eyebrows (list 1→5, where index 0 was a blank
entry), and their tattoo index shifts by one (33→34, blank Cleanface prepended at 0), so a saved
index 0 goes from `tattoo_female_a_mat` to no tattoo. Affects the 20 authored female elf lords in
`characters/lords.xml` (Galadriel, Arwen, …) and the three elf-culture character-creation defaults.

RCA: [`docs/reviews/rca-elf-female-skins-2026-07-23.md`](docs/reviews/rca-elf-female-skins-2026-07-23.md).

### fix(banner-bearers): guard the native SetFormationBanner call against heraldry-less troops — siege CTD (#349)

A manual siege assault at Stranding (`sturgia_town_c`) hard-crashed to desktop every time — native
`0xC0000005` in `TaleWorlds.Native.dll+0x28ac0e`, no managed exception, BUTR captured nothing. This is
the root cause the defensive `IsFieldBattle` guards on 2026-07-15 were blocking on (they guessed
MixedFormations/SmartCavalryAI; the fault offset now points elsewhere). BannerBearers (#351) drives the
engine's native `SetFormationBanner` for **every team's** formations at deployment; the native
banner-tableau rebuild access-violates when a bearer's heraldry `Banner` (`agent.Origin.Banner`, seeded
at spawn) is null or has an empty `BannerDataList` — the state a custom-faction garrison with no
heraldry produces. Vanilla only banners player-side, hero-captained formations, which always have
heraldry; TAOM broadened the caller set without re-checking that precondition.

Fix: `BannerBearerAssignmentMissionLogic` now skips `SetFormationBanner` unless **every** bearer-candidate
in the formation carries renderable heraldry — checking all candidates, not slot 0, because the engine
picks the bearer by priority from the whole set. The per-troop read is exception-safe
(`PartyAgentOrigin.Banner` is a computed getter that can throw, not just return null). Siege banner
bearers still appear wherever the parties have valid heraldry; only heraldry-less formations are skipped.
`/deep-review` (5 agents) caught 3 MED issues in the first cut — all fixed in-session. Suite 4410 green.

RCA: [`docs/reviews/rca-banner-bearers-siege-ctd-2026-07-23.md`](docs/reviews/rca-banner-bearers-siege-ctd-2026-07-23.md).
**Not yet game-verified** — the native precondition is confirmed by decompile; the Stranding siege still owes an in-game smoke test.

### feat(gondor): dress Gondor lords in new lord-tier armour — 15 regional variant sets by clan, battle + civilian (#358)

Gondor's ~111 lords (77 in `lords.xml` + 34 injected via `lords.xslt`) shared 5 mid-tier templates —
one even put an *infantry* chest on a lord, and groups of 16–36 lords looked identical. Replaced with
**15 lord-tier equipment templates** (`gondor_lord_<region>_<n>`) from the 2026-07 KEYforce noble drop,
each mirrored as a civilian twin (`_civ`, `equipmentType="Civilian"`) so lords wear the same regional
look in **town and battle** — 30 rosters total. Lords vary within a region; clans differ across regions:

- **Dol-Amroth** ×4 — `dol_cav_helmet_elite_a–d` + `dol_chest_elite_a/b` + `dol_pauld_cape_noble_elite_a`
- **Lond-Galen** ×2 — `lon_helmet_elite_a/b` + `lon_chest_lord_a` + Serelond elite cape/bracer/greaves
- **Linhir** ×3 — `lin_helmet_lord_a/b` + `lin_chest_elite_a` + `lin_pauld_cape_noble_elite_a`
- **Arndir/Pinnath** ×3 — `pin_noble_cav_helmet_elite_a/b` + `pin_nob_chest_elite_a/b` + `pin_pauld_cape_noble_elite_a`
- **Anorien/capital** ×3 — `osg_inf_chest_elite_a` + `ith_noble_helmet_heavy_a/b`/`osg_noble_helmet_heavy_a` + Anorien noble capes

**Distribution is by clan** (lord id prefix): each clan gets one region, variants rotate across the
clan's lords; regions spread evenly (17/16/16/15/13). Region↔clan geography isn't stored in the data,
so it's assigned round-robin (adjustable). **Fountain Guard / Citadel Guard armour is reserved for
those units** — lords use Osgiliath/noble pieces instead. Authored one missing item def,
`sk_gd_osg_inf_chest_elite_a` (elite, mesh present but un-authored), via `generate_gondor_armor.py`.

**Named-armour lords untouched:** `lord_1_60` (`tirnelion_bat_equipment`) and the six captain sets
(Imrahil, Faramir, Forlong, Golasgil, Hirluin, Angbor) keep their signature gear.

An adversarial deep-review caught a ship-blocker before commit: the new rosters initially wrapped their
`<Equipment>` directly under `<EquipmentRoster>` with **no `<EquipmentSet>` element** — the engine
deserializer (`MBEquipmentRoster`) reads only `EquipmentSet` children, so every roster would have loaded
empty and all 111 lords fought naked (battle-only; the validator/build never start a campaign so none
caught it). Fixed by wrapping all 30 rosters. Every armour item (all 30 rosters) verified exact against
armory defs + geo-tpac mesh TOCs; all lord refs resolve; `validate_moduledata` PASS. `gondor_bat_template_medium_a`
remains the culture `default_battle_equipment_roster` (untouched, mid-tier).

Not-tested: in-game render — REQUIRES a full game restart + battle spawn to confirm lords are clothed
(the item XML also awaits the `AssetPackages/` recompile, per #358).

### feat(bandits): rename eastern dwarf bandits "Erebor Warriors" → "Blacklocks" (immersion)

The dwarf-race bandit faction (`erebor_warriors`, ~10 hideouts in the north-east near Erebor and the
Iron Hills) is renamed from **"Erebor Warriors"** to **"Blacklocks"** — one of the four Eastern
Dwarf-houses of the Orocarni, here a clan fallen to shadow. This fits the eastern-map placement better
than the old "exiled Erebor deserters" framing, since Erebor is Durin's Longbeards in the north-west.
Community request.

- Renamed the faction/culture/clan display name (`taom_bandit_erebor_name`, which drives the on-map
  party name), the boss card `erebor_warriors_boss` "[Erebor] Warrior Captain" → **"Blacklock
  Chieftain"**, and the 10 `hideout_erebor_*` camp names in the **live** `TAOM_Map/settlements.xml`.
- Every `id=` kept (`erebor_warriors`, `erebor_warriors_boss`, `hideout_erebor_N`, clan id) → save
  games unaffected; `faction_banner_key` / `clan_heraldry` stay bound, so no heraldry change.
- Fixed a pre-existing gap: `HideoutDescriptionService` had no `erebor_warriors` entry, so the dwarf
  hideout menu fell back to vanilla "(Undefined hideout type)". Added `taom_hideout_desc_erebor` and
  a test (RED→GREEN).
- Localization: name set to "Blacklocks" (verbatim proper noun) across all 12 language files + the
  translation cache + the RU override, so the rename shows in every language. The new
  hideout-description sentence is English-only for now, deferred to a later `/localize` pass.
- Rank-and-file troop cards ("[Erebor] Miner" etc.) are **unchanged** — they are shared with the
  legitimate Erebor kingdom army, so renaming them would mislabel the real faction.

TAOM.Tests 4403 passed / 2 skipped (+1 new test). `validate_moduledata.py` clean.

Save-compat: display text only; all ids stable.

Constraint: `TAOM_Map/settlements.xml` (the live hideout names) is outside this repo — that edit is applied to the game install and won't appear in the git diff.

### fix(careers): correct misleading "regeneration" pip labels (9 careers)

The defensive `_b`-branch pips labeled **"+X% troop regeneration"** actually map to the `TroopSurvival`
effect — a post-battle survival-chance multiplier (`TaomPartyHealingModel.GetSurvivalChance`: a downed
troop survives as wounded instead of dying), not any form of healing or per-day HP recovery. Renamed
to **"troop survival"** across all 9 careers that share the branch: `dale_guardsman`, `ironguard`,
`blade_dancer` (Ñoldor), `warden`, `shadow_walker`, `uruk_berserker`, `cave_troll_master`,
`shadow_warrior`, `corsair_boarder`. The paired `HeroHealing` pip **"+15% health regeneration"** →
**"hero health regeneration"** so it reads as hero daily HP, not troops. 27 pips, each in two English
files (`taom_career_choices.xml` inline defaults + `taom_career_strings.xml` source strings);
magnitudes and mechanics unchanged.

**English only** — the 11 AI-translated languages + PL still show the old wording; deferred to a later
`/localize` pass. TAOM.Tests 4402 green (no test references these strings).

Save-compat: none — display text only.

### feat(gondor-armor): incorporate KEYforce noble armor item defs — Dol-Amroth, Linhir, Blackroot Vale, Arndir, Lond-Galen (#358)

106 new `sk_gd_*` item definitions authored into `LOTRLOME_Armory` for five southern-Gondor noble
lines from KEYforce's 2026-07-21 mesh drop, via the phase-2 generator: **Dol-Amroth** (`sk_gd_dol_*`,
33), **Linhir** (`sk_gd_lin_*`, 22), **Blackroot Vale** (`sk_gd_vale_*`, 20), **Pinnath Gelin
"Arndir" noble** (`sk_gd_pin_noble_*`, 21), **Lond-Galen** (`sk_gd_lon_*`, 10). Every id was verified
against the geo-tpac mesh TOCs (`Assets/gondor_assets/{belfalas,pinnath_gelin,anfalas}/*_geo.tpac`),
not just the spec — this caught the Lond-Galen rename below. The generator's `beard_cover_type`
default is aligned to `none` per commit `c4886891`.

**Lond-Galen was a mesh rename, not a new line.** The drop renamed its meshes
`sk_gd_anf_lon_helmet_*` → `sk_gd_lon_helmet_*` and `sk_gd_lon_nob_chest_*` → `sk_gd_lon_chest_*`
(the old names are absent from every geo tpac). The generator's Jun-06 "Anfalas Noble" entries were
renamed to the verified ids, and the two troops that wore the old gear (`gondor_anf_vet_infantry`,
`gondor_anf_vet_cavalry`) were repointed to the new ids so they stay clothed after the recompile.
The 10 old dead-mesh item defs still linger in the live XML, now unused — retire in the follow-up.

**Item defs only** — the new lines' troop trees (Dol-Amroth up to T9 Swan Knight, etc.) are a tracked
follow-up, so the four brand-new lines' items read as "unused" until wired. Gates:
`validate_gondor_refs.py` PASS (0 missing), `validate_moduledata.py` PASS, no duplicate ids across
`gondor/`, all slot XMLs parse. The new armor renders once the runtime `AssetPackages/` bundles are
recompiled via the Modding Kit (handled/coming).

Research: gondor_armors_and_troops.md (KEYforce spec) + geo-tpac mesh TOCs (ground truth)
Not-tested: in-game mesh render (gated on Modding-Kit pack recompile)

### feat(banner-bearers): infantry-only bearers + denser banners (#351)

In-game tuning after first play confirmed the feature works (a Dunlending line flew the deer-bane
standard). Two changes:

- **Only infantry troops become bearers.** A bearer swaps its weapons for a banner + a 1H sidearm,
  so making an archer or cavalry troop a bearer wastes its bow or mount. New `AllowedFormationGroups`
  config (default `["Infantry"]`) gates `CanAgentBecomeBannerBearer` on the troop's
  `DefaultFormationClass` — the same `default_group` its XML declares. Because vanilla's
  `CanFormationDeployBannerBearers` gates a whole formation on whether any unit is eligible, this
  gives pure archer/cavalry formations zero banners automatically, and `FindBannerBearableAgents`
  excludes non-infantry from candidacy so a mixed formation never falls back to an archer. Add a
  class name to the list to re-enable it; unknown names are dropped at load, an empty/all-invalid
  list reverts to Infantry.
- **Denser banners.** `InfantryBannerPerSoldiers` 20 → 10 and `MaxBearersPerFormation` 4 → 6, so a
  ~60-man infantry line shows ~6 standards in the engine's neat banner-row arrangement (cap stays
  6 — the arrangement tables hold 6 positions).

The per-class ratios for Ranged/Cavalry/HorseArcher are inert while those classes aren't allowed,
kept and documented so a class can be re-enabled by config alone. +13 tests (74 total); full suite
green. `FormationClass` is a fixed engine enum so `AllowedFormationGroups` is validated at load.

Research: BasicCharacterObject.DefaultFormationClass, BannerBearerLogic.FindBannerBearableAgents, DefaultFormationArrangementModel
Not-tested: in-game density + that archer/cavalry formations show no banner

## 2026-07-21

### fix(lotr-issues): the 7 SandBox vanilla issues were never suppressed in-game — CTD on quest accept

A Patreon crash report (TAOM v2.0.12, new Rhûn campaign) showed a CTD accepting a rural notable's
quest. The quest was vanilla **NotableWantsDaughterFound** — one of the 7 SandBox-module issue
behaviors `LotrIssueSuppression` resolves by `Type.GetType("…, SandBox")`. That bind fails in-game
(module DLLs are `LoadFrom`-loaded outside the appbase and the engine's `AssemblyResolve` matches
exact FullName only), so all 7 SandBox issues stayed **live in every campaign** since the feature
shipped; the failure was masked by graceful degradation (a warning log nobody read) and by the test
host, where SandBox.dll IS loadable. The daughter quest then NRE'd in its constructor: it maps giver
culture → rogue template through the vanilla `steppe_bandits` clan, which TAOM's `spclans.xslt`
deletes → `CreateSpecialHero(null, …)` inside the accept-click → crash.

Fixes: `VanillaIssueBehaviorTypes` now resolves the 7 by scanning loaded assemblies for the simple
name `SandBox` (`ResolveTypesFromLoadedAssemblies`); the under-count log is `LogError`; and a new
`OnGameLoaded` safety-net sweep in `LotrIssuesCampaignBehavior` cancels **uncommitted** vanilla
issues lingering in saves made on broken builds — accepted quests, dispatched alternative
solutions, and lord solutions are left to finish (their start paths already ran safely; cancelling
would destroy player progress — deep-review catch). `SuppressAll` now counts actual behavior-list
shrinkage, not just "the call didn't throw". New tests pin the 7 names against the real
SandBox.dll as an engine-bump canary. Suite green (4389). Deep-review: 4 findings fixed
in-session, 1 disputed — RCA `docs/reviews/rca-lotr-issues-suppression-gap-2026-07-21.md`;
lesson `docs/reviews/lessons/adapters-taleworlds-api.md`.

### fix(education): age-8 child education CTD for lothlorien + 3 more cultures (#354)

A player crash bundle (`94c7b795`) CTD'd clicking the child-education map notification for a
Lothlórien child at age 8. Root cause: `lothlorien` shipped with **zero**
`child_education_templates_stage_2_page_0_branch_{0-5}_lothlorien` NPCCharacters — the v1.4.7
engine resolves those ids at the Year8 stage and dereferences the result with **no null guard**
(`EducationCampaignBehavior.GetSpecialCharacterPropertiesForOption`), so the education screen NREs
at first paint. `umbar`, `goblin`, and `mistymountainorcs` had the identical gap (ages 2/5 never
consult these templates, which is why it survived). Fixes:

- **Data:** 24 stage_2 tutor templates added (4 cultures × 6 branches; lothlorien mirrors
  rivendell — elf race, `fighter_rivendell` face; umbar mirrors gondor; the orc pair wear the
  shared `sk_md_orc_*` pool their NPCs use). Plus 392 `child_education_equipments_*` rosters for
  lothlorien/umbar/shaghana/abanissa (cosmetic — engine lookups there are null-safe).
- **Prevention:** `tools/validate_moduledata.py` now ERRORs (`MISSING_EDUCATION_TEMPLATES`) when
  any `is_main_culture="true"` culture lacks the 6 stage_2 templates; 6 new validator tests.
- **Diagnostics:** `PatchShield` finalizers now rethrow the ORIGINAL exception instead of the
  TIE-unwrapped inner one — the unwrap+rethrow reset the exception's stack to the patched frame,
  which is exactly why this bundle blamed `ViewModel.ExecuteCommand` with no inner exception.
  Swallow classification (missing-member trinity) is unchanged.

Suite green (4386) + validator PASS. Not-tested: in-game education screen for the 4 cultures
(owed: load a lothlorien save with an age-8 child); PatchShield finalizer runtime path (no test
harness for the TAOM.Dependencies assembly — verified by compile + a rethrow-semantics harness).
Lesson: `docs/reviews/lessons/data-content-cultures.md`.

## 2026-07-17

### feat(blow-diagnostics): durable per-blow instrumentation to catch a dwarf-siege native crash

A player hit two crashes-to-desktop in one siege as a dwarf — once when the character was
**wounded**, once when a **fire pot** was about to land. Both are native AccessViolations: the
attached logs (managed `taom_debug` + native `rgl_log`) just stop mid-battle with **no managed
stack**, and TAOM's crash pipeline can't capture a pure native AV. Investigation ruled out the
tempting culprits — the dwarf action-set parity is clean on 1.4.7 (`audit_action_set_parity.py`
= 0 gaps), and the defender-Trebuchet `TaomSiegeEventModel` override is **dead API** (no caller
in 1.4.5–1.4.7), so the fire pot is a plain vanilla FireCatapult projectile. The surviving lead
is a custom-race (dwarf) agent taking a *specific* blow through native `Agent.HandleBlowAux` /
`Agent.Die` — the same fault family the spider Patch47/48 guard, but those are spider-gated and
leave a plain dwarf unguarded.

New **`Patch63_BlowDiagnostics`** (feature `Main/Features/BlowDiagnostics/`) stamps every damaging
blow, death, and siege-engine shot to the durable (synchronous-flush) log, so the LAST line before
a hard crash names the fatal blow — victim race, blow flags, damage type, missile/fall, health.
Toggle-gated behind MCM **"TAOM — Blow Diagnostics"**, **OFF by default** (it's a per-hit hot path);
turn it on only to reproduce, then send `Logs/taom_debug_*.log`. Diagnostic siblings of Patch47/48
(separate classes, the spider guards untouched); the `HandleBlowAux` prefix runs at
`Priority.First` so it records the pristine blow. 14 unit tests; full suite green (4377). See
[docs/features/blow-diagnostics.md](docs/features/blow-diagnostics.md).

Follow-up tracked separately: the dead-API `GetAvailableDefenderSiegeEngines` override means the
SiegeDefense "defenders get Trebuchets (Minas Tirith)" behavior is silently non-functional on
1.4.5+ — its own issue, not this crash.

### feat(vassal-rewards): every kingdom hands out its own troops, normalized to 6

The one-time troop/item gift you get for swearing fealty (vanilla `DefaultVassalRewardsModel`,
keyed off the **kingdom's** culture) had three defects across TAOM's 18 kingdoms:

- **Three bespoke reward parties were orphaned.** `vassal_reward_troops_rohan`, `_harad`, and
  `_rhun` existed but the XSLT wired Rohan/Harad/Easterlings to the vanilla-named
  `_vlandia`/`_aserai`/`_khuzait` templates — which don't exist in TAOM and resolved to stock
  **Calradian** troops. Joining Rohan literally gave you Vlandian knights. Repointed the three
  XSLT cultures at their hand-authored LOTR parties.
- **Sizes were inconsistent** (Mordor gave **1** troop, Dol Guldur 4, Dale 8, the rest 6).
  Rebalanced every reward to a uniform **6** (1 elite + 5 second-tier, vanilla parity): Mordor
  = Barad-dûr Guard + 5 Uruk Vanguard; Dol Guldur = 1+3+2; Dale trimmed 8→6.
- **Cultures on borrowed rewards.** Umbar/Shaghâna/Âbanissa handed out vanilla Aserai troops —
  now Umbar has its own `vassal_reward_troops_umbar` (Naru n'Aru Royal Guard + Abrazanim), and
  Shaghâna/Âbanissa share Harad's (they already use Harad's roster). Dale and Khand never got a
  TAOM reward *item* — Dale now grants a Dale longbow. Khand (Variag), which has no native LOTR
  roster of its own, gets a **Mordor-proxy** reward (Mordor troops + a Mordor blade), since the
  Variags serve Mordor.

Data-only (party templates + `taom_spcultures.xml` + `spcultures.xslt`); no C# change, no new
strings, no save impact (read at join-time; reward is one-time via vanilla `_receivedVassalRewards`).
`validate_moduledata.py` clean.

### fix(settlements): repoint two scenes Bannerlord 1.4.7 removed, and harden the scene audit

A raid defense of Nan Angren hung forever on the loading screen (the player had to force-close).
Root cause: Bannerlord 1.4.7 deleted the vanilla scene `battania_village_c`, leaving an **empty
`SceneObj/battania_village_c/` husk** on disk, but TAOM_Map's `settlements.xml` still routed Nan
Angren's `village_center` at it — the engine stalled opening a `scene.xscene` that no longer exists.
The same 1.4.7 wave removed `sturgia_castle_keep_a_l1_interior`, referenced by **36** Dale/Sturgia
lord's-halls (the L1 building-level slot) — every one of them a latent land-load hang.

Remapped both to scenes that survived 1.4.7 (`battania_village_e`; `sturgia_castle_keep_a_l2_interior`),
applied to the live `TAOM_Map/settlements.xml` (37 refs). The full live settlement set now resolves
every scene ref to an on-disk `scene.xscene` (vanilla or TAOM-custom).

The mandated post-bump scene audit missed this because `audit_scene_names.py` had two blind spots,
both now fixed: it tested SceneObj **folder** existence (an empty husk passed) instead of the actual
`scene.xscene` inside, and its regex matched only `scene_name="…"`, skipping the `scene_name_1/2/3`
lord's-hall slots (which is why the 36 keep refs were never flagged). Separate from this hang, the
earlier non-fatal "outside Isengard" shader AV is the known #287 `pbr_terrain` compile issue.

## 2026-07-16

### feat(prisoner-recruitment): no morale lost recruiting your own side's prisoners (#353)

Recruiting a prisoner cost morale regardless of who they were. An Isengard player pressing captured
Mordor orcs into service paid the same −1 per troop as they would for Gondorian knights, even though
both sides serve Sauron. Vanilla's only relief is a perk: `Leadership.Presence` (same culture) or
`Roguery.TwoFaced` (bandits).

The waiver now fires on two rules: the prisoner is of your **own culture**, or on your **own
non-Neutral alignment side**. Everything else keeps the vanilla penalty, including the −2 for bandits.

Rule 1 exists because of the Neutral factions. Khand, Umbar, Shaghâna and Âbanissa are `neutral` in
`alignment.json`, so the side rule deliberately never fires for them — without a same-culture rule a
Khand player would lose morale recruiting Khand troops. The consequence is an asymmetry worth knowing:
Khand waives Khand but not Umbar. Neutral means unaffiliated, not allied.

No data change was needed. Dunland's culture is `empire`, already `evil` — as are `mordor`,
`isengard`, `gundabad`, `dolguldur`, `khuzait` (Rhûn) and `aserai` (Harad).

The hook is `PrisonerRecruitmentCalculationModel.GetPrisonerRecruitmentMoraleEffect`, which AI
recruitment, the party screen, and the UI cost label all resolve through `Campaign.Current.Models` —
so one override covers all three and the shown cost can't desync from the applied one. No Harmony
patch. Side data is reused from Execution's `IAlignmentService` rather than duplicated; this feature is
the mirror of AlignmentDesertion, which sheds troops whose culture opposes the owner's side.

Bandits never waive, via three barriers TAOM already had: bandit troops carry dedicated bandit
cultures, none of those ids appears in `alignment.json`, and vanilla blocks bandit-culture prisoners
from recruitment outright. Only the middle one is an editable file, so a test pins it — deriving the
bandit id set from `taom_spcultures.xml` rather than hardcoding a list that could itself go stale.

49 tests. MCM group "World/Prisoner Recruitment" (master + player/AI gates, default on); master off is
exact vanilla. Save-clean — no persisted state.

`/deep-review` found no live bug. Its data-flow agent did catch that the bandit safety story was
weaker than written: the barrier "no `occupation="Bandit"` troop carries a mainline culture" was a fact
about the shipped data with nothing enforcing it, and vanilla keys the −2 on per-troop occupation while
gating recruitability on per-culture `IsBandit` — so a troop pairing the two would have been both
recruitable and waivable. None exists; a test now derives the bandit-culture set from
`taom_spcultures.xml` and pins it. RCA: `docs/reviews/rca-prisoner-recruitment-2026-07-16.md`.

Not-tested: in-game verification that the model resolves at runtime (owed).
Research: DefaultPrisonerRecruitmentCalculationModel.GetPrisonerRecruitmentMoraleEffect, GameModelsManager.GetGameModel, Campaign.Initialize
Rejected: hoisting the duplicated kingdom→culture ResolveSide onto IAlignmentService — a 4-service refactor doesn't belong in a feature PR
Save-compat: No new state — decision is recomputed per call

### fix(armory): siege load hung forever on two physics-body typos (#352)

Loading a siege with Dunland troops froze the game permanently — no crash, no error log, one CPU
core at 100%. A user running their own `TAOMAssetLoadGuard` submod traced it with ClrMD to
`PreloadHelper.WaitForMeshesToBeLoaded`, which polls every registered physics-body name and only
exits once each resolves. One name that never resolves is an infinite loop on the main thread.

They identified the body — `bo_dunland_caerdh_sword_blade_2h` — but concluded the asset was never
shipped, and worked around it by replacing the sword with an axe. The asset ships fine:
`bo_dunland_caerdh_sword_blade_2h_a` sits in `pack1.tpac` alongside the axe blades that work. The
crafting piece's `mesh` carries the `_a` suffix and its `body_name` doesn't. A one-token typo, so
the workaround deleted a working sword.

Auditing every `body_name` in the Armory against every `.tpac` found a second, unreported instance:
`bo_wm_harad_spear_a02_blade` should be `..._a02_head` (Harad spears use `_head`; only swords and
glaives use `_blade`). It reaches missions through a crafting template rather than a troop roster,
so it hangs only for players who craft that spear head — which is why nobody had reported it. Those
two were the only unresolved body refs; the rest resolve against Native.

The uncomfortable part: `tools/validate_mesh_refs.py` was built in #262 to test exactly this
hypothesis ("a missing `bo_` collision mesh causes infinite battle-load hangs"), and it catches both
typos at the exact lines — when pointed at the right directory. Its `DEFAULT_ITEMS` scanned
`ModuleData/LOTRLOME_items/`, while crafting pieces live in `ModuleData/` root. The tool built to
catch this never looked at the file containing it. Widened the default to `ModuleData/`.

Tier C was also built on a wrong assumption, stated in its own header: that `bo_` bodies are "NOT in
the .tpac TOC (they live embedded in mesh metadata)", which forced a coarse raw-byte scan needing
rgl_log confirmation. They are in the TOC, as `PhysicsShape` items — `pack1.tpac` exposes 382, a
count derived independently two ways (a hand-rolled GUID parse and TpacTool's own
`PhysicsShape.TYPE_GUID`). Tier C now matches against that exact set and only falls back to the byte
scan for packs that soft-fail to parse. The clean-run message no longer claims the hypothesis is
"WEAKENED" — a clean result only ever meant "clean within `--items` scope", which is precisely the
error that let this ship.

Not adopting the reporter's other change, a 30s timeout on the preload loop that drops unresolvable
shapes and continues: it converts a loud hang into quiet missing-collision behavior. The validator
catches this class before ship instead.

RCA (all three "the guard existed but was pointed slightly wrong" failures, incl. this session
extending the wrong tool off a false-negative grep): `docs/reviews/rca-siege-load-hang-2026-07-16.md`.

Research: PreloadHelper.WaitForMeshesToBeLoaded; TpacTool.Lib PhysicsShape.TYPE_GUID
Not-tested: in-game siege load (owed)

### feat(banner-bearers): formations raise their faction standard, and bearers keep their race (#351)

TAOM battle lines now field banner bearers. A bearer is one of the formation's own soldiers, so
an orc formation's bearer is an orc, a dwarf formation's is a dwarf.

Reviewed the third-party "Raise your Banner" mod (v16.1.7, 4,535 lines decompiled) as the
reference implementation and did **not** port it. The engine already ships the whole system —
`BannerBearerLogic` is added to every field battle, sally-out and siege — it simply never
switches on: `SetFormationBanner` has only two gameplay callers, and both need a hero captain
carrying a banner item. TAOM's lords carry none, so no formation ever got one. Supplying that
one call is the entire feature.

Driving the native system also makes the race bug structurally impossible. RYB spawns clones
from a synthetic `ryb_banner_bearer` character that declares no `race`, so `AgentData` derives a
human skeleton *and* human skin from it. That is unfixable in its architecture: `AgentRace` is
referenced zero times in `Mission.cs` (multiplayer-only), `AgentData.Race()` sets
`GenderOverriden` without a gender and would silently force every bearer male, and the skin comes
from `Character.Race` on a shared `MBObjectManager` singleton. The engine's `UpdateAgent`
converts an existing agent in place and never respawns it, so race cannot drift.

- `TaomBattleBannerBearersModel` overrides the `BattleBannerBearersModel` slot: bearers scale
  with formation size per class (vanilla hardcodes 1), a configurable race gate, and a backstop
  for the unarmed-bearer bug.
- `BannerBearerAssignmentMissionLogic` assigns banners on `OnTeamDeployed` — vanilla's own call
  site. Gated hard on `Mission.Mode == Deployment`: `SetFormationBanner` ends in
  `SetIsAIPaused(true)` and the only unpause is `DeploymentMissionController.FinishDeployment`,
  which removes itself, so a post-deployment call freezes every bearer for the whole battle.
  No tick loop — the engine self-heals reinforcements via `GetMissingBannerCount`.
- Race gate excludes trolls and named races (`cave_troll`, `hill_troll`, `nazghul`, `saruman`,
  `sauron`), configurable in JSON. Mirrors the #346 cave-troll guard exclusion. `Agent.IsHuman`
  is the *humanoid* flag, so all 14 LOTRLOME races already qualified as bearers.
- Ships zero art: vanilla's 45 banner items are neutral-culture with `using_tableau="true"`, so
  the cloth renders each party's own heraldry. Custom LOTR meshes swap in later via config only.
- Added `<banner_bearer_replacement_weapons>` to the 8 `is_bandit` cultures that declared none —
  they would have fielded bearers holding a banner and nothing else, since vanilla returns null
  there and the engine clears the other weapon slots. All 28 cultures now declare them.
- Master toggle off = exact vanilla: the assignment logic does nothing **and every GameModel
  override defers to `base`**, so vanilla's own hero-captain banner path keeps working.
  Campaign-only; Custom Battle is unaffected.

`/deep-review` (5 agents) found 2 CRITICAL + 2 HIGH + 1 MED + 1 LOW, all fixed in-session; one
further CRITICAL was disputed and dismissed with evidence. Both CRITICALs were data-flow gaps
invisible to the type system and silent at runtime:

- **Six culture keys matched nothing.** `spcultures.xslt` re-skins the vanilla cultures by
  overriding `<name>` but never `id` — so Rohirrim is `vlandia`, Dunlendings `empire`, Haradrim
  `aserai`, Easterlings `khuzait`, Barding `sturgia`, Variag `battania`. The config keyed all six
  on their LOTR names, so six of the mod's highest-volume factions silently flew the generic
  Gondor standard. Fixed, plus three regression tests pinning every key against the real culture
  set.
- **The master toggle didn't restore vanilla.** With the feature off, the model returned `0`
  bearers instead of deferring to vanilla's `1` — suppressing banners for formations vanilla's
  own hero-captain path had bannered, i.e. worse than vanilla rather than equal to it. Three
  sibling overrides leaked the same way. All four now `return base.<Method>(...)` when disabled.

A third bug surfaced while preparing the Codex pass: `DefaultBannerItemId` shipped as
`standard_of_duty_t1`, so every **unmapped** culture flew a Gondor standard. 38 cultures are
registered but only 28 are TAOM's — the rest are vanilla leftovers (`looters`, `sea_raiders`,
`forest_bandits`, …) still carrying ~99 live references in TAOM's own data, so every
vanilla-culture bandit warband would have raised the Standard of Duty. The default now ships
empty: only explicitly-mapped cultures field standards. Fail closed — a forgotten culture with no
banner is a cosmetic absence, with the wrong banner it is an immersion break.

`/review-codex` (gpt-5.5 xhigh) then ran against the corrected state: **0 CRITICAL, 0 HIGH, 2 MED,
1 LOW, zero disagreements — SHIP WITH FIXES.** All six seeded suspects came back favourable,
independently confirming the deployment-window freeze guard is sufficient. Codex found none of the
bugs the internal review found and two it structurally could not have, both about whether a
*choice* is semantically right rather than whether code is correct:

- **Formation culture came from `GetFirstUnit()`** — which is literally `Arrangement.GetAllUnits()[0]`,
  an arrangement slot, not a culture owner. A mixed-culture formation (allied army, mercenary-heavy
  party) flew whichever standard landed in slot 0. Now a majority vote with an ordinal tie-break, so
  the result never depends on arrangement order.
- **MixedFormations' `Patch30` was overriding banner-bearer placement** — it blanket-suppresses
  vanilla `GetOrderPositionOfUnit` for every unit in a field battle, so the engine's dedicated banner
  slots were ignored and standards scattered through the ranks. Bearers now fall through to vanilla.
- `ExcludedRaces` typos failed open (an unknown name never matches). Now validated on first use,
  where the race registry is live, and warned once.

**The most useful finding wasn't about banners.** `.claude/rules/xml-data.md` has a section titled
"Config ID Cross-Reference (MANDATORY)" that names all six wrong culture names explicitly — and its
`paths:` matched only `**/*.xml` while its prose said "ANY XML/JSON config". The rule written to
prevent this exact mistake could not fire on the `.json` file that made it, and 58 of TAOM's 59
ModuleData JSON configs sat outside the trigger. Fixed by adding `**/*.json`. The fact was already
documented in four live places; the failure was knowledge with no trigger, not missing knowledge.

RCA + 7 lessons codified: `docs/reviews/rca-banner-bearers-2026-07-16.md`.

61 tests, all green; full suite green. In-game verification still owed — see the feature doc.

Research: BannerBearerLogic, BattleBannerBearersModel, SandboxBattleBannerBearersModel, AgentData, Mission.SpawnTroop, spcultures.xslt
Rejected: clean-room port of Raise your Banner — re-inherits the race bug the engine path cannot have
Rejected: C# fallback for the unarmed-bearer gap — the loop breaches ADR-002 and the sealed types make a service extraction breach ADR-007; the data fix has neither problem
Constraint: SetFormationBanner is deployment-only; UpdateAgent pauses AI and only DeploymentMissionController unpauses
Not-tested: in-game bearer movement, reinforcement waves, MixedFormations arrangement interaction

### fix(battle-load-diagnostics): crash-durable logging + split the OpenNew→Initialize blind window

Triaging a player CTD (TAOM v2.0.12, attacking Deserters at Nan Angren, vanilla scene
`battania_village_c`) surfaced two defects that made the crash **unprovable from its own log**. The
crash itself is NOT fixed — root cause is still unknown, and the reporter can supply no further
artifacts. This makes the next report self-localizing.

- **`FileLogger` lost its final lines on every hard crash.** `Enqueue` only queued; a background
  thread (`IsBackground`, 50 ms idle sleep) did the writing, so a dying process took the undrained
  queue with it — the forensics instrument systematically dropped the lines it exists to capture.
  INFO/WARNING/ERROR now drain and flush synchronously on the calling thread; DEBUG (5079 of the
  5356 lines in the player's session) stays async. One queue, one lock, so global order holds and
  `StreamWriter` is never touched concurrently. Also closes a pre-existing race: `Dispose`'s
  `Join(5s)` can time out, and the drain/dispose then raced the live writer into an
  `ObjectDisposedException` on a background thread. Cost is ~15 ms across a multi-second load.
- **The `MissionOpenNew` → `MissionInitialize` window was one dark segment** spanning a tick
  boundary, because the OpenNew stamp is a *Prefix*. Four new stamps name the segment that died:
  an `OpenNew` Postfix, the private `MissionState.LoadMission`, the native
  `Utilities.ClearOldResourcesAndObjects` (the one native call in the window, and the shape that
  access-violates), and `Mission.AfterStart` — which brackets *every* submodule's
  `OnMissionBehaviorInitialize`, so a report can now **exonerate** TAOM rather than only accuse it.
  TAOM's own behaviors are stamped by name via an `AddTaomBehavior` helper.
- `Patch43_BattleLoadDiagnostics` is now try/catch-guarded on apply, like Patch60/61/62 — the
  category binds a private method by string, and a diagnostics category must never break startup.
- Existing `FileLoggerTests` were structurally blind: all four disposed before asserting, and
  `Dispose` drains, so they passed against any implementation. New tests read the open file via
  `FileShare.ReadWrite` without disposing.

Registry corrections: Patch43 is **14** hooks (was 11), and `Mission.Initialize` is **public**
(`Mission.cs:1798`), not private as the entry claimed since the feature shipped.

**Deep review (6 agents) found 2 MED defects in the fix itself — both fixed in-session.** The five
core agents all read `FileLogger` and passed it; both defects were caught only by a hand-rolled
concurrency agent, because no core agent has a thread-safety rule set:
- `Drain()` early-returned on a null writer **before** dequeuing, so post-`Dispose` the queue never
  emptied and `ProcessQueue`'s `!_queue.IsEmpty` loop could spin a core at 100% until process exit.
  A regression: the old loop always dequeued (writing via `_logFile?.`) and could not spin.
- The empty `catch` gave a write fault (disk full, AV lock) zero signal — the forensics instrument
  would look healthy while silently losing lines. Now counted and self-reported as a `WARNING` on
  the next successful drain.

RCA: `docs/reviews/rca-battle-load-blind-window-2026-07-16.md` (3 lessons filed); REVIEW-LOG entry 75.

Docs swept for the durability change. `battle-load-diagnostics.md` and `save-load-diagnostics.md` both
still asserted the OLD async-only logger ("flushes on a background writer thread (50 ms poll)") — that
claim was the reason a reader would trust the last DEBUG line, and it is now wrong for INFO and still
right for DEBUG, so both now state the split explicitly. Also corrected while in there: the feature doc
said 6 load-phase hooks (**8**) and 50 tests (**72**), both measured, not recalled. Separately re-synced
the lessons index — every one of its 13 per-category counts had drifted, total **206 → 227 measured**
(the index total, not just my +3). Per that record's own "never restate a value in prose" lesson, a
re-sync resets the clock without removing the mechanism; making `lint_docs.py` assert the counts is the
actual fix and is **not** done here.

Not-tested: Harmony patch invocation (requires live game) — in-game smoke owed.
Research: `MissionState.cs:302/235/241/243/345`, `Mission.cs:1798/3799/3815` (installed v1.4.7).

### fix(tools): deep review of the 10 asset-pipeline scripts — 1 CRITICAL + 8 HIGH confirmed, fixed in-session

- 5 focused tooling agents (core C# agents N/A for a pure-Python changeset, per deep-review Step 2c).
  Highlights: citysplit foliage placements were recorded at world origin (bake zeroed matrices before
  the transform snapshot — CRITICAL); stale `bound_box` after `join()` no-opped the chunk re-pivot and
  explained the dismissed "0.0 dims" symptom; `'tree' ⊂ 'street'` foliage misclassification;
  `generate_rivendell_materials --force` would have overwritten hand-made materials against its own
  docstring (now enforced via `_generated_manifest.json`, seeded 183 generated / hand-made excluded);
  texture `--dry-run` still wrote to the live module; meshlist pollution let citysplit chunk ids match
  as placement "templates" (now split into `_meshlists_assembled`). Full findings table, output audit,
  deferred items (stem-map sidecar, sanitize unification, weld-inside-big-join, normal-map resampling):
  [`docs/reviews/rca-asset-pipeline-tools-2026-07-16.md`](docs/reviews/rca-asset-pipeline-tools-2026-07-16.md);
  2 lessons appended to `docs/reviews/lessons/build-tooling-workflow.md`.
- Docs: new reference [`docs/reference/ue-to-bannerlord-asset-pipeline.md`](docs/reference/ue-to-bannerlord-asset-pipeline.md)
  (pipeline stages + tpac material format + Blender/UE gotchas, CLAUDE.md Doc-Lookup row added);
  the 10 scripts registered in `tools/README.md` § UE→Bannerlord asset pipeline; Rivendell + Tents
  rows added to `docs/kitbash/README.md` (incl. the t_-material naming exception).
- Known-bad outputs: the four `assembled/*_layout.json` files predate these fixes — their foliage arrays
  are origin-garbage; do not build prefabs from them (assembled direction is parked anyway).
- Note: the user's hand-made `t_rivendell_arch_starlight_mtl.tpac` is missing from disk (no tooling here
  deletes materials; likely removed in an editor session) — surfaced for recreation if unintended.

### feat(tools): Medieval Tent Collection (Fab) → Bannerlord Tents kit

- `tools/oneoff/convert_tent_textures.py`: Substance-style separate maps → `t_tent_*_{d,n,s,h}`
  triples (user decision: 'clear' white canvas only, dyed variants skipped; parts frame/rope/
  rivet/fasteners included; `_s` = R:metal/G:gloss/B:AO as the Rivendell kit; DirectX normals).
  10 sets converted. The vault ships texture zips only for High/Long/Open — **Wide family +
  On_Sticks textures are missing from the Fab download** (user to fetch; converter re-run covers them).
- Normalizer generalized: `--kit`/`--physmat` args + no-double-prefix naming. All 9 tents →
  `AssetSources/Scenes/Tents/tents_medieval_kit.fbx` (ONE kit FBX, Mirkwood pattern): unique
  `sm_tent_*` ids + `bo_` twins, `wood` physics, material slots stem-matched to the texture sets.

### fix(tools): Rivendell assembled-scene pipeline — root-caused four Blender batch bugs

- Stale `matrix_world` before depsgraph eval; multi-user `transform_apply` silent skip; negative
  (mirrored) instance scales flipping baked normals; weld destroying paper-thin double-shell drapes.
  All fixed in `blender_normalize_rivendell.py` (data-level `bake_world`, determinant-gated normal
  flip, weld only above decimate target). Buildings-as-single-mesh at 300k (`sm_rivendell_bld_*`)
  shipped but **failed the user's quality bar** (soup decimation melts flat architecture);
  reconstruction-from-modular-pieces prototyped (92% instance match, `blender_reconstruct_buildings.py`,
  `blender_dump_level_placements.py`) — first output also rejected; assembled-scene direction
  PARKED pending user decision against the original UE exports.

## 2026-07-16

### test(deps): pin the BUTR dependency couplings that silently rotted through two engine bumps

A 6-agent `/deep-review` of the 2026-07-15 BUTR update found **no runtime defects** — and one systemic hole: nothing
in the repo asserted a single dependency coupling, so the Native engine pin sat at `v1.4.5.*` through the 1.4.6
**and** 1.4.7 bumps, and the vendored impl set stayed capped at the game-1.4.1 build while BUTR shipped through
1.4.5, all under a continuously-green 4212-test suite. Added
[`BundledDependencyManifestTests`](TAOM.Tests/Infrastructure/Dependencies/BundledDependencyManifestTests.cs) (8 tests):
compile-pin parity between both csprojs, the v99 stub derivation, vendored-DLL version homogeneity (split-brain
refresh guard), the Native↔pinned-engine coupling, and licence attribution. It asserts *relationships*, never
version literals — a test that restates a version is one more drift site. Verified RED against the pre-fix state.

Documentation fixes from the same review (37 stale version lines across 6 files):

- **`Dependencies/_Module/SubModule.xml`** — the loader's pin-comment block was stale on 6 counts. Version values
  deleted; it now points at the authoritative sources.
- **`THIRD-PARTY-LICENSES.txt`** — attributed 4 wrong upstream versions for binaries this update physically
  swapped. Corrected, and now test-enforced. (A binary swap is a licence event, not just a build event.)
- **`dr3-maintenance.md`** — restructured rather than re-synced: duplicated version values deleted (the Category 1
  table had already been re-synced once in May and drifted again by July; its stub rows were logged broken in June
  and were still broken). **Scenario A rewritten** — its "most likely nothing needs to change" advice for a patch
  bump is what caused this drift. Contradictory stub rules reconciled to the minor-keyed one. The 6
  `BUTR.CrashReport*` DLLs added to the file inventory and marked MANDATORY — their omission predates this work and
  would have had a maintainer rebuild a bin folder that reproduces a known `ReflectionTypeLoadException`.
- **Audit doc** — retracted a **circular** safety argument for keeping `Patch41_McmLayoutFix` (idempotency holds
  identically in the safe and unsafe cases, so it discriminated nothing). Replaced with evidence: a byte-scan of
  MCM's embedded prefabs across both releases (`VerticalBottomToTop` ×9→×0, total conserved at 11) proves 5.12.x
  fixed the attribute. **Patch41 stays anyway** — MCM DLLs are unsigned and resolve by simple name, and another
  module on the same install ships MCM 5.11.4, so load order decides which prefabs win; if an older MCM wins,
  Patch41 is still load-bearing.

API compatibility independently verified against the restored DLLs (13 verified / 0 incompatible): Patch41's
`CreateAndRegister(string, XmlDocument)` target intact, `WidgetFactoryManager` byte-identical 2.13.1→2.13.2,
`MCMv5.dll` 5.11.4→5.12.1 identical apart from version stamps. Deployed game install verified at 2.13.2.0 /
5.12.1.0 / 2.11.0.0 with all six impls 1.4.0–1.4.5. RCA:
[`docs/reviews/rca-butr-dependency-update-2026-07-16.md`](docs/reviews/rca-butr-dependency-update-2026-07-16.md).

Known limitation: the in-game smoke test remains outstanding (launcher was running); no GitHub issue exists for
this work yet.

## 2026-07-15

### fix(deps): update BUTR stack to current — ButterLib 2.11.0, MCM 5.12.1, UIExtenderEx 2.13.2; Native pin → 1.4.7

Users reported the bundled dependencies looked out of date with 1.4.7. Audited all four BUTR deps against
NuGet, the BUTR GitHub releases, and the current Steam Workshop installs (full findings + evidence:
[`docs/migration/dependency-audit-2026-07-15.md`](docs/migration/dependency-audit-2026-07-15.md)) — the
versions had been static since the May 2026 DR3 internalization, never revisited for the 1.4.6/1.4.7 bumps —
then applied the updates:

- **Native engine constraint** `v1.4.5.*` → `v1.4.7.*` (`Main/_Module/SubModule.xml`).
- **ButterLib 2.10.4 → 2.11.0** — refreshed the vendored `Bannerlord.ButterLib.dll` + added the
  `Implementation.1.4.2`–`1.4.5` DLLs. TAOM previously bundled only `1.4.0`/`1.4.1`, so on a 1.4.7 game the
  BUTR meta-loader fell back to the implementation built for game 1.4.1; it now selects 1.4.5.
- **MCM 5.11.4 → 5.12.1** — bumped the `Bannerlord.MCM` NuGet pin (both csprojs) + refreshed the vendored
  `Bannerlord.MBOptionScreen.v1.4.0`–`v1.4.5` + `MCM.UI.Adapter.MCMv5.dll`. 5.12.1 fixes the upside-down mod
  list that `Patch41_McmLayoutFix` works around; Patch41 is idempotent (only rewrites
  `VerticalBottomToTop`→`VerticalTopToBottom`) so it's a harmless no-op against the corrected prefabs — its
  removal is deferred to an in-game-confirmed cleanup.
- **UIExtenderEx 2.13.1 → 2.13.2** (both csprojs). **Harmony stays 2.4.2** (already current). Bumped the
  ButterLib + MBOptionScreen `.99` stub versions to match; polyfills/CrashReport/ModuleLoader verified
  byte-identical to current Workshop (no drift).

Build 0 errors, suite green (4212 passed); restored runtime DLLs confirmed UIExtenderEx 2.13.2.0 /
MCMv5 5.12.1.0 / 0Harmony 2.4.2.0. **Pending: close the launcher, deploy, and in-game-verify the MCM options
screen renders top-to-bottom** (and whether `[McmLayoutFix]` still logs flips — the signal to delete Patch41).

Save-compat: dependency DLLs + module metadata only; no save-data impact.

### fix(battle-tactics): SmartCavalryAI + MixedFormations no longer run outside open-field battles

A playtester's first siege hard-crashed to desktop — native CTD, no managed exception, nothing caught by
the crash pipeline — during OrderOfBattle formation distribution ~1s after the battle became playable
(engine v1.4.6.115628, TAOM v2.0.12; siege of Grymmclúd on `sturgia_castle_c`). Two TAOM formation features
had **no mission-type guard** and could manipulate formations mid-siege: **SmartCavalryAI** (`Patch31`, a
`Formation.SetMovementOrder` postfix) synchronously re-enters native `Formation.SetPositioning` /
`SetMovementOrder` on a player-team cavalry-classed formation; **MixedFormations** (`Patch30`, a
`Formation.GetOrderPositionOfUnit` prefix) overrides unit slots. Both are open-field-only by design. Gated
both on the engine's `Mission.IsFieldBattle` (true ONLY for `MissionTeamAIType == FieldBattle`; false for
siege / sally-out / hideout / naval / settlement missions — verified v1.4.7 `Mission.cs:1373`):

- **SmartCavalryAI** — added `IsFieldBattle` to `IBattlefieldQueryAdapter` (already injected into
  `CavalryChargeService`) and gated `HandleChargeOrder` + `Tick`; fully unit-tested (+2 regression tests).
  **Also gated `SmartCavalryAIMissionBehavior.OnMissionTick`** — its `ApplyCollisionAvoidance` writes
  `agent.SetMovementDirection` per mounted unit per frame *bypassing the service*, so the service gate alone
  left the feature still manipulating cavalry every frame in a siege (deep-review HIGH, caught by the
  data-flow agent — see the RCA). The tick gate covers both paths and skips the per-formation adapter build.
- **MixedFormations** — guarded the two thin entry points: the `Patch30` prefix (first line, before the
  ~40,000×/frame hot path allocates) and `MixedFormationsMissionBehavior.OnMissionTick` (suppresses both
  auto-layout and the manual cycle hotkey in non-field missions). Entry points are game-tested per ADR-008.

`/deep-review`: 1 HIGH + 1 MED + 2 LOW confirmed (all fixed or explicitly rejected with reason), 1 false
positive refuted. RCA: [`docs/reviews/rca-siege-guards-2026-07-16.md`](docs/reviews/rca-siege-guards-2026-07-16.md).
Verified against the engine on **both** v1.4.7 (dev) and v1.4.6 (the playtester's build) — the relevant
`Mission`/`Formation`/`SandBoxMissions` regions are byte-for-byte identical, so the gate is safe on their install.

Correct-by-design defensive guards — open-field features should never touch siege formations. They are **not
yet confirmed** as this crash's root cause (that awaits the player's Windows Event Log fault offset), and
SmartCavalryAI ships OFF by default so it could only have fired if the player opted in. A separate leading
hypothesis (a `cave_troll` garrison agent) was investigated and **de-prioritised**: the Armory snapshot shows
the troll is a valid humanoid with a non-degenerate collision capsule ("confirmed working in battle"), so a
troll fix would be speculative + gameplay-changing and is deferred pending an actual repro. Suite green (4214).

Files: `Main/Adapters/IBattlefieldQueryAdapter.cs` + `BattlefieldQueryAdapter.cs`,
`Main/Features/SmartCavalryAI/CavalryChargeService.cs`,
`Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs` +
`MixedFormationsMissionBehavior.cs`, `TAOM.Tests/Features/SmartCavalryAI/CavalryChargeServiceTests.cs`.
Research: `Mission.IsFieldBattle`, `SandBoxMissions` team-AI-type mapping.
Not-tested: Harmony prefix/postfix + MissionBehavior entry points (game-tested per ADR-008).
Constraint: `SiegeMissionNoDeployment` relief-force assaults are engine-tagged `FieldBattle`, so both features
still run there — acceptable (genuine maneuvering battle), documented rather than chased.

### feat(tools): UE 5.1 → Bannerlord conversion pipeline for the ElvenForestCity kit (Rivendell)

- The purchased "Environment 3D Cosmos — ElvenForestCity" kit ships as a UE 5.1 project (~511 static-mesh
  uassets + assembled city levels), not FBX. Three one-off scripts convert it into a Bannerlord Rivendell
  kit at `TAOM_Map/AssetSources/Scenes/Rivendell/`, following the Erebor kitbash conventions:
  - `tools/oneoff/ue_export_rivendell.py` — UE editor Python: bulk FBX export (UCX riding along, LOD0
    only), Texture2D → TGA, and a mesh→material→texture-parameter bindings JSON (parent-chain walk).
    Runs headless via `UnrealEditor-Cmd -ExecutePythonScript` once UE 5.1 is installed.
  - `tools/oneoff/blender_normalize_rivendell.py` — headless Blender batch: cm→m guard, lowercase
    `sm_rivendell_*` renaming (engine lowercases on import), `m_rivendell_*` slot renames, `bo_` collision
    twins (UCX/bo_ joined when present, decimated copy or hull otherwise; foliage skips), 150k-tri visual
    decimate cap, per-FBX meshlist dumps for a future kitbash builder, CSV report. Blender here is the
    MS-Store app — invoke via `blender-launcher.exe` (detaches; completion = `_normalize_report/DONE.txt`).
  - `tools/oneoff/convert_rivendell_textures.py` — metal-rough → spec-gloss: `t_rivendell_*_{d,n,s}.png`
    with `_s` = R:metallic / G:gloss / B:AO (EMPIRICAL — derived from the shipped Gondor/Mirkwood `_s`
    maps: metal lamps R bimodal 0/255, stone statue R=0, B tracks crevices). Metallic defaults OFF: the
    kit's ChannelMaps carry constant B=255, and reading that as metallic blacked out the diffuse in live
    testing — a constant-high metal channel is now refused even when requested.
- Smoke-tested without UE: converter verified on the kit's loose foliage textures (alpha masks kept);
  normalizer verified on a kit shrub + a Gondor building FBX (decimate 200929→149998, correct bo_ pairing).
- **Batch ran same day** (user installed UE 5.7.4 — content-only 5.1 project opens forward, export-only):
  460/460 meshes exported and normalized via 8 parallel Blender shards, zero errors (collision: 406 UCX /
  27 generated / 27 foliage-skip; 14 Nanite meshes hit the 150k-tri cap); 745/745 textures exported
  (20 TGA failures recovered as PNG via `ue_export_rivendell_fixup.py`); 217 spec-gloss sets converted.
  ORM packing confirmed from master-material parameter names via `analyze_rivendell_bindings.py`
  (31 masters; 82 `MM_RGB_Masking` meshes simplified to their Tileable_* triple; `_B` = BaseColor quirk).
- **Material naming (user decision, revised once):** FBX material slots are named after their texture
  set INCLUDING the `t_` prefix — material `t_rivendell_dining_set` binds `t_rivendell_dining_set_{d,n,s}` —
  a deliberate Rivendell-scoped departure from the kitbash `m_`/`t_` prefix split, chosen so editor
  material creation points straight at its textures. Same-set UE instances merged: 308 instances →
  **212 final materials** (`build_rivendell_material_sheet.py` → `material_rename_map.json` +
  `material_sheet.csv`, every texture ref existence-checked, 0 missing).
- **Material tpac generation** (`generate_rivendell_materials.py`): the Modding Kit's per-material
  `*_mtl.tpac` format was reverse-engineered from the user's hand-made materials (container layout =
  `tpac_skeleton_inject.py`; three 16-byte texture-GUID slots at meta offsets 106/126/146 for this
  shader config; the 8-byte checksum is **not validated by the editor** — copied verbatim from the
  template after a 9-algo × 7-slice sweep found no match; pilot validated in-editor before batching).
  All **196 texture-bearing materials generated** (never overwrites hand-made files; name-convention
  fallback binds same-named textures when the UE master bound no param). 16 translucent specials
  remain manual.
- Owed: mesh re-import (stale mixed-vintage _geo.tpac) + foliage material flags (alpha/two-sided —
  diff-one-then-batch) + in-editor smoke test → `docs/kitbash/rivendell/` catalog. Pass-2:
  scalar Metallic/Roughness for no-ORM sets, per-category tri budgets, emissive maps, full-city reference.

## 2026-07-14

### fix(settlement-guards): cave trolls no longer spawn as visible town/castle guards (#346)

- Root cause: vanilla `GuardsCampaignBehavior` fills its guard candidate list from the garrison filtered
  only on `Occupation == Soldier` and picks **weighted by troop level** — the L51 `cave_troll` (shed into
  Mordor garrisons by `kingdom_hero_party_mordor_template`) dominated the draw wherever no guard pool is
  configured (everywhere but Gondor), so trolls routinely manned Mordor guard posts.
- Fix: race-keyed guard-duty exclusion (`SettlementGuardService.IsRaceExcludedFromGuardDuty`, hardcoded
  `cave_troll`, validate-before-lookup via `IRaceManager`) enforced at two points: a new manual Postfix on
  `GuardsCampaignBehavior.InitializeGarrisonCharacters` scrubs `_garrisonTroops` in place (covers all five
  guard types; empty list → vanilla `culture.Guard`), and the existing TakeGuardAgentData Prefix rejects
  excluded-race config-pool entries. Fail-open: cached `AccessTools.Field`, one-shot warnings.
- Garrison membership and siege defense untouched (`MemberRoster` never modified); prison guards unaffected.
- Tests: +4 service (exclusion predicate incl. invalid-race-id gate), wiring catalog extended to the third
  manual patch site, binding gate pins `_garrisonTroops` + backfills `PrepareGuardAgentDataFromGarrison`
  (35 reflection rows, all resolve on installed v1.4.7). Suite 4210 green.
- Owed: in-game smoke (Mordor town with troll garrison, Gondor pool regression, siege defense), then close #346.
  Follow-up content issue: authored Mordor guard pool.

## 2026-07-13

### fix(balance): Gondor now out-armors Mordor at every tier (#342)

- User reports "Mordor has better armor than Gondor" confirmed against the design curve (Gondor +1 /
  Mordor −1 protection): the Black Uruk `sk_uruk_mordor_*_heavy_*` set (bracers 30, pauldrons 25/20)
  sat above even Gondor's ELITE kit, Mordor's elite chest/helmets tied Gondor's (50/40), and Gondor's
  keyword-tiered items sat at baseline+0 (authored before the +1 cultural mod existed). Troop-level:
  L31–36 uruks totalled 180–185 armor vs Gondor's standard 157; even L1 orc recruits out-armored L6 levies.
- Live-armory fix via `tools/oneoff/fix_gondor_mordor_armor_parity.py` (371 stat changes, backups
  `*.bak-parity-20260713`): Mordor-exclusive kit capped at the Mordor curve — the uruk heavy set
  re-statted as roster-ELITE (worn L26–36), per owner decision; Gondor baseline+0 stats topped up to +1.
  Shared orc pools (`sk_gn_orc_*`/`sk_md_orc_*`, worn by goblin/mistymountainorcs/isengard) and
  umbar/isengard items misfiled in `mordor/` left untouched — Gondor's +1 breaks those ties instead.
- Roster fixes (`troops_{gondor,mordor}.xml`): L6 levies get light helmets; L16 line infantry/nobles
  upgrade light gloves/greaves → medium; mirrored militia units topped up; L6 warg rider dropped to
  band-correct light kit.
- Verified: Gondor > Mordor at every slot×tier item max AND per-troop armor totals (median + max) at
  every shared level band. `validate_moduledata.py` PASS; analyzer unchanged (2 pre-existing elf flags).
- Owed: in-game smoke (needs game restart — item XML loads at launch), then close #342.

### fix(troops): skills now follow equipment — 73 troops corrected, weapon spec is equipment-driven (#340, #341, #344)

- Player reports: Tolfalas Sharpshooter (crossbowman) showed Bow 245 / Crossbow 50; Arndir Hill-Knight
  (two-handed-sword cavalry) showed Polearm 345 / Two Handed 165. Root cause: `rebalance_troops.py`
  detected weapon specialization from **name keywords only** — crossbowmen named Sharpshooter/Marksman/
  Scout/Sniper never got the Bow↔Crossbow swap (12 troops), and two-hander troops named Knight/Berserker/
  Champion inherited the polearm-biased Cavalry/Infantry baselines untouched (59 troops). The analyzer
  imports the same name-derived curve, so it structurally couldn't flag either.
- Tooling fix: `taom_schema.build_item_class_registry` (item id → skill class; reads BOTH vanilla
  `<Item Type>` and Armory `<CraftedItem crafting_template>` — zero `Type="TwoHandedWeapon"` items exist
  anywhere) + equipment-driven swap rules in `rebalance_troops.py` (writer hard-fails without the game
  install; analyzer degrades loudly). `naffatun` keyword removed (had swapped 2 javelin throwers).
  `harad_mumakil_rider` added to `SKIP_TROOP_IDS`. 10 contract tests in `tools/tests/test_rebalance_equipment.py`.
- Data fix via frozen-set one-off (`tools/oneoff/fix_skill_equipment_mismatch.py`): 73 troops across 11
  files, every write a pure pair permutation (aborts otherwise — protects the 5 hand-tuned
  `gondor_loss_noble*` residuals). 7 vestigial `bodkin_arrows_a` (arrows, no bow) stripped from the
  Gondor crossbow line. Save-compatible (values + equipment lines only, ids untouched).
- Name-vs-weapon equipment fixes (#344): 6 troops whose name promised a weapon they didn't carry —
  Balcoth Axeman line → Loke Axes I/II, Serelond Maceman line → empire maces t4/t5, Harad Spear
  Fighter/Guard → southern spears t3/t4 (Item0 swaps only; no skill ripple, generator dry-run identical).
- Deferred to #343: 108 one-hander-only troops with Polearm strictly top (+46 ties) need a 3-way
  redistribution decision; optional validator skill-vs-equipment WARNING check.
- Owed: in-game smoke (encyclopedia: Sharpshooter Crossbow-top, Hill-Knight TwoHanded-top, Balcoth
  Axeman wielding an axe).

### fix(arena): Patch62 — tournament-exit heap-corruption AV contained to a logged movie leak (#339)

- Player crash report (TAOM v2.0.12, Bannerlord 1.4.7, signature `4698b4d4`): CTD exiting a won
  tournament at arena_aserai_a — `AccessViolationException` in `Dictionary.FindEntry` via
  `WidgetFactory.IsCustomType` during the recursive `WidgetTemplate.OnRelease` walk of the Tournament
  movie. The AV fired twice on the same exit: Patch60's early release caught it (fail-safe → vanilla
  leak), then `GauntletLayer.ClearContext` re-walked the same corrupt tree at `ScreenManager.PopScreen`
  uncaught. Corruption pre-dates mission end (prize tableau render in flight at exit — the #331
  round-1 fingerprint); root cause is engine/native territory, this fix is containment.
- `Patch62_MovieReleaseAvGuard`: AV-only Harmony Finalizer on `GauntletMovie.Release` (Patch50
  pattern) — suppress + WARNING with the movie name, everything else propagates. Suppressing on the
  first (Patch60 → `ReleaseMovie`) attempt also drops the movie from `_movieIdentifiers`, so the
  fatal pop-time re-walk never happens. Cost on recurrence: one bounded leaked movie instead of a
  lost session. Cold path (once per movie lifetime). 4 behavior tests.
- CrashReport correlator fix: "Patches on throwing call stack" printed `(no patches)` for every
  Harmony replacement frame (`*_PatchN`) — `GetPatchInfo` is keyed on the ORIGINAL method. The
  collector now resolves frames via `Harmony.GetOriginalMethodFromStackframe` (verified present in
  shipped 2.4.2), so field reports name patch owners on exactly the frames that matter. 2 tests
  (live in-process Harmony patch; RED confirmed pre-fix).

## 2026-07-12

### fix(map): town_LN1 extra siege-ram slot — CTD joining any siege at Rivendell town

- Player crash bundle `4d003ae6` (TAOM v2.0.10, Bannerlord 1.4.6): `IndexOutOfRangeException` in vanilla
  `SettlementVisualManager.TickSiegeMachineCircles` every campaign-map frame while participating in a siege
  at town_LN1. Root cause: the live TAOM_Map `Main_map/scene.xscene` gave town_LN1 **two** `map_siege_ram`
  + two `map_siege_tower` slot entities = 4 attacker melee frames, while the engine hardcodes
  `SiegeEvent.SiegeEnginesContainer.DeployedMeleeSiegeEngines` to length 3 (1.4.6 + 1.4.7 verified) — the
  tower loop indexes `[ramFrames + towerIdx]` → `[3]` → IOORE → CTD. Three unguarded consumer paths share
  the frames (circle tick, engine-visual tick, `MapSiegeVM` deploy UI); all TAOM C# exonerated (zero
  patches on the crash stack).
- Fix (external TAOM_Map module, not in repo): removed the duplicate ram entity (in-editor) → town_LN1 is
  now 1 ram + 2 towers like the other 220 fortifications. Full-map audit: 221 fortifications, ram census
  221, tower census 442, **zero** engine-cap violations or shape deviations remaining.
- Cosmetic cleanup in the same pass: 16 fortifications carried wrong `map_defensive_engine_*` suffixes
  (15 with all four slots tagged `_3`, castle_ES1 `_0,_2,_3,_3` — suffixes only feed the slot sort order,
  counts were all exactly 4, no crash potential). 5 fixed in-editor; the remaining 11 (town_E2/E3/E4,
  town_RU1/RU3–RU8, castle_ES1) retagged `_0.._3` by scripted byte-surgical digit swap preserving the
  current effective sort order (file length unchanged). Defender tag census now uniform: 221 × each of
  `_0`–`_3`. Backups: `scene.xscene.bak-20260712-{siege-ram-fix,suffix-fix}`.
- Existing saves are safe (scene entities aren't serialized; no save can reference a 4th melee slot —
  the campaign array was always length 3). In-game smoke owed: join a siege at Rivendell town, ≥30 s on
  the map with the siege overlay active, confirm 1 ram + 2 tower circles and no CTD.

### docs(tests): test-mirror gap assessment — 3 of 4 flagged gaps are non-gaps (repo-reorg Track D)

- The reorg audit flagged 4 `Main/Features/` dirs without `TAOM.Tests/Features/` mirrors. Assessed:
  **ElephantLike** — covered by proxy (`ElephantAttackServiceTests` + `MumakilAttackServiceTests` exercise the
  shared `ElephantLikeAttackService` through both bindings; a mirror dir would duplicate). **BattleScenes** —
  3 thin Harmony hooks on a DISABLED feature (entry points: "test via game" per ADR-008). **CharacterSelection** —
  one shipped transpiler (`Late_Transpiler`), same entry-point category. **MissionDiagnostic** — the one real
  item: `MissionDiagnosticService` (173 lines) has no tests; the two snapshot methods read live engine state
  (boundary, not unit-testable) but `LogActionSetSeen`/`ResetForNewMission` dedup logic is testable — deferred
  as test debt (writing C# was out of the reorg's scope; pick up with the next MissionDiagnostic change).

### refactor(agents-md): rolling essay log — 25 per-review essays archived, catalog kept (repo-reorg Track D)

- AGENTS.md's "Lessons From Prior Reviews" held 25 verbatim per-review essays (~53 KB) PLUS the ~90-pattern
  distilled catalog (bugs-Codex-misses / false-positives / what-Codex-does-well) that already harvested their
  lessons. The essays moved verbatim to `docs/reviews/agents-md-review-lessons-archive.md` (complete history,
  newest first); AGENTS.md keeps the newest 5 essays + the FULL catalog (the operative reviewer calibration)
  + intentional-patterns + harness-review notes. **New rolling convention:** each review cycle adds its essay
  at the top, rotates the 6th-oldest to the archive, and harvests durable patterns into the catalog +
  `docs/reviews/lessons/`. AGENTS.md 149.7 KB → 110.8 KB (−26% of Codex's per-review context).

### chore(harness): Track D follow-ups — fresh context baseline, scan fix, hook recency window, description trims (repo-reorg)

- **`docs/context-budget-baseline.md` re-baselined** (April's was 8x stale): eager startup excl. MCP =
  ~26K tokens (CLAUDE.md 14.2K + always-load rules 8.9K + skill/agent descriptions + MEMORY.md).
  `scan.sh` fixed to split always-load vs paths-gated rules (pre-fix it counted all 22 rules eager,
  +18K phantom). The 59.4K MCP figure is CONDITIONAL — this session observed schemas DEFERRED behind
  ToolSearch (eager ~0); the baseline documents the levers (unauthenticated github/imagine, ilspy vs
  taom-src overlap) as user decisions if another session type proves eager.
- **`check-deep-review.sh`** mute-grep now recency-scoped (last 8h, awk timestamp filter, fail-open to
  the old whole-file grep) — months-old audit entries had permanently silenced the reminder; stale
  "Bannerlord 1.3" in the message fixed.
- **Skill descriptions**: `lint-cleanup-loop` 42→22 words, `taom-src` 31→23 (the ≤30 cap).

### refactor(claude-md): secondary slims + budget gate flipped to ENFORCE (repo-reorg Tracks C7+C8, decomposition complete)

- **Section slims:** Skills table -> routing-only one-liners (descriptions already load eagerly from SKILL.md
  frontmatter — the fat table double-charged); Native C++ port discipline -> new paths-scoped rule
  `.claude/rules/native-cpp-ports.md`; inline-hook-activation -> pointer to harness-facts; GitHub/KB
  templates + the 13-step completion sequence -> `docs/ai-includes/completion-workflow.md` (verbatim,
  CLAUDE.md keeps the mandates); decompile folder-layout table + wEditor warning -> merged into
  `docs/reference/bannerlord-engine-and-toolchain.md`; Localization prose -> 4 bullets + guide link;
  Doc Lookup / Skill Routing / Scoped Rules / Hooks / Equipment over-cap rows trimmed. Fixed live
  version-drift while in there (the taom-src paragraph still said "installed v1.4.6" / "v1.4.5 dump" —
  now version-agnostic via the pin).
- **Scoped Rules table:** `hook-authoring.md` + `native-cpp-ports.md` registered (follow-up commit).
- **Config-row consolidation:** 16 per-feature config rows (SettlementFood config, CaravanTrade config,
  CareerSystem sprites, ...) dropped after verifying each path is documented in its feature doc's
  Configuration section; one umbrella row remains.
- **Budget gate ON (calibrated):** `CLAUDE_MD_BUDGET_ENFORCE = True` — `--fail-on-drift` (the pre-commit
  hook) now hard-blocks budget violations. **Cap recalibrated 60 KB -> 100 KB hard / 95 KB warn:** the
  plan's 60 KB estimate predated recovering 17 missing Harmony categories and assumed fewer/shorter
  index rows; the decomposition's honest floor at one-line density with all 85 Key Paths + 65 Harmony +
  40 GameModel rows kept is ~91 KB. Gate proven end-to-end (pass at 91 KB -> forced-fail at a 50 KB cap ->
  pass restored). **Net: CLAUDE.md 174 KB -> 91 KB (-48%, ~15-20K tokens per session + per agent spawn),
  zero verified information loss, regrowth hard-gated.**

### refactor(claude-md): Key Paths verify-merge — 34 essay rows → one-liners + doc links (repo-reorg Track C5)

- The Key Paths table carried a 200–3,400-char compressed restatement of each feature's doc (52 KB of the
  file). A 28-agent verify-merge pass processed the 34 over-cap rows: each agent diffed its row's claims
  against the destination doc, **appended anything missing to the doc first** (e.g. QuickActions' thread-static
  vanilla-bypass + `TransactionCount` mechanics, NavalTravel's navmesh-stays-enabled rationale, Messengers'
  `MapCoord` ADR-007 row, SettlementFood's siege-gating rationale), then produced the ≤400-char thin row.
  Load-bearing flags stay in-row: PARKED/DISABLED + re-enable pointers (NavalTravel, NativeSkinFixes),
  the TAOM_Map LIVE-vs-stale-shadow warning, the vendored-DLL allowlist (`/improve` depends on both).
- **`docs/features/mcm.md` authored** (the doc-gap hook's standing flag): Patch41 on UIExtenderEx
  `WidgetFactoryManager.CreateAndRegister` flips MCM's 5 embedded prefabs to top-to-bottom layout;
  grounded in `f23434b0`/#252. INDEX.md gains mcm + the previously-unlinked save-load-diagnostics rows.
- **Harmony registry completeness:** a code sweep found 15 patch categories in `Main/**`
  `[HarmonyPatchCategory]` attributes that the old CLAUDE.md table never listed (Patch13 RaceAge,
  Patch25 LocalizationOverride, Patch30 MixedFormations, Patch33 EquipPresets, Patch35 CompanionTactics,
  Patch36 FiefManagement, Patch37 CrashReport, Patch39 BanditPartySize, Patch41 McmLayoutFix,
  Patch43 BattleLoadDiagnostics, Patch51 RecruitmentResourceGate, Patch55 BasicTableauRaceGuard,
  3× Patch61 reflection sub-categories, + `Late_*` ×2) — documented in the registry from the actual
  patch files; the CLAUDE.md/AGENTS.md thin tables gain their rows. (Reverse check: Patch28 + Patch31
  legitimately carry no category attribute — manual patches.)
- CLAUDE.md 140.6 KB → ~107 KB; budget findings 51 → 17.

### refactor(rules): harness-facts.md split — durable facts stay always-load, authoring lore goes scoped (repo-reorg Track C6)

- `harness-facts.md` (23.5 KB, always-load) held durable harness facts AND authoring-time conventions +
  incident write-ups. Split: hook-authoring conventions (sibling-mirroring table, git-invocation-forms +
  two-stage matcher, amend exemptions, + a new log-rotation convention) → new **paths-scoped rule
  `.claude/rules/hook-authoring.md`** (loads only when a `.claude/hooks/` file is open); the parallel-port
  build-watcher saga, CombatMechanics builder-brief seam findings, and worktree evidence/invocation detail →
  `docs/ai-includes/agent-teams.md` "Case studies" (verbatim). Distilled rules (worktree isolation
  when-to-apply, builder-briefs checklist, watcher prevention) stay always-load with pointers.
  harness-facts.md 23.5 KB → 14.6 KB (−9 KB eager per session); "Last verified" bumped to 2026-07-12.

### refactor(claude-md): GameModel Overrides rows capped at one line (repo-reorg Track C4)

- All 40 GameModel rows stay (the table is the routing map for "which model owns X"), but the 5 rows
  that had grown into 400–1,100-char essays (`TaomPartySizeModel`, `TaomPartyNavigationModel`,
  `TaomMarriageModel`, `TaomSettlementEconomyModel`, `TaomCombatMechanicsModel`) are now one-liners +
  feature-doc links — each evicted claim verified present in the linked doc before thinning (grep-checked:
  limit-deflation mechanism, PARKED re-enable steps, wraith marriage overrides, `GetTownGoldChange`
  scope + engine-bump note, CTB/cleave/knockdown mechanics). Budget findings 56 → 51.

### refactor(claude-md): Harmony table → docs/reference/harmony-patch-registry.md, thin routing residue (repo-reorg Track C3)

- The 25.6 KB Harmony Patch Categories table (48 categories; the fat rows were 400–3,000-char essays) moved
  **verbatim** into `docs/reference/harmony-patch-registry.md` (one `## PatchNN` section per category, with
  Target + full rationale/history/RCA links). CLAUDE.md keeps a 4-column thin routing table
  (Category | Feature | exact Target signature | Status) — stack-trace→owner routing stays eager;
  PARKED/DISABLED flags stay load-bearing in-row. CLAUDE.md 160.8 KB → 142.8 KB; budget findings 75 → 56.
- Corrections while in there: `Patch15_BannerLayerLimit` now shows **DISABLED (engine-native since
  v1.4.7)** — the old table predated the bump; `AGENTS.md`'s Harmony snapshot (stale: ended at Patch22,
  wrong Patch17 target) replaced with the current thin table + registry pointer (registry = single
  maintained source).
- `.claude/rules/harmony-patches.md` (paths-scoped: loads when editing hooks) gains the read-the-registry
  step, the **`Patch_MissionTime_SetMovementOrder` mandate** (any `MovementOrder`-signature postfix must
  join the deferred category — `MovementOrder.cctor` reads `Mission.Current`), apply-timing guidance, and
  its stale "v1.4.5"/"Patch0 through Patch6" lines fixed. `submodule-lifecycle-and-harmony.md` citation +
  INDEX.md row updated.

### refactor(claude-md): Rebalancing Tools table → tools/README.md union-merge (repo-reorg Track C2)

- The 13.4 KB / 42-row Rebalancing Tools table duplicated (and in 23 cases was the ONLY home of) per-tool
  documentation. **Union-merged into `tools/README.md`:** 23 missing tools added (armor authors #211,
  troop revamps #212 + polish #224 as a new "Troop revamps" section, `extract_perks`/`analyze_lord_balance`/
  `analyze_troop_balance`, starter-armor pair, `raise_party_template_maxes`, `validate_gondor_refs`,
  `rollback_erebor_iron_misfile`); overlapping rows enriched with the CLAUDE.md-only gotchas
  (**battania=khand** CULTURE_MAP, clean-tree-regen pre-flight + unsafe per-culture re-resolution,
  `detect_culture` elite-line routing + militia-L21-by-design, iron_hills canonical folder, elf-lord
  tier-cap math, engine-ignores-inline-skills). Verified: all 42 table tools now resolve in README.
- CLAUDE.md residue: a 3-line "Rebalancing & Data Tools" pointer (catalog + preferred validators +
  analyze-before-apply rule). CLAUDE.md 173.8 KB → 160.8 KB; budget findings 88 → 75.
  `author-armor` skill repointed to the README sections.

### feat(lint-docs): CLAUDE.md eager-load budget check — warn-only until the decomposition lands (repo-reorg Track C1)

- CLAUDE.md loads into every session + agent spawn; it hit 174 KB (~30K tokens, 8× its April baseline)
  because feature prose kept accreting into table rows. `check_claude_md_budget()` in `tools/lint_docs.py`
  now enforces: **≤60,000 B file** (55 KB warn), **≤400-char table rows**, **≤600-char prose lines**
  (fenced blocks exempt). New `budget` report section + `claude_budget:` summary line; wired into
  `--fail-on-drift` behind `CLAUDE_MD_BUDGET_ENFORCE` (False = warn-only during the Track C migration,
  flipped at C8). `check-doc-config-drift.sh`'s detail-extraction + deny message cover the new section.
- Current reading: 88 findings (1 size + 87 over-cap rows/lines) — the migration's progress meter.
- Preamble trimmed to the pin + doc pointers (the baseline-dump history it carried is in
  `docs/migration/v1.4.7-impact.md`); the `Target: Bannerlord 1.4.7` line stays verbatim-parseable
  for `check_version_consistency` (version_mismatch still 0).

### refactor(docs): split LESSONS-LEARNED.md into per-category files under docs/reviews/lessons/ (repo-reorg Track B)

- The master lessons record had reached 371 KB / 206 lessons in one file — the review skills' "read the
  relevant category first" step meant loading (or section-hunting) the whole thing. **All 206 lessons moved
  VERBATIM** (script-verified: per-category `###` counts sum to the source's 206) into 13 files at
  `docs/reviews/lessons/<category>.md` (6–54 KB each); `LESSONS-LEARNED.md` stays at its path as a thin
  index (3.4 KB — intro, house shape, linked ToC with per-category counts) so every historical
  "LESSONS-LEARNED 'Category'" prose citation still resolves.
- Each category file carries an append-here header with the house shape (`### rule` → Why missed →
  Prevent → Source). Read/append instruction sites updated: `/deep-review` (Phase 3e), `/review-codex`
  (Phase 3e), CLAUDE.md Doc Lookup row, harness MEMORY.md.

### chore(tools): segregate finished one-offs into tools/oneoff/ (repo-reorg Track B)

- 33 scripts referenced by **no living doc** (checked `tools/`-prefixed AND bare-name mentions across
  CLAUDE.md, AGENTS.md, `.claude/`, docs/ai-includes+features+migration+reference, INDEX, tools/README,
  .github; plus cross-import + subprocess-invocation scans over `tools/*.py` + `tools/tests/`) moved via
  `git mv` to `tools/oneoff/` — per-culture clan/lord authors, v1.4.x migration fixes, kitbash test
  builders, dao-rock scene one-offs. Zero dangling references confirmed post-move; lint unchanged (11).
- 14 candidates **kept** in `tools/` on evidence: bare-name README/rule/doc references (`audit_item_refs`,
  `repair_sav_strings.ps1`, the faction-map trio — also pending the unmerged `impl-005` edit to
  `process_faction_map.py`), plus living-but-undocumented `analyze_reviews.py` + `spider_render_triage.py`,
  which gained README rows ("Review analytics").
- **Convention (new):** one-off scripts land in `tools/oneoff/` when their job is done — documented in
  `tools/README.md` § One-offs + a CLAUDE.md Key Paths row.

### chore(changelog): roll 2026 H1 entries to docs/changelog-archive/ (repo-reorg Track B)

- Root `CHANGELOG.md` had grown to 1.49 MB / ~11.9K lines (112 date sections since 2026-01-24) —
  grep noise + a heavy read for any session that opens it. Entries 2026-01-24 → 2026-06-30
  (~10.7K lines) moved verbatim to `docs/changelog-archive/CHANGELOG-2026-H1.md`; root keeps
  July+ (~1.3K lines) with an archive pointer under the header.
- Hook compatibility verified: `check-changelog-updated.sh` (substring on diff names) and
  `check-changelog-changed.sh` (exact-match on root path) both key on the root file, which stays;
  `session-start.sh` prints only the first date section. **Roll cadence: each Jan 1 / Jul 1.**

### chore(hooks): size-capped rotation for the two unbounded .claude/logs writers (repo-reorg Track B)

- `session-stop.sh` rotates `session-log.md` at 1 MB → `.1` generation (was 2.8 MB, unbounded since
  March); `log-agent.sh` rotates `agent-audit.log` at 256 KB → `.1` (was 270 KB). One previous
  generation kept; both verified live (real oversized logs rolled on first trigger).
- Side benefit: `check-deep-review.sh` greps `agent-audit.log` for deep-review evidence — with months
  of unrotated history the reminder was permanently satisfied; rotation restores a recent window
  (session-scoped filtering remains an optional follow-up).
- `/context-save` Storage notes now tell the saver to prune >30-day snapshots whose work has landed
  (the 18 stale 2026-05-13 phase snapshots this session deleted were the motivating case).

### chore(reviews): retention policy — raw Codex transcripts move to gitignored docs/reviews/raw/ (repo-reorg Track B)

- **Problem.** `docs/reviews/` had grown to 43 MB / 265 git-tracked files, ~36 MB of it raw Codex stdout
  transcripts (2–4 MB each) accumulating ~100 files/month with no retention scheme — repo bloat + grep noise
  on every review-history search.
- **One-time sweep.** 73 raw outputs (`codex-adversarial-*` non-prompt + `codex-prereview/selfreview/result-*`)
  untracked (`git rm --cached`) and moved to `docs/reviews/raw/` (new, gitignored). Files stay on disk;
  history keeps the old blobs (only ~1.5% of the pack — rewrite pointless). Deleted `_issue_body_tmp.md`.
- **Kept committed** (the durable record): all 71 prompts (`*.prompt.md` + legacy `codex-prompt-*`), all
  `rca-*.md`, `LESSONS-LEARNED.md`, `REVIEW-LOG/GUIDE`, adopt/audit docs. 14 RCA/REVIEW-LOG links repointed
  to `raw/…` (resolve on-disk; dead in a fresh clone — accepted, the distillate is the record).
- **Future flow.** `/review-codex`, `/codex-verify`, `/deep-review` now dispatch `codex exec` output to
  `docs/reviews/raw/` (`mkdir -p` guard for fresh clones); prompts still commit. Retention section added to
  `REVIEW-GUIDE.md`.

### chore(repo): remove tracked root scratch + relocate legacy scripts/ (repo-reorg Track B)

- **Removed from tracking** (regenerable or one-off artifacts committed by accident): `SPOrderOfBattleVM.tmp.cs`
  (scratch decompile), `mordor-lords.html` (one-off lords viz), `out/0Harmony.decompiled.cs` (497 KB stale
  decompile — `/taom-src` regenerates on demand; `out/` was already gitignored), `report.json` (empty `[]`;
  regenerates via `tools/validate_moduledata.py --json report.json`, now gitignored as `/report.json`).
- **Moved** the 11 legacy Jan–Mar lords-migration one-offs `scripts/` → `tools/oneoff/lords-migration/`
  (one-off scripts now live under `tools/oneoff/`); repointed the 5 references in
  `docs/migration/{SESSION-S5a-S5b-PROMPT,TRACKING,v1.4.x-equipment-overhaul,v1.4.x-taom-impact}.md`.
- `lint_docs.py`: 0 new dead links (13 pre-existing, unrelated); drift checks clean.
- Part of the approved 2026-07-11 repo-reorg plan (Track B item 1 of 6).

## 2026-07-11

### balance(mordor): make Black Uruks rarer in recruitment and lord parties

- **Recruitment.** Dropped the `mordor_uruk_grunt` (Black Uruk Grunt) weight **3 → 1** in the Mordor
  town pool + culture fallback (`VolunteerRecruitmentService.Mordor.cs`), taking the recruitable Black
  Uruk from **20% → ~7.7%** of a town's volunteers. Castles were already 0% (unchanged). The
  "Morannon more plentiful than Black Uruks" invariant still holds (5 vs 1).
- **Lord parties.** Set every `mordor_uruk_*` stack to `min_value="0" max_value="8"` (from `50`) across
  the 16 Mordor lord templates — the generic `kingdom_hero_party_mordor_template` + the 15
  `kingdom_hero_party_mordor_empire_south_1…15_template` (71 stacks). The engine's seed-fill weights
  each troop by `min + (max−min)×ratio` with every other Mordor stack at `max=50`, so **`max_value` is
  the proportion lever, not `min_value`** — dropping uruk `max` 50→8 cuts the Black Uruk share of a
  freshly-spawned Mordor lord army from ~32% to ~7%; orcs/wargs/Morannon become the bulk.
- **Scope.** Mercenary / outlaw / patrol / rebel Mordor templates left as-is (per user). No troop ids
  changed → save-clean. Recruitment DataRow tests re-baselined to the new 13-total pool; full suite
  green (4198), `validate_moduledata.py` PASS.

### fix(shader-precompilation): 1.4.7 deployment-NRE — precompile stuck on 1.4.7 (#336)

- **Symptom.** On Bannerlord 1.4.7 the main-menu **Pre-compile Shaders** walk got stuck indefinitely;
  worked on 1.4.6. A user debugger caught `NullReferenceException` at
  `DeploymentMissionController.SetupTeams():173`, thrown every mission tick.
- **Root cause — a 1.4.7 engine regression, not TAOM code.** 1.4.7 added an **unconditional** deref of
  `Mission.InitialPlayerAgent` to `DeploymentMissionController.SetupTeams()`/`FinishDeployment()` (the new
  `AgentControllerType` hand-control). That field is set only when an agent builds with `Controller ==
  Player` (`Mission.cs:4024`); the precompile custom battle is **headless** (no human), so it stays null
  and the deref NREs. 1.4.6 had no such deref. Managed shader APIs are byte-identical across the bump.
  Scoped to precompile — every real battle has a player agent, so normal play is unaffected.
- **Fix.** `ShaderPrecompilePlayerAgentGuard` (`MissionLogic`, added only during a walk via
  `SubModule.OnMissionBehaviorInitialize` gated on `ShaderPrecompileRunner.IsWalkInProgress`): seeds
  `InitialPlayerAgent` on the first agent build (before the deref) + force-finishes the OoB deployment so
  the headless battle doesn't freeze waiting for a *Deploy* click. Reflection write of the private field is
  drift-guarded by `ReflectionSiteBindingTests`.
- **Robustness package** (bounds any future stall regardless of cause): per-item-kind decider caps (a scene
  pass bails at 8 min instead of the 90 min the character battle needs), a **churn backstop** (a count that
  changes every frame but never returns to 0 now aborts — the old frozen-count guard missed it),
  self-classifying abort logs (`AbsoluteTimeout`/`FrozenCount`/`ChurnTimeout`), and a **Ctrl+Shift+K**
  in-game cancel.
- **Verified.** In-game 1.4.7: `WALK COMPLETE — 13 items in 8m 6s`, 0 NRE, 0 hang; the seed fired on all
  12 deployment missions. 7 new/updated unit tests; full suite green. Deep-reviewed (5 agents: 0 functional
  defects). RCA `docs/reviews/rca-shader-precompile-1.4.7-2026-07-11.md`.
- **Known caveat.** The successful run completed the character battle in 20s on a warm shader cache, so the
  force-finish path for that item wasn't exercised (its `InitialPlayerAgent` was non-null and it settled
  before deployment mattered); a cold-cache run would additionally validate it. #336 stays OPEN for that.

### fix(animation): give every race the full civilian action-set family (elves shared one town idle)

- **Symptom.** Elf NPCs in every town all played the *same* idle animation. **Root cause:** a settlement
  NPC's idle-role animation comes from a GENERATED action-set name `as_<race>[_female]_<suffix>`
  (`villager`/`lord`/`beggar`/`guard`/carry-prop/`map`…, via `ActionSetCode.GenerateActionSetNameWithSuffix`);
  when `as_<race>_<suffix>` is absent the engine silently falls back to ONE default set. Elves shipped with
  ONLY `as_elf_facegen`/`_female_facegen` (Character-Creation), so all 82 civilian roles collapsed to one idle.
- **Wider audit (per user request — "check each race").** The gap wasn't elf-only: **every** non-human race
  was also missing the same 3 prop-carry sets (`villager_carry_bucket_on_lefthand`, `villager_carry_fish_buckets`,
  `worker_carry_wood_on_shoulder`).
- **Fix — data, in LOTRLOME_Armory `action_sets.xml` (live) + the tracked snapshot.** 194 thin `base_set`
  aliases in a `TAOM-CIVILIAN-COVERAGE` block: elf + sauron get the full 82 each, the other 10 non-human races
  their 3 missing carry sets. Human-skeleton races (elf/sauron/orc/uruk/…) alias to `as_human_<suffix>` (correct
  role animation, shared skeleton); dwarf (own skeleton) aliases to `as_dwarf_villager` (never a human clip on
  the dwarf rig, the 1.4.6 water-CTD class). No C# change — the existing
  `ActionSetCode_GenerateActionSetNameWithSuffix_Patch` already emits `as_elf_villager`; the fix makes it resolve.
- **Tooling (new).** `tools/audit_civilian_action_set_coverage.py` (read-only per-race coverage vs human, exits
  non-zero on a gap) + `tools/generate_race_civilian_action_sets.py` (idempotent alias generator, `.bak`, dry-run
  default). Re-run both after every engine bump / LOTRLOME update (added to the snapshot-README discipline).
- **Verified:** both files parse; coverage audit 43/43 + 39/39 for all 13 settlement races;
  `audit_action_set_parity.py` 0 humanoid gaps; generator byte-idempotent (re-apply → identical hash). Trolls
  excluded by design (never townsfolk). **In-game verification owed** (elf-CC RCA rule: XML animation fixes must
  be confirmed live) — visit towns with elf/orc/dwarf populations, confirm varied male + female idles.
- **Reviewed (3-agent adversarial pass).** Completeness agent decompiled every engine caller of
  `GetActionSetWithSuffix` (townsfolk/notable/hero-spawn/disguise/carry-item helpers) and confirmed the 43+39
  reference == the full set of generated civilian suffixes — no role falls back. Regression agent cleared
  facegen / active-patch / save / duplicate-id interactions and disproved the T-pose risk (civilian sets DO
  inherit `base_set`, unlike the facegen path). Tooling agent found only latent re-run hazards (own-skeleton
  race lacking its own villager → self-alias; no dangling-abort; fragile skeleton detection) — all fixed
  in-session: explicit `OWN_SKELETON_RACES`, dangling → refuse-to-write, empty-native + non-UTF-8 guards,
  multi-block dedup, self-reference skip.

### fix(caravan-trade): stop caravans leaving a town and immediately returning

- **Two root causes, both confirmed against the decompiled v1.4.7 engine.** (1) The shipped
  "anti-shuttle penalty" was **inert**: it keyed on `caravanParty.LastVisitedSettlement`, which is set
  only on settlement *enter* (`MobileParty.cs:602`) and never cleared, and the caravan re-decides its
  destination *while still parked* — so at decision time `LastVisitedSettlement == CurrentSettlement`,
  the town vanilla already excludes from candidates (`CaravansCampaignBehavior.cs:923`). The penalty
  never fired on a selectable town. (2) The home town was **exempt from the distance re-weight**, so it
  kept vanilla's full `1/days` near-field spike + growing `num5` gravity while every neighbor was
  compressed — a caravan homed at a hub (e.g. Minas Tirith) re-selected home the moment it parked at any
  neighbor, reading as "leaves and immediately returns."
- **Fix.** New per-caravan **visit memory** (`ICaravanVisitMemory` + thin `CaravanVisitMemoryBehavior`
  on `SettlementEntered`/`MobilePartyDestroyed`, no `SyncData`) records the last 4 towns each caravan
  entered and yields a recency penalty that deprioritizes just-visited towns — targeting genuinely
  *selectable* towns, unlike the old `LastVisitedSettlement` check. The penalty is a strictly-positive
  multiplicative floor (never a hard exclusion → no stranding in sparse regions), routed *into*
  `ReweightTradeScore` so the `IsActiveFor` player-scope gate governs it. The home town is now
  distance-compressed like any other (`homeDistanceReweight`, default on), which loses its proximity
  edge while preserving vanilla's upstream `num5` home-gravity — caravans still return home on the
  payout cadence. **Verified safe:** `DefaultClanFinanceModel.AddIncomeFromParty` pays the owner
  regardless of caravan location, so home-compression cannot starve caravan income.
- **Config.** Repurposed the (previously inert) `antiShuttlePenalty` knob as the recency-penalty
  strength (default `0.35 → 0.5`); added `homeDistanceReweight` (default `true`) as a JSON escape hatch
  to restore the old home exemption if playtest shows home visits are too rare. Both JSON-only.
- **Known residual:** the recency memory enlarges the loop to ~5 distinct towns rather than guaranteeing
  map-wide circulation; tunable via `antiShuttlePenalty`. In-game playtest owed (home-return frequency is
  the one thing unit tests can't settle).

Research: `CaravansCampaignBehavior` (`FindNextDestinationForCaravan`/`GetTradeScoreForTown`/`num5`), `MobileParty.LastVisitedSettlement`, `DefaultClanFinanceModel.AddIncomeFromParty`, `CampaignEvents.{SettlementEntered,MobilePartyDestroyed}` (installed v1.4.7).
Save-compat: no new SyncData — ephemeral memory, rebuilds as caravans move; master-off = exact vanilla.
Not-tested: the Harmony postfix + behavior invocation (requires a live campaign) — the pure memory + reweight services are unit-tested (80 CaravanTrade tests green).

### refactor(troop-weight): move the "elite tax" from the member count to the party-size limit — raw counts everywhere

- **Player-facing fix.** Troop counts were confusingly inflated: a party could show **325** on the party
  screen / **407** "Land Troop Capacity" while only **159** fought in battle and showed on the map. That
  gap was the TroopWeight feature weighting the member COUNT (`NumberOfAllMembers`) so heavy troops cost
  more party-size budget — the weighted number leaked into every count display.
- **The rework.** The weighting now lives on the party-size **limit** instead of the count. `TaomPartySizeModel`
  subtracts the weight surplus (`ceil(weighted) − raw`) from the limit via
  `ITroopWeightService.ApplyPartySizeWeightPenalty` (pure, unit-tested `ComputeSizePenalty`, clamped so the
  limit stays ≥ 1). Result: **every count reads raw everywhere** (map nameplate, party screen, land-capacity,
  tooltips, menus, battle all agree), while the recruit cap still fills at the troop weight. The displayed
  *limit* honestly shrinks as you stack elites (`150 / 240` instead of `150 / 300`) — no invisible recruit wall.
- **Removed (~26 files):** the two `NumberOfAllMembers`/`NumberOfRegularMembers` getter patches + hooks, the
  5 weighted-display hooks (phantom-wounded fix — now moot since nothing is weighted in display), the
  `WeightedCountCache`, and the temporary `[CountFlicker]` diagnostic (its job — proving the map "200↔20"
  flicker is the vanilla army-sum, not the weighting — is done). Shed-on-upgrade stays, adapted to the
  deflated-limit frame.
- **Blast-radius handling.** Unpatching a global getter changes every consumer of the weighted count:
  `SpecialResources` battle-reward scaling is **preserved** (switched to an explicit weighted-count call);
  `SettlementFood`'s garrison-leak correction self-neutralizes to zero (vanilla food now reads raw at
  source — net food unchanged); incidental side-effects on other engine consumers (e.g. elite parties
  moving slightly slower) are intentionally gone — the feature now affects only the size cap.
- New player-facing string `{=taom_troop_weight_size}Heavy troops` (party-size tooltip label) — renders via
  its inline default; **`/localize` owed** to propagate to the 11 AI-translated languages.
- Behavior changes flagged for review: shrinking displayed limit; slightly different intermediate
  party-fill ratio (recruit cap unchanged); elite-party incidental effects removed.
- **Deep-review (5 agents) fixes**, all from the cross-system data-flow trace: (1) the shed hook recovered
  a lossy `deflated + surplus` base that overshot when the penalty clamped — it now reads the exact
  pre-penalty base via `GetTrueBaseSizeLimit` (cached), so it no longer under-trims heavy post-upgrade
  parties; (2) SpecialResources battle-reward scaling re-gated on `EnableTroopWeight` (weighted on / raw
  off) — the removed getter patch used to gate it, so "off = vanilla" was briefly broken; (3)
  `TroopWeightXmlLoader` now rejects `weight="NaN"`/`"Infinity"` via `FiniteFloatValidator`; (4) stale
  "Patch17-weighted" comments in SettlementFood + TroopShedPlanning corrected. RCA:
  `docs/reviews/rca-troopweight-count-to-limit-2026-07-11.md`.

Research: `MobileParty.PartySizeRatio`/`PartyBase.PartySizeLimit`/`GetPartyMemberSizeLimit` (installed v1.4.7).
Not-tested: the GameModel invocation + shed Harmony postfix (require a live campaign) — the penalty clamp math + NaN-loader rejection are unit-tested; full suite green (4199).

### fix(troop-weight): reference-key the count caches + add a map-nameplate flicker diagnostic (superseded same day)

- **Confirmed defect fixed.** Both count-getter hooks (`PartyBaseNumberOfAllMembersHook`,
  `PartyBaseNumberOfRegularMembersHook`) cached their weighted result in a process-global
  `Dictionary<int,…>` keyed by `partyBase.GetHashCode()`. `object.GetHashCode()` isn't unique per
  instance, so two parties that collided AND shared a `MemberRoster.VersionNo` read each other's
  weighted count — the latent hazard flagged in `rca-troopweight-phantom-wounded-2026-06-07.md` §2
  and never back-ported from the display path. Replaced with a shared, reference-keyed
  `WeightedCountCache<PartyBase>` (`ConditionalWeakTable`): identity keying (no collisions),
  GC-eviction (no unbounded growth), internal synchronization (the old `Dictionary` was unlocked).
  RED→GREEN test (`WeightedCountCacheTests`) reproduces the cross-party contamination against the old
  hashcode key and proves the fix.
- **Flicker diagnostic (TEMPORARY).** Investigating the "bandit/AI-lord party count shows 200 then 20
  then back" report, an engine trace showed the campaign-map nameplate reads RAW
  `NumberOfHealthyMembers` (via `SandBoxUIHelper.GetPartyHealthyCount`) — untouched by the weighted
  getters — so the visible flicker is NOT the weighting. Added `SandBoxUIHelper_GetPartyHealthyCount_Patch`
  (Postfix, `Patch17_TroopWeight` category): on a large-ratio count swing it logs one `[CountFlicker]`
  line classifying the mechanism (army-sum toggle / cache poison / raw-roster change) so the next repro
  self-identifies. Sample-gated (per-party cap), try/catch'd, remove once root-caused.
- **Doc:** corrected `troop-weight-system.md` Performance section (the count cache is now a
  `ConditionalWeakTable`; the previously-documented "trims 25% at 2000 entries" never existed).

Research: `SandBox.ViewModelCollection.SandBoxUIHelper.GetPartyHealthyCount`, `PartyBase.NumberOf*` getters (installed v1.4.7).
Not-tested: the Harmony postfix invocation (requires a live campaign) — the pure detector/formatter are unit-tested.

## 2026-07-10

### chore: relocate repo to `E:\repos\TAOM`

- Moved the working copy from `C:\Users\mikew\source\repos\TAOM` to `E:\repos\TAOM`.
- Repointed the runtime configs that embedded the old absolute path: `.mcp.json` (serena
  `--project`, filesystem root, taom-moduledata server), `.codex/config.toml` (filesystem root).
- Future-proofed 7 hooks that hardcoded `cd "c:/Users/mikew/source/repos/TAOM"` — now
  `cd "${CLAUDE_PROJECT_DIR:-$(pwd)}"` (matching the newer hooks) so a future move needs no edits:
  `session-start`, `session-stop`, `pre-compact`, `post-compact`, `detect-docs-gaps`,
  `check-build-before-commit`, `log-agent`.
- Build stays relocation-clean (`Directory.Build.props` resolves the game via `BANNERLORD_GAME_DIR`).
  The Claude Code memory dir was moved alongside (slug `c--…` → `e--repos-TAOM`).

### feat(career-system): all 49 career ability icons + compact battle HUD (#101)

- **Icons:** every enabled career now has a 256x256 ability icon in a unified "named effect-icon"
  style — the ability's effect/emblem as a gritty painterly oil painting with the ability name
  hand-lettered across the bottom (Poisoned Blades = venom-slick crossed scimitars, Soul Drain =
  souls spiraling into a shadow hand, Warcry of Eorl = the sounding horn + white-horse banner, …).
  Art user-generated in Midjourney from per-ability prompts (faction palette + grounded-LOTR VFX
  policy: no wild-fantasy glow; overt effects only for the Dol Guldur sorcery set); downscaled
  Lanczos to 256 and baked into the `ui_taom_career_system` atlas (49/49 rects pixel-verified,
  manifest + atlas + `_tex.tpac` chain regenerated in order, install↔repo synced byte-identical).
- **Battle HUD** (`GUI/PreFabs/CareerSystem/AbilityHUD.xml`): panel 220x132 → 130x166, icon
  64 → 110, career-name line and black backdrop removed — the icon, "Press V" ready text, and the
  charge bar now float directly on the battle view. The VM's `AbilityName` property is now unbound
  (dead binding, candidate for later cleanup).
- **Rename:** `cave_troll_master` ability "Troll Frenzy" → "Gundabad Berserker"
  (`taom_career_strings.xml`, `taom_career_choices.xml`, the disabled template block — 16
  occurrences). The 12 `Languages/*/std_taom_career_strings_*.xml` files still carry the old
  translated name for those 8 string ids until the next `/localize` run.
- **Docs:** `career-system.md` (icon how-to rewritten: bake required, `sprite=` attr is dead,
  house style recorded) + `gui-sprite-system.md` (Sprites-Needed row closed; two empirical bake
  lessons: a repo→install deploy can silently clobber a fresh CLI bake — always
  `sync_sprite_bake.ps1` immediately; an editor pass can rebuild only the tpac without re-packing —
  mtime-check the manifest/atlas/tpac trio).

Not-tested: career-screen render of the new icons (battle HUD render verified in-game via
screenshot; the career screen resolves the identical sprite id).

### fix(dependencies): tournament-exit hang round 2 — PatchShield must never shield the Gauntlet UI layer (#331, the REAL fix)

- **Round-2 evidence (post-Patch60):** the ~107s stall MOVED with the relocated movie release into `EndMissionInternal` (2026-07-09 logs: `ReleaseMovie=104,482ms` / `108,866ms`; `RemoveLayer=0ms`), with the gen0 GC delta **+8,276 in all three measured hangs** across different towns and 4-745 agents — a deterministic fixed workload intrinsic to releasing the Tournament movie. Round-1's static arithmetic (widget counts, O(1) scans) was built on assumed counts and wrong.
- **Measured, not modeled:** new `ExitStallSampler` (`Main/Features/BattleLoadDiagnostics/`) — background thread that photographs the MAIN thread's managed stack at +15/+30/+60s into any exit stall (armed by the exit window's new `ExitWindowOpenedUtcTicks`; `Thread.Suspend` + the obsolete-as-warning `StackTrace(Thread,bool)` ctor, net472). First repro named the sink in one shot: `PatchShield.ShieldFinalizerVoid` atop a 16-deep `WidgetTemplate.OnRelease_Patch2` recursion; the second sample caught `MethodBase.GetMethodFromHandle` inside `WidgetFactory.IsCustomType_Patch2`.
- **Three-factor root cause, each harmless alone:** (1) the engine's tournament UI re-instantiates bracket templates per round, accumulating `WidgetTemplate._customTypeChildren` into a ~10^6-call release recursion (fixed per tournament — hence the invariant gen0 delta); (2) UIExtenderEx legitimately patches `WidgetFactory.IsCustomType` (prefix) and blank-transpiles `WidgetTemplate.OnRelease`; (3) TAOM.Dependencies' **PatchShield** stacks a `__originalMethod`-binding Harmony finalizer on EVERY patched method in the process — Harmony's wrapper then pays `GetMethodFromHandle` + try/catch per call (~50µs). ~10^6 × ~50µs ≈ 107s of frozen exit.
- **Fix:** `PatchShield.Install` now skips targets in `TaleWorlds.GauntletUI`/`TaleWorlds.TwoDimension`/`TaleWorlds.MountAndBlade.GauntletUI` namespaces (`ExcludedTargetNamespacePrefixes`) — the UI layer is per-widget-recursion hot and shield value there is nil. **Measured result: tournament exit 105-109s → 9.5s** (`ReleaseMovie=8,822ms`, gen0 delta +3). The residual ~9s is UIExtenderEx's legitimate prefix wrapper at ~10^6 calls — normal loading-screen territory, not worth patching third-party internals (simplicity criterion). The third prefix came out of the round-2 deep review's compat agent: TAOM's own Patch38 nameplate-fade target (~3000 calls/sec on the campaign map) was silently paying the same shield tax every frame.
- Patch60 (round 1) stays: the leak it fixes is real and its relocation is cost-neutral; its new per-exit `ReleaseMovie=Nms` stamp is the permanent regression canary. Sampler thresholds raised to +15/+30/+60s (above the known-good residual) and kept as standing diagnostics.
- Suite 4177 green (+12 tests: sampler schedule, exit-window ticks lifecycle, toggle/closer regressions).
- **Round-2 reviews (deep-review 5 agents + Codex review 73, 0 P1 / 2 P2 / 4 P3 — all addressed):** `Poll` gained an `Interlocked` reentrancy guard (Timer ticks overlap when a capture blocks); the sampler got its own MCM kill switch ("Enable Exit Stall Sampler" — the only diagnostics component that suspends the main thread); capture errors now log AFTER Resume (nothing allocates inside the suspended window); the compat agent's empirical CLR pass killed a false code comment (the `StackTrace(Thread,bool)` ctor was never hidden — a named-argument typo had been misread as a missing ctor; now a direct call) and caught TAOM's own Patch38 nameplate target (~3000 calls/sec) still paying the shield tax → third exclusion prefix `TaleWorlds.MountAndBlade.GauntletUI`. RCA findings 5-14: `docs/reviews/rca-tournament-exit-hang-2026-07-06.md`.

Research: WidgetTemplate.CreateWidgets/OnRelease + WidgetFactory.IsCustomType (installed 1.4.6), Bannerlord.UIExtenderEx WidgetFactoryManager.Patch (vendored DLL decompile), PatchShield.Install/ShieldFinalizerVoid
Save-compat: none — UI teardown + diagnostics only.

## 2026-07-09

### balance(special-resources): raise all caps 400–600 → 10000, zero the starting amounts

- **Why:** the 2000-cost Mûmakil (`harad_mumakil_rider`, `recruit_cost="2000"`) was permanently
  unrecruitable — every resource capped at 400–600, and a creature is charged in the *recruiting
  player's* resolved resource (War Spoils for Mordor/Isengard/Gundabad/Dol Guldur players, War Drums
  for Harad/Aserai players), both far under 2000. Raising all 11 caps to 10000 makes the Mûmakil — and
  any future high-cost special creature/elite — affordable in every faction.
- **Also:** `starting_amount` set to 0 on all 11 resources — heroes now begin with an empty reserve and
  earn from scratch (was 20–40).
- **Data-only:** `special_resources_config.xml`. The `cap` flows `SpecialResourceConfigProvider` →
  `SpecialResource.Cap` → the `Math.Min(current + amount, Cap)` earning clamp and the `… / Cap` map-bar
  display; no C# change. Config is singleton-cached, so a **full game restart** (not a save reload) is
  needed to pick up the new values.
- **Files:** `Main/_Module/ModuleData/special_resources/special_resources_config.xml`,
  `docs/features/special-resources.md`.

Save-compat: none — the raised cap only relaxes the ceiling (existing balances ≤500 round-trip unchanged
and may now grow toward 10000); `starting_amount` affects only fresh hero seeding, so no saved balance is
retroactively zeroed.

## 2026-07-08

### chore(docs): enforce config-example + version-marker consistency (prevent doc drift)

- **Why:** the v1.4.7 deep-review found the banner-color feature doc still advertised the old
  `EnableLayerLimitTranspiler: true` default after the flip — a silent doc-vs-code drift (docs aren't
  compiled or tested). Rather than just fix the one doc, make the whole class a hard gate.
- **Two new `tools/lint_docs.py` checks:** (1) **config-example drift** — a `docs/features/*.md`
  `json` example whose values disagree with the shipped `Main/_Module/ModuleData/**/*.json` config it
  mirrors (compares shared keys only, so partial examples are fine; also flags a doc key the shipped
  config no longer has); (2) **version mismatch** — CLAUDE.md's "Target: Bannerlord X" line(s) or an
  API-snapshot header that disagrees with `.claude/pinned-game-version.txt`. Historical docs
  (migration/archive/rca-/codex-*) are exempt, reusing the existing stale-version exemption set.
- **Enforcement:** `.claude/hooks/check-doc-config-drift.sh` (PreToolUse Bash) runs
  `lint_docs.py --fail-on-drift` and **hard-blocks `git commit`** when a relevant file is staged and
  drift/mismatch is found. Fail-open per the TAOM hook rule (no python / linter crash / nothing
  relevant staged never blocks). **Wiring into `.claude/settings.json` is pending — the
  config-protection guardrail blocks settings edits without an explicit OK (the hook is dormant until
  registered).**
- **Drift found + fixed by the new checks:** `docs/features/war-of-the-ring.md` config example
  (`triggerDay` 1→2/14, testMode days) was out of sync with the shipped `war_of_the_ring.json`;
  `docs/ai-includes/agent-operating-manual.md` + `docs/features/bannerlord-together-compat.md` still
  named v1.4.5 as the *current* target. All fixed; `config_drift` + `version_mismatch` now 0.
- **Also made version-labels self-updating** so this class stops recurring: `tools/snapshot_api_surface.ps1`
  and the `taom-src` skill now derive the version from `Version.xml`/auto-detection instead of a
  hardcoded string.
- **Tests:** `tools/tests/test_lint_docs.py` — 14 unit tests (value mismatch, partial-example OK,
  extra/removed key, non-JSON skip, BOM config, historical exemption, version consistency, v-prefix).
- **Files:** `tools/lint_docs.py`, `.claude/hooks/check-doc-config-drift.sh`, `tools/tests/test_lint_docs.py`,
  `.claude/skills/lint-docs/SKILL.md`, CLAUDE.md hooks table, the doc fixes above. RCA:
  `docs/reviews/rca-v1.4.7-bump-2026-07-08.md`.

Save-compat: none — docs, tooling, and a commit-gate hook only.

### chore(engine): bump to Bannerlord v1.4.7 + impact analysis

- **Bump:** Steam auto-updated the installed shipping client v1.4.6 → **v1.4.7** (base game + War Sails). Handled via the
  `/engine-bump` offline pipeline: preserved the v1.4.6 decompile baseline (`_shipping_build_v1.4.6` + `_categories_v1.4.6`),
  regenerated the category tree + dual-build + `_manifest.json` to v1.4.7, MD5-diffed the blast radius (**10 assemblies
  changed**, none added/removed), bumped `.claude/pinned-game-version.txt`.
- **Compatibility:** `BindingVerification` gate **green (50/50)** — every Harmony target, GameModel override, and reflection
  site still resolves against v1.4.7. Creature/scene parity clean (`audit_mount_parity`, `audit_action_set_parity` 0 gaps,
  `audit_battle_scenes` all 256 indices). API snapshot regenerated + reproducible; the generator now version-stamps from
  `Version.xml` so its header no longer goes stale. Full impact matrix: `docs/migration/v1.4.7-impact.md`.
- **Patch15_BannerLayerLimit disabled** — v1.4.7 "made the banner layers unlimited in the banner reader"
  (`Banner.TryGetBannerDataFromCode` no longer has the `RemoveRange`/32-cap), so the transpiler is a no-op that logged
  `RemoveRange not found` every load. Flipped `EnableLayerLimitTranspiler` false in BOTH `BannerColorConfig.cs` and the
  shipped `banner_color_config.json` (JSON overrides the C# default) + added an early quiet-return guard so a disabled
  transpiler no longer logs the warning (the warning fired before the flag was consulted). Kept, not deleted. 3 tests flipped.
- **Patch49_ArmyGatheringNreGuard kept** — the v1.4.7 "null reference in AI behaviour" fix is a different site; the
  decompile confirms the guarded `Army.FindBestGatheringSettlementAndMoveTheLeader` derefs (`Army.cs:726` / `:659`) are
  still unguarded in v1.4.7, so the crash guard remains load-bearing (comment refreshed).
- **Unaffected (verified):** save-metadata stamp (Patch61 already upserts), attacking-a-raiding-party + village-no-militia
  crashes (different sites), `.sack` shader bloat (no TAOM workaround), cloth-sim crash (NativeSkinFixes parked).
- **Owed:** in-game control battles (vanilla → creatures charge/melee, Messenger conversation exit, SmartCavalry charge,
  >32-layer banner) — the only checks an offline session can't run.

Save-compat: none — decompile/docs/config-default changes only; no save-serialized state touched.

### chore(rendering): disable NativeSkinFixes by default (parked at the wiring level)

- **Change:** the three native MinHook detours (covers_head hand-morph freeze + hair/beard cloth physics) are
  now OFF by default. The install call in `SubModule.OnBeforeInitialModuleScreenSetAsRoot` is commented out, so
  the hooks never load and engine rendering is vanilla for everyone — regardless of any persisted MCM value.
- **Why the wiring-level park, not just a default flip:** the install gate reads `TaomSettings.Instance.EnableNativeSkinFixes`,
  and MCM persists a user's saved value over the compiled default. Flipping the default alone would leave the feature
  ON for any machine that already saved the toggle ON (the NavalTravel-park rationale). The compiled MCM default is
  also set to `false` and the hint rewritten to note the parked state.
- **Files:** `Main/SubModule.cs` (install branch commented out + `RE-ENABLE` breadcrumb), `Main/Features/TaomSettings.cs`
  (`EnableNativeSkinFixes` default `true`→`false`, hint), `TAOM.Tests/.../NativeSkinFixesInstallerTests.cs` (pinning
  test flipped to assert the `false` default), `docs/features/native-skin-fixes.md` (parked status).
- **Reversible:** the native DLL + C++ source stay in place; RE-ENABLE = uncomment the install branch + flip the default.

Save-compat: no save impact — the change only governs whether native hooks install at boot.

### feat(map): lore + role starting building levels for all 221 towns & castles

- **Problem:** TAOM's `settlements.xml` seeded each fief's building levels (fortifications/barracks/marketplace/…)
  as a semi-random scatter uncorrelated with prosperity or importance — the lowest-prosperity town outgunned the
  highest, and only Minas Tirith was hand-set. New campaigns therefore started arbitrary. Building levels are read
  once at new-campaign creation (`Town.Deserialize`, skipped for saved games), valid range 0–3, fortifications
  floors at 1; towns carry 12 `building_settlement_*`, castles 11 `building_castle_*` (grounded in installed
  vanilla `DefaultBuildingTypes`).
- **Change:** every one of the 221 towns/castles hand-curated to a lore + role standard — capitals & legendary
  fortresses maxed (Minas Tirith, Barad-dûr, Erebor, Orthanc, Dol Guldur); the Black Gate / Cirith Ungol /
  Cair Andros / Helm's Deep read as great fortresses regardless of prosperity; remote holds sparse but defensible.
  Consistent numbers via a pinned role-tier expander + culture flavor (orc garrisons brutal & civic-poor; dwarven
  wall-and-mason; elven refined; Umbar mercantile). fort3 rationed to capitals + legendary fortresses only. Applied
  to the LIVE `TAOM_Map/ModuleData/settlements.xml`: 221 fiefs, 1,363 building levels altered.
- **Tooling (new, modeled on the prosperity analyze/rebalance pair):** `tools/author_settlement_buildings.py`
  (source of truth: hand decisions + deterministic expander → per-culture JSONs + audit doc),
  `tools/dump_settlement_buildings.py` (read-only current-level dumper), `tools/apply_settlement_buildings.py`
  (two-level-regex safe applier: `.bak`, byte round-trip, exactly-once assertion, range/fort-floor/id-set
  validation, dry-run default, idempotent). Decisions recorded in `tools/data/settlement_building_levels/*.json`.
- **Review:** 7-bloc adversarial workflow over the audit doc; 3 low-severity consistency fixes incorporated
  (Barad Wath / Barad Nûrn fort3→2 to reserve fort3 for legendary Mordor fortresses; Ardûvar fort2→3 to match the
  Khand capital). Verified live: re-run reports 0 changes (idempotent), XML re-parses clean, lore overrides confirmed.
- **Docs:** `docs/features/settlement-building-levels.md`; per-fief audit artifact
  `docs/reviews/settlement-buildings-audit-2026-07-08.md`.

Save-compat: seeds NEW campaigns only; existing saves keep their own building data.

### balance(culture-conversion): ship the 1-day conversion hold as the default for everyone (#333)

- **Change:** `RequiredHoldDays` / `CultureConversionHoldDays` default 45 → **1** in all three places — the JSON
  (`culture_conversion_config.json`), the compiled config fallback (`CultureConversionConfig`), and the MCM
  compiled default (`TaomSettings`). The MCM default is the one that actually governs a new player (MCM-over-JSON:
  the settings provider reads `TaomSettings.Instance.CultureConversionHoldDays` and only falls back to the JSON
  when MCM is absent), so all three move together to keep the shipped default coherent.
- **Effect:** a cross-culture fief converts the day after its hold begins — culture flips + notables replace almost
  immediately on capture, and the foreign-occupier loyalty penalty drops right away (near-instant pacification). A
  deliberate fast-war-map choice; raise "Days To Convert" in MCM for slower, gradual assimilation.
- **Existing players:** MCM persists a player's saved value, so anyone who already launched the mod keeps their
  stored hold (typically 45) until they reset MCM or set "Days To Convert" to 1 themselves; only fresh installs
  pick up the new default automatically.
- 4 config-provider default-assertion tests updated (45 → 1); suite 4169/0 green.

## 2026-07-07

### fix(war-of-the-ring): chunk momentum SyncData so it never corrupts saves — the v2.0.9 "A problem occured while trying to load the saved game." bug

- **Root cause (confirmed both halves against the decompiled v1.4.6 engine + our code):** `WarOfTheRingMomentum` serialized its entire event log as ONE SyncData string (`_taom_wotr_momentum_v2` — [WarOfTheRingMomentumBehavior.cs:86](Main/Features/WarOfTheRingMomentum/WarOfTheRingMomentumBehavior.cs), each of up to 100 events/type/side carrying its full localized `Description` via [MomentumStateStore.cs:78](Main/Features/WarOfTheRingMomentum/MomentumStateStore.cs)). In a developed campaign that JSON crosses ~32 KB around day ~50. The engine's `ArchiveSerializer.SerializeEntry` writes each save-archive entry's length as `(short)Data.Length` — a signed int16 truncation (`ArchiveSerializer.cs:27`) — but writes the data in full, so any string entry > 32,767 bytes gets a wrong length **on write** and desyncs on the next load (`ArgumentException: Source array was not long enough` inside `ArchiveDeserializer.LoadFrom`, or `OverflowException` for the 32,768–65,535 range). Every save past that point was unloadable. Independently root-caused by a user whose forensics matched our arithmetic exactly (a day-52 save: 72,915 B true, stored as `72915 mod 65536 = 7379`).
- **Fix (cap + split, zero gameplay change):** `MomentumSyncChunker` splits the serialized JSON across a count key + N chunk keys, each capped at 10,000 UTF-16 chars (≤ 30,000 UTF-8 bytes worst case — a proven margin under the 32,763-byte entry limit). No single synced string can reach the engine limit regardless of how the log grows; descriptions, the 100/type cap, and the momentum math are all unchanged. Keys renamed `_v2`→`_v3` so an old single-string save loads as absent → one-time momentum reset (kingdoms re-enroll + momentum re-accrues on the next daily tick); the campaign is untouched.
- **Recovery for already-bricked saves:** `tools/repair_sav_strings.py` (offline, stdlib) decompresses the save, parses the Strings archive (recovering the truncated entry length via the sequential-entry-id anchor), resets the oversized momentum string to empty, re-frames + recompresses to `<name>_fixed.sav`. Zero campaign-data loss — only the cosmetic war-meter history is cleared; the fixed save loads on the vanilla engine (no runtime patch). Verified on two real user saves (day-52 desync case + day-20 negative-length case): both repaired, re-parse clean, pass `inspect_sav.py --verify`. A no-install **PowerShell twin `tools/repair_sav_strings.ps1`** (recommended for players — ships with Windows 10/11, uses .NET `DeflateStream` = the same library the engine uses) produces byte-identical decompressed output. Player-facing Windows how-to: `docs/SAVE-REPAIR-GUIDE.md` + `.html`.
- **Tests:** `MomentumSyncChunkerTests` (7) incl. the end-to-end proof — a realistic max log exceeds the limit as one string but every chunk stays under it and round-trips losslessly (multibyte-UTF-8 covered). Suite 4169/0.
- Diagnosis validated the new SaveLoadDiagnostics `ArchiveDeserializer.LoadFrom` hook — it's the exact stamp that fires on this in the field. **Follow-up:** bump the module version on every distributed package (v2.0.9 spanned 34 commits, which blinded field triage).

Save-compat: `_v2`→`_v3` key rename → one-time momentum reset on first load; no campaign data affected.
Research: ArchiveSerializer.SerializeEntry / ArchiveDeserializer.LoadFrom / SaveEntry / EntryId / BinaryReader/Writer / GameData.Write / MetaData.Serialize (installed 1.4.6)

### fix(culture-conversion): same-culture ownership changes no longer restart the hold-timer (#333)

Play-test report (Grymmclúd/`castle_E6` captured as Rhûn, still dwarven): the conversion pipeline itself was
healthy — the fief had simply been queued toward `khuzait` **16 times across four days of play with zero
completions**, because `OnSettlementConquered` restarted the 45-day clock on EVERY ownership change. The
capture→grant double-fire (every conquest), kingdom re-grants, barters, and same-culture recaptures each reset
the timer, so a contested frontier fief could never accumulate the hold.

- `CultureConversionService.OnSettlementConquered`: if a timer is already pending toward the new owner's culture,
  it now CONTINUES (placed after the recruitment-pool/player-owned gates, so gate cancels still win). A different
  culture still restarts; a recapture by the effective culture still cancels (uninterrupted-hold by design —
  documented as a known limitation with the MCM "Days To Convert" pointer).
- Cancel + stale-timer-drop paths now log at DEBUG — both were silent, which slowed the diagnosis.
- Tests: +2 (same-target re-grant keeps the original start day and converts on the original schedule;
  capture→grant double-fire keeps the first timestamp). Suite 4162/0 green.

### feat(diagnostics): SaveLoadDiagnostics (Patch61) — name the real cause behind "A problem occured while trying to load the saved game."

- **Why:** multiple players report saves failing to load with the engine's generic load-failure dialog. The engine swallows the real exception — `LoadContext.Load` catches everything and prints only `ex.Message` (with TWParallel fill loops that's the useless "One or more errors occurred"), `LoadResult.CreateFailed` records the hardcoded string "Not implemented", and CrashReport never fires because nothing escapes. Field triage was additionally blind because the shipped "v2.0.9" label spans 34 commits.
- **Feature (`Main/Features/SaveLoadDiagnostics/`):** always-on `[SaveLoad]` lifecycle logging to `Logs/taom_debug_*.log` — 15 thin hooks in four categories (`Patch61_SaveLoadDiagnostics` + one isolated category per internal-type reflection hook, so one drifted binding can't kill its siblings) delegating to a lock-free, fault-throttled service. All Finalizers are void + `Priority.First` — SaveShield (TAOM.Dependencies) finalizes 4 overlapping methods and swallows, so Patch61 must observe the exception first (33-agent adversarial review HIGH; the review also added header-phase/`ArchiveDeserializer.LoadFrom`/deferred-callback coverage, `SaveId.GetStringId()` attribution, and 4 binding drift-guard tests). Load side: save-identity dump at `TryLoadSave` (module list + versions = which build wrote the save), interior Finalizers at the graph throw sites (`LoadContext.CreateLoadData` for objects, `ContainerLoadData.FillCreatedObject/Read/FillObject` for containers — where the big SyncData dicts live), unknown-SaveId detection for definer/build mismatch (`ObjectHeaderLoadData.CreateObject` + `ContainerHeaderLoadData.GetObjectTypeDefinition` — the engine silently null-fills these today), per-behavior SyncData attribution (`CampaignBehaviorDataStore.LoadBehaviorData` — the engine's raw `(T)value` cast has no per-behavior context). Save side: `FileDriver.Save` Finalizer fires ON the async writer thread at the #292 `GameData.Write` throw site, `SaveOutput.PrintStatus` catches the faulted-task `Game.OnSaveCompleted` signature, non-Success `SaveResult`s logged (antivirus/OneDrive write blocks surface here). Every Finalizer rethrows — engine behavior byte-identical.
- **Build stamp:** `MBSaveLoad.GetSaveMetaData` Postfix writes `TAOM_Build` (assembly + informational version) into every save's metadata — future saves self-identify their exact build.
- **Offline triage:** `tools/inspect_sav.py` dumps a .sav's version/character/module table without launching the game; `--verify` walks the deflate data region (OK / truncated / corrupt with offsets). Already caught two zero-header corrupt saves (interrupted-write signature) on the dev machine.
- Applied in `OnSubModuleLoad` (Patch58 precedent) — loads fire from the main menu, so the late batch would miss the first load. 20 service tests + 4 binding drift-guards; `HarmonyPatchBindingTests` binds all 15 targets against the installed 1.4.6 engine; suite 4162/0.
- **Field confirmation (same day):** a user-supplied failure trace — `TaleWorlds.Library.BinaryReader.ReadBytes` "Source array was not long enough" inside `ArchiveDeserializer.LoadFrom`, then "Not implemented" — lands exactly on the instrumented `archiveParse` site and matches the truncated/incomplete-write class (two zero-header saves found on the dev machine; one affected user reported disabling antivirus).

Save-compat: additive — one inert metadata key (`TAOM_Build`); no SyncData, no definer.
Research: SandBoxSaveHelper.{TryLoadSave,LoadGameAction} / MBSaveLoad.{LoadSaveGameData,GetSaveMetaData} / SaveManager.{Load,Save} / LoadContext.{Load,CreateLoadData} / ObjectHeaderLoadData / ContainerHeaderLoadData / ContainerLoadData / FileDriver.{Save,Load} / AsyncFileSaveDriver / SaveOutput.PrintStatus / CampaignBehaviorDataStore.LoadBehaviorData (installed 1.4.6)

### fix(castle-recruitment): guard castle notable spawn against missing culture templates — new-game infinite loading loop

- **Crash (tester machine, `taom_debug_2026-07-07_13-32-19.log`):** starting a new campaign NRE'd in `HeroCreator.CreateHero` ← `CreateNotable` ← `CastleNotableMaintainer.EnsureCastleNotables` ← `OnNewGameCreated`. The escaped exception stalls the engine's `GameLoadingState` — it re-runs campaign creation every tick, so the same NRE recurred **26,306+ times over ~16 minutes** as an infinite loading screen (CrashReport dedupe-suppressed the repeats). `OnGameLoaded` calls the same path, so save-loads were equally exposed.
- **Engine mechanism:** `GetRandomTemplateByOccupation` returns **null** when `settlement.Culture.NotableTemplates` has no template for the requested occupation; `CreateHero` derefs it. Identical pitfall already guarded in `CultureConversionAdapter.ReplaceNotable` (#325) — `CastleNotableMaintainer` lacked the guard.
- **Fix (`CastleNotableMaintainer`):** (1) per-occupation template pre-check (skip + warn once per culture:occupation pair, naming castle/culture/occupation so the offending data self-identifies in the log); (2) per-castle try/catch in `EnsureAllCastles` + `TickCastle` — a handler that runs inside `OnNewGameCreated`/`OnGameLoaded` must never throw (the failure mode is a loading-loop hang, not a CTD); (3) null-check on the `CreateNotable` return. Maintainer now takes `IModLogger`.
- **Data audit (dev machine):** all 143 castles / 19 castle cultures in the live TAOM_Map have GangLeader/Headman/Merchant/Artisan templates resolving with correct occupations — current data cannot produce the null; the tester's install had a stale module folder (LOTRLOME_Armory 2.0.7 physical copy in place of the current-version link). The guard makes any such divergence a logged skip instead of a bricked campaign.
- **Deep-review hardening (same day, 5 agents: 2 MED + 1 LOW, all resolved):** (1) null-ENTRY gate added — `NotableTemplates` can contain literal null entries (`ReadObjectReferenceFromXml` returns null on a malformed `<notable_templates>` ref) and the engine's occupation filter does NOT null-check, so the original pre-check could pass while `CreateNotable` still threw (caught by the try/catch, but bypassing the warn-once dedup → daily error spam); the culture is now skipped entirely with a one-time warning, and the same gate is propagated to the sibling call site `CultureConversionAdapter.ReplaceNotable` (#325); (2) the `CreateNotable == null` branches in both call sites are unreachable on v1.4.6 (the engine throws rather than returning null) — re-commented as explicit forward-guards so no future maintainer mistakes them for the real safety net; (3) no unit test for the maintainer guard — declined per ADR-008 boundary convention, decision recorded. RCA: `docs/reviews/rca-castle-recruitment-guard-2026-07-07.md` (pattern: a guard copied from a precedent inherits the precedent's unverified assumptions).
- **Verified:** suite 4153 passed / 0 failed (post-hardening).

Save-compat: none — spawn-guard only; no new state.
Research: HeroCreator.{CreateNotable,CreateHero} / DefaultHeroCreationModel.GetRandomTemplateByOccupation / CharacterObject.CreateFrom (installed 1.4.6)

### chore(harness): encode the tournament-exit review lessons as permanent gates (#331 close-out)

- **`.claude/rules/harmony-patches.md`** — new "Latches & Toggle Gates" section (MANDATORY): (1) enumerate a closer for every latch-opener path, (2) toggles gate I/O never state transitions, (3) verify "unconditional" at the OUTERMOST gate (grep all callers). Auto-loads for every `Main/**/Hooks/**` edit.
- **`/deep-review` Agent 5** — new rule 5d (latch closer coverage + toggle gating + outermost-gate verification) so the Data Flow agent checks this class on every future review; fix-loop guidance now requires caller-layer verification before marking a guard-semantics fix done.
- Documentation sweep for #331: `battle-load-diagnostics.md` test counts + hardened window semantics, RCA finding #4 (Codex caller-gate catch), LESSONS-LEARNED outermost-gate clause, AGENTS.md review-72 entry, REVIEW-LOG row 21. Issue #331 closed — fix chain: measured 108s → engine leak root-caused → Patch60 → deep-review 2 MED fixed → Codex 1 P2 fixed → suite 4136 green.

## 2026-07-06

### fix(arena): release the tournament UI at mission end — kills the 30s-2min tournament-exit hang (#331)

- **Root cause (engine defect, 1.4.6):** `MissionGauntletTournamentView.OnMissionScreenFinalize` nulls `_gauntletMovie`/`_gauntletLayer` **without** `ReleaseMovie`/`RemoveLayer` (the arena practice view releases both correctly at the same hook). The leaked 'Tournament' movie — the only mission UI holding live item-tableau/character-tableau widgets (prize item, per-round weapon icons, winner panel), with a prize render request typically in flight ~0.7s before exit — is then torn down inside `ScreenBase.HandleFinalize`'s layer loop under the exit loading screen, after the mission frame pump is dead, where it stalled **108 measured seconds** (+8,276 gen0 GCs; native scene clear itself = 4ms). Same teardown while the mission is alive costs milliseconds (every practice exit proves it).
- **Fix:** `Patch60_TournamentExitMovieRelease` (`Main/Features/Arena/Hooks/`) — capture-Prefix + release-Postfix on `OnMissionScreenFinalize` replicating the practice view's `ReleaseMovie` → `RemoveLayer` sequence at the identical lifecycle point (mission renderer still alive). Postfix-shaped because the original body must run first (drops focus, finalizes the VM — releasing in a Prefix would NRE in `TryLoseFocus` on the cleared UI context). Fail-safe: any capture/release failure degrades to today's vanilla leak + hang, never breaks the exit.
- **Evidence chain:** exit-phase diagnostics (below) localized the hang to the screen-layer finalize loop; 22 research/verification agents over 2 adversarial rounds decompiled the installed engine end-to-end — all TAOM code exonerated (zero bytes allocated in the window), every alternative mechanism (widget-count teardown, gamepad-nav scans, native scene clear, crowd-size scaling, autosave) refuted with arithmetic.
- **Verified:** suite 4133 passed / 0 failed; `HarmonyPatchBindingTests` binds the new target against the installed engine; 2 new drift-guard tests pin the private-field bindings (a 1.4.x rename silently reverts to the leak — these turn it red offline).

Not-tested: the release under a real tournament exit (in-game verification owed — the hang is only reproducible in-game).
Research: MissionGauntletTournamentView / MissionGauntletArenaPracticeFightView / GauntletLayer.ReleaseMovie / ScreenBase.{RemoveLayer,HasLayer,HandleFinalize} / GauntletMovie.Release / MissionScreen OnEndMission-vs-UnregisterView ordering (installed 1.4.6)
Save-compat: none — UI teardown only.

### feat(diagnostics): mission-EXIT phase stamps — localize the tournament-exit hang (#331)

- **Problem:** exiting any tournament hangs the loading screen 30 s–2 min (constant, incl. the first tournament of a session); practice fights and field battles exit normally. Static analysis ruled out every TAOM tournament hook and mission-end teardown (all O(small); engine prize pools cached per tournament instance) — the time sink can't be located without exit-side instrumentation, which didn't exist (`BattleLoadDiagnostics` covered entry only).
- **Change:** 9 new exit phases in the `[BattleLoad]` log contract — `ExitBegin` (mission/scene/agent counts + GC/heap stamp) → `ExitTeardownBegin/Done` (`Mission.EndMissionInternal`) → `ExitStateFinalizeBegin/Done` (`MissionState.OnFinalize`) → `ExitResourceClearBegin/Done` (`Mission.ClearUnreferencedResources` — the forced full GC + native GPU clear) → `MapResumed` (GC delta + `SaveHandler.IsSaving`) → `FirstMapTick` (closes the window). Six new thin hooks in the existing `Patch43_BattleLoadDiagnostics` category; exit-window gating keeps probes silent where targets also fire at load/every-frame (`MapState.OnTick` postfix is a two-read early-out). Same `EnableBattleLoadDiagnostics` master toggle.
- **Verified:** all 6 new patches bind against the installed 1.4.6 engine (`HarmonyPatchBindingTests` green); +10 service tests (window gating, seq restart, GC/isSaving tokens).
- **Deep-review hardening (same day):** the Data Flow agent confirmed two exit-window state-machine defects, both fixed — (1) window state transitions were gated behind `IsEnabled`, so an MCM toggle-off mid-window latched it forever (now unconditional; only logging is gated); (2) the window opened for ANY mission but every closer was campaign-only, so custom-battle/chained-mission exits leaked it into spurious `MapResumed` stamps (now campaign-gated at `ExitBegin` + unconditional stale-close at `Mission.Initialize`; residual quit-to-menu case documented as a known limitation). +3 regression tests. RCA: `docs/reviews/rca-tournament-exit-hang-2026-07-06.md`; lesson appended to LESSONS-LEARNED (State/Lifecycle).
- **Codex adversarial pass (review 72, gpt-5.5 xhigh): 0 P1 / 1 P2 / 0 P3.** The P2 caught the deep-review fix being incomplete at the CALLER layer: `PlayerEncounter_Start_Patch` + `Mission_Initialize_BattleLoad_Patch` early-out on `!IsEnabled` before invoking the now-unconditional window-closers, so the toggle-off latch remained reachable through those paths. Fixed — both hooks call the state-closing service method before their toggle gate (service self-gates its logging). Suite 4136 green. LESSONS-LEARNED sharpened: "unconditional" must be verified at the outermost gate.

Research: Mission.EndMission/EndMissionInternal/OnMissionStateFinalize/ClearUnreferencedResources, MissionState.OnFinalize/OnTick, MapState.OnActivate/OnTick, SaveHandler.IsSaving (installed 1.4.6 via taom-src)
Save-compat: none — diagnostics only, no persisted state.

### refactor(career): prune passive-effect vocabulary, harden config parse, retune career screen

- **`PassiveEffectType` pruned + renamed + regrouped.** 15 unused members deleted (none referenced by code, shipped XML, or saves — the enum is parsed from XML at load and never persisted). 10 members renamed to project vocabulary: `SpecialResourceGain` / `SpecialResourceUpkeepModifier` / `SpecialResourceUpgradeCostModifier` (match the SpecialResources feature), `MountHealth` / `MountChargeDamage` (TAOM mounts aren't only horses), `SmithingCostReduction`, `TroopSurvival`, `HeroHealing`, `RenownGain`, `ShrugOff`. Swept across all consumers: 8 C# files, 240 `type=` attributes in `taom_career_choices.xml`, 4 test files, 2 tools scripts. Members regrouped by domain with a consumers-note header. The engine parameter `isShruggedOff` (TaleWorlds signature in `TaomCombatMechanicsModel`) is intentionally untouched.
- **Unknown `type=` values now warn at load** (`CareerConfigProvider.ParseChoice`): an unrecognized value previously coerced silently to `Special` (inert pip); now a WARNING names the choice id + raw value. Case-insensitive parity with `ParseEnum` pinned by a new test (`LoadChoices_UnknownPassiveType_LogsWarningAndFallsBackToSpecial`).
- **Career screen prefab retune** (`GUI/PreFabs/CareerSystem/CareerScreen.xml`): VisualDefinitions renamed (`CareerHeaderSlide` / `CareerFooterSlide` / `CareerNodePanel`) and retimed, inert `EaseIn` markup dropped (decompile-verified — the prefab parser never reads it), pane split now 520/1400, node hover width 768. `CareerChoiceGroupObjectVM` click handlers rewritten to LINQ (behavior-identical; click-rate only).
- Deep review (5 agents) + Codex pre-review: **0 HIGH/P1/P2**; 2 P3 test-hygiene findings fixed in-session (old vocabulary in test method names; negative-parse exemplar → synthetic `NoSuchEffectType`). Stale enum names fixed in `docs/features/battle-balance.md` + `special-resources.md`. RCA: `docs/reviews/rca-career-enum-prefab-cleanup-2026-07-06.md`; systemic lesson appended to LESSONS-LEARNED (rename sweeps end with a substring pass over tests/docs). Full suite green (4121 passed).

Not-tested: career screen render at the new geometry (in-game open owed — prefab drift fails silently).
Save-compat: none — `PassiveEffectType` is never persisted; career saves store id strings only.

### docs: archive cleanup and provenance-note refresh

- Removed 14 superseded research/review artifacts from `docs/reviews/` + `docs/archive/` (early one-shot research prompts, comparison studies, and raw adversarial-review transcripts whose distilled findings live on in REVIEW-LOG and the RCAs); updated the archive index and every referring link.
- Refreshed provenance/design notes across feature docs, CLAUDE.md, AGENTS.md, README, historical CHANGELOG entries, and tool headers; trimmed the now-empty README Acknowledgments section.
- Docs linter: no new dead links (13 pre-existing items unchanged). Build + CombatMechanics tests green after the comment-only C# touches.

## 2026-07-05

### fix(HeroRace): make race persistence robust to skins.xml race-list reordering (#330)

`RacePersistenceService` stores each hero's race in the save as an **int** — a position index into `FaceGen.GetRaceNames()`, the merged skins.xml `<race>` list in module load order. Insert/remove/reorder a race between save and load (a LOTRLOME skins.xml change anywhere but append-at-end, a Bannerlord patch touching Native races, the player toggling another race mod) and every saved int silently re-points to a *different* race — the existing `IsValidRaceId` guard is an in-range check and cannot detect a shift. Only append-at-end has happened so far (`sauron`, 2026-07-02), which is why no save has visibly broken yet.

- **Race-name legend**: `CaptureHeroRaces` snapshots the ordered race-name list as one `;`-joined string (the engine's own `GetRaceIds` delimiter), synced under the new key `_taom_raceNameLegend` beside the existing `Dictionary<string,int>`. Restore translates `savedInt → legend[savedInt] → GetRaceIdFromName(name)` — reorder-proof; a genuinely removed race skips + warns and the hero keeps its XML race. Deliberately NOT a `Dictionary<string,string>` (failed to round-trip `IDataStore` at ~1000 entries — WotR Momentum, 2026-07-03).
- **New `IRaceManager.GetOrderedRaceNames()`** — exposes the init-time FaceGen array; `GetAllRaceNames()` rides `Dictionary.Values` ordering and is unsafe for index math.
- **Clear-on-load**: `SyncRaceData` resets map + legend when `dataStore.IsLoading` before syncing — an absent-key `SyncData` leaves ref values unchanged, so a same-process load of an older-format/pre-TAOM save previously inherited the prior campaign's races onto colliding StringIds (#130-R1 class, until now only handled for new campaigns).
- **Migration is automatic**: pre-#330 saves have no legend → legacy raw-int path byte-for-byte (incl. race-0 bypass + `IsValidRaceId` guard); the first save after the update writes the legend. Old TAOM builds ignore the new key.
- +14 tests (legend shift/removed/out-of-range/no-op, capture, clear-on-load, legacy path, shifted round-trip; `GetOrderedRaceNames` order + fallbacks). Full suite green (4120 passed). Deep-review (5 agents): 0 code findings; engine semantics (`IsLoading`, absent-key behavior, `SyncData<string>` + `Dictionary<string,int>` support) verified against installed 1.4.6 DLLs.

Save-compat: new `_taom_raceNameLegend` string key; absent on old saves → legacy path; additive only. See `docs/features/hero-race.md`.

### feat(diagnostics): log which sieges hit the vanilla gathering dead end

`Patch49_ArmyGatheringNreGuard` already swallows the vanilla siege-start NRE in `Army.FindBestGatheringSettlementAndMoveTheLeader` (`Army.cs:726` null `GatePosition`) so a besieger army that can't resolve a gathering fortification no longer CTDs — but the finalizer logged only a context-free `LogDebug` breadcrumb, so there was no way to see *which* sieges are broken. The guard now records the failure context before swallowing, turning the dead end into a reviewable to-fix list.

- New `ISiegeGatheringDiagnosticsService` (+ `SiegeGatheringDiagnosticsService`): classifies each failure (`KingdomNull` / `NoFortifications` / `AllFortificationsUnderSiege` / `NoReachableFortification` / `Unknown`), **dedups by `(kingdom, focus settlement)`** — first occurrence logs full detail at **WARNING**, repeats increment a counter and drop to DEBUG so WARNINGs never spam. Routed through the existing `IModLogger` → `Logs/taom_debug_*.log`; grep the `[SiegeDiag]` tag.
- Boundary DTO `SiegeGatheringFailureInfo.FromArmy(Army, Settlement)` is the sole sealed-type reader (ADR-002/007, mirrors `TownFoodSnapshot.FromTown`): army/leader/clan/kingdom + focus settlement id/name/culture/faction + a one-pass `Kingdom.Settlements` fortification census (total / under-siege) + leader & focus map positions + campaign time. Every access is null-guarded — it never throws a secondary exception.
- The finalizer widens to inject Harmony's `__instance` + `focusSettlement`; the whole diagnostic path sits inside the existing try/catch, so if it ever throws the NRE is still suppressed — **the crash guard is never weakened, behavior is otherwise unchanged**.
- +14 tests (`SiegeGatheringDiagnosticsServiceTests`): every `Classify` branch, dedup/level routing, and null/NaN-safe `Format`. Full suite green (4106 passed). `FromArmy` + the finalizer stay in-game-validated (ADR-008).

Note: the guard runs *after* the throw, so under an attached debugger the NRE still surfaces as a first-chance exception at `Army.cs:726` (`Source = "0Harmony"`) — expected, not a guard failure; press Continue and read the `[SiegeDiag]` log. See `docs/features/army-targeting.md` "Patch49".

## 2026-07-04

### feat(momentum): reskin the War of the Ring map bar with custom LOTR art

The on-map momentum bar dropped its generic native widgets (`Kingdom.Support.Fill` / `SPKingdom\progress_bar_frame` / `Kingdom.Support.Handle`) for three custom LOTR-themed sprites under `GUI/SpriteParts/ui_taom/WarOfTheRing/` (Imagine-generated source, cut + composited to transparent PNGs locally with PIL — background removal needed a paid Imagine plan, so the cutouts were done with a corner flood-key for the frame/fill and a soft distance-matte for the Ring's glow):

- **`wotr_frame.png`** (700×164) — an obsidian+gold casing, the **Eye of Sauron (Evil) at the left end** and the **White Tree of Gondor (Free) at the right**, with a recessed channel between them (matches the bar's `positive = Free = right` convention). Opaque background.
- **`wotr_fill.png`** (360×57) — a clean red\|green track (`WarOfTheRing.Bar.Fill` brush) sized to sit inside the channel. A smooth red→green gradient muddies to brown/grey at the midpoint, so a two-tone split was chosen instead.
- **`wotr_ring.png`** (150×151) — the One Ring (`WarOfTheRing.Bar.Handle` brush), the sliding handle; it travels toward the Eye when Evil leads, toward the Tree when the Free Peoples lead.

`MomentumMapIndicator.xml` was restructured so the frame is the opaque background and the `SliderWidget` (fill + Ring handle) draws on top, sized + centered to the channel — measured at **55.4% × 34.2%** of the frame, centered on both axes, so alignment needs no margins. Brushes added to `Main/_Module/GUI/Brushes/BalanceOfPower.xml`. All bindings (`@Momentum`, `@IsIndicatorVisible`, click→popup) are unchanged; this is cosmetic only, no C#.

Play-test follow-ups during iteration:
- **Frame border re-cut** — the first local cutout used a flat flood-fill tolerance, which bled into the obsidian frame's near-black outer rim (same colour as the background) and ate ragged gaps + left stray specks in the silhouette. Replaced with a cleanly pre-cut source (transparent background, anti-aliased edges), autocropped + resized to 700w; channel geometry unchanged (55.3%×34.3%, centered) so the slider still aligns. (Frame PNG changed → **re-bake required**.)
- **Popup mirrors the bar** — the detail popup put Free on the left, but the bar puts Evil (the Eye) on the left. Swapped the popup's `Good*`/`Evil*` bindings (banners, leaders, both ally rows) and the `Number1`/`Number2` breakdown/stats columns so **Evil is on the left, Free on the right** everywhere. Prefab-only; the VM keeps Free=`Good*` semantics.
- **Raid momentum cut** (`momentum.json` `raidMomentum` 200→100→50) — village raids were the single largest momentum source for both sides, and because Good factions rarely raid it structurally over-fed Evil. Cut to a quarter of the original — now by far the lowest per-event weight (siege 250 / army 200 / battle-max 300). JSON-only; retune freely (singleton-cached → restart to reload).
- **Enemies-killed now feeds the meter** (new `MomentumActionType.EnemiesKilled` + `momentum.json` `killMomentumPerHundred` = 10) — battle-won momentum is `casualties ÷ loser-strength`-normalized, so it stayed tiny (16–27) despite hundreds of thousands of kills, and the huge "Enemies Killed" total was display-only. Added a raw-attrition source: each side scores momentum for the enemies it kills, on the same battles as the kill stat, shown as an "Enemies Killed" breakdown row (`= kills × 10 ÷ 100` displayed, 504h decay). Save-safe (enum persisted by name; old saves restore an empty queue). Pure `MomentumEventService.AwardKillMomentum`, validated config, 5 new tests (150 momentum green).
- **Battle-won bumped** (`momentum.json` `maxBattleMomentum` 300→350) — a slight increase to the win reward, per request.
- **Army-gathering weight cut** (`momentum.json` `armyMomentum` 200→50) — gathering an army is a routine, repeatable move, not a war outcome; now weighted the same as a village raid.
- **Settlement captures worth more** (`momentum.json` `siegeMomentum` 250→400) — taking a fief is the war's real objective, so it's now the highest per-event weight (battle-cap 350 / army 50 / raid 50). JSON-only.
- **Relative Strength retired** (`momentum.json` `maxStrengthMomentum` 300→0) — Evil out-strengths the Free Peoples for most of a campaign, so the daily strength-differential award handed Evil free momentum every day regardless of what either side did. `MomentumEventService.AwardDailyStrengthMomentum` now early-returns when the cap is ≤ 0, and `RelativeStrength` is excluded from the popup breakdown so no dead "Relative Strength 0/0" row shows. Config-reversible (set the cap > 0). 145 momentum tests green.
- **Map-bar title moved below** — the taller custom frame overlapped the "War of the Ring" title (the `ButtonWidget` drew it on top via a stale `MarginTop`). Title + bar now stack in a vertical `ListPanel` with the title **below** the frame. (Note: the editor sprite-bake's post-run sync copies the install's `GUI/PreFabs/` back over the repo, which reverted this + the popup side-swap once — re-applied; deploy repo→install and don't sync prefabs install→repo.)
- **Popup banner flicker** (`MomentumPopupVM`) — leader/ally banners flashed in and vanished. Root cause: the popup live-recomputes on every `MomentumChanged`, and each `Rebuild` re-created the `BannerImageIdentifierVM`s; banner textures render asynchronously, so a fresh event replaced each VM before its texture finished. Fixed by building the banner/roster VMs **once** at open and refreshing only the numbers (total/color/breakdown/stats) on change — the enrolled factions don't change during the popup's brief life. Not a reskin regression (pre-existing in the live-recompute path).

New helper `tools/sync_sprite_bake.ps1` — copies ONLY the editor sprite-bake outputs (manifest + `AssetSources/GauntletUI/` + `Assets/GauntletUI/`, mirrored) from the game install back to the repo, and nothing else. Replaces the manual whole-folder copy that was silently reverting repo prefab/brush/JSON edits (the root cause of the "it keeps using the old png / my change didn't take" churn this session). Source files flow repo→install via `build.ps1` only.

**Not-tested:** the three sprites are loose PNGs that must be packed by the editor sprite-generation (`SpriteSheetGenerator.exe` + the `ui_taom_*_tex.tpac` texture-compile) before they render — a loose PNG is blank until baked. Sizes/alignment are first estimates; the bake + one in-game tuning pass are the remaining step (baked ≠ visible). Feature doc: `docs/features/war-of-the-ring-momentum.md` "UI & display".

### chore(logging): trim now-redundant per-tick diagnostics from four working features

A single 2.5-hour session produced a 21 MB / 169,676-line `taom_debug_*.log` — 97.5% of it per-tick tracing from features that are now confirmed working (and the log is bundled into crash reports via `IModLogger.LogFilePath`, so it bloated every crash ZIP). `FileLogger` has no level filter, so the noise was removed at the call sites; all WARNING/ERROR lines and the one-time "feature loaded" INFO markers are kept.

- **AlignmentDesertion** — the `AlignmentDesertionBehavior` desertion log (INFO, 60,432 lines this session, **0** of them player-relevant) now gates on `isPlayerOwned`, so only the player's own desertions record; AI-kingdom desertions no longer log.
- **CultureMarketplace** — the per-town daily summary (DEBUG, ~87.9k lines; 89% were `+0 injected` no-ops) reverts to its pre-2026-05-21 `>0` gate (logs only when a pass changed the roster); the per-tick "No pool for culture X" (~16.9k) and "no owner culture" lines gain once-per-culture / once-per-settlement `HashSet` guards.
- **Momentum** — deleted the per-battle `Player event recorded` DEBUG counter (~1.3k) and removed the now-dead injected `IModLogger` from `PlayerMomentumService` (enrollment/victory INFO is unaffected).
- **SpecialResources** — deleted the per-day `DAILY: net=…` DEBUG (~1.1k). The resolve/`CanAfford` DEBUG lines were left as-is (already once-per-key guarded, or bounded to the open party screen); every resource-reward line stays INFO.
- **Diplomacy** — dropped the two allowed-path `AllianceActionHook` DEBUG lines; the blocked-path INFO stays.

Net: ~170k → ~2k lines per session, with every meaningful marker preserved. One regression test pins the CultureMarketplace once-per-culture guard. Full suite 4087 pass.

### feat(caravan): AI caravans range further, trade across the war, carry fuller baskets

New `CaravanTrade` feature (`Patch59_CaravanTrade`) — four Harmony postfixes on vanilla `CaravansCampaignBehavior` private methods plus two `TaomCaravanModel` overrides, all delegating to a pure `ICaravanTradeService`. Fixes the "caravans shuttle between Minas Tirith and East/West Osgiliath and only buy one good" behavior. Mirrors the `ArmyTargeting` service+config+MCM pattern; **master-off = exact vanilla**, fully save-clean (no `SyncData`).

- **War gate** (`CanTradeWith`) — in TAOM's endless Free-vs-Evil war, vanilla only lets caravans visit non-enemy factions, which pens them into their own clustered towns. Lifts the war veto per `WarTradePolicy` (default `SameAlignmentAndNeutral`: a Free caravan reaches other Free + Neutral towns but not Evil ones). Only ever flips a war-caused `false→true`; honors the player's prohibited-kingdom list even during war.
- **Range re-weight** (`GetTradeScoreForTown`) — vanilla scores a town by `1/days`, so the closest always wins. Strips that spike and re-applies a gentler `1/(nearFieldFlatten+days)^decay` curve (clamped), with an anti-shuttle cut on the town just left. Near-equal towns tie on distance so the built-in profit estimate decides; longer profitable trips become competitive. Selection-only — profit/payout untouched.
- **Range envelope** (`CacheVeryFarDistances`) — scales the vanilla "very far" ceiling by `rangeMultiplier` so profitable distant towns aren't hard-rejected. Once per session.
- **Basket diversity** (`CalculateBudgetFactor` + `GetInitialTradeGold`) — vanilla's `budgetFactor = 0.1 + gold/5000` leaves a poor caravan buying one good; a floor + higher starting-gold let more categories clear the buy gate.

"Further = more money" is **emergent**: vanilla already prices undersupplied far towns up to 10× — the feature just lets caravans reach them, and the existing ClanFinance drip pays the owner more. No injected gold. Applies to player caravans too (MCM-toggleable). Config `caravan_trade/caravan_trade_config.json` (validated, MCM-over-JSON) + MCM "Caravan Trade" group. Research verified all bindings against installed v1.4.6.

Deep-review (5 agents): Standards PASS, Compat 24/24, Completeness COMPLETE. The data-flow agent caught one **HIGH** — the `SameAlignmentAndNeutral` default silently blocked all Neutral-faction trade because it delegated to `IAlignmentService.AreEnemyAlignments`, whose "Neutral is everyone's enemy" semantics are inverted for this purpose (the sibling `AlignmentRecruitment` feature had already documented this trap). Fixed in-session by resolving `GetKingdomSide` directly + Neutral-faction regression tests. `/review-codex` (gpt-5.5 xhigh) then returned **0 HIGH, 4 MED, 1 LOW**, all fixed/documented: the range lever now scales a read-each-time getter (`GetDistanceLimitVeryFarAsDaysForNavigationType`) instead of mutating the cache, so an MCM master-off reverts it live; a player-founded kingdom now sides by culture fallback (`GetKingdomSide`→`GetCultureSide`) so it can't trade across the Free/Evil line; and the range lever's global scope + the war gate's faction-level scope are documented honestly in the MCM hint. Codex correctly disproved a seeded player-detection hypothesis by decompiling the caravan-creation path; its double-distance MED was verified cache-backed (not a pathfind) and kept for terrain accuracy. RCA (both passes): `docs/reviews/rca-caravan-trade-2026-07-04.md`; feature doc: `docs/features/caravan-trade.md`. Tests: 58 CaravanTrade (service matrix + war-policy incl. Neutral & player-founded kingdom + config validation + binding drift-guards); full suite 4086 pass.

### chore(localization): full 11-language AI rerun + translator gap-file / parser fixes

Re-ran the AI translation pipeline (`translate_with_claude.py` → `rebuild_translation_files.py`) across all 11 AI-translated languages for every module — TAOM + TAOM_Map (settlements) + LOTRLOME_Armory. Filled the three shipped string files that both scripts had silently skipped, so they had been English-only in every non-PL language: `taom_wotr_strings` (23, momentum UI), `taom_lotr_issue_strings` (308), `taom_emissary_strings` (21). ~6,000 strings translated; $6.14 total.

- **Source-list gap fix** — added `lotr_issue` + `emissary` to `translate_with_claude.py`'s `english_source_files` and `wotr` / `lotr_issue` / `emissary` to `rebuild_translation_files.py`'s `taom_sources`; both now cover all 10 shipped `std_taom_*` files.
- **Parser hardening** — `_extract_translations` accepts the JSON response shapes the model actually emits (alternate value key, single-key wrapper, bare `{id: text}` map), not only the prescribed array. A shape drift had been wiping whole 40-string batches to 0/40 (323 strings failed the first pass; a retry after the fix rescued all but 7).
- **Residual** — 7 gender-conditional `{?GENDER}…{?}…{\?}` strings in `taom_module_strings.xml` (DE/CNs/FR/KO/TR) stay English; the placeholder-integrity guard rejected translations that changed the conditional-token count.

PL untouched (community hand-translated). TAOM_Map + LOTRLOME_Armory outputs write to the game install (external modules), not this repo. Validation: `LanguageDataXmlTests` 22/22.

### feat(momentum): endless war by default — victory is now opt-in (#327)

Added a victory on/off toggle (`victoryEnabled` JSON + MCM "Enable Victory"), **default OFF = endless war**. `MomentumVictoryService` returns None when off, so no side ever wins — the War of the Ring is tracked open-endedly. Reason: with the (intentionally) runaway momentum, an enabled threshold-victory fires almost immediately once the player has ~5 events, which ends the war anticlimactically (a play-tester hit "Long live Sauron!" unexpectedly). The victory machinery is fully wired + tested; enabling it is best paired with a future bounded-momentum rebalance. On load with victory off, a war that ended under a prior build is un-frozen (momentum/kingdoms/stats kept) so the meter resumes — an already-ended save becomes endless again.


### docs(momentum): refresh feature doc + index the feature (#327)

Brought `docs/features/war-of-the-ring-momentum.md` current with all play-test fixes (kingdom resolution, JSON-string persistence, culture-fallback + reconciling enrollment, Khand-neutral, ratio slider, colored Total), added a UI & display section + a play-test fix-history table + the runaway-momentum known-limitation, and indexed the feature in `docs/INDEX.md` + CLAUDE.md Key Paths (it was undocumented in both).


### fix(momentum): map bar now moves + colored balance total (#327)

Two play-test UI issues:

- **Map slider was pinned to one end and never moved.** It normalized the raw momentum lead against the victory threshold (500), but in a long war the lead accumulates many times past that (trimmed-at-cap events never subtract; the player gate can hold the war open), so it clamped forever. Replaced with a RELATIVE balance ratio `(free − evil)/(free + evil)` mapped to −100..+100 — the bar stays readable at any magnitude. Sign flipped so **positive = Free ahead = bar fills right toward the green end** (was positive = Evil), matching green-good intuition.
- **Popup total was an ever-growing negative number in near-invisible dark text.** Now shows the bounded balance magnitude (0–100), colored **green when the Free Peoples lead, red when Evil leads** (parchment when even) — direction by color, so the sign isn't needed; also fixes the readability.


### balance(alignment): Khand (battania) is now Neutral, not Evil

Changed `execution/alignment.json` `battania` from `evil` to `neutral`. Khand is a shared alignment key, so this applies to ALL alignment-aware systems, not just the War of the Ring meter: it no longer enrolls on the Evil side of the momentum war, no longer blocks/ is blocked by recruitment, its troops no longer desert over alignment, and it gets neutral execution-relation + diplomacy treatment. Updated the enrollment comment + the three alignment feature docs. New-campaign + live-save effective (config read at load; the momentum meter drops Khand on the next enrollment sweep).


## 2026-07-03

### fix(momentum): reload reset + blank banners + Relative-Strength 0 + narrow columns (#327)

In-game play-testing found three issues (two of them self-inflicted by the deep-review efficiency "fix"):

- **Blank Leaders/Allies banners + Relative-Strength stuck at 0/0:** the deep-review efficiency fix had swapped `Kingdom.All.FirstOrDefault(k => k.StringId==id)` for `MBObjectManager.GetObject<Kingdom>(id)` in `KingdomStrengthAdapter` + `MomentumPopupVM.ResolveKingdom`. `MBObjectManager` does NOT resolve campaign kingdoms → null → blank banners + every side strength 0 (so the daily strength award never fired). Reverted to the vanilla `Kingdom.All` idiom (the scan was never a hot path). Unit tests missed it because they mock the adapter — live-only regression.
- **State reset on save/reload:** the momentum store was synced as a `Dictionary<string,string>` (up to ~1000 entries in a deep campaign), which did not round-trip through the engine's `IDataStore` at scale — total stats and momentum reset every load. Now the store dictionary is JSON-encoded to a single string and that string is synced (key `_taom_wotr_momentum_v2`); a single string is unbounded and needs no container definition. Existing test-saves reset once (old dict-format key is ignored), then persist.
- **Popup number columns wrapped** (`12200` rendered as `200-`/`00`): the four value `TextWidget`s were pinned at `SuggestedWidth=50`, too narrow for 5-6 digit totals. Widened to 120.


## 2026-07-03

### feat(momentum): War of the Ring momentum — Evil vs Good progress tracking, victory, and map UI (#327)

Port of LOTRAOM 1.2.12's "Momentum" system onto TAOM 1.4.6, wired into the existing WotR phase machine
(`Main/Features/WarOfTheRingMomentum/` + `UI/`; ~20 services/VMs, 140 new tests, feature branch `feature/wotr-momentum`).

- **Scoring**: signed Free↔Evil momentum from battles won (scaled by casualties ÷ loser-side strength, cap 300),
  sieges (+250), raids (+200), armies gathered (+200), and a daily strength-differential award (cap 300); events
  decay after 21d/21d/21d/7d/12h. Player participation multiplies gains ×1.5 (MCM-tunable) and records toward the
  victory gate. Sides come from dynamic enrollment: every `alignment.json` Free/Evil kingdom sweeps in at FullWar
  (covers player-founded kingdoms; Neutral never enrolls; enrollment never declares wars — Diplomacy owns stances).
- **Victory**: at ±500 internal momentum (MCM 100–2000) or one side eliminated — gated on ≥5 player events (both
  sides, LOTRAOM parity) — the war ENDS: new `WarPhase.WarEnded` terminal state lifts all three peace-block layers
  (they key off `IsWarOfTheRingActive`/`ShouldBlockPeace`), cross-side at-war pairs peace out via
  `IAllianceAdapter.MakePeace` (ordering pinned by test), a localized inquiry announces the winner, meter freezes.
- **UI**: persistent on-map "War of the Ring" slider (MapView + GauntletLayer, appears at FullWar, MCM-hideable)
  opening a popup — faction banners (Gondor/Mordor), leaders/allies rows, per-type momentum breakdown with
  accumulating tooltips, total-stats table. TAOM's fork-residue MomentumView prefabs reused (already 1.4.x-migrated);
  edits: `StaticDiplomacyButton` (Diplomacy-mod dependency) deleted, dead `ListWidget` (removed in 1.4.6) replaced,
  labels localized, `KingdomIcon.xml` deleted. Zero new sprites/fonts.
- **Persistence**: primitive-dict SyncData `_taom_wotr_momentum` (Messengers pattern, no SaveableTypeDefiner);
  fixes LOTRAOM's unpersisted player victory gate. Phase + outcome persist in the Diplomacy behavior.
- **Deliberate deviations from LOTRAOM (bugs not ported)**: config event values are internal-scale units — the
  donor added them raw while comparing raw÷100 against the threshold, so its own tuning comments ("~2 sieges for
  victory") were off by 100× and the meter barely moved; raids now require an ENROLLED kingdom (donor sided raids
  by culture, so every looter raid fed Evil +200); alliance-stance reflection dropped (`StanceType.Alliance` does
  not exist on 1.4.6 — would throw at runtime); indicator VM event-subscription leak fixed.
- Config `momentum/momentum.json` (validated, defaults = donor's shipped XML values) + MCM "War of the Ring/Momentum"
  (enable, map meter, threshold, multiplier, player gate). Strings `taom_wotr_strings.xml` (localization pass pending).
- 1.4.6 drift handled: `Kingdom.CurrentTotalStrength`, `MapEventSide.TroopCasualties`, `ArmyGathered(Army, IMapPoint)`,
  `BannerImageIdentifierVM`, `GauntletLayer(string,int)`, banned `PartyBase.Owner` getter avoided via `MobileParty?.Owner`.

Not-tested: in-game meter/popup rendering + victory flow (control campaign pending; testMode phase2Day=3 fast-path).

### fix(momentum): Codex adversarial-review findings (#327)

Codex (1 HIGH, 2 MED, 2 LOW, all confirmed + fixed; RCA `docs/reviews/rca-wotr-momentum-2026-07-03.md` Codex-pass section):

- HIGH: a player-FOUNDED kingdom (id not in `alignment.json`) resolved Neutral and never enrolled — the player's own war contributions weren't counted and their kingdom never showed on the meter. Enrollment now falls back to the kingdom's CULTURE side (`GetKingdomCultureId` → `GetCultureSide`), reproducing LOTRAOM's culture-based siding for dynamically-created kingdoms.
- MED: battle momentum `casualties/loserStrength` is now clamped at 1.0 so a lopsided endgame battle can't blow past the documented `MaxBattleMomentum` cap and instant-win the war.
- MED: the enrollment sweep now prunes enrolled ids absent from the live kingdom set, so a kingdom destroyed while the feature was toggled off can't linger and block the elimination-victory count.
- LOW ×2: corrected `war-of-the-ring.md` (WarEnded phase + persisted phase/outcome) and an enrollment comment (Khand/`battania` is Evil, not Neutral).
- +6 regression tests; full suite 4,021 green.

### balance(startup-resources): retune per-culture lord gold + clan influence

New-game startup grants (`startup_resources_config.xml`; new campaigns only):

- Elves (rivendell/lothlorien/mirkwood): influence 1000 → **1250** per clan (gold stays 600k per lord).
- Erebor: gold 50k → **800k**, influence 150 → **1000**.
- Khuzait (Easterlings): gold 50k → **75k** (influence stays 1000).
- Gondor: gold 50k → **100k**, influence 500 → **1000**.
- Isengard/Dol Guldur: gold 200k → **75k**, influence 2000 → **500**; Gundabad: gold 200k → **75k**, influence 2000 → **1000**.
- Umbar: influence 500 → **1000** (gold stays 200k).
- `playerGold` and all other cultures unchanged.

### feat(culture-conversion): notables now convert with the settlement — foreign-culture notables replaced at conversion (#325)

Review confirmed the reported gap: a Mordor-captured Gondor town flipped `Settlement.Culture` (recruitment,
militia, loyalty) after the hold period, but its notables stayed Gondorian forever — nothing in TAOM or vanilla
ever changes a living notable's `Hero.Culture`, and vanilla turnover can't fix it (a notable dying at power ≥ 100
spawns a relative that COPIES the old culture; only rare low-power propertyless notables disappear for the weekly
deficit refill to backfill from the converted culture).

- `ApplyConversion` now replaces each still-alive, culture-mismatched notable in the town/castle + bound villages,
  AFTER the culture flip (replacement templates come from the NEW culture's `NotableTemplates`).
- Per notable, `CultureConversionAdapter.ReplaceNotable` runs the order-critical engine sequence: template
  pre-check (CreateNotable NREs on a missing occupation template — skip+warn instead) → spawn same-occupation
  replacement → transfer workshops/alleys/caravans (`ApplyByDeath`/`SetOwner`/`TransferCaravanOwnership` — before
  removal, or the engine destroys/reassigns them) → cancel any issue/quest (`CompleteIssueWithCancel`; relations
  deliberately NOT transferred) → zero power (suppresses the vanilla old-culture heir spawn at
  `NotableDisappearPowerLimit`) → `KillCharacterAction.ApplyByRemove`.
- Fail-safe throughout: any per-notable skip keeps the old notable + warns, never blocks the conversion or the
  daily tick. One-shot at conversion — the on-load re-apply never replaces. Restore-to-original replaces
  symmetrically.
- New `replaceNotablesOnConversion` JSON field + MCM "Replace Notables On Conversion" (default on).
- Data audit (one-off script): every conversion-eligible culture — all `taom_spcultures.xml` cultures + the 6
  vanilla-id cultures re-templated in `spcultures.xslt` — covers all 5 notable occupations; the pre-check fail-safe
  is currently unreachable for real cultures.
- Tests: +9 (8 service replacement decisions incl. flip-before-replace ordering + fail-continue + re-apply guard;
  1 config default). Full suite 3873 green. Engine signatures verified against installed 1.4.6 via `taom-src`.
- Reviews: `/deep-review` (5 agents — 0 code findings; 2 process findings fixed, RCA
  `docs/reviews/rca-culture-conversion-notables-2026-07-03.md`) + Codex adversarial (gpt-5.5 xhigh) VERDICT
  CLEAN, all 6 seeded Known Suspects disputed with decompile evidence (Review 70).

**Save-compat:** additive — no new SyncData; pre-feature converted settlements keep their old notables (documented
limitation; reconquest + re-conversion catches them up).

### balance(lords): north-orc Leadership raised to 130 average (#328)

The #322 cut left gundabad/dolguldur/goblin/mistymountainorcs lords at 74-84 avg Leadership — too weak on
morale/garrison scaling. +52 on the `north_orc_*` trio (227/127/112) + Bolgath (237): pooled resolved average
lands exactly 130.2 (per-culture 126-133). Mordor/Isengard/Dunland verified untouched; Steward stays ~100.
New campaigns only.

### docs(lords): lord-skills docs caught up to the balance arc (#322–#326)

`docs/ai-includes/lord-skills-authoring.md` (the `/lord-skills` source of truth) rewritten where stale:
regen-drift pre-flight + repoint-script contract in Quick reference, archetype catalog renumbered to current
values (74 archetypes; elf/dwarf command tiers), new "Per-culture balance variants (`archetype_alias`)" section
with the fork/alias/repoint rules, post-#326 power-threshold tiering, 7 new gotcha rows (generator drift,
per-culture regen unsafety, shared-set bleed, stale culture maps, child-lord rule, diff re-anchoring), file map +
verification checklist extended. Also: CLAUDE.md Rebalancing Tools table + tools/README gain
`apply_culture_skills_traits.py` / `repoint_evil_lord_skillsets.py` / `author_elf_lords.py` rows;
`docs/features/lord-perk-review.md` documents the khand/mirkwood grouping fix + the inline-sync mismatch cleanup;
4 lessons appended to LESSONS-LEARNED (generator drift, diff-presentation surgery, re-themed culture maps,
shared-set fork discipline).

### balance(lords): multi-culture Steward/Leadership/Tactics retune (#326)

Second balance pass on lord army stats, to resolved-lord average targets (children/rookie-template
lords excluded, per the established child treatment). Landed (resolved avg, target):

- **Elves** (mirkwood/lothlórien/rivendell): Leadership +72, Tactics +61 on all elf archetype +
  canonical sets — pooled resolved avg lands 299.8 Led / 300.2 Tac (per-culture 291–312 spread from
  set-mix composition; the three cultures share the elf sets, so per-culture exactness would need
  absurd per-canonical residuals).
- **Gondor** 200/200/190 (S/L/T, exact): +8/+26/+25 across 34 canonical sets + `elder_lord` in place;
  6 new `gondor_*` forks of the shared man sets (knight/lady/young_lady/young_lord/matriarch/lord).
- **Erebor** 280/300/310 (exact): +73/+127/+140 on the 5 dwarf sets + canonical E1_2 — dwarves are now
  the premier non-elf commanders.
- **Rohan** 180/180/190 (exact): +7/+12/+23 on rider/shieldmaiden/horse_breeder + 18 canonicals;
  4 `rohan_*` forks.
- **Dale** Tactics 175 (exact): +22 on `dale_lord` + matriarch/lord (dale-only after the gondor forks)
  + 4 `dale_*` forks (Tactics-only).
- **North orcs** (gundabad/dolguldur/goblin/mistymountainorcs — misty included per user call):
  Steward −53 on the north_orc trio + Bolgath — pooled resolved avg exactly 100 (per-culture 91–104).
- Shared base sets (`taom_knight/lady/young_*/matriarch/lord`) are UNCHANGED for
  shaghana/abanissa/rhun/harad/umbar/khand — verified byte-identical averages.
- Mechanics: 14 new fork archetypes + 3 `archetype_alias` maps in the generator (145 sets total,
  regen acceptance = exactly the planned cells); repoint script now syncs the FULL inline `<skills>`
  block from `taom_lord_skill_sets.xml` for every managed-culture lord (replaces the hand-maintained
  parity map; 187 template swaps, post-condition + idempotency PASS).
- **Save-compat:** hero skills bake at creation — NEW campaigns only.

## 2026-07-02

### content(lords): elf lord expansion — Lothlórien 10 adults (+2 new clans), Rivendell 20 adults (#324)

Party size per lord was fixed by #323, but army COUNT is capped per clan (tier<3: 1 party, t3-4: 2, t5+: 3 —
`DefaultClanTierModel`). Lothlórien had 3 adult lords in one clan; Nos Glorfindel (t6, 3 slots) had one. Now:

- **+7 Lothlórien lords, +2 clans**: `clan_lothlorien_2` **Wardens of the Naith** (t6 — Thandirion elf_lord owner,
  Baranthir, Aeglossen elf_archer, Nimlothiel elf_lady, + existing Caurmínas moved in from clan 1, fixing his
  L2-id-in-clan-1 mismatch) and `clan_lothlorien_3` **Nos Malgalad** (t5 — Malthorn elf_lord owner, Galuvir,
  Silivren elf_lady). Kingdom party slots 3 → 9; 10 adult lords, adult avg Steward 340.5.
- **+3 Rivendell lords** into Nos Glorfindel: Gildor Inglorion, Erestor (elf_lord counsellor), Lindir — clan now
  fills its 3 slots; kingdom at 20 adult lords, adult avg Steward 334.4.
- Authored by new one-off `tools/author_elf_lords.py` (--dry-run/--apply, well-formedness gate): NPCCharacter
  blocks (inline skills = live SkillSet values incl. the Steward boost, archetype traits, culture equipment
  templates a–e rotated, donor elf face keys per the existing shared-key convention) + Hero lore blurbs + Faction
  blocks (banner keys donated from existing elf clans). 4 canonical archetype pins added to the generator
  (regen-stable, byte-identical sets file). `validate_moduledata` PASS.
- Names avoid collisions — Haldir/Rúmil/Orophin already exist as Mirkwood lords, so Lothlórien's new lords use
  invented Sindarin names; Rivendell reuses canon Imladris figures.
- **Save-compat:** new heroes/clans appear on NEW campaigns only. Localization keys (`{=aom_*}`) ship with inline
  English defaults; 12-language propagation is a follow-up (`/localize`).

### fix(tools): Culture.battania is Khand, not Mirkwood — rebalance_lords mapping corrected

`rebalance_lords.CULTURE_MAP` still carried the pre-mirkwood-culture `battania → mirkwood` entry, so the 41 Variag
lords (taom_spcultures renames battania to Khand) received **elven** cultural modifiers on any `--apply` and were
folded into "mirkwood" in every balance report (71 = 30 elves + 41 Variags — the earlier session tables had this
pollution). Now `battania → khand` (no CULTURAL_MODS entry → baseline curve, same as mordor/goblin), and the real
Woodland Realm gained its missing `Culture.mirkwood → mirkwood` entry — without it those 30 lords fell through to
NO mods while Khand wore their elf bonuses. Reports now show khand (41) and mirkwood (30) separately.

### balance(lords): lord_R3_1 assigned a real elf SkillSet; child lords stay on rookie templates (#323 follow-up)

Of the 5 elf lords outside the TAOM SkillSet system, only one was a real gap: `lord_R3_1` (adult, age 30, owner of
the third Rivendell clan, placeholder name) had **no skill_template at all** — he now resolves to
`taom_elf_lord_skills` (Steward 355) via a canonical entry in the generator + a new `TEMPLATE_ASSIGN` mechanism in
`tools/repoint_evil_lord_skillsets.py` that inserts the missing attribute. The other 4 (`lord_M1_12`, `lord_L1_3`,
`lord_R1_11`, `lord_R2_11`) are **children aged 6-12** (two literally named "PlaceHolder Child") — vanilla
`spc_*_rookie` templates are the correct child-hero treatment (the generator deliberately skips age<14), so they
keep them. Also noted: Círdan's `taom_canonical_lord_R3_2_skills` is orphaned — no `lord_R3_2` NPC exists.
Adult-only elf Steward now: lothlórien 355 / rivendell 337 / mirkwood 333.

### balance(lords): elf lord Steward +100 — Rivendell / Lothlórien / Mirkwood party size (#323)

Follow-up to #322 after the mechanics correction: **Steward** is the direct party-size driver
(`StewardPartySizeBonus` = +0.25 party size per point, `DefaultSkillEffects.cs:281` → `DefaultPartySizeLimitModel.cs:266`,
v1.4.6); Leadership feeds party size only via perks (and morale/garrison). All lords of the three elf cultures get
**+100 Steward** (= +25 party size each): rivendell avg 211→300, lothlórien 194→269, mirkwood 225→322. Everyone else
byte-unchanged on both skills.

- All `taom_elf_*` sets are elf-exclusive (verified) → pure **in-place** boost: 7 elf archetypes + 9 elf canonical
  sets (Galadriel 415, Elrond 400, Thranduil 360, Celeborn 350, Glorfindel 342, Legolas 338…), plus the
  template-less `lord_R3_1` whose engine-authoritative inline block went 200→300. No forks, no repointing.
- `tools/repoint_evil_lord_skillsets.py` generalized into the balance-pass parity tool: per-template
  Leadership+Steward parity map + `INLINE_OVERRIDES` for template-less lords.
- **Finding:** the 41 `Culture.battania` lords are **Khand Variags** (evil — `taom_spcultures` renames battania to
  Variag; the generator's `khand` entry owns it), but `rebalance_lords.CULTURE_MAP` still says battania→mirkwood, so
  the analyzer folds them into "mirkwood" (71 = 30 elves + 41 Variags) and the rebalance curve would hand them elven
  modifiers. Correctly excluded here; stale mapping left for a follow-up pass.
- Not covered: 5 elf lords on vanilla `spc_*_rookie` templates (M1_12, L1_3, R1_11, R2_11 + 1) — the pre-existing
  93-lord vanilla-template gap.
- **Save-compat:** new campaigns only (hero skills bake at creation).

### balance(lords): evil-faction lord Leadership nerf — Gundabad / Misty Orcs / Goblins / Dol Guldur / Dunland (#322)

Those five cultures' lords hosted armies big enough to crush Rivendell and Lothlórien; Leadership (the army-size
driver) is cut to per-archetype targets while **Mordor + Isengard keep the base orc sets**. Average lord Leadership:
gundabad 168→81, mistymountainorcs 164→76, goblin 160→74, dolguldur 169→81, dunland 174→84 — vs Rivendell 212 /
Lothlórien 214 (unchanged, as are mordor/isengard/mirkwood — bleed-checked).

- **New variant archetypes** (only Leadership differs from the parent): `north_orc_chieftain` 175 /
  `north_orc_warrior` 75 / `north_orc_female` 60; `dunland_knight` 90 / `dunland_lady` 80 /
  `dunland_young_lord` 55 / `dunland_young_lady` 55 / `dunland_marauder` 80 (forked from `dunland_raider`,
  whose 2 mirkwood users keep 180). In-place cuts on dunland-exclusive sets: `dunland_warrior` 200→100,
  `dunland_brenin` 265→130; Gundabad's canonical Bolgath (`lord_G4_1`) 280→185 (ruler stays above his chieftains).
- **`archetype_alias`** — new per-culture hook in `tools/apply_culture_skills_traits.py` so gundabad/dolguldur/dunland
  resolve shared archetypes to their variants on any future generator run (no un-nerf on regen).
- **`tools/repoint_evil_lord_skillsets.py`** (new, one-off, `--dry-run`/`--apply`) did the actual swap for all five
  cultures — 510 `skill_template` swaps + 544 inline-`<skills>` Leadership doc-parity updates across
  `characters/lords.xml` + `lords.xslt` — instead of the generator's `process_file`, whose per-NPC re-resolution
  can't reproduce the live hand-tuned assignments (the 1f7a7a9a 149-lord drift; goblin/mistymountainorcs have no
  CULTURES entry at all). Post-condition + idempotency verified (0 swaps on re-run).
- Verified: per-culture averages match predictions exactly; `validate_moduledata` PASS; zero dangling SkillSet refs;
  lords.xslt well-formed with all 396 template ids present in vanilla SandBox `lords.xml`.
- **Save-compat:** hero skills bake at hero creation — new campaigns only; existing saves keep old stats.

### chore(lord-skills): sync SkillSet generator to the hand-tuned live XML (1f7a7a9a maintenance debt)

`tools/apply_culture_skills_traits.py` had drifted from `taom_lord_skill_sets.xml` since the legendary-lord
hierarchy commit hand-edited the XML (its own CHANGELOG note flagged this: "update its canonical entries first
if regenerating"). A blind `--apply` would have reverted 14 hand-tuned canonical-lord sets and **deleted**
`taom_sauron_skills` / `taom_witch_king_skills` / `taom_canonical_lord_M1_1_skills` (Sauron #321 would have lost
his stats). Synced: the 14 canonical `skills=` dicts now carry the live values, Thranduil (`lord_M1_1`) gained his
explicit dict, and `sauron` + `witch_king` are BASE_ARCHETYPES entries. Acceptance: regen output == committed XML
semantically (123 sets, zero value drift); the only file change is deterministic id-sorting of the 3 hand-appended
sets. Generator is once again safe to re-run.

### feat(sauron): grounded Dark Lord + dedicated `sauron` race — towering, immortal, NPC-only (#321)

Sauron (`lord_1_17`) now fights on foot: the `Horse` (`charger`) + `HorseHarness` slots were removed from both
`sauron_bat_equipment` and `sauron_civ_equipment` (`taom_equipment_sets_mordor.xml`); `default_group="Infantry"`
was already set in `lords.xslt`, so the mount was the only thing putting him in the saddle. He also moves off the
shared `elf` race onto his own **`sauron` race** so height and per-race combat tuning can target him alone:
a verbatim elf clone (same `sk_elf_basemesh_a1_*` meshes, `human_skeleton`, 10 maturity/gender skins) appended at
the END of LOTRLOME_Armory `skins.xml` + 5 Monster entries in `monsters.xml` (live install AND
`docs/reference/lotrlome-armory-snapshot/`, `.bak-sauron` backups) — race ints are skins.xml merge-order indices,
so append-at-end preserves every existing race id. Only deltas from elf: **adult `min_scale` 1.07/1.06 → 1.40**
(movie-towering; child/teen/tween/toddler skins untouched) and the race id. No `as_sauron_*` action_sets — battles
use `Monster.ActionSetCode` = `as_human_warrior`; settlement/map suffixed lookups (`as_sauron_lord`/`_map`) resolve
via the engine's native silent fallback on missing action-set ids (the elf-proven path — `as_elf_map` fires for
every elf lord party icon today). Facegen sets are CC-only: the race is **NPC-only**
(no `cultures.json` `races[]` lists it, so Patch9's allow-list dropdown can never offer it). Aging: `immortal: true`
in `race_age_config.json` (verbatim saruman — the other Maia). CombatMechanics parity: `["sauron"]` mirrors
`["elf"]` (CtbAttackBonus 20, RemoveNonOverheadPenalty) in BOTH the compiled defaults and
`combat_mechanics_config.json` (the JSON dict REPLACES compiled defaults), so the race split doesn't silently drop
his modifiers — pinned by `GetConfig_MissingFile_SauronDefaultsMirrorElf`.

Deep-review (6 agents) caught one HIGH before commit: the engine's pregnancy check runs on the FEMALE only
(`PregnancyCampaignBehavior.DailyTickHero` gates on `hero.IsFemale`), so Sauron's immortal entry alone never
gated conception with Morgha (`lord_1_18`, race-unset → human, fertile) — `TaomPregnancyModel` now also returns
0 when the SPOUSE's race is immortal, making the "no future children" promise real for immortal fathers
(Sauron today; any wraith/Saruman pairing later). RCA: `docs/reviews/rca-sauron-race-2026-07-02.md`.

Combat tuning (user decision, resolves the review's deferred weight question): the `sauron` race joins every
offensive CombatMechanics capability + charge-knockdown resistance, in BOTH config surfaces (compiled defaults
+ JSON): `knockdownResistanceMultiplier` **3.0** (above the dwarf ceiling 2.5 — the 1.40-scale Dark Lord keeps
elf Monster weight 80, so this row is what stops horse-bowling), `swingEnergyBonusFactor` **0.20** (strongest;
orc 0.15), `monsterCrushMonsterIds` + `sauron` (swings auto-crush any non-shield block, troll tier),
`orcShieldCrushRaces` + `sauron` (crushes shield blocks too, energy/skill-gated — AI-only by that mechanic's
design, and Sauron is NPC-only anyway), `cleaveMonsterIds` + `sauron` (hits keep 30% momentum and slice through).
Pinned by `GetConfig_MissingFile_SauronOffenseAndKnockdownDefaults` + updated list-count tests.

Save-compat: new campaigns only (heroes snapshot race + equipment at campaign start; `RacePersistenceService`
restores the captured race on legacy saves) — existing saves keep the mounted elf-race Sauron by design.
In-game verification owed: full restart + new campaign (Armory XML loads at process launch).

### chore(review-infra): mechanize the CombatMechanics RCA preventions — NaN-gate + parallel-builder rules now load, not just sit in the RCA

The rca-combat-mechanics RCA promised three preventive actions; promises don't fire on the next feature, rules do.
All are now mechanized: **(1)** `.claude/rules/csharp-architecture.md` gained "Engine-Float Decision Gates: NaN Must
FAIL the Gate" — the runtime sibling of the config-float rule (4th NaN-gate instance proved the scope was one
category too narrow each time; inverted early-exits like `x <= 0f` pass NaN, gates must be positive requirements,
`bool?` services return null on non-finite input) — plus config-rule point 7: dual-surface JSON+MCM values enforce
the same invariants at both surfaces. **(2)** `/deep-review` Agent 5 gained rule 4b (engine-float NaN-polarity audit
on every gate) and the toggle-coverage rule 2b gained the master-toggle fold check (enumerate EVERY override incl.
constant getters when a hint promises "off = vanilla" — the `GetHorseChargePenetration` miss). **(3)**
`harness-facts.md` gained "Parallel builder briefs: shared sub-problems get ONE prescribed solution" (pre-dispatch
checklist; the CombatMechanics findings all lived at builder seams) + CLAUDE.md briefing item 6 pointing at it.
LESSONS-LEARNED entries for both rule classes were appended earlier the same session; the RCA's codify section now
records each action as DONE with file refs. Review log: REVIEW-LOG entry 69; AGENTS.md lessons updated to 69 reviews.

### feat(combat): CombatMechanics — crush-through, creature cleave/unstoppable, weight-based charge knockdown, shield penetration, race modifiers (#320)

Clean-room adaptation of five mechanics from a reference damage model (reference repo
commit `d8ded52`, GPLv3 — no code copied; constants/formulas recorded as facts in
an internal spec), plus two TAOM-original systems.
New `TaomCombatMechanicsModel` occupies the engine's single `AgentApplyDamageModel` slot by DERIVING from the
CareerSystem `TaomAgentApplyDamageModel` (now `abstract` — career damage passives ride via inheritance;
registration swapped at the one `AddModel<AgentApplyDamageModel>` site in `SubModule.cs`). Nine thin overrides
delegate to four pure services (`CrushThroughService`, `ChargeKnockdownService`, `CreatureCombatService`,
`ShieldPenetrationService`) + a shared `RaceCombatModifiersResolver` (lazy race-name validation — the registry
is engine state — with validate-before-lookup so invalid race ids get Neutral, never the "human" fallback row).

Mechanics: **skill-based crush-through-block** (exponential skill-gap curve over a 30-point dead zone, capped
50% at Δ200, energy-gated at 25 with a momentum ramp, off-angle ×0.5; the vanilla 58f overhead path is
untouched); **monster auto-CTB** (troll/mûmakil/elephant/spider swings crush any non-shield block); **AI-orc
shield-CTB** (orc-family races crush even shield blocks, energy/skill-gated, never the player); **creature
cleave** (troll/mûmakil hits keep 30% momentum AND force SlicedThrough past vanilla's chain-terminating
Bounced/Stuck branches — both overrides verified necessary from the 1.4.6 momentum wiring); **creature
stagger immunity** (per-monster damage thresholds; shrug-off also suppresses knockback/knockdown/dismount by
engine design); **weight-driven charge knockdown** (TAOM-original: `Monster.Weight` ratio × charge speed ×
per-race resistance — Branch A auto-floors at ratio ≥8 [mûmakil 9999 vs man 80 ≈ 125], Branch B scales the
vanilla `DecideCombatEffect` penetration by weight ratio around neutral 6.0 = Native horse+rider/human so
horse-vs-man stays ≈ vanilla, and keeps the 0.7-dot KnockBack gate; horses can't floor 160-weight trolls);
**shield penetration** (config item-id/weapon-class lists — default javelins — grant
CanPenetrateShield/MultiplePenetration after base, preserving the vanilla Javelin+Impale grant; runtime-flag
shield-damage ÷0.3 correction for the native underestimation, config-gated pending a 1.4.6 control-battle
re-verify); **per-race combat modifiers** (one JSON table: dwarf ctbDefense +15 / knockdown-resist 2.5× /
stagger 1.5×, elf ctbAttack +20 + no off-angle penalty, orc "Brute" swing-energy +15%, uruk_hai +10% + 1.25×;
"tree-spirits dig in" is a future data row, not code).

Config `combat_mechanics/combat_mechanics_config.json` (validated: FiniteFloatValidator before every range,
ordering invariants, unknown weapon-class/race-name entries skipped+warned, `ObjectCreationHandling.Replace`
so JSON lists replace compiled defaults; app-restart reload scope) + MCM "Combat Mechanics" (GroupOrder 24,
master + 8 mechanic toggles + 2 sliders; master off = exactly pre-feature behavior). 107 new tests
(decision-matrix boundaries: dead zone 30/31, energy-gate 25, damage==threshold, roll==chance; one test per
config validation rule; NaN-gate regressions on engine inputs; `CombatMechanicsModelInvariantsTests`
reflection-pins the derivation + abstract parent + exact override set under the BindingVerification harness).
Full suite 3862 green; API snapshot refreshed (44 GameModels) and `-Check`-reproducing. Engine facts verified
against installed 1.4.6 via ilspycmd (`DecideCrushedThrough`/knockdown/momentum call-site flow,
`Monster.Weight`, `RelativeSpeedLimitForCharge` float.MaxValue default, WeaponFlags bit values). 6-agent deep
review (standards/compat/efficiency/completeness/data-flow/spec-conformance): all 8 findings fixed in-session
— per-hit Substring normalization replaced with construction-time variant expansion, engine-input NaN gates
rewritten to positive polarity (4th instance of the NaN-gate class — new LESSONS-LEARNED rule),
`GetHorseChargePenetration` now folds the mechanic toggle, MCM slider floor aligned to the JSON ordering
invariant, enum-name cache for the missile/shield paths. RCA: `docs/reviews/rca-combat-mechanics-2026-07-02.md`.
Owed in-game: control battles (mûmakil charge, troll cleave, dwarf line vs cavalry, javelin-vs-shield
correction A/B).

Research: SandboxAgentApplyDamageModel, MissionCombatMechanicsHelper, Mission.ChargeDamageCallback/CreateMeleeBlow, Monster, AttackInformation (installed 1.4.6)
Save-compat: No SyncData, no save-format impact — pure GameModel + config.

### refactor(special-resources): unify the three earning-notification blocks

`SpecialResourcesBehavior` carried three near-identical resolve→guard→display blocks (`NotifyEarning`,
`NotifyEarningDelta`, and an inline copy in `OnMapEventEnded`). One `NotifyEarning(..., float? before = null)`
helper now covers all earning toasts (null = running-total display; non-null = positive-delta-only). Deliberate
display-only wording change, verified in the diff: the victory toast reads "+N X from victory" (was "+N X earned
from victory"), matching the other delta toasts. Round-4 micro-cleanup O1; display text only, no service logic
touched. Branch: `refactor/round4-micro-cleanups`.

### chore(research-infra): decompile dump refreshed to v1.4.6 — category tree no longer lags installed

The `E:\Decompiled_Bannerlord\` category browse tree (Campaign/, MountAndBlade/, …) was still the v1.4.5
decompile (manifest 2026-05-30) while the installed engine and the `_shipping_build`/`_editor_build`
dual decompile (regenerated 2026-06-12) were v1.4.6 — every research task carried a "dump is one version
behind" caveat. Preserved the v1.4.5 category tree + manifest to `_categories_v1.4.5\` (joining the
existing `_shipping_build_v1.4.5\` baseline), regenerated via `tools/decompile_to_folder.ps1 -Force`
(59 DLLs, 60s), verified the new manifest reads v1.4.6 and spot-checked `GetTownGoldChange` against the
installed-DLL formula. CLAUDE.md version caveats updated (4 sites); `taom-src` on installed DLLs remains
authoritative for signatures after any future bump.

### feat(settlement-economy): tunable town market-gold regeneration — towns no longer stay broke (#317)

User reports: town markets drain to 0 gold and never recover, so players can't sell loot. A 10-agent
investigation (formulas verified on installed 1.4.6) found no TAOM bug — an equilibrium mismatch: the engine
regenerates town gold daily toward `10000 + Prosperity×12` at 25% of the deficit (`GetTownGoldChange`, sole
caller `ItemConsumptionBehavior.UpdateTownGold`), but TAOM's drains run ~2× vanilla (LOTRLOME loot computes to
~2.2× vanilla item values via the engine's `2.75^tier` formula — #318; +22% villager deliveries at 2.78 avg
bound villages/town), so wartime loot dumps + deliveries pin towns at ~0. Refuted: garrison wages (clan
expense, never `Town.Gold`); CultureMarketplace injection (moves no gold). Fix: `TaomSettlementEconomyModel :
DefaultSettlementEconomyModel` (SettlementFood donor pattern — thin model → pure `SettlementEconomyService`
with banker's-rounding parity → validated `SettlementEconomyConfigProvider`) overriding ONLY
`GetTownGoldChange`, knobs in `settlement_economy/settlement_economy_config.json`, **shipped base 25000**
(slope 12 / rate 0.25 stay vanilla — base-heavy buffing gives collapsed towns 2.5× faster recovery while
median towns gain ~29%; adversarial review confirmed no runaway loop, drains are goods-bounded). Castles never
reach the override (`DailyTickTownEvent` iterates `Town.AllTowns` only). MCM "Settlement Economy" master
toggle (off = base passthrough = vanilla); applies to existing saves (~90% convergence in 8 days). 29 tests.
Data companions: `tools/analyze_settlement_prosperity.py` (read-only report; found 89 castles flat @600 + 31
towns flat @3500 generator defaults) + `tools/rebalance_settlement_prosperity.py` (lift-only vanilla
quantile-map, dry-run validated: 141 raised / 0 lowered; `--apply` deferred to user — edits the live TAOM_Map
module). Follow-ups filed: #318 (LOTRLOME value rebaseline), #319 (CultureMarketplace filter defeats the
price-crash anti-farming guard; its stale "60-item cap" doc line corrected to 200). New engine-reference
section "Town gold — the market wallet" in `docs/reference/engine/settlement-economy-food-prosperity.md`;
feature doc `docs/features/settlement-economy.md`.

### fix(hero-race): uruk saves preview true-to-race on the Load Game screen (per-race allow-list)

User report: a new uruk (Mordor) campaign previewed as a bald human on the save list, though CC and in-game
rendered correctly. Root cause was TAOM's own `Patch55_BasicTableauRaceGuard` (2026-06-24, #299): it coerced
**every** custom race to human in the `BasicCharacterTableau` agentless native build because a **dwarf** head had
proven the morph-data AV (#295) — no other race was ever tested. An instrumented pass-through build showed the
native build renders **uruk fine** (all uruk skins ride `human_skeleton` with `sk_uruk_basemesh_a_*` meshes), so
the wholesale coercion was too broad for it. `BasicTableauRaceGuard` refactored from a hardcoded int set (`{0}`)
to a name-based `TableauSafeRaceNames` (uruk verified 2026-07-02) resolved per call via `IRaceManager` — ids are
skins.xml merge-order indices and shift with the module set; validate-before-lookup so an invalid id coerces
instead of riding the `GetRaceNameFromId` "human" fallback; any resolution throw fails safe to the human base
(worst case a human thumbnail, never a CTD). Cold-menu name resolution verified safe: `FaceGen.CreateInstance()`
runs from the engine's native `OnLoadCommonFinished` before the initial screen. Dwarf and all unverified races
stay coerced; the per-race verification recipe is documented in `docs/features/hero-race.md`. 9 guard tests
(safe-race pass-through, casing, dwarf/elf coercion, invalid-id + fallback-trap pins, throw fail-safe) + a
`Patch55` binding drift-guard pinning `BasicCharacterTableau._race` as `int` against the installed engine
(the `____race` field injection isn't covered by the generic `HarmonyPatchBindingTests` target resolution).
Reviews: 5-agent deep review 0 findings; Codex adversarial review CLEAN (0 P1/P2/P3 — all 6 Known Suspects
disputed with decompiled evidence; cross-session race-index drift classified vanilla-equivalent residual).
Review 67 in `docs/reviews/REVIEW-LOG.md`; issue #316; commit `4697ada5` + review-artifacts follow-up.

### balance(party-templates): stack maxes raised to 50 — bandit + kingdom hero parties (#315)

Map bandit parties averaged 20-25. Spawn size is `min + (max-min) × ratio` per template stack, the bandit ratio
averages ~0.2 early game, and `Patch39_BanditPartySize` caps its scaling at each stack's `max_value` — so the
template max is the binding lever. Per user direction (literal per-stack reading, chosen over a total-≈50
scaling with consequences stated): `max_value="50"` on every stack of the 8 bandit cultures' raider + boss
templates and all 221 `kingdom_hero_party_*` templates (2,607 stacks). The 1/1 hideout-boss hero stacks stay
1/1 (one boss is load-bearing for the boss conversation); `min_value` untouched; looters stay vanilla. Applied
via the new idempotent `tools/raise_party_template_maxes.py` (`--dry-run`/`--apply`, CRLF/BOM-preserving).
Expected: bandit parties ~30-75 early game, up to ~200 endgame. Accepted trade-offs: lord spawns can exceed the
party-size limit (engine adds the templated roster verbatim — no clamp; over-limit lords can't recruit and pay
big wages until attrition) and mercenary/outlaw templates lose their fixed min=max compositions. Value-only
change — save-compatible; full game restart required to load the new values; already-spawned parties keep their
size.

### fix(starting-equipment): non-Gondor characters naked after career until a full game restart (+ prevention)

The 2026-06-30 starter-armor change authored 12 new `LOTRLOME_items/<culture>/starter_armors.xml` files. On first
play every **non-Gondor** character was naked after selecting a career (Gondor fine). Not a data defect: Bannerlord
loads managed item XML in two one-shot phases — it **registers** each `<XmlName id="Items"
path="LOTRLOME_items/<culture>">` *directory* at process launch (`Module.cs:246→1032`) and **globs** it
(`DirectoryInfo.GetFiles("*.xml")`) at campaign start (`Campaign.cs:1471 LoadXML("Items")` →
`MBObjectManager.cs:894/900/901/903`), with no hot-reload. A file created **after** launch is invisible until a
full restart; Gondor's `starter_armors.xml` pre-existed the last launch, which is why only it was clothed. A full
restart loads all 12 files (user-confirmed) — no data change needed. Mechanism decompile-verified and
adversarially checked (workflow `naked-regression-prevention`).

Prevention (the reason `validate_moduledata` PASS + green build + green tests didn't catch it — none start a
campaign or instantiate `MBObjectManager`): documented the new-file/restart blind spot in
`.claude/rules/moduledata-validation.md` (auto-loads on ModuleData edits) and
`docs/features/starting-equipment-tuning.md`; added an RCA lesson to `docs/reviews/LESSONS-LEARNED.md`; and both
`tools/generate_starter_armor.py` and `tools/wire_career_starter_armor.py` now print a RESTART-REQUIRED +
verify-in-game reminder after `--apply`.

### fix(battle-balance): new-campaign CTD — throwing `PartyBase.Owner` getter banned assembly-wide (crash 0b462fd8)

Every v2.0.8.0 campaign crashed within its first in-game day: the engine's settlement daily tick feeds every
`settlement.Party` into `TaomPartyHealingModel.GetDailyHealingHpForHeroes` (added in the 2026-06-26 career
pip-bonus wiring, `9034e5dc`), which resolved the career-passive hero via `party?.Owner`. `PartyBase.get_Owner`
throws for a settlement party whose `OwnerClan` is null — `Settlement.Owner => OwnerClan.Leader`, unguarded —
and TAOM_Map's `retirement_retreat` (the lone `CustomSettlementComponent` settlement among 988) is exactly that.
A `?.` on the result cannot guard a getter that throws internally (`adapters.md`, the #281 family — this is the
third shipping instance of the class, and the #281 fix itself had planted `party.Owner?.Culture` inside the
"null-safe" `ResolvePartyCulture` chokepoint).

- New `CareerPassiveHero.ResolveId` (`Main/Features/CareerSystem/`): `(party?.MobileParty?.Owner ??
  party?.LeaderHero)?.StringId` — `MobileParty.Owner` (`=> _partyComponent?.PartyOwner`) is the safe owner
  accessor; owner-first order preserved so player-owned caravans/garrisons led by non-career companions still
  resolve to the player. All 6 career-passive call sites route through it (PartyHealing ×2, PartySize,
  PartyTroopUpgrade, BattleReward, Raid); `ResolvePartyCulture`'s owner limb swaps to
  `party.MobileParty?.Owner?.Culture`.
- **Prevention:** `PartyOwnerGetterBanTests` walks the raw IL of every method body in `TAOM.dll` (incl. generic
  definitions and compiler-generated types) and bans `PartyBase.get_Owner` outright. RED at 7 violations
  pre-fix — it found a 7th site (`TaomRaidModel`, `attackerSide?.LeaderParty?.Owner`) that text grep missed —
  GREEN post-fix.
- Intended behavior deltas (deep-review-verified negligible): settlement parties no longer resolve a
  career-passive hero (passives are player-hero-exclusive; `settlement.Party` rosters hold no combat members),
  and settlement-party culture feats fall to the `Settlement.Culture` field (vanilla `HasFeat`'s own final limb).
- 5-agent deep review: standards/compat/efficiency/completeness/data-flow all PASS; installed-1.4.6 verification
  of all 10 `PartyComponent.PartyOwner` overrides confirms the replacement chain cannot throw for any
  validly-constructed party. RCA: `docs/reviews/rca-party-owner-getter-nre-2026-07-02.md`.
## 2026-07-01

### chore(hooks): remove CLAUDE.md from config-protection's blocked list (user decision)

`config-protection.sh` no longer blocks Edit/Write to CLAUDE.md — explicit user decision (2026-07-02, solo
developer): the agent maintains CLAUDE.md as living documentation and the block forced a manual approval on
every routine doc correction (e.g. the #305 rename remainder). The hook itself stays: Directory.Build.props,
settings.json/settings.local.json, and ADRs remain protected — those gates guard against the agent weakening
build config, permissions, and architecture decisions rather than against collaborators. CLAUDE.md's Hooks
table updated to match.

### docs(claude-md): update the Elephant/Mûmakil + VolunteerRecruitment Key Paths rows for the 2026-07-01 refactors

USER-AUTHORIZED CLAUDE.md edit (config-protection deliberately bypassed by explicit instruction, hook untouched):
the War Elephant and Mûmakil rows now describe the shared `Main/Features/ElephantLike/` BT nodes bound via
`ElephantCombat.Profile`/`MumakilCombat.Profile` and the thin service bindings (#305) instead of the deleted
`BehaviorTreeElements/` folders and `ElephantAttackActions`; the VolunteerRecruitment row points at the
`RecruitmentPools/VolunteerRecruitmentService.<Culture>.cs` partial split (#308). Closes the "known remainder"
from `rca-refactor-stack-2026-07-01.md`.

### refactor(hero-race): extract RaceTableauPositioning from CharacterTableauService (4x duplicated, untested)

The per-race tableau frame-offset block was duplicated FOUR times inside `CharacterTableauService`
(character + mount frames in both refresh paths) with zero tests, and its axis mapping is deliberately
unintuitive (config `Horizontal`→`origin.y`, `Vertical`→`origin.z`, `Zoom`→`origin.x` — camera-relative naming
from the donor CharacterAvatarPatch config). The offset math + the case-insensitive config-lookup builder now
live in pure `RaceTableauPositioning` with 8 tests pinning the axis mapping, null-item passthrough,
struct-copy non-mutation, and lookup semantics (case-insensitive, skip-empty, last-wins). Service behavior
byte-identical. Round-3 target R5 (the survey's other two untested-pure-logic claims —
PlainTextCrashReportRenderer + ShaderPrecompileRunner — were FALSE: both already have test files; caught by
inline vet after the survey's vet agents were rate-limited). Branch: `refactor/round2-cleanups`.

### fix(elephant)+review(round2): deliver the promised mission-end telemetry + round-2 RCA

Round-2 deep review (5 dimensions + adversarial verification): behavior preservation, efficiency, and wiring
parity all clean; one confirmed finding — R4's claimed "elephant gains late-attach telemetry" was only
half-delivered (mid-mission first-late log wired, mission-end summary missing). Fixed:
`ElephantMissionBehavior.OnRemoveBehavior` now emits the Spider/Mûmakil-parity summary and clears the error
dedup. RCA: `docs/reviews/rca-round2-cleanups-2026-07-01.md`; new LESSONS-LEARNED rule — a commit message's
claimed deltas are part of the diff, verify each before committing. Issues #309-#312 cover round 2.

### refactor(advanced-combat): extract shared CreatureTreeTracker from the three cloned creature MissionBehaviors

Spider/Elephant/Mûmakil MissionBehaviors each carried an identical ~40-line attach/prune block (shadow component
list, dedup TryAttach keyed on the Monster predicate, first-tick scan, late-spawn attach, dead-agent pruning) —
and the copies had already drifted: Spider/Mûmakil gained late-attach telemetry the elephant copy never did. The
bookkeeping now lives once in `Main/Features/AdvancedCombat/CreatureTreeTracker.cs`; the three behaviors keep
their own log tags, Armory-drift guards, and feature-specific work (howdah, summaries). Two deliberate log-only
deltas: the elephant gains the late-attach counter + first-late log (drift repair), and all three share the
tracker's build-failed message shape. The warg keeps its own wiring (different predicate mechanism + wording +
extra infra — forcing it in fails the simplicity bar). Boundary code, game-tested per ADR-008. Round-2 target R4.
Branch: `refactor/round2-cleanups`.

### test(behavior-trees): characterization tests for the inlined BT builder (zero coverage before)

The vendored-then-inlined `BehaviorTrees` builder (`Main/BehaviorTrees/BehaviorTreesCore.cs`) is load-bearing for
all four creature features but had no tests. 9 new tests pin the semantics the creature trees depend on: the
blackboard reflection-copy shares the tree's `BTBlackboardValue` INSTANCES with nodes and decorators at Add* time
(so trees must initialize blackboard values in their ctor — a post-build reassignment does not propagate), a node
whose blackboard interface the tree lacks fails the build with `MissingTreeBlackBoardException`, get-only
blackboard properties fail with `IncorrectPropertyException`, non-blackboard interfaces are ignored, `Up()` past
the root throws, and a trivial tree executes its task exactly once per `RunTree`. Round-2 target R3. Branch:
`refactor/round2-cleanups`.

### refactor(core-validation): consolidate copy-pasted SafeClamp helpers into TAOM.Core.Validation.SettingClamp

The byte-identical private `SafeClamp` (float, NaN-guarded) / `SafeClampInt` helpers copy-pasted across the
SmartCavalryAI, BanditManagement, CultureConversion, and CastleRecruitment settings providers now live once as
`SettingClamp.Clamp` overloads beside `FiniteFloatValidator`, with 15 new tests pinning the exact semantics —
including the asymmetry the consolidation surfaced: a NULL setting takes the default and flows through the range
clamp, while a NaN/Infinity setting returns the compiled default verbatim (early return). Providers keep their
per-knob ranges; only the mechanism is shared. Round-2 target R2. Branch: `refactor/round2-cleanups`.

### refactor(companion-tactics): delete four orphaned BattleActionBar action enums

`ShieldAction`, `PolearmAction`, `CavalryAction`, `RangedAction` (Main/Features/CompanionTactics/BattleActionBar/Models/)
had zero references outside their own definition files — repo-wide grep across C#, XML, and prefabs; the single
`RangedAction` hit was a substring in a test-method NAME, not a type usage. Deletion holding parity (3688 tests
green). Round-2 target R1 from the vetted duplication/dead-code survey. Branch: `refactor/round2-cleanups`.

### review(refactor-stack): 6-dimension deep review — code clean, 2 stale-doc findings fixed + prevention installed

`/deep-review` of the 4-branch refactor stack (#305-#308) via a 6-agent workflow (standards, installed-DLL API
compat, efficiency, completeness, data flow, behavior-preservation diff audit) + adversarial verification: **zero
code findings** — registration parity, Harmony wiring, ctor argument order, engine signatures, hot-path caching
all held. Two confirmed findings, both stale docs: `docs/features/elephant.md` + `mumakil.md` still pointed at the
deleted `BehaviorTreeElements/` folders and pre-unification type names — fixed. RCA:
`docs/reviews/rca-refactor-stack-2026-07-01.md`; durable prevention: LESSONS-LEARNED "structural refactor sweeps
must cover living docs" + a mandatory Documentation-sweep step 6 in the refactoring-specialist agent. Known
remainder: CLAUDE.md lines 372-373 carry the same stale names — edit blocked by config-protection (needs user
approval). GitHub issues #305/#306/#307/#308 filed for the four refactors.

### refactor(troop-progression): split VolunteerRecruitmentService per-culture pools into partial-class files

The 994-line service is now a 264-line core (maps, JSON loader, conditional→settlement→clan→culture cascade,
weighted pick, test helpers) plus 15 per-culture partial-class files under
`Main/Features/TroopProgression/RecruitmentPools/` — each culture's pools and their design-rationale comments
(Codex findings, user specs) live together, moved verbatim; the static ctor is unchanged. **Deliberate deviation
from plan T5's JSON migration:** the existing Gondor pattern is JSON-override-with-hand-written-fallback, so
extending it to 14 more cultures would have created a dual source of truth per culture and stranded the
rationale comments (JSON has none) while the 2,698-line test suite pins the hand-written maps — rejected per
`simplicity-criterion.md`; the split delivers the modularity goal with zero functional change. 3688 tests
green. Branch: `refactor/recruitment-pool-split` (plan T5, restructured).

### refactor(faction-map): extract PolygonWidget hit-test math to unit-tested AlphaHitMap + PolygonPointParser

`PolygonWidget` (1,140 lines, previously zero unit coverage) now delegates its pixel-accurate hit testing to
`AlphaHitMap` (downsampled max-alpha build + normalized opaque lookup, the off-by-one-prone index math; the
DS=4 constant was duplicated in builder and lookup and is now single-sourced) and its `Points` parsing to
`PolygonPointParser` — both TaleWorlds-free, 19 new tests, TDD (RED confirmed before implementing). One
deliberate fix: `PointsToString` formatted with the CURRENT culture while parsing was invariant, breaking
round-trips on comma-decimal locales; formatting is now invariant both ways. Plan-scope deviation: no
point-in-polygon code exists (hit-testing was always alpha-map only) and the hover tween is 4 lines — the
planned `AnimatedFloat` extraction was rejected per the simplicity criterion. Build + 3688 tests green.
Branch: `refactor/polygon-widget-math` (plan T4).

### refactor(submodule): extract the private-target manual-patch block to ManualPatchApplicator

The ~66-line run of AccessTools-resolved `_harmony.Patch(...)` calls for PRIVATE engine methods
(SettlementGuards ×2, BannerColor MobilePartyVisual/AgentVisuals/MapConversationTableau ×2, CompanionTactics
captain tooltip) moves verbatim from `OnGameInitializationFinished` to `Main/ManualPatchApplicator.ApplyAll`,
apply order + fail-safe warnings unchanged. `SettlementGuardsWiringTests` re-pinned to the new location plus a
new assert that SubModule still invokes `ApplyAll`. SubModule.cs is down to ~930 lines from 944 pre-T2. Build +
3669 tests green. Branch: `refactor/submodule-slim` (plan T3).

### refactor(submodule): extract OnGameStart registration block into ordered registration methods (ADR-002)

OnGameStart carried ~250 inline lines of behavior/model registration. The block now lives in seven private
static registration methods (`RegisterProgressionAndIdentity` → `RegisterCampaignLifeBehaviors`) invoked in the
original statement order from a slim coordinator that hoists the shared `careerPassives`/`culturalFeats`
resolves. Pure mechanical move — every AddBehavior/AddModel/RemoveBehaviors/SuppressAll call is verbatim and
order-preserved (script-verified token counts). Build + 3668 tests green. Branch: `refactor/submodule-slim`
(plan T2).

### refactor(elephant-like): unify Elephant + Mumakil duplicated attack code into a shared ElephantLike layer

The Mûmakil's attack service, BT task base, cooldown/engage decorators, blackboard interface, and action caches
were byte-identical clones of the war elephant's (only type names + config constants differed — verified by
name-substituted diff). Both features now bind a shared `Main/Features/ElephantLike/` layer: a pure
`ElephantLikeAttackService` base (ctor-bound tuning) behind per-creature marker interfaces (IoC registration +
`TaomAgentStatCalculateModel` injection unchanged), and shared BT nodes parameterized by an
`ElephantLikeCombatProfile` (scan ranges, blow magnitude, clip caches, lazy service resolver).
`IsElephantMonster`/`IsMumakilMonster` collapse to `IsCreatureMonster`. Zero behavior change — 3668 tests green
before and after, net −125 LOC. Branch: `refactor/elephant-mumakil-unify` (plan T1 of the 2026-07-01 refactor
target audit).

### diag(troop-weight): add temporary troop-count diagnostic for special-currency undercount report

A player reported 30 troops (10 special-currency) showing as 20 on the campaign-map nameplate + party-size
counter. Static analysis ruled out every TAOM mechanism as a cause of an *undercount*: Patch17 TroopWeight is
increase-only, a missing weight entry defaults to 1.0, the display hooks walk the full roster, and no roster-add
path bypasses `AddToCounts`. The symptom needs runtime roster state (wounded vs. troops living outside the main
party vs. a stale cached count) to resolve, so this ships an instrumented build to capture it.

- **`TroopCountDiagnosticsBehavior`** (`Main/Features/TroopWeight/Diagnostics/`) — on party-screen open, logs the
  main party's raw + weighted counts (per-slot bodies, wounded, resolved weight, special-currency flag,
  `EnableTroopWeight`) under a `[TroopCountDiag]` prefix, plus a scan of where the player's special-currency
  troops live across clan war parties + garrisons. Runs regardless of the Troop Weight setting; whole path is
  try/catch'd.
- Pure, unit-tested `TroopCountDiagnosticsFormatter` (6 tests) owns the line formatting incl. a slot-bodies vs.
  `TotalManCount` MISMATCH detector for the stale-count hypothesis.
- **Temporary** — registered in `TroopWeightIoC` + `SubModule`; both the behavior and its registrations are to be
  removed once the log pins the root cause and the real fix lands.


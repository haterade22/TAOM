# RCA: campaign start options maintainer review (PR #1441, 2026-11-18)

## Top-line

The maintainer review of PR #1441 (campaign rules picked on the new-game screen and pinned into the
save) ran `/deep-review` tier 1: lens 5 data flow, lens 2 engine compatibility and lens 7 XML, at the
definition's effort. None found a CRITICAL or HIGH, so no second wave ran. Lens 2 verified 31 engine
uses with 0 incompatible. The owner confirmed the two gameplay choices on 2026-11-18: rules are
pinned per new campaign, and the start screen offers only War of the Ring and Sandbox. Codex did not
run: it is out of usage.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| R1 | MEDIUM | The Strict preset row said desertion is blocked across the Free and Evil line. `AlignmentRules.Strict` turns alignment desertion on (lens 7). | Player text contradicts the code | The row was written from the preset's intent, and no test reads the row against the rules. | Row reworded: troops of the other side desert. |
| R2 | LOW-MED | A host or server console `set` during a co-op session changed only that peer: Coop sends no `AdvancedStartData` to clients, while each peer's party size model reads the value (lens 5). | Co-op write path | Only the client case was refused. | The editor refuses while any co-op session is live; new test `Editor_OnACoopHost_RefusesToWrite`. |
| R3 | LOW | A loaded pinned save logged no values, so a bug report showed MCM rules the campaign may not run (lens 5). | Diagnostics | The pin logged only at creation. | `Begin()` logs one `CampaignStartOptions.Loaded:` line; the loaded-save test asserts it. |
| R4 | LOW | The notice told every player the console command changes the choices; it needs cheat mode (lens 7). | Player text | | Notice says "With cheat mode on". |
| R5 | LOW | The menu-row test pinned only the Phase 1 options, so a lost Phase 2 row would show an error line in the menu with every test green (lens 7). | Test gap | | The Phase 2 option and alignment item ids are in the test arrays. |
| R6 | LOW | "Five vanilla scenario parameters" in a comment, a test and two doc lines; Two Faction War has two, so six (lens 2, lens 7). | Count | | Corrected in all four places. |
| R7 | LOW | The doc gave a wrong reason for the empty-kingdom start under United Empire (lens 2). | Engine claim | | Reworded from the engine lines. |
| R8 | LOW | The adapter accepts any `T`, but only five generic types save (lens 2). | Contract not stated | | The interface comment names them. |
| R9 | LOW | Fingerprint, dedicated server file and Escape-menu label effects were not documented (lens 5). | Doc gap | | Notes in the feature doc and `server-settings.md`. |
| R10 | LOW | `CampaignOptionReadersGateTests` cannot see reads through `ITaomSettingsProvider` or a local alias (lens 5). | Gate blind spot | Regex on receiver names. | FOLLOW-UP: widen the gate or rename those members `NewCampaign...`, no issue yet (owner waived new issues for this work). |
| R11 | LOW | Four retitle rows in `global_strings.xml` serve scenarios the screen no longer offers (lens 5). | Dead rows | | Comment corrected. FOLLOW-UP: remove the rows with their language rows, no issue yet (owner waived new issues for this work). |

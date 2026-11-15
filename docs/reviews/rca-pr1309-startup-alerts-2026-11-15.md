# RCA: review of PR #1309, startup alerts for War Sails and separate library copies

PR #1309 (REDACTEDSTUFF, head 83851e7cc) shows one inquiry at the main menu when the War Sails module (NavalDLC) is
loaded, or when separate copies of Harmony, ButterLib, UIExtenderEx or MCM sit in `Modules` or in the Steam Workshop
folder. TAOM.Dependencies already provides those libraries through alias stubs. The review ran at tier 2 (UI that
reads game state and the file system at startup): lens 5 (data flow) at `effort: "high"`. Codex did not run: an
outside PR needs Mike's word for each paid pass.

Lens 5 traced 7 flows. The inquiry shows once per process, is skipped on a dedicated server, compares module ids
without case, and fails open on a missing folder or a bad `SubModule.xml`. The War Sails text says "loaded", and the
code reads the loaded module list.

| # | Sev | Finding (source) | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | For a real copy in `Modules`, the alert said "delete the folder". BUTR's zips unpack to `Modules\<ID>`, the stub's own folder, so the real copy has replaced the stub there, and deleting it removes the only module with that id; mods that depend on it then stop loading (lens 5) | Advice from the common case | The doc assumed "the stub wins", which holds only for a Workshop copy | Fixed: the text says to reinstall TAOM, which puts the stub back, and not to delete the folder. The doc and `dr3-maintenance.md` now separate the two cases. Lesson appended to `localization-ui.md` |
| 2 | LOW | The 4 new English keys had no row in the 12 language files (session, test run) | Seed skipped | Trunk was already red on that test | Fixed: seeded after the trunk merge |
| 3 | LOW | `dr3-maintenance.md` said BLSE keeps the first module per id, while a later paragraph in the same file says that is not verified, and BLSE is not installed here (lens 5) | Unverified claim | Copied from the vanilla engine path | Fixed: the claim now says "not verified" for BLSE |
| 4 | LOW | The Workshop path assumes Steam keeps Workshop items in the library that holds the game (lens 5) | External assumption | Steam behaviour, not in any decompile | NOT APPLIED: a wrong guess finds nothing and shows no alert, which fails open |
| 5 | INFO | The feature doc says the project owner decided "no issue"; the PR has no linked issue (lens 5) | Contributor text | Not verifiable from the repo | Reported to Mike |

## Why the lens caught these

Lens 5 followed the stub's deploy path (`TAOM.Dependencies.csproj`, `package_release.py`) to the folder name a real
BUTR copy uses, which the PR's tests did not model.

## In-game checks owed

`startup-alerts.md` "How to verify in game". On this desktop the stubs sit one level too deep (`_Module\`), so the
Workshop copies load and the alert lists all four; the stub case needs a redeploy first.

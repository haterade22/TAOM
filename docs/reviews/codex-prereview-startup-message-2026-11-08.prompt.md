# Codex pre-review: a startup chat message is deleted (2026-11-08)

You are the reviewer (`.ai/roles/reviewer.md`). This is a review-only assignment: do not modify,
create or delete any file, do not run a build that deploys (`./build.ps1`), and do not commit. Read
`AGENTS.md`, `.ai/policy.md` and `.ai/review-reference.md` first. Every claim needs a source location
you read in this run; mark anything you cannot prove UNVERIFIED. Other sessions are editing this
checkout, so print `git rev-parse HEAD` at the top of your report.

## The change

An uncommitted hunk on `bannerlord-1.5.x`. Only this file is in scope; run
`git diff -- Main/SubModule.cs` to see it. It removes the last statement of
`SubModule.OnSubModuleLoad`, a call to `InformationManager.DisplayMessage` with a green
"TAOM loaded successfully!" message, and the blank line above it.

The maintainer's instruction was to delete the line if it does not work. The lesson that first said
it does not work is "Nothing receives a chat message before the initial screen" in
`docs/reviews/lessons/localization-ui.md`, dated 2026-09-24. Treat that lesson as a claim to test on
the current engine, not as a fact.

Engine signatures and bodies: the installed v1.5.6 DLLs are the authority (`pwsh tools/taom-src.ps1
path <Type>`, or the `ilspy` MCP server). Do not paste decompiled engine bodies into your report
beyond the short quote that proves a claim.

## Known suspects (give each a verdict with evidence)

1. **Subscribers at that moment.** Does anything subscribe to
   `InformationManager.DisplayMessageInternal` before TAOM's `OnSubModuleLoad` returns, on any path:
   the client, the dedicated server, an engine module that loads before TAOM, `TAOM.Dependencies`,
   or a mod that this repo couples to (`docs/reference/external-mod-couplings.md`)?
2. **Callback order.** Does every module's `OnSubModuleLoad` finish before any module's
   `OnBeforeInitialModuleScreenSetAsRoot` starts, in `TaleWorlds.MountAndBlade.Module`? Is
   `OnSubModuleLoad` ever reached a second time, later, when a subscriber does exist?
3. **Side effects of the removed call.** Does constructing the `InformationMessage`, reading
   `Colors.Green` or entering `InformationManager` do anything that later code depends on?
4. **Consumers of the text.** Does any log parser, in-game check manifest, tool, test or other mod
   read the string "TAOM loaded successfully!"?
5. **Gates that read this method.** Do the source-text or IL tests that read
   `SubModule.OnSubModuleLoad` depend on its last statement (`PatchCategoryApplierTests`,
   `SubModuleServerGuardTests`, `EntryPointLineCountGateTests`)?
6. **The comment left above the deletion.** Is `Main/SubModule.cs` "No ReportPatchFailures here:
   nothing receives a message yet" still true and still complete?

## Output

Group findings by severity (CRITICAL, HIGH, MEDIUM, LOW). For each one give the file and line, the
evidence, and the failure it causes. If there are no findings, say so and list what you checked.

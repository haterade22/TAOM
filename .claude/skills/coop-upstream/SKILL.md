---
name: coop-upstream
description: Use before filing a BannerlordCoop issue or PR, answering one, or merging development into bannerlord-1.5.x. Carries the Coop team's filing, PR and branch rules.
---

# Coop upstream work (Bannerlord-Coop-Team/BannerlordCoop)

The rules the Coop team asked TAOM's agents to follow (Coodsz and Curzek, Coop team, 2026-11-13), plus
Mike's branch policy. Coop's own `AGENTS.md` is the full source: read it first, the "GitHub Etiquette"
and "Unowned AI Contributions" parts above all. Clone and remotes: `E:\repos\BannerlordCoop`
(`upstream` is the team repo, `origin` is the fork); work from a worktree, never the main clone.

## Filing an issue

1. Search issues and open PRs on every branch first (`gh issue list --search`, `gh pr list --state all
   --search`). A 1.5.x port of a `development` fix shows up there. Comment on an existing issue rather
   than file a duplicate, and link anything related.
2. Use the matching template in `.github/ISSUE_TEMPLATE`, an area tag in the title (`[SAVES]`,
   `[BATTLES]`, `[TOURNAMENTS]`), `--type Bug|Feature|Task|Epic`, `--assignee "@me"` and existing labels only.
3. Put the evidence in the issue itself. Never point at a comment that is not posted yet.
4. Attach `Coop_server.log` and `Coop_client.log`, copied out of `bin\Win64_Shipping_Client` before the
   game starts again (a new launch overwrites them). `gh` cannot upload files: ask Mike to drag them in,
   and label `has-client-logs` / `has-server-logs` once they are on the issue.
5. Name the branch, the commit, the game version and the mod list.
6. Test without TAOM when you can, so it is clear whether the bug is Coop's or the mod's. If you could
   not, say so.
7. Cite real file and line numbers on the branch you name, and mark a guess as a guess.
8. Keep mod-specific code out of Coop. Ask for a general hook instead.

## Opening a PR

1. Search open PRs on every branch first (step 1 above).
2. Target `bannerlord-1.5.x` for 1.5.x work (Coop's `AGENTS.md` says `development` for v1.4.8 work).
3. Build the body from `.github/pull_request_template.md` with `--body-file`; tick its checklist and PR
   type boxes. Fill only the core sections, in the Coop maintainer voice (`AGENTS.md`, "Review Comments
   & PR Prose"): lean, no verification write-up.
4. Fill `Resolves #N` with the issue number (a closing keyword; a bare `#N` closes nothing).
5. Add a test that fails without the fix, or say why there is none.
6. Keep the exact `## Bot Changelog Entry` heading in every PR. Leave it empty when players will not
   notice the change (log noise, tests, refactors). When players see the change, write one short line
   for players under it.
7. `--assignee "@me"`; open as `--draft` until the build and tests pass.
8. No maintainer actions unasked: no merging, approving, closing or reopening others' work.

## Keeping bannerlord-1.5.x current (Mike, 2026-11-13: "our branch")

Merge `development` into `bannerlord-1.5.x` directly and push; no PR that only ports a `development` fix.
Our own 1.5.x fixes can go in the same update. Before the push, on the merged head:

1. `dotnet build source/CoopTests.slnf -c Release -p:ModName= -p:NuGetAudit=false` (needs `BANNERLORD_DIR`).
   New `development` E2E tests that call `MapEvent.SetBattleType`, `SetMapEventSettlement` or
   `RestoreComponent` need `using GameInterface.Tests.Utils;` (the v1.5 helpers).
2. `pwsh E:\repos\TAOM\tools\check_engine_member_refs.ps1 -Dll "<Coop,Coop.Core,GameInterface,Missions dlls>"`: 0 MISSING.
3. `dotnet test source/CoopUnitTests.slnf`, `source/VerificationHarness.Tests`, `source/E2E.Tests` and
   `tools/CoopMcpServer.Tests`, all `-c Release`.
4. `git push upstream HEAD:bannerlord-1.5.x` (never a bare push to `origin`, the fork; never force).
   Report the push, then close our own PRs that the update includes, each with a one-line comment.

Co-op game runs take priority over other sessions' game runs (Mike, 2026-11-13).

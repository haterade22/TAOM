# Working on TAOM with Codex

Use Codex as the builder, reviewer, researcher or adjudicator the user requested.
This guide supplies the Codex entry points and local working details. The
[shared policy](../../.ai/policy.md), [scope map](../../.ai/scopes.md), ADRs and
feature docs remain the project rules. Do not copy them into a separate Codex
rulebook or assume access to Claude's session history.

## Start a session

Read root [AGENTS.md](../../AGENTS.md), the shared policy, and the applicable
[builder](../../.ai/roles/builder.md), [reviewer](../../.ai/roles/reviewer.md) or
[adjudicator](../../.ai/roles/adjudicator.md) role. For a diagnosis, research and
explain; do not turn it into a fix without authority. Use [docs/INDEX.md](../INDEX.md)
to find the current feature and research references, not old review totals.

Before choosing commands, check the actual workspace and available executables:

```powershell
git rev-parse --show-toplevel
git branch --show-current
git rev-parse HEAD
git status --short
Get-Command codex, python, pwsh, dotnet, ilspycmd -ErrorAction SilentlyContinue
codex --version
codex --help
```

These checks do not dispatch a model. A missing executable is a limitation to
report, not permission to install it. Inspect [development-machines.md](../reference/development-machines.md)
before using an absolute path. The desktop uses `E:\repos\TAOM`; the laptop and
hosted environments differ. `BANNERLORD_GAME_DIR` selects the game for the build
and data tools. An incomplete external module install is not a repo defect.
Do not print the entire environment or credential-bearing configuration.

Preserve staged, unstaged and untracked work. Do not stash or commit it to create
a clean review. If multiple sessions are editing, agree file ownership; use a
separate authorized checkout for review or tests that write fixtures. In
particular, coordinate changes to `Main/SubModule.cs`, IoC registrations,
project files, shared configuration, indexes and the changelog.

## What Codex loads, and what it does not

Root `AGENTS.md` is the instruction entry point. Nested instructions can affect
tasks launched below the root. Codex does not automatically expand every linked
document, so read the selected role and topic references explicitly. Restart a
session when diagnosing stale startup instructions. See the official
[AGENTS.md discovery guide](https://learn.chatgpt.com/docs/agent-configuration/agents-md).

Repository skills are under `.agents/skills/`. In Codex CLI or the IDE, use
`/skills` or mention `$taom-review` and the other names below. Names/descriptions
are discovery metadata; the body is read when selected. If the client does not
show a new skill, restart or explicitly read its `SKILL.md`. See the official
[skill discovery documentation](https://learn.chatgpt.com/docs/build-skills).

| Task | Repository skill | Authoritative workflow |
| --- | --- | --- |
| Implement an authorized feature, data change or tool | [taom-build](../../.agents/skills/taom-build/SKILL.md) | [Builder role](../../.ai/roles/builder.md) and scoped technical rules |
| Review a change or full snapshot without fixes | [taom-review](../../.agents/skills/taom-review/SKILL.md) | [Reviewer role](../../.ai/roles/reviewer.md) and [review reference](../../.ai/review-reference.md) |
| Reproduce or refute disputed findings and RCA claims | [taom-adjudicate](../../.agents/skills/taom-adjudicate/SKILL.md) | [Adjudicator role](../../.ai/roles/adjudicator.md) |
| Research engine behavior, APIs or a crash | [taom-research](../../.agents/skills/taom-research/SKILL.md) | [Engine research guide](taleworlds-research-guide.md) |
| Run scoped checks and report evidence | [taom-verify](../../.agents/skills/taom-verify/SKILL.md) | [Verification contract](../../.ai/verification.md) |

These are instruction-only skills. There are no corresponding named runtime
subagents or automatic paid dispatchers. Choosing a skill does not authorize
extra writes or paid model calls. Plain `.ai/roles/` documents are not Codex
runtime profiles.

Claude's `/verify`, `/research`, `/deep-review`, `/review-codex` and `/ship` are
not Codex commands. Use the workflows above; do not simulate success by claiming
to have invoked unavailable skills or hooks. Technical Markdown rules under
`.claude/rules/` are shared by explicit links in the scope map. Claude-specific
frontmatter, hook activation and tools are not inherited by Codex.

## Configuration and tools

[.codex/config.toml](../../.codex/config.toml) contains existing client settings,
and [.codex/README.md](../../.codex/README.md) explains their boundary. Project
configuration requires a trusted project; command-line overrides can take
precedence. Do not assume a user profile wins over project settings. See
[official configuration precedence](https://learn.chatgpt.com/docs/config-file/config-basic).

Do not replace the selected model, approval policy, environment inheritance or
MCP configuration as part of documentation or ordinary feature work. Record the
actual model identity from runtime evidence when reporting a dispatched review.
An identity written in an old template is not evidence.

Local observation on 2026-09-11: `Get-Command codex` selected the VS Code extension
binary and `codex --version` returned `0.154.0-alpha.6.2`. Its help advertised
`on-request` and `never` approval choices, while the repository configuration
contained `on-failure`. This is a compatibility check to resolve when configuring
a run, not proof that the current application uses that binary or that the setting
was rejected. Recheck the selected executable and effective settings. This docs
change does not migrate the setting, test authentication or start a paid run.

The existing project configuration declares filesystem, Git and ILSpy MCP servers.
Check `codex mcp --help` and, when appropriate, `codex mcp list`; inspect output
locally before sharing it. A declaration or listing does not prove a working
connection. Confirm available tools in the current client with a harmless read.
Do not assume root `.mcp.json` and Codex TOML are interchangeable. See the
[official MCP configuration guide](https://learn.chatgpt.com/docs/extend/mcp).

| Need | Baseline without MCP | Optional connected tool |
| --- | --- | --- |
| Find/read source | `rg`, Git and shell file reads | Filesystem or code-index tools |
| Inspect history/diff | `git show`, `git diff`, `git log` | Git MCP |
| Verify installed TaleWorlds types | `tools/taom-src.ps1` or `ilspycmd` | ILSpy MCP |
| Query/validate ModuleData | Existing `tools/validate_moduledata.py` and source | `tools/taom_mcp_server.py`, if configured and connected |

Discover actual tool names and schemas before calling them. Do not install a
server, reconfigure another client's MCP, log in, or broaden filesystem roots
without the required authority. Review both native shell and external-tool
permissions; a shell sandbox does not establish an MCP server's write boundary.

## Research before engine-dependent changes

Start with the relevant [engine process doc](../reference/engine/), then inspect
the installed type and its consumers. The [research guide](taleworlds-research-guide.md)
has the detailed method. `tools/taom-src.ps1 path Fully.Qualified.Type` is the
repository helper: inspect its parameters and the resolved game before using it.
It writes a versioned cache under the user's profile, which may need separate
permission. It is not a workspace-only read. Never run its cleanup modes as an
automatic recovery step.

The pre-decompiled dump helps navigation, but installed DLLs establish current
signatures. Some assemblies live under game `Modules/<module>/bin/`, not the
top-level `bin/`. Shipping client, server and editor builds differ. Check the
appropriate build before concluding a type does not exist. Keep third-party
no-decompile restrictions in scope; see [provenance rules](../../.claude/rules/provenance.md).

Read the event raise site, every competing patch on the relevant engine method,
and the base method an override calls. Quote the proving code with its assembly,
build/version and location. Missing native/runtime evidence stays UNVERIFIED.
Do not turn a remembered review lesson into an unverified engine claim.

## Implement and verify

Follow TDD and [the architectural contract](../../.ai/review-reference.md). Keep
entry points thin, adapt sealed engine objects at the boundary and inject service
dependencies. Use the linked [scope rules](../../.ai/scopes.md) for data, UI,
native code, assets, localization and tooling too. Do not assume C# is the only
reviewable surface. Read the applicable intentional exceptions before flagging
IoC use, base calls, co-op gates or dedicated-server detection.

All current commands and caveats live in [verification.md](../../.ai/verification.md).
Build the solution and tests in the same configuration before using `--no-build`;
keep both module-copy suppression properties. Do not use the deploying default
of `build.ps1` for a review. A failed build stops its dependent test step.

For documentation/skill work, run the contract lint and the focused documentation
tests; use the skill validator where available. For data or gameplay changes,
run the relevant broader checks with real dependencies. Inspect skip output,
fixture writes and test discovery. Do not rerun a test against shared files with
broader privileges merely because it failed on permissions. Report actual
commands, totals, skips and scope limits, not stale suite counts.

Update the relevant feature doc and index when the task warrants it; the commit
body is the changelog entry.
Keep documentation grounded in the checked source. Do not automatically create
or close an issue, publish a branch, or commit a shared worktree on the strength
of a legacy completion checklist.

## Independent review and explicit paid dispatch

The maintainer authorizes paid AI dispatch when they explicitly ask to conduct
it. That request is authorization within its stated scope and any budget; do
not request the same permission repeatedly. A general build, documentation,
diagnosis or local-review request is not blanket permission for additional paid
agents. Ask only when an unresolved choice materially changes scope, cost or
data sharing. See [the shared policy](../../.ai/policy.md).

Use the [packet workflow](../../.ai/README.md) for a committed review. Confirm
base/head and isolate the source from an active builder. A requested dirty-tree
review can produce provisional findings but cannot approve a commit. If the
worktree is dirty or its directories cannot be read, do not make it "clean" by
deleting, stashing or committing unrelated work.

Give each first-pass reviewer the same acceptance criteria and relevant source,
not the builder's theory or another review. Reports from multiple Codex sessions
using OpenAI models still represent one provider, and cannot satisfy the external
provider requirement when an OpenAI model was the builder. Assign bounded slices
for a full audit; aggregate actual coverage through the validator.

When explicitly authorized to dispatch, check installed client help and available
authentication without printing credentials. The checked CLI supports `exec`
with stdin input (`-`), `--sandbox read-only` and `--output-last-message`; these
are useful pieces for an operator-run review, not an installed TAOM wrapper.
The output directory must be writable by the launcher, and source/MCP access
must still match the review boundary. Capture the actual run identity, exit
status and raw output. A timeout, error, empty result or malformed JSON is not
approval. `review.template.json` is an example record, NOT a JSON Schema for
`--output-schema`. Validate the returned record using `reviewctl`.

Do not resume the builder's session for an independent review. After blind
reports, adjudicate against source and the RCA; then the authorized builder fixes
confirmed defects and obtains a fresh packet at the new SHA. Keep original
reports. Only the human maintainer can accept risk or supply a final human
disposition. No local report parser authenticates a model or maintainer.

## Artifacts and handoff

Use an agreed, uniquely named artifact directory, normally `.ai/runs/<run-id>/`
or an authorized external directory. Do not reuse another session's scratch
folder or delete unfamiliar `.tmp/` material. Keep decompiled proprietary code,
raw transcripts, keys and game content out of commits and external uploads.
Before any cleanup, identify exact owned paths and obtain the needed authority.

End a task with what changed or was found, the source/revision examined, checks
actually run, known omissions and remaining work. Do not call local instructions
authenticated merge enforcement. Named runtime subagents, automated paid-provider
adapters, remote CI approval gates and scheduled audits are not installed by
this documentation and skill layer.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/agent-operating-manual.md](./agent-operating-manual.md)
- [docs/ai-includes/completion-workflow.md](./completion-workflow.md)
- [docs/ai-includes/taleworlds-research-guide.md](./taleworlds-research-guide.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/codex-integration.md](../reference/codex-integration.md)

<!-- backlinks-end -->

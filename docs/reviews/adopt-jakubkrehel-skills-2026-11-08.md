# Reviewing jakubkrehel/skills (2026-11-08)

Procedure: `/adopt-external` ([external-repo-adoption.md](../ai-includes/external-repo-adoption.md)).
**Outcome: nothing taken.** No skill, tool or text came over. TAOM has no web UI, so most of the repository is out of
domain, and the few ideas that transfer are already in TAOM or failed a test on TAOM's own history.

## Source, and what was read

Repository: `jakubkrehel/skills` at `157da62016338e8693dcff93d47154a9e36f2157` (MIT, 104 files, created 2026-07-10,
pushed 2026-11-07, plugin `interfaces` v1.19.0). It holds 15 agent skills for web product interfaces. All content
came through the GitHub API. Nothing was installed and no file from it was run.

| Depth | Files |
|---|---|
| Read in full | `plugin.json`, `marketplace.json`, `opencode.json`, `CLAUDE.md`, `README.md`, upstream `AGENTS.md`, `afk`, `break`, `state-machine`, `better-writing` and its `patterns.md`, `interface-review` and its `removed-signals.md`, `teardown/scripts/commands.cjs` and `config.cjs` |
| Read in part | `teardown/scripts/proof.cjs` (the execution lines), `check-viewer.cjs` (lines 40 to 60) |
| Scanned by pattern, description read | All 104 files |

**Not read:** the bodies of `better-accessibility`, `better-colors`, `better-layout`, `better-typography`, `better-ui`,
`better-interface`, `build-design`, `explain-interface`, `variant` and `teardown`, and the `teardown` assets apart from
one line of `viewer.js`. Their verdicts rest on their descriptions, their file lists and upstream's ownership table.

## Security

Safe to learn from: yes. Safe to install: no need arose, and TAOM ports text rather than installing.

- The repository has no hooks, no MCP servers, no lifecycle scripts and no `package.json`.
- An in-memory scan of all 104 files found no hidden Unicode and no credential reads. The `base64` hits are fonts and
  screenshots.
- `teardown` starts local tools only (`which`, Chrome, `rsvg-convert`, `resvg`, `qlmanage`) through `execFileSync` or
  `spawn` with argument arrays. `check-viewer.cjs` connects to Chrome's debugger on 127.0.0.1.
- `proof.cjs` runs model scripts in Node `vm`, which is not a sandbox. The project's own config names those scripts.
- `afk` runs for hours, commits locally and reads connected analytics and sibling projects. That conflicts with the
  AGENTS.md rule that a git action needs the user's request. The skill does set `disable-model-invocation: true`.

**Automated vet:** `python tools/audit_claude_config.py --root <extracted copy> --external` scanned 104 files, matching
the tree, and reported one MED and one INFO. The MED is `output-innerhtml` at `skills/teardown/assets/viewer.js:110`,
`group.innerHTML = result.markup`, in a drawing viewer TAOM does not take. The INFO says the secret sweep needs a git
work tree.

## Verdict by item

| Item | Verdict | Reason |
|---|---|---|
| `better-accessibility`, `better-colors`, `better-layout`, `better-typography`, `better-ui`, `better-interface` | Skip | Rules for HTML, ARIA and CSS, which Gauntlet prefabs are not. [The impeccable review](adopt-impeccable-2026-10-26.md) declined the same class of rules. |
| `build-design`, `explain-interface`, `variant`, `teardown` | Skip | Figma files, live URLs, web harness pages and SVG technical drawings. |
| `break`, `state-machine` | Skip | Each needs a scratch web route. The "States" section of [the Gauntlet screen checklist](../reference/gauntlet-screen-checklist.md) lists the same states. |
| `afk` | Skip | `/improve` covers it, and its direction lane already ranks by player or maintainer value. Unattended commits conflict with AGENTS.md. |
| `better-writing`, build strings whole | Already done | `lessons/localization-ui.md:707` holds the whole-sentence rule. A search of `Main` and `Client` for connector words such as `" to " +` found 5 hits, and none is translated player text: 4 are diagnostics and 1 is an English error line. |
| `better-writing`, verb and object on buttons | Already done | The checklist says "Text names the action". TAOM has 2 Yes/No confirm dialogs (`PresetsOverlayVM.cs:222`, `QuickActionsService.cs:118`). Vanilla ships `str_yes` and `str_no`, and the checklist asks for consistency with vanilla. |
| `better-writing`, toggles describe the ON state | Skip | 5 of 283 MCM toggles start with Hide or No, and those labels do describe the ON state. |
| `interface-review`, read the removed lines | Tried, not added | See below. |
| `interface-review`, Introduced, Regression and Pre-existing statuses | Already done | `/improve branch` tags findings as introduced or pre-existing. |
| Upstream authoring rules (one owner per rule, no counts that go stale) | Already done | `external-skill-ports.md` and CLAUDE.md have them. |
| `disable-model-invocation: true` on `/review-codex` | Not taken | Mike's decision, on the recommendation below. |

## The removed-lines trial

`interface-review` reads the `-` side of each hunk, because a regression does not show in the post-change state. TAOM
has one incident of that kind: #591, where a prefab clone moved a sprite off the widget the engine writes to, and
relation colour stayed lost for five months. So the review searched the removed side of the last 40 commits that touch
`Main/_Module/GUI` for `Sprite=`, `Brush=`, `Command.`, `IsVisible=`, `IsEnabled=`, `DoNotAcceptEvents`,
`DoNotPassEventsToChildren`, `HintWidget` and `{=`.

Fourteen of the 40 commits had hits, up to 105 lines in one commit. The largest are deliberate cleanups: `fc932d4ca`
(105), `450332b74` (46) and `62fb6bb1d` (21). A step like this would ask for a reasoned reply on about a third of prefab
commits. `gui-ui.md` already holds the rule for re-parented visuals, and the XML lens already reviews every changed
line. So it was not added.

**Not traced:** a search of commit messages did not find the commit that introduced #591, so whether the step would have
flagged it is unknown.

## The invocation flag

Upstream sets `disable-model-invocation: true` on every skill only a person should start. No TAOM skill sets it, and no
hook in `.claude/hooks` matches `codex exec`, so CLAUDE.md's "never auto-invoke" row for the paid `/review-codex` is
text. The flag would make the harness refuse a skill the model started, at the cost that `/ship` steps 4 and 6 stop until
Mike types the command each round. `codex exec` through Bash would stay open, and `/deep-review` dispatches that way, so
the gate would be partial.

A search of `lessons/`, the RCAs and the harness retros found no skill started without Mike's word, and four records
show the rule holding for `/issue`. A guard against a case with no incident is Mike's call under
[simplicity-criterion.md](../../.claude/rules/simplicity-criterion.md). The recommendation was not to add it, and it
was not added.

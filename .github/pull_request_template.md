<!-- Target branch: bannerlord-1.5.x (Bannerlord 1.5.6), or bannerlord-1.4.5 for 1.4.8 bug fixes. See CONTRIBUTING.md. -->

## What this fixes

Issue: #

<!-- One or two sentences: the bug, and what the change does about it. -->

## Reproduction

- Bannerlord version:
- TAOM version:
- Steps:
- Before:
- After:

## Checks I ran

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=`
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`
- [ ] A test in `TAOM.Tests` fails without the fix and passes with it
- [ ] Tested in game

<!-- Leave a box unticked if you did not run it, and say why. That is fine; we build and test on our side. -->

## Engine evidence

<!-- If the fix relies on a TaleWorlds API, name the type, method and game version you checked. -->

## Confirm

- [ ] No compiled binaries, archives or packaged assets
- [ ] No changes to build files, `.claude/`, `.ai/`, `.github/`, `AGENTS.md` or `CLAUDE.md` (or an issue agreed on them)
- [ ] I have the right to submit this under the repo's licences (CONTRIBUTING.md, "Licensing")

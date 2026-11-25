TAOM is a Lord of the Rings total conversion for Mount & Blade II: Bannerlord (.NET Framework 4.7.2, game v1.5.7). Review the uncommitted change in this working tree (branch `fix/dwarf-attack-kick-type`, base `bannerlord-1.5.x` at `git rev-parse HEAD`): issue #1499, a native hang in battles with dwarves. Read-only review: change no file, run no game, and do not touch the live game install.

## What the change does

The two dwarf special attacks (`act_dwarf_head_butt`, `act_dwarf_drop_kick`, feature `Main/Features/DwarfAttacks/`) are started by TAOM's behaviour tree with `SetActionChannel`. Their rows in the Armory's `action_types.xml` carried `type="actt_kick"`. Their clips carry no combat parameter on purpose (`tools/gen_dwarf_attack_clips.ps1`), because TAOM deals the blow itself. Issue #1499 reports a hang whose dump shows the engine's kick code reading the combat parameter record at index -1 for a dwarf in one of these attacks.

The change takes the type off both rows:

- `tools/bind_dwarf_attack_actions.py`: writes the rows with no type, retypes a file that still carries the `actt_kick` rows the tool wrote before (`_retyped`), refuses any other type, and `validate_types` wants no type.
- `tools/tests/test_bind_dwarf_attack_actions.py`: the new `RetypeTests` and the changed literals.
- `docs/reference/lotrlome-armory-snapshot/action_types.xml`: the repo's snapshot of the Armory file, rewritten by the tool's planner.
- `TAOM.Tests/Features/DwarfAttacks/DwarfAttacksWiringTests.cs`: the pin now wants no `type` attribute, in the snapshot and in the live install.
- `tools/tests/test_armory_kick_typed_actions.py` (new): the set of Armory actions typed `actt_kick`, `actt_kick_continue`, `actt_kick_hit` or `actt_weapon_bash` must equal a fixed list of five mount attacks.
- Docs: `docs/features/dwarf-attacks.md`, `docs/features/stamina.md`, `docs/reference/lotrlome-dwarf-attack-changes.md`, `tools/README.md`, the header of `tools/gen_dwarf_attack_clips.ps1`.

No production C# changes. The live Armory file (`<game>/Modules/LOTRLOME_Armory/ModuleData/action_types.xml`, unversioned) still has the typed rows; the tool's `--apply` will retype it. So three live-install tests fail in this tree until then; that is expected and is not a finding.

## Evidence in the tree

- `docs/reviews/raw/native-rva670270-v1.5.7.txt`: Ghidra pseudo-C of the function at RVA 0x670270 of the installed `TaleWorlds.Native.dll`, with one level of callers. Lines 462 to 523 are the branch in question: `*(int *)(param_1 + 0x2ac) == 2`, the test of the two channels' action types against 28 to 31 (`- 0x1cU < 4`), the read of a short at clip + 0x7e, the product with 0x68 added to `DAT_180daf618`, the byte at record + 100 used as an index times 0x100, and the lock loop. Lines 1579 to 1625 move the field at +0x2ac from 1 to 2 when a channel holds a type of that range, and back to 0.
- The decompiled managed engine source for v1.5.7 is under `C:\Users\mikew\.taom-src\v1.5.7-202611101605\` (`Agent.ActionCodeType`: Kick = 28, KickContinue = 29, KickHit = 30, WeaponBash = 31).

Open questions that I did not settle, stated as questions:

1. What writes 1 to the field at +0x2ac? Is it the engine's own kick decision (AI or the Kick event flag), so that the faulty read needs an engine kick to start while a TAOM kick-typed action is in a channel? Or does a kick-typed action alone lead to it?
2. Can a mount's channel reach the same branch? Five mount attacks keep `actt_kick` (`act_spider_kick`, `act_elephant_kick`, `act_war_ram_butt`, `act_animalia_elk_antler`, `act_animalia_moose_antler`), and at least the ram, elk and moose clips have no combat parameter either. The change leaves them and lists them in the new gate.

## What to look for

1. **Does taking the type away remove the faulty read for a dwarf?** Read the pseudo-C. Find any other native or managed path that would still treat these two actions as kicks, or any path where an action with no type (type 0) is worse: read every managed reader of `ActionCodeType` and of `GetCurrentActionType` in the engine source and in `Main/` and `Client/` (for example `Agent.IsInBeingStruckAction`, `StandingPoint.DoesActionTypeStopUsingGameObject`, the AI's attack and defend decisions, `Main/Features/Stamina/`, `Main/Features/DwarfAttacks/`, `Main/Features/CombatMechanics/`).
2. **What TAOM loses with the type.** The docs now say: a dwarf on a machine starts no attack (`DwarfAttackService.MayAttack`), and a dwarf attack costs no stamina. Find any other behaviour that depended on the engine reading these actions as kicks: the AI holding its own swing during the attack, an interruption rule, a co-op sync rule, the Exhausted lock, `DwarfAttacksCombat.IsOurs`.
3. **The tool.** `_retyped` runs before every plan. Find an input for which the tool writes something other than the two rows with no type, loses a byte it should keep (BOM, CRLF, the war ram's row, a comment), retypes a row it does not own, reports `ok` for a file that still has a typed dwarf row, or passes `--check` on one. Check the note replacement (`OLD_NOTE` to `NOTE`) on a file whose note was edited by hand.
4. **The new gate.** `tools/tests/test_armory_kick_typed_actions.py`: can a kick-typed row escape `kick_typed` (attribute order, quotes, a multi-line row, a commented block, upper case)? Is a fixed list the right gate, and is the claim in its docstring supported by the pseudo-C?
5. **Other gates that run the tool.** `tools/data_gates.py` and `tools/audit_armory_refs.py` call `bind_dwarf_attack_actions.py --check`. Do they still mean what their text says?
6. **The docs.** Find a sentence the change made false or left false (the kick type, the stamina cost, the machine rule), in the changed docs and in docs the change did not touch.
7. **Tests that pin nothing.** Name a changed or new test that would still pass with the old behaviour.

## Output

Findings most severe first, each with a severity (P1 breaks play or a campaign; P2 a defect a game run or a tool run will meet; P3 minor), the file and line, the concrete input or state that shows it, and the engine or TAOM line you read. Say UNVERIFIED for any claim you did not read the source for. Answer the two open questions if the pseudo-C or the source settles them; say so if they do not.

You are an adversarial code reviewer for TAOM, a Lord of the Rings total-conversion mod for Mount & Blade II: Bannerlord v1.4.8. Repo root is the current directory.

Your job is to FIND BUGS, not to bless the code. Assume it is wrong and prove it. A review that finds nothing is a failed review unless you can show you tried hard.

## What was built

Feature "UncapturableHeroes" (GitHub issue #513): Sauron and the nine Nazgûl can never be taken prisoner. When a battle they lose would capture them, they become fugitives instead. It does NOT block death, deliberately.

Read these files:

- `Main/Features/UncapturableHeroes/`: all of it (Domain/, Hooks/, registry, config provider, settings provider, service, IoC)
- `Main/Adapters/IHeroCaptivityAdapter.cs`, `Main/Adapters/HeroCaptivityAdapter.cs`
- `Main/_Module/ModuleData/uncapturable_heroes/uncapturable_heroes_config.json`
- `TAOM.Tests/Features/UncapturableHeroes/`: all six test files
- `docs/features/uncapturable-heroes.md`
- The relevant hunks of `Main/IoC.cs`, `Main/SubModule.cs`, `Main/Features/TaomSettings.cs`

## The design claim you must attack

The whole feature rests on ONE premise: `MapEvent.CaptureDefeatedPartyMembers` gates capture on `Hero.CanBecomePrisoner()` at `MapEvent.cs:1983`, and when that gate fails the hero is still in the defeated member roster, so vanilla's own fall-through at `MapEvent.cs:2004-2008` applies `MakeHeroFugitiveAction`. So denying capture IS granting escape, and the mod writes no escape code.

Verify this yourself against the decompiled v1.4.8 engine source. Decompiled sources are at `E:\Decompiled_Bannerlord\_categories_v1.4.8\` and `C:\Users\mikew\.taom-src\v1.4.8\`. If that premise is wrong or incomplete in any case, that is a CRITICAL finding.

## Two seams

1. Postfix on `Hero.CanBecomePrisoner()` (public, instance, no args, returns bool).
2. Prefix returning bool on `TakePrisonerAction.Apply(PartyBase capturerParty, Hero prisonerCharacter)` (public static).

Both in Harmony category `Patch76_UncapturableHeroes`.

## Known suspects, go here first

1. **State corruption.** Trace every path where the prefix vetoes a capture. Is there any caller of `TakePrisonerAction.Apply` that is left holding inconsistent state when the capture does not happen? Enumerate ALL 15 engine call sites and check what each does immediately after the call. Look especially for a caller that reads `hero.PartyBelongedToAsPrisoner`, advances a quest stage, mutates a roster, or shows UI on the assumption the capture succeeded.

2. **The fugitive action does not clear a prison roster.** `MakeHeroFugitiveAction` never removes a hero from a captor's `PrisonRoster`. The prefix guards with `if (prisonerCharacter.IsPrisoner) return true;`. Is that guard sufficient for every re-capture path, including a hero being moved between captors? Check `MapEvent.LootDefeatedPartyPrisoners` and `EnterSettlementAction.ApplyForPrisoner`.

3. **Save compatibility.** Does anything here touch saved state? The service claims to hold no per-campaign state and there is no `SyncData`. Verify. Also: what happens to a save made before this feature shipped, where a Nazgûl is already a prisoner?

4. **The identity rule.** `UncapturableRegistry` resolves in a fixed order: excludeHeroIds, heroIds, heroSets (`nazgul_nine` via `INazgulRegistry`), then a race rule. Check the shipped config against the shipped character data. Verify the claim that six of the Nine carry no race attribute in `Main/_Module/ModuleData/lords.xslt` and three (`lord_1_48_1/_2/_3`) are `race="uruk"` in `Main/_Module/ModuleData/characters/lords.xml`. Note `lords.xslt` emits attributes as `<xsl:attribute name="race">`, not literal `race="..."`. Is any wraith reachable by neither axis?

5. **Concurrency.** `UncapturableRegistry` uses a `volatile` field plus a lock to build its table once. Is the double-checked pattern correct on net472? Can two threads see a partially constructed `Tables`?

6. **Fail-open direction.** The postfix must never turn a vanilla `true` into `false` by accident, and the prefix must let vanilla run whenever it did not actually free the hero. Verify both, including the exception paths. Note TAOM's `PatchShield` (`Dependencies/Foundation/PatchShield.cs`) attaches a finalizer to patched engine methods that swallows `Missing*`/`TypeLoad` and lets the target return `default(T)` : for a bool gate that is `false`, i.e. every hero uncapturable. Is that adequately defended?

7. **The MCM toggle.** `EnableUncapturableHeroes` must gate every behavioural path. Find any path that acts without consulting it.

8. **Localization.** Two keys, `taom_uncapturable_escapes_battle` and `taom_uncapturable_escapes_capture`. Are they registered, correctly variable-substituted (`{HERO}`), and emitted on the right seam? Are the player-relevance predicates correct, or will the message fire for battles the player cannot see?

9. **Test quality.** Read every test. Find assertions that cannot fail, tests that would pass if the production code were deleted, and behaviours claimed in `docs/features/uncapturable-heroes.md` that no test covers.

## Findings already known: do not re-report these unless you can show the fix is wrong

A prior review pass found and FIXED these four. Verify the fixes are actually correct and complete; report only if a fix is wrong or incomplete:

- The JIT-time `Missing*`/`TypeLoad` hole behind PatchShield, mitigated by adding a `KillCharacterActionDetail.None` binding assertion.
- The binding test resolved `TakePrisonerAction.Apply` by parameter type not name; a `ParameterInfo.Name` assertion was added because Harmony binds by name.
- No Harmony priority; `[HarmonyPriority(Priority.Last)]` was added to the postfix.
- `LordConversationsCampaignBehavior.cs:3072-3076` / `:3145-3149` ("You are my prisoner now.") are reachable and not `IsPrisoner`-gated; deliberately left ungated because the escape message resolves it. Argue if you disagree.

## Rules of engagement

- Cite `file:line` for every claim. An uncited claim is worthless.
- Verify engine signatures against the decompiled v1.4.8 source before asserting anything about the engine.
- Rate each finding P1 (must fix before merge) / P2 (should fix) / P3 (nice to have).
- For each finding give: what is wrong, the exact failure scenario with concrete inputs, and the minimal fix.
- If you believe a design decision is wrong rather than a bug, say so separately under "Design disagreements".
- Do NOT modify any file. This is review only.

Output a markdown report: Summary line, then P1 / P2 / P3 sections, then Design disagreements, then "What I verified and found correct" (brief).

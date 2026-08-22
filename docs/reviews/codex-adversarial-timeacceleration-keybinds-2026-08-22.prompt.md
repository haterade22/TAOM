ADVERSARIAL CODE REVIEW -- TAOM mod for Mount and Blade II Bannerlord v1.4.8

FEATURE UNDER REVIEW
Rebindable Time Acceleration keys. TAOM previously read three HARDCODED keys on the campaign map (Space, E, Ctrl+Space) to drive campaign time speed. E collides with vanilla MapRotateRight (GameKey id 59 in MapHotKeyCategory, default InputKey.E), so pressing E both accelerated time and rotated the camera, with no way for a player to change it. This change publishes the three actions as NATIVE rebindable GameKeys in a new GameKeyContext so they appear in Options > Keybindings > Campaign Map. Defaults are deliberately unchanged (Space, E, Ctrl+Space).

Your job is to find bugs. Be adversarial. Assume the author was wrong.

READ FIRST
- docs/features/time-acceleration.md (feature doc, NOT yet updated for this change)
- .claude/rules/csharp-architecture.md (ADR rules, NaN gates, entity state matrix)
- .claude/rules/harmony-patches.md section "Latches and Toggle Gates"
- .claude/rules/adapters.md (ADR-007 adapter pattern)

FILES CHANGED

New:
- Main/Features/TimeAcceleration/TaomTimeControlHotKeyCategory.cs
- TAOM.Tests/Features/TimeAcceleration/TaomTimeControlHotKeyCategoryTests.cs

Modified:
- Main/Features/TimeAcceleration/IMapInputAdapter.cs
- Main/Features/TimeAcceleration/MapInputAdapter.cs
- Main/Features/TimeAcceleration/TimeAccelerationService.cs
- Main/Features/TimeAcceleration/UI/TimeAccelerationMixin.cs
- Main/Features/TaomSettings.cs (Time Acceleration group hint text only)
- Main/SubModule.cs (registration block only)
- Main/_Module/ModuleData/taom_module_strings.xml
- Main/_Module/ModuleData/Languages/ 12 std_taom_module_strings files
- TAOM.Tests/Features/TimeAcceleration/TimeAccelerationServiceTests.cs

Unchanged but load-bearing context:
- Main/Features/TimeAcceleration/TimeAccelerationIoC.cs
- Main/Features/TimeAcceleration/TimeControlAdapter.cs
- Main/Features/TimeAcceleration/TimeAccelerationSettingsProvider.cs
- Main/Features/CoopInterop/CoopSettingsRelevance.cs
- TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs

Use "git diff HEAD" to see exactly what changed. Use "git show HEAD:PATH" to see the previous version of any modified file.

KNOWN SUSPECTS -- CONFIRM or DISPUTE each with evidence

SUSPECT 1 (HIGHEST RISK) -- Localization id shape.
The engine renders a game key label via KeyOptionVM, which builds its id from a cast of gameKey.Id to GameKeyDefinition followed by ToString(), NOT from gameKey.StringId. GameKeyDefinition is a plain enum ending at TotalGameKeyCount (116). The author used ids 500/501/502 so the cast yields the bare number, and added XML strings with ids of the form str_key_name.TaomTimeControlHotKeyCategory_500 and str_key_description.TaomTimeControlHotKeyCategory_500. Decompile GameKeyGroupVM and KeyOptionVM and GameKeyOptionCategoryVM in TaleWorlds.MountAndBlade.ViewModelCollection and confirm the EXACT id string the engine looks up, character for character, including which segment is GroupId versus MainCategoryId. If the author used the wrong segment the Options screen shows a raw untranslated id. Verify against how vanilla ships these in Modules/Native/ModuleData/global_strings.xml.

SUSPECT 2 -- Does taom_module_strings.xml actually reach the engine GameTextManager?
TAOM has a Harmony patch Patch25_LocalizationOverride that parses taom_module_strings.xml for ENGLISH overrides of existing texts. That is a different mechanism from the engine loading a module strings file as game texts. Determine whether str_key_name entries placed in Main/_Module/ModuleData/taom_module_strings.xml are actually loaded into the engine text manager at all, or whether they need to live in a differently named or differently registered file. Check Main/_Module/SubModule.xml. Compare with how the shipped NavalDLC module registers its own str_key_name entries. If these strings never load, the whole Options-screen labelling is dead and this is a HIGH finding.

SUSPECT 3 -- protected internal cross-assembly call.
GameKeyContext.RegisterGameKey is declared protected internal in TaleWorlds.InputSystem.dll. TaomTimeControlHotKeyCategory derives from it in TAOM.dll, a different assembly. Confirm the call actually compiles and is legal, and that nothing about the accessibility changes at runtime.

SUSPECT 4 -- Shared default key between two actions.
FastForward (id 500) and Turbo (id 502) BOTH default to InputKey.Space. So one physical Space press raises IsFastForwardPressed AND IsTurboPressed in the same frame, and only IsControlDown separates them. Walk the if/else-if chain in TimeAccelerationService.OnTick and prove correctness for every case: Space alone; Ctrl+Space; Ctrl released while Space still down; Space released while Ctrl still down; turbo rebound to a different key; both rebound apart; turbo unbound entirely. Look for any sequence that strands the engine at the boosted turbo multiplier.

SUSPECT 5 -- Unbound key soft-lock.
GameKey.KeyboardKey is null when a binding is Invalid, and a player can clear a binding in the Options screen at runtime. MapInputAdapter.BoundKey returns InputKey.Invalid in that case and the adapter reports not-pressed. Now consider: turbo is ACTIVE (_turboActive true, engine at the boosted multiplier), and the player clears the turbo binding. Trace whether _turboActive can ever be cleared afterwards, or whether the campaign is stuck at the turbo multiplier. Check RestoreTurboIfActive and every early-out path.

SUSPECT 6 -- Registration timing.
The Native module ViewSubModule.OnSubModuleLoad calls HotKeyManager.RegisterInitialContexts, which CLEARS the category dictionary before repopulating it. TAOM registers its context from the TAOM SubModule.OnSubModuleLoad. Confirm the TAOM call is guaranteed to run AFTER the Native one, for every module load order the launcher can produce. If TAOM ever registered first, its category would be silently erased and every key would be dead with no error. Also confirm that a context registered at this point still gets the player saved bindings applied from BannerlordGameKeys.xml -- check HotKeyManager.RegisterContext, the _needsLoading flag, HandleSaveLoad and LoadAsync.

SUSPECT 7 -- Sparse id null padding.
The GameKeyContext constructor pre-fills its list with gameKeysCount nulls, and RegisterGameKey writes by INDEX. With ids 500-502 and a count of 503, exactly 500 slots stay null. Enumerate EVERY engine code path that iterates GameKeyContext.RegisteredGameKeys and confirm each one null-guards. Check at minimum GameKeyOptionCategoryVM, InputContext.RegisterHotKeyCategory, the HotKeyManager save and load paths, and anything in the Options screen apply/reset flow. One unguarded iteration is a null-reference crash in the Options menu.

SUSPECT 8 -- Per-frame cost.
MapInputAdapter.EnsureResolved sets its _resolved latch ONLY when the category is found. If registration failed, the foreach over HotKeyManager.GetAllCategories runs on every property read, every frame, forever. TimeAccelerationService reads up to four adapter properties per tick. Assess whether that fallback is acceptable and whether the foreach over a Dictionary ValueCollection allocates. The file docs/features/time-acceleration.md claims OnTick is allocation-free.

REQUIRED SECTIONS IN YOUR OUTPUT

VANILLA CODE
Decompile and paste as code blocks the relevant bodies of: GameKeyContext (constructor, RegisterGameKey, GetGameKey, RegisteredGameKeys), HotKeyManager (RegisterContext, RegisterInitialContexts, GetCategory, GetAllCategories, Tick, HandleSaveLoad, LoadAsync, SaveAsync), GameKey (both constructors, KeyboardKey), the GameKeyOptionCategoryVM constructor, GameKeyGroupVM.RefreshValues, the KeyOptionVM constructor, MapHotKeyCategory.RegisterGameKeys, OptionsProvider.GetGameKeyCategoriesList and GetHiddenGameKeys. Decompiled source is at E:/Decompiled_Bannerlord/_shipping_build_v1.4.8/. Installed DLLs under the Bannerlord bin/Win64_Shipping_Client folder are authoritative for signatures.

STATE MACHINE ANALYSIS
Build the full truth table for TimeAccelerationService.OnTick across: IsControlDown, IsFastForwardPressed, IsTurboPressed, IsTurboReleased, IsExtraFastForwardPressed, _turboActive, plus the three early-out guards (co-op ShouldDeferToHost, campaign inactive or map inactive, menu open and not locked). Identify every state from which the engine can be left at a boosted SpeedUpMultiplier with _turboActive false, or _turboActive true with no path to clear it.

CONFIG AND STRING CROSS-REFERENCE
Cross-reference every string id added to taom_module_strings.xml against what the engine looks up. Cross-reference the 12 language files for the 6 new keys. Confirm no id typos and no drift between the C# id constants and the XML numbers.

REGRESSION CHECK
Diff the old and new IMapInputAdapter and TimeAccelerationService using "git show HEAD:PATH". Confirm the ONLY behavioural change is rebindability. Flag any dropped behaviour, especially around the co-op gate and the turbo save and restore.

FINDINGS OR OBSERVATIONS
For every finding: severity (HIGH/MED/LOW), file and line, what is wrong, concrete repro or reasoning, and the minimal fix. Separate CONFIRMED bugs from OBSERVATIONS. If a Known Suspect is a false alarm, say DISPUTED and show why.

QUALITY GATES
- Do not flag code that merely matches vanilla behaviour as a bug.
- Do not claim something is missing without grepping for it first.
- Cite file and line for every claim.
- If you cannot verify something, say UNVERIFIED rather than guessing.

LESSONS FROM PRIOR REVIEWS
SUCCESSES: decompiling the vanilla target caught missing gates; lifecycle tracing caught stale caches and latches; cross-referencing config ids caught real mismatches.
FAILURES: Codex has previously flagged vanilla-matching code as bugs, assumed wrong TAOM culture ids, and skipped the hard decompilation sections. Do not skip the VANILLA CODE section.

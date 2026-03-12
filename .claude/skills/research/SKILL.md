---
name: research
description: Decompile and analyze TaleWorlds classes before implementing or fixing. Use when touching Harmony patches, adapters, GameModels, or investigating bugs.
argument-hint: [ClassName or DLL.ClassName]
---

# TaleWorlds Research Skill

Follow the TaleWorlds Research Protocol from @docs/ai-includes/taleworlds-research-guide.md

## Target

Decompile and analyze: `$ARGUMENTS`

## Workflow

1. **Identify the DLL** — Determine which assembly contains the target class:
   - `TaleWorlds.CampaignSystem.dll` — Campaign logic, kingdom decisions, diplomacy
   - `TaleWorlds.CampaignSystem.ViewModelCollection.dll` — UI ViewModels
   - `TaleWorlds.Core.dll` — Core game types
   - `TaleWorlds.MountAndBlade.dll` — Battle/mission logic
   - `TaleWorlds.Library.dll` — Base types, PropertyOwner
   - `SandBox.dll` / `SandBox.ViewModelCollection.dll` — Sandbox-specific logic

2. **Decompile the class**:
   ```powershell
   ilspycmd "%BANNERLORD_GAME_DIR%\bin\Win64_Shipping_Client\<DLL>" -t "TaleWorlds.<Namespace>.<ClassName>"
   ```

3. **Analyze the decompiled output**:
   - Method signatures (parameters, return types, access modifiers)
   - Virtual vs sealed vs static methods
   - Property getters/setters and computed properties
   - Null handling patterns (TextObject.Empty vs null)
   - Collection types and modification safety
   - Event timing and state change ordering

4. **Document findings** — Summarize:
   - Key methods and their signatures
   - Properties (read-only vs read-write)
   - Nested sealed types that need adapter wrapping
   - Important null/edge case behavior
   - Any v1.3.12-specific changes observed

5. **Provide recommendations** for how to safely integrate with the analyzed class.

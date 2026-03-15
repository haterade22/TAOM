---
name: Use SandBoxCore for vanilla XML reference
description: Always reference SandBoxCore/ModuleData (not SandBox/ModuleData) as the authoritative source for vanilla XML structure
type: feedback
---

Always use `SandBoxCore/ModuleData/` as the authoritative source for vanilla XML structure, NOT `SandBox/ModuleData/`.

**Why:** SandBoxCore uses the element names the engine actually reads (e.g., `<notable_templates>`), while SandBox uses different names (e.g., `<notable_and_wanderer_templates>`) that the engine ignores. Using the wrong source leads to XSLT transforms that silently fail.

**How to apply:** When writing or debugging XSLT files, when looking up vanilla XML element/attribute names, or when cross-referencing culture definitions — always check SandBoxCore first.

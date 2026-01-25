# Migration Guide: Bannerlord 1.2.12 to 1.3.12

This directory contains documentation for migrating TAOM from Bannerlord 1.2.12 to 1.3.12.

## Overview

The migration from 1.2 to 1.3 involves:

1. **XML Schema Changes** - Module XML, cultures, troops, items
2. **API Breaking Changes** - Method signatures, removed/renamed methods
3. **Dependency Updates** - NuGet packages, Harmony version
4. **Runtime Behavior Changes** - Game engine behavior differences

## XSLT Transformation Approach

TAOM uses XSLT transformations to modify vanilla Bannerlord XML at load time. This approach:

- **Preserves vanilla structure** - No need to maintain full copies of game XML
- **Enables themed renaming** - Kingdoms, cultures, clans, lords renamed
- **Adds biographical content** - Hero lore text added via transformation
- **Minimizes maintenance** - Only name/text changes, not structural

### Current XSLT Files

| File | Purpose | Count |
|------|---------|-------|
| `spkingdoms.xslt` | Kingdom name transformations | 8 |
| `spcultures.xslt` | Culture name transformations | 6 |
| `spclans.xslt` | Clan name transformations | 73 |
| `splords.xslt` | Lord name transformations | ~350 |
| `spheroes.xslt` | Hero biographical text | 415 |

## Quick Links

| Document | Purpose |
|----------|---------|
| [TRACKING.md](./TRACKING.md) | Migration status tracker |
| [v1.3-api-changes.md](./v1.3-api-changes.md) | Summary of key API changes |
| [xml-migration-strategy.md](./xml-migration-strategy.md) | Step-by-step XML migration |
| [XML-SCHEMA-CHANGES.md](./XML-SCHEMA-CHANGES.md) | 1.2 to 1.3 schema differences |
| [ROT-CORE-ANALYSIS.md](./ROT-CORE-ANALYSIS.md) | ROT-Core DLL compatibility analysis |

## Migration Status

See [TRACKING.md](./TRACKING.md) for current status of all migration tasks.

### Completed
- Module XML (SubModule.xml)
- XSLT Transformations (5/5 files)
- Kingdom names
- Culture names
- Clan names
- Lord names
- Hero biographies

### Remaining
- Settlement XML
- Troop XML (spnpccharacters)
- Item XML
- Equipment XML
- Code changes (Harmony patches, GameModels, CampaignBehaviors)

## Priority Order

1. **Module XML** - Required for mod to load
2. **Core XML Files** - Cultures, settlements (spcultures.xml, settlements.xml)
3. **Troop/Character XML** - spnpccharacters.xml, lords.xml
4. **Item/Equipment XML** - spitems.xml, equipment sets
5. **Code Changes** - API compatibility fixes
6. **Testing** - Full gameplay verification

## Key Changes Summary

### XML Changes
- `module_version` attribute changes
- New required elements in culture definitions
- Settlement XML restructuring
- Troop tree schema updates

### Code Changes
- `Mission` constructor changes
- `CampaignBehaviorBase` event signature changes
- Some `GameModels` method signature changes
- Harmony 2.3 compatibility

## Resources

- [TaleWorlds Modding Documentation](https://docs.bannerlord.com/)
- [Bannerlord Mod Development Discord](https://discord.gg/bannerlord)
- [BUTR Community Resources](https://github.com/BUTR)

## Process

1. **Read** the v1.3 API changes documentation
2. **Check** TRACKING.md for current status
3. **Follow** xml-migration-strategy.md for XML files
4. **Test** each change individually
5. **Update** TRACKING.md when complete

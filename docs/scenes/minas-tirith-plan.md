# Minas Tirith Scene - Project Plan

## Context
Building a full Minas Tirith scene for TAOM in the Bannerlord editor. MT has 7 levels (circles) with the Citadel at top. The scene needs to support **siege** (levels 1 and 7 only, with scene transition between them), **civilian** mode (all 7 levels walkable), and look visually accurate to Tolkien's vision. KEYForce has provided MT prefabs/blocks. Sceners will execute — this plan provides the A-to-Z roadmap.

### Key Decisions
- **Civilian mode**: All 7 levels walkable
- **Siege mode**: Level 1 + Level 7 only. After Level 1 falls, a **scene transition** (cutscene/loading) moves to Level 7

---

## Phase 1: Map Setup — Terrain

### 1.1 Generate Terrain
- [ ] Determine scene terrain size (likely 1024x1024 or larger given MT's scale)
- [ ] Plan terrain node count and resolution

### 1.2 Layout Planning
- [ ] Define zones on a 2D reference map:
  - **Pelennor Fields** — flat area in front of MT
  - **Level 1 (Great Gate)** — main wall, gatehouse, siege focus
  - **Levels 2–6** — ascending tiers (visual/blocking, not full siege)
  - **Level 7 (Citadel)** — Tower of Ecthelion, White Tree courtyard, siege endpoint
  - **Mindolluin Mountain** — backdrop behind the city
  - **Outer mesh / skybox boundary** — where the playable area ends
- [ ] Decide playable area boundaries vs. visual-only backdrop
- [ ] Reference images / concept art collected and shared with sceners

### 1.3 Height Map
- [ ] Generate height map in World Creator matching MT's tiered elevation
- [ ] Import height map into Bannerlord editor
- [ ] Verify scale and elevation feel correct in-engine

### 1.4 Terrain Sculpting
- [ ] Sculpt the 7 tiers / levels to match planned layout
- [ ] Sculpt Mindolluin mountain backdrop
- [ ] Sculpt Pelennor Fields (relatively flat with gentle rolls)
- [ ] Sculpt any river/moat features if needed
- [ ] Verify walkable slopes for AI navigation

### 1.5 Terrain Texture Planning
- [ ] Identify required texture layers:
  - Stone/rock (mountain, walls base)
  - Grass (Pelennor)
  - Dirt/road (paths between levels)
  - Dry earth / dust (near walls)
- [ ] Decide: import new textures vs. use existing Bannerlord textures

---

## Phase 2: Terrain Texturing & Environment

### 2.1 Texture Layers
- [ ] Import any new textures needed
- [ ] Add terrain layers in editor
- [ ] Paint terrain layers using slope/altitude/manual tools:
  - Grass on Pelennor Fields
  - Rock on mountain and steep areas
  - Road/dirt on paths
  - Stone/paved near city areas

### 2.2 Flora
- [ ] Add flora (grass, bushes, trees) to Pelennor Fields
- [ ] White Tree of Gondor placement (Level 7 courtyard) — custom asset or adapted?
- [ ] Keep flora sparse near walls (historically accurate, siege lines of sight)

### 2.3 Environmental Prefabs & Entities
- [ ] Farms / homesteads on Pelennor (civilian flavor)
- [ ] Wheat fields, crops
- [ ] Roads / paths leading to the Great Gate
- [ ] Any water features (Anduin visible in distance?)
- [ ] Lighting setup — time of day, atmosphere

---

## Phase 3: Blocking Phase (Structure Placement)

### 3.1 KEYForce Prefab Import
- [ ] Import all KEYForce MT prefabs/blocks into the scene
- [ ] Catalog what prefabs are available (walls, towers, gates, buildings per level)

### 3.2 Level 1 (Priority — Siege Critical)
- [ ] Place Level 1 outer wall sections
- [ ] Place Great Gate
- [ ] Place towers flanking the gate
- [ ] Place interior buildings/structures for Level 1
- [ ] Ensure wall walkways are navigable
- [ ] Verify scale feels right for gameplay

### 3.3 Levels 2–6 (Visual / Planning)
- [ ] Place wall rings for each level
- [ ] Place major landmark structures per level
- [ ] Align structures with terrain tiers
- [ ] These don't need full detail yet — blocking for visual impression

### 3.4 Level 7 — Citadel (Siege Endpoint)
- [ ] Place Citadel structures
- [ ] Tower of Ecthelion
- [ ] White Tree courtyard
- [ ] Throne room / Great Hall entrance

### 3.5 Alignment & Review
- [ ] Walk through all levels, verify sightlines
- [ ] Ensure all structures align with terrain (no floating, no clipping)
- [ ] Screenshot / video review with team before moving to siege phase

---

## Phase 4: Siege Phase

> *To be fleshed out — high-level structure below*

### 4.1 Level 1 Siege Setup
- [ ] Define attacker spawn points (Pelennor Fields)
- [ ] Define defender spawn points (behind Level 1 wall)
- [ ] Place siege engine positions (rams, towers, ladders)
- [ ] Place battlements / arrow slits / murder holes
- [ ] Gate breach mechanics — destructible gate?
- [ ] AI navigation mesh for attackers and defenders
- [ ] Test siege flow: approach → breach → melee inside Level 1

### 4.2 Scene Transition (Level 1 → Level 7)
- [ ] Define trigger for transition (Level 1 gate breached / flag captured)
- [ ] This will be a separate scene or scene-state — determine Bannerlord's mechanism
- [ ] Loading screen / cutscene assets if needed

### 4.3 Level 7 Siege Setup
- [ ] Attacker spawn points inside the city (post-transition)
- [ ] Defender positions at Citadel
- [ ] Final stand area design (courtyard of the White Tree?)
- [ ] Win/loss trigger zones

### 4.4 Siege Testing
- [ ] Siege AI pathfinding validation
- [ ] Ladder/tower placement validation
- [ ] Performance testing during siege (entity count, draw calls)
- [ ] Balance pass — are walls too easy/hard to breach?

---

## Phase 5: Civilian Phase

> *To be fleshed out — high-level structure below*

### 5.1 Planning
- [ ] Use editor debugger to identify all required civilian entities
- [ ] Plan NPC spawn points across all 7 levels
- [ ] Plan which level hosts each facility:
  - Level 1: Markets, tavern, main gate area
  - Levels 2–4: Residential, shops, smithy
  - Levels 5–6: Noble quarters, armorer
  - Level 7: Lord's Hall / Throne Room, White Tree courtyard
- [ ] Plan navigation between levels (ramps/gates between tiers)

### 5.2 Required Civilian Entities
- [ ] Tavern
- [ ] Smithy / armorer
- [ ] Marketplace / merchant stalls
- [ ] Arena (if applicable for Gondor)
- [ ] Lord's Hall / Throne Room (Level 7)
- [ ] NPC waypoints / patrol paths (all 7 levels)
- [ ] Ambient NPCs (civilians walking, guards patrolling)
- [ ] Gate/passage points between each level

### 5.3 Implementation
- [ ] Place all required entities with correct tags
- [ ] Set up NPC spawn points across all levels
- [ ] Ensure AI can navigate between all 7 levels
- [ ] Verify in editor debugger — all requirements green
- [ ] Test civilian mode walkthrough (full traversal Level 1 → 7)

---

## Open Questions
1. **Scene size** — What terrain size works best for MT's scale?
2. **KEYForce prefabs** — Full inventory of what's available?
3. **Performance budget** — Max entity count target?
4. **Anduin / surrounding geography** — Visible? Osgiliath ruins in distance?
5. **Custom assets needed** — White Tree, Gondorian banners, unique architecture pieces?

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/modding/module-map.md](../modding/module-map.md)

<!-- backlinks-end -->

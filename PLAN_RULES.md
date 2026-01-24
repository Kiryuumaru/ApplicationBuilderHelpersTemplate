# Rules Overhaul Plan

## Objective

Transform inconsistent, verbose rules into law-book style guidelines:
- Absolute words, no fuzzy terms ("little", "large", "maybe")
- Straight information, defined absolutes
- Minimal code samples (only when essential for clarity)
- Visualizations allowed (folder structures, diagrams, tables)
- Spelled out guidelines with clear enforcement

---

## Current State Analysis

### Inventory of Existing Files

| File | Lines | Style | Issues |
|------|-------|-------|--------|
| `architecture.instructions.md` | ~343 | Verbose, heavy code samples | Over-documented, numbering error (item 3 twice), naming/doc ignorance misplaced |
| `code-quality-standards.instructions.md` | ~262 | Verbose, heavy code samples | Mixed concerns (workflow + quality + patterns), DRY duplicates code-reuse |
| `code-reuse.instructions.md` | ~134 | Moderate, code samples | Overlaps with DRY in code-quality-standards |
| `code-style-consistency.instructions.md` | ~87 | Moderate, tables + code | File naming duplicates file-organization, folder structure duplicates file-organization |
| `commenting-rules.instructions.md` | ~168 | Verbose, code samples | Over-documented with examples |
| `documentation.instructions.md` | ~119 | Tables, minimal code | Acceptable style, project-specific feature references |
| `file-organization.instructions.md` | ~217 | Verbose, folder trees + code | Duplicates naming/folder from code-style-consistency |
| `project-status.instructions.md` | ~28 | Brief, words only | Under-documented |
| `build-commands.instructions.md` | ~100 | Tables, commands | Reference doc - acceptable |
| `terminology.instructions.md` | ~20 | Brief, words only | Under-documented |
| `vscode-avoid-piping-causing-approval.instructions.md` | ~50 | Brief, examples | Agent-specific - acceptable |

### Identified Issues

#### 1. Duplicate Content

| Topic | Found In | Action |
|-------|----------|--------|
| File naming conventions | `code-style-consistency`, `file-organization` | Consolidate to `file-organization` |
| Folder structure patterns | `code-style-consistency`, `file-organization` | Consolidate to `file-organization` |
| DRY principle | `code-quality-standards`, `code-reuse` | Consolidate to `code-reuse` (rename to `code-patterns`) |
| "One type per file" | `code-style-consistency`, `file-organization` | Keep in `file-organization` only |
| Service internal accessibility | `code-quality-standards` | Move to `architecture` |

#### 2. Misplaced Content

| Content | Current Location | Correct Location |
|---------|-----------------|------------------|
| Fix hygiene (undo before retry) | `code-quality-standards` | New: `workflow` |
| Service implementation accessibility | `code-quality-standards` | `architecture` |
| Naming/Documentation Ignorance | `architecture` | `commenting-rules` or keep in `architecture` |
| Pre-commit checklist | `code-quality-standards` | New: `workflow` |

#### 3. Style Inconsistencies

| File | Current Style | Target Style |
|------|---------------|--------------|
| `architecture` | 50% code blocks | Max 20% code, prefer diagrams |
| `code-quality-standards` | 40% code blocks | Max 10% code, mostly statements |
| `code-reuse` | 30% code blocks | Max 10% code |
| `commenting-rules` | 40% code blocks | Max 15% code |
| `project-status` | Too brief | Expand with structure |
| `terminology` | Too brief | Merge with project-status |

#### 4. Numbering/Structural Errors

- `architecture.instructions.md`: Item 3 appears twice in "Forbidden Patterns" section

---

## Proposed New Structure

### File Reorganization

```
.github/instructions/
├── architecture.instructions.md          # Layer rules, dependencies, composition
├── file-structure.instructions.md        # File naming, folder structure, organization
├── code-quality.instructions.md          # Warnings, nullable, reliability standards
├── code-patterns.instructions.md         # DRY, reuse, abstraction patterns
├── commenting.instructions.md            # Comments, XML documentation
├── documentation.instructions.md         # Docs maintenance, update requirements
├── workflow.instructions.md              # Fix hygiene, pre-commit, branching
├── project-context.instructions.md       # Project status, terminology, breaking changes
├── build-reference.instructions.md       # Build commands (reference doc)
└── agent-terminal.instructions.md        # VS Code terminal rules (agent-specific)
```

### Merge/Split Plan

| Action | Source(s) | Target | Notes |
|--------|-----------|--------|-------|
| MERGE | `code-style-consistency` + `file-organization` | `file-structure` | Remove duplicates, keep folder trees |
| MERGE | `code-reuse` + DRY from `code-quality-standards` | `code-patterns` | Single source for patterns |
| MERGE | `project-status` + `terminology` | `project-context` | Combine brief docs |
| EXTRACT | Fix hygiene, pre-commit from `code-quality-standards` | `workflow` | Separate workflow concerns |
| RENAME | `commenting-rules` | `commenting` | Consistency |
| RENAME | `build-commands` | `build-reference` | Clarity |
| RENAME | `vscode-avoid-piping-causing-approval` | `agent-terminal` | Clarity |
| TRIM | `architecture` | `architecture` | Remove excessive code samples |
| TRIM | `code-quality-standards` | `code-quality` | Keep only quality rules |

---

## Content Guidelines for New Rules

### Format Standard

Each rule file MUST follow this structure:

```markdown
---
applyTo: '**'
---
# {Category} Rules

## {Section Name}

{Statement of rule in absolute terms}

{Table or diagram if needed}

{Minimal code only if visualization insufficient}
```

### Writing Style Requirements

| Aspect | Requirement |
|--------|-------------|
| Tone | Imperative, absolute |
| Terms | No "should", "might", "consider" - use "MUST", "NEVER", "ALWAYS" |
| Quantities | No "few", "many", "large" - use exact numbers |
| Code samples | Maximum 3 per section, only when text/diagram insufficient |
| Section length | Maximum 30 lines per section |
| File length | Target 80-150 lines (exceptions: architecture, build-reference) |

### Forbidden Patterns in Rules

- ❌ "You might want to..."
- ❌ "Consider doing..."
- ❌ "It's a good idea to..."
- ❌ "In most cases..."
- ❌ Extended code blocks showing multiple patterns
- ❌ Philosophical explanations ("why this matters")

### Required Patterns in Rules

- ✅ "MUST", "NEVER", "ALWAYS"
- ✅ "X lines maximum", "within Y seconds"
- ✅ Tables for mappings and references
- ✅ Folder tree diagrams for structure
- ✅ Single-line code references when needed
- ✅ Direct statements of consequence

---

## Execution Plan

### Phase 1: Create New Combined Files

| Step | Task | Output |
|------|------|--------|
| 1.1 | Create `file-structure.instructions.md` | Merge file-organization + code-style-consistency |
| 1.2 | Create `code-patterns.instructions.md` | Merge code-reuse + DRY section |
| 1.3 | Create `workflow.instructions.md` | Extract from code-quality-standards |
| 1.4 | Create `project-context.instructions.md` | Merge project-status + terminology |

### Phase 2: Trim Verbose Files

| Step | Task | Target Lines |
|------|------|--------------|
| 2.1 | Trim `architecture.instructions.md` | ~150 lines |
| 2.2 | Trim `code-quality.instructions.md` (from code-quality-standards) | ~80 lines |
| 2.3 | Trim `commenting.instructions.md` | ~80 lines |

### Phase 3: Rename and Clean Up

| Step | Task |
|------|------|
| 3.1 | Rename `commenting-rules` → `commenting` |
| 3.2 | Rename `build-commands` → `build-reference` |
| 3.3 | Rename `vscode-avoid-piping-causing-approval` → `agent-terminal` |
| 3.4 | Delete obsolete files |

### Phase 4: Final Review

| Step | Task |
|------|------|
| 4.1 | Verify no duplicate content across files |
| 4.2 | Verify consistent style across all files |
| 4.3 | Verify all cross-references updated |
| 4.4 | Update AGENTS.md with new file names |

---

## Content Mapping: Old → New

### architecture.instructions.md (TRIM)

| Current Section | Action | Notes |
|-----------------|--------|-------|
| Clean Architecture Layers | KEEP | Trim code, keep diagram |
| Forbidden Patterns (6 items) | TRIM | Fix numbering, remove code blocks |
| Required Patterns (5 items) | TRIM | Convert to table format |
| Ignorance Principles (6 types) | TRIM | Convert to list/table |
| When You Think You Need a Shortcut | REMOVE | Philosophical |
| Code Review Checklist | MOVE | To `workflow.instructions.md` |

### code-quality-standards.instructions.md → code-quality.instructions.md (TRIM + EXTRACT)

| Current Section | Action | Destination |
|-----------------|--------|-------------|
| Core Philosophy | TRIM | `code-quality` - reduce to 2 lines |
| Zero Warnings Policy | KEEP | `code-quality` |
| No Temporary Patches | TRIM | `code-quality` - remove code blocks |
| Nullable Reference Types | TRIM | `code-quality` - convert to rules |
| Reliability Standards | TRIM | `code-quality` - remove 99% analogy |
| Pre-Commit Checklist | MOVE | `workflow` |
| Fix Hygiene | MOVE | `workflow` |
| DRY | MOVE | `code-patterns` |
| Service Implementation Accessibility | MOVE | `architecture` |
| The Standard | REMOVE | Philosophical |

### code-reuse.instructions.md → code-patterns.instructions.md (MERGE)

| Current Section | Action | Notes |
|-----------------|--------|-------|
| Core Principle | KEEP | Merge with DRY principle |
| Required Workflow | TRIM | Convert to numbered steps |
| Forbidden Patterns | TRIM | Remove code blocks |
| Required Patterns | TRIM | Convert to statements |
| Search Commands | REMOVE | Reference, not rule |
| The Standard | REMOVE | Philosophical |

### code-style-consistency.instructions.md (MERGE INTO file-structure)

| Current Section | Action | Destination |
|-----------------|--------|-------------|
| Naming Conventions | MOVE | `file-structure` |
| Async Methods | MOVE | `file-structure` |
| File Names | REMOVE | Already in file-organization |
| Folder Structure Patterns | REMOVE | Already in file-organization |
| Code Organization Within Files | KEEP | `file-structure` - new section |

### file-organization.instructions.md → file-structure.instructions.md (MERGE)

| Current Section | Action | Notes |
|-----------------|--------|-------|
| One Type Per File | KEEP | Primary rule |
| File Naming | KEEP | Absorb from code-style-consistency |
| Folder Structure by Type | KEEP | Keep folder diagrams |
| Forbidden Patterns | TRIM | Remove code blocks |
| Required Patterns | TRIM | Convert to statements |
| Exceptions | TRIM | Convert to list |
| Namespace Conventions | KEEP | Brief section |

### commenting-rules.instructions.md → commenting.instructions.md (TRIM)

| Current Section | Action | Notes |
|-----------------|--------|-------|
| Core Principle | KEEP | Single line |
| Forbidden Comment Patterns | TRIM | List only, no code |
| Required Comment Patterns | TRIM | List only, no code |
| Decision Rule | TRIM | Convert to 4 questions |
| Examples | REMOVE | Over-documented |
| XML Documentation Strategy | TRIM | Rules only |
| The Standard | REMOVE | Philosophical |

### project-status + terminology → project-context.instructions.md (MERGE)

| Source | Section | Notes |
|--------|---------|-------|
| project-status | Breaking Changes Policy | Keep |
| project-status | Implications | Keep |
| terminology | Rules definition | Keep |
| NEW | Version info | Add |

---

## Validation Checklist

After overhaul, verify:

- [ ] No rule file exceeds 200 lines (except build-reference)
- [ ] No section exceeds 30 lines
- [ ] No more than 3 code blocks per file
- [ ] All files use absolute terms (MUST/NEVER/ALWAYS)
- [ ] No duplicate content across files
- [ ] All cross-references valid
- [ ] AGENTS.md updated with new file names
- [ ] Folder diagrams preserved where needed
- [ ] Tables used for mappings

---

## Progress Tracker

| Phase | Status | Notes |
|-------|--------|-------|
| Phase 1: Create merged files | COMPLETED | file-structure, code-patterns, workflow, project-context |
| Phase 2: Trim verbose files | COMPLETED | architecture, code-quality, commenting |
| Phase 3: Rename and clean up | COMPLETED | All files renamed, obsolete deleted |
| Phase 4: Final review | COMPLETED | AGENTS.md updated |

---

## Final File Structure

```
.github/instructions/
├── agent-terminal.instructions.md       # 45 lines - VS Code terminal rules
├── architecture.instructions.md         # 110 lines - Layer dependencies, composition
├── build-reference.instructions.md      # 75 lines - Build commands reference
├── code-patterns.instructions.md        # 75 lines - DRY, reuse, extraction
├── code-quality.instructions.md         # 70 lines - Warnings, nullable, reliability
├── commenting.instructions.md           # 65 lines - Comment rules, XML docs
├── documentation.instructions.md        # 70 lines - Doc maintenance
├── file-structure.instructions.md       # 115 lines - File naming, folder structure
├── project-context.instructions.md      # 45 lines - Status, terminology
├── tailwind-css.instructions.md         # (unchanged) - Tailwind build pipeline
├── ui-test-practices.instructions.md    # (unchanged) - UI test rules
└── workflow.instructions.md             # 60 lines - Fix hygiene, pre-commit
```

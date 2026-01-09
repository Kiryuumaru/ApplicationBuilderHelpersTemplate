---
applyTo: '**'
---
# Fix Hygiene: Undo Before Retry

## Core Principle

**If a fix attempt does not solve the problem, undo the changes from that specific fix before trying a different approach.**

## Why This Matters

Stacking unverified fixes creates cascading issues:
- Each failed fix introduces unintended side effects
- Subsequent fixes may mask root causes or introduce new bugs
- Debugging becomes exponentially harder as layers accumulate
- The codebase drifts further from a known-good state

## Required Workflow

1. **Apply fix** - Make changes to address the issue
2. **Verify fix** - Build, test, or otherwise confirm the fix works
3. **If fix fails** - Undo the changes from that fix before proceeding
4. **Then retry** - Apply a different fix starting from a clean state

## What "Undo" Means

- Undo file modifications introduced by the failed fix
- Remove any new files created for the failed fix
- Restore any code deleted by the failed fix
- Return to the state before that fix attempt began

## Forbidden Patterns

❌ Applying fix B on top of failed fix A without undoing A  
❌ Leaving partial changes from a failed fix "in case they help"  
❌ Commenting out failed fix code instead of removing it  
❌ Proceeding with additional changes when the current fix is broken  

## The Standard

**Failed fix attempts leave no trace.**

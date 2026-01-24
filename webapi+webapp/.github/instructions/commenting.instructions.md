---
applyTo: '**'
---
# Commenting Rules

## Core Principle

Comments exist for future readers with no conversation context. The codebase stands alone.

---

## Prohibited Comments

- NEVER write comments referencing prompts: "As requested", "Per instruction", "Added because asked"
- NEVER write comments referencing conversation: "As discussed", "Per our conversation", "Following the plan"
- NEVER write meta-comments: "NEW:", "CHANGED:", "This is the fix"
- NEVER write obvious descriptions: "Loop through list", "Return result", "Check if null"

---

## Required Comments

- MUST document non-obvious behavior: security implications, order dependencies
- MUST document external requirements: RFC references, spec compliance
- MUST document edge cases: intentional empty returns, fail-safe defaults
- MUST document reasoning: why this approach, not what it does

---

## Comment Decision

1. Is this obvious from the code? → No comment
2. Does this reference conversation? → No comment
3. Would future reader need this context? → Comment
4. Is there non-obvious reasoning? → Comment

---

## XML Documentation

XML docs (`///`) belong on interfaces only. Implementations do not require XML documentation.

- Public interface MUST have XML docs
- Interface methods MUST have XML docs
- Implementation class MUST NOT have XML docs
- Implementation methods MUST NOT have XML docs

---

## Prohibited

- NEVER write comments that would not make sense 2 years later without conversation context
- NEVER write XML documentation on internal implementation classes
- NEVER describe what code does when code is self-explanatory

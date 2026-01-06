---
applyTo: '**'
---
# Commenting Rules

## Core Principle: Comments Are for Future Readers

Comments must be written as if the reader has **no access to the LLM conversation history**. The codebase must stand on its own.

## Forbidden Comment Patterns

### ❌ NEVER DO:

1. **References to prompts or requests**
   ```csharp
   // Fixed because asked
   // As requested
   // Changed per instruction
   // Added per user request
   // Implementing the requested feature
   ```

2. **References to conversation context**
   ```csharp
   // As discussed above
   // Per our conversation
   // Based on the requirements mentioned
   // Following the plan
   ```

3. **Meta-comments about the change**
   ```csharp
   // NEW: Added this method
   // CHANGED: Updated logic here
   // This is the fix for the issue
   ```

4. **Obvious code descriptions**
   ```csharp
   // Loop through the list
   for (var item in items) { }
   
   // Return the result
   return result;
   
   // Check if null
   if (value == null) { }
   ```

## Required Comment Patterns

### ✅ ALWAYS DO:

1. **Explain non-obvious behavior**
   ```csharp
   // Token validation must happen before role expansion to prevent
   // privilege escalation via modified role claims
   ValidateToken(token);
   ExpandRoles(principal);
   ```

2. **Document why, not what**
   ```csharp
   // RFC 9068 requires the typ header for access token identification
   header.Typ = "at+jwt";
   ```

3. **Warn about edge cases or gotchas**
   ```csharp
   // SECURITY: Empty scope set intentionally returns no permissions
   // to fail-safe when RBAC version claim is missing
   if (!hasRbacVersion)
       return new HashSet<string>();
   ```

4. **Reference standards or external requirements**
   ```csharp
   // Per RFC 7643 Section 4.1.2, roles is a multi-valued attribute
   public const string Roles = "roles";
   ```

## Decision Rule

Before adding a comment, ask:

1. **Is this obvious from the code?** → Don't comment
2. **Does this reference the conversation?** → Don't comment
3. **Would a future reader need this context?** → Comment
4. **Is there a non-obvious reason for this approach?** → Comment

## Examples

### Bad → Good

```csharp
// BAD: References request
// Added TokenType enum as requested

// GOOD: Explains purpose
// TokenType enum enables RFC 9068 compliant typ header validation
```

```csharp
// BAD: Obvious
// Create a new list
var list = new List<string>();

// GOOD: No comment needed - code is self-explanatory
var list = new List<string>();
```

```csharp
// BAD: References conversation
// Changed from "role" to "roles" per discussion

// GOOD: References standard
// RFC 9068 Section 2.2.3.1 specifies "roles" (plural) for role claims
public const string Roles = "roles";
```

## The Standard

**If a comment wouldn't make sense to someone reading the code 2 years from now with no context about how it was written, don't write it.**

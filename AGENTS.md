# Early-Phase Clean Architecture Mandate (Critical)

Messaging is in its earliest development phase.

There are:

* No external consumers
* No production clients
* No backward compatibility guarantees
* No versioning constraints
* No migration burden

Backward compatibility is not a tradeoff dimension.

If a change improves correctness or clarity:

→ Replace the old implementation.
→ Update call sites.
→ Update tests.
→ Remove superseded code.

Never preserve incorrect structure for imaginary clients.

---

# 🏗 Pragmatic Layered Architecture (Authoritative)

Messaging uses a **strict inward dependency model**:

````

Messaging.Api → Messaging.Application → Messaging.Persistence → Messaging.Core
Messaging.Workers → Messaging.Persistence → Messaging.Core

```

Layer purposes:

### Core
Domain invariants and lifecycle rules only.

### Persistence
SQL + rehydration + DB facts only.

### Application
Use-case orchestration.
Maps DB facts → domain outcomes.

### API
Maps outcomes → HTTP.

### Workers
Execute lifecycle transitions + delivery.

No layer may leak responsibilities upward or downward.

---

# 🧠 Idempotency Boundary (Critical)

Persistence returns:

```

(Message Message, bool Inserted)

```

That is a **database fact** only.

Application maps:

```

Inserted → IdempotencyOutcome.Created
Not Inserted → IdempotencyOutcome.Replayed

```

API maps:

```

Created → 201
Replayed → 200

```

Persistence must never:

* Return HTTP status codes
* Return WasCreated DTOs
* Return API semantics
* Interpret idempotency meaning

Application owns idempotency meaning.

---

# 🚫 Absolutely Prohibited

AI agents must NOT:

* Add compatibility shims
* Add dual-path logic
* Preserve deprecated shapes
* Introduce fallback branching
* Add feature flags for hypothetical users
* Add transitional lifecycle states
* Add compatibility DTOs
* Preserve unused parameters
* Widen contracts to avoid breaking changes
* Add API semantics to Persistence
* Leak HTTP concerns into Application
* Leak lifecycle rules into Persistence

If structure is wrong:

Delete and replace.

---

# ✂ Refactor Default

When revising:

* Replace old structures outright.
* Update all call sites.
* Update tests.
* Remove superseded abstractions.

Never layer new logic on top of flawed structure.

---

# 🧱 Mandatory Schema Qualification Rule

All SQL must:

* Use `core.` schema explicitly.
* Fully qualify tables.
* Fully qualify foreign keys.
* Fully qualify indexes.
* Fully qualify constraints.
* Fully qualify sequences.
* Fully qualify joins.
* Fully qualify ON CONFLICT targets.

Never rely on `search_path`.

Unqualified SQL is invalid.

---

# 🧪 Test Alignment Rule

Tests must reflect current architecture.

Do NOT:

* Preserve legacy expectations.
* Add dual test paths.
* Maintain conditional assertions.

If behavior changes intentionally, tests must be rewritten.

---

# 🧠 Enforcement Philosophy

Messaging optimizes for:

* Structural clarity
* Deterministic behavior
* Canonical lifecycle correctness
* Clean boundaries
* Delete-ability
* Explicit ownership

Over:

* Stability for imaginary consumers
* Defensive compatibility
* Incremental patch layering

---

# 🎯 Governing Rule

If a change makes the system:

* Simpler
* More correct
* More aligned with canonical boundaries
* More explicit
* Easier to reason about

The old implementation must be removed.

Not preserved.
```

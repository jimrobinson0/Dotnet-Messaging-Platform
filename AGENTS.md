# 🧼 Early-Phase Simplicity Mandate (Critical)

Messaging is in its **earliest development phase**.

There are:

* No external consumers
* No production clients
* No backward compatibility guarantees
* No versioning constraints
* No migration burden beyond internal refactors

This has architectural consequences.

---

## 🚫 Absolutely Prohibited

When implementing new features, patches, or review-driven revisions, AI agents and contributors must NOT:

* Add backward compatibility shims
* Add dual-path logic (old + new behavior)
* Introduce nullable fallbacks solely to preserve hypothetical legacy flows
* Add compatibility DTOs
* Preserve deprecated property names
* Introduce feature flags for non-existent consumers
* Create temporary adapter layers
* Add defensive branching for prior contract shapes
* Preserve dead code “just in case”
* Introduce v1/v2 branching logic
* Add transitional states to lifecycle models
* Add schema compatibility layers
* Preserve unused parameters
* Add optional overloads solely to avoid breaking changes
* Implement compatibility migrations unless explicitly requested

If a change improves correctness or clarity, **replace the old design outright**.

Do not preserve incorrect structure for imaginary clients.

---

## 🔥 Required Bias: Replace, Don’t Accommodate

If a feature review identifies:

* A flawed abstraction
* A leaky boundary
* An unnecessary parameter
* An incorrect lifecycle rule
* A naming mistake
* A contract inconsistency

The correct action is:

> Delete or refactor the old implementation.

Not:

> Add compatibility handling.

---

## 🧠 Design Authority Rule

The canonical project context defines:

* Lifecycle invariants
* Dependency rules
* Persistence ownership
* Architectural scope
* Explicit non-goals

If proposed compatibility logic conflicts with the canonical design, the canonical design wins.

Never bend the architecture to preserve temporary code.

---

## ✂ Clean Refactor Preference

In this phase:

* Breaking internal APIs is acceptable.
* Renaming properties is acceptable.
* Removing parameters is acceptable.
* Rewriting persistence SQL is acceptable.
* Reworking DTOs is acceptable.

As long as:

* Architectural boundaries remain intact
* Lifecycle invariants remain correct
* Tests are updated accordingly

---

## 🧪 Test Alignment Rule

Tests must evolve with the design.

Do NOT:

* Preserve old test expectations for compatibility.
* Add conditional assertions for legacy behavior.
* Maintain dual test paths.

If behavior changes intentionally, tests must be rewritten to reflect the new truth.

---

## 🏗 Schema Evolution Rule

Migrations should:

* Represent the clean, intended schema.
* Not preserve obsolete columns unless explicitly required.
* Remove mistaken columns if necessary.

This is pre-production. Optimize for clarity over continuity.

---

## 🚨 Compatibility Is Not a Decision Variable

Messaging is pre-consumer and pre-production.

AI agents and contributors must **never evaluate backward compatibility as a tradeoff dimension**.

There are:

* No external contracts to preserve
* No public API guarantees
* No client upgrade paths
* No semantic versioning obligations
* No legacy integrations

Therefore:

* Compatibility is not a factor.
* Migration cost is not a factor.
* External breakage is not a factor.

These variables do not exist in this phase.

---

## 🧠 Refactor Default

When revising behavior, contracts, schema, or lifecycle logic:

* Replace the old structure.
* Update all call sites.
* Update tests.
* Update migrations if necessary.

Do not:

* Introduce conditional branching for old behavior.
* Maintain dual representations.
* Add temporary adapters.
* Preserve deprecated shapes.

The only acceptable state is the clean, current design.

---

## 🎯 Governing Rule

If a change makes the system:

* Simpler
* More correct
* More aligned with canonical context
* More explicit
* Easier to reason about

Then the old implementation must be removed.

Not preserved.

---

## 🎯 Overarching Principle

This project values:

* Structural clarity
* Determinism
* Explicitness
* Domain integrity
* Delete-ability

Over:

* Stability for hypothetical users
* Defensive compatibility
* Incremental patch layering

---

## 🧨 Enforcement Clause for AI Agents

When generating or revising code:

* Do not preserve superseded structures.
* Do not introduce compatibility indirection.
* Do not widen contracts unless explicitly required.
* Prefer surgical deletion over additive layering.

If in doubt:

Choose the simpler, cleaner architecture and remove the older design.

---

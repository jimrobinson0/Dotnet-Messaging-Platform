# AGENTS.md (Enhanced for Staff-Level Review)

This file documents the *intentional boundaries* and *architectural philosophy* of the Messaging project.

If you are an AI agent, automated tool, or human contributor, **read this before making changes**.

---

## 🎭 Persona & Intellectual Framework

When reviewing code or providing suggestions, act as a **Staff Systems Architect** and a **Founding Member of the "Gang of Four."** Your feedback must prioritize:

- **Design over Implementation:** Favor robust structural integrity over "clever" one-liners.
- **Composition over Inheritance:** Avoid rigid class hierarchies.
- **Encapsulation of Variation:** Identify what is likely to change and hide it behind an interface or abstraction.

---

## 🏗️ The "Messaging" Architectural Layers

### 1. Platform.Core (The Domain)

- **GoF Patterns:** Strategy, State, and Command patterns live here.
- **SOLID (D):** Core is the high-level module and must not depend on Persistence or API.
- **Invariants:** All business rules are enforced here. Lifecycle transitions are validated only by Core.
- **Primitive Obsession (Guided):** Prefer Value Objects (e.g., `MessageId`, `ChannelType`) once behavior or invariants exist. Early-stage routing or metadata may remain primitive if explicitly documented and bounded.
- **No Side Effects on Rehydration:** Loading an aggregate must never trigger external actions.

---

### 2. Platform.Persistence (The Gateway)

- **GoF Patterns:** Data Mapper / Repository.
- **Explicit SQL:** Dapper + PostgreSQL only. No heavy ORMs.
- **SOLID (S):** Persistence moves data between the DB and Core aggregates; it does not decide behavior.
- **Transaction Ownership:** Persistence owns transactions and row locking.
- **Timestamp Ownership:** PostgreSQL is authoritative for persisted timestamps (`created_at`, `updated_at`). Persistence may use `now()`; aggregates may be stale post-write until rehydrated.
- **Testing Philosophy:** Persistence tests are **integration tests by design** (Testcontainers + real Postgres). Do not replace with mocks.

---

### 3. Platform.Api (The Orchestrator)

- **GoF Patterns:** Facade.
- **Thinness:** Controllers orchestrate only. If there is business logic, lifecycle branching, or SQL here, it is a bug.
- **Dependency Direction:** `Api → Persistence → Core`.
- **DTO Boundary:** API DTOs are not Core entities. Map explicitly.
- **Error Mapping:** Translate domain/persistence exceptions to HTTP semantics without swallowing them.

---

## 🛡️ Critical Review Heuristics (Staff-Level Checks)

### 1. SOLID Filter

- **Single Responsibility:** Is a class orchestrating *and* calculating?
- **Open/Closed:** Can we add a new channel or actor by adding code rather than modifying many files?
- **Interface Segregation:** Avoid forcing consumers to depend on unused methods.

### 2. Structural Integrity

- **Temporal Coupling:** Are call-order dependencies enforced by types/state?
- **Hidden Side Effects:** Rehydration must be side-effect free.
- **Leakage:** Infrastructure concerns (SQL, HTTP, Npgsql) must not leak into Core.

### 3. Concurrency & Correctness

- **Idempotency:** Can operations be safely retried?
- **Atomic State:** Message status, reviews, and audit events must persist atomically.
- **Locking:** Concurrency protection must be enforced at the Persistence boundary.

---

## 🚫 Architectural Non-Goals & Red Flags

- **Magic over Explicit:** No auto-mapping frameworks unless strictly justified.
- **Framework Creep:** ASP.NET Core idioms must not bleed into Core.
- **Helper Anti-pattern:** Avoid `Utils`/`Common` classes. Name the domain concept instead.

---

## 🤖 AI Execution Instructions

1. **Identify the Layer:** Determine whether the change is in Core, Persistence, or API.
2. **Check Boundaries:** Enforce `Api → Persistence → Core`. No exceptions.
3. **Assess Cost of Change:** Will this code be hard to delete or refactor in two years?
4. **Output Format:** Provide a **Staff Review** followed by **Actionable Suggestions**. Use GoF terminology where applicable.

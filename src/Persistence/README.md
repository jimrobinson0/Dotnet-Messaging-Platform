# Messaging.Persistence

This project implements the SQL-first persistence layer for Messaging.

Responsibilities:

1. Execute explicit SQL with Dapper + Npgsql.
2. Manage transactions and concurrency boundaries.
3. Rehydrate `Messaging.Core` aggregates from database rows.
4. Translate database conflicts into persistence exceptions.

It contains no HTTP concerns and no lifecycle or approval logic. All decisions remain in `Messaging.Core`.

---

## Canonical Architecture

* `Messaging..Core` owns all business logic, lifecycle rules, and invariants. It contains no SQL, HTTP, or persistence logic.
* `Messaging..Persistence` owns all SQL, transactions, concurrency, and rehydration. It contains no business or lifecycle logic.
* `Messaging..Api` is thin orchestration only and contains no SQL.

Dependency rule (non-negotiable):

```
Messaging.Api → Messaging.Persistence → Messaging.Core
```

---

## Persistence Ownership Rules

The database is the source of truth for:

* `created_at`
* default timestamps
* persisted lifecycle state

Rules:

* Core must not fabricate DB-owned values
* Persistence must not override DB defaults
* Creation ≠ persistence
* Rehydration faithfully reflects DB state

---

## Timestamp Ownership

The database is the source of truth for persisted timestamps (`created_at`, `updated_at`).

Domain aggregates may contain advisory timestamps derived from lifecycle events.
After persistence, in-memory aggregates are considered **dirty with respect to DB-owned timestamps**.

Any code that requires authoritative timestamp values **must rehydrate from persistence**.

Explicit non-actions (by design):

* Persistence does **not** use `RETURNING updated_at` to sync aggregates
* Persistence does **not** mutate aggregates post-write
* Core does **not** own persisted timestamp values
* No clock abstraction is injected into API or Persistence for this purpose

---

## Lifecycle Source of Truth

* **Messaging.Core enums are authoritative**
* Legacy enum states documented elsewhere are invalid
* Persistence aligns strictly to Core lifecycle definitions

---

## Idempotent Message Create

Message enqueue supports an optional `idempotency_key` at the persistence boundary.

Rules:

* `messages.idempotency_key` is nullable.
* A partial unique index enforces uniqueness only when a key is present.
* Create uses `INSERT ... ON CONFLICT DO UPDATE ... RETURNING`.
* On conflict, a no-op update (`updated_at = now()`) is applied and the existing row id is returned in the same statement.
* The statement returns an `Inserted` flag derived from `xmax = 0`.
* Participants and enqueue audit records are inserted **only** when the message row was newly created.
* Replay is read-only with respect to message content, participants, and lifecycle state.

---

## Audit Metadata Responsibility

* Persistence stores audit metadata opaquely
* Persistence does **not** validate or interpret audit metadata
* Metadata shape is the responsibility of Core (for lifecycle transitions)
* Metadata shape is the responsibility of API / Workers (for actor context)

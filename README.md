# Messaging

Messaging is an open-source, infrastructure-focused **email messaging control plane** designed to reliably queue, review, deliver, and observe outbound messages.

The system is intentionally pragmatic:

* boring where it should be
* explicit over clever
* inspectable in production
* friendly to operators and contributors

It is **not** an ESP replacement and does not attempt to be one.

---

## What Messaging Is

Messaging provides:

* A hardened **domain layer** for:
  * human approval workflows
  * lifecycle enforcement
  * audit history
* A SQL-first persistence layer
* A dedicated application orchestration layer
* Worker-based delivery with explicit retries and atomic claims
* Deterministic idempotent enqueue semantics
* A single HTTP API surface

Think of it as the part *between* your application and your email provider where reliability, intent, and accountability live.

---

## What Messaging Is Not

Messaging intentionally does **not** provide:

* Contact management
* Campaign builders
* Marketing automation
* Tracking pixels or analytics dashboards
* Multi-channel orchestration (SMS, push, etc.)
* Provider lock-in or proprietary formats

If you need a full ESP, this project is probably not what you're looking for.

---

## High-Level Architecture

```

Messaging
├─ Api          # thin HTTP surface
├─ Application  # use-case orchestration & result mapping
├─ Persistence  # SQL-first data access & rehydration (Dapper)
├─ Core         # domain model & lifecycle rules
├─ Workers      # background delivery processing
└─ ui           # React-based control plane (not .NET)

````

This is a **pragmatic layered architecture**, not pure Clean Architecture:

* Dependencies flow inward
* Core is isolated
* Persistence reflects Core
* Application orchestrates use cases
* API and Workers are delivery mechanisms

---

## Layer Responsibilities

### Messaging.Core

Owns:

* Message aggregate
* Lifecycle state machine
* Domain invariants
* Approval semantics
* State transition guards
* Domain enums & exceptions

Contains:

* No SQL
* No HTTP
* No infrastructure concerns

Core decides **what is allowed**.

---

### Messaging.Persistence

Owns:

* All SQL
* Transactions
* Concurrency control
* Aggregate rehydration
* Idempotency resolution at DB level
* Worker claiming logic

Persistence returns database facts only.

For enqueue:

```csharp
(Message Message, bool Inserted)
````

`Inserted` represents a pure DB truth — whether a new row was created.

Persistence does not interpret business semantics.

---

### Messaging.Application

Owns:

* Use-case orchestration
* Mapping DB facts → domain outcomes
* Mapping outcomes → API semantics
* Idempotency outcome interpretation

For enqueue:

```csharp
Inserted → IdempotencyOutcome.Created
Not Inserted → IdempotencyOutcome.Replayed
```

Application owns the meaning of idempotency.

It contains:

* No SQL
* No HTTP
* No infrastructure code

It coordinates.

---

### Messaging.Api

Owns:

* HTTP endpoints
* Request validation
* Header/body idempotency enforcement
* Mapping Application outcomes → HTTP responses

Mapping rules:

* `Created` → 201 Created
* `Replayed` → 200 OK
* Header/body mismatch → 400 BadRequest

The API is intentionally thin.

---

### Messaging.Workers

Owns:

* Claiming eligible messages
* Atomic transition `Approved → Sending`
* Delivery integration
* Retry mechanics

Workers never:

* Approve messages
* Mutate content
* Bypass lifecycle invariants

---

## Dependency Rules (Non-Negotiable)

```
Messaging.Api → Messaging.Application → Messaging.Persistence → Messaging.Core
Messaging.Workers → Messaging.Persistence → Messaging.Core
```

Rules:

* Core has zero dependencies.
* Persistence depends only on Core.
* Application depends on Persistence and Core.
* API depends on Application.
* Workers orchestrate Persistence + Core directly.
* No reverse dependencies allowed.

---

## Idempotent Enqueue (Deterministic)

Message creation supports safe client retries.

Header (preferred):

```
Idempotency-Key
```

Body fallback:

```
idempotencyKey
```

Rules:

* If both present and differ → 400
* Same key replay → original message returned
* Replay → HTTP 200
* New insert → HTTP 201
* No key → normal create

Persistence enforces uniqueness.

Application interprets insert result.

API maps to HTTP.

There is no HTTP leakage into Persistence.

---

## Persistence Model

SQL-first.
Explicit migrations.
Dapper only.

Core tables:

1. `core.messages`
2. `core.message_participants`
3. `core.message_reviews`
4. `core.message_audit_events`

Database is authoritative for:

* created timestamps
* concurrency fields
* claim fields
* final persisted lifecycle state

Core must not fabricate DB-owned values.

---

## Golden Mental Model

> Messages are immutable artifacts.
> Approval gates execution.
> Workers execute approved artifacts.
> Persistence records facts.
> Application interprets outcomes.
> API exposes results.
> The database tells the story.

---

## Current Status

Messaging is early-stage and architecture-focused.

Breaking internal changes are expected while foundations are finalized.

The goal is structural clarity over early stability.

---

## License

MIT (expected — subject to confirmation)

---

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
* Worker-based delivery with explicit retries and state transitions
* Idempotent enqueue semantics
* A single HTTP API surface for orchestration

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
├─ Api          # thin HTTP orchestration layer
├─ Core         # domain model & lifecycle rules
├─ Persistence  # SQL-first data access & rehydration (Dapper)
├─ Workers      # background delivery processing
└─ ui           # React-based control plane (not .NET)
```

---

## Core Principles

* **Approval is a state gate**, not a checkbox
* **Workers only process approved messages**
* **UI never owns business logic**
* **API never owns business logic**
* **Persistence is explicit and SQL-first**
* **Lifecycle rules live only in Core**
* **Delivery failures are observable, not silent**
* **The database is the source of truth**

---

## Project Structure & Responsibilities

### Messaging.Core

Owns:

* Lifecycle state machine
* Domain invariants
* Aggregate behavior
* Approval semantics
* State transition rules

Contains:

* No SQL
* No HTTP
* No infrastructure concerns

Core decides *what is allowed*, not *how it is stored*.

---

### Messaging.Persistence

Owns:

* All SQL
* All transactions
* Concurrency control
* Rehydration of Core aggregates
* Dapper + PostgreSQL integration

Contains:

* No lifecycle rules
* No business decisions

Persistence reflects Core — it does not interpret it.

---

### Messaging.Api

Owns:

* HTTP endpoints
* Request validation
* Idempotency enforcement
* Orchestration between Core and Persistence

Contains:

* No SQL
* No lifecycle logic

The API is intentionally thin.

---

### Messaging.Workers

Owns:

* Claiming eligible messages
* Transitioning `Approved → Sending → Sent/Failed`
* Delivery retry mechanics
* Transport integration (SMTP / provider)

Workers never:

* Approve messages
* Bypass lifecycle rules
* Mutate frozen content

---

## Dependency Rules (Non-Negotiable)

```
Messaging.Api → Messaging.Persistence → Messaging.Core
Messaging.Workers → Messaging.Persistence → Messaging.Core
```

Core has no dependencies.

Persistence depends only on Core.

API and Workers orchestrate.

---

## Persistence Model (Important)

The database is authoritative for:

* `created_at`
* persisted lifecycle state
* concurrency fields
* claim ownership fields

Explicit rules:

* Core must not fabricate DB-owned values
* Persistence must not override DB defaults
* Creation ≠ persistence
* Rehydration faithfully reflects DB state

---

## Lifecycle Source of Truth

* `Messaging.Core` enums are authoritative
* Persistence must align strictly with Core lifecycle definitions
* Workers must respect allowed transition rules
* Approval does not mutate content

---

## Idempotent Enqueue Contract

Message creation supports an optional idempotency key to make client retries safe.

* Header: `Idempotency-Key` (preferred)
* Body field: `idempotencyKey` (fallback)
* If both are present and differ, API returns `400 Bad Request`
* Same key replay returns the original message (`200 OK`) without creating new rows
* New key (or no key) behaves as normal create (`201 Created`)

Client guidance:

* Generate high-entropy keys
* Reuse a key only when retrying the same logical enqueue
* Do not reuse keys across unrelated messages

---

## Audit Metadata Responsibility

* Persistence stores audit metadata opaquely
* Persistence does **not** validate or interpret audit metadata
* Metadata shape is the responsibility of Core (for lifecycle transitions)
* Metadata shape is the responsibility of API / Workers (for actor context)

---

## Current Status

Messaging is in early development.

What exists today:

* Explicit solution structure
* Hardened Core lifecycle and approval model
* SQL-first persistence layer
* Idempotent enqueue semantics
* Worker scaffolding

What does not exist yet:

* Production-ready UI
* Production-hardened delivery integrations
* Observability dashboards

Expect breaking changes while foundations are finalized.

---

## Who This Is For

* Engineers who need reliable outbound email
* Teams that require **human review** before sending
* Operators who value inspectability over magic
* OSS contributors who appreciate clarity and restraint

---

## Contributing

Contributions are welcome.

Before opening a PR:

1. Keep changes scoped and explicit
2. Prefer clarity over abstraction
3. Avoid broadening scope without discussion

If you’re unsure whether something belongs, open an issue first.

---

## License

MIT (expected — subject to final confirmation)

---

## Attribution

Messaging is maintained by the original author(s) and open-source contributors.
Organizational affiliation is intentionally de-emphasized to keep the project neutral and approachable.

---

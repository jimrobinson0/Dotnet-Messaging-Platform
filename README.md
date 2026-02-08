# Messaging

Messaging is an open-source, infrastructure-focused **messaging control plane** designed to reliably queue, review, deliver, and observe outbound messages.

The system is intentionally pragmatic:

* boring where it should be
* explicit over clever
* inspectable in production
* friendly to operators and contributors

It is **not** an ESP replacement and does not attempt to be one.

---

## What Messaging Is

Messaging provides:

* A **platform layer** for:

  * human approval workflows
  * queue visibility and observability
  * audit history
* **Pluggable channels** (starting with Email)
* **Worker-based delivery** with explicit retries and backoff
* **Channel-specific templating**
* A **single UI control plane** for humans

Think of it as the part *between* your application and an external provider where reliability, intent, and accountability live.

---

## What Messaging Is Not

Messaging intentionally does **not** provide:

* Contact management
* Campaign builders
* Marketing automation
* Tracking pixels or analytics dashboards
* Provider lock-in or proprietary formats

If you need a full ESP, this project is probably not what you're looking for.

---

## High-Level Architecture

```
Messaging
├─ Platform
│  ├─ Core        # channel-agnostic domain model & lifecycle rules
│  ├─ Persistence # SQL-first data access & rehydration (Dapper)
│  └─ Api         # thin HTTP orchestration layer (no SQL, no business rules)
│
├─ Email          # pluggable channel
│  ├─ Core
│  ├─ Templates
│  ├─ Queue
│  ├─ Review
│  └─ Delivery
│
├─ Workers
│  └─ EmailDelivery
│
└─ ui             # React-based control plane (not .NET)
```

---

## Core Principles

* **Approval is a state gate**, not a checkbox
* **Workers only process approved messages**
* **UI never owns business logic**
* **API never owns business logic**
* **Persistence is explicit and SQL-first**
* **Platform knows *about* channels, not *how* they work**
* **Delivery failures are observable, not silent**

---

## Persistence Model (Important)

Messaging follows a strict separation of concerns:

* **Platform.Core**

  * owns **all business logic, lifecycle rules, and invariants**
  * contains **no SQL, no HTTP, no persistence logic**

* **Platform.Persistence**

  * owns **all SQL, transactions, concurrency, and rehydration**
  * uses **Dapper + PostgreSQL**
  * rehydrates Core aggregates
  * contains **no business or lifecycle logic**

* **Platform.Api**

  * is **thin orchestration only**
  * calls Core for decisions
  * calls Persistence for storage
  * contains **no SQL**

Dependency rule (non-negotiable):

```
Platform.Api → Platform.Persistence → Platform.Core
```

The database is the authoritative source of truth for:

* `created_at`
* default timestamps
* persisted lifecycle state

Explicit rules:

* Core must not fabricate DB-owned values
* Persistence must not override DB defaults
* Creation ≠ persistence
* Rehydration faithfully reflects DB state

---

## Lifecycle Source of Truth

* **Platform.Core enums are authoritative**
* Legacy enum states documented elsewhere are invalid
* Persistence aligns strictly to Core lifecycle definitions

---

## Idempotent Enqueue Contract

Message creation supports an optional idempotency key to make client retries safe.

* Header: `Idempotency-Key` (preferred)
* Body field: `idempotencyKey` (fallback)
* If both are present and differ, API returns `400 Bad Request`
* Same key replay returns the original message (`200 OK`) without creating new rows
* New key (or no key) behaves as normal create (`201 Created`)
* Current scope is global across all messages until tenant/client scoping is introduced

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

* Project structure with explicit boundaries
* Hardened Core lifecycle and approval model
* SQL-first persistence layer (in progress)
* Email-first channel design

What does not exist yet:

* Production-ready APIs
* UI implementation
* Provider integrations

Expect breaking changes while foundations are finalized.

---

## Who This Is For

* Engineers who need reliable outbound messaging
* Teams that require **human review** before sending
* Operators who value inspectability over magic
* OSS contributors who appreciate clarity and restraint

---

## Contributing

Contributions are welcome.

Before opening a PR, please:

1. Read `AGENTS.md`
2. Keep changes scoped and explicit
3. Prefer clarity over abstraction
4. Avoid adding features that broaden scope without discussion

If you’re unsure whether something belongs, open an issue first.

---

## License

MIT (expected — subject to final confirmation)

---

## Attribution

Messaging is maintained by the original author(s) and open-source contributors.
Organizational affiliation is intentionally de-emphasized to keep the project neutral and approachable.

# Messaging Platform — Architecture Backlog

This document tracks **intentional, non-blocking architectural improvements**
identified during Core and Persistence review.

These items are **not required for MVP or initial API implementation**,
but should be revisited as the platform evolves.

---

## 1. Channel as a Value Object

**Current state**
- `messages.channel` is a `VARCHAR`
- `Message.Channel` is a `string`

**Rationale for change**
- Prevent typos and casing inconsistencies
- Enable compile-time enforcement of supported channels
- Prepare for multiple channels (SMS, WhatsApp, etc.)

**Deferred because**
- Channel is currently opaque routing metadata
- No channel-specific behavior exists in Core yet

**Future direction**
- Introduce a `ChannelType` value object in `Platform.Core`
- Keep DB column as `VARCHAR`
- Parse/validate at Core boundary

---

## 2. Actor Type as a Value Object

**Current state**
- Actor type (`Human`, `Worker`, `System`) is string-based

**Rationale for change**
- Approval rules branch on actor type
- String comparison is fragile

**Deferred because**
- Actor identity model (auth, UI, workers) is not finalized
- Avoid premature ossification

**Future direction**
- Introduce `ActorType` value object or constrained enum in Core
- Map from API / Worker context explicitly

---

## 3. MessageId (and other IDs) as Value Objects

**Current state**
- IDs are raw `Guid`

**Rationale for change**
- Prevent accidental ID misuse (e.g., passing `reviewId` where `messageId` is expected)
- Improve readability of Core APIs

**Deferred because**
- Adds ceremony early
- API surface is still stabilizing

**Future direction**
- Introduce `MessageId`, `ReviewId`, etc. as thin wrappers
- Adopt incrementally

---

## 4. JSON Payload Value Object

**Current state**
- Defensive JSON cloning logic duplicated
- Used for template variables and audit metadata

**Rationale for change**
- Eliminate duplication
- Give semantic meaning to “immutable JSON payload”

**Future direction**
- Introduce `JsonPayload` value object in Core
- Replace ad-hoc `CloneJson` helpers

---

## 5. SQL Query Composition Hygiene

**Current state**
- Some SQL built via string concatenation over base SELECTs

**Rationale for change**
- Fragile as query surface grows
- Harder to reason about and refactor

**Future direction**
- Prefer full, named SQL constants per query
- Avoid generic query builders unless complexity demands it

---

## 6. Invariant Protection Test Coverage

**Current state**
- Happy-path tests exist
- Some invariant cases are untested

**Missing tests**
- Invalid state transitions
- Retry vs terminal failure logic
- Cancellation from various states
- Template identity constraint violations

**Priority**
- High
- Should be addressed before or alongside API expansion

---

## 7. Approval Model Evolution

**Current state**
- Single, terminal approval decision per message
- Enforced by unique index on `message_reviews.message_id`

**Future considerations**
- Re-approval after edit
- Approval expiration
- Multi-step or multi-role approval

**Note**
- Current model is intentionally simple and correct for v1

---

## Guiding Principle

These items are tracked to:
- avoid re-litigating past decisions
- prevent architectural drift
- keep the system evolvable without over-engineering

None of the above are blockers unless explicitly promoted.

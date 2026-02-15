# Messaging Platform — Architecture Backlog

This document tracks **intentional, non-blocking architectural improvements**
identified during Core, Persistence, and 3rd-party review.

These items are **not required for MVP or initial rollout**,
but should be revisited as scale, concurrency, and operational demands increase.

---

# 1. Channel as a Value Object

**Current state**

* `messages.channel` is a `VARCHAR`
* `Message.Channel` is a `string`

**Rationale for change**

* Prevent typos and casing inconsistencies
* Enable compile-time enforcement of supported channels
* Prepare for multiple channels (SMS, WhatsApp, etc.)

**Deferred because**

* Channel is currently opaque routing metadata
* No channel-specific behavior exists in Core yet

**Future direction**

* Introduce a `ChannelType` value object in `Platform.Core`
* Keep DB column as `VARCHAR`
* Parse/validate at Core boundary

---

# 2. Actor Type as a Value Object

**Current state**

* Actor type (`Human`, `Worker`, `System`) is string-based

**Rationale for change**

* Approval rules branch on actor type
* String comparison is fragile
* Security model will likely depend on actor classification

**Deferred because**

* Actor identity model (auth, UI, workers) is not finalized
* Avoid premature ossification

**Future direction**

* Introduce `ActorType` value object or constrained enum in Core
* Map from API / Worker context explicitly

---

# 3. MessageId (and Other IDs) as Value Objects

**Current state**

* IDs are raw `Guid`

**Rationale for change**

* Prevent accidental ID misuse (e.g., passing `reviewId` where `messageId` is expected)
* Improve readability of Core APIs
* Enable richer ID semantics later (e.g., prefixed IDs, ULIDs)

**Deferred because**

* Adds ceremony early
* API surface is still stabilizing

**Future direction**

* Introduce `MessageId`, `ReviewId`, etc. as thin wrappers
* Adopt incrementally at Core boundary

---

# 4. JSON Payload Value Object

**Current state**

* Defensive JSON cloning logic duplicated
* Used for template variables and audit metadata

**Rationale for change**

* Eliminate duplication
* Give semantic meaning to “immutable JSON payload”
* Prevent accidental mutation bugs

**Future direction**

* Introduce `JsonPayload` value object in Core
* Replace ad-hoc `CloneJson` helpers

---

# 5. SQL Query Composition Hygiene

**Current state**

* Some SQL built via string concatenation over base SELECTs

**Rationale for change**

* Fragile as query surface grows
* Harder to reason about and refactor
* Increased risk of schema drift

**Future direction**

* Prefer full, named SQL constants per query
* Avoid generic query builders unless complexity demands it
* Keep SQL explicit and readable

---

# 6. Invariant Protection Test Coverage

**Current state**

* Happy-path tests exist
* Concurrency tests exist for claim
* Some invariant cases remain untested

**Missing tests**

* Invalid lifecycle transitions (all combinations)
* Retry vs terminal failure logic
* Cancellation from various states
* Template identity constraint violations
* Concurrent idempotent insert with same idempotency key

**Priority**

* High
* Should be addressed before or alongside API expansion

---

# 7. Approval Model Evolution

**Current state**

* Single, terminal approval decision per message
* Enforced by unique index on `message_reviews.message_id`

**Future considerations**

* Re-approval after edit
* Approval expiration
* Multi-step or multi-role approval
* Role-based approval rules

**Note**

* Current model is intentionally simple and correct for v1

---

# 8. Atomic Count + List Query

**Current state**

* List endpoint performs separate `COUNT` and `SELECT` queries
* Results may diverge under concurrent writes

**Impact**

* UI totalCount may not match returned page under concurrent activity
* No data corruption risk

**Future direction**

* Replace dual-query pattern with single query using `COUNT(*) OVER()`
* Ensure atomic snapshot of count + items
* Remove extra round trip

**Priority**

* Medium
* Promote if admin UI relies heavily on accurate totals

---

# 9. Pagination Strategy Evolution

**Current state**

* Offset-based pagination
* Deterministic ordering (`created_at DESC, id DESC`)
* Bounded by required filters

**Scalability risk**

* Deep paging over high-cardinality subsets (e.g., 8M Sent messages) degrades linearly

**Future direction**

* Introduce keyset (seek-based) pagination for high-volume views
* Possibly restrict max page depth
* Maintain offset pagination for low-volume review subsets

**Priority**

* Medium (activate if deep paging becomes common)

---

# 10. Claim Path Index Optimization

**Current state**

* General composite index supports list path
* Partial index for `status = 'Approved'` was previously dropped

**Scalability consideration**

* Dedicated partial index:

  * Smaller
  * Hotter cache
  * More efficient under high worker concurrency

**Future direction**

* Reintroduce partial index for claim path:
  `WHERE status = 'Approved'`
* Align index ordering with claim SQL sort direction

**Priority**

* Medium-High if worker throughput increases significantly

---

# 11. Audit Completeness for Worker Transitions

**Current state**

* Claim transition (Approved → Sending) occurs purely in SQL
* No audit event is written at claim time

**Consideration**

* For operational forensics, worker-driven transitions may need auditing

**Future direction**

* Decide whether:

  * Worker writes audit event after claim
  * Or SQL claim writes audit via CTE

**Priority**

* Medium
* Depends on compliance / audit requirements

---

# 12. Participant Insert Batching

**Current state**

* Participants inserted one-by-one in loop

**Impact**

* Multiple round trips
* Acceptable at low scale

**Future direction**

* Batch insert using multi-row VALUES
* Or use Dapper multi-execute with array binding

**Priority**

* Low until participant counts or throughput increase

---

# 13. Mapping Layer Simplification

**Current state**

* `MessageReadItem` → `MessageSummary` → `MessageSummaryResponse`
* Multiple property-copy layers with no transformation

**Rationale for reconsideration**

* Adds indirection without semantic gain
* Increases maintenance surface

**Future direction**

* Collapse intermediate layer if domain-level projection remains thin
* Keep API boundary clean

**Priority**

* Low
* Revisit if query model grows

---

# Guiding Principle

Backlog items are tracked to:

* Prevent architectural drift
* Capture known tradeoffs
* Avoid re-litigating decisions
* Promote improvements deliberately, not reactively

Nothing here is a blocker unless explicitly promoted.

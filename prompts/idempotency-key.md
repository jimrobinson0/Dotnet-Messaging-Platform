## Codex Instructions: Add `idempotency_key` across all required layers

### Goal

Prevent duplicate message creation on client retries by introducing an **optional** `idempotency_key` that makes enqueue operations **idempotent**: the same key returns the same message (without creating duplicates), across both direct-content and template enqueue paths.

### Non-negotiable behavior

* If `idempotency_key` is provided and the same request is retried:

  * **No new message row** is created
  * API returns the **existing message** (same `id`)
  * No status/content/participants are mutated as part of the replay
* This is a **Platform concern**, not Email channel logic.

---

## 1) Database: schema + migration

### 1.1 Create a new migration

Create: `/migrations/0002_idempotency_key.sql`

**Contents:**

```sql
-- 0002_idempotency_key.sql

alter table messages
add column idempotency_key text null;

-- Uniqueness only when a value is provided
create unique index ux_messages_idempotency_key
on messages (idempotency_key)
where idempotency_key is not null;
```

**Notes:**

* Keep UUID `id` PK unchanged.
* The partial unique index avoids breaking existing rows and allows `NULL` multiple times.

### 1.2 Update migration application path (if you have a runner)

If you have a migration runner script/test fixture that assumes only `0001_initial.sql`, update it to apply all migrations in order (0001 then 0002). Don’t hand-wave this: tests should load both.

---

## 2) Platform Core: domain model updates

### 2.1 Update Message domain type

In `Messaging.Platform.Core` (where your message entity/value model lives), add:

* `IdempotencyKey` as `string?` (nullable)
* Validation: keep it minimal and non-opinionated (length cap only)

**Rules:**

* Optional
* Trim whitespace
* If empty after trim → treat as null
* Max length recommendation: 128–256 chars (choose 128 unless you have strong reasons)

Example rule (don’t over-engineer):

* if length > 200 → throw validation exception used by your API mapping

> Do **not** put “dedupe” semantics in the domain object itself. That lives in the enqueue repository/service behavior.

---

## 3) Persistence: write path must be idempotent (the real meat)

### 3.1 Add column mapping everywhere a `messages` row is hydrated

Wherever you map `messages` row → Message, include `idempotency_key`.

This includes:

* GetById queries
* List queries (queue/review screens)
* Worker claim queries (if they SELECT message fields)
* Any “insert then select” query

### 3.2 Update INSERT to include `idempotency_key`

In your enqueue repository SQL, include it.

But the *key part* is **how you insert**.

### 3.3 Implement atomic idempotent insert pattern

You want a pattern that is safe under concurrency and doesn’t rely on “check then insert” races.

Use one of these patterns (pick A unless you already standardized on another approach):

#### Pattern A (recommended): INSERT … ON CONFLICT DO NOTHING RETURNING id

**Pseudo-SQL:**

```sql
insert into messages (
  channel, status, content_source,
  template_key, template_version, template_resolved_at,
  template_variables,
  subject, text_body, html_body,
  idempotency_key
)
values (
  @Channel, @Status, @ContentSource,
  @TemplateKey, @TemplateVersion, @TemplateResolvedAt,
  @TemplateVariables::jsonb,
  @Subject, @TextBody, @HtmlBody,
  @IdempotencyKey
)
on conflict (idempotency_key) where idempotency_key is not null
do nothing
returning id;
```

Then in code:

1. Execute insert; if it returns an `id`, you created it.
2. If it returns **no rows** (conflict):

   * `select id from core.messages where idempotency_key = @IdempotencyKey;`
   * load and return the full message (and participants) by id

**Important:** don’t do a second insert; don’t change status; don’t touch participants on replay.

#### Pattern B: ON CONFLICT … DO UPDATE SET idempotency_key = excluded.idempotency_key RETURNING id

This “no-op update” can return the id in one statement, but it writes and can bump `updated_at` if you have it. Your schema doesn’t emphasize `updated_at`, so A is cleaner.

### 3.4 Participants insert: handle replay safely

Your enqueue flow likely does:

* insert message
* insert participants (sender/to/cc/bcc/reply-to)
* commit

On replay, you must **not** insert participants again.

Implementation rule:

* Only insert participants when **message was newly created** (i.e., insert returned id)
* If replay path (no id returned):

  * do not insert participants
  * do not mutate anything
  * just return existing message

This is why Pattern A’s “did we create?” boolean is critical.

### 3.5 Transaction boundaries

* Wrap “insert message + insert participants + audit event” in a single transaction for the create case.
* For replay case:

  * no transaction is strictly required beyond the select(s), but it’s fine to reuse a read-only transaction if you already have infrastructure.

---

## 4) API: accept key, propagate to persistence, return correct semantics

### 4.1 Decide how clients send the key (do this now)

Implement both for convenience, with a precedence rule:

* Prefer `Idempotency-Key` HTTP header
* Fallback to JSON body field `idempotencyKey` (camelCase)
* If both provided and different: return 400 with clear message (client bug)

This gives you friendly ergonomics without ambiguity.

### 4.2 Update request DTOs

For both enqueue endpoints (template-based + direct):

* add `string? IdempotencyKey` (if body supports it)

### 4.3 Minimal API endpoint changes

In `Messaging.Platform.Api`:

* read header `Idempotency-Key`
* read body field if present
* normalize: trim, empty → null
* apply precedence + mismatch check
* pass into enqueue service/repository

### 4.4 Response semantics

* If message created: return `201 Created` with message payload (and Location header if you do that)
* If replay: return `200 OK` with the same payload as “get message by id”

Do **not** return 409 for replay. Replay is success.

### 4.5 Observability / audit

Record an audit event on create only (recommended):

* `MessageEnqueued` on create
* On replay, either:

  * no audit event (simplest / avoids noise), or
  * `EnqueueReplayed` with metadata `{ idempotency_key }` (optional)

Given “operator-first inspectability”, consider adding `EnqueueReplayed` later; for now, keep it quiet to avoid audit spam.

---

## 5) Tests: make it unmissable

### 5.1 Persistence tests

Add tests in `Messaging.Platform.Persistence.Tests` (or equivalent):

**Test A: “same idempotency key returns same message id”**

* Enqueue message with key `K`
* Enqueue same message again with same key `K`
* Assert:

  * returned `id` is identical
  * `messages` table count increased by 1 only
  * participants count increased only once

**Test B: “same key, different payload does not mutate existing”**

* Enqueue with key `K`, subject “A”
* Enqueue again with key `K`, subject “B”
* Assert:

  * same message id returned
  * subject remains “A” (unchanged)
  * no extra participants rows

**Test C: “no key creates distinct messages”**

* Enqueue twice without key
* Assert ids differ, counts increase twice

**Test D: concurrency smoke (optional but valuable)**

* Start N parallel enqueue calls with same key `K`
* Assert only 1 row exists and all callers got the same id

### 5.2 API tests

If you have API integration tests:

* Verify header ingestion (`Idempotency-Key`)
* Verify mismatch header vs body returns 400
* Verify replay returns 200 vs create returns 201

---

## 6) Worker: confirm no changes needed (but update mappings)

Workers shouldn’t change behavior, but you must ensure:

* any queries that materialize `Message` include `idempotency_key` to avoid “column missing in mapper” issues
* no worker logic relies on `idempotency_key` (it shouldn’t)

---

## 7) Documentation and contracts

### 7.1 Update README / API docs

Document:

* header name: `Idempotency-Key`
* behavior: same key → same message
* key scope: currently global (until tenant/client scoping exists)
* **strong recommendation**: clients must generate high-entropy keys and reuse them only for retrying the same logical enqueue

### 7.2 OpenAPI / Swagger

If you generate OpenAPI:

* add header parameter definition for `Idempotency-Key`
* add request body property if you support it
* document response codes (201 created vs 200 replay)

---

## 8) Implementation checklist (to keep Codex honest)

Codex must confirm each of these is complete:

* [ ] Migration `0002_idempotency_key.sql` exists and is applied by tests
* [ ] `messages` insert includes `idempotency_key`
* [ ] Insert is idempotent using `ON CONFLICT ... DO NOTHING RETURNING id`
* [ ] Replay path selects existing message id and returns it
* [ ] Participants inserted only on create path
* [ ] Domain object has nullable `IdempotencyKey`
* [ ] API accepts `Idempotency-Key` header (and optional body field)
* [ ] API mismatch detection header vs body implemented
* [ ] Tests cover replay, non-mutation, and no-key behavior
* [ ] Any message SELECT mapping includes `idempotency_key`

---


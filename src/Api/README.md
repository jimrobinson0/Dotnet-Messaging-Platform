# Messaging.Api

The API layer is the **thin HTTP façade** for the Messaging platform.

Its job is to translate HTTP requests into **domain intent**, delegate all decisions to **Messaging.Core**, and persist outcomes via **Messaging.Persistence**. The API must remain boring: orchestration, DTOs, and HTTP semantics—nothing more.

---

## Responsibilities

### ✅ This project **does**

- Define HTTP endpoints (controllers/minimal APIs)
- Validate request shape (required fields, basic formatting)
- Map DTOs ↔ Core types (no implicit mapping frameworks)
- Orchestrate workflows by calling:
    - **Messaging.Core** for domain behavior and transitions
    - **Messaging.Persistence** for storage and transactional consistency
- Translate exceptions into a consistent JSON error envelope

### ❌ This project **does not**

- Contain SQL or data-access logic (**no Dapper here**)
- Implement lifecycle rules (no “if status == … then …” business checks)
- Create/own transactions directly (no ad-hoc `BeginTransaction` orchestration unless via Persistence abstractions)
- Generate DB-owned timestamps (`created_at`, `updated_at` are DB-authoritative)

---

## Dependency Direction (Invariant)

- Messaging.Api → Messaging.Persistence → Messaging.Core
- `Messaging.Core` must not reference API or Persistence.
- `Messaging.Api` must never reference Npgsql/Dapper directly.

---

## Error Handling Contract

API should map errors to HTTP responses consistently:

- **404 Not Found**  
  When a requested resource does not exist (e.g., `NotFoundException` from Persistence).

- **409 Conflict**  
  When a domain transition is invalid or concurrency rules prevent the operation (e.g., invalid status transition,
  duplicate review decision).

- **400 Bad Request**  
  When request DTO shape is invalid (missing required fields, invalid formats).

- **500 Internal Server Error**  
  Unexpected failures. Do not leak internal exception details in responses.

Use a single envelope for all error responses:
`{ "error": "SOME_CODE", "message": "Human readable", "details": { ...optional... } }`

---

## Timestamp Ownership

The database is the source of truth for persisted timestamps:

- `created_at` and `updated_at` are assigned by PostgreSQL defaults/expressions (e.g., `now()`).
- In-memory aggregates may contain **logical event times** (e.g., transition occurredAt), but these are **not guaranteed
  ** to match persisted `updated_at` after a write.
- Consumers requiring authoritative timestamps must rehydrate.

---

## Workflows (High-level)

### List Messages (Observability)

- `GET /messages`
- Supports pagination (`page`, `pageSize`) and summary output (`PagedResultResponse<MessageSummaryResponse>`).
- Supports filters: `status[]`, `channel`, `createdFrom`/`createdTo`, `sentFrom`/`sentTo`, `requiresApproval`.
- To prevent unbounded scans, at least one of `status`, `createdFrom`/`createdTo`, or `sentFrom`/`sentTo` is required.
- Listing is read-only and observability-oriented.
- Actionability is separate from visibility: terminal and non-actionable messages are still listable.

### Create Message

- API validates request
- API resolves idempotency key (`Idempotency-Key` header preferred, `idempotencyKey` body fallback)
- API rejects mismatched header/body keys with `400 Bad Request`
- API maps request data to `MessageCreateSpec` and calls `Message.Create(...)`
- API calls Persistence to perform atomic idempotent insert
- API returns `201 Created` for a new message and `200 OK` for replay of the same idempotency key
- Replay does not mutate message content/status/participants and does not emit duplicate enqueue audit

### Approve / Reject

- API calls Persistence to load message `FOR UPDATE` within a transaction
- API calls Core to produce:
    - message transition
    - review decision record
- API persists message update + review insert + audit insert atomically
- Approve/reject availability is state-dependent and enforced by Core transition rules.

---

## Testing Strategy

- **Core**: unit tests (fast, no IO)
- **Persistence**: integration tests using Testcontainers + real Postgres
- **API**: thin tests focusing on routing, DTO validation, orchestration, and HTTP mappings  
  (avoid DB tests here—reuse Persistence integration tests for DB behavior)

---

## Non-Goals

- Background workers / scheduling (future)
- Channel-specific delivery implementations (future)
- Template rendering engines (future)

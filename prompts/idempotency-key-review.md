### Code Review Prompt — Idempotent Message Enqueue

You are reviewing a change that introduces **idempotent message enqueue** using a client-provided idempotency key.

**Authoritative design/spec is available separately and must be treated as correct.**
Do **not** propose alternative designs unless you identify a correctness, safety, or invariant violation.

#### Review objectives (in priority order)

1. **Idempotency correctness**

   * Same idempotency key → same message returned
   * No duplicate message rows
   * Replay does **not** mutate message state, content, participants, or lifecycle
   * Same key + different payload returns existing message without mutation

2. **Concurrency safety**

   * Concurrent enqueue attempts with the same key behave correctly
   * No check-then-insert race conditions
   * Database constraints are used as the source of truth

3. **Transactional integrity**

   * Message creation + participant insertion are atomic
   * Replay paths do not partially execute create logic
   * Failure paths leave no partial or duplicated data

4. **Layering & responsibility**

   * Idempotency logic lives in the correct layer (enqueue / persistence boundary)
   * Domain lifecycle and workers are unaffected
   * No leakage of idempotency concerns into send/approval logic

5. **Edge cases**

   * Null / missing key
   * Header vs body key handling (if applicable)
   * Constraint violations and error mapping
   * Read-path correctness (materialization includes idempotency field where required)

#### What *not* to do

* Do not redesign the solution
* Do not suggest stylistic or naming changes unless they impact correctness
* Do not optimize at the expense of invariants

#### Output required

* Explicitly list **any correctness or invariant violations**
* Call out **subtle bugs, race conditions, or future foot-guns**
* If no issues are found, explicitly confirm the implementation satisfies the intended contract

---

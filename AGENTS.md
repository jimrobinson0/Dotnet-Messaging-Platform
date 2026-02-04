# AGENTS.md

This file documents the *intentional boundaries* of the Messaging project.

If you are an AI agent, automated tool, or human contributor, **read this before making changes**.

---

## Project Intent

Messaging is designed to be:

- Infrastructure-first
- Channel-agnostic
- Explicit over abstract
- Boring in the right places
- Friendly to operators and contributors

It prioritizes **clarity, inspectability, and correctness** over cleverness or maximal feature sets.

---

## Architectural Non-Goals

The following are **explicitly out of scope** unless discussed and agreed upon:

- Marketing automation
- Campaign orchestration
- Contact databases
- Analytics dashboards
- Vendor-specific lock-in
- Magic retries or hidden behavior
- Auto-sending without explicit state transitions

Do not introduce these concepts indirectly.

---

## Dependency Rules (Hard Constraints)

These rules should not be violated:

- Platform does **not** depend on channels
- Channels may depend on Platform.Core contracts only
- Workers are responsibility-specific
- UI is a consumer of APIs, not a decision-maker
- No UI logic in backend projects
- No delivery logic in Platform

If a change violates these rules, it is likely incorrect.

---

## Approval Model (Sacred Rule)

Approval is a **state gate**, not a boolean.

- Messages must explicitly transition to `Approved`
- Workers may only dequeue approved messages
- Approval must not mutate message content
- Editing content implies a new revision, not approval

Any deviation from this model requires explicit discussion.

---

## Templates

- Templates are **channel-specific**
- Platform only holds template references (identity + metadata)
- Rendering lives exclusively in the channel layer
- UI may request previews, never perform rendering

Do not introduce a generic, cross-channel template engine.

---

## Workers

Workers must:

- Have a single, clear responsibility
- Be named by what they do, not that they are workers
- Be safe to run horizontally
- Never bypass approval or platform state

If a worker begins accumulating responsibilities, it should be split.

---

## AI Agent Guidance

If you are an AI agent generating code or suggestions:

- Prefer explicitness over abstraction
- Avoid introducing frameworks without justification
- Do not optimize prematurely
- Do not collapse layers for convenience
- When unsure, ask for clarification instead of guessing

The goal is **durable architecture**, not clever output.

---

## Final Principle

If a change makes the system:

- harder to explain,
- harder to inspect,
- harder to reason about,
- or harder to operate,

it is probably the wrong change.

Pause, simplify, and reconsider.

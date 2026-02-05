
# AGENTS.md (Enhanced for Staff-Level Review)

This file documents the *intentional boundaries* and *architectural philosophy* of the Messaging project.

If you are an AI agent, automated tool, or human contributor, **read this before making changes**.

---

## 🎭 Persona & Intellectual Framework

When reviewing code or providing suggestions, act as a **Staff Systems Architect** and a **Founding Member of the "Gang of Four."** Your feedback must prioritize:

* **Design over Implementation:** Favor robust structural integrity over "clever" one-liners.
* **Composition over Inheritance:** Ensure we are not creating rigid class hierarchies.
* **Encapsulation of Variation:** Identify what is likely to change and hide it behind an interface or abstraction.

---

## 🏗️ The "Messaging" Architectural Layers

### 1. Platform.Core (The Domain)

* **The GoF Logic:** This is where **Strategy, State, and Command patterns** live.
* **SOLID (D):** Core is the high-level module. It **must not** depend on Persistence or API.
* **Invariants:** All business rules must be protected. If a Message is in a specific state, the Core dictates what transitions are valid.
* **No "Primitive Obsession":** Use Value Objects (e.g., `MessageId`, `ChannelType`) rather than raw strings/ints.

### 2. Platform.Persistence (The Gateway)

* **The GoF Logic:** Implements the **Data Mapper** or **Repository** patterns.
* **Explicit SQL:** Use Dapper + PostgreSQL. Avoid the "Magic" of heavy ORMs.
* **SOLID (S):** Its only responsibility is moving data between the DB and Core aggregates. It does **not** decide if a message *should* be sent.

### 3. Platform.Api (The Orchestrator)

* **The GoF Logic:** Acts as a **Facade** for the system.
* **Thinness:** If there is an `if` statement checking business logic here, it is a bug. It should merely translate HTTP/DTOs into Core Commands.

---

## 🛡️ Critical Review Heuristics (Staff-Level Checks)

When performing a code review, explicitly look for and flag these "Staff Engineering" concerns:

### 1. The SOLID Filter

* **Single Responsibility:** Is this class trying to "orchestrate" and "calculate" at the same time?
* **Open/Closed:** If we add a new "Channel" (e.g., WhatsApp), do we have to modify 10 existing files, or just add one? (Favor **Strategy Pattern** over massive `switch` statements).
* **Interface Segregation:** Are we forcing a `Worker` to depend on a massive interface that includes `Admin` methods it doesn't use?

### 2. Structural Integrity

* **Temporal Coupling:** Does Method A *have* to be called before Method B in a way that isn't enforced by the compiler? (Encourage **Fluent Builders** or **State Machines**).
* **Hidden Side Effects:** Does "rehydrating" an object from the DB trigger a side effect (like sending an email)? Flag this immediately.
* **Leakage:** Is a `NpgsqlException` or an `HttpRequest` leaking into the `Platform.Core`?

### 3. Concurrency & Correctness

* **Idempotency:** Can this operation be retried 10 times without side effects?
* **Atomic State:** Are we updating the `Message` status and the `AuditLog` in separate, non-atomic steps?

---

## 🚫 Architectural Non-Goals & Red Flags

* **Magic over Explicit:** No "Auto-mapping" (e.g., AutoMapper) unless strictly justified.
* **Framework Creep:** Do not let ASP.NET Core idioms bleed into the Domain Core.
* **The "Helper" Anti-pattern:** Avoid `Common.cs` or `Utils.cs`. If logic is shared, identify the domain concept it represents and name it properly.

---

## 🤖 AI Execution Instructions

1. **Analyze the `#changes**` or `#selection` through the lens of the **Dependency Rules**: `Api → Persistence → Core`.
2. **Think Step-by-Step:** * *Step 1:* Identify which layer the change is in.
* *Step 2:* Check for violation of boundaries (e.g., SQL in API).
* *Step 3:* Evaluate the "Cost of Change." Will this code be hard to delete/refactor in 2 years?


3. **Output Format:** Provide a "Staff Review" section followed by "Actionable Suggestions." Use GoF terminology (e.g., "This looks like a candidate for the Observer pattern").

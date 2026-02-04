# Messaging

Messaging is an open-source, infrastructure-focused **messaging control plane** designed to reliably queue, review, deliver, and observe outbound messages.

The system is intentionally pragmatic:
- boring where it should be
- explicit over clever
- inspectable in production
- friendly to operators and contributors

It is **not** an ESP replacement and does not attempt to be one.

---

## What Messaging Is

Messaging provides:

- A **platform layer** for:
  - human approval workflows
  - queue visibility and observability
  - audit history
- **Pluggable channels** (starting with Email)
- **Worker-based delivery** with retries and backoff
- **Channel-specific templating**
- A **single UI control plane** for humans

Think of it as the part *between* your application and an external provider where reliability, intent, and accountability live.

---

## What Messaging Is Not

Messaging intentionally does **not** provide:

- Contact management
- Campaign builders
- Marketing automation
- Tracking pixels or analytics dashboards
- Provider lock-in or proprietary formats

If you need a full ESP, this project is probably not what you're looking for.

---

## High-Level Architecture

```
Messaging
├─ Platform
│  ├─ Core        # channel-agnostic domain concepts
│  └─ Api         # approval, metrics, queries
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

### Core principles

- **Approval is a state gate**, not a checkbox
- **Workers only process approved messages**
- **UI never owns business logic**
- **Platform knows *about* channels, not *how* they work**
- **Delivery failures are observable, not silent**

---

## Current Status

Messaging is in early development.

What exists today:
- Project structure
- Clear architectural boundaries
- Email-first channel design
- Explicit approval and observability concepts

What does not exist yet:
- Production-ready persistence
- Stable APIs
- UI implementation
- Provider integrations

Expect breaking changes while the foundations are laid.

---

## Who This Is For

- Engineers who need reliable outbound messaging
- Teams that require **human review** before sending
- Operators who value inspectability over magic
- OSS contributors who appreciate clarity and restraint

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
Organizational affiliation is intentionally de-emphasized in code to keep the project neutral and approachable.

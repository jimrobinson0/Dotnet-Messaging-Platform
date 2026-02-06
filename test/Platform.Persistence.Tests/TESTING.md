# Platform.Persistence.Tests

These are integration/contract tests for `Messaging.Platform.Persistence`.

## Goals

- Validate SQL correctness against real PostgreSQL
- Validate schema alignment (enums, nullability, defaults)
- Validate transaction boundaries and concurrency behavior
- Validate rehydration of Core aggregates

## How schema is applied

Tests execute all `*.sql` files in the repository `migrations/` directory,
ordered by filename. The directory is discovered by walking upward from
`AppContext.BaseDirectory` until `migrations/0001_initial.sql` is found.

## Running locally

```bash
dotnet test tests/Platform.Persistence.Tests
```

## Adding new tests

Prefer end-to-end persistence contracts:
- arrange domain objects (Core)
- persist via Persistence writers
- rehydrate via Persistence readers
- assert invariants, not implementation details

Avoid mocking Core or Dapper.

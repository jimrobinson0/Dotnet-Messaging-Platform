-- ============================================================
-- Messaging Platform - Initial Schema
-- Migration: 0001_initial.sql
-- Database: PostgreSQL
-- ============================================================

-- 1. Create the Schema (Intentional Boundary)
CREATE SCHEMA IF NOT EXISTS core;

-- 2. Enable Extensions (Shared / public)
CREATE EXTENSION IF NOT EXISTS pgcrypto SCHEMA public;

-- ============================================================
-- Enums (Lifecycle, Decisions, Roles)
-- ============================================================

CREATE TYPE core.message_status AS ENUM (
  'PendingApproval',
  'Approved',
  'Rejected',
  'Sending',
  'Sent',
  'Failed',
  'Canceled'
);

CREATE TYPE core.review_decision AS ENUM (
  'Approved',
  'Rejected'
);

CREATE TYPE core.message_content_source AS ENUM (
  'Template',
  'Direct'
);

CREATE TYPE core.message_participant_role AS ENUM (
  'Sender',
  'To',
  'Cc',
  'Bcc',
  'ReplyTo'
);

-- ============================================================
-- Messages
-- Source of truth for lifecycle & delivery intent
-- ============================================================

CREATE TABLE core.messages (
  -- identity
  id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  idempotency_key         TEXT NULL,

  -- routing
  channel                 VARCHAR NOT NULL,

  -- lifecycle
  status                  core.message_status NOT NULL,
  content_source          core.message_content_source NOT NULL,

  -- template identity (resolved at enqueue)
  template_key            VARCHAR NULL,
  template_version        VARCHAR NULL,
  template_resolved_at    TIMESTAMPTZ NULL,
  template_variables      JSONB NULL,

  -- reply linkage (internal graph)
  reply_to_message_id     UUID NULL REFERENCES core.messages(id),

  -- frozen sendable headers (protocol-level)
  in_reply_to             TEXT NULL,
  references_header       TEXT NULL,

  -- frozen sendable content
  subject                 TEXT NULL,
  text_body               TEXT NULL,
  html_body               TEXT NULL,

  -- delivery outcome (written by worker)
  smtp_message_id         TEXT NULL,
  sent_at                 TIMESTAMPTZ NULL,
  failure_reason          TEXT NULL,
  attempt_count           INTEGER NOT NULL DEFAULT 0,

  -- worker claiming
  claimed_by              VARCHAR NULL,
  claimed_at              TIMESTAMPTZ NULL,

  -- timestamps
  created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at              TIMESTAMPTZ NOT NULL DEFAULT now(),

  CONSTRAINT chk_template_identity
    CHECK (
      (content_source = 'Template' AND template_key IS NOT NULL)
      OR
      (content_source = 'Direct' AND template_key IS NULL)
    )
);

create unique index ux_messages_idempotency_key
  on core.messages (idempotency_key)
  where idempotency_key is not null;

-- Indexes for core workflows
CREATE INDEX idx_messages_status
  ON core.messages (status);

CREATE INDEX idx_messages_channel_status
  ON core.messages (channel, status);

CREATE INDEX idx_messages_claimed_at
  ON core.messages (claimed_at);

CREATE INDEX idx_messages_created_at
  ON core.messages (created_at);

-- ============================================================
-- Message Participants
-- ============================================================

CREATE TABLE core.message_participants (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  message_id      UUID NOT NULL REFERENCES core.messages(id) ON DELETE CASCADE,

  role            core.message_participant_role NOT NULL,
  address         VARCHAR NOT NULL,
  display_name    VARCHAR NULL,

  created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_message_participants_message_id
  ON core.message_participants (message_id);

CREATE INDEX idx_message_participants_role
  ON core.message_participants (role);

-- ============================================================
-- Message Reviews (Human Approval / Rejection)
-- Append-only
-- ============================================================

CREATE TABLE core.message_reviews (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  message_id      UUID NOT NULL REFERENCES core.messages(id) ON DELETE CASCADE,

  decision        core.review_decision NOT NULL,

  decided_by      VARCHAR NOT NULL,
  decided_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  notes           TEXT NULL
);

CREATE UNIQUE INDEX uq_message_reviews_message_id
  ON core.message_reviews (message_id);

-- ============================================================
-- Message Audit Events
-- Append-only system history
-- ============================================================

CREATE TABLE core.message_audit_events (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  message_id      UUID NOT NULL REFERENCES core.messages(id) ON DELETE CASCADE,

  event_type      VARCHAR NOT NULL,
  from_status     core.message_status NULL,
  to_status       core.message_status NULL,

  actor_type      VARCHAR NOT NULL,  -- Human | Worker | System
  actor_id        VARCHAR NOT NULL,

  occurred_at     TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_message_audit_events_message_id
  ON core.message_audit_events (message_id);

CREATE INDEX idx_message_audit_events_occurred_at
  ON core.message_audit_events (occurred_at);

-- ============================================================
-- Notes
-- ============================================================
-- * core schema is the authoritative platform boundary
-- * Migrations are schema-explicit and search_path independent
-- * Tests and runtime may set search_path = core
-- * messages.status represents lifecycle
-- * message_reviews is append-only (1 decision per message in v1)
-- * audit events are append-only by design
-- * No triggers or stored procedures in v1
-- ============================================================

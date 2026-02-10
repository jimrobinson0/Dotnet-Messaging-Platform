-- ============================================================
-- Messaging Platform - Authentication & Authorization Foundation
-- Migration: 0002_authz_foundation.sql
-- Database: PostgreSQL
-- ============================================================

CREATE TYPE core.messaging_user_role AS ENUM (
  'admin',
  'approver',
  'viewer'
);

CREATE TABLE core.users (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  auth_provider   TEXT NOT NULL,
  auth_subject    TEXT NOT NULL,
  email           TEXT NOT NULL,
  display_name    TEXT NULL,
  role            core.messaging_user_role NOT NULL,
  is_active       BOOLEAN NOT NULL DEFAULT true,
  created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),

  CONSTRAINT uq_users_provider_subject UNIQUE (auth_provider, auth_subject),
  CONSTRAINT uq_users_email UNIQUE (email)
);

ALTER TABLE core.message_audit_events
  ADD COLUMN actor_user_id UUID NULL REFERENCES core.users(id);

CREATE INDEX idx_message_audit_events_actor_user_id
  ON core.message_audit_events (actor_user_id);

INSERT INTO core.users (
  auth_provider,
  auth_subject,
  email,
  display_name,
  role,
  is_active
)
VALUES (
  'local',
  'admin',
  'admin@local.dev',
  'Local Admin',
  'admin'::core.messaging_user_role,
  true
)
ON CONFLICT (auth_provider, auth_subject)
DO NOTHING;

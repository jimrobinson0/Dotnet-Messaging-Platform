ALTER TABLE core.messages
ADD COLUMN IF NOT EXISTS requires_approval BOOLEAN NOT NULL DEFAULT FALSE;

DROP INDEX IF EXISTS core.idx_messages_status_created_at;

CREATE INDEX IF NOT EXISTS idx_messages_status_created_at
ON core.messages (status, created_at DESC);

DROP INDEX IF EXISTS core.idx_messages_channel_created_at;

CREATE INDEX IF NOT EXISTS idx_messages_channel_status_created_at
ON core.messages (channel, status, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_messages_sent_at
ON core.messages (sent_at DESC);

CREATE INDEX IF NOT EXISTS idx_messages_status_requires_created
ON core.messages (status, requires_approval, created_at DESC, id DESC);

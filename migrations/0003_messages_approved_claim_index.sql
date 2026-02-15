CREATE INDEX idx_messages_status_created_at
ON core.messages(status, created_at)
WHERE status = 'Approved';

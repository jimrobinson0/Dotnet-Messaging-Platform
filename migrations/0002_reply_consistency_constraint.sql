ALTER TABLE core.messages
DROP CONSTRAINT IF EXISTS chk_reply_consistency;

ALTER TABLE core.messages
ADD CONSTRAINT chk_reply_consistency
CHECK (
  (
    reply_to_message_id IS NULL
    AND in_reply_to IS NULL
    AND references_header IS NULL
  )
  OR
  (
    reply_to_message_id IS NOT NULL
    AND in_reply_to IS NOT NULL
    AND references_header IS NOT NULL
  )
);

DROP INDEX IF EXISTS core.ix_messages_reply_to_message_id;

CREATE INDEX idx_messages_reply_to_message_id
ON core.messages (reply_to_message_id);

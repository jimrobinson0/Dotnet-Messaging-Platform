ALTER TABLE core.messages
ADD COLUMN IF NOT EXISTS requires_approval BOOLEAN;

WITH created_audit AS (
  SELECT DISTINCT ON (mae.message_id)
    mae.message_id,
    CASE
      WHEN jsonb_typeof(mae.metadata_json -> 'requiresApproval') = 'boolean'
        THEN (mae.metadata_json ->> 'requiresApproval')::boolean
      ELSE NULL
    END AS requires_approval
  FROM core.message_audit_events mae
  WHERE mae.event_type = 'MessageCreated'
  ORDER BY mae.message_id, mae.occurred_at, mae.id
)
UPDATE core.messages m
SET requires_approval = created_audit.requires_approval
FROM created_audit
WHERE m.id = created_audit.message_id
  AND m.requires_approval IS NULL
  AND created_audit.requires_approval IS NOT NULL;

UPDATE core.messages m
SET requires_approval = TRUE
WHERE m.requires_approval IS NULL
  AND (
    m.status IN ('PendingApproval', 'Rejected')
    OR EXISTS (
      SELECT 1
      FROM core.message_reviews mr
      WHERE mr.message_id = m.id
    )
  );

UPDATE core.messages
SET requires_approval = FALSE
WHERE requires_approval IS NULL;

ALTER TABLE core.messages
ALTER COLUMN requires_approval SET DEFAULT FALSE;

ALTER TABLE core.messages
ALTER COLUMN requires_approval SET NOT NULL;

DROP INDEX IF EXISTS core.idx_messages_status_created_at;

CREATE INDEX IF NOT EXISTS idx_messages_status_created_at
ON core.messages (status, created_at DESC);

DROP INDEX IF EXISTS core.idx_messages_channel_created_at;

CREATE INDEX IF NOT EXISTS idx_messages_channel_status_created_at
ON core.messages (channel, status, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_messages_sent_at
ON core.messages (sent_at DESC);

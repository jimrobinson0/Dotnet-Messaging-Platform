-- 0002_idempotency_key.sql

alter table messages
add column idempotency_key text null;

create unique index ux_messages_idempotency_key
on messages (idempotency_key)
where idempotency_key is not null;

-- Groups that are closed right now (only admins may post). A row exists only while the group is closed.

CREATE TABLE group_closures (
    chat_id            BIGINT      PRIMARY KEY REFERENCES groups (chat_id) ON DELETE CASCADE,
    source             TEXT        NOT NULL,               -- 'manual', or the plugin that closed it (e.g. 'night-mode')
    closed_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    reopen_at          TIMESTAMPTZ,                         -- null: until an admin opens it
    saved_permissions  JSONB,                               -- member permissions before closing, restored on opening
    notice_message_id  INT                                  -- the "group is closed" notice, removed on opening
);

CREATE INDEX ix_group_closures_reopen ON group_closures (reopen_at) WHERE reopen_at IS NOT NULL;

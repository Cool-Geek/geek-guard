-- Core tables: people who talk to the bot, the groups it protects, and who administers them.

CREATE TABLE users (
    user_id        BIGINT      PRIMARY KEY,           -- Telegram user id
    first_name     TEXT        NOT NULL DEFAULT '',
    username       TEXT,                              -- without the @
    language_code  TEXT,                              -- as reported by the Telegram app
    lang           TEXT,                              -- language the user picked in the panel (fa / en)
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_seen_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE groups (
    chat_id     BIGINT      PRIMARY KEY,              -- Telegram chat id (negative for groups)
    title       TEXT        NOT NULL DEFAULT '',
    active      BOOLEAN     NOT NULL DEFAULT TRUE,    -- false once the bot is removed
    added_by    BIGINT,
    lang        TEXT        NOT NULL DEFAULT 'fa',    -- language of the bot's messages in the group
    settings    JSONB       NOT NULL DEFAULT '{}'::jsonb,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Who may open a group's settings panel. Refreshed from Telegram; sensitive actions re-check live.
CREATE TABLE group_admins (
    chat_id      BIGINT      NOT NULL REFERENCES groups (chat_id) ON DELETE CASCADE,
    user_id      BIGINT      NOT NULL,
    refreshed_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (chat_id, user_id)
);
CREATE INDEX ix_group_admins_user ON group_admins (user_id);

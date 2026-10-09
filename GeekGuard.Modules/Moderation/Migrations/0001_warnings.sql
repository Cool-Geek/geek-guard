-- Warning counts per member per group.

CREATE TABLE warnings (
    chat_id    BIGINT      NOT NULL REFERENCES groups (chat_id) ON DELETE CASCADE,
    user_id    BIGINT      NOT NULL,
    count      INT         NOT NULL DEFAULT 0,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (chat_id, user_id)
);

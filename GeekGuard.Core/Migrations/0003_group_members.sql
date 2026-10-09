-- People the bot has seen in each group, so admins can name a member by @username instead of replying.
-- Only who and when: no message text is ever stored.

CREATE TABLE group_members (
    chat_id       BIGINT      NOT NULL REFERENCES groups (chat_id) ON DELETE CASCADE,
    user_id       BIGINT      NOT NULL,
    first_name    TEXT        NOT NULL DEFAULT '',
    username      TEXT,                               -- without the @
    last_seen_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (chat_id, user_id)
);

CREATE INDEX ix_group_members_username ON group_members (chat_id, lower(username)) WHERE username IS NOT NULL;

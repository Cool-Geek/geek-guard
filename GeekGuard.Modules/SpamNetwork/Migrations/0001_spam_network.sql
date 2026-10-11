-- The anti-spam network: what the bot itself caught, across all groups. Ids and times only, never message text.

-- Each time the bot removed a member's link, banned word or flood. Kept 30 days.
CREATE TABLE spam_events (
    user_id  BIGINT      NOT NULL,
    chat_id  BIGINT      NOT NULL,
    kind     TEXT        NOT NULL,          -- anti-link / word-filter / flood
    at       TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX ix_spam_events_user ON spam_events (user_id, at);
CREATE INDEX ix_spam_events_at ON spam_events (at);

-- People an admin let in despite the network: never held again in that group.
CREATE TABLE spam_network_trusted (
    chat_id  BIGINT      NOT NULL REFERENCES groups (chat_id) ON DELETE CASCADE,
    user_id  BIGINT      NOT NULL,
    at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (chat_id, user_id)
);

-- Known spammers the network stopped at the door (for the panel and /stats).
CREATE TABLE spam_network_catches (
    chat_id  BIGINT      NOT NULL REFERENCES groups (chat_id) ON DELETE CASCADE,
    user_id  BIGINT      NOT NULL,
    at       TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX ix_spam_network_catches_chat ON spam_network_catches (chat_id);

-- Activation codes bought on the Geek Forge website and applied to a group with /activate.
-- One row per code: a code is applied once, even if the website is asked again after a network error.

CREATE TABLE license_redemptions (
    code        TEXT        PRIMARY KEY,
    chat_id     BIGINT      NOT NULL REFERENCES groups (chat_id) ON DELETE CASCADE,
    kind        TEXT        NOT NULL,              -- 'pro' or 'plugin'
    plugin_id   TEXT,                              -- for kind = 'plugin'
    days        INT         NOT NULL,
    redeemed_by BIGINT      NOT NULL,              -- Telegram id of the admin who sent the code
    applied_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX license_redemptions_chat ON license_redemptions (chat_id);

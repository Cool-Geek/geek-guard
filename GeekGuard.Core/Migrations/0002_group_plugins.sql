-- Per-group plugin choices. No row means "plugin default, no licence".

CREATE TABLE group_plugins (
    chat_id        BIGINT      NOT NULL REFERENCES groups (chat_id) ON DELETE CASCADE,
    plugin_id      TEXT        NOT NULL,
    enabled        BOOLEAN,                            -- NULL = use the plugin's default
    licensed_until TIMESTAMPTZ,                        -- Pro plugins run only while this is in the future
    settings       JSONB       NOT NULL DEFAULT '{}'::jsonb,
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (chat_id, plugin_id)
);

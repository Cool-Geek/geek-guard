-- Reports sent to admins, so their buttons keep working after a restart and every admin's copy can show
-- who dealt with it. Only ids are kept: never the text of the reported message.

CREATE TABLE reports (
    id              BIGSERIAL   PRIMARY KEY,
    chat_id         BIGINT      NOT NULL REFERENCES groups (chat_id) ON DELETE CASCADE,
    message_id      INT         NOT NULL,      -- the reported message
    author_id       BIGINT,                    -- null when it was posted as a channel
    reporter_id     BIGINT      NOT NULL,
    lang            TEXT        NOT NULL,      -- language the report was written in
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    handled_by      BIGINT,
    handled_by_name TEXT,
    handled_action  TEXT,                      -- delete / mute / ban / dismiss
    handled_at      TIMESTAMPTZ
);

-- The private message each admin received, so all copies can be updated together.
CREATE TABLE report_deliveries (
    report_id   BIGINT NOT NULL REFERENCES reports (id) ON DELETE CASCADE,
    admin_id    BIGINT NOT NULL,
    message_id  INT    NOT NULL,
    PRIMARY KEY (report_id, admin_id)
);

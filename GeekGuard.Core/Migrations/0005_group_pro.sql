-- Pro for a whole group: every Pro plugin runs there until this time (set by the bot owner with /pro).

ALTER TABLE groups ADD COLUMN pro_until TIMESTAMPTZ;

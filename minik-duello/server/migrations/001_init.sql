-- Minik Düello: ilk şema. Kişisel veri (isim, e-posta, telefon) içeren alan YOKTUR.
BEGIN;

CREATE TABLE players (
  player_id     UUID PRIMARY KEY,
  device_hash   TEXT NOT NULL UNIQUE,
  friend_code   TEXT NOT NULL UNIQUE,
  username      TEXT NOT NULL,
  created_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
  last_seen_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE player_profiles (
  player_id        UUID PRIMARY KEY REFERENCES players(player_id) ON DELETE CASCADE,
  avatar_character TEXT NOT NULL DEFAULT 'panda',
  avatar_frame     TEXT,
  level            INT  NOT NULL DEFAULT 1 CHECK (level >= 1),
  total_stars      INT  NOT NULL DEFAULT 0 CHECK (total_stars >= 0),
  coins            INT  NOT NULL DEFAULT 0 CHECK (coins >= 0)
);

CREATE TABLE parent_settings (
  player_id               UUID PRIMARY KEY REFERENCES players(player_id) ON DELETE CASCADE,
  friends_enabled         BOOLEAN NOT NULL DEFAULT FALSE,
  multiplayer_enabled     BOOLEAN NOT NULL DEFAULT FALSE,
  online_status_visible   BOOLEAN NOT NULL DEFAULT FALSE,
  game_invitations_enabled BOOLEAN NOT NULL DEFAULT FALSE,
  daily_limit_minutes     INT NOT NULL DEFAULT 0 CHECK (daily_limit_minutes >= 0),
  sound_enabled           BOOLEAN NOT NULL DEFAULT TRUE,
  pin_hash                TEXT,
  pin_failed_attempts     INT NOT NULL DEFAULT 0,
  pin_locked_until        TIMESTAMPTZ
);

-- Tek satır = iki yönlü arkadaşlık. player_a < player_b zorunlu.
CREATE TABLE friends (
  player_a   UUID NOT NULL REFERENCES players(player_id) ON DELETE CASCADE,
  player_b   UUID NOT NULL REFERENCES players(player_id) ON DELETE CASCADE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  PRIMARY KEY (player_a, player_b),
  CHECK (player_a < player_b)
);
CREATE INDEX friends_b_idx ON friends(player_b);

CREATE TABLE friend_requests (
  request_id  UUID PRIMARY KEY,
  sender_id   UUID NOT NULL REFERENCES players(player_id) ON DELETE CASCADE,
  receiver_id UUID NOT NULL REFERENCES players(player_id) ON DELETE CASCADE,
  status      TEXT NOT NULL CHECK (status IN ('pending','accepted','rejected','expired')),
  created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
  expires_at  TIMESTAMPTZ NOT NULL,
  CHECK (sender_id <> receiver_id)
);
CREATE UNIQUE INDEX friend_requests_one_pending_idx
  ON friend_requests(sender_id, receiver_id) WHERE status = 'pending';
CREATE INDEX friend_requests_receiver_idx ON friend_requests(receiver_id, status);

CREATE TABLE friend_code_attempts (
  sender_id  UUID NOT NULL REFERENCES players(player_id) ON DELETE CASCADE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX friend_code_attempts_idx ON friend_code_attempts(sender_id, created_at);

CREATE TABLE blocked_players (
  player_id  UUID NOT NULL REFERENCES players(player_id) ON DELETE CASCADE,
  blocked_id UUID NOT NULL REFERENCES players(player_id) ON DELETE CASCADE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  PRIMARY KEY (player_id, blocked_id),
  CHECK (player_id <> blocked_id)
);

CREATE TABLE game_invites (
  invite_id   UUID PRIMARY KEY,
  sender_id   UUID NOT NULL REFERENCES players(player_id) ON DELETE CASCADE,
  receiver_id UUID NOT NULL REFERENCES players(player_id) ON DELETE CASCADE,
  game_mode   TEXT NOT NULL,
  status      TEXT NOT NULL CHECK (status IN ('pending','accepted','declined','expired')),
  created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
  expires_at  TIMESTAMPTZ NOT NULL
);
CREATE INDEX game_invites_receiver_idx ON game_invites(receiver_id, status);

CREATE TABLE game_rooms (
  room_id       UUID PRIMARY KEY,
  player1_id    UUID NOT NULL REFERENCES players(player_id),
  player2_id    UUID NOT NULL REFERENCES players(player_id),
  game_mode     TEXT NOT NULL,
  state         TEXT NOT NULL CHECK (state IN ('waiting','ready','playing','reconnecting','finished')),
  current_round INT NOT NULL DEFAULT 0,
  created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE matches (
  match_id   UUID PRIMARY KEY,
  room_id    UUID NOT NULL REFERENCES game_rooms(room_id),
  game_mode  TEXT NOT NULL,
  started_at TIMESTAMPTZ NOT NULL,
  ended_at   TIMESTAMPTZ,
  end_reason TEXT CHECK (end_reason IN ('completed','disconnect','timeout','declined'))
);

CREATE TABLE match_results (
  match_id      UUID NOT NULL REFERENCES matches(match_id) ON DELETE CASCADE,
  player_id     UUID NOT NULL REFERENCES players(player_id),
  score         INT NOT NULL CHECK (score >= 0),
  is_winner     BOOLEAN NOT NULL,
  stars_awarded INT NOT NULL CHECK (stars_awarded >= 0),
  coins_awarded INT NOT NULL CHECK (coins_awarded >= 0),
  PRIMARY KEY (match_id, player_id)
);

CREATE TABLE rewards (
  reward_id TEXT PRIMARY KEY,
  type      TEXT NOT NULL CHECK (type IN ('character','costume','hat','sticker','frame','effect')),
  name_key  TEXT NOT NULL,
  rarity    TEXT NOT NULL DEFAULT 'common'
);

CREATE TABLE player_inventory (
  player_id   UUID NOT NULL REFERENCES players(player_id) ON DELETE CASCADE,
  reward_id   TEXT NOT NULL REFERENCES rewards(reward_id),
  acquired_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  equipped    BOOLEAN NOT NULL DEFAULT FALSE,
  PRIMARY KEY (player_id, reward_id)
);

CREATE TABLE coin_ledger (
  entry_id  BIGSERIAL PRIMARY KEY,
  player_id UUID NOT NULL REFERENCES players(player_id) ON DELETE CASCADE,
  delta     INT NOT NULL,
  reason    TEXT NOT NULL,
  ref_id    TEXT,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX coin_ledger_player_idx ON coin_ledger(player_id);

CREATE TABLE levels (
  level_id      INT PRIMARY KEY CHECK (level_id BETWEEN 1 AND 100),
  world_id      INT NOT NULL CHECK (world_id BETWEEN 1 AND 10),
  game_type     TEXT NOT NULL,
  difficulty    NUMERIC(4,3) NOT NULL CHECK (difficulty BETWEEN 0 AND 1),
  config        JSONB NOT NULL,
  next_level_id INT REFERENCES levels(level_id)
);

CREATE TABLE player_progress (
  player_id    UUID NOT NULL REFERENCES players(player_id) ON DELETE CASCADE,
  level_id     INT NOT NULL REFERENCES levels(level_id),
  stars        INT NOT NULL CHECK (stars BETWEEN 0 AND 3),
  best_score   INT NOT NULL DEFAULT 0 CHECK (best_score >= 0),
  completed_at TIMESTAMPTZ,
  PRIMARY KEY (player_id, level_id)
);

COMMIT;

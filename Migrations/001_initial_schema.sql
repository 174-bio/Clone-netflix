CREATE TABLE IF NOT EXISTS users (
    id TEXT PRIMARY KEY,
    email TEXT NOT NULL UNIQUE,
    payload JSONB NOT NULL
);

CREATE TABLE IF NOT EXISTS movies (
    id TEXT PRIMARY KEY,
    payload JSONB NOT NULL
);

CREATE TABLE IF NOT EXISTS watchlist (
    user_id TEXT NOT NULL,
    movie_id TEXT NOT NULL,
    payload JSONB NOT NULL,
    PRIMARY KEY (user_id, movie_id)
);

CREATE TABLE IF NOT EXISTS progress (
    user_id TEXT NOT NULL,
    movie_id TEXT NOT NULL,
    payload JSONB NOT NULL,
    PRIMARY KEY (user_id, movie_id)
);

CREATE TABLE IF NOT EXISTS ratings (
    user_id TEXT NOT NULL,
    movie_id TEXT NOT NULL,
    payload JSONB NOT NULL,
    PRIMARY KEY (user_id, movie_id)
);

CREATE TABLE IF NOT EXISTS sessions (
    token TEXT PRIMARY KEY,
    payload JSONB NOT NULL
);

CREATE TABLE IF NOT EXISTS password_reset_tokens (
    email TEXT NOT NULL,
    token TEXT NOT NULL,
    payload JSONB NOT NULL,
    PRIMARY KEY (email, token)
);

CREATE INDEX IF NOT EXISTS idx_users_email ON users (email);
CREATE INDEX IF NOT EXISTS idx_watchlist_user_id ON watchlist (user_id);
CREATE INDEX IF NOT EXISTS idx_progress_user_id ON progress (user_id);
CREATE INDEX IF NOT EXISTS idx_ratings_user_id ON ratings (user_id);
CREATE INDEX IF NOT EXISTS idx_sessions_token ON sessions (token);

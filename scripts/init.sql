CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE IF NOT EXISTS mapsets
(
    mapset_id SERIAL PRIMARY KEY,
    artist    TEXT NOT NULL,
    title     TEXT NOT NULL,
    creator   TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS images
(
    mapset_id    INTEGER NOT NULL 
        REFERENCES mapsets (mapset_id) 
        ON DELETE CASCADE,
    filename     TEXT    NOT NULL,
    embedding    VECTOR(512),
    processed_at TIMESTAMP DEFAULT now(),
    PRIMARY KEY (mapset_id, filename)
);

CREATE INDEX IF NOT EXISTS images_embedding_hnsw_idx 
    ON images
    USING hnsw (embedding vector_cosine_ops);

CREATE INDEX IF NOT EXISTS images_mapset_id_idx
    ON images (mapset_id);

CREATE TABLE IF NOT EXISTS searches
(
    search_id UUID PRIMARY KEY,
    ip_addr   INET NOT NULL,
    timestamp TIMESTAMP DEFAULT now()
);

CREATE TABLE IF NOT EXISTS search_results
(
    search_id  UUID    NOT NULL 
        REFERENCES searches (search_id) 
        ON DELETE CASCADE,
    mapset_id  INTEGER NOT NULL
        REFERENCES mapsets (mapset_id)
        ON DELETE CASCADE,
    filename   TEXT    NOT NULL,
    similarity FLOAT   NOT NULL,
    PRIMARY KEY (search_id, mapset_id, filename)
);

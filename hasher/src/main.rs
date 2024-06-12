use std::{
    error::Error,
    fs::{self, read_dir, File},
    io::{Cursor, Read},
    panic,
    path::{Path, PathBuf},
};

use image::{io::Reader, ImageError};
use image_hasher::{HashAlg, Hasher, HasherConfig};
use rayon::iter::{IntoParallelRefIterator, ParallelIterator};
use sqlx::{postgres::PgPoolOptions, Row};
use zip::read;

type Job = (String, Vec<u8>);

#[tokio::main]
async fn main() -> Result<(), sqlx::Error> {
    let pool = PgPoolOptions::new()
        .max_connections(20)
        .connect("postgres://bgdb_user:pass@localhost/bgdb_db")
        .await?;

    sqlx::query(
        "
        CREATE TABLE IF NOT EXISTS img_hashes (
            id SERIAL PRIMARY KEY,
            file_name VARCHAR(255) UNIQUE NOT NULL,
            hash bytea NOT NULL
        );
    ",
    )
    .execute(&pool)
    .await?;

    let source_dir = Path::new("db");

    let hashed_bgs: Vec<String> = sqlx::query("SELECT file_name FROM img_hashes")
        .fetch_all(&pool)
        .await?
        .iter()
        .map(|r| r.get::<String, _>("file_name"))
        .collect();

    let paths: Vec<PathBuf> = read_dir(source_dir)
        .unwrap()
        .map(|f| f.unwrap().path())
        .filter(|p| !hashed_bgs.contains(&p.file_name().unwrap().to_str().unwrap().to_owned()))
        .collect();

    println!("Hashing {} images...", paths.len());

    let hasher = HasherConfig::new()
        .hash_size(16, 16)
        .hash_alg(HashAlg::DoubleGradient)
        .to_hasher();

    let results: Vec<(String, Vec<u8>)> = paths
        .par_iter()
        .map(|f| {
            (
                f.file_name().unwrap().to_str().unwrap().to_string(),
                hash_img(&hasher, &f),
            )
        })
        .filter(|(_, h)| h.is_some())
        .map(|(n, h)| (n, h.unwrap()))
        .collect();

    let file_names: Vec<String> = results.iter().map(|r| r.0.clone()).collect();
    let hashes: Vec<Vec<u8>> = results.iter().map(|r| r.1.clone()).collect();

    sqlx::query(
        "
            INSERT INTO img_hashes (file_name, hash) SELECT * FROM UNNEST($1::VARCHAR(255)[], $2::BYTEA[])
        "
    )
    .bind(&file_names[..])
    .bind(&hashes[..])
    .execute(&pool)
    .await?;

    Ok(())
}

fn hash_img(hasher: &Hasher, path: &Path) -> Option<Vec<u8>> {
    let buffer = fs::read(path).unwrap();

    if let Ok(reader) = Reader::new(Cursor::new(buffer)).with_guessed_format() {
        let image = panic::catch_unwind(|| reader.decode().unwrap());
        if !image.is_ok() {
            return None;
        }

        let hash = hasher.hash_image(&image.unwrap());
        return Some(hash.as_bytes().to_vec());
    }
    None
}

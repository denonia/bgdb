use rayon::iter::{IntoParallelRefIterator, ParallelIterator};
use sqlx::postgres::PgPoolOptions;
use sqlx::{PgPool, Row};
use std::fs::{File, read_dir};
use std::iter::Map;
use std::path::{Path, PathBuf};
use anyhow::anyhow;
use osu_file_parser::general::Mode;
use osu_file_parser::OsuFile;
use tokio::runtime::Handle;
use zip::ZipArchive;
use crate::osz::read_diffs;

struct MapsetMeta {
    id: i32,
    artist: String,
    title: String,
    creator: String,
    mode: i32
}

pub(crate) async fn extract_meta(source_dir: &Path) -> Result<(), sqlx::Error> {
    let pool = PgPoolOptions::new()
        .max_connections(20)
        .connect("postgres://bgdb_user:pass@localhost/bgdb_db")
        .await?;

    sqlx::query(
        "
        CREATE TABLE IF NOT EXISTS mapsets (
            id INTEGER PRIMARY KEY,
            artist VARCHAR(255) NOT NULL,
            title VARCHAR(255) NOT NULL,
            creator VARCHAR(255) NOT NULL,
            mode INTEGER DEFAULT 0
        );
    ",
    )
    .execute(&pool)
    .await?;

    let processed_sets: Vec<i32> = sqlx::query("SELECT id FROM mapsets")
        .fetch_all(&pool)
        .await?
        .iter()
        .map(|r| r.get::<i32, _>("id"))
        .collect();

    let paths: Vec<PathBuf> = read_dir(source_dir)
        .unwrap()
        .map(|f| f.unwrap().path())
        .filter(|p| {
            !processed_sets.contains(
                &p.file_stem()
                    .unwrap()
                    .to_str()
                    .unwrap()
                    .parse::<i32>()
                    .unwrap(),
            )
        })
        .collect();

    println!("Extracting meta from {} mapsets...", paths.len());

    let handle = Handle::current();

    for chunk in paths.chunks(100) {
        process_batch(pool.clone(), chunk).await.unwrap();
    }

    Ok(())
}

async fn process_batch(pool: PgPool, paths: &[PathBuf]) -> Result<(), sqlx::Error> {
    let results: Vec<MapsetMeta> = paths.par_iter()
        .map(|f| handle_file(f))
        .inspect(|r| {
            if let Err(e) = r {
                println!("{}", e);
            }
        })
        .flatten().collect();

    let ids: Vec<i32> = results.iter().map(|r| r.id).collect();
    let artists: Vec<String> = results.iter().map(|r| r.artist.clone()).collect();
    let titles: Vec<String> = results.iter().map(|r| r.title.clone()).collect();
    let creators: Vec<String> = results.iter().map(|r| r.creator.clone()).collect();
    let modes: Vec<i32> = results.iter().map(|r| r.mode).collect();

    sqlx::query(
        "
            INSERT INTO mapsets (id, artist, title, creator, mode) SELECT * FROM UNNEST($1::INTEGER[], $2::VARCHAR(255)[], $3::VARCHAR(255)[], $4::VARCHAR(255)[], $5::INTEGER[])
        "
    )
        .bind(&ids[..])
        .bind(&artists[..])
        .bind(&titles[..])
        .bind(&creators[..])
        .bind(&modes[..])
        .execute(&pool)
        .await?;

    println!("Batch finished: {} mapsets", results.len());
    Ok(())
}

fn handle_file(path: &Path) -> anyhow::Result<MapsetMeta> {
    let file = File::open(path)?;
    let mut archive = ZipArchive::new(file)?;

    let mapset_id = path.file_stem().unwrap().to_str().unwrap().parse::<i32>().unwrap();

    read_diffs(&mut archive)?
        .iter()
        .nth(0)
        .map(|diff| get_diff_meta(mapset_id, diff))
        .unwrap()
}

fn get_diff_meta(mapset_id: i32, diff: &String) -> anyhow::Result<MapsetMeta> {
    let osu_file = diff.parse::<OsuFile>()?;

    let mode = match osu_file.general.ok_or(anyhow!("failed to read general section"))?.mode.unwrap_or(Mode::Osu) {
        Mode::Osu => 0,
        Mode::Taiko => 1,
        Mode::Catch => 2,
        Mode::Mania => 3,
        _ => 0
    };

    let meta = osu_file.metadata.unwrap();
    Ok(MapsetMeta {
        id: mapset_id,
        artist: meta.artist.unwrap().into(),
        title: meta.title.unwrap().into(),
        creator: meta.creator.unwrap().into(),
        mode
    })
}

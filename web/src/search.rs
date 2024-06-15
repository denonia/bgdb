use image::io::Reader;
use image_hasher::{HashAlg, HasherConfig, ImageHash};
use itertools::Itertools;
use rocket::form::Form;
use rocket::fs::TempFile;
use rocket::serde::Serialize;
use rocket_dyn_templates::{context, Template};
use sqlx::{PgPool, Row};
use std::io::Cursor;
use tokio::io::AsyncReadExt;

#[derive(FromForm)]
pub struct SearchRequest<'r> {
    img_input: TempFile<'r>,
}

#[derive(PartialEq, Eq, Hash)]
struct HashRecord {
    pub file_name: String,
    pub hash: ImageHash,
}

struct HashDistance {
    pub file_name: String,
    pub distance: u32,
}

#[derive(Serialize)]
struct ResultModel {
    pub file_name: String,
    pub artist: String,
    pub title: String,
    pub creator: String,
    pub preview_url: String,
    pub similarity: u32,
}

#[post("/search", data = "<data>")]
pub async fn post_search(
    data: Form<SearchRequest<'_>>,
    pool: &rocket::State<PgPool>,
) -> std::io::Result<Template> {
    let mut stream = data.img_input.open().await?;
    let mut buffer = Vec::new();
    stream.read_to_end(&mut buffer).await?;

    let hasher = HasherConfig::new()
        .hash_size(16, 16)
        .hash_alg(HashAlg::DoubleGradient)
        .to_hasher();
    let reader = Reader::new(Cursor::new(buffer))
        .with_guessed_format()
        .unwrap();
    let image = reader.decode().unwrap();
    let dest_hash = hasher.hash_image(&image);

    let rows = sqlx::query("SELECT * FROM img_hashes")
        .fetch_all(pool.inner())
        .await
        .unwrap();

    let results: Vec<HashDistance> = rows
        .iter()
        .map(|r| HashRecord {
            file_name: r.get::<String, _>("file_name"),
            hash: ImageHash::from_bytes(r.get::<Vec<u8>, _>("hash").as_slice()).unwrap(),
        })
        .map(|j| HashDistance {
            file_name: j.file_name,
            distance: j.hash.dist(&dest_hash),
        })
        .sorted_by(|a, b| a.distance.cmp(&b.distance))
        .take(10)
        .collect();

    let result_ids = results
        .iter()
        .map(|r| r.file_name[..r.file_name.find("_").unwrap()].to_owned())
        .map(|id| id.parse::<i32>().unwrap())
        .collect();

    let meta = fetch_meta(result_ids, pool.inner()).await;

    let results_top = results
        .iter()
        .zip(meta)
        // .map(|r| (r, meta.iter().filter(|m| m.id == r)))
        .map(|(r, m)| ResultModel {
            file_name: r.file_name[r.file_name.find("_").unwrap() + 1..].to_owned(),
            artist: m.artist,
            title: m.title,
            creator: m.creator,
            // preview_url: format!("img/{}", url_escape::encode_fragment(&r.file_name)),
            preview_url: format!("https://assets.ppy.sh/beatmaps/{}/covers/raw.jpg", m.id),
            similarity: ((1.0 - (r.distance as f32 / 100.0)) * 100.0) as u32,
        })
        .collect_vec();

    Ok(Template::render("search", context! { results_top }))
}

struct MapsetMeta {
    id: i32,
    artist: String,
    title: String,
    creator: String,
}

async fn fetch_meta(mapset_ids: Vec<i32>, pool: &PgPool) -> Vec<MapsetMeta> {
    let rows = sqlx::query(
        "SELECT * FROM mapsets WHERE id = ANY ($1)"
    )
        .bind(&mapset_ids[..])
        .fetch_all(pool)
        .await.unwrap();

    rows.iter().map(|r| MapsetMeta {
        id: r.get::<i32, _>("id"),
        artist: r.get::<String, _>("artist"),
        title: r.get::<String, _>("title"),
        creator: r.get::<String, _>("creator"),
    }).collect()
}

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

    let results = rows
        .iter()
        .map(|r| HashRecord {
            file_name: r.get::<String, _>("file_name"),
            hash: ImageHash::from_bytes(r.get::<Vec<u8>, _>("hash").as_slice()).unwrap(),
        })
        .map(|j| HashDistance {
            file_name: j.file_name,
            distance: j.hash.dist(&dest_hash),
        })
        .sorted_by(|a, b| a.distance.cmp(&b.distance));

    let results_top = results
        .take(10)
        .map(|r| ResultModel {
            file_name: r.file_name[r.file_name.find("_").unwrap() + 1..].to_owned(),
            preview_url: format!("img/{}", url_escape::encode_fragment(&r.file_name)),
            similarity: ((1.0 - (r.distance as f32 / 100.0)) * 100.0) as u32,
        })
        .collect_vec();

    Ok(Template::render("search", context! { results_top }))
}

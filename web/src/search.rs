use std::io;
use std::io::Cursor;

use askama::Template;
use axum::extract::{Multipart, State};
use axum::http::StatusCode;
use axum::response::IntoResponse;
use diesel::{ExpressionMethods, PgNetExpressionMethods, QueryDsl, RunQueryDsl, SelectableHelper};
use futures::TryStreamExt;
use image::io::Reader;
use image_hasher::{HashAlg, HasherConfig, ImageHash};
use itertools::Itertools;
use tokio_util::io::StreamReader;

use crate::models::{ImgHash, Mapset};
use crate::schema::img_hashes::dsl::img_hashes;
use crate::schema::mapsets::dsl::mapsets;
use crate::template::HtmlTemplate;
use crate::AppState;
use crate::schema::mapsets::{id, table};

pub(crate) async fn search(
    State(state): State<AppState>,
    mut multipart: Multipart,
) -> Result<impl IntoResponse, (StatusCode, String)> {
    while let Ok(Some(field)) = multipart.next_field().await {
        let file_name = if let Some(file_name) = field.file_name() {
            file_name.to_owned()
        } else {
            continue;
        };

        if file_name == "" {
            continue;
        }

        let body_with_io_error = field.map_err(|err| io::Error::new(io::ErrorKind::Other, err));
        let body_reader = StreamReader::new(body_with_io_error);
        futures::pin_mut!(body_reader);

        let mut buf: Vec<u8> = Vec::new();
        tokio::io::copy(&mut body_reader, &mut buf).await.unwrap();
        let hasher = HasherConfig::new()
            .hash_size(16, 16)
            .hash_alg(HashAlg::DoubleGradient)
            .to_hasher();
        let reader = Reader::new(Cursor::new(buf)).with_guessed_format().unwrap();
        let image = reader.decode().unwrap();
        let dest_hash = hasher.hash_image(&image);

        let pool = state.pool;

        let mut conn = pool.get().await.unwrap();
        let hashes = conn
            .interact(move |conn| img_hashes.select(ImgHash::as_select()).load(conn))
            .await
            .unwrap()
            .unwrap();

        let results = get_matches(hashes, dest_hash);
        let result_ids: Vec<i32> = results.iter().map(|r| r.id).collect();

        let result_sets = conn
            .interact(move |conn| mapsets
                .filter(id.eq_any(result_ids))
                .select(Mapset::as_select())
                .load(conn))
            .await
            .unwrap()
            .unwrap();

        let results_meta = results
            .into_iter()
            .map(|r| {
                let m = result_sets.iter().find(|m| m.id == r.id).unwrap();
                ResultView {
                    id: r.id,
                    file_name: r.file_name,
                    distance: r.distance,
                    artist: m.artist.clone(),
                    title: m.title.clone(),
                    creator: m.creator.clone(),
                    preview_url: format!("https://assets.ppy.sh/beatmaps/{}/covers/raw.jpg", r.id),
                    mapset_url: format!("https://osu.ppy.sh/beatmapsets/{}", r.id),
                }
            })
            .collect();

        let template = SearchTemplate { results: results_meta };
        return Ok(HtmlTemplate(template));
    }

    Err((StatusCode::BAD_REQUEST, "Please select a file".to_owned()))
}

fn get_matches(hashes: Vec<ImgHash>, dest_hash: ImageHash) -> Vec<ImgDistance> {
    hashes
        .iter()
        .map(|h| ImgDistance {
            id: h.file_name[..h.file_name.find("_").unwrap()].parse::<i32>().unwrap(),
            file_name: h.file_name.clone(),
            distance: ImageHash::from_bytes(&*h.hash).unwrap().dist(&dest_hash),
        })
        .sorted_by(|a, b| a.distance.cmp(&b.distance))
        .take(10)
        .collect()
}

struct ImgDistance {
    id: i32,
    file_name: String,
    distance: u32,
}

struct ResultView {
    id: i32,
    file_name: String,
    distance: u32,

    artist: String,
    title: String,
    creator: String,
    preview_url: String,
    mapset_url: String,
}

#[derive(Template)]
#[template(path = "search.html")]
struct SearchTemplate {
    results: Vec<ResultView>,
}

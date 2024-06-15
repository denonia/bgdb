use rocket_dyn_templates::{context, Template};
use sqlx::{PgPool, Row};

#[get("/")]
pub async fn get_index(pool: &rocket::State<PgPool>) -> Template {
    let count: (i64,) = sqlx::query_as("SELECT COUNT(*) FROM img_hashes")
        .fetch_one(pool.inner())
        .await.unwrap();

    Template::render("index", context! {
        total_images: count.0
    })
}
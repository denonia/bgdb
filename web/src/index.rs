use askama::Template;
use axum::extract::State;
use axum::response::IntoResponse;
use crate::AppState;
use crate::template::HtmlTemplate;
use diesel::prelude::*;
use diesel::row::NamedRow;
use crate::schema::mapsets::dsl::mapsets;

pub(crate) async fn index(State(state): State<AppState>) -> impl IntoResponse {
    let pool = state.pool;

    let mut conn = pool.get().await.unwrap();
    let count = conn.interact(move |conn| {
        mapsets.count().get_result::<i64>(conn)
    }).await.unwrap().unwrap();

    let template = IndexTemplate { total_images: count as i32 };
    HtmlTemplate(template)
}

#[derive(Template)]
#[template(path = "index.html")]
struct IndexTemplate {
    total_images: i32,
}


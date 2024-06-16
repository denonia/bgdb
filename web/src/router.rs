use axum::http::StatusCode;
use axum::response::IntoResponse;
use axum::Router;
use axum::routing::{get, post};
use tower_http::services::{ServeDir, ServeFile};

use crate::AppState;
use crate::index::index;
use crate::search::search;

pub fn app_router(state: AppState) -> Router<AppState> {
    Router::new()
        .route("/", get(index))
        .route("/search", post(search))
        .route_service("/css/styles.css", ServeFile::new("static/css/styles.css"))
        .fallback(handler_404)
}

async fn handler_404() -> impl IntoResponse {
    (
        StatusCode::NOT_FOUND,
        "The requested resource was not found",
    )
}

mod router;
mod search;
mod template;
mod index;
mod schema;
mod models;

use std::io;
use askama::Template;
use axum::{
    http::StatusCode,
    response::{Html, IntoResponse, Response},
    routing::get,
    Router,
};
use axum::extract::Multipart;
use axum::routing::post;
use deadpool_diesel::postgres::{Manager, Pool};
use dotenv::dotenv;
use futures::{Stream, TryStreamExt};
use tokio_util::io::StreamReader;
use tracing_subscriber::layer::SubscriberExt;
use tracing_subscriber::util::SubscriberInitExt;
use crate::router::app_router;

#[derive(Clone)]
pub struct AppState {
    pool: Pool,
}

#[tokio::main]
async fn main() {
    tracing_subscriber::registry()
        .with(
            tracing_subscriber::EnvFilter::try_from_default_env()
                .unwrap_or_else(|_| "example_templates=debug".into()),
        )
        .with(tracing_subscriber::fmt::layer())
        .init();

    dotenv().ok();

    let manager = Manager::new(
        std::env::var("DB_URL").expect("DB_URL must be set."),
        deadpool_diesel::Runtime::Tokio1,
    );
    let pool = Pool::builder(manager).build().unwrap();
    let state = AppState { pool };

    let app = app_router(state.clone()).with_state(state);

    let listener = tokio::net::TcpListener::bind("0.0.0.0:8000")
        .await
        .unwrap();
    tracing::debug!("listening on {}", listener.local_addr().unwrap());
    axum::serve(listener, app).await.unwrap();
}

mod index;
mod search;

use dotenv::dotenv;
use rocket::fs::FileServer;
use rocket_dyn_templates::Template;
use sqlx::PgPool;

#[macro_use]
extern crate rocket;


#[rocket::main]
async fn main() -> Result<(), rocket::Error> {
    dotenv().ok();

    let database_url = std::env::var("DB_URL").expect("DB_URL must be set.");;
    let pool = sqlx::PgPool::connect(&database_url)
        .await
        .expect("Failed to connect to database");

    rocket::build()
        .mount("/", routes![index::get_index, search::post_search])
        .mount("/css", FileServer::from("web/static/css"))
        .mount("/img", FileServer::from("db"))
        .attach(Template::fairing())
        .manage::<PgPool>(pool)
        .launch()
        .await?;

    Ok(())
}

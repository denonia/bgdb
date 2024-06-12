mod index;
mod search;

use rocket::fs::FileServer;
use rocket_dyn_templates::Template;
use sqlx::PgPool;

#[macro_use]
extern crate rocket;


#[rocket::main]
async fn main() -> Result<(), rocket::Error> {
    let database_url = "postgres://bgdb_user:pass@localhost/bgdb_db";
    let pool = sqlx::PgPool::connect(database_url)
        .await
        .expect("Failed to connect to database");

    rocket::build()
        .mount("/", routes![index::get_index, search::post_search])
        .mount("/css", FileServer::from("static/css"))
        .mount("/img", FileServer::from("db"))
        .attach(Template::fairing())
        .manage::<PgPool>(pool)
        .launch()
        .await?;

    Ok(())
}

use rocket_dyn_templates::{context, Template};

#[get("/")]
pub fn get_index() -> Template {
    Template::render("index", context! {})
}
use diesel::{Queryable, Selectable};

#[derive(Queryable, Selectable)]
#[diesel(table_name = crate::schema::mapsets)]
#[diesel(check_for_backend(diesel::pg::Pg))]
pub struct Mapset {
    pub id: i32,
    pub artist: String,
    pub title: String,
    pub creator: String,
    pub mode: i32,
}

#[derive(Queryable, Selectable)]
#[diesel(table_name = crate::schema::img_hashes)]
#[diesel(check_for_backend(diesel::pg::Pg))]
pub struct ImgHash {
    pub id: i32,
    pub file_name: String,
    pub hash: Vec<u8>,
}

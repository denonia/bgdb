diesel::table! {
    mapsets (id) {
        id -> Int4,
        artist -> Varchar,
        title -> Varchar,
        creator -> Varchar,
        mode -> Int4,
    }
}

diesel::table! {
    img_hashes (id) {
        id -> Int4,
        file_name -> Varchar,
        hash -> Bytea,
    }
}

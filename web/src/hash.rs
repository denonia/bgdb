use image_hasher::{HashAlg, HasherConfig, ImageHash};
use sqlx::{postgres::PgPoolOptions, Row};

#[derive(PartialEq, Eq, Hash)]
struct HashRecord {
    pub file_name: String,
    pub distance: u32
}


async fn asd() -> Result<(), sqlx::Error> {
    
    let pool = PgPoolOptions::new()
        .max_connections(20)
        .connect("postgres://bgdb_user:pass@localhost/bgdb_db")
        .await?;

    let result = sqlx::query("SELECT * FROM img_hashes")
        .fetch_all(&pool)
        .await?;

    let hasher = HasherConfig::new().hash_alg(HashAlg::Blockhash).to_hasher();
    let img = image::open("11202_miku2a.jpg").unwrap();
    let dest_hash = hasher.hash_image(&img);

    let mut results: Vec<HashRecord> = vec![];

    for row in result.iter() {
        let hash = ImageHash::from_bytes(row.get::<Vec<u8>, _>("hash").as_slice()).unwrap();

        let hash_record = HashRecord {
            file_name: row.get::<String, _>("file_name"),
            distance: dest_hash.dist(&hash)
        };
        results.push(hash_record);
    }

    results.sort_by(|a, b| a.distance.cmp(&b.distance));

    results.iter().take(10).for_each(|r| {
        println!("{} - {}", r.file_name, r.distance);
    });


    Ok(())
}
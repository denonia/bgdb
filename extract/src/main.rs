mod images;
mod meta;
mod osz;

use std::path::Path;
use crate::images::extract_images;
use crate::meta::extract_meta;

#[tokio::main]
async fn main() -> Result<(), sqlx::Error> {
    let source_dir = Path::new("D:\\RankedMaps\\Songs");
    let dst_dir = Path::new("db");

    extract_meta(&source_dir).await?;
    // extract_images(&source_dir, &dst_dir);

    Ok(())
}

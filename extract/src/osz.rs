use std::fs::File;
use std::io::Read;
use zip::ZipArchive;

pub(crate) fn read_diffs(archive: &mut ZipArchive<File>) -> anyhow::Result<Vec<String>> {
    let mut result: Vec<String> = vec![];

    for i in 0..archive.len() {
        let mut file = archive.by_index(i)?;

        let outpath = match file.enclosed_name() {
            Some(path) => path,
            None => continue,
        };

        if outpath.to_str().unwrap().ends_with(".osu") {
            let mut buf = String::new();
            file.read_to_string(&mut buf)?;
            result.push(buf);
        }
    }
    Ok(result)
}


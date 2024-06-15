use std::fs::{File, read_dir};
use std::io;
use std::io::Read;
use std::path::{Path, PathBuf};
use itertools::Itertools;
use osu_file_parser::events::Event;
use osu_file_parser::OsuFile;
use rayon::iter::{IntoParallelRefIterator, ParallelIterator};
use zip::ZipArchive;
use crate::osz::read_diffs;

pub(crate) fn extract_images(source_dir: &Path, dst_dir: &Path) {
    let existing_sets: Vec<String> = read_dir(dst_dir)
        .unwrap()
        .map(|dir_entry| dir_entry.unwrap().path().file_name().unwrap().to_str().unwrap().to_owned())
        .map(|f| f[..f.find("_").unwrap()].to_owned())
        .collect();

    let paths: Vec<PathBuf> = read_dir(source_dir)
        .unwrap()
        .map(|f| f.unwrap().path())
        .filter(|p| p.extension().unwrap() == "osz")
        .filter(|p| !existing_sets.contains(&p.file_stem().unwrap().to_str().unwrap().to_owned()))
        .collect();

    println!("Extracting {} sets...", paths.len());

    paths.par_iter().for_each(|f| handle_file(&f, dst_dir));
}

fn handle_file(osz_path: &Path, dst_dir: &Path) {
    let Ok(file) = File::open(osz_path) else { return };
    let Ok(mut archive) = ZipArchive::new(file) else { return };

    let path_prefix = format!("{}/{}_", dst_dir.to_str().unwrap(), osz_path.file_stem().unwrap().to_str().unwrap());

    read_diffs(&mut archive)
        .into_iter()
        .flatten()
        .map(|diff| get_diff_bg(diff))
        .unique()
        .flatten()
        .for_each(|path| extract_bg(&mut archive, path, &path_prefix));
}

fn get_diff_bg(diff: String) -> Option<String> {
    let Ok(osu_file) = diff.parse::<OsuFile>() else { return None };

    let bg = osu_file
        .events
        .unwrap()
        .0
        .into_iter()
        .filter_map(|e| match e {
            Event::Background(bg) => Some(bg),
            _ => None,
        })
        .nth(0);

    bg.map(|b| rem_first_and_last(b.file_name.get().to_str().unwrap()))
}

fn extract_bg(archive: &mut ZipArchive<File>, path_in_archive: String, path_prefix: &String) {
    let Ok(mut file) = archive.by_name(&path_in_archive) else { return };

    let outpath = format!("{}{}", path_prefix, path_in_archive.replace("\\", "").replace("/", ""));

    if Path::new(&outpath).exists() {
        return;
    }

    let mut outfile = File::create(&outpath).unwrap();
    io::copy(&mut file, &mut outfile).unwrap();
}

fn rem_first_and_last(value: &str) -> String {
    let mut chars = value.chars();
    chars.next();
    chars.next_back();
    chars.as_str().to_owned()
}

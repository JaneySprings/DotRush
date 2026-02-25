use std::{
    collections::HashSet,
    fs,
    path::{Path, PathBuf},
};

/// Since fs::remove_dir_all is not work properly
/// (dup remove entry / non-empty dir error)
/// This function scan directory contents and remove them recursively
pub(crate) fn remove_dir<P: AsRef<Path>>(path: P) -> std::io::Result<()> {
    let paths = scan_dir_contents(&path)?;
    let mut dir_vec: Vec<PathBuf> = vec![];
    for path in paths {
        if path.is_file() {
            fs::remove_file(path)?;
        } else {
            dir_vec.push(path);
        }
    }
    // reversed sorting by depth
    dir_vec.sort_by(|a, b| b.cmp(a));
    for dir in dir_vec {
        fs::remove_dir_all(dir)?;
    }

    fs::remove_dir_all(path)?;
    Ok(())
}

pub(crate) fn scan_dir_contents<P: AsRef<Path>>(path: P) -> std::io::Result<HashSet<PathBuf>> {
    let mut paths = HashSet::new();
    for entry in fs::read_dir(path)? {
        let entry = entry?;
        let path = entry.path();

        if entry.file_type()?.is_dir() {
            paths.extend(scan_dir_contents(&path)?);
        }
        paths.insert(path);
    }

    Ok(paths)
}

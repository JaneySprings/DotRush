pub mod mono;
pub mod ncdbg;

use crate::{utils, ROOT_DIR};

use serde::{Deserialize, Serialize};
use zed_extension_api::{self as zed};

/// Represents a process id that can be either an integer or a string (containing a number)
#[derive(Serialize, Deserialize, Debug, Clone, PartialEq, Eq)]
#[serde(untagged)]
pub enum ProcessId {
    Int(i32),
    String(String),
}

pub(super) fn get_binary_abs_common(path: &str) -> Result<String, String> {
    let (platform, _) = zed::current_platform();

    let binary_path = utils::get_absolute_path(match platform {
        zed::Os::Windows => format!("{}/{}.exe", ROOT_DIR, path),
        _ => format!("{}/{}", ROOT_DIR, path),
    })
    .map_err(|e| format!("Cannot get {}: {}", path, e))?;

    let mut path = binary_path.to_string_lossy().to_string();
    // remove the weird directory slash at beginning of path
    path.remove(0);

    Ok(path)
}

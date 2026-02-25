use serde::{Deserialize, Serialize};
use std::{collections::HashMap, fs};
use zed_extension_api::{self as zed, http_client::*, serde_json};

use crate::utils;

#[derive(Default, Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub(crate) struct VsxInfo {
    #[serde(default)]
    version: String,
    #[serde(default, rename = "downloads")]
    downloads: HashMap<String, String>,
}

pub fn fetch_vsx_info() -> Result<VsxInfo, String> {
    let response = HttpRequestBuilder::new()
        .method(HttpMethod::Get)
        .url("https://open-vsx.org/api/nromanov/dotrush/latest")
        .build()?
        .fetch();

    response
        .map_err(|e| format!("Failed to fetch from open VSX: {}", e))
        .and_then(|r| {
            serde_json::from_slice(&r.body)
                .map_err(|e| format!("Failed deserialize vsx body json: {}", e))
        })
}

pub fn download_vsx(vsx_info: VsxInfo, arch: &str) -> Result<String, String> {
    let download_url = vsx_info
        .downloads
        .get(arch)
        .ok_or("No download URL found")?;

    // VSX can be treated as a zip file and extracted properly
    zed::download_file(download_url, "./temp", zed::DownloadedFileType::Zip)?;

    // move extracted files to bin directory
    fs::rename("./temp/extension/extension/bin", "./bin")
        .map_err(|e| format!("Failed to move binary directory: {}", e))?;

    utils::remove_dir("./temp").map_err(|e| format!("Failed to remove temp vsx folder: {}", e))?;

    Ok(format!(
        "Downloaded VSX successfully from: {}",
        download_url
    ))
}

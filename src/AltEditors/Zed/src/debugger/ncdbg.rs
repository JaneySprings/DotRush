// ref: https://github.com/qwadrox/zed-netcoredbg

use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::fs;
use zed_extension_api::{
    self as zed, serde_json, DebugAdapterBinary, DebugConfig, DebugRequest, DebugScenario,
    DebugTaskDefinition, StartDebuggingRequestArguments, Worktree,
};

use crate::{debugger::get_binary_abs_common, ROOT_DIR};

#[derive(Serialize, Deserialize, Debug)]
#[serde(rename_all = "camelCase")]
struct NetCoreDebugConfig {
    pub request: String,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub program: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub args: Option<Vec<String>>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub cwd: Option<String>,
    #[serde(default)]
    pub env: HashMap<String, String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub stop_at_entry: Option<bool>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub just_my_code: Option<bool>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub enable_step_filtering: Option<bool>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub process_id: Option<super::ProcessId>,
}

const REPO_SOURCE: &str = "Samsung/netcoredbg";
const BASE_DIR: &str = "Debugger";
const BINARY_PATH: &str = "netcoredbg/netcoredbg";

pub fn get_dap_binary(
    config: DebugTaskDefinition,
    _user_provided_debug_adapter_path: Option<String>,
    worktree: &Worktree,
) -> zed::Result<DebugAdapterBinary, String> {
    let configuration = config.config.to_string();
    let dbg_config: NetCoreDebugConfig =
        serde_json::from_str(&configuration).map_err(|e| e.to_string())?;

    let request = match dbg_config.request.as_str() {
        "launch" => zed::StartDebuggingRequestArgumentsRequest::Launch,
        "attach" => zed::StartDebuggingRequestArgumentsRequest::Attach,
        unknown => {
            return Err(format!(
                "Invalid 'request' value: '{}'. Expected 'launch' or 'attach'",
                unknown
            ));
        }
    };

    let binary_path = get_binary_path(false)?;
    let dab = zed::DebugAdapterBinary {
        command: if fs::metadata(&binary_path).map_or(false, |stat| stat.is_file()) {
            Some(get_binary_path(true)?)
        } else {
            Some(download_netcoredbg()?)
        },
        arguments: vec!["--interpreter=vscode".to_string()],
        envs: dbg_config.env.into_iter().collect(),
        cwd: Some(dbg_config.cwd.unwrap_or_else(|| worktree.root_path())),
        connection: None,
        request_args: StartDebuggingRequestArguments {
            configuration,
            request,
        },
    };

    Ok(dab)
}

pub fn dap_request_kind(
    config: zed_extension_api::serde_json::Value,
) -> zed::Result<zed::StartDebuggingRequestArgumentsRequest, String> {
    match config.get("request").and_then(|v| v.as_str()) {
        Some("launch") => Ok(zed::StartDebuggingRequestArgumentsRequest::Launch),
        Some("attach") => Ok(zed::StartDebuggingRequestArgumentsRequest::Attach),
        _ => Err("Invalid request".to_string()),
    }
}

pub fn dap_config_to_scenario(
    config: DebugConfig,
) -> zed_extension_api::Result<zed_extension_api::DebugScenario, String> {
    let dap_config = match config.request {
        DebugRequest::Launch(launch) => NetCoreDebugConfig {
            request: "launch".to_string(),
            program: Some(launch.program),
            args: if launch.args.is_empty() {
                None
            } else {
                Some(launch.args)
            },
            cwd: launch.cwd,
            env: launch.envs.into_iter().collect(),
            stop_at_entry: None,
            just_my_code: None,
            enable_step_filtering: None,
            process_id: None,
        },
        DebugRequest::Attach(attach) => {
            let pid = attach.process_id.ok_or("process_id not provided")?;

            NetCoreDebugConfig {
                request: "attach".to_string(),
                program: None,
                args: None,
                cwd: None,
                env: HashMap::new(),
                stop_at_entry: config.stop_on_entry,
                just_my_code: None,
                enable_step_filtering: None,
                process_id: Some(crate::debugger::ProcessId::Int(pid as i32)),
            }
        }
    };

    let json = serde_json::to_string(&dap_config)
        .map_err(|e| format!("Failed to serialize NetCoreDebugConfig: {}", e))?;

    Ok(DebugScenario {
        label: config.label,
        adapter: config.adapter,
        build: None,
        config: json,
        tcp_connection: None,
    })
}

fn download_netcoredbg() -> Result<String, String> {
    let release = zed::latest_github_release(
        REPO_SOURCE,
        zed::GithubReleaseOptions {
            require_assets: true,
            pre_release: false,
        },
    )?;

    let (platform, arch) = zed::current_platform();

    let asset_name = match platform {
        zed::Os::Windows => "netcoredbg-win64.zip".to_string(),
        os => format!(
            "netcoredbg-{os}-{arch}.tar.gz",
            os = match os {
                zed::Os::Mac => "osx",
                zed::Os::Linux => "linux",
                _ => "unknown",
            },
            arch = match arch {
                zed::Architecture::Aarch64 => "arm64",
                zed::Architecture::X8664 => "amd64",
                zed::Architecture::X86 => todo!(),
            }
        ),
    };

    let asset = release
        .assets
        .into_iter()
        .find(|asset| asset.name == asset_name)
        .ok_or_else(|| format!("Asset not found: {}", asset_name))?;

    zed::download_file(
        &asset.download_url,
        format!("{}/{}", ROOT_DIR, BASE_DIR).as_str(),
        zed::DownloadedFileType::Zip,
    )
    .map_err(|e| format!("failed to download file: {e}"))?;

    let binary_path = get_binary_path(false)?;

    if fs::metadata(&binary_path).map_or(false, |stat| stat.is_file()) {
        zed::make_file_executable(&binary_path)?;
        return Ok(get_binary_path(true)?);
    }

    Err("failed to download binary".to_string())
}

fn get_binary_path(abs: bool) -> Result<String, String> {
    let (platform, _) = zed::current_platform();

    if abs {
        return get_binary_abs_common(format!("{}/{}", BASE_DIR, BINARY_PATH).as_str());
    }

    Ok(match platform {
        zed::Os::Windows => format!("{}/{}/{}.exe", ROOT_DIR, BASE_DIR, BINARY_PATH),
        _ => format!("{}/{}/{}", ROOT_DIR, BASE_DIR, BINARY_PATH),
    })
}

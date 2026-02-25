mod types;

use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::fs;
use types::DebuggerOptions;
use zed_extension_api::{
    self as zed, serde_json, AttachRequest, DebugAdapterBinary, DebugConfig, DebugRequest,
    DebugScenario, DebugTaskDefinition, StartDebuggingRequestArguments, Worktree,
};

use crate::utils;

#[derive(Serialize, Deserialize, Debug)]
#[serde(rename_all = "camelCase")]
struct MonoDebugConfig {
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
    pub process_id: Option<ProcessId>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub debugger_options: Option<DebuggerOptions>,
    #[serde(default, rename = "type", skip_serializing_if = "Option::is_none")]
    pub ttype: Option<String>,
}

/// Represents a process id that can be either an integer or a string (containing a number)
#[derive(Serialize, Deserialize, Debug, Clone, PartialEq, Eq)]
#[serde(untagged)]
pub enum ProcessId {
    Int(i32),
    String(String),
}

impl MonoDebugConfig {
    fn default_attach(config: DebugConfig, attach: AttachRequest) -> Result<DebugScenario, String> {
        if attach.process_id.is_none() {
            return Err("process_id is required".to_string());
        }

        // process id select from zed
        // let process_id = ProcessId::Int(attach.process_id.unwrap() as i32);

        let mono_debug_config = Self {
            request: "attach".to_string(),
            program: None,
            args: None,
            cwd: None,
            env: HashMap::new(),
            process_id: None,
            debugger_options: Some(DebuggerOptions::default()),
            ttype: Some("unity".to_string()),
        };
        let json = serde_json::to_string(&mono_debug_config)
            .map_err(|e| format!("Failed to serialized attach config: {}", e))?;

        Ok(DebugScenario {
            label: config.label,
            adapter: config.adapter,
            build: None,
            config: json,
            tcp_connection: None,
        })
    }
}

pub fn get_dap_binary(
    config: DebugTaskDefinition,
    _user_provided_debug_adapter_path: Option<String>,
    worktree: &Worktree,
) -> zed::Result<DebugAdapterBinary, String> {
    let (platform, _) = zed::current_platform();

    let binary_path = utils::get_absolute_path(match platform {
        zed::Os::Windows => "./bin/DebuggerMono/monodbg.exe",
        _ => "./bin/DebuggerMono/monodbg",
    })
    .map_err(|e| format!("Cannot get monodbg binary path: {}", e))?;

    if !(fs::metadata(&binary_path).map_or(false, |stat| stat.is_file())) {
        return Err("Cannot find monodbg binary".to_string());
    }

    let configuration = config.config.to_string();
    let dbg_config: MonoDebugConfig =
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

    let mut path = binary_path.to_string_lossy().to_string();
    // remove the weird directory slash at beginning of path
    path.remove(0);
    Ok(zed::DebugAdapterBinary {
        command: Some(path),
        arguments: vec![],
        envs: dbg_config.env.into_iter().collect(),
        cwd: Some(dbg_config.cwd.unwrap_or_else(|| worktree.root_path())),
        connection: None,
        request_args: StartDebuggingRequestArguments {
            configuration,
            request,
        },
    })
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
    match config.request {
        DebugRequest::Launch(_launch) => Err("Launch not implemented".to_string()),
        DebugRequest::Attach(attach) => MonoDebugConfig::default_attach(config, attach),
    }
}

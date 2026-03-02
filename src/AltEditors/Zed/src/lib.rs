mod debugger;
mod utils;
mod vsx_handler;

use std::fs;
use zed::serde_json;
use zed_extension_api::settings::LspSettings;
use zed_extension_api::{
    self as zed, DebugAdapterBinary, DebugConfig, DebugTaskDefinition, LanguageServerId, Result,
    Worktree,
};

pub(crate) const ROOT_DIR: &str = "./bin";

struct DotRushExtension {}
impl DotRushExtension {
    fn language_server_binary(&mut self, language_server_id: &LanguageServerId) -> Result<String> {
        let (platform, arch) = zed::current_platform();

        let binary_path = match platform {
            zed::Os::Windows => "./bin/LanguageServer/DotRush.exe",
            _ => "./bin/LanguageServer/DotRush",
        }
        .to_string();

        if fs::metadata(&binary_path).map_or(false, |stat| stat.is_file()) {
            return Ok(binary_path);
        }

        let vsx_info = vsx_handler::fetch_vsx_info()?;

        let target_arch = format!(
            "{os}-{arch}",
            os = match platform {
                zed::Os::Mac => "darwin",
                zed::Os::Linux => "linux",
                zed::Os::Windows => "win32",
            },
            arch = match arch {
                zed::Architecture::Aarch64 => "arm64",
                zed::Architecture::X8664 => "x64",
                zed::Architecture::X86 => todo!(),
            }
        );

        zed::set_language_server_installation_status(
            language_server_id,
            &zed::LanguageServerInstallationStatus::Downloading,
        );

        vsx_handler::download_vsx(vsx_info, &target_arch)?;

        Ok(binary_path)
    }
}

impl zed::Extension for DotRushExtension {
    fn new() -> Self {
        Self {}
    }

    fn language_server_command(
        &mut self,
        language_server_id: &zed::LanguageServerId,
        _worktree: &Worktree,
    ) -> Result<zed::Command> {
        let dotrush_executable = self.language_server_binary(language_server_id)?;

        Ok(zed::Command {
            command: dotrush_executable,
            args: Default::default(),
            env: Default::default(),
        })
    }

    fn language_server_workspace_configuration(
        &mut self,
        _language_server_id: &zed::LanguageServerId,
        worktree: &zed::Worktree,
    ) -> Result<Option<serde_json::Value>> {
        let settings = LspSettings::for_worktree("dotrush", worktree)
            .ok()
            .and_then(|lsp_settings| lsp_settings.settings.clone())
            .unwrap_or_default();
        Ok(Some(settings))
    }

    fn get_dap_binary(
        &mut self,
        adapter_name: String,
        config: DebugTaskDefinition,
        user_provided_debug_adapter_path: Option<String>,
        worktree: &Worktree,
    ) -> zed::Result<DebugAdapterBinary, String> {
        match adapter_name.as_str() {
            "monodbg" => {
                debugger::mono::get_dap_binary(config, user_provided_debug_adapter_path, worktree)
            }
            "ncdbg" => {
                debugger::ncdbg::get_dap_binary(config, user_provided_debug_adapter_path, worktree)
            }
            _ => todo!(),
        }
    }

    fn dap_request_kind(
        &mut self,
        adapter_name: String,
        config: zed_extension_api::serde_json::Value,
    ) -> zed::Result<zed::StartDebuggingRequestArgumentsRequest, String> {
        match adapter_name.as_str() {
            "monodbg" => debugger::mono::dap_request_kind(config),
            "ncdbg" => debugger::ncdbg::dap_request_kind(config),
            _ => todo!(),
        }
    }

    fn dap_config_to_scenario(
        &mut self,
        config: DebugConfig,
    ) -> zed_extension_api::Result<zed_extension_api::DebugScenario, String> {
        match config.adapter.as_str() {
            "monodbg" => debugger::mono::dap_config_to_scenario(config),
            "ncdbg" => debugger::ncdbg::dap_config_to_scenario(config),
            _ => todo!(),
        }
    }
}

zed::register_extension!(DotRushExtension);

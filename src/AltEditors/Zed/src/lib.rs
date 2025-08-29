use std::fs;
use zed_extension_api::{self as zed, LanguageServerId, Result, Worktree};
use zed::serde_json;
use zed_extension_api::settings::LspSettings;

struct DotRushExtension {}
impl DotRushExtension {
    fn language_server_binary(&mut self, language_server_id: &LanguageServerId) -> Result<String> {
        let binary_dir = "./LanguageServer".to_string();
        let binary_path = "./LanguageServer/DotRush".to_string();
        if fs::metadata(&binary_path).map_or(false, |stat| stat.is_file()) {
            return Ok(binary_path);
        }

        let release = zed::latest_github_release(
            "JaneySprings/DotRush",
            zed::GithubReleaseOptions {
                require_assets: true,
                pre_release: false,
            },
        )?;

        let (platform, arch) = zed::current_platform();
        let asset_name = format!(
            "DotRush.Bundle.Server_{os}-{arch}.zip",
            os = match platform {
                zed::Os::Mac => "osx",
                zed::Os::Linux => "linux",
                zed::Os::Windows => "win",
            },
            arch = match arch {
                zed::Architecture::Aarch64 => "arm64",
                zed::Architecture::X8664 => "x64",
                zed::Architecture::X86 => todo!(),
            }
        );

        let asset = release
            .assets
            .iter()
            .find(|asset| asset.name == asset_name)
            .ok_or_else(|| format!("no asset found matching {:?}", asset_name))?;

        zed::set_language_server_installation_status(
            language_server_id,
            &zed::LanguageServerInstallationStatus::Downloading,
        );

        zed::download_file(
            &asset.download_url,
            &binary_dir,
            zed::DownloadedFileType::Zip,
        )
        .map_err(|e| format!("failed to download file: {e}"))?;

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
}

zed::register_extension!(DotRushExtension);

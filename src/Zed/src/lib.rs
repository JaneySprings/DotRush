use zed_extension_api::{self as zed, LanguageServerId, Result, Worktree};

struct MyExtension {
    // ... state
}

impl zed::Extension for MyExtension {
    fn new() -> Self {
        Self {}
    }

    fn language_server_command(
        &mut self,
        _language_server_id: &LanguageServerId,
        _worktree: &Worktree,
    ) -> Result<zed::Command> {
        let dot_rush_executable = "/Users/You/.lsp/DotRush.Server/DotRush"; // Adjust path to the DotRush executable

        Ok(zed::Command {
            command: dot_rush_executable.to_owned(),
            args: Default::default(),
            env: Default::default(),
        })
    }
}

zed::register_extension!(MyExtension);

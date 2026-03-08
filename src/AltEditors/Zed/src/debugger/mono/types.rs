use std::collections::HashMap;

use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DebuggerOptions {
    pub evaluation_options: EvaluationOptions,
    pub step_over_properties_and_operators: bool,
    pub project_assemblies_only: bool,
    pub automatic_source_link_download: bool,
    pub debug_subprocesses: bool,
    pub symbol_search_paths: Vec<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub source_code_mappings: Option<HashMap<String, String>>,
    pub search_microsoft_symbol_server: bool,
    pub search_nu_get_symbol_server: bool,
    pub skip_native_transitions: bool,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct EvaluationOptions {
    pub evaluation_timeout: u32,
    pub member_evaluation_timeout: u32,
    pub allow_target_invoke: bool,
    pub allow_method_evaluation: bool,
    pub allow_to_string_calls: bool,
    pub flatten_hierarchy: bool,
    pub group_private_members: bool,
    pub group_static_members: bool,
    pub use_external_type_resolver: bool,
    pub integer_display_format: IntegerDisplayFormat,
    pub current_exception_tag: String,
    pub ellipsize_strings: bool,
    pub ellipsized_length: u32,
    pub chunk_raw_strings: bool,
    pub stack_frame_format: StackFrameFormat,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub enum IntegerDisplayFormat {
    Decimal,
    Hexadecimal,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct StackFrameFormat {
    pub line: bool,
    pub module: bool,
    pub parameter_names: bool,
    pub parameter_types: bool,
    pub parameter_values: bool,
    pub language: bool,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub external_code: Option<bool>,
}

impl Default for DebuggerOptions {
    fn default() -> Self {
        Self {
            evaluation_options: Default::default(),
            step_over_properties_and_operators: true,
            project_assemblies_only: true,
            automatic_source_link_download: true,
            debug_subprocesses: false,
            symbol_search_paths: vec![],
            source_code_mappings: None,
            search_microsoft_symbol_server: false,
            search_nu_get_symbol_server: false,
            skip_native_transitions: true,
        }
    }
}

impl Default for EvaluationOptions {
    fn default() -> Self {
        Self {
            evaluation_timeout: 1000,
            member_evaluation_timeout: 5000,
            allow_target_invoke: true,
            allow_method_evaluation: true,
            allow_to_string_calls: true,
            flatten_hierarchy: false,
            group_private_members: true,
            group_static_members: true,
            use_external_type_resolver: true,
            integer_display_format: IntegerDisplayFormat::Decimal,
            current_exception_tag: "$exception".to_string(),
            ellipsize_strings: true,
            ellipsized_length: 100,
            chunk_raw_strings: false,
            stack_frame_format: Default::default(),
        }
    }
}

impl Default for StackFrameFormat {
    fn default() -> Self {
        Self {
            line: false,
            module: true,
            parameter_names: false,
            parameter_types: false,
            parameter_values: false,
            language: false,
            external_code: None,
        }
    }
}

import { ExternalTypeResolver } from '../features/externalTypeResolver';
import * as res from '../resources/constants';
import * as vscode from 'vscode';

export class MonoDebugConfigurationProvider implements vscode.DebugConfigurationProvider {
	async resolveDebugConfiguration(folder: vscode.WorkspaceFolder | undefined,
									config: vscode.DebugConfiguration, 
									token?: vscode.CancellationToken): Promise<vscode.DebugConfiguration | undefined> {

		if (!config.type && !config.request && !config.name) {
			config.name = res.debuggerUnityTitle;
			config.type = res.debuggerUnityId;
			config.request = 'attach';
		}

		if (!config.cwd)
			config.cwd = folder?.uri.fsPath;

        return MonoDebugConfigurationProvider.provideDebuggerOptions(config);
	}

	private static provideDebuggerOptions(options: vscode.DebugConfiguration): vscode.DebugConfiguration {
        if (options.debuggerOptions === undefined) {
            options.debuggerOptions = {
                // evaluationOptions: {
                //     evaluationTimeout: ConfigurationController.getSettingOrDefault<number>(res.configIdDebuggerOptionsEvaluationTimeout),
                //     memberEvaluationTimeout: ConfigurationController.getSettingOrDefault<number>(res.configIdDebuggerOptionsMemberEvaluationTimeout),
                //     allowTargetInvoke: ConfigurationController.getSettingOrDefault<boolean>(res.configIdDebuggerOptionsAllowTargetInvoke),
                //     allowMethodEvaluation: ConfigurationController.getSettingOrDefault<boolean>(res.configIdDebuggerOptionsAllowMethodEvaluation),
                //     allowToStringCalls: ConfigurationController.getSettingOrDefault<boolean>(res.configIdDebuggerOptionsAllowToStringCalls),
                //     flattenHierarchy: ConfigurationController.getSettingOrDefault<boolean>(res.configIdDebuggerOptionsFlattenHierarchy),
                //     groupPrivateMembers: ConfigurationController.getSettingOrDefault<boolean>(res.configIdDebuggerOptionsGroupPrivateMembers),
                //     groupStaticMembers: ConfigurationController.getSettingOrDefault<boolean>(res.configIdDebuggerOptionsGroupStaticMembers),
                //     useExternalTypeResolver: ConfigurationController.getSettingOrDefault<boolean>(res.configIdDebuggerOptionsUseExternalTypeResolver),
                //     integerDisplayFormat: ConfigurationController.getSettingOrDefault<string>(res.configIdDebuggerOptionsIntegerDisplayFormat),
                //     currentExceptionTag: ConfigurationController.getSettingOrDefault<string>(res.configIdDebuggerOptionsCurrentExceptionTag),
                //     ellipsizeStrings: ConfigurationController.getSettingOrDefault<boolean>(res.configIdDebuggerOptionsEllipsizeStrings),
                //     ellipsizedLength: ConfigurationController.getSettingOrDefault<number>(res.configIdDebuggerOptionsEllipsizedLength),
                //     stackFrameFormat: {
                //         line: false, // VSCode already shows the line number
                //         module: ConfigurationController.getSettingOrDefault<boolean>(res.configIdDebuggerOptionsStackFrameFormatModule),
                //         parameterTypes: ConfigurationController.getSettingOrDefault<boolean>(res.configIdDebuggerOptionsStackFrameFormatParameterTypes),
                //         parameterValues: ConfigurationController.getSettingOrDefault<boolean>(res.configIdDebuggerOptionsStackFrameFormatParameterValues),
                //         parameterNames: ConfigurationController.getSettingOrDefault<boolean>(res.configIdDebuggerOptionsStackFrameFormatParameterNames),
                //         language: ConfigurationController.getSettingOrDefault<boolean>(res.configIdDebuggerOptionsStackFrameFormatLanguage),
                //     },
                // },
                // stepOverPropertiesAndOperators: ConfigurationController.getSettingOrDefault<boolean>(res.configIdDebuggerOptionsStepOverPropertiesAndOperators),
                // projectAssembliesOnly: ConfigurationController.getSettingOrDefault<boolean>(res.configIdDebuggerOptionsProjectAssembliesOnly),
                // automaticSourceLinkDownload: ConfigurationController.getSettingOrDefault<boolean>(res.configIdDebuggerOptionsAutomaticSourcelinkDownload),
                // symbolSearchPaths: ConfigurationController.getSettingOrDefault<string[]>(res.configIdDebuggerOptionsSymbolSearchPaths),
                // sourceCodeMappings: ConfigurationController.getSettingOrDefault<any>(res.configIdDebuggerOptionsSourceCodeMappings),
                // searchMicrosoftSymbolServer: ConfigurationController.getSettingOrDefault<boolean>(res.configIdDebuggerOptionsSearchMicrosoftSymbolServer),
                // skipNativeTransitions: ConfigurationController.getSettingOrDefault<boolean>(res.configIdDebuggerOptionsSkipNativeTransitions),
            }
        }
        
        // if (options.justMyCode === undefined)
        //     options.justMyCode = Extensions.getSetting('debugger.projectAssembliesOnly', false);
        // if (options.enableStepFiltering === undefined)
        //     options.enableStepFiltering = Extensions.getSetting('debugger.stepOverPropertiesAndOperators', false);
        // if (options.console === undefined)
        //     options.console = Extensions.getSetting('debugger.console');
        // if (options.symbolOptions === undefined)
        //     options.symbolOptions = {
        //         searchPaths: Extensions.getSetting('debugger.symbolSearchPaths'),
        //         searchMicrosoftSymbolServer: Extensions.getSetting('debugger.searchMicrosoftSymbolServer', false),
        //     };
        // if (options.sourceLinkOptions === undefined)
        //     options.sourceLinkOptions = {
        //         "*": { enabled: Extensions.getSetting('debugger.automaticSourcelinkDownload', true) }
        //     }

        options.transportId = ExternalTypeResolver.feature.transportId;
        return options;
    }
}
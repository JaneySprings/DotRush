import { ExternalTypeResolver } from '../features/externalTypeResolver';
import { Extensions } from '../extensions';
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
                evaluationOptions: {
                    evaluationTimeout: 1000,
                    memberEvaluationTimeout: 5000,
                    allowTargetInvoke: true,
                    allowMethodEvaluation: true,
                    allowToStringCalls: true,
                    flattenHierarchy: false,
                    groupPrivateMembers: true,
                    groupStaticMembers: true,
                    useExternalTypeResolver: true,
                    integerDisplayFormat: 'Decimal',
                    currentExceptionTag: '$exception',
                    ellipsizeStrings: true,
                    ellipsizedLength: 260,
                    stackFrameFormat: {
                        line: false
                    },
                },
                stepOverPropertiesAndOperators: Extensions.getSetting<boolean>(res.configIdDebuggerStepOverPropertiesAndOperators),
                projectAssembliesOnly: Extensions.getSetting<boolean>(res.configIdDebuggerProjectAssembliesOnly),
                automaticSourceLinkDownload: Extensions.getSetting<boolean>(res.configIdDebuggerAutomaticSourcelinkDownload),
                symbolSearchPaths: Extensions.getSetting<string[]>(res.configIdDebuggerSymbolSearchPaths),
                // sourceCodeMappings: Extensions.getSetting(res.configIdDebuggerSourceCodeMappings),
                searchMicrosoftSymbolServer: Extensions.getSetting<boolean>(res.configIdDebuggerSearchMicrosoftSymbolServer),
                skipNativeTransitions: true,
            }
        }

        options.transportId = ExternalTypeResolver.feature.transportId;
        return options;
    }
}
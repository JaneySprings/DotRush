import { DevExpressTemplateProvider } from '../integration/devexpressTemplateProvider';
import { ProcessArgumentBuilder } from '../processes/processArgumentBuilder';
import { ProcessRunner } from '../processes/processRunner';
import { Template } from '../models/template';

export class DotNetTemplateProvider {
    private static templateCache: Template[] = [];

    public static async getTemplates(): Promise<Template[]> {
        if (DotNetTemplateProvider.templateCache.length !== 0)
            return DotNetTemplateProvider.templateCache;

        let templates: Template[] = [];
        const rawResult = (await ProcessRunner.runAsync(new ProcessArgumentBuilder('dotnet')
            .append('new', 'list', '--columns', 'type', 'tags')))
            .split('\n');

        if (rawResult.length < 4)
            return templates;

        for (const template of rawResult.slice(4, rawResult.length - 1)) {
            const record = template.replace(/\s\s+/g, '#').trim();
            if (record.length === 0)
                continue;

            const pair = record.split('#');
            if (pair.length < 4)
                continue;

            templates.push({ title: pair[0], type: pair[2], tags: pair[3].split('/'), invocation: pair[1].split(',') });
        }

        templates = DevExpressTemplateProvider.integrate(templates);
        DotNetTemplateProvider.templateCache = templates;
        return templates;
    }
}

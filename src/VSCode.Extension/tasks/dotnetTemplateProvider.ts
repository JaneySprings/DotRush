import { ProcessArgumentBuilder } from '../processes/processArgumentBuilder';
import { ProcessRunner } from '../processes/processRunner';
import { Template } from '../models/template';

export class DotNetTemplateProvider {
    private static readonly templateCache: Template[] = [];

    public static async getTemplates(): Promise<Template[]> {
        if (this.templateCache.length !== 0)
            return this.templateCache;

        const rawResult = await ProcessRunner.runAsync(new ProcessArgumentBuilder('dotnet')
            .append('new', 'list')
            .append('--columns', 'type')
        );
        const result = rawResult.split('\n');
        const templates: Template[] = [];
        if (result.length < 4)
            return templates;

        for (const template of result.slice(4, result.length - 1)) {
            const record = template.replace(/\s\s+/g, '#').trim();
            if (record.length === 0)
                continue;

            const pair = record.split('#');
            if (pair.length < 3)
                continue;

            templates.push({ name: pair[0], type: pair[2], invocation: pair[1].split(',') });
        }

        if (templates.length !== 0)
            this.templateCache.push(...templates);

        return templates;
    }
}

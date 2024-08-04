import { Template } from "../models/template";

export class DevExpressTemplateProvider {
    private static readonly templateLinks: Template[] = [
        {
            title: ".NET MAUI DevExpress App",
            downloadLink: "DevExpress.Maui.ProjectTemplates",
            type: "project",
            tags: ["MAUI", "Android", "iOS"],
            invocation: ["maui-dx"]
        },
    ];

    public static integrate(templates: Template[]): Template[] {
        for (const template of DevExpressTemplateProvider.templateLinks)
            if (templates.find(t => t.title === template.title) === undefined)
                templates.push(template);

        return templates.sort((a, b) => a.title.localeCompare(b.title));
    }
}
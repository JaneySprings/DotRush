import { ThemeIcon } from "vscode";

export class Icons {
    public static readonly project = "$(window)";
    public static readonly solution = "$(project)";
    public static readonly target = "$(window)";
    public static readonly computer = "$(vm)";
    public static readonly active = "$(circle)";
    public static readonly module = "$(symbol-namespace)";
    public static readonly test = "$(beaker)";

    public static readonly moduleIcon = new ThemeIcon("symbol-namespace");
}

import { ThemeIcon } from "vscode";

export class Icons {
    public static readonly project = "$(window)";
    public static readonly solution = "$(file-submodule)";
    public static readonly active = "$(circle)";
    public static readonly module = "$(symbol-namespace)";
    public static readonly library = "$(folder-library)";
    public static readonly yes = "$(check)";
    public static readonly no = "$(close)"

    public static readonly moduleIcon = new ThemeIcon("symbol-namespace");
}

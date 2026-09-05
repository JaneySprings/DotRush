import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';

export function initializeComponents(webview: vscode.Webview, speedscopeRoot: vscode.Uri, profileUri: vscode.Uri): string {
    const nonce = Math.random().toString(36).slice(2) + Math.random().toString(36).slice(2);
    const csp = [
        `default-src 'none'`,
        `style-src ${webview.cspSource} 'unsafe-inline'`,
        `script-src ${webview.cspSource} 'nonce-${nonce}'`,
        `connect-src ${webview.cspSource}`,
        `img-src ${webview.cspSource} data:`,
        `font-src ${webview.cspSource}`,
    ].join('; ');
    // speedscope only exposes 'window.speedscope.loadFileFromBase64' when a local profile path is requested via the hash.
    // The file itself is fetched from the webview resource origin because the app refuses profile URLs on non-http origins.
    const loader = `<script nonce="${nonce}">
(function () {
    window.location.hash = '#localProfilePath=dotrush';
    const vscode = acquireVsCodeApi();
    const profileUri = ${JSON.stringify(webview.asWebviewUri(profileUri).toString())};
    const fileName = ${JSON.stringify(path.basename(profileUri.fsPath))};

    function waitForSpeedscope() {
        return new Promise(resolve => {
            const check = () => window.speedscope !== undefined ? resolve(window.speedscope) : setTimeout(check, 50);
            check();
        });
    }
    function toBase64(blob) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onerror = () => reject(reader.error);
            reader.onload = () => resolve(String(reader.result).split(',', 2)[1]);
            reader.readAsDataURL(blob);
        });
    }
    async function load() {
        const response = await fetch(profileUri);
        if (!response.ok)
            throw new Error(response.status + ' ' + response.statusText);
        const base64 = await toBase64(await response.blob());
        const speedscope = await waitForSpeedscope();
        speedscope.loadFileFromBase64(fileName, base64);
    }
    load().catch(error => vscode.postMessage({ type: 'error', message: String(error && error.message || error) }));
})();
</script>`;

    // Rewrite the relative asset paths of the speedscope index.html to webview URIs and inject the CSP and the loader
    const html = fs.readFileSync(path.join(speedscopeRoot.fsPath, 'index.html'), 'utf8');
    return html
        .replace(/(href|src)="([^":]+)"/g, (_, attribute, value) => `${attribute}="${webview.asWebviewUri(vscode.Uri.joinPath(speedscopeRoot, value))}"`)
        .replace(/<head>/, `<head>\n    <meta http-equiv="Content-Security-Policy" content="${csp}">`)
        .replace(/<script /, `${loader}\n    <script `);
}

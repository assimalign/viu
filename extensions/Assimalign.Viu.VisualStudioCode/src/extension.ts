import * as fs from 'fs';
import * as path from 'path';

import * as vscode from 'vscode';
import {
    Executable,
    LanguageClient,
    LanguageClientOptions,
    ServerOptions,
    TransportKind
} from 'vscode-languageclient/node';

/**
 * The published file name of the standalone Viu language server. `dotnet publish` appends the
 * platform's executable suffix, so only Windows runtimes carry `.exe`.
 */
const languageServerExecutableBaseName = 'Assimalign.Viu.LanguageServer';

/**
 * Node's `process.platform` values mapped to the platform segment of a .NET runtime identifier.
 * Unlisted platforms have no published payload and are reported as unsupported rather than
 * silently falling back to a runtime that cannot execute.
 */
const runtimePlatformIdentifiers: ReadonlyMap<string, string> = new Map([
    ['win32', 'win'],
    ['linux', 'linux'],
    ['darwin', 'osx']
]);

/**
 * Node's `process.arch` values mapped to the architecture segment of a .NET runtime identifier.
 */
const runtimeArchitectureIdentifiers: ReadonlyMap<string, string> = new Map([
    ['x64', 'x64'],
    ['arm64', 'arm64']
]);

/**
 * The runtime identifiers this extension is published for. Kept in step with
 * `ViuLanguageServerAllRuntimeIdentifiers` in `build/Targets/Build.LanguageServer.targets`, which is
 * the single source of truth for what `Build.ps1` stages into `server/`.
 */
const publishedRuntimeIdentifiers: readonly string[] = [
    'win-x64',
    'win-arm64',
    'linux-x64',
    'osx-arm64',
    'osx-x64'
];

let languageClient: LanguageClient | undefined;

/**
 * The outcome of resolving the bundled language-server payload for the running platform: either an
 * executable path, or the reason no payload can be used.
 */
type LanguageServerResolution =
    | { readonly kind: 'resolved'; readonly executablePath: string }
    | { readonly kind: 'failed'; readonly message: string };

export async function activate(context: vscode.ExtensionContext): Promise<void> {
    const configuration = vscode.workspace.getConfiguration('viu');
    if (configuration.get<boolean>('languageServer.enabled', true) !== true) {
        return;
    }

    const resolution = resolveLanguageServer(
        context.extensionPath,
        configuration.get<string>('languageServer.path', ''));

    if (resolution.kind === 'failed') {
        void vscode.window.showErrorMessage(resolution.message);
        return;
    }

    makeExecutable(resolution.executablePath);

    const server: Executable = {
        command: resolution.executablePath,
        args: [],
        transport: TransportKind.stdio,
        options: {
            cwd: path.dirname(resolution.executablePath)
        }
    };

    const serverOptions: ServerOptions = {
        run: server,
        debug: server
    };

    const fileWatcher = vscode.workspace.createFileSystemWatcher('**/*.{viu,vue,css,csproj}');
    context.subscriptions.push(fileWatcher);

    const clientOptions: LanguageClientOptions = {
        // The `viu` language id binds to `.viu` only. The `**/*.vue` pattern is a compatibility
        // surface, not a language claim: Viu compiles tag-based `.vue` containers, and the server
        // performs a per-document owning-project check that declines any `.vue` file whose nearest
        // project is not a Viu project. Because this extension activates on `onLanguage:viu`, the
        // pattern can only ever apply in a session where a `.viu` document was already opened.
        documentSelector: [
            { language: 'viu' },
            { pattern: '**/*.vue' }
        ],
        outputChannelName: 'Viu Language Server',
        synchronize: {
            fileEvents: fileWatcher
        }
    };

    languageClient = new LanguageClient(
        'viuLanguageServer',
        'Viu Language Server',
        serverOptions,
        clientOptions);

    // The client is deliberately NOT pushed onto context.subscriptions: deactivate() already stops
    // it, and disposing it a second time races the shutdown handshake with the server process.
    await languageClient.start();
}

export async function deactivate(): Promise<void> {
    const client = languageClient;
    languageClient = undefined;

    if (client !== undefined) {
        await client.stop();
    }
}

/**
 * Resolves the language-server executable for the running platform and architecture. An explicit
 * configured path wins, so the server itself can be developed without restaging a payload.
 */
function resolveLanguageServer(
    extensionPath: string,
    configuredPath: string): LanguageServerResolution {
    if (configuredPath.length > 0) {
        if (!fs.existsSync(configuredPath)) {
            return {
                kind: 'failed',
                message:
                    `The Viu language server configured at 'viu.languageServer.path' was not found: ` +
                    `${configuredPath}. Clear the setting to use the payload bundled with this extension.`
            };
        }

        return { kind: 'resolved', executablePath: configuredPath };
    }

    const runtimeIdentifier = resolveRuntimeIdentifier(process.platform, process.arch);
    if (runtimeIdentifier === undefined) {
        return {
            kind: 'failed',
            message:
                `The Viu language server is not published for ${process.platform}-${process.arch}. ` +
                `Supported runtimes: ${publishedRuntimeIdentifiers.join(', ')}. ` +
                `Set 'viu.languageServer.path' to a server you built for this platform.`
        };
    }

    const executablePath = path.join(
        extensionPath,
        'server',
        runtimeIdentifier,
        process.platform === 'win32'
            ? `${languageServerExecutableBaseName}.exe`
            : languageServerExecutableBaseName);

    if (!fs.existsSync(executablePath)) {
        return {
            kind: 'failed',
            message:
                `The Viu language-server payload for ${runtimeIdentifier} is missing at ${executablePath}. ` +
                `This extension package was built without it. Run ` +
                `extensions/Assimalign.Viu.VisualStudioCode/Build.ps1 to publish and stage the server, ` +
                `or install the extension package built for this platform.`
        };
    }

    return { kind: 'resolved', executablePath };
}

/**
 * Maps a Node platform and architecture pair to a .NET runtime identifier, or `undefined` when no
 * payload is published for it.
 */
function resolveRuntimeIdentifier(
    platform: string,
    architecture: string): string | undefined {
    const platformIdentifier = runtimePlatformIdentifiers.get(platform);
    const architectureIdentifier = runtimeArchitectureIdentifiers.get(architecture);

    if (platformIdentifier === undefined || architectureIdentifier === undefined) {
        return undefined;
    }

    const runtimeIdentifier = `${platformIdentifier}-${architectureIdentifier}`;
    return publishedRuntimeIdentifiers.includes(runtimeIdentifier)
        ? runtimeIdentifier
        : undefined;
}

/**
 * Restores the executable bit on non-Windows hosts. A VSIX built on Windows carries no POSIX file
 * mode, so a staged Linux or macOS payload arrives unexecutable and the client would fail with
 * EACCES instead of starting the server.
 */
function makeExecutable(executablePath: string): void {
    if (process.platform === 'win32') {
        return;
    }

    try {
        fs.chmodSync(executablePath, 0o755);
    } catch {
        // A read-only install location is not fatal on its own: the payload may already carry the
        // bit. Let the spawn attempt produce the actionable error instead.
    }
}

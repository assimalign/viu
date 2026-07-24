using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Assimalign.Viu.VisualStudio;

internal sealed class ViuLanguageServerConfiguration
{
    private const string DefaultX64ExecutablePath =
        @"LanguageServer\win-x64\Assimalign.Viu.LanguageServer.exe";
    private const string DefaultArm64ExecutablePath =
        @"LanguageServer\win-arm64\Assimalign.Viu.LanguageServer.exe";

    private ViuLanguageServerConfiguration(
        string relativeX64ExecutablePath,
        string relativeArm64ExecutablePath,
        IReadOnlyList<string> arguments)
    {
        this.RelativeX64ExecutablePath = relativeX64ExecutablePath;
        this.RelativeArm64ExecutablePath = relativeArm64ExecutablePath;
        this.Arguments = arguments;
    }

    public string RelativeX64ExecutablePath { get; }

    public string RelativeArm64ExecutablePath { get; }

    public IReadOnlyList<string> Arguments { get; }

    public static ViuLanguageServerConfiguration Load(string extensionDirectory)
    {
        string configurationPath = Path.Combine(extensionDirectory, "language-server.json");
        if (!File.Exists(configurationPath))
        {
            return new(DefaultX64ExecutablePath, DefaultArm64ExecutablePath, []);
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(configurationPath));
        JsonElement root = document.RootElement;
        string relativeX64ExecutablePath = DefaultX64ExecutablePath;
        string relativeArm64ExecutablePath = DefaultArm64ExecutablePath;

        if (root.TryGetProperty(
                "relativeExecutablePaths",
                out JsonElement executablePathsElement) &&
            executablePathsElement.ValueKind == JsonValueKind.Object)
        {
            relativeX64ExecutablePath = GetExecutablePath(
                executablePathsElement,
                "x64",
                DefaultX64ExecutablePath);
            relativeArm64ExecutablePath = GetExecutablePath(
                executablePathsElement,
                "arm64",
                DefaultArm64ExecutablePath);
        }

        List<string> arguments = [];

        if (root.TryGetProperty("arguments", out JsonElement argumentsElement) &&
            argumentsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement argumentElement in argumentsElement.EnumerateArray())
            {
                string? argument = argumentElement.GetString();
                if (!string.IsNullOrWhiteSpace(argument))
                {
                    arguments.Add(argument);
                }
            }
        }

        return new(relativeX64ExecutablePath, relativeArm64ExecutablePath, arguments);
    }

    public string ResolveExecutablePath(
        string extensionDirectory,
        Architecture processArchitecture)
    {
        string relativeExecutablePath = processArchitecture switch
        {
            Architecture.X64 => this.RelativeX64ExecutablePath,
            Architecture.Arm64 => this.RelativeArm64ExecutablePath,
            _ => throw new PlatformNotSupportedException(
                $"The Viu Visual Studio extension does not support the '{processArchitecture}' process architecture."),
        };
        string normalizedExtensionDirectory = Path.GetFullPath(extensionDirectory);
        string executablePath = Path.GetFullPath(
            Path.Combine(normalizedExtensionDirectory, relativeExecutablePath));
        string relativePath = Path.GetRelativePath(normalizedExtensionDirectory, executablePath);

        if (Path.IsPathRooted(relativePath) ||
            relativePath.Equals("..", StringComparison.Ordinal) ||
            relativePath.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The Viu language-server executable must remain inside the extension directory.");
        }

        return executablePath;
    }

    private static string GetExecutablePath(
        JsonElement executablePathsElement,
        string architectureName,
        string defaultExecutablePath)
    {
        if (!executablePathsElement.TryGetProperty(
                architectureName,
                out JsonElement executablePathElement) ||
            executablePathElement.ValueKind != JsonValueKind.String)
        {
            return defaultExecutablePath;
        }

        string? executablePath = executablePathElement.GetString();
        return string.IsNullOrWhiteSpace(executablePath)
            ? defaultExecutablePath
            : executablePath;
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Assimalign.Viu.UtilityCss.VisualStudio;

/// <summary>
/// Resolves and launches the architecture-specific utility-CSS language-server payload installed
/// beside this extension.
/// </summary>
internal sealed class UtilityCssLanguageServerConfiguration
{
    private const string DefaultX64ExecutablePath =
        @"LanguageServer\win-x64\Assimalign.Viu.UtilityCss.LanguageServer.exe";
    private const string DefaultArm64ExecutablePath =
        @"LanguageServer\win-arm64\Assimalign.Viu.UtilityCss.LanguageServer.exe";

    private static readonly char[] CharactersRequiringQuotes = [' ', '\t', '\n', '\v', '"'];

    private UtilityCssLanguageServerConfiguration(
        string relativeX64ExecutablePath,
        string relativeArm64ExecutablePath,
        IReadOnlyList<string> arguments)
    {
        this.RelativeX64ExecutablePath = relativeX64ExecutablePath;
        this.RelativeArm64ExecutablePath = relativeArm64ExecutablePath;
        this.Arguments = arguments;
    }

    internal string RelativeX64ExecutablePath { get; }

    internal string RelativeArm64ExecutablePath { get; }

    internal IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// Derives the installed extension directory from the assembly location Visual Studio loaded.
    /// </summary>
    internal static string GetExtensionDirectory(string assemblyLocation)
    {
        if (assemblyLocation is null || string.IsNullOrWhiteSpace(assemblyLocation))
        {
            return Directory.GetCurrentDirectory();
        }

        string? directory = Path.GetDirectoryName(assemblyLocation);
        return directory is null || string.IsNullOrWhiteSpace(directory)
            ? Directory.GetCurrentDirectory()
            : directory;
    }

    /// <summary>Loads the optional payload layout and process arguments.</summary>
    internal static UtilityCssLanguageServerConfiguration Load(string extensionDirectory)
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
                if (argument is not null && !string.IsNullOrWhiteSpace(argument))
                {
                    arguments.Add(argument);
                }
            }
        }

        return new(relativeX64ExecutablePath, relativeArm64ExecutablePath, arguments);
    }

    /// <summary>Selects the payload matching the Visual Studio process architecture.</summary>
    internal string ResolveExecutablePath(
        string extensionDirectory,
        Architecture processArchitecture)
    {
        string relativeExecutablePath = processArchitecture switch
        {
            Architecture.X64 => this.RelativeX64ExecutablePath,
            Architecture.Arm64 => this.RelativeArm64ExecutablePath,
            _ => throw new PlatformNotSupportedException(
                $"Viu Utilities for Visual Studio does not support the '{processArchitecture}' process architecture."),
        };
        string normalizedExtensionDirectory = Path.GetFullPath(extensionDirectory);
        string executablePath = Path.GetFullPath(
            Path.Combine(normalizedExtensionDirectory, relativeExecutablePath));
        string containmentPrefix = normalizedExtensionDirectory.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        if (!executablePath.StartsWith(containmentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The utility-CSS language-server executable must remain inside the extension directory.");
        }

        return executablePath;
    }

    /// <summary>Creates a hidden process with protocol streams and diagnostic output redirected.</summary>
    internal ProcessStartInfo CreateProcessStartInformation(
        string executablePath,
        string extensionDirectory)
    {
        string? executableDirectory = Path.GetDirectoryName(executablePath);
        return new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = FormatArguments(this.Arguments),
            WorkingDirectory = executableDirectory is null ||
                string.IsNullOrWhiteSpace(executableDirectory)
                    ? extensionDirectory
                    : executableDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
    }

    /// <summary>
    /// Quotes configured arguments under the rules reversed by <c>CommandLineToArgvW</c>.
    /// </summary>
    internal static string FormatArguments(IReadOnlyList<string> arguments)
    {
        if (arguments is null || arguments.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        foreach (string argument in arguments)
        {
            if (builder.Length != 0)
            {
                builder.Append(' ');
            }

            AppendArgument(builder, argument);
        }

        return builder.ToString();
    }

    private static void AppendArgument(StringBuilder builder, string argument)
    {
        if (argument.Length != 0 && argument.IndexOfAny(CharactersRequiringQuotes) < 0)
        {
            builder.Append(argument);
            return;
        }

        builder.Append('"');
        for (int index = 0; index < argument.Length; index++)
        {
            int backslashCount = 0;
            while (index < argument.Length && argument[index] == '\\')
            {
                backslashCount++;
                index++;
            }

            if (index == argument.Length)
            {
                builder.Append('\\', backslashCount * 2);
                break;
            }

            if (argument[index] == '"')
            {
                builder.Append('\\', (backslashCount * 2) + 1);
            }
            else
            {
                builder.Append('\\', backslashCount);
            }

            builder.Append(argument[index]);
        }

        builder.Append('"');
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
        return executablePath is null || string.IsNullOrWhiteSpace(executablePath)
            ? defaultExecutablePath
            : executablePath;
    }
}

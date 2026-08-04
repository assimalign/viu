using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Assimalign.Viu.VisualStudio;

internal sealed class ViuLanguageServerConfiguration
{
    private const string DefaultX64ExecutablePath =
        @"LanguageServer\win-x64\Assimalign.Viu.Tooling.LanguageServer.exe";
    private const string DefaultArm64ExecutablePath =
        @"LanguageServer\win-arm64\Assimalign.Viu.Tooling.LanguageServer.exe";

    // The set CommandLineToArgvW treats as significant. An argument free of all of them round-trips
    // unquoted; anything else is quoted and its backslash runs doubled.
    private static readonly char[] CharactersRequiringQuotes = [' ', '\t', '\n', '\v', '"'];

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

    /// <summary>
    /// Derives the installed extension's directory from the location of the assembly the extension
    /// was loaded from.
    /// </summary>
    /// <remarks>
    /// In process, the extension has no host-supplied installation path: the assembly Visual Studio
    /// loaded from the VSIX layout sits beside <c>language-server.json</c> and the
    /// <c>LanguageServer\</c> payload, so its own directory is the extension directory. An assembly
    /// with no on-disk location (loaded from bytes) falls back to the current directory rather than
    /// failing, and the caller's <c>File.Exists</c> guard then reports the missing executable.
    /// </remarks>
    public static string GetExtensionDirectory(string assemblyLocation)
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
                // The explicit null test is what the compiler reads: the .NET Framework reference
                // assemblies this extension compiles against carry no nullable annotations, so the
                // NotNullWhen contract on IsNullOrWhiteSpace is not visible here.
                string? argument = argumentElement.GetString();
                if (argument is not null && !string.IsNullOrWhiteSpace(argument))
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

        // Containment is checked by prefix over two normalized absolute paths. A rooted configured
        // path, a '..' escape, and a sibling directory that merely starts with the same characters
        // are all rejected, because the prefix always ends in a directory separator.
        string containmentPrefix = normalizedExtensionDirectory.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        if (!executablePath.StartsWith(containmentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The Viu language-server executable must remain inside the extension directory.");
        }

        return executablePath;
    }

    /// <summary>
    /// Builds the start information for one language-server session: a redirected standard input and
    /// output pair, no console window, and no shell.
    /// </summary>
    /// <remarks>
    /// Standard error is deliberately left inherited. The server reserves it for diagnostics, and a
    /// redirected pipe nobody drains fills its buffer and blocks the server mid-write.
    /// </remarks>
    public ProcessStartInfo CreateProcessStartInformation(
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
            UseShellExecute = false,
            CreateNoWindow = true,
        };
    }

    /// <summary>
    /// Joins configured arguments into one command line using the quoting rules
    /// <c>CommandLineToArgvW</c> reverses, so an argument survives the round trip unchanged.
    /// </summary>
    /// <remarks>
    /// The .NET Framework surface a classic extension compiles against has no
    /// <c>ProcessStartInfo.ArgumentList</c> — that collection, which quotes on the caller's behalf,
    /// arrived with .NET Core. The quoting is therefore performed here.
    /// </remarks>
    public static string FormatArguments(IReadOnlyList<string> arguments)
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
                // A backslash run ending the argument precedes the closing quote, so it doubles.
                builder.Append('\\', backslashCount * 2);
                break;
            }

            if (argument[index] == '"')
            {
                // A backslash run before a quote doubles, and the quote itself is escaped.
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

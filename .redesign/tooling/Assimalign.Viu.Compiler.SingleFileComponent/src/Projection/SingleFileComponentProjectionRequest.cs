using System;

namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// Carries editor-neutral source and options for one deterministic component projection.
/// </summary>
/// <remarks>Specified by <c>[SFC-PIPE-2]</c> and <c>[SFC-PIPE-3]</c>.</remarks>
public sealed class SingleFileComponentProjectionRequest
{
    /// <summary>Initializes a projection request.</summary>
    public SingleFileComponentProjectionRequest(
        SingleFileComponentFormat format,
        string filePath,
        string text,
        string projectDirectory,
        string? rootNamespace = null,
        bool emitDevelopmentMetadata = true)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentException("A source path is required.", nameof(filePath));
        }

        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        if (string.IsNullOrEmpty(projectDirectory))
        {
            throw new ArgumentException("A project directory is required.", nameof(projectDirectory));
        }

        Format = format;
        FilePath = filePath;
        Text = text;
        ProjectDirectory = projectDirectory;
        RootNamespace = rootNamespace;
        EmitDevelopmentMetadata = emitDevelopmentMetadata;
    }

    /// <summary>Gets the source container format.</summary>
    public SingleFileComponentFormat Format { get; }

    /// <summary>Gets the absolute or project-relative source path.</summary>
    public string FilePath { get; }

    /// <summary>Gets the complete source text.</summary>
    public string Text { get; }

    /// <summary>Gets the project directory used for deterministic identities.</summary>
    public string ProjectDirectory { get; }

    /// <summary>Gets the optional project root namespace.</summary>
    public string? RootNamespace { get; }

    /// <summary>Gets whether the projection should emit the compiler/runtime development ABI.</summary>
    public bool EmitDevelopmentMetadata { get; }
}

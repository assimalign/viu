using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>Provides a read-only snapshot of the arguments passed to a template component.</summary>
public interface IComponentArguments : IEnumerable<KeyValuePair<string, object?>>
{
    /// <summary>Gets the number of supplied arguments.</summary>
    int Count { get; }

    /// <summary>Gets an argument by name, or null when it is absent.</summary>
    /// <param name="parameterName">The declared parameter name.</param>
    object? this[string parameterName] { get; }

    /// <summary>Gets a typed argument by name, or the type's default value when it is absent.</summary>
    /// <typeparam name="T">The expected argument type.</typeparam>
    /// <param name="parameterName">The declared parameter name.</param>
    /// <returns>The typed value or its default.</returns>
    T? Get<T>(string parameterName);

    /// <summary>Determines whether an argument was supplied.</summary>
    /// <param name="parameterName">The declared parameter name.</param>
    /// <returns>True when the argument is present.</returns>
    bool Contains(string parameterName);
}


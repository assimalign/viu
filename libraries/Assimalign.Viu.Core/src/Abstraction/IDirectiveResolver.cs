namespace Assimalign.Viu;

/// <summary>Resolves application-registered runtime directives by compiler-emitted name.</summary>
public interface IDirectiveResolver
{
    /// <summary>Resolves a directive, or returns null when the name is not registered.</summary>
    /// <param name="name">The directive registration name.</param>
    /// <returns>The reusable directive, or null.</returns>
    IDirective? Resolve(string name);
}

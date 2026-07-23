namespace Assimalign.Viu;

/// <summary>
/// The lifetime of a service registered on an <see cref="IServiceContainer"/> — the Viu
/// counterpart of <c>Microsoft.Extensions.DependencyInjection.ServiceLifetime</c>
/// (https://learn.microsoft.com/dotnet/core/extensions/dependency-injection#service-lifetimes),
/// declared here so Core takes no dependency on that package. It has no Vue upstream: app-level
/// dependency injection over <see cref="System.IServiceProvider"/> is a .NET-idiomatic addition
/// layered beside Vue's component-tree provide/inject (see <see cref="DependencyInjection"/>).
/// <para>
/// In Viu's default provider the <b>application is the single root scope</b>, so
/// <see cref="Singleton"/> and <see cref="Scoped"/> both resolve once per application and differ only
/// across applications (each has its own provider). The three lifetimes are still distinct so a
/// bring-your-own container adapter can map them onto a container that <i>does</i> create child scopes
/// (a server host resolving per request); see <see cref="IServiceContainer"/>.
/// </para>
/// </summary>
public enum ServiceLifetime
{
    /// <summary>
    /// One instance per provider — created on first resolution and cached for the provider's (the
    /// application's) lifetime, then disposed with it if it is <see cref="System.IDisposable"/>.
    /// </summary>
    Singleton,

    /// <summary>
    /// One instance per scope. Viu's default provider treats the application as the only scope (it
    /// creates no child scopes), so a scoped service resolves once per application — isolated across
    /// applications, cached and disposed like a <see cref="Singleton"/> within one. A bring-your-own
    /// container that supports child scopes gives this its full per-scope meaning.
    /// </summary>
    Scoped,

    /// <summary>
    /// A fresh instance on every resolution. The default provider does not track transient instances
    /// for disposal (a disposable transient is the caller's responsibility) — bring a container
    /// adapter if transient-disposal tracking is required.
    /// </summary>
    Transient,
}

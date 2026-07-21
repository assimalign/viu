using Assimalign.Viu;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// The <see cref="IApplicationBuilder"/> for server-rendering applications — created by
/// <see cref="ServerApplication.CreateBuilder(IComponentDefinition, VirtualNodeProperties?)"/>.
/// Records plugins/provides/registrations on the base <see cref="ApplicationBuilder"/> and, on
/// <see cref="Build"/>, constructs a fresh <see cref="ServerApplication"/> and replays the recorded
/// configuration onto it in order. Build one app per request so no reactive state crosses requests.
/// Not thread-safe (single-threaded render model).
/// </summary>
public sealed class ServerApplicationBuilder : ApplicationBuilder
{
    internal ServerApplicationBuilder(IComponentDefinition rootComponent, VirtualNodeProperties? rootProperties)
        : base(rootComponent, rootProperties)
    {
    }

    /// <summary>
    /// Builds the server application and applies the recorded configuration in call order. Render the
    /// returned app with
    /// <see cref="ServerRenderer.RenderToStringAsync(ServerApplication, SsrContext?, System.Threading.CancellationToken)"/>.
    /// </summary>
    /// <returns>The configured <see cref="ServerApplication"/>.</returns>
    public override ServerApplication Build()
    {
        var application = new ServerApplication(RootComponent, RootProperties);
        // Attach the built provider before ApplyConfiguration so a plugin install can resolve from
        // app.Services ([V01.01.03.24]); the app owns and disposes it (Dispose()).
        application.Context.Services = BuildServiceProvider();
        ApplyConfiguration(application);
        return application;
    }
}

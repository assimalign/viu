using System.Runtime.Versioning;

using Assimalign.Viu;

namespace Assimalign.Viu.Browser;

/// <summary>
/// The <see cref="IApplicationBuilder"/> for browser applications — created by
/// <see cref="BrowserApplication.CreateBuilder(IComponentDefinition, VirtualNodeProperties?, bool)"/>
/// or <see cref="BrowserApplication.CreateSsrBuilder(IComponentDefinition, VirtualNodeProperties?)"/>.
/// Records plugins/provides/registrations on the base <see cref="ApplicationBuilder"/> and, on
/// <see cref="Build"/>, constructs a <see cref="BrowserApplication"/> over the direct or
/// command-buffered browser node-ops and replays the recorded configuration onto it in order.
/// Not thread-safe (browser main thread only).
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class BrowserApplicationBuilder : ApplicationBuilder
{
    private readonly bool _useCommandBuffer;
    private readonly bool _hydrate;

    internal BrowserApplicationBuilder(
        IComponentDefinition rootComponent,
        VirtualNodeProperties? rootProperties,
        bool useCommandBuffer,
        bool hydrate)
        : base(rootComponent, rootProperties)
    {
        _useCommandBuffer = useCommandBuffer;
        _hydrate = hydrate;
    }

    /// <summary>
    /// Builds the browser application and applies the recorded configuration in call order. Mount the
    /// returned app with <c>await app.MountAsync("#app")</c>.
    /// </summary>
    /// <returns>The configured <see cref="BrowserApplication"/>.</returns>
    public override BrowserApplication Build()
    {
        var application = BrowserApplication.Create(RootComponent, RootProperties, _useCommandBuffer, _hydrate);
        ApplyConfiguration(application);
        return application;
    }
}

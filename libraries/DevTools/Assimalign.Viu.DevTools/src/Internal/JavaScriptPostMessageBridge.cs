using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.DevTools;

[SupportedOSPlatform("browser")]
internal sealed partial class JavaScriptPostMessageBridge : IPostMessageBridge
{
    internal const string ModuleName = "Assimalign.Viu.DevTools";

    private static readonly RetryableInitialization Initialization = new();
    private readonly string _targetOrigin;
    private int _subscriptionIdentifier;

    private JavaScriptPostMessageBridge(string targetOrigin) =>
        _targetOrigin = targetOrigin;

    internal static async ValueTask<IPostMessageBridge> CreateAsync(
        string targetOrigin,
        CancellationToken cancellationToken)
    {
        await Initialization.InitializeAsync(InitializeCoreAsync, cancellationToken)
            .ConfigureAwait(false);
        return new JavaScriptPostMessageBridge(targetOrigin);
    }

    public ValueTask StartAsync(
        Func<string, ValueTask> receiver,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_subscriptionIdentifier != 0)
        {
            return ValueTask.CompletedTask;
        }

        _subscriptionIdentifier = PostMessageInteropDispatch.Register(receiver);
        try
        {
            Imports.Subscribe(_subscriptionIdentifier, _targetOrigin);
        }
        catch
        {
            PostMessageInteropDispatch.Unregister(_subscriptionIdentifier);
            _subscriptionIdentifier = 0;
            throw;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask SendAsync(string message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Imports.Send(_subscriptionIdentifier, message);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (_subscriptionIdentifier != 0)
        {
            Imports.Unsubscribe(_subscriptionIdentifier);
            PostMessageInteropDispatch.Unregister(_subscriptionIdentifier);
            _subscriptionIdentifier = 0;
        }

        return ValueTask.CompletedTask;
    }

    private static async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        await JSHost.ImportAsync(
            ModuleName,
            "/_content/Assimalign.Viu.DevTools/viu-devtools.js",
            cancellationToken).ConfigureAwait(false);
        await Imports.Initialize().ConfigureAwait(false);
    }

    private static partial class Imports
    {
        [JSImport("postMessage.subscribe", ModuleName)]
        internal static partial void Subscribe(int subscriptionIdentifier, string targetOrigin);

        [JSImport("postMessage.unsubscribe", ModuleName)]
        internal static partial void Unsubscribe(int subscriptionIdentifier);

        [JSImport("postMessage.send", ModuleName)]
        internal static partial void Send(int subscriptionIdentifier, string message);

        [JSImport("initialize", ModuleName)]
        [return: JSMarshalAs<JSType.Promise<JSType.Void>>]
        internal static partial Task Initialize();
    }
}

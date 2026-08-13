using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace Assimalign.Viu.DevTools;

[SupportedOSPlatform("browser")]
internal static partial class PostMessageInteropDispatch
{
    private static readonly Dictionary<int, ReceiverRegistration> Receivers = [];
    private static int _nextSubscriptionIdentifier = 1;

    internal static int Register(Func<string, ValueTask> receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        int identifier = checked(_nextSubscriptionIdentifier++);
        Receivers.Add(identifier, new ReceiverRegistration(receiver));
        return identifier;
    }

    internal static void Unregister(int identifier)
    {
        if (Receivers.Remove(identifier, out ReceiverRegistration? registration))
        {
            registration.Stop();
        }
    }

    [JSExport]
    internal static void Dispatch(int subscriptionIdentifier, string message)
    {
        if (Receivers.TryGetValue(
            subscriptionIdentifier,
            out ReceiverRegistration? registration))
        {
            registration.Dispatch(message);
        }
    }

    private sealed class ReceiverRegistration
    {
        private readonly object _gate = new();
        private readonly Queue<string> _messages = [];
        private readonly Func<string, ValueTask> _receiver;
        private bool _isDraining;
        private bool _stopped;

        internal ReceiverRegistration(Func<string, ValueTask> receiver) =>
            _receiver = receiver;

        internal void Dispatch(string message)
        {
            lock (_gate)
            {
                if (_stopped)
                {
                    return;
                }

                _messages.Enqueue(message);
                if (_isDraining)
                {
                    return;
                }

                _isDraining = true;
            }

            _ = DrainAsync();
        }

        internal void Stop()
        {
            lock (_gate)
            {
                _stopped = true;
                _messages.Clear();
            }
        }

        private async Task DrainAsync()
        {
            while (true)
            {
                string message;
                lock (_gate)
                {
                    if (_stopped || !_messages.TryDequeue(out message!))
                    {
                        _isDraining = false;
                        return;
                    }
                }

                try
                {
                    await _receiver(message).ConfigureAwait(false);
                }
                catch
                {
                    // A client receiver failure is isolated to its frame and cannot stop delivery.
                }
            }
        }
    }
}

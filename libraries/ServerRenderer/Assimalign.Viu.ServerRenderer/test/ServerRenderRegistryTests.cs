using System;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.ServerRenderer.Tests;

public sealed class ServerRenderRegistryTests
{
    [Fact]
    public void Freeze_RepeatedCalls_ReturnSameRegistryAndRejectPostFreezeRegistration()
    {
        ServerRenderRegistry registry = new();
        ServerRenderRegistration registration = CreateRegistration("registered");
        registry.Register(registration);

        IServerRenderRegistry firstFreeze = registry.Freeze();
        IServerRenderRegistry secondFreeze = registry.Freeze();
        // [SSR-TARGET-4] freezes the same registry identity and closes registration permanently.
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
            registry.Register(CreateRegistration("rejected")));

        firstFreeze.ShouldBeSameAs(registry);
        secondFreeze.ShouldBeSameAs(registry);
        exception.Message.ShouldBe(
            "The server render registry is frozen and cannot accept registrations.");
        firstFreeze.TryResolve(registration.Reference, out ServerRenderRegistration? resolved)
            .ShouldBeTrue();
        resolved.ShouldBeSameAs(registration);
    }

    [Fact]
    public async Task TryResolve_FrozenRegistry_HighConcurrencyReturnsStableRegistrations()
    {
        const int registrationCount = 128;
        const int readerCount = 64;
        const int readsPerReader = 4096;
        ServerRenderRegistry registry = new();
        ServerRenderRegistration[] registrations = new ServerRenderRegistration[registrationCount];
        for (int index = 0; index < registrations.Length; index++)
        {
            registrations[index] = CreateRegistration($"component-{index}");
            registry.Register(registrations[index]);
        }

        // [SSR-TARGET-4] requires the published immutable snapshot to support concurrent reads.
        IServerRenderRegistry frozenRegistry = registry.Freeze();
        TaskCompletionSource releaseReaders = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Task[] readers = new Task[readerCount];
        for (int readerIndex = 0; readerIndex < readers.Length; readerIndex++)
        {
            int readerOffset = readerIndex;
            readers[readerIndex] = Task.Run(async () =>
            {
                await releaseReaders.Task.ConfigureAwait(false);
                for (int readIndex = 0; readIndex < readsPerReader; readIndex++)
                {
                    ServerRenderRegistration expected =
                        registrations[(readerOffset + readIndex) % registrations.Length];
                    frozenRegistry.TryResolve(
                        expected.Reference,
                        out ServerRenderRegistration? resolved).ShouldBeTrue();
                    resolved.ShouldBeSameAs(expected);
                }
            });
        }

        releaseReaders.SetResult();
        await Task.WhenAll(readers);
    }

    private static ServerRenderRegistration CreateRegistration(string name) =>
        new(
            ComponentReference.ForName(name),
            static (_, _, _, _) => Task.CompletedTask);
}

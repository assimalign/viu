using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Tests;

// Pins the default AOT-safe service provider ([V01.01.03.24]) — a factory-delegate registry with
// Singleton / Scoped-per-app / Transient lifetimes, per-provider isolation, and owned-disposable
// disposal. This has no Vue upstream: it is app-level DI over System.IServiceProvider, layered beside
// the Vue-semantic provide/inject chain (DependencyInjectionTests). Mirrors the shape of
// Microsoft.Extensions.DependencyInjection without taking the package dependency.
public class ServiceProviderTests
{
    private sealed class Service
    {
        public int Id { get; init; }
    }

    private sealed class Dependent
    {
        public Dependent(Service service) => Service = service;

        public Service Service { get; }
    }

    private sealed class TrackingDisposable : IDisposable
    {
        private readonly List<string> _log;
        private readonly string _name;

        public TrackingDisposable(List<string> log, string name)
        {
            _log = log;
            _name = name;
        }

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
            _log.Add(_name);
        }
    }

    [Fact]
    public void Singleton_ResolvesTheSameInstance_WithinAProvider()
    {
        var provider = new ServiceProviderBuilder()
            .AddSingleton(_ => new Service { Id = 1 })
            .Build();

        var first = provider.GetRequiredService<Service>();
        var second = provider.GetRequiredService<Service>();

        first.ShouldBeSameAs(second);
    }

    [Fact]
    public void Singleton_Instance_IsReturnedVerbatim()
    {
        var instance = new Service { Id = 7 };
        var provider = new ServiceProviderBuilder().AddSingleton(instance).Build();

        provider.GetRequiredService<Service>().ShouldBeSameAs(instance);
    }

    [Fact]
    public void Transient_ResolvesAFreshInstance_EveryTime()
    {
        var created = 0;
        var provider = new ServiceProviderBuilder()
            .AddTransient(_ => new Service { Id = ++created })
            .Build();

        var first = provider.GetRequiredService<Service>();
        var second = provider.GetRequiredService<Service>();

        first.ShouldNotBeSameAs(second);
        created.ShouldBe(2);
    }

    [Fact]
    public void Scoped_ResolvesOncePerProvider_AndIsIsolatedAcrossProviders()
    {
        // The application is the root scope, so scoped == one-per-app; two providers (two apps) never
        // share the instance — the per-app isolation the reshape promises.
        var builder = new ServiceProviderBuilder().AddScoped(_ => new Service());

        var providerA = builder.Build();
        var providerB = builder.Build();

        providerA.GetRequiredService<Service>().ShouldBeSameAs(providerA.GetRequiredService<Service>());
        providerA.GetRequiredService<Service>().ShouldNotBeSameAs(providerB.GetRequiredService<Service>());
    }

    [Fact]
    public void Singleton_IsIsolatedAcrossProviders()
    {
        var builder = new ServiceProviderBuilder().AddSingleton(_ => new Service());

        var providerA = builder.Build();
        var providerB = builder.Build();

        providerA.GetRequiredService<Service>().ShouldNotBeSameAs(providerB.GetRequiredService<Service>());
    }

    [Fact]
    public void Factory_ResolvesDependenciesThroughTheProvider()
    {
        var provider = new ServiceProviderBuilder()
            .AddSingleton(_ => new Service { Id = 42 })
            .AddSingleton(sp => new Dependent(sp.GetRequiredService<Service>()))
            .Build();

        provider.GetRequiredService<Dependent>().Service.Id.ShouldBe(42);
    }

    [Fact]
    public void Provider_ResolvesItself()
    {
        var provider = new ServiceProviderBuilder().Build();

        provider.GetService(typeof(IServiceProvider)).ShouldBeSameAs(provider);
    }

    [Fact]
    public void GetService_ForUnregisteredType_ReturnsNull()
    {
        var provider = new ServiceProviderBuilder().Build();

        provider.GetService<Service>().ShouldBeNull();
        provider.GetService(typeof(Service)).ShouldBeNull();
    }

    [Fact]
    public void GetRequiredService_ForUnregisteredType_Throws()
    {
        var provider = new ServiceProviderBuilder().Build();

        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<Service>());
    }

    [Fact]
    public void LastRegistration_WinsForAServiceType()
    {
        var provider = new ServiceProviderBuilder()
            .AddSingleton(_ => new Service { Id = 1 })
            .AddSingleton(_ => new Service { Id = 2 })
            .Build();

        provider.GetRequiredService<Service>().Id.ShouldBe(2);
    }

    [Fact]
    public void Factory_ReturningNull_Throws()
    {
        var provider = new ServiceProviderBuilder()
            .Add(new ServiceRegistration(typeof(Service), ServiceLifetime.Singleton, _ => null!))
            .Build();

        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<Service>());
    }

    [Fact]
    public void Dispose_DisposesOwnedSingletonAndScopedInstances()
    {
        var log = new List<string>();
        var provider = new ServiceProviderBuilder()
            .AddSingleton(_ => new TrackingDisposable(log, "singleton"))
            .AddScoped(_ => new SecondDisposable(log, "scoped"))
            .Build();
        var singleton = (TrackingDisposable)provider.GetRequiredService<TrackingDisposable>();
        var scoped = (SecondDisposable)provider.GetRequiredService<SecondDisposable>();

        (provider as IDisposable)!.Dispose();

        singleton.IsDisposed.ShouldBeTrue();
        scoped.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public void Dispose_DisposesInReverseCreationOrder()
    {
        var log = new List<string>();
        var provider = new ServiceProviderBuilder()
            .AddSingleton(_ => new TrackingDisposable(log, "first"))
            .AddSingleton(_ => new SecondDisposable(log, "second"))
            .Build();
        // Resolve "first" then "second" so "second" is created last.
        provider.GetRequiredService<TrackingDisposable>();
        provider.GetRequiredService<SecondDisposable>();

        (provider as IDisposable)!.Dispose();

        // A later-created service may depend on an earlier one, so it is torn down first (upstream parity).
        log.ShouldBe(["second", "first"]);
    }

    private sealed class SecondDisposable : IDisposable
    {
        private readonly List<string> _log;
        private readonly string _name;

        public SecondDisposable(List<string> log, string name)
        {
            _log = log;
            _name = name;
        }

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
            _log.Add(_name);
        }
    }

    [Fact]
    public void Dispose_DoesNotDisposeTransients()
    {
        var log = new List<string>();
        var provider = new ServiceProviderBuilder()
            .AddTransient(_ => new TrackingDisposable(log, "transient"))
            .Build();
        var transient = (TrackingDisposable)provider.GetRequiredService<TrackingDisposable>();

        (provider as IDisposable)!.Dispose();

        // A disposable transient is the caller's responsibility (documented divergence from MS.Ext.DI).
        transient.IsDisposed.ShouldBeFalse();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var log = new List<string>();
        var provider = new ServiceProviderBuilder()
            .AddSingleton(_ => new TrackingDisposable(log, "singleton"))
            .Build();
        provider.GetRequiredService<TrackingDisposable>();

        (provider as IDisposable)!.Dispose();
        (provider as IDisposable)!.Dispose();

        log.ShouldBe(["singleton"]); // disposed exactly once
    }

    [Fact]
    public void GetService_AfterDispose_Throws()
    {
        var provider = new ServiceProviderBuilder().AddSingleton(_ => new Service()).Build();
        (provider as IDisposable)!.Dispose();

        Should.Throw<ObjectDisposedException>(() => provider.GetService<Service>());
    }

    [Fact]
    public void DependencyCycle_Throws_InsteadOfStackOverflow()
    {
        // Two singletons whose factories resolve each other: A's factory needs B, B's factory needs A.
        var provider = new ServiceProviderBuilder()
            .AddSingleton<Dependent>(sp => new Dependent(sp.GetRequiredService<Service>()))
            .Add(new ServiceRegistration(typeof(Service), ServiceLifetime.Singleton,
                sp => ((Dependent)sp.GetRequiredService<Dependent>()).Service))
            .Build();

        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<Dependent>());
    }
}

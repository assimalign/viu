using System;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Core.Tests;

public sealed class ApplicationLifetimeTests
{
    [Fact]
    public void Constructor_SecondLifetimeForContext_Throws()
    {
        ApplicationContext context = CreateContext();
        using var lifetime = new ApplicationLifetime(context);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => new ApplicationLifetime(context));

        exception.Message.ShouldContain("only one application lifetime");
    }

    [Fact]
    public void Transitions_NormalLifetime_UpdatesStateSignalAndContext()
    {
        ApplicationContext context = CreateContext();
        using var lifetime = new ApplicationLifetime(context);

        lifetime.State.ShouldBe(ApplicationState.Created);
        context.Stopping.ShouldBe(lifetime.Stopping);
        context.IsRunning.ShouldBeFalse();

        lifetime.StartExecution();
        lifetime.State.ShouldBe(ApplicationState.Starting);
        lifetime.SignalRunning();
        lifetime.State.ShouldBe(ApplicationState.Running);
        context.IsRunning.ShouldBeTrue();

        lifetime.RequestStopping();
        lifetime.State.ShouldBe(ApplicationState.Stopping);
        lifetime.Stopping.IsCancellationRequested.ShouldBeTrue();
        context.IsRunning.ShouldBeFalse();

        lifetime.CompleteStopping();
        lifetime.State.ShouldBe(ApplicationState.Stopped);
        lifetime.HasFailed.ShouldBeFalse();
    }

    [Fact]
    public void StartExecution_AfterStopping_StillRejectsSecondExecution()
    {
        using var lifetime = new ApplicationLifetime(CreateContext());
        lifetime.StartExecution();
        lifetime.CompleteStopping();

        Should.Throw<InvalidOperationException>(lifetime.StartExecution);
    }

    [Fact]
    public void AlreadyCancelledStartToken_IsObservedAfterClaimWithoutRunning()
    {
        ApplicationContext context = CreateContext();
        using var lifetime = new ApplicationLifetime(context);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        lifetime.StartExecution();
        using CancellationTokenRegistration registration = cancellationSource.Token.Register(
            lifetime.RequestStopping);

        lifetime.State.ShouldBe(ApplicationState.Stopping);
        context.IsRunning.ShouldBeFalse();
        lifetime.CompleteStopping();
        lifetime.State.ShouldBe(ApplicationState.Stopped);
    }

    [Fact]
    public void Fail_CancelsBeforeOneShotReportAndRetainsFailedState()
    {
        int reports = 0;
        bool cancellationObservedByHandler = false;
        ApplicationLifetime? lifetime = null;
        var options = new ApplicationOptions
        {
            RootComponent = new CommentNode("root"),
            ErrorHandler = (_, _, diagnosticInformation) =>
            {
                reports++;
                cancellationObservedByHandler = lifetime!.Stopping.IsCancellationRequested;
                diagnosticInformation.ShouldBe("application lifetime");
            },
        };
        var context = new ApplicationContext(options);
        using var createdLifetime = new ApplicationLifetime(context);
        lifetime = createdLifetime;
        createdLifetime.StartExecution();
        createdLifetime.SignalRunning();
        createdLifetime.Fail(new InvalidOperationException("first"));
        createdLifetime.Fail(new InvalidOperationException("second"));

        createdLifetime.State.ShouldBe(ApplicationState.Failed);
        createdLifetime.HasFailed.ShouldBeTrue();
        context.IsRunning.ShouldBeFalse();
        reports.ShouldBe(1);
        cancellationObservedByHandler.ShouldBeTrue();
    }

    [Fact]
    public void Fail_FromCreatedOrStopped_IsRejected()
    {
        using var createdLifetime = new ApplicationLifetime(CreateContext());
        Should.Throw<InvalidOperationException>(
            () => createdLifetime.Fail(new InvalidOperationException("created")));

        using var stoppedLifetime = new ApplicationLifetime(CreateContext());
        stoppedLifetime.StartExecution();
        stoppedLifetime.CompleteStopping();
        Should.Throw<InvalidOperationException>(
            () => stoppedLifetime.Fail(new InvalidOperationException("stopped")));
    }

    [Fact]
    public void IsStoppingCancellation_RequiresRequestedSharedToken()
    {
        using var lifetime = new ApplicationLifetime(CreateContext());
        lifetime.StartExecution();
        var stoppingException = new OperationCanceledException(lifetime.Stopping);
        var unrelatedException = new OperationCanceledException(new CancellationToken(true));

        lifetime.IsStoppingCancellation(stoppingException).ShouldBeFalse();
        lifetime.RequestStopping();
        lifetime.IsStoppingCancellation(stoppingException).ShouldBeTrue();
        lifetime.IsStoppingCancellation(unrelatedException).ShouldBeFalse();
    }

    [Fact]
    public void Dispose_RunningLifetime_CompletesStopping()
    {
        ApplicationContext context = CreateContext();
        var lifetime = new ApplicationLifetime(context);
        lifetime.StartExecution();
        lifetime.SignalRunning();

        lifetime.Dispose();

        lifetime.State.ShouldBe(ApplicationState.Stopped);
        lifetime.Stopping.IsCancellationRequested.ShouldBeTrue();
        context.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_AlreadyCancelledToken_ClaimsThenStopsWithoutRunning()
    {
        await using var application = new TestApplication(CreateContext());
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await application.RunAsync(cancellationSource.Token);

        application.Lifetime.State.ShouldBe(ApplicationState.Stopped);
        application.Context.IsRunning.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(application.Lifetime.StartExecution);
    }

    private static ApplicationContext CreateContext() =>
        new(new ApplicationOptions { RootComponent = new CommentNode("root") });

    private sealed class TestApplication : IApplication
    {
        private CancellationTokenRegistration _registration;

        internal TestApplication(ApplicationContext context)
        {
            Context = context;
            Lifetime = new ApplicationLifetime(context);
        }

        public IApplicationContext Context { get; }

        internal ApplicationLifetime Lifetime { get; }

        public IApplication Use(ApplicationMiddleware middleware)
        {
            ArgumentNullException.ThrowIfNull(middleware);
            return this;
        }

        public ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            Lifetime.StartExecution();
            _registration = cancellationToken.Register(Lifetime.RequestStopping);
            if (!Lifetime.Stopping.IsCancellationRequested)
            {
                Lifetime.SignalRunning();
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            Lifetime.CompleteStopping();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _registration.Dispose();
            Lifetime.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

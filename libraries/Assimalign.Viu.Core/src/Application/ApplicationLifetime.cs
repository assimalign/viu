using System;
using System.Diagnostics;
using System.Threading;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>Coordinates the platform-invariant transitions of one application context.</summary>
/// <remarks>
/// Construction claims the context exactly once. The state machine is single-threaded and may be
/// driven only by the persistent host that owns the terminal mount. Specified by <c>[APP-1]</c>.
/// </remarks>
public sealed class ApplicationLifetime : IDisposable
{
    private readonly ApplicationContext _context;
    private readonly CancellationTokenSource _stoppingSource = new();
    private readonly CancellationToken _stopping;
    private bool _isDisposed;
    private bool _isFailureReported;

    /// <summary>Initializes a lifetime and claims the application context.</summary>
    /// <param name="context">The context this lifetime exclusively coordinates.</param>
    public ApplicationLifetime(ApplicationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _stopping = _stoppingSource.Token;
        try
        {
            context.ClaimLifetime(_stopping);
        }
        catch
        {
            _stoppingSource.Dispose();
            throw;
        }
    }

    /// <summary>Gets the current application state.</summary>
    public ApplicationState State { get; private set; }

    /// <summary>Gets whether this lifetime terminated through a failure.</summary>
    public bool HasFailed => State == ApplicationState.Failed;

    /// <summary>Gets the shared graceful-shutdown token.</summary>
    public CancellationToken Stopping => _stopping;

    /// <summary>
    /// Claims the lifetime's sole execution attempt by moving from Created to Starting.
    /// </summary>
    /// <exception cref="InvalidOperationException">Execution was already claimed.</exception>
    public void StartExecution()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (State != ApplicationState.Created)
        {
            throw new InvalidOperationException("An application lifetime can start only once.");
        }

        State = ApplicationState.Starting;
    }

    /// <summary>Signals that the host terminal mounted and moves Starting to Running.</summary>
    public void SignalRunning()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        _stopping.ThrowIfCancellationRequested();
        if (State != ApplicationState.Starting)
        {
            throw new InvalidOperationException(
                "The application cannot enter Running from its current state.");
        }

        State = ApplicationState.Running;
        _context.SetIsRunning(true);
    }

    /// <summary>
    /// Requests graceful shutdown. Starting and Running move to Stopping; repeated requests are
    /// idempotent.
    /// </summary>
    public void RequestStopping()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        RequestStoppingCore();
    }

    /// <summary>
    /// Completes graceful shutdown, first requesting it when execution is still Starting or
    /// Running. Created, Stopped, and Failed remain unchanged.
    /// </summary>
    public void CompleteStopping()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        CompleteStoppingCore();
    }

    /// <summary>
    /// Moves an executing application to Failed, cancels <see cref="Stopping"/>, and then reports
    /// the error exactly once. Cancellation-before-report ordering is guaranteed.
    /// </summary>
    /// <param name="exception">The lifetime failure.</param>
    /// <param name="source">The component context associated with the failure, when any.</param>
    public void Fail(Exception exception, ComponentContext? source = null)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(exception);
        if (State == ApplicationState.Failed)
        {
            return;
        }

        if (State is not (ApplicationState.Starting
            or ApplicationState.Running
            or ApplicationState.Stopping))
        {
            throw new InvalidOperationException(
                "The application can fail only while starting, running, or stopping.");
        }

        State = ApplicationState.Failed;
        _context.SetIsRunning(false);
        CancelStopping();

        if (_isFailureReported)
        {
            return;
        }

        _isFailureReported = true;
        try
        {
            _context.ErrorHandler?.Invoke(exception, source, "application lifetime");
        }
        catch (Exception handlerException)
        {
            Debug.WriteLine($"[Viu warning] Application error handler failed: {handlerException}");
        }
    }

    /// <summary>
    /// Gets whether an operation cancellation represents this lifetime's shared stopping signal.
    /// </summary>
    /// <param name="exception">The cancellation exception to classify.</param>
    /// <returns>True only for the requested shared stopping token.</returns>
    public bool IsStoppingCancellation(OperationCanceledException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return _stopping.IsCancellationRequested
            && exception.CancellationToken == _stopping;
    }

    /// <summary>Releases this transition machine after ensuring shutdown was requested.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        if (State is ApplicationState.Starting
            or ApplicationState.Running
            or ApplicationState.Stopping)
        {
            CompleteStoppingCore();
        }
        else
        {
            _context.SetIsRunning(false);
            CancelStopping();
        }

        _isDisposed = true;
        _stoppingSource.Dispose();
    }

    private void RequestStoppingCore()
    {
        if (State is ApplicationState.Starting or ApplicationState.Running)
        {
            State = ApplicationState.Stopping;
        }

        _context.SetIsRunning(false);
        CancelStopping();
    }

    private void CompleteStoppingCore()
    {
        if (State is ApplicationState.Starting or ApplicationState.Running)
        {
            RequestStoppingCore();
        }

        if (State == ApplicationState.Stopping)
        {
            State = ApplicationState.Stopped;
        }
    }

    private void CancelStopping()
    {
        if (!_stoppingSource.IsCancellationRequested)
        {
            _stoppingSource.Cancel();
        }
    }
}

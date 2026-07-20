using System;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Store;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// The Pinia-style store for the user page (the C# port of vue-hackernews-2.0's user view). Loads a
/// single profile with explicit loading/error state; a superseded load is cancelled. Never throws.
/// </summary>
internal sealed class UserStore : Store<UserState>
{
    private readonly IHackerNewsClient _client;
    private CancellationTokenSource? _inFlight;

    /// <summary>Creates the store over <paramref name="client"/>.</summary>
    /// <param name="client">The data client all reads go through.</param>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> is null.</exception>
    public UserStore(IHackerNewsClient client)
        : base(
            "user",
            static () => new UserState
            {
                ActiveId = null,
                Profile = null,
                IsLoading = false,
                Error = null,
            },
            static (target, source) =>
            {
                target.ActiveId = source.ActiveId;
                target.Profile = source.Profile;
                target.IsLoading = source.IsLoading;
                target.Error = source.Error;
            })
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <summary>
    /// Loads the profile for <paramref name="id"/>, modeling loading and error as state. A newer call
    /// cancels an in-flight older one. Never throws.
    /// </summary>
    /// <param name="id">The case-sensitive user id.</param>
    public async Task LoadUserAsync(string id)
    {
        _inFlight?.Cancel();
        _inFlight?.Dispose();
        var cancellation = new CancellationTokenSource();
        _inFlight = cancellation;
        var token = cancellation.Token;

        Patch(state =>
        {
            state.ActiveId = id;
            state.Profile = null;
            state.IsLoading = true;
            state.Error = null;
        });

        try
        {
            var profile = string.IsNullOrEmpty(id) ? null : await _client.GetUserAsync(id, token);
            token.ThrowIfCancellationRequested();
            Patch(state =>
            {
                state.Profile = profile;
                state.IsLoading = false;
                state.Error = profile is null ? "That user does not exist." : null;
            });
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer load.
        }
        catch (Exception exception)
        {
            if (!token.IsCancellationRequested)
            {
                Patch(state =>
                {
                    state.IsLoading = false;
                    state.Error = exception.Message;
                });
            }
        }
    }
}

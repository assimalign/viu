using System;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// A HackerNews user profile — the domain projection of <c>/v0/user/{id}</c>
/// (https://github.com/HackerNews/API#users). Immutable; the user page renders from it.
/// </summary>
/// <param name="Id">The case-sensitive user id (also the display name).</param>
/// <param name="Created">The account creation time (converted from the API's Unix seconds).</param>
/// <param name="Karma">The user's karma.</param>
/// <param name="About">The self-description HTML, or null.</param>
/// <param name="SubmittedCount">The number of items the user has submitted.</param>
internal sealed record HackerNewsUser(
    string Id,
    DateTimeOffset Created,
    int Karma,
    string? About,
    int SubmittedCount);

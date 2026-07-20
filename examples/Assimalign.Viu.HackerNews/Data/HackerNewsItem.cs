using System;
using System.Collections.Generic;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// A HackerNews item — the domain projection of the unified <c>/v0/item/{id}</c> resource
/// (https://github.com/HackerNews/API#items), which represents a story, job, Ask/Show HN post, poll,
/// or comment depending on <see cref="Type"/>. Immutable with a stable <see cref="Id"/>, so story
/// lists render as keyed rows with stable identity (no per-item ad-hoc interop) and a virtualization
/// strategy can be added later without touching the views. The wire shape is mapped in
/// <see cref="HackerNewsClient"/> from the internal <c>ItemPayload</c> DTO.
/// </summary>
/// <param name="Id">The item's unique id.</param>
/// <param name="Type">The item kind: <c>story</c>, <c>comment</c>, <c>job</c>, <c>poll</c>, or <c>pollopt</c>.</param>
/// <param name="By">The submitting user's id, or null for a deleted item.</param>
/// <param name="Time">The submission time (converted from the API's Unix seconds).</param>
/// <param name="Title">The story/poll title, or null for a comment.</param>
/// <param name="Url">The story's target URL, or null (Ask/Show/self posts have none).</param>
/// <param name="Host">The display host derived from <see cref="Url"/> (e.g. <c>example.com</c>), or null.</param>
/// <param name="Text">The HTML body of a comment or self/Ask post, or null.</param>
/// <param name="Score">The story's score, or 0.</param>
/// <param name="Descendants">The total comment count for a story/poll, or 0.</param>
/// <param name="Kids">The direct child comment ids in ranked display order.</param>
/// <param name="Parent">The parent item id for a comment, or 0.</param>
/// <param name="Deleted">Whether the item was deleted.</param>
/// <param name="Dead">Whether the item is dead (flagged/removed).</param>
internal sealed record HackerNewsItem(
    long Id,
    string Type,
    string? By,
    DateTimeOffset Time,
    string? Title,
    string? Url,
    string? Host,
    string? Text,
    int Score,
    int Descendants,
    IReadOnlyList<long> Kids,
    long Parent,
    bool Deleted,
    bool Dead);

using System;
using System.Collections.Generic;

using Assimalign.Viu;
using Assimalign.Viu.Router;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// The user-profile route (<c>/user/:id</c>) — the C# port of vue-hackernews-2.0's user view. Reads
/// <c>:id</c> from the route, drives <see cref="UserStore"/> through a route-watching effect, and
/// renders the profile from store state. Wrapped as an async route component in <see cref="AppRoutes"/>.
/// </summary>
internal sealed class UserView : IComponent
{
    /// <summary>The shared route-view definition instance.</summary>
    public static readonly UserView Instance = new();

    private UserView()
    {
    }

    /// <inheritdoc />
    public string? Name => "UserView";

    /// <inheritdoc />
    public ComponentSetup Setup(ComponentProperties properties, ComponentSetupContext context)
    {
        var router = DependencyInjection.Inject(RouterInjectionKeys.Router)!;
        var stores = DependencyInjection.GetRequiredService<HackerNewsStores>();
        var store = stores.User.UseStore();

        string ReadId() => router.CurrentRoute.Value.Parameters.TryGetString("id", out var id) ? id : string.Empty;

        Reactive.Watch(
            ReadId,
            (id, _, _) =>
            {
                if (!string.IsNullOrEmpty(id))
                {
                    _ = store.LoadUserAsync(id);
                }
            },
            new WatchOptions { Immediate = true });

        return () =>
        {
            var state = store.State;
            if (state.Error is not null)
            {
                return View(ErrorView.Inline(state.Error));
            }
            if (state.Profile is null)
            {
                return View(VirtualNodeFactory.Component(LoadingView.Instance));
            }
            return View(Profile(state.Profile));
        };
    }

    private static VirtualNode View(params VirtualNode?[] children)
        => VirtualNodeFactory.Element("section", VirtualNodeFactory.Properties(("class", "hn-view hn-user-view")), children);

    private static VirtualNode Profile(HackerNewsUser user)
    {
        var children = new List<VirtualNode?>(5)
        {
            Ui.Text("h1", "hn-view-title", user.Id),
            Ui.Text(
                "div",
                "hn-user-meta",
                $"joined {user.Created.UtcDateTime:MMM d, yyyy} · {Ui.Plural(user.Karma, "karma point")} · {Ui.Plural(user.SubmittedCount, "submission")}"),
        };

        var about = HtmlText.ToParagraphs(user.About);
        if (about.Count > 0)
        {
            var aboutNodes = new VirtualNode?[about.Count];
            for (var index = 0; index < about.Count; index++)
            {
                aboutNodes[index] = Ui.Text("p", null, about[index]);
            }
            children.Add(VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("class", "hn-user-about")), aboutNodes));
        }

        children.Add(Ui.ExternalLink(
            $"https://news.ycombinator.com/user?id={Uri.EscapeDataString(user.Id)}",
            "hn-external-profile",
            "View on news.ycombinator.com →"));

        return VirtualNodeFactory.Element("article", VirtualNodeFactory.Properties(("class", "hn-user")), children.ToArray());
    }
}

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Router.Tests;

// Pins the base-path arithmetic of vue-router's history layer: normalizeBase / stripBase / createHref
// (packages/router/src/history/common.ts, location.ts) and the createCurrentLocation / hash-base
// logic (html5.ts, hash.ts). [V01.01.08.02] base handling AC.
public class HistoryPathNormalizationTests
{
    [Theory]
    [InlineData(null, "")]      // no base -> "/" default -> trailing slash trimmed -> "" (no base)
    [InlineData("", "")]
    [InlineData("/", "")]
    [InlineData("/app", "/app")]
    [InlineData("/app/", "/app")]
    [InlineData("app", "/app")] // forced leading slash
    [InlineData("#", "#")]      // a hash base keeps its leading '#'
    [InlineData("#/", "#")]
    [InlineData("/folder/#", "/folder/#")]
    public void NormalizeBase_MatchesUpstream(string? input, string expected)
        => HistoryPathNormalization.NormalizeBase(input).ShouldBe(expected);

    [Theory]
    [InlineData("/app/users", "/app", "/users")]
    [InlineData("/app", "/app", "/")]              // exact match collapses to "/"
    [InlineData("/other", "/app", "/other")]       // no prefix -> unchanged
    [InlineData("/APP/users", "/app", "/users")]   // case-insensitive prefix match
    [InlineData("/users", "", "/users")]           // empty base strips nothing
    public void StripBase_MatchesUpstream(string pathname, string @base, string expected)
        => HistoryPathNormalization.StripBase(pathname, @base).ShouldBe(expected);

    [Theory]
    [InlineData("/app", "/users", "/app/users")]   // plain base is prefixed
    [InlineData("", "/users", "/users")]
    [InlineData("#", "/users", "#/users")]         // leading '#' base kept verbatim
    [InlineData("/folder/#", "/users", "#/users")] // "<path>#" collapses to '#'
    public void CreateHref_MatchesUpstreamBeforeHashReplacement(string @base, string location, string expected)
        => HistoryPathNormalization.CreateHref(@base, location).ShouldBe(expected);

    [Theory]
    [InlineData(null, "example.com", "/folder/", "", "/folder/#")]  // default base from pathname + '#'
    [InlineData(null, "", "/folder/", "", "#")]                     // hostless file:// ignores the path
    [InlineData("/app/", "example.com", "/folder/", "", "/app/#")]  // provided base gets a '#'
    [InlineData("/folder/#/app/", "example.com", "/x", "", "/folder/#/app/")] // already hashed -> untouched
    public void ComputeHashBase_MatchesUpstream(string? providedBase, string host, string pathname, string search, string expected)
        => HistoryPathNormalization.ComputeHashBase(providedBase, host, pathname, search).ShouldBe(expected);

    [Theory]
    // Web mode (no '#' in base): stripBase(pathname) + search + hash.
    [InlineData("/app", "/app/users", "?q=1", "#sec", "/users?q=1#sec")]
    [InlineData("", "/users", "", "", "/users")]
    // Hash mode (base carries '#'): the fragment after the hash-base, forced to a leading '/'.
    [InlineData("#", "/anything", "", "#/users", "/users")]
    [InlineData("/folder/#", "/folder/", "", "#/users", "/users")]
    [InlineData("#", "/x", "", "", "/")]            // empty hash -> root
    [InlineData("#!/", "/x", "", "#!/users", "/users")]
    public void CreateCurrentLocation_MatchesUpstream(string @base, string pathname, string search, string hash, string expected)
        => HistoryPathNormalization.CreateCurrentLocation(@base, pathname, search, hash).ShouldBe(expected);

    [Theory]
    [InlineData("https://example.com/app/", "/app/")] // scheme://host stripped
    [InlineData("/app/", "/app/")]                    // relative href untouched
    [InlineData("https://example.com", "")]           // host with no path -> empty
    public void StripBaseHrefOrigin_MatchesUpstream(string href, string expected)
        => HistoryPathNormalization.StripBaseHrefOrigin(href).ShouldBe(expected);
}

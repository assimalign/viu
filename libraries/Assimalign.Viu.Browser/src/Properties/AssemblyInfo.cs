using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Assimalign.Viu.Browser.Tests")]
// The Router<->DOM bridge's tests synthesize BrowserEvents (whose constructor is internal — in
// production events only come from the dispatch [JSExport]) to drive RouterLink through the bridge
// DOM-free ([V01.01.08.03.01], issue #191).
[assembly: InternalsVisibleTo("Assimalign.Viu.Router.Browser.Tests")]

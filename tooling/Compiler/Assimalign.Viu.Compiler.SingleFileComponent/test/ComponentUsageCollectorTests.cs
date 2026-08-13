using Shouldly;
using Xunit;

using Assimalign.Viu.Syntax;
using Assimalign.Viu.Syntax.Templates;

namespace Assimalign.Viu.Compiler.SingleFileComponent.Tests;

/// <summary>
/// Pins which component-shaped tags participate in static declaration resolution
/// ([SFC-CG-8], [SFC-USE-5], [V01.01.05.11]).
/// </summary>
public sealed class ComponentUsageCollectorTests
{
    [Fact]
    public void Collect_ConfiguredBuiltInAndRuntimeBuiltIns_ExcludesOnlyBuiltIns()
    {
        const string source =
            "<Portal/><Teleport/><Suspense/><KeepAlive/><BaseTransition/>" +
            "<Transition/><TransitionGroup/>" +
            "<component :is=\"Target\"/><Component :is=\"Target\"/><Missing/>";
        var parserOptions = ParserOptions.CreateHtml();
        parserOptions.IsBuiltInComponent = static tag => tag == "Portal";
        var root = TemplateParser.Parse(source, parserOptions);

        var usages = ComponentUsageCollector.Collect(
            root,
            "C:/project/Host.viu",
            new Position(0, 1, 1),
            static tag => tag switch
            {
                "Portal" => HelperNames.Teleport,
                "Transition" => HelperNames.Transition,
                "TransitionGroup" => HelperNames.TransitionGroup,
                _ => null,
            });

        var usage = usages.ShouldHaveSingleItem();
        usage.Tag.ShouldBe("Missing");
    }

    [Fact]
    public void Collect_VueIsCast_ExcludesRuntimeResolvedComponent()
    {
        var root = TemplateParser.Parse(
            "<div is=\"vue:runtime-widget\"></div>",
            ParserOptions.CreateHtml());

        var usages = ComponentUsageCollector.Collect(
            root,
            "C:/project/Host.vue",
            new Position(0, 1, 1));

        usages.Count.ShouldBe(0);
    }
}

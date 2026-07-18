using Shouldly;

using Xunit;

namespace Assimalign.Vue.SingleFileComponent;

// Block options replace Vue's block attributes and are honored per the spec: scoped, module[="name"],
// and lang on @style; lang on @script/@template; custom blocks keep their options. Option semantics
// mirror the Vue SFC spec (https://vuejs.org/api/sfc-spec.html, sfc-css-features).
public class OptionParsingTests
{
    [Fact]
    public void Parse_StyleScoped_SetsScoped()
    {
        var style = SingleFileComponentTestHelpers.Parse("@style scoped {\n}\n").Styles[0];

        style.Scoped.ShouldBeTrue();
        style.IsModule.ShouldBeFalse();
        style.ModuleName.ShouldBeNull();
    }

    [Fact]
    public void Parse_StyleModuleFlag_SetsModuleWithoutName()
    {
        var style = SingleFileComponentTestHelpers.Parse("@style module {\n}\n").Styles[0];

        style.IsModule.ShouldBeTrue();
        style.ModuleName.ShouldBeNull();
    }

    [Fact]
    public void Parse_StyleModuleWithName_SetsModuleName()
    {
        var style = SingleFileComponentTestHelpers.Parse("@style module=\"classes\" {\n}\n").Styles[0];

        style.IsModule.ShouldBeTrue();
        style.ModuleName.ShouldBe("classes");
    }

    [Fact]
    public void Parse_StyleScopedAndLang_HonorsBoth()
    {
        var style = SingleFileComponentTestHelpers.Parse("@style scoped lang=\"scss\" {\n}\n").Styles[0];

        style.Scoped.ShouldBeTrue();
        style.Lang.ShouldBe("scss");
    }

    [Fact]
    public void Parse_ScriptLang_IsHonored()
    {
        SingleFileComponentTestHelpers.Parse("@script lang=\"csharp\" {\n}\n").Script!.Lang.ShouldBe("csharp");
    }

    [Fact]
    public void Parse_TemplateLang_IsHonored()
    {
        SingleFileComponentTestHelpers.Parse("@template lang=\"html\" {\n}\n").Template!.Lang.ShouldBe("html");
    }

    [Fact]
    public void Parse_OptionsPreserveOrderNamesAndValues()
    {
        var style = SingleFileComponentTestHelpers.Parse("@style scoped module=\"m\" lang=\"scss\" {\n}\n").Styles[0];

        style.Options.Count.ShouldBe(3);

        style.Options[0].Name.ShouldBe("scoped");
        style.Options[0].Value.ShouldBeNull();

        style.Options[1].Name.ShouldBe("module");
        style.Options[1].Value.ShouldBe("m");

        style.Options[2].Name.ShouldBe("lang");
        style.Options[2].Value.ShouldBe("scss");
    }

    [Fact]
    public void Parse_CustomBlockOptions_ArePreserved()
    {
        var custom = SingleFileComponentTestHelpers.Parse("@docs lang=\"md\" title=\"Guide\" {\n    text\n}\n").CustomBlocks[0];

        custom.Name.ShouldBe("docs");
        custom.Lang.ShouldBe("md");
        custom.GetOptionValue("title").ShouldBe("Guide");
        custom.HasOption("title").ShouldBeTrue();
        custom.HasOption("missing").ShouldBeFalse();
    }

    [Fact]
    public void Parse_BlockWithNoOptions_HasEmptyOptions()
    {
        SingleFileComponentTestHelpers.Parse("@template {\n}\n").Template!.Options.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_OptionImmediatelyBeforeBrace_NeedsNoSpace()
    {
        // "scoped{" with no separating space still parses the option and the brace.
        var style = SingleFileComponentTestHelpers.Parse("@style scoped{\n}\n").Styles[0];

        style.Scoped.ShouldBeTrue();
        SingleFileComponentTestHelpers.Errors("@style scoped{\n}\n").Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_TabSeparatedHeader_ParsesOptions()
    {
        // Tabs are valid inline whitespace in a header, between the name, options, and the brace.
        var source = "@style\tscoped\t{\n}\n";

        var style = SingleFileComponentTestHelpers.Parse(source).Styles[0];

        style.Scoped.ShouldBeTrue();
        SingleFileComponentTestHelpers.Errors(source).Count.ShouldBe(0);
    }
}

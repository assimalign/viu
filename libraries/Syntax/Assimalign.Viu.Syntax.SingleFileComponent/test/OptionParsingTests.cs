using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.SingleFileComponent;

// The honored (typed) block options, per docs/FORMAT.md: scoped, module[="name"], and lang on
// <style>; lang on <template>; lang on
// @script; custom blocks keep their @-options. The [V01.01.06.10] hybrid container has two header
// grammars — HTML attributes on tag blocks ('"'/'\''/unquoted values, whitespace around '=') and the
// stricter @-options on @-blocks (double-quoted only, no whitespace around '=') — surfacing through
// one identical SingleFileComponentBlockOption record.
public class OptionParsingTests
{
    [Fact]
    public void Parse_StyleScopedAttribute_SetsScoped()
    {
        var style = SingleFileComponentTestHelpers.Parse("<style scoped>\n</style>\n").Styles[0];

        style.Scoped.ShouldBeTrue();
        style.IsModule.ShouldBeFalse();
        style.ModuleName.ShouldBeNull();
    }

    [Fact]
    public void Parse_StyleModuleFlagAttribute_SetsModuleWithoutName()
    {
        var style = SingleFileComponentTestHelpers.Parse("<style module>\n</style>\n").Styles[0];

        style.IsModule.ShouldBeTrue();
        style.ModuleName.ShouldBeNull();
    }

    [Fact]
    public void Parse_StyleModuleWithName_SetsModuleName()
    {
        var style = SingleFileComponentTestHelpers.Parse("<style module=\"classes\">\n</style>\n").Styles[0];

        style.IsModule.ShouldBeTrue();
        style.ModuleName.ShouldBe("classes");
    }

    [Fact]
    public void Parse_StyleScopedAndLang_HonorsBoth()
    {
        var style = SingleFileComponentTestHelpers.Parse("<style scoped lang=\"scss\">\n</style>\n").Styles[0];

        style.Scoped.ShouldBeTrue();
        style.Lang.ShouldBe("scss");
    }

    [Fact]
    public void Parse_ScriptLangOption_IsHonored()
    {
        SingleFileComponentTestHelpers.Parse("@script lang=\"csharp\" {\n}\n").Script!.Lang.ShouldBe("csharp");
    }

    [Fact]
    public void Parse_TemplateLangAttribute_IsHonored()
    {
        SingleFileComponentTestHelpers.Parse("<template lang=\"html\">\n</template>\n").Template!.Lang.ShouldBe("html");
    }

    [Fact]
    public void Parse_TagAttributes_PreserveOrderNamesAndValues()
    {
        var style = SingleFileComponentTestHelpers.Parse("<style scoped module=\"m\" lang=\"scss\">\n</style>\n").Styles[0];

        style.Options.Count.ShouldBe(3);

        style.Options[0].Name.ShouldBe("scoped");
        style.Options[0].Value.ShouldBeNull();

        style.Options[1].Name.ShouldBe("module");
        style.Options[1].Value.ShouldBe("m");

        style.Options[2].Name.ShouldBe("lang");
        style.Options[2].Value.ShouldBe("scss");
    }

    [Fact]
    public void Parse_TagAttributeQuoting_AcceptsSingleDoubleAndUnquotedValues()
    {
        // The tag grammar is the .vue attribute grammar: '\''/'"'/unquoted values and whitespace
        // around '=' are all valid — unlike the @-option grammar.
        var source = "<style lang='scss' module=classes data-x = \"1\">\n</style>\n";

        var style = SingleFileComponentTestHelpers.Parse(source).Styles[0];

        style.Lang.ShouldBe("scss");
        style.ModuleName.ShouldBe("classes");
        style.GetOptionValue("data-x").ShouldBe("1");
        SingleFileComponentTestHelpers.Errors(source).Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_DuplicateTagAttribute_IsReported()
    {
        var result = SingleFileComponentParser.Parse("<style scoped scoped>\n</style>\n");

        result.Descriptor.Styles.Count.ShouldBe(1);
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.DuplicateTagAttribute);
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
    public void Parse_BlocksWithNoOptions_HaveEmptyOptions()
    {
        SingleFileComponentTestHelpers.Parse("<template></template>\n").Template!.Options.Count.ShouldBe(0);
        SingleFileComponentTestHelpers.Parse("@script {\n}\n").Script!.Options.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_AtOptionImmediatelyBeforeBrace_NeedsNoSpace()
    {
        // "csharp"{ with no separating space still parses the @-option and the brace.
        var source = "@script lang=\"csharp\"{\n}\n";

        var script = SingleFileComponentTestHelpers.Parse(source).Script!;

        script.Lang.ShouldBe("csharp");
        SingleFileComponentTestHelpers.Errors(source).Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_TabSeparatedAtHeader_ParsesOptions()
    {
        // Tabs are valid inline whitespace in an @-header, between the name, options, and the brace.
        var source = "@script\tlang=\"csharp\"\t{\n}\n";

        var script = SingleFileComponentTestHelpers.Parse(source).Script!;

        script.Lang.ShouldBe("csharp");
        SingleFileComponentTestHelpers.Errors(source).Count.ShouldBe(0);
    }
}

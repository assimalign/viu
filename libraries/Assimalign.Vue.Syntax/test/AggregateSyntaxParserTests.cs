using System;

using Shouldly;

using Xunit;

namespace Assimalign.Vue.Syntax;

// Pins the AggregateSyntaxParser<T> registration contract ([V01.01.05.09]): embedded sources dispatch
// to the first matching registered parser in node order, nodes without an embedded source are
// skipped, a registration-free parse degrades to the plain container parse, and every entry point —
// including the untyped one build tooling dispatches through — runs the full aggregate pipeline.
public class AggregateSyntaxParserTests
{
    [Fact]
    public void ParseAggregate_NoRegistrations_DegradesToPlainContainerParse()
    {
        var parser = new FakeAggregateParser();

        var result = parser.ParseAggregate("style:a;b|template:x");

        result.SourceResults.Count.ShouldBe(0);
        result.Nodes.Count.ShouldBe(2);
        var derived = result.ShouldBeOfType<FakeAggregateResult>();
        derived.Tag.ShouldBe("from-parse-core");
    }

    [Fact]
    public void ParseAggregate_Registration_DispatchesMatchingSourcesInNodeOrder()
    {
        var options = new AggregateSyntaxParserOptions<FakeBlockNode>();
        options.RegisterParser(static source => source.Name == "style", new FakeSyntaxParser());
        var parser = new FakeAggregateParser(options);

        var result = parser.ParseAggregate("style:a;b|template:x|style:c");

        result.Nodes.Count.ShouldBe(3);
        result.SourceResults.Count.ShouldBe(2);

        var first = result.SourceResults[0];
        first.Node.Name.ShouldBe("style");
        first.Source.Text.ShouldBe("a;b");
        first.Result.Nodes.Count.ShouldBe(2);

        var second = result.SourceResults[1];
        second.Node.Content.ShouldBe("c");
        second.Result.Nodes.Count.ShouldBe(1);

        // The with-clone that attaches the dispatched results preserves the derived result record.
        result.ShouldBeOfType<FakeAggregateResult>().Tag.ShouldBe("from-parse-core");
    }

    [Fact]
    public void ParseAggregate_FirstMatchingRegistrationWins()
    {
        var options = new AggregateSyntaxParserOptions<FakeBlockNode>();
        options.RegisterParser(static source => source.Name == "style", new FakeSyntaxParser(tag: "first"));
        options.RegisterParser(static _ => true, new FakeSyntaxParser(tag: "second"));
        var parser = new FakeAggregateParser(options);

        var result = parser.ParseAggregate("style:a|template:x");

        result.SourceResults.Count.ShouldBe(2);
        result.SourceResults[0].Result.ShouldBeOfType<FakeSyntaxParserResult>().Tag.ShouldBe("first");
        result.SourceResults[1].Result.ShouldBeOfType<FakeSyntaxParserResult>().Tag.ShouldBe("second");
    }

    [Fact]
    public void ParseAggregate_NodeWithoutEmbeddedSource_IsSkipped()
    {
        var options = new AggregateSyntaxParserOptions<FakeBlockNode>();
        options.RegisterParser(static _ => true, new FakeSyntaxParser());
        var parser = new FakeAggregateParser(options);

        var result = parser.ParseAggregate("opaque:z|style:a");

        result.Nodes.Count.ShouldBe(2);
        var dispatched = result.SourceResults.ShouldHaveSingleItem();
        dispatched.Node.Name.ShouldBe("style");
    }

    [Fact]
    public void Parse_UntypedEntryPoint_RunsTheFullAggregatePipeline()
    {
        var options = new AggregateSyntaxParserOptions<FakeBlockNode>();
        options.Analyzers.Add(new FakeBlockAnalyzer());
        options.RegisterParser(static source => source.Name == "style", new FakeSyntaxParser());
        SyntaxParser parser = new FakeAggregateParser(options);

        var result = parser.Parse("style:a|template:x");

        var aggregate = result.ShouldBeOfType<FakeAggregateResult>();
        aggregate.SourceResults.ShouldHaveSingleItem().Source.Name.ShouldBe("style");
        aggregate.Diagnostics.ShouldHaveSingleItem().Message.ShouldBe("from-analyzer");
        aggregate.Tag.ShouldBe("from-parse-core");
    }

    [Fact]
    public void RegisterParser_NullArguments_Throw()
    {
        var options = new AggregateSyntaxParserOptions<FakeBlockNode>();

        Should.Throw<ArgumentNullException>(() => options.RegisterParser(null!, new FakeSyntaxParser()));
        Should.Throw<ArgumentNullException>(() => options.RegisterParser(static _ => true, null!));
    }

    [Fact]
    public void ParseAggregate_EqualInput_YieldsEqualResults()
    {
        var options = new AggregateSyntaxParserOptions<FakeBlockNode>();
        options.RegisterParser(static source => source.Name == "style", new FakeSyntaxParser());
        var parser = new FakeAggregateParser(options);

        var first = parser.ParseAggregate("style:a|template:x");
        var second = parser.ParseAggregate("style:a|template:x");

        // The incremental-caching contract holds through the aggregate layer: equal input yields
        // equal, equally-hashed results, dispatched inner results included.
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }
}

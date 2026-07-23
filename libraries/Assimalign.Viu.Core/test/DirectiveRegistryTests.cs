using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Core.Tests;

public sealed class DirectiveRegistryTests
{
    [Fact]
    public void Resolve_HyphenatedName_ResolvesRawCamelAndPascalRegistrations()
    {
        IDirective raw = new Directive();
        IDirective camel = new Directive();
        IDirective pascal = new Directive();
        DirectiveRegistry registry = new(
        [
            new KeyValuePair<string, IDirective>("raw-directive", raw),
            new KeyValuePair<string, IDirective>("camelDirective", camel),
            new KeyValuePair<string, IDirective>("PascalDirective", pascal),
        ]);

        registry.Resolve("raw-directive").ShouldBeSameAs(raw);
        registry.Resolve("camel-directive").ShouldBeSameAs(camel);
        registry.Resolve("pascal-directive").ShouldBeSameAs(pascal);
    }

    [Fact]
    public void Resolve_AliasEquivalentRegistrations_PrefersRawThenCamelThenPascal()
    {
        IDirective raw = new Directive();
        IDirective camel = new Directive();
        IDirective pascal = new Directive();
        DirectiveRegistry registry = new(
        [
            new KeyValuePair<string, IDirective>("my-directive", raw),
            new KeyValuePair<string, IDirective>("myDirective", camel),
            new KeyValuePair<string, IDirective>("MyDirective", pascal),
        ]);
        DirectiveRegistry fallbackRegistry = new(
        [
            new KeyValuePair<string, IDirective>("myDirective", camel),
            new KeyValuePair<string, IDirective>("MyDirective", pascal),
        ]);

        registry.Resolve("my-directive").ShouldBeSameAs(raw);
        registry.Resolve("myDirective").ShouldBeSameAs(camel);
        registry.Resolve("MyDirective").ShouldBeSameAs(pascal);
        fallbackRegistry.Resolve("my-directive").ShouldBeSameAs(camel);
    }

    [Fact]
    public void Constructor_DuplicateRawName_Throws()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(
            () =>
            {
                _ = new DirectiveRegistry(
                [
                    new KeyValuePair<string, IDirective>(
                        "duplicate-directive",
                        new Directive()),
                    new KeyValuePair<string, IDirective>(
                        "duplicate-directive",
                        new Directive()),
                ]);
            });

        exception.Message.ShouldContain("registered more than once");
    }
}

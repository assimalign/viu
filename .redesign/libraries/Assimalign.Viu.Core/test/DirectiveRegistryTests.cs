using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu;

namespace Assimalign.Viu.Core.Tests;

public sealed class DirectiveRegistryTests
{
    [Fact]
    public void Resolve_RegisteredTypeToken_ReturnsBorrowedDirective()
    {
        IDirective directive = new Directive();
        var registry = new DirectiveRegistry(
        [
            new KeyValuePair<Type, IDirective>(typeof(DirectiveToken), directive),
        ]);

        registry.Resolve(typeof(DirectiveToken)).ShouldBeSameAs(directive);
        registry.Resolve(typeof(UnregisteredDirectiveToken)).ShouldBeNull();
    }

    [Fact]
    public void Constructor_DuplicateTypeToken_Throws()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(
            () => _ = new DirectiveRegistry(
            [
                new KeyValuePair<Type, IDirective>(
                    typeof(DirectiveToken),
                    new Directive()),
                new KeyValuePair<Type, IDirective>(
                    typeof(DirectiveToken),
                    new Directive()),
            ]));

        exception.Message.ShouldContain("registered more than once");
    }

    private sealed class DirectiveToken
    {
    }

    private sealed class UnregisteredDirectiveToken
    {
    }
}

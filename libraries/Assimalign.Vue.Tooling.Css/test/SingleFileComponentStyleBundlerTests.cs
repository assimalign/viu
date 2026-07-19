using System;

using Assimalign.Vue.Tooling.Css;

using Shouldly;

using Xunit;

namespace Assimalign.Vue.Tooling.Css.Tests;

/// <summary>
/// Pins the <see cref="SingleFileComponentStyleBundler"/> — the deterministic per-component <c>@style</c>
/// bundle the <c>VuecsBundleCss</c> task writes ([V01.01.12.12]). Fixes the ordering, the LF-only layout, and
/// the byte-identical-to-generated-constant contract (each component's segment is the exact string the
/// generator emits as <c>ExtractedStyles</c>, because both call the same compiler).
/// </summary>
public sealed class SingleFileComponentStyleBundlerTests
{
    private const string ProjectDirectory = "C:/proj";

    private static readonly SingleFileComponentStyleInput Card = new(
        "C:/proj/Components/Card.viu", "@style scoped {\n    .card { color: red; }\n}\n");

    private static readonly SingleFileComponentStyleInput Panel = new(
        "C:/proj/Components/Panel.viu", "@style scoped {\n    .panel { color: blue; }\n}\n");

    /// <summary>Components are ordered by ascending project-relative path, independent of input order.</summary>
    [Fact]
    public void Bundle_OrdersComponentsByProjectRelativePath()
    {
        // Panel before Card in input order; Card must still come first (ordinal path order).
        var bundle = SingleFileComponentStyleBundler.Bundle(new[] { Panel, Card }, ProjectDirectory);

        bundle.ShouldNotBeNull();
        bundle!.IndexOf("Components/Card.viu", StringComparison.Ordinal)
            .ShouldBeLessThan(bundle.IndexOf("Components/Panel.viu", StringComparison.Ordinal));
    }

    /// <summary>The bundle is LF-only (no carriage returns), so it is byte-stable across platforms.</summary>
    [Fact]
    public void Bundle_IsLfOnly()
    {
        var bundle = SingleFileComponentStyleBundler.Bundle(new[] { Card, Panel }, ProjectDirectory);

        bundle.ShouldNotBeNull();
        bundle!.ShouldNotContain("\r");
    }

    /// <summary>
    /// Each component's segment is byte-identical to the string the generator emits as its
    /// <c>ExtractedStyles</c> constant — both come from the one shared compiler over the same input.
    /// </summary>
    [Fact]
    public void Bundle_SegmentIsByteIdenticalToCompilerOutput()
    {
        var compiled = SingleFileComponentStyleCompiler.CompileFile(
            SingleFileComponentParserFactory.CreateForStyleExtraction(), Card.Text, Card.FilePath, ProjectDirectory);

        var bundle = SingleFileComponentStyleBundler.Bundle(new[] { Card, Panel }, ProjectDirectory);

        bundle.ShouldNotBeNull();
        compiled.ExtractedStyles.ShouldNotBeNull();
        bundle!.ShouldContain(compiled.ExtractedStyles!);
    }

    /// <summary>Identical inputs (in any order) produce the identical bundle string — the incremental-cache contract.</summary>
    [Fact]
    public void Bundle_IsDeterministicAndOrderIndependent()
    {
        var forward = SingleFileComponentStyleBundler.Bundle(new[] { Card, Panel }, ProjectDirectory);
        var reversed = SingleFileComponentStyleBundler.Bundle(new[] { Panel, Card }, ProjectDirectory);

        forward.ShouldBe(reversed);
    }

    /// <summary>A project whose components declare no <c>@style</c> block bundles to <see langword="null"/> (no asset written).</summary>
    [Fact]
    public void Bundle_NoStyleBlocks_ReturnsNull()
    {
        var styleless = new SingleFileComponentStyleInput("C:/proj/Components/NoStyle.viu", "@template {\n    <div>ok</div>\n}\n");

        SingleFileComponentStyleBundler.Bundle(new[] { styleless }, ProjectDirectory).ShouldBeNull();
    }

    /// <summary>An empty component set bundles to <see langword="null"/>.</summary>
    [Fact]
    public void Bundle_EmptyInput_ReturnsNull()
        => SingleFileComponentStyleBundler.Bundle(Array.Empty<SingleFileComponentStyleInput>(), ProjectDirectory).ShouldBeNull();
}

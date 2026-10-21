using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Components.Tests;

public sealed class GeneratedCatalogTests
{
    [Fact]
    public void TypedNode_UsesDefinitionReferenceWithoutConstructingAComponent()
    {
        // [CMP-35] The static contract describes identity without activation.
        ComponentNode.For<Definition>().Component.ShouldBeSameAs(Definition.Registration.Reference);
    }

    [Fact]
    public void GeneratedCatalogs_ApplyInOrdinalOrderOncePerFactory()
    {
        // [CMP-36] Diamond paths and repeated calls cannot replay a catalog into one factory.
        List<string> calls = [];
        GeneratedComponentCatalogs.Register("catalog-test-b", _ => calls.Add("b"));
        GeneratedComponentCatalogs.Register("catalog-test-a", _ => calls.Add("a"));
        try
        {
            ComponentFactory factory = new();
            factory.RegisterGeneratedComponents();
            factory.RegisterGeneratedComponents();
            calls.ShouldBe(["a", "b"]);
            new ComponentFactory().RegisterGeneratedComponents();
            calls.ShouldBe(["a", "b", "a", "b"]);
        }
        finally
        {
            GeneratedComponentCatalogs.Register("catalog-test-a", _ => { });
            GeneratedComponentCatalogs.Register("catalog-test-b", _ => { });
        }
    }

    [Fact]
    public void CatalogPublication_DuringPopulation_UsesSnapshotAndNextCallSeesNewCatalog()
    {
        // [CMP-36] An opted-in module can publish while its catalog delegate is first invoked.
        int lateCalls = 0;
        GeneratedComponentCatalogs.Register("catalog-test-publisher", _ =>
            GeneratedComponentCatalogs.Register("catalog-test-late", _ => lateCalls++));
        try
        {
            ComponentFactory factory = new();
            factory.RegisterGeneratedComponents();
            lateCalls.ShouldBe(0);
            factory.RegisterGeneratedComponents();
            lateCalls.ShouldBe(1);
        }
        finally
        {
            GeneratedComponentCatalogs.Register("catalog-test-publisher", _ => { });
            GeneratedComponentCatalogs.Register("catalog-test-late", _ => { });
        }
    }

    [Fact]
    public void CatalogReplacement_ExistingFactoryRetainsRegistrationsAndNewFactorySeesUpdatedCatalog()
    {
        // [CMP-36] Hot reload keeps live registration identity; new composition observes new catalogs.
        ComponentRegistration first = ComponentRegistration.Define("catalog-replacement-first",
            new ComponentContract(), _ => _ => null);
        ComponentRegistration second = ComponentRegistration.Define("catalog-replacement-second",
            new ComponentContract(), _ => _ => null);
        GeneratedComponentCatalogs.Register("catalog-test-replacement", factory => factory.Register(first));
        try
        {
            ComponentFactory existing = new();
            existing.RegisterGeneratedComponents();
            GeneratedComponentCatalogs.Register("catalog-test-replacement", factory => factory.Register(second));
            existing.RegisterGeneratedComponents();
            existing.Resolve(first.Reference).ShouldBeSameAs(first);
            existing.TryResolve(second.Reference, out _).ShouldBeFalse();
            ComponentFactory updated = new();
            updated.RegisterGeneratedComponents();
            updated.Resolve(second.Reference).ShouldBeSameAs(second);
        }
        finally
        {
            GeneratedComponentCatalogs.Register("catalog-test-replacement", _ => { });
        }
    }

    [Fact]
    public void DifferentCatalogs_WithConflictingReferences_Throw()
    {
        ComponentFactory factory = new();
        factory.RegisterGeneratedCatalog("first", value => value.Register(Definition.Registration));
        Should.Throw<ArgumentException>(() => factory.RegisterGeneratedCatalog("second",
            value => value.Register(Definition.Registration)));
    }

    private sealed class Definition : IComponentDefinition
    {
        public static ComponentRegistration Registration { get; } = ComponentRegistration.Define(
            "GeneratedCatalogDefinition", new ComponentContract(), _ => _ => new TextNode("defined"));
    }
}

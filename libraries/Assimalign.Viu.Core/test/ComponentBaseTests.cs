using Shouldly;
using Xunit;

using Assimalign.Viu;

namespace Assimalign.Viu.Tests;

// Pins the Component authoring base's lazy Configure contract (reshape arc 2 R7,
// docs/NET-RESHAPE-PLAN.md; ADR-0004 composition-only descriptor — props/emits are definition-time
// metadata only). Upstream analogue: defineComponent's props/emits options
// (vuejs/core packages/runtime-core/src/apiDefineComponent.ts).
public class ComponentBaseTests
{
    // A Component-base authored component that counts Configure invocations and registers one prop
    // and one emit through the descriptor.
    private sealed class ConfiguredComponent : Component
    {
        public int ConfigureRuns { get; private set; }

        protected override void Configure(IComponentDescriptor descriptor)
        {
            ConfigureRuns++;
            descriptor
                .WithProperty(new ComponentPropertyDefinition("value"))
                .WithEmit(new ComponentEmitDefinition("change"));
        }

        public override ComponentSetup Setup(ComponentProperties properties, ComponentSetupContext context)
            => static () => null;
    }

    [Fact]
    public void Configure_DoesNotRun_DuringConstruction()
    {
        var component = new ConfiguredComponent();

        // The base defers Configure past the constructor: a virtual call from the ctor would run
        // this override before the derived ctor body finished. Nothing has read metadata yet, so
        // Configure has not run.
        component.ConfigureRuns.ShouldBe(0);
    }

    [Fact]
    public void Configure_RunsOnce_OnFirstPropertiesAccess()
    {
        var component = new ConfiguredComponent();

        var properties = component.Properties;

        component.ConfigureRuns.ShouldBe(1);
        properties.Count.ShouldBe(1);
        properties[0].Name.ShouldBe("value");
    }

    [Fact]
    public void Configure_RunsOnce_OnFirstEmitsAccess()
    {
        var component = new ConfiguredComponent();

        var emits = component.Emits;

        component.ConfigureRuns.ShouldBe(1);
        emits.Count.ShouldBe(1);
        emits[0].Name.ShouldBe("change");
    }

    [Fact]
    public void Configure_RunsExactlyOnce_AcrossRepeatedMetadataReads()
    {
        var component = new ConfiguredComponent();

        _ = component.Properties;
        _ = component.Emits;
        _ = component.Properties;
        _ = component.Emits;

        // A single bool guard, set before the call, makes Configure run exactly once regardless of
        // which metadata member is read first or how often either is read (single-threaded model).
        component.ConfigureRuns.ShouldBe(1);
    }

    [Fact]
    public void InheritAttributes_DefaultsTrue_WithoutTriggeringConfigure()
    {
        var component = new ConfiguredComponent();

        component.InheritAttributes.ShouldBeTrue();

        // InheritAttributes is not descriptor-registered, so reading it must not force Configure.
        component.ConfigureRuns.ShouldBe(0);
    }
}

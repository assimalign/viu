using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>
/// Defines autonomous HTML custom elements backed by explicitly registered Viu components.
/// One owner shares a buffered renderer across every definition and connected instance.
/// </summary>
/// <remarks>
/// Await <see cref="BrowserRuntime.InitializeAsync"/> before construction. The owner borrows its
/// component factory and services; dispose it to unmount every instance and disable its definitions.
/// Browser registries cannot unregister names. This owner holds the exclusive Browser renderer
/// lease and is single-threaded, browser-main-thread-only, and not thread-safe.
/// Specified by <c>[CEL-1]</c>, <c>[CEL-2]</c>, and <c>[CEL-8]</c> ([V01.01.04.08]).
/// </remarks>
[SupportedOSPlatform("browser")]
public sealed class BrowserCustomElements : IDisposable
{
    private static int _nextDefinitionIdentifier;
    private readonly IComponentFactory _components;
    private readonly IServiceProvider? _services;
    private readonly BufferedBrowserNodeOperations _operations;
    private readonly Renderer<int> _renderer;
    private readonly IDisposable _activation;
    private readonly Dictionary<int, CustomElementDefinition> _definitions = [];
    private readonly Dictionary<int, CustomElementInstance> _instances = [];
    private bool _disposed;

    internal static BrowserCustomElements? Active { get; private set; }

    /// <summary>
    /// Acquires one renderer lease for all definitions, borrowing the explicit resolvers without
    /// activating a component. Specified by <c>[CEL-1]</c> and <c>[CMP-9]</c>.
    /// </summary>
    /// <param name="componentFactory">The explicit resolver containing each component registration.</param>
    /// <param name="services">Optional borrowed services available during component setup.</param>
    /// <exception cref="InvalidOperationException">The bridge is uninitialized or a renderer is active.</exception>
    public BrowserCustomElements(IComponentFactory componentFactory, IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(componentFactory);
        BrowserRuntime.EnsureBridgeInitialized();
        _components = new BrowserComponentFactory(componentFactory);
        _services = services;
        _operations = BufferedBrowserNodeOperations.CreateProduction();
        _renderer = RendererFactory.CreateRenderer(_operations.Create());
        _activation = _operations.Activate();
        try
        {
            _operations.ErrorSink = exception =>
                BrowserCustomElementInterop.Warn(exception.ToString());
            Active = this;
            BrowserCustomElementInterop.SetReady(true);
        }
        catch
        {
            if (ReferenceEquals(Active, this))
            {
                Active = null;
            }

            _activation.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Registers one custom-element class from the component's static parameter table, without
    /// reflection or per-component JavaScript. Options are snapshotted before registration.
    /// Specified by <c>[CEL-1]</c>, <c>[CEL-3]</c>, and <c>[CEL-4]</c>.
    /// </summary>
    /// <param name="component">The reference resolved through the borrowed component factory.</param>
    /// <param name="tagName">A valid WHATWG custom-element name, including a hyphen.</param>
    /// <param name="options">Optional shadow-root and attribute-name policies.</param>
    /// <exception cref="ArgumentException">A tag or mapped attribute name is invalid or ambiguous.</exception>
    /// <exception cref="InvalidOperationException">The component is unavailable or the tag is already defined.</exception>
    /// <exception cref="ObjectDisposedException">This owner has been disposed.</exception>
    public void Define(ComponentReference component, string tagName, CustomElementOptions? options = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(component);
        CustomElementValues.ValidateTagName(tagName);
        ComponentContract contract = _components.Resolve(component).Contract;
        CustomElementDefinition definition = CustomElementDefinition.Create(
            component,
            contract,
            options);

        int identifier = checked(++_nextDefinitionIdentifier);
        _definitions.Add(identifier, definition);
        try
        {
            BrowserCustomElementInterop.Define(
                identifier,
                tagName,
                definition.UseShadowRoot,
                definition.ParameterNames,
                definition.AttributeNames);
        }
        catch (JSException exception)
        {
            _definitions.Remove(identifier);
            throw new InvalidOperationException($"Cannot define custom element '{tagName}': {exception.Message}", exception);
        }
        catch
        {
            _definitions.Remove(identifier);
            throw;
        }
    }

    /// <summary>
    /// Unmounts all owned instances, releases their handles and listeners, and permanently disables
    /// the definitions. Repeated calls do nothing; factories and services remain caller-owned.
    /// Specified by <c>[CEL-8]</c>.
    /// </summary>
    /// <exception cref="AggregateException">
    /// One or more host resources failed to release after every owned resource was attempted.
    /// </exception>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        List<Exception>? failures = null;
        try
        {
            TryCleanup(
                static () => BrowserCustomElementInterop.SetReady(false),
                ref failures);
            foreach (int definition in _definitions.Keys)
            {
                TryCleanup(
                    () => BrowserCustomElementInterop.Undefine(definition),
                    ref failures);
            }

            foreach (int identifier in new List<int>(_instances.Keys))
            {
                TryCleanup(() => Disconnect(identifier), ref failures);
            }

            _definitions.Clear();
        }
        finally
        {
            if (ReferenceEquals(Active, this))
            {
                Active = null;
            }

            TryCleanup(_activation.Dispose, ref failures);
        }

        if (failures is not null)
        {
            throw new AggregateException(
                "One or more custom-element resources could not be released.",
                failures);
        }
    }

    internal void Dispatch(
        int[] operations,
        int[] definitionIdentifiers,
        int[] elementIdentifiers,
        int[] containers,
        string[] names,
        object?[] values)
    {
        if (_disposed)
        {
            return;
        }
        // Observe the entire batch before allocating: multiple JS roots can precede any .NET render.
        foreach (int container in containers)
        {
            _operations.ObserveForeignHandle(container);
        }

        for (int index = 0; index < operations.Length; index++)
        {
            int identifier = elementIdentifiers[index];
            try
            {
                if (operations[index] == 2)
                {
                    Disconnect(identifier);
                    continue;
                }

                if (operations[index] == 1)
                {
                    if (!_definitions.TryGetValue(definitionIdentifiers[index], out CustomElementDefinition? definition))
                    {
                        continue;
                    }

                    Connect(identifier, containers[index], definition, names[index]);
                }
                else if (_instances.TryGetValue(identifier, out CustomElementInstance? instance))
                {
                    if (operations[index] is 3 or 4)
                    {
                        Change(instance, names[index], values[index], operations[index] == 3);
                    }
                    else if (operations[index] == 6)
                    {
                        SetSlots(instance, names[index]);
                        Scheduler.QueueJob(instance.Job);
                    }
                    // Adoption itself is observable through disconnect + fresh connect. The callback
                    // also forwards operation 5, which requires no additional managed mutation.
                }
            }
            catch (Exception exception)
            {
                BrowserCustomElementInterop.Warn(
                    $"Custom element {identifier}: {exception.Message}");
                try
                {
                    Disconnect(identifier);
                }
                catch (Exception cleanupException)
                {
                    BrowserCustomElementInterop.Warn(
                        $"Custom element {identifier} cleanup: {cleanupException.Message}");
                }
            }
        }
    }

    private void Connect(int identifier, int container, CustomElementDefinition definition, string slotNames)
    {
        if (_instances.ContainsKey(identifier))
        {
            return;
        }

        CustomElementInstance? instance = null;
        ApplicationContext application = new(new ApplicationOptions
        {
            RootComponent = new ComponentNode(definition.Component),
            Components = _components,
            Services = _services,
            Directives = BrowserDirectiveResolver.Instance,
            WarnHandler = BrowserCustomElementInterop.Warn,
            ErrorHandler = (exception, _, phase) => BrowserCustomElementInterop.Warn($"{phase}: {exception.Message}"),
            EventObserver = (context, name, arguments) =>
            {
                if (!_disposed && context.Parent is null)
                {
                    Emit(identifier, name, arguments);
                }
            },
        });
        instance = new CustomElementInstance
        {
            Identifier = identifier,
            Container = container,
            Definition = definition,
            Application = application,
            Job = new SchedulerJob(() => Render(instance!)) { Name = "custom element inputs" },
        };
        SetSlots(instance, slotNames);
        _instances.Add(identifier, instance);
        Scheduler.QueueJob(instance.Job);
    }

    private static void SetSlots(CustomElementInstance instance, string slotNames)
    {
        if (!instance.Definition.UseShadowRoot)
        {
            return;
        }

        instance.Slots.Clear();
        // JSON is only a string-list transport, read directly; no reflective serialization.
        using JsonDocument document = JsonDocument.Parse(slotNames);
        foreach (JsonElement entry in document.RootElement.EnumerateArray())
        {
            string name = entry.GetString() ?? string.Empty;
            instance.Slots[name.Length == 0 ? "default" : name] = _ => new ElementNode(
                new QualifiedName("slot"), name.Length == 0 ? null : [ElementBinding.Attribute(new QualifiedName("name"), name)]);
        }
    }

    private static void Change(CustomElementInstance instance, string name, object? input, bool attribute)
    {
        if (!instance.Definition.Parameters.TryGetValue(name, out ComponentParameter? parameter))
        {
            return;
        }

        object? value;
        bool accepted;
        if (attribute
            && input is null
            && CustomElementValues.ValueType(parameter.ParameterType) != typeof(bool))
        {
            if (instance.Parameters.Remove(name))
            {
                Scheduler.QueueJob(instance.Job);
            }

            return;
        }
        if (attribute)
        {
            accepted = CustomElementValues.TryAttribute(parameter.ParameterType, input as string, out value);
        }
        else if (parameter.ParameterType == typeof(JSObject))
        {
            value = input;
            accepted = input is null or JSObject;
        }
        else
        {
            accepted = CustomElementValues.TryProperty(parameter.ParameterType, input, out value);
        }

        if (!accepted)
        {
            if (input is JSObject rejectedObject)
            {
                rejectedObject.Dispose();
            }

            BrowserCustomElementInterop.Warn(
                $"Invalid {(attribute ? "attribute" : "property")} value for "
                    + $"custom-element parameter '{name}'; unchanged.");
            // Restore a rejected property write to the last accepted value in the shared JS cache.
            object? previous;
            if (!instance.Parameters.TryGetValue(name, out previous)
                && (instance.Context is null
                    || !instance.Context.Bindings.Parameters.TryGetValue(name, out previous)))
            {
                previous = null;
            }

            BrowserCustomElementInterop.UpdateProperties(
                instance.Identifier,
                [name],
                [ToInteropValue(previous)]);
            return;
        }

        if (input is JSObject javascriptObject)
        {
            instance.Objects.Add(javascriptObject);
        }

        if (instance.Parameters.TryGetValue(name, out object? existing) && Equals(existing, value))
        {
            return;
        }

        instance.Parameters[name] = value;
        Scheduler.QueueJob(instance.Job);
    }

    private void Render(CustomElementInstance instance)
    {
        if (instance.Job.IsDisposed)
        {
            return;
        }

        try
        {
            ComponentInvocation invocation = new(
                instance.Parameters,
                instance.Slots,
                slotStability: SlotStability.Dynamic);
            instance.Context = _renderer.Render(
                new ComponentNode(instance.Definition.Component, invocation),
                instance.Container,
                instance.Application);
            List<string> names = [];
            List<object?> values = [];
            foreach (ComponentParameter parameter in instance.Definition.Contract.Parameters)
            {
                names.Add(parameter.Name);
                values.Add(ToInteropValue(instance.Context?.Bindings.Parameters.GetValueOrDefault(parameter.Name)));
            }
            BrowserCustomElementInterop.UpdateProperties(
                instance.Identifier,
                names.ToArray(),
                values.ToArray());
            foreach (JSObject previous in new List<JSObject>(instance.Objects))
            {
                if (!instance.Parameters.ContainsValue(previous))
                {
                    instance.Objects.Remove(previous);
                    previous.Dispose();
                }
            }
        }
        catch (Exception exception)
        {
            BrowserCustomElementInterop.Warn($"Custom-element render failed: {exception.Message}");
            try
            {
                Disconnect(instance.Identifier);
            }
            catch (Exception cleanupException)
            {
                BrowserCustomElementInterop.Warn(
                    $"Custom-element render cleanup failed: {cleanupException.Message}");
            }
        }
    }

    private void Disconnect(int identifier)
    {
        if (!_instances.Remove(identifier, out CustomElementInstance? instance))
        {
            return;
        }

        instance.Job.IsDisposed = true;
        try
        {
            _renderer.Render(null, instance.Container, instance.Application);
            _operations.ApplyPending();
        }
        finally
        {
            try
            {
                _operations.Invokers.PurgeReleasedHandles(BrowserCustomElementInterop.Release(identifier));
            }
            finally
            {
                foreach (JSObject value in instance.Objects)
                {
                    value.Dispose();
                }

                instance.Objects.Clear();
            }
        }
    }

    private static void Emit(int identifier, string name, IReadOnlyList<object?> arguments)
    {
        object?[] values = new object?[arguments.Count];
        for (int index = 0; index < values.Length; index++)
        {
            values[index] = ToInteropValue(arguments[index]);
        }

        BrowserCustomElementInterop.DispatchEvent(identifier, name, values);
    }

    private static object? ToInteropValue(object? value)
    {
        // The same primitive/string boundary used by Browser event and property operations.
        // Arbitrary managed objects never enter a serializer or invoke user conversion code [CEL-5].
        switch (value)
        {
            case null or string or bool or double or JSObject: return value;
            case char character: return character.ToString();
            case byte or sbyte or short or ushort or int or uint or long or ulong or float or decimal:
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            case Half number: return (double)number;
            case Int128 number: return (double)number;
            case UInt128 number: return (double)number;
            case nint number: return (double)number;
            case nuint number: return (double)number;
            default:
                BrowserCustomElementInterop.Warn("A custom-element value is not a supported interop primitive or JavaScript object; using null.");
                return null;
        }
    }

    private static void TryCleanup(Action cleanup, ref List<Exception>? failures)
    {
        try
        {
            cleanup();
        }
        catch (Exception exception)
        {
            failures ??= [];
            failures.Add(exception);
        }
    }
}

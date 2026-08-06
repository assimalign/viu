using System;
using System.Collections.Generic;
using System.ComponentModel;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Browser;

/// <summary>
/// Compiler-generated code only; not part of the supported Viu API.
/// Applies generated single-file-component <c>v-bind()</c> values as CSS custom properties.
/// </summary>
/// <remarks>
/// A <c>v-bind()</c> in a component's style block compiles to a getter over the component's reactive
/// state; this type is what turns each evaluated result into a CSS custom property on the
/// component's outermost elements, so the stylesheet rule that reads the variable updates without
/// re-rendering the component at all. The component context is passed explicitly — Browser never
/// reads an ambient component instance, because there is none to read in an AOT build. Specified by
/// <c>[STY-6]</c> through <c>[STY-8]</c>.
/// </remarks>
[System.ComponentModel.EditorBrowsable(EditorBrowsableState.Never)]
public static class CssVariables
{
    /// <summary>
    /// Compiler-generated code only; not part of the supported Viu API.
    /// Registers a component's generated CSS-variable getter against its current outermost browser
    /// elements.
    /// </summary>
    /// <remarks>
    /// Call once during <see cref="IComponentTemplate.Setup(IComponentContext)"/>. The getter runs
    /// after mount, reruns in the post-flush phase when a reactive value it reads changes, and is
    /// reapplied after component updates in case the rendered root elements changed. Every root
    /// receives all custom properties in one browser operation. The watcher stops before component
    /// teardown.
    /// </remarks>
    /// <param name="context">The component context supplied to setup.</param>
    /// <param name="getter">
    /// Produces hashed custom-property names without the leading <c>--</c> and their current values.
    /// </param>
    [System.ComponentModel.EditorBrowsable(EditorBrowsableState.Never)]
    public static void UseCssVariables(
        IComponentContext context,
        Func<IReadOnlyDictionary<string, string>> getter)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(getter);

        void Apply()
        {
            IReadOnlyDictionary<string, string> variables =
                getter()
                ?? throw new InvalidOperationException(
                    "The CSS variable getter returned null.");
            IReadOnlyList<int> roots =
                ComponentHost.GetRootElements<int>(context);
            bool applied = false;
            for (int index = 0; index < roots.Count; index++)
            {
                applied |= ApplyToElement(roots[index], variables);
            }

            if (applied)
            {
                ComponentHost.QueueHostCommit(context);
            }
        }

        context.Lifecycle.OnUpdated(Apply);
        context.Lifecycle.OnMounted(
            () =>
            {
                WatchHandle watcher =
                    ViuWatch.WatchEffect(
                        Apply,
                        new WatchOptions
                        {
                            Flush = WatchFlushMode.Post,
                        });
                context.Lifecycle.OnBeforeUnmount(watcher.Stop);
            });
    }

    private static bool ApplyToElement(
        int element,
        IReadOnlyDictionary<string, string> variables)
    {
        if (variables.Count == 0
            || BrowserDirectiveOperations.Current is not { } operations)
        {
            return false;
        }

        string[] names = new string[variables.Count];
        string[] values = new string[variables.Count];
        int index = 0;
        foreach (KeyValuePair<string, string> variable in variables)
        {
            names[index] = "--" + variable.Key;
            values[index] = variable.Value;
            index++;
        }

        operations.SetCssVariables(element, names, values);
        return true;
    }
}

using System;
using System.Collections.Generic;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Browser;

/// <summary>
/// Applies generated single-file-component <c>v-bind()</c> values as CSS custom properties.
/// </summary>
/// <remarks>
/// This is Viu's C# port of Vue 3.5's <c>useCssVars</c>:
/// https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-dom/src/helpers/useCssVars.ts.
/// The component context is explicit; Browser never reads an ambient component instance.
/// </remarks>
public static class CssVariables
{
    /// <summary>
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

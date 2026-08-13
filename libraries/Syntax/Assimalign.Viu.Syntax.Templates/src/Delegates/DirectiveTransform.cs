using System;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// A transform that translates a single directive on an element into virtual-node properties (and optionally a
/// runtime directive). Directive transforms are keyed by directive name and run
/// during element prop building. A platform compiler can wrap a base transform through the
/// <paramref name="augmentor"/>.
/// </summary>
/// <param name="directive">The directive being transformed.</param>
/// <param name="element">The element the directive is on.</param>
/// <param name="context">The active transform context.</param>
/// <param name="augmentor">An optional wrapper that a platform-specific transform applies to the base result.</param>
/// <returns>The directive's contributed properties and runtime needs.</returns>
public delegate DirectiveTransformResult DirectiveTransform(
    DirectiveNode directive,
    ElementNode element,
    TransformContext context,
    Func<DirectiveTransformResult, DirectiveTransformResult>? augmentor);

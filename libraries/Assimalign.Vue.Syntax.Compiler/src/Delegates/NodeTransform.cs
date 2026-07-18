using System;

namespace Assimalign.Vue.Syntax.Compiler;

/// <summary>
/// A transform that operates directly on a node, optionally replacing or removing it via the
/// <see cref="TransformContext"/>. The C# port of Vue 3.5's <c>NodeTransform</c> function type
/// (<c>@vue/compiler-core</c> <c>transform.ts</c>). Node transforms run in registration order on entry and
/// their returned exit callbacks run in reverse on exit, after all children have been transformed.
/// </summary>
/// <param name="node">The node being visited (a template node or a transform-introduced container).</param>
/// <param name="context">The active transform context.</param>
/// <returns>An exit callback to run after the node's children are transformed, or <see langword="null"/>.</returns>
public delegate Action? NodeTransform(TemplateSyntaxNode node, TransformContext context);

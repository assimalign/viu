using System;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu;

internal sealed class MountedComponent<TNode> : MountedNode<TNode>
    where TNode : notnull
{
    internal MountedComponent(
        ComponentNode value,
        ComponentActivation activation,
        MountedNode<TNode> subtree,
        RuntimeComponentContext? owner)
        : base(value, owner)
    {
        Activation = activation;
        Subtree = subtree;
        View = new MountedComponentView<TNode>(this);
    }

    internal ComponentActivation Activation;

    internal IComponent Instance => Activation.Instance;

    internal RuntimeComponentContext Context => Activation.Context;

    internal ComponentRenderer Renderer => Activation.Renderer;

    internal ComponentRenderFrame Frame => Activation.Frame;

    internal EffectScope Scope => Activation.Scope;

    internal ComponentLifecycle Lifecycle => Activation.Lifecycle;

    internal MountedNode<TNode> Subtree;

    internal ReactiveEffect RenderEffect = null!;

    internal SchedulerJob RenderJob = null!;

    internal VirtualNode? PendingTree;

    internal bool HasKeepAliveLifecycleState;

    internal bool IsKeepAliveLifecycleActive;

    internal IDisposable? HotReloadRegistration;

    internal MountedComponentView<TNode> View;

    internal override TNode FirstHostNode => Subtree.FirstHostNode;

    internal override TNode LastHostNode => Subtree.LastHostNode;
}

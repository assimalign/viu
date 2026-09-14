using System;

namespace Assimalign.Viu.Reactivity;

/// <summary>Weak owner metadata held only while its dependency lives.</summary>
internal sealed class ReactivityInspectionAttribution
{
    internal WeakReference<object>? Owner;
    internal string? MemberName;

    internal void Set(object owner, string? memberName)
    {
        if (Owner is null)
        {
            Owner = new WeakReference<object>(owner);
        }
        else
        {
            Owner.SetTarget(owner);
        }

        MemberName = memberName;
    }
}

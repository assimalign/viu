using System;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Viu;

public interface IComponentDescriptor
{
    IComponentDescriptor OnBeforeMount(Action hook);
    IComponentDescriptor OnMounted(Action hook);
    IComponentDescriptor OnBeforeUpdate(Action hook);
    IComponentDescriptor OnUpdated(Action hook);
    IComponentDescriptor OnBeforeUnmount(Action hook);
    IComponentDescriptor OnUnmounted(Action hook);
    IComponentDescriptor OnActivated(Action hook);
    IComponentDescriptor OnDeactivated(Action hook);
    IComponentDescriptor WithEmit(ComponentEmitDefinition emit);
    IComponentDescriptor WithProperty(ComponentPropertyDefinition property);
}

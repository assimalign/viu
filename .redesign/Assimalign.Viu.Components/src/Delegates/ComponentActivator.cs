using System;

namespace Assimalign.Viu.Components;

/// <summary>Creates one component template using the shared service resolver.</summary>
/// <param name="services">The service resolver exposed by the component factory.</param>
/// <returns>A fresh component template for one mount.</returns>
public delegate IComponentTemplate ComponentActivator(IServiceProvider services);


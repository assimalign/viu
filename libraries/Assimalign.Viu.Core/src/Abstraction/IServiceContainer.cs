using System;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Viu;

public interface IServiceContainer
{

    IServiceContainer Add(ServiceRegistration service);

    IServiceProvider Build();
}

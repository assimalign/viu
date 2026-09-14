using System;

namespace Assimalign.Viu.State;

internal interface IStateStoreMutationSource
{
    StateStoreSubscription SubscribeMutation(Action callback);
}

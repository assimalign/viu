using Assimalign.Viu.Reactivity;

namespace ComponentLibraryConsumer;

// The package-consumer build must run the Reactivity generator delivered through
// Assimalign.Viu.App.Ref. Without that analyzer this partial property has no
// implementation and compilation fails, making generator-package drift observable.
[Reactive]
internal partial class ReactivePackageCanary
{
    public partial int Count { get; set; }
}

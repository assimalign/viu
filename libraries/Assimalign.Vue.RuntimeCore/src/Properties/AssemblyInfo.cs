using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Assimalign.Vue.RuntimeCore.Tests")]
[assembly: InternalsVisibleTo("Assimalign.Vue.Testing")]
// The RuntimeDom directive tests drive the RuntimeCore scheduler directly (v-model/v-show run
// through the real renderer + post-flush pipeline), so they reset it between tests just as the
// Testing library does per mount ([V01.01.04.06]).
[assembly: InternalsVisibleTo("Assimalign.Vue.RuntimeDom.Tests")]

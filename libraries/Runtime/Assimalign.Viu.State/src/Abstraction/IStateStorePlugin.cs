namespace Assimalign.Viu.State;

/// <summary>
/// Attaches registry-owned behavior to each subsequently created store. Plugins run once per
/// creation, in registration order, inside the store's scope. Specified by <c>[STA-10]</c>.
/// </summary>
public interface IStateStorePlugin
{
    /// <summary>
    /// Configures one newly created store before it is returned by its registry. A failure aborts
    /// creation and stops the store scope, including earlier plugin attachments. Specified by
    /// <c>[STA-10]</c>.
    /// </summary>
    /// <param name="context">The store, definition, services, and lifetime being configured.</param>
    void Apply(StateStorePluginContext context);
}

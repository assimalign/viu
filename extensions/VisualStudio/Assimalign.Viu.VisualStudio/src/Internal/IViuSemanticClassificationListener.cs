namespace Assimalign.Viu.VisualStudio;

/// <summary>Receives weakly held semantic-classification state changes for one editor buffer.</summary>
internal interface IViuSemanticClassificationListener
{
    /// <summary>Invalidates the buffer after its semantic classification publication changes.</summary>
    void OnSemanticClassificationsChanged(string documentIdentifier);
}

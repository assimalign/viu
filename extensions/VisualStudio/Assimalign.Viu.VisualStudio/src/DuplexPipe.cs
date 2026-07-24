using System.IO.Pipelines;

namespace Assimalign.Viu.VisualStudio;

internal sealed class DuplexPipe : IDuplexPipe
{
    public DuplexPipe(
        PipeReader input,
        PipeWriter output)
    {
        this.Input = input;
        this.Output = output;
    }

    public PipeReader Input { get; }

    public PipeWriter Output { get; }
}

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Delivers one classification report to Visual Studio: the document range the report covers and
/// every lexical span inside it.
/// </summary>
/// <param name="calculatedRangeStart">Start offset of the range the report accounts for.</param>
/// <param name="calculatedRangeLength">Length of the range the report accounts for.</param>
/// <param name="lexicalSpans">Every span classified inside the calculated range.</param>
/// <param name="cancellationToken">Cancellation token for the async call.</param>
/// <remarks>
/// The range and the spans are separate because Visual Studio caches them separately: the range is
/// what marks a region as answered — including the answer "no tags here" — while the spans are the
/// tags themselves. Expressing the report in plain offsets keeps
/// <see cref="ViuClassificationTagger"/>'s reporting policy independent of the editor types, which
/// carry internal members and cannot be implemented outside the Visual Studio SDK.
/// </remarks>
internal delegate Task ViuClassificationReportHandler(
    int calculatedRangeStart,
    int calculatedRangeLength,
    IReadOnlyList<ViuLexicalSpan> lexicalSpans,
    CancellationToken cancellationToken);

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;

using Xunit;

using Assimalign.Viu.LanguageService;

namespace Assimalign.Viu.LanguageServer.Tests;

/// <summary>
/// Pins coalesced, cancellation-aware document publication outside the protocol loop.
/// [V01.01.12.07.11] [V01.01.12.07.15]
/// </summary>
public class LanguageServerPublicationSchedulingTests
{
    [Fact]
    public async Task RunAsync_DidChangeBurst_CancelsStaleRoundAndPublishesOnlyLastVersion()
    {
        var languageService = new BlockingPublicationLanguageService();
        await using var session = new LanguageServerHostSession(
            new LanguageServerHost(languageService));

        await InitializeAsync(session);
        await session.SendAsync(OpenMessage());
        await languageService.InitialPublicationCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        try
        {
            await session.SendAsync(ChangeMessage(version: 2));
            await languageService.ChangedPublicationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            await session.SendAsync(ChangeMessage(version: 3));
            await languageService.StalePublicationCanceled.Task.WaitAsync(TimeSpan.FromSeconds(1));
            await session.SendAsync(ChangeMessage(version: 4));

            await languageService.FinalPublicationCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using JsonDocument finalPublication = await session.ReadNotificationAsync(
                LanguageServerSemanticClassificationPublication.MethodName,
                parameters =>
                    parameters.GetProperty("version").ValueKind == JsonValueKind.Number &&
                    parameters.GetProperty("version").GetInt32() == 4);
        }
        finally
        {
            languageService.ReleaseAll.Set();
        }

        using (JsonDocument shutdown = await ShutdownAsync(session))
        {
            shutdown.RootElement.TryGetProperty("result", out _).ShouldBeTrue();
        }

        var publishedVersions = session.Notifications
            .Where(message =>
                message.RootElement.TryGetProperty("method", out var method) &&
                method.GetString() == LanguageServerSemanticClassificationPublication.MethodName)
            .Select(message => message.RootElement
                .GetProperty("params")
                .GetProperty("version")
                .GetInt32())
            .ToArray();
        publishedVersions.ShouldBe([1, 4]);
        languageService.CanceledPublicationCount.ShouldBeGreaterThanOrEqualTo(1);

        await session.SendAsync(ExitMessage());
        (await session.CompleteAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_FeatureRequestDuringPublication_RespondsBeforePublicationCompletes()
    {
        var languageService = new BlockingPublicationLanguageService();
        await using var session = new LanguageServerHostSession(
            new LanguageServerHost(languageService));

        await InitializeAsync(session);
        await session.SendAsync(OpenMessage());
        await languageService.InitialPublicationCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await session.SendAsync(ChangeMessage(version: 2));
        await languageService.ChangedPublicationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        try
        {
            await session.SendAsync(
                """
                {"jsonrpc":"2.0","id":"feature","method":"textDocument/completion","params":{"textDocument":{"uri":"file:///Counter.viu"},"position":{"line":0,"character":0}}}
                """);
            await languageService.FeatureRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
            using JsonDocument feature = await session.ReadResponseAsync("feature")
                .WaitAsync(TimeSpan.FromSeconds(5));
            feature.RootElement.TryGetProperty("result", out _).ShouldBeTrue();
        }
        finally
        {
            languageService.ReleaseAll.Set();
        }

        using (JsonDocument shutdown = await ShutdownAsync(session))
        {
            shutdown.RootElement.TryGetProperty("result", out _).ShouldBeTrue();
        }

        await session.SendAsync(ExitMessage());
        (await session.CompleteAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_PreDebounceBurst_InvokesPublicationOnlyForLastVersion()
    {
        var languageService = new BlockingPublicationLanguageService();
        var publicationDelay = new ControlledPublicationDelay();
        await using var session = new LanguageServerHostSession(
            new LanguageServerHost(languageService, publicationDelay.WaitAsync));

        await InitializeAsync(session);
        await session.SendAsync(OpenMessage());
        await languageService.InitialPublicationCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await session.SendAsync(ChangeMessage(version: 2));
        await session.SendAsync(ChangeMessage(version: 3));
        await session.SendAsync(ChangeMessage(version: 4));
        await session.SendAsync(FoldingRangeMessage("burst-applied"));
        using (JsonDocument barrier = await session.ReadResponseAsync("burst-applied"))
        {
            barrier.RootElement.TryGetProperty("result", out _).ShouldBeTrue();
        }

        languageService.PublicationVersions.ShouldBe([1]);
        publicationDelay.Release();
        await languageService.FinalPublicationCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using JsonDocument finalPublication = await session.ReadNotificationAsync(
            LanguageServerSemanticClassificationPublication.MethodName,
            parameters =>
                parameters.GetProperty("version").ValueKind == JsonValueKind.Number &&
                parameters.GetProperty("version").GetInt32() == 4);

        using (JsonDocument shutdown = await ShutdownAsync(session))
        {
            shutdown.RootElement.TryGetProperty("result", out _).ShouldBeTrue();
        }

        languageService.PublicationVersions.ShouldBe([1, 4]);
        await session.SendAsync(ExitMessage());
        (await session.CompleteAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_DidCloseDuringPublication_CancelsBeforeClosedStateAndKeepsLoopResponsive()
    {
        var languageService = new BlockingPublicationLanguageService();
        await using var session = new LanguageServerHostSession(
            new LanguageServerHost(languageService));

        await InitializeAsync(session);
        await session.SendAsync(OpenMessage());
        await languageService.InitialPublicationCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await session.SendAsync(ChangeMessage(version: 2));
        await languageService.ChangedPublicationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        try
        {
            await session.SendAsync(CloseMessage());
            await session.SendAsync(FoldingRangeMessage("after-close"));
            using JsonDocument response = await session.ReadResponseAsync("after-close")
                .WaitAsync(TimeSpan.FromSeconds(5));
            response.RootElement.TryGetProperty("result", out _).ShouldBeTrue();
            await languageService.StalePublicationCanceled.Task.WaitAsync(TimeSpan.FromSeconds(1));
        }
        finally
        {
            languageService.ReleaseAll.Set();
        }

        session.Notifications.Any(
            message =>
                message.RootElement.TryGetProperty("method", out var method) &&
                method.GetString() == LanguageServerSemanticClassificationPublication.MethodName &&
                message.RootElement.GetProperty("params").GetProperty("isClosed").GetBoolean())
            .ShouldBeTrue();
        session.Notifications.Any(
            message =>
                message.RootElement.TryGetProperty("method", out var method) &&
                method.GetString() == LanguageServerSemanticClassificationPublication.MethodName &&
                message.RootElement.GetProperty("params").GetProperty("version").ValueKind ==
                    JsonValueKind.Number &&
                message.RootElement.GetProperty("params").GetProperty("version").GetInt32() == 2)
            .ShouldBeFalse();

        using (JsonDocument shutdown = await ShutdownAsync(session))
        {
            shutdown.RootElement.TryGetProperty("result", out _).ShouldBeTrue();
        }

        await session.SendAsync(ExitMessage());
        (await session.CompleteAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_ShutdownDuringPublication_CancelsBeforeDraining()
    {
        var languageService = new BlockingPublicationLanguageService();
        await using var session = new LanguageServerHostSession(
            new LanguageServerHost(languageService));

        await InitializeAsync(session);
        await session.SendAsync(OpenMessage());
        await languageService.InitialPublicationCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await session.SendAsync(ChangeMessage(version: 2));
        await languageService.ChangedPublicationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        try
        {
            using JsonDocument shutdown = await ShutdownAsync(session)
                .WaitAsync(TimeSpan.FromSeconds(5));
            shutdown.RootElement.TryGetProperty("result", out _).ShouldBeTrue();
            await languageService.StalePublicationCanceled.Task.WaitAsync(TimeSpan.FromSeconds(1));
        }
        finally
        {
            languageService.ReleaseAll.Set();
        }

        await session.SendAsync(ExitMessage());
        (await session.CompleteAsync()).ShouldBe(0);
    }

    private static async Task InitializeAsync(LanguageServerHostSession session)
    {
        await session.SendAsync(
            """
            {"jsonrpc":"2.0","id":"initialize","method":"initialize","params":{"initializationOptions":{"semanticClassificationNotifications":true},"capabilities":{}}}
            """);
        using JsonDocument response = await session.ReadResponseAsync("initialize");
        response.RootElement.TryGetProperty("result", out _).ShouldBeTrue();
    }

    private static async Task<JsonDocument> ShutdownAsync(LanguageServerHostSession session)
    {
        await session.SendAsync(
            """
            {"jsonrpc":"2.0","id":"shutdown","method":"shutdown","params":null}
            """);
        return await session.ReadResponseAsync("shutdown");
    }

    private static string OpenMessage()
        => """
           {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///Counter.viu","languageId":"viu","version":1,"text":"@script {\n    int Value;\n}\n"}}}
           """;

    private static string ChangeMessage(int version)
        => "{\"jsonrpc\":\"2.0\",\"method\":\"textDocument/didChange\",\"params\":{" +
           "\"textDocument\":{\"uri\":\"file:///Counter.viu\",\"version\":" + version + "}," +
           "\"contentChanges\":[{\"text\":\"@script {\\n    int Value" + version +
           ";\\n}\\n\"}]}}";

    private static string CloseMessage()
        => """
           {"jsonrpc":"2.0","method":"textDocument/didClose","params":{"textDocument":{"uri":"file:///Counter.viu"}}}
           """;

    private static string FoldingRangeMessage(string identifier)
        => "{\"jsonrpc\":\"2.0\",\"id\":\"" + identifier +
           "\",\"method\":\"textDocument/foldingRange\",\"params\":{" +
           "\"textDocument\":{\"uri\":\"file:///Counter.viu\"}}}";

    private static string ExitMessage()
        => """
           {"jsonrpc":"2.0","method":"exit"}
           """;

    private sealed class BlockingPublicationLanguageService : ILanguageService
    {
        private readonly object synchronization = new();
        private readonly List<int> publicationVersions = [];
        private int version;
        private int canceledPublicationCount;

        internal TaskCompletionSource InitialPublicationCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource ChangedPublicationStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource StalePublicationCanceled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource FinalPublicationCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource FeatureRequestStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal ManualResetEventSlim ReleaseAll { get; } = new(initialState: false);

        internal ManualResetEventSlim ChangedPublicationExited { get; } = new(initialState: false);

        internal int CanceledPublicationCount => Volatile.Read(ref canceledPublicationCount);

        internal int[] PublicationVersions
        {
            get
            {
                lock (synchronization)
                {
                    return publicationVersions.ToArray();
                }
            }
        }

        public void OpenDocument(string documentUri, string text, int? version)
            => Volatile.Write(ref this.version, version ?? 0);

        public bool ChangeDocument(
            string documentUri,
            int? version,
            IReadOnlyList<LanguageDocumentChange> changes)
        {
            Volatile.Write(ref this.version, version ?? 0);
            return true;
        }

        public bool CloseDocument(string documentUri) => true;

        public LanguageClassificationSnapshot? GetClassificationSnapshot(
            string documentUri,
            CancellationToken cancellationToken = default)
        {
            var observedVersion = Volatile.Read(ref version);
            return new LanguageClassificationSnapshot(
                observedVersion,
                $"checksum-{observedVersion}",
                []);
        }

        public LanguageDocumentPublication GetDocumentPublication(
            string documentUri,
            bool includeSemanticClassifications,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var observedVersion = Volatile.Read(ref version);
            lock (synchronization)
            {
                publicationVersions.Add(observedVersion);
            }

            var diagnostics = GetDiagnostics(observedVersion, cancellationToken);
            return new LanguageDocumentPublication(
                includeSemanticClassifications
                    ? new LanguageClassificationSnapshot(
                        observedVersion,
                        $"checksum-{observedVersion}",
                        [])
                    : null,
                diagnostics);
        }

        public IReadOnlyList<LanguageDiagnostic> GetDiagnostics(
            string documentUri,
            CancellationToken cancellationToken = default)
            => GetDiagnostics(Volatile.Read(ref version), cancellationToken);

        private IReadOnlyList<LanguageDiagnostic> GetDiagnostics(
            int observedVersion,
            CancellationToken cancellationToken)
        {
            if (observedVersion == 1)
            {
                InitialPublicationCompleted.TrySetResult();
                return [];
            }

            if (observedVersion >= 4)
            {
                FinalPublicationCompleted.TrySetResult();
                return [];
            }

            ChangedPublicationStarted.TrySetResult();
            try
            {
                WaitHandle.WaitAny(
                    [ReleaseAll.WaitHandle, cancellationToken.WaitHandle],
                    TimeSpan.FromSeconds(10));
                cancellationToken.ThrowIfCancellationRequested();
                return [];
            }
            catch (OperationCanceledException)
            {
                Interlocked.Increment(ref canceledPublicationCount);
                StalePublicationCanceled.TrySetResult();
                throw;
            }
            finally
            {
                ChangedPublicationExited.Set();
            }
        }

        public IReadOnlyList<LanguageCompletionItem> GetCompletions(
            string documentUri,
            LanguagePosition position,
            CancellationToken cancellationToken = default)
        {
            FeatureRequestStarted.TrySetResult();
            if (!ChangedPublicationExited.Wait(TimeSpan.FromSeconds(5), cancellationToken))
            {
                throw new TimeoutException("The canceled publication did not leave its semantic gate.");
            }

            return [];
        }

        public LanguageHover? GetHover(
            string documentUri,
            LanguagePosition position,
            CancellationToken cancellationToken = default)
            => null;

        public string? ResolveCompletionDocumentation(
            string documentUri,
            string completionLabel,
            CancellationToken cancellationToken = default)
            => null;

        public IReadOnlyList<LanguageDocumentSymbol> GetDocumentSymbols(
            string documentUri,
            CancellationToken cancellationToken = default)
            => [];

        public IReadOnlyList<LanguageFoldingRange> GetFoldingRanges(
            string documentUri,
            CancellationToken cancellationToken = default)
            => [];

        public IReadOnlyList<LanguageCodeAction> GetCodeActions(
            string documentUri,
            LanguageRange range,
            CancellationToken cancellationToken = default)
            => [];
    }

    private sealed class ControlledPublicationDelay
    {
        private readonly TaskCompletionSource released =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task WaitAsync(CancellationToken cancellationToken)
            => released.Task.WaitAsync(cancellationToken);

        internal void Release() => released.TrySetResult();
    }
}

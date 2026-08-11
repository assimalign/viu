using System;
using System.Collections.Generic;
using System.IO;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Stores exact semantic classifications outside any Visual Studio type. Publications are immutable,
/// checksum-gated by consumers, and listeners are weak so the process-wide bridge cannot retain a
/// closed editor buffer. Thread-safe: language-client callbacks and editor classification requests
/// may arrive on different threads.
/// </summary>
internal sealed class ViuSemanticClassificationState
{
    private readonly object synchronization = new();
    private readonly Dictionary<string, ViuSemanticClassificationPublication> publications =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<WeakReference<IViuSemanticClassificationListener>>> listeners =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the process-wide state shared by the language client and classifiers.</summary>
    internal static ViuSemanticClassificationState Shared { get; } = new();

    /// <summary>Weakly subscribes one listener to a document's publications.</summary>
    internal void Subscribe(
        string documentIdentifier,
        IViuSemanticClassificationListener listener)
    {
        if (string.IsNullOrWhiteSpace(documentIdentifier))
        {
            throw new ArgumentException("A document identifier is required.", nameof(documentIdentifier));
        }

        if (listener is null)
        {
            throw new ArgumentNullException(nameof(listener));
        }

        var key = NormalizeDocumentIdentifier(documentIdentifier);
        lock (synchronization)
        {
            if (!listeners.TryGetValue(key, out var documentListeners))
            {
                documentListeners = [];
                listeners.Add(key, documentListeners);
            }

            for (var index = documentListeners.Count - 1; index >= 0; index--)
            {
                if (!documentListeners[index].TryGetTarget(out var existing))
                {
                    documentListeners.RemoveAt(index);
                }
                else if (ReferenceEquals(existing, listener))
                {
                    return;
                }
            }

            documentListeners.Add(new WeakReference<IViuSemanticClassificationListener>(listener));
        }
    }

    /// <summary>
    /// Publishes one server snapshot, rejecting only a version older than the last accepted snapshot.
    /// The checksum is evaluated by the classifier against its current text.
    /// </summary>
    internal bool Publish(ViuSemanticClassificationPublication publication)
    {
        if (publication is null)
        {
            throw new ArgumentNullException(nameof(publication));
        }

        var key = NormalizeDocumentIdentifier(publication.DocumentIdentifier);
        IViuSemanticClassificationListener[] liveListeners;
        lock (synchronization)
        {
            if (publications.TryGetValue(key, out var current) &&
                current.Version is int currentVersion &&
                publication.Version is int nextVersion &&
                nextVersion < currentVersion)
            {
                return false;
            }

            if (publication.IsClosed)
            {
                publications.Remove(key);
            }
            else
            {
                publications[key] = publication;
            }

            liveListeners = CaptureLiveListeners(key);
        }

        foreach (var listener in liveListeners)
        {
            listener.OnSemanticClassificationsChanged(key);
        }

        return true;
    }

    /// <summary>Gets classifications only when they describe the supplied editor text checksum.</summary>
    internal bool TryGetClassifications(
        string documentIdentifier,
        string textChecksum,
        out IReadOnlyList<ViuSemanticClassification> classifications)
    {
        if (string.IsNullOrWhiteSpace(documentIdentifier))
        {
            throw new ArgumentException("A document identifier is required.", nameof(documentIdentifier));
        }

        if (string.IsNullOrWhiteSpace(textChecksum))
        {
            throw new ArgumentException("A text checksum is required.", nameof(textChecksum));
        }

        var key = NormalizeDocumentIdentifier(documentIdentifier);
        lock (synchronization)
        {
            if (publications.TryGetValue(key, out var publication) &&
                string.Equals(publication.TextChecksum, textChecksum, StringComparison.Ordinal))
            {
                classifications = publication.Classifications;
                return true;
            }
        }

        classifications = Array.Empty<ViuSemanticClassification>();
        return false;
    }

    /// <summary>
    /// Removes every server-authored snapshot and invalidates affected listeners. A new language-
    /// server session must not inherit version ordering or classifications from the previous one.
    /// </summary>
    internal void ClearPublications()
    {
        Dictionary<string, IViuSemanticClassificationListener[]> notifications;
        lock (synchronization)
        {
            notifications = new Dictionary<string, IViuSemanticClassificationListener[]>(
                publications.Count,
                StringComparer.OrdinalIgnoreCase);
            foreach (string key in publications.Keys)
            {
                notifications.Add(key, CaptureLiveListeners(key));
            }

            publications.Clear();
        }

        foreach (KeyValuePair<string, IViuSemanticClassificationListener[]> notification in
                 notifications)
        {
            foreach (IViuSemanticClassificationListener listener in notification.Value)
            {
                listener.OnSemanticClassificationsChanged(notification.Key);
            }
        }
    }

    private IViuSemanticClassificationListener[] CaptureLiveListeners(string key)
    {
        if (!listeners.TryGetValue(key, out var documentListeners))
        {
            return Array.Empty<IViuSemanticClassificationListener>();
        }

        var liveListeners = new List<IViuSemanticClassificationListener>(documentListeners.Count);
        for (var index = documentListeners.Count - 1; index >= 0; index--)
        {
            if (documentListeners[index].TryGetTarget(out var listener))
            {
                liveListeners.Add(listener);
            }
            else
            {
                documentListeners.RemoveAt(index);
            }
        }

        if (documentListeners.Count == 0)
        {
            listeners.Remove(key);
        }

        return liveListeners.ToArray();
    }

    private static string NormalizeDocumentIdentifier(string documentIdentifier)
    {
        if (Uri.TryCreate(documentIdentifier, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            return NormalizeFilePath(uri.LocalPath);
        }

        return Path.IsPathRooted(documentIdentifier)
            ? NormalizeFilePath(documentIdentifier)
            : documentIdentifier;
    }

    private static string NormalizeFilePath(string filePath)
        => filePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
}

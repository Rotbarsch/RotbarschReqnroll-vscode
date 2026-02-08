using OmniSharp.Extensions.LanguageServer.Protocol;
using System.Collections.Concurrent;

namespace Reqnroll.LanguageServer.Services;

/// <summary>
/// Thread-safe in-memory store for document contents indexed by URI.
/// Used by handlers to access the current state of open documents.
/// </summary>
public class DocumentStorageService
{
    private readonly ConcurrentDictionary<string, string> _documents = new();

    /// <summary>
    /// Stores or updates document content for the specified URI.
    /// </summary>
    public void Set(DocumentUri uri, string content)
    {
        _documents[uri.ToString()] = content;
    }

    /// <summary>
    /// Retrieves document content for the specified URI, or null if not found.
    /// </summary>
    public string? Get(DocumentUri uri)
    {
        _documents.TryGetValue(uri.ToString(), out var content);
        return content;
    }

    public void Remove(DocumentUri uri)
    {
        _documents.TryRemove(uri.ToString(), out _);
    }
}

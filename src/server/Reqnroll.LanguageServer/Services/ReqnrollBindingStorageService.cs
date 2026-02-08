using System.Text.RegularExpressions;
using ReqnRollBindingMetadataExtractorService.Model;
using ReqnRollBindingMetadataExtractorService.Services;

namespace Reqnroll.LanguageServer.Services;

/// <summary>
/// Caches Reqnroll step binding metadata from the workspace.
/// Automatically refreshes bindings every 30 seconds to detect changes.
/// </summary>
public class ReqnrollBindingStorageService
{
    private string? _workspaceDirectory;
    private List<BindingMetadata> _bindingInfos = new List<BindingMetadata>();
    private DateTime _lastLoadTime = DateTime.MinValue;
    
    /// <summary>
    /// Sets the workspace directory and loads bindings from it.
    /// </summary>
    public void SetWorkspaceDirectory(string directory)
    {
        _workspaceDirectory = directory;
        RefreshBindings();
    }
    
    private void RefreshBindings()
    {
        if (!string.IsNullOrEmpty(_workspaceDirectory))
        {
            _bindingInfos = BindingMetadataManager.GetAll(_workspaceDirectory);
            _lastLoadTime = DateTime.UtcNow;
        }
    }
    
    /// <summary>
    /// Checks if any binding matches the given step text using regex pattern matching.
    /// </summary>
    public bool HasMatchingBinding(string stepText)
    {
        EnsureBindingsLoaded();
        return _bindingInfos.Any(exp => Regex.IsMatch(stepText, exp.Expression));
    }

    /// <summary>
    /// Returns all available step bindings from the workspace.
    /// </summary>
    public List<BindingMetadata> GetAllBindings()
    {
        EnsureBindingsLoaded();
        return _bindingInfos;
    }

    public List<BindingMetadata> GetBindingsByStepType(string stepType)
    {
        EnsureBindingsLoaded();
        return _bindingInfos
            .Where(b => b.StepType.Equals(stepType, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private void EnsureBindingsLoaded()
    {
        // Refresh if never loaded or if data is older than 30 seconds
        if (_bindingInfos.Count == 0 || (DateTime.UtcNow - _lastLoadTime).TotalSeconds > 30)
        {
            RefreshBindings();
        }
    }
}
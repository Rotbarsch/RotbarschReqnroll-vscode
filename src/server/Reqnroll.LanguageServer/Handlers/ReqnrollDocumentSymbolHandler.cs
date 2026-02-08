using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Reqnroll.LanguageServer.Services;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using System.Collections.ObjectModel;
using System.Reflection;

namespace Reqnroll.LanguageServer.Handlers;

/// <summary>
/// Provides document symbol (outline) functionality for Reqnroll feature files.
/// </summary>
public class ReqnrollDocumentSymbolHandler : IDocumentSymbolHandler
{
    private readonly DocumentStorageService _documentStorageService;

    public ReqnrollDocumentSymbolHandler(DocumentStorageService documentStorageService)
    {
        _documentStorageService = documentStorageService;
    }

    public DocumentSymbolRegistrationOptions GetRegistrationOptions(DocumentSymbolCapability capability, ClientCapabilities clientCapabilities)
    {
        return new DocumentSymbolRegistrationOptions
        {
            DocumentSelector = new DocumentSelector(new DocumentFilter
            {
                Language = "reqnroll-feature",
                Pattern = "**/*.feature"
            })
        };
    }

    public Task<SymbolInformationOrDocumentSymbolContainer> Handle(DocumentSymbolParams request, CancellationToken cancellationToken)
    {
        var documentUri = request.TextDocument.Uri;
        var documentContent = _documentStorageService.Get(documentUri);

        if (string.IsNullOrEmpty(documentContent))
        {
            return Task.FromResult(new SymbolInformationOrDocumentSymbolContainer());
        }

        var symbols = ParseDocumentSymbols(documentContent, documentUri.ToString());
        return Task.FromResult(new SymbolInformationOrDocumentSymbolContainer(symbols));
    }

    private static void SetChildren(DocumentSymbol symbol, List<DocumentSymbol> children)
    {
        // Use reflection to set the init-only Children property
        var property = typeof(DocumentSymbol).GetProperty("Children");
        if (property != null)
        {
            property.SetValue(symbol, new Container<DocumentSymbol>(children));
        }
    }

    private List<SymbolInformationOrDocumentSymbol> ParseDocumentSymbols(string documentContent, string documentUri)
    {
        var symbols = new List<SymbolInformationOrDocumentSymbol>();
        var lines = documentContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        
        DocumentSymbol? currentFeature = null;
        List<DocumentSymbol>? featureChildren = null;
        DocumentSymbol? currentScenario = null;
        List<DocumentSymbol>? scenarioChildren = null;
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();
            
            // Feature
            if (trimmed.StartsWith("Feature:", StringComparison.OrdinalIgnoreCase))
            {
                // Finalize previous feature's children if exists
                if (currentFeature != null && featureChildren != null)
                {
                    SetChildren(currentFeature, featureChildren);
                }
                
                var name = trimmed.Substring("Feature:".Length).Trim();
                if (string.IsNullOrEmpty(name))
                {
                    name = "Feature";
                }
                
                featureChildren = new List<DocumentSymbol>();
                currentFeature = new DocumentSymbol
                {
                    Name = name,
                    Kind = SymbolKind.Module,
                    Range = new Range(i, 0, i, line.Length),
                    SelectionRange = new Range(i, 0, i, line.Length)
                };
                
                symbols.Add(currentFeature);
                currentScenario = null;
                scenarioChildren = null;
            }
            // Background
            else if (trimmed.StartsWith("Background:", StringComparison.OrdinalIgnoreCase))
            {
                var name = trimmed.Substring("Background:".Length).Trim();
                if (string.IsNullOrEmpty(name))
                {
                    name = "Background";
                }
                
                var backgroundSymbol = new DocumentSymbol
                {
                    Name = name,
                    Kind = SymbolKind.Event,
                    Range = new Range(i, 0, i, line.Length),
                    SelectionRange = new Range(i, 0, i, line.Length)
                };
                
                if (featureChildren != null)
                {
                    featureChildren.Add(backgroundSymbol);
                }
                else
                {
                    symbols.Add(backgroundSymbol);
                }
                
                currentScenario = null;
                scenarioChildren = null;
            }
            // Scenario Outline
            else if (trimmed.StartsWith("Scenario Outline:", StringComparison.OrdinalIgnoreCase))
            {
                // Finalize previous scenario's children if exists
                if (currentScenario != null && scenarioChildren != null)
                {
                    SetChildren(currentScenario, scenarioChildren);
                }
                
                var name = trimmed.Substring("Scenario Outline:".Length).Trim();
                if (string.IsNullOrEmpty(name))
                {
                    name = "Scenario Outline";
                }
                
                scenarioChildren = new List<DocumentSymbol>();
                currentScenario = new DocumentSymbol
                {
                    Name = name,
                    Kind = SymbolKind.Method,
                    Range = new Range(i, 0, i, line.Length),
                    SelectionRange = new Range(i, 0, i, line.Length)
                };
                
                if (featureChildren != null)
                {
                    featureChildren.Add(currentScenario);
                }
                else
                {
                    symbols.Add(currentScenario);
                }
            }
            // Scenario
            else if (trimmed.StartsWith("Scenario:", StringComparison.OrdinalIgnoreCase))
            {
                // Finalize previous scenario's children if exists
                if (currentScenario != null && scenarioChildren != null)
                {
                    SetChildren(currentScenario, scenarioChildren);
                }
                
                var name = trimmed.Substring("Scenario:".Length).Trim();
                if (string.IsNullOrEmpty(name))
                {
                    name = "Scenario";
                }
                
                scenarioChildren = new List<DocumentSymbol>();
                currentScenario = new DocumentSymbol
                {
                    Name = name,
                    Kind = SymbolKind.Method,
                    Range = new Range(i, 0, i, line.Length),
                    SelectionRange = new Range(i, 0, i, line.Length)
                };
                
                if (featureChildren != null)
                {
                    featureChildren.Add(currentScenario);
                }
                else
                {
                    symbols.Add(currentScenario);
                }
            }
            // Examples
            else if (trimmed.StartsWith("Examples:", StringComparison.OrdinalIgnoreCase))
            {
                var name = trimmed.Substring("Examples:".Length).Trim();
                if (string.IsNullOrEmpty(name))
                {
                    name = "Examples";
                }
                
                var examplesSymbol = new DocumentSymbol
                {
                    Name = name,
                    Kind = SymbolKind.Array,
                    Range = new Range(i, 0, i, line.Length),
                    SelectionRange = new Range(i, 0, i, line.Length)
                };
                
                if (scenarioChildren != null)
                {
                    scenarioChildren.Add(examplesSymbol);
                }
                else if (featureChildren != null)
                {
                    featureChildren.Add(examplesSymbol);
                }
                else
                {
                    symbols.Add(examplesSymbol);
                }
            }
        }
        
        // Finalize last feature's children
        if (currentFeature != null && featureChildren != null)
        {
            SetChildren(currentFeature, featureChildren);
        }
        
        // Finalize last scenario's children
        if (currentScenario != null && scenarioChildren != null)
        {
            SetChildren(currentScenario, scenarioChildren);
        }
        
        return symbols;
    }
}

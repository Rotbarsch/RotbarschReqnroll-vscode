using MediatR;
using Newtonsoft.Json;

namespace Reqnroll.LanguageServer.Models.TestDiscovery;

public class DiscoverTestsParams : IRequest<List<DiscoveredTest>>
{
    [JsonProperty("uri")]
    public string Uri { get; set; } = string.Empty;
}
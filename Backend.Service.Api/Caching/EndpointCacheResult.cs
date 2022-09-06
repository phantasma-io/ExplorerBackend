namespace Backend.Service.Api.Caching;

public class EndpointCacheResult
{
    public string Key { get; set; }
    public string Content { get; set; }
    public bool Cached { get; set; }
}

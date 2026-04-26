using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace RazerKeyboard;

sealed class ChromaClient : IAsyncDisposable
{
    const string Endpoint = "http://localhost:54235/razer/chromasdk";
    const string InitBody =
        """{"title":"RazerMatrix","description":"Matrix rain for Razer keyboards","author":{"name":"user","contact":"user@user.com"},"device_supported":["keyboard"],"category":"application"}""";

    // Razer color format: 0x00BBGGRR — green channel = G * 256
    public const int Green  = 65280;   // G = 255
    public const int Green2 = 46080;   // G = 180
    public const int Green3 = 28160;   // G = 110
    public const int Green4 = 14080;   // G =  55
    public const int Green5 =  5120;   // G =  20
    public const int Green6 =  1536;   // G =   6
    public const int Black  =     0;

    readonly HttpClient _http = new();
    string _sessionUri = "";

    public async Task<bool> InitAsync()
    {
        try
        {
            var r = await Post(Endpoint, InitBody);
            using var doc = JsonDocument.Parse(r);
            _sessionUri = doc.RootElement.GetProperty("uri").GetString() ?? "";
            return _sessionUri.Length > 0;
        }
        catch { return false; }
    }

    public Task SetStaticGreenAsync()
        => Put($"{_sessionUri}/keyboard",
               $"{{\"effect\":\"CHROMA_STATIC\",\"param\":{{\"color\":{Green}}}}}");

    public Task SetCustomAsync(int[][] grid)
    {
        var sb = new System.Text.StringBuilder(1200);
        sb.Append("{\"effect\":\"CHROMA_CUSTOM\",\"param\":[");
        for (int r = 0; r < grid.Length; r++)
        {
            if (r > 0) sb.Append(',');
            sb.Append('[');
            for (int c = 0; c < grid[r].Length; c++)
            {
                if (c > 0) sb.Append(',');
                sb.Append(grid[r][c]);
            }
            sb.Append(']');
        }
        sb.Append("]}");
        return Put($"{_sessionUri}/keyboard", sb.ToString());
    }

    public Task HeartbeatAsync() => Put($"{_sessionUri}/heartbeat", "{}");

    async Task<string> Post(string url, string body)
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync(url, content);
        return await resp.Content.ReadAsStringAsync();
    }

    async Task Put(string url, string body)
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        await _http.PutAsync(url, content);
    }

    public async ValueTask DisposeAsync()
    {
        if (_sessionUri.Length > 0)
            try { await _http.DeleteAsync(_sessionUri); } catch { }
        _http.Dispose();
    }
}

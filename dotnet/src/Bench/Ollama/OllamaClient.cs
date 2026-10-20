using System.Net.Http.Json;
using System.Text.Json;

namespace Bench.Ollama;

// Talks to Ollama's HTTP API directly (not Microsoft.Extensions.AI — that comes in #7 and hides the timings).
public sealed class OllamaClient(HttpClient http)
{
    // Ollama's JSON is snake_case. Without this, eval_count etc. silently read as 0.
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public Task<GenerateResponse> GenerateAsync(string model, string prompt, int numCtx) =>
        PostAsync(new
        {
            model,
            prompt,
            stream = false,      // Ollama streams by default; we want one reply with the timings at the end
            keep_alive = "10m",
            options = new { temperature = 0, seed = 42, num_ctx = numCtx, num_predict = 512 },
        });

    // Empty prompt = only load the model. Use the SAME num_ctx as the real calls,
    // or Ollama reloads the model inside the first measured call.
    public Task LoadAsync(string model, int numCtx) =>
        PostAsync(new { model, prompt = "", stream = false, keep_alive = "10m", options = new { num_ctx = numCtx } });

    // keep_alive 0 = unload now, so the next model gets the whole GPU.
    public Task UnloadAsync(string model) =>
        PostAsync(new { model, prompt = "", stream = false, keep_alive = 0 });

    // Same as `ollama ps`: how big the loaded model is and how much of it sits in VRAM.
    public async Task<RunningModel?> GetRunningAsync(string model)
    {
        var ps = await http.GetFromJsonAsync<PsResponse>("api/ps", Json);
        return ps?.Models.FirstOrDefault(m => m.Name == model);
    }

    private async Task<GenerateResponse> PostAsync(object body)
    {
        using var response = await http.PostAsJsonAsync("api/generate", body, Json);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Ollama {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        return (await response.Content.ReadFromJsonAsync<GenerateResponse>(Json))!;
    }
}

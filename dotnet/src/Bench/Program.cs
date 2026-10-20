using System.Text;
using System.Text.Json;
using Bench;
using Bench.Ollama;

// Agentic AI #4 — the same 10 prompts on three local models: speed, memory and quality.
// Run from ai-lab/dotnet:   dotnet run -c Release --project src/Bench

string[] models = ["llama3.1:8b-instruct-q4_K_M", "qwen2.5:7b-instruct-q4_K_M", "gemma3:4b"];
const int NumCtx = 4096;

var prompts = JsonSerializer.Deserialize<List<PromptCase>>(
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "prompt.json")),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

// The default 100 s timeout is too short for a cold load or a model spilling onto the CPU.
using var http = new HttpClient { BaseAddress = new Uri("http://localhost:11434/"), Timeout = TimeSpan.FromMinutes(10) };
var ollama = new OllamaClient(http);

var report = new StringBuilder();
var answers = new StringBuilder("\n## Answers\n");

// ── 1. The benchmark ────────────────────────────────────────────────────────
report.AppendLine($"# Model benchmark — {DateTime.Now:yyyy-MM-dd HH:mm}");
report.AppendLine($"\nnum_ctx {NumCtx} · temperature 0 · seed 42 · num_predict 512 · warm model (load time excluded)\n");
report.AppendLine("| Model | Loaded size | Processor | Gen tok/s median (min) | Prompt tok/s median | TTFT ≈ median | Checks | Manual /4 |");
report.AppendLine("|---|---|---|---|---|---|---|---|");

foreach (var model in models)
{
    var (results, loaded) = await RunModelAsync(model, prompts, NumCtx);

    var gen = results.Select(r => r.Reply.TokensPerSecond).ToList();
    var graded = results.Where(r => r.Passed is not null).ToList();

    report.AppendLine(
        $"| {model} | {loaded?.SizeGb:F1} GB | {loaded?.Processor} " +
        $"| {Median(gen):F1} ({gen.Min():F1}) " +
        $"| {Median(results.Select(r => r.Reply.PromptTokensPerSecond)):F0} " +
        $"| {Median(results.Select(r => r.Reply.TimeToFirstToken)):F2} s " +
        $"| {graded.Count(r => r.Passed == true)}/{graded.Count} | ? |");

    foreach (var r in results)
    {
        answers.AppendLine($"\n### {model} · #{r.Prompt.Id} {r.Prompt.Kind} · {Verdict(r.Passed)}");
        answers.AppendLine($"in {r.Reply.PromptEvalCount} tokens · out {r.Reply.EvalCount} tokens · done_reason {r.Reply.DoneReason}");
        if (r.Passed is null) answers.AppendLine($"\nRubric: {r.Prompt.Check.Rubric}");
        answers.AppendLine($"\n````\n{r.Reply.Response.Trim()}\n````");
    }
}

// ── 2. The #3 experiment: one long prompt, 4K vs 32K context ─────────────────
// Three copies of prompt #10 ≈ 4.9K input tokens. At 4K, Ollama silently cuts the input.
var summary = prompts.Single(p => p.Id == 10);
var longPrompt = summary with { Kind = "long summary", Prompt = string.Join("\n\n", Enumerable.Repeat(summary.Prompt, 3)) };

report.AppendLine($"\n## Context experiment — {models[0]}, one ~5K-token prompt\n");
report.AppendLine("| num_ctx | Input tokens used | Gen tok/s | TTFT ≈ | Loaded size | Processor |");
report.AppendLine("|---|---|---|---|---|---|");

foreach (var ctx in new[] { 4096, 32768 })
{
    var (results, loaded) = await RunModelAsync(models[0], [longPrompt], ctx);
    var reply = results[0].Reply;
    report.AppendLine($"| {ctx} | {reply.PromptEvalCount} | {reply.TokensPerSecond:F1} | {reply.TimeToFirstToken:F1} s | {loaded?.SizeGb:F1} GB | {loaded?.Processor} |");
}

Directory.CreateDirectory("results");
var path = Path.GetFullPath(Path.Combine("results", $"{DateTime.Now:yyyy-MM-dd}.md"));
File.WriteAllText(path, report.ToString() + answers);
Console.WriteLine($"\nWrote {path}");

// ── One model: load → run every prompt → read `ollama ps` → unload ──────────
async Task<(List<Result> Results, RunningModel? Loaded)> RunModelAsync(string model, List<PromptCase> cases, int numCtx)
{
    Console.WriteLine($"\n{model}  (num_ctx {numCtx})");
    await ollama.LoadAsync(model, numCtx);      // warm-up, not measured: keeps load time out of the numbers

    var results = new List<Result>();
    foreach (var p in cases)
    {
        var reply = await ollama.GenerateAsync(model, p.Prompt, numCtx);
        if (reply.EvalCount == 0)
            throw new InvalidOperationException($"{model} #{p.Id} generated no tokens — don't trust any number from this run.");

        var result = new Result(p, reply, p.Check.Passes(reply.Response));
        results.Add(result);
        Console.WriteLine($"  #{p.Id,-2} {p.Kind,-12} {reply.TokensPerSecond,6:F1} tok/s   {Verdict(result.Passed)}");
    }

    var loaded = await ollama.GetRunningAsync(model);   // read while it is still loaded
    await ollama.UnloadAsync(model);                    // free the GPU for the next model
    return (results, loaded);
}

static string Verdict(bool? passed) => passed switch { true => "PASS", false => "FAIL", null => "score by hand" };

static double Median(IEnumerable<double> values)
{
    var sorted = values.Order().ToArray();
    var mid = sorted.Length / 2;
    return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
}

record Result(PromptCase Prompt, GenerateResponse Reply, bool? Passed);

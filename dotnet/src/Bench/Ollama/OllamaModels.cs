namespace Bench.Ollama;

// What POST /api/generate sends back. Every duration is in NANOSECONDS.
public sealed record GenerateResponse(
    string Response,
    string? DoneReason,       // "stop" = finished, "length" = cut off by num_predict
    long LoadDuration,
    int PromptEvalCount,      // input tokens  (prefill)
    long PromptEvalDuration,
    int EvalCount,            // output tokens (decode)
    long EvalDuration)
{
    public double TokensPerSecond => EvalCount / Seconds(EvalDuration);
    public double PromptTokensPerSecond => PromptEvalCount / Seconds(PromptEvalDuration);

    // Approximate: with the model already loaded, the first token arrives right after prefill.
    public double TimeToFirstToken => Seconds(PromptEvalDuration);

    private static double Seconds(long nanoseconds) => nanoseconds / 1e9;
}

// One row of GET /api/ps — the same data `ollama ps` prints.
public sealed record RunningModel(string Name, long Size, long SizeVram)
{
    public double SizeGb => Size / 1e9;

    // Size = weights + KV cache + buffers. Whatever is not in VRAM runs on the CPU.
    public string Processor
    {
        get
        {
            var gpu = (int)(SizeVram * 100 / Size);
            return gpu == 100 ? "100% GPU" : $"{100 - gpu}%/{gpu}% CPU/GPU";
        }
    }
}

public sealed record PsResponse(List<RunningModel> Models);

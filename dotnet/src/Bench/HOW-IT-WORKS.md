# Bench — how it works

**The idea in one line:** send the same 10 prompts to each model with the same settings, read Ollama's own timing numbers, check the answers, and write a report.

Read this in order: **1. the big picture → 2. the step-by-step flow → 3. one call up close → 4. the files → 5. the C# syntax.**

---

## 1. The big picture

```
for each model:
    load it (empty prompt)          ← warm-up, NOT measured
    for each of the 10 prompts:
        POST /api/generate          ← one call, wait for the whole answer
        tokens/s from the reply     ← Ollama's numbers, not a stopwatch
        check the answer            ← PASS / FAIL / score by hand
    GET /api/ps                     ← how big it is in memory, % on the GPU
    unload it (keep_alive 0)        ← free the GPU for the next model

then: one long prompt at num_ctx 4096 and at 32768   (the #3 VRAM experiment)
then: write results/<date>.md
```

---

## 2. The step-by-step flow — what runs, in order

Open `Program.cs` beside this and follow along. A C# program with **top-level statements** simply runs from the first line to the last.

| # | Where | What happens |
|---|---|---|
| 1 | `Program.cs` top | **Settings.** `models` = the three model names. `NumCtx` = 4096 |
| 2 | `Program.cs` | **Read `prompt.json`** → `JsonSerializer.Deserialize` turns the JSON text into a `List<PromptCase>`. `PropertyNameCaseInsensitive` lets `requiredKeys` in the file fill `RequiredKeys` in C# |
| 3 | `Program.cs` | **Create the HTTP client** pointed at `http://localhost:11434/` with a 10-minute timeout, and wrap it in `OllamaClient` |
| 4 | `Program.cs` | **Start the report** — two `StringBuilder`s: `report` (tables) and `answers` (every answer). Write the header and the table's first two lines |
| 5 | `foreach (var model in models)` | **For each model, call `RunModelAsync(model, prompts, 4096)`** — jump to the bottom of the file ↓ |
| 5a | `RunModelAsync` | `LoadAsync` → POST with an **empty prompt** → Ollama loads the model into VRAM and returns. Nothing is measured |
| 5b | `RunModelAsync` loop | For each prompt: `GenerateAsync` → POST → Ollama answers → the JSON becomes a `GenerateResponse` |
| 5c | | **Sanity check:** `EvalCount == 0` means nothing was generated → stop the whole run (no fake numbers) |
| 5d | | `p.Check.Passes(reply.Response)` → `true` / `false` / `null` (score by hand) |
| 5e | | Store `(prompt, reply, passed)` as a `Result`; print one console line |
| 5f | | After all 10: `GetRunningAsync` → GET `/api/ps` → loaded size and how much is on the GPU |
| 5g | | `UnloadAsync` → POST with `keep_alive: 0` → VRAM is free |
| 5h | | Return **two things at once** — the results and the loaded info — as a tuple |
| 6 | back in the loop | **One table row:** median and min tok/s, median prompt tok/s, median TTFT, checks passed. Then every answer goes into `answers` |
| 7 | `Program.cs` | **Context experiment.** Take prompt #10, make a copy whose text is three copies joined (`with`). Run it once at 4096 and once at 32768 — one row each |
| 8 | `Program.cs` | **Write the file** `results/<date>.md` — `results` is relative to the **current folder**, which is why Visual Studio put it in `bin\Debug\net10.0` |

---

## 3. One call up close — `GenerateAsync`, from C# to Ollama and back

```
GenerateAsync("qwen2.5:7b…", "What is the capital…", 4096)
   │
   │ 1. build an anonymous object:  new { model, prompt, stream = false, keep_alive = "10m", options = new { … } }
   ▼
PostAsync(body)
   │ 2. PostAsJsonAsync turns it into JSON:
   │    {"model":"qwen2.5:7b…","prompt":"What is…","stream":false,"keep_alive":"10m",
   │     "options":{"temperature":0,"seed":42,"num_ctx":4096,"num_predict":512}}
   │ 3. HTTP POST  http://localhost:11434/api/generate
   ▼
Ollama  (reads the prompt = prefill, writes tokens = decode, times both)
   │ 4. replies with JSON:
   │    {"response":"Canberra","done_reason":"stop","prompt_eval_count":43,
   │     "prompt_eval_duration":22000000,"eval_count":3,"eval_duration":60000000, …}
   ▼
ReadFromJsonAsync<GenerateResponse>
   │ 5. SnakeCaseLower maps eval_count → EvalCount, done_reason → DoneReason …
   │    (fields we don't declare, like created_at, are ignored)
   ▼
GenerateResponse record
   │ 6. computed properties do the maths when you read them:
   │    TokensPerSecond = EvalCount / (EvalDuration / 1e9)  = 3 / 0.06 = 50 tok/s
   ▼
back to RunModelAsync
```

If Ollama returns an error (a model name it doesn't know, for example), `PostAsync` throws with Ollama's own message, so you see *why*.

---

## 4. The files

| File | Read it for |
|---|---|
| `Program.cs` | The flow in section 2 |
| `Ollama/OllamaClient.cs` | The 4 HTTP calls |
| `Ollama/OllamaModels.cs` | What Ollama sends back, and the 3 small formulas |
| `PromptCase.cs` | One entry of `prompt.json` and how its answer is checked |

### Where to change things

| To change | Edit |
|---|---|
| **Which models** | `Program.cs`: `string[] models = [...]`. Names must match `ollama list` exactly |
| Context size for the main run | `Program.cs`: `const int NumCtx = 4096;` |
| Temperature, seed, max output tokens | `OllamaClient.GenerateAsync` → `options` |
| Prompts and checks | `prompt.json` |

### The 4 calls to Ollama

| Method | What it sends | Why |
|---|---|---|
| `GenerateAsync` | `model`, `prompt`, `stream: false`, `options` | The real call. `stream: false` = one reply with the timings at the end |
| `LoadAsync` | empty `prompt`, same `num_ctx` | Loads the model only. Same `num_ctx`, or Ollama reloads inside the first measured call |
| `UnloadAsync` | empty `prompt`, `keep_alive: 0` | Unloads now, so two models never share the 8 GB |
| `GetRunningAsync` | `GET /api/ps` | Same as `ollama ps`: loaded size and GPU share |

### The maths (all Ollama durations are nanoseconds)

- **Generation tok/s** = `eval_count ÷ eval_duration × 1e9` — output speed (decode)
- **Prompt tok/s** = `prompt_eval_count ÷ prompt_eval_duration × 1e9` — input speed (prefill)
- **Time to first token ≈** `prompt_eval_duration` — fair only because the model is already loaded
- **Processor** = `size_vram ÷ size`. Under 100% means part of the model runs on the CPU

### The checks

| Type | Passes when |
|---|---|
| `contains` | the answer contains the expected text (any case) |
| `number` | the exact number appears on its own (`0.05` does not match `10.05`) |
| `json` | the whole answer parses as a JSON object with the required keys — a ```json fence **fails**, because the prompt forbade it |
| `bullets` | exactly 3 non-empty lines, each starting with `- ` |
| `manual` | never — you score it with the rubric (0–2) |

### The traps this code guards against

1. **snake_case** — Ollama sends `eval_count`; without `SnakeCaseLower` every number reads as 0, no error.
2. **Streaming is the default** — forget `stream: false` and you get many small JSON lines, not one.
3. **100 s HttpClient timeout** — too short for a cold load or a model spilling to the CPU; set to 10 min.
4. **Load time in the numbers** — the warm-up keeps it out.
5. **Two models in VRAM** — unload between models.
6. **A run that measured nothing** — `EvalCount == 0` stops the run instead of writing fake numbers.

---

## 5. The C# syntax in this code

Each row: the syntax, the line from this code, what it means. Learn the ⭐ ones first — they are everywhere in modern .NET.

### Program structure

| Syntax | From this code | Meaning |
|---|---|---|
| ⭐ Top-level statements | `Program.cs` has no `class Program` / `Main` | The compiler wraps the file in a hidden `Main`. Code runs top to bottom |
| File-scoped namespace | `namespace Bench.Ollama;` | Everything in the file is in that namespace — no `{ }` block, one less indent |
| `using` directive | `using System.Text.Json;` | Lets you write `JsonSerializer` instead of `System.Text.Json.JsonSerializer` |
| ⭐ Local function | `async Task<…> RunModelAsync(…) { … }` at the bottom of `Program.cs` | A function declared inside the top-level code. It can use the variables above it (`ollama`) |
| `static` local function | `static double Median(…)` | Same, but it may **not** touch outside variables — a promise that it only uses its parameters |

### Variables and values

| Syntax | From this code | Meaning |
|---|---|---|
| ⭐ `var` | `var prompts = …` | The compiler works out the type. Still strongly typed — just less to write |
| `const` | `const int NumCtx = 4096;` | A value fixed at compile time; can never change |
| ⭐ Collection expression | `string[] models = ["llama…", "qwen…", "gemma…"];` · `[longPrompt]` | C# 12 short way to make an array or list |
| `new[] { … }` | `new[] { 4096, 32768 }` | Older way to make an array; the element type is inferred |
| ⭐ Object initializer | `new HttpClient { BaseAddress = …, Timeout = … }` | Create the object, then set these properties — in one expression |
| Target-typed `new` | `new() { PropertyNamingPolicy = … }` | The type comes from the left side, so you don't repeat it |
| ⭐ Anonymous type | `new { model, prompt, stream = false }` | A throwaway object with no class. `model` alone means "a property called `model` with the variable's value". Perfect for a JSON body |
| `1e9` | `nanoseconds / 1e9` | Scientific notation: 1 × 10⁹ = one billion (nanoseconds in a second) |

### Types: records and classes

| Syntax | From this code | Meaning |
|---|---|---|
| ⭐ Positional record | `public sealed record PromptCase(int Id, string Kind, string Prompt, Check Check);` | One line gives you a constructor, read-only properties, value equality and a readable `ToString()`. Ideal for data |
| Record with a body | `record GenerateResponse(…) { public double TokensPerSecond => …; }` | A record can still have extra members |
| ⭐ `with` expression | `summary with { Kind = "long summary", Prompt = … }` | Copy a record, changing only these properties. The original is untouched |
| ⭐ Primary constructor | `public sealed class OllamaClient(HttpClient http)` | The constructor parameter `http` is usable anywhere in the class — no field, no constructor body |
| `sealed` | `public sealed class …` | No class may inherit from this one |
| `static readonly` field | `private static readonly JsonSerializerOptions Json = …;` | One shared instance for the whole program, created once, never replaced |

### Members

| Syntax | From this code | Meaning |
|---|---|---|
| ⭐ Expression-bodied member | `public double SizeGb => Size / 1e9;` | `=>` = "returns this". A property computed every time you read it |
| Expression-bodied method | `public Task UnloadAsync(string model) => PostAsync(…);` | A one-line method |
| Property with a `get` block | `public string Processor { get { … } }` | A computed property that needs more than one line |

### Null handling

| Syntax | From this code | Meaning |
|---|---|---|
| ⭐ `?` on a type | `string? DoneReason` · `RunningModel?` · `bool?` | "May be null". `bool?` has three values: true, false, null — used here for PASS / FAIL / score by hand |
| ⭐ `?.` | `loaded?.SizeGb` | If `loaded` is null, the result is null instead of a crash |
| `!` (null-forgiving) | `Expected!` · `Deserialize<…>(…)!` | "I know this isn't null" — silences the warning. **It checks nothing at runtime** |
| `is not null` | `r.Passed is not null` | Null test using pattern matching |

### Logic

| Syntax | From this code | Meaning |
|---|---|---|
| ⭐ Switch expression | `Type switch { "contains" => …, "json" => …, _ => throw … }` | Picks one value by matching. `_` = anything else. Arms are tried top to bottom |
| Throw expression | `_ => throw new InvalidDataException(…)` | `throw` used where a value is expected |
| `try` / `catch` | `IsJsonWithKeys` | If `JsonDocument.Parse` throws `JsonException`, return `false` instead of crashing |
| `using var` | `using var http = new HttpClient …` · `using var doc = …` | Dispose (clean up) the object automatically when the method ends |

### Async

| Syntax | From this code | Meaning |
|---|---|---|
| ⭐ `async` / `await` | `var reply = await ollama.GenerateAsync(…);` | Start the HTTP call, give the thread back while waiting, continue here when the answer arrives |
| ⭐ `Task` / `Task<T>` | `Task<GenerateResponse> GenerateAsync(…)` | "A result that will arrive later". `Task` alone = no result, just "done" |
| Returning a `Task` without `await` | `=> PostAsync(new { … });` | Passing the task straight through — the caller awaits it |

### Tuples

| Syntax | From this code | Meaning |
|---|---|---|
| ⭐ Named tuple as return type | `Task<(List<Result> Results, RunningModel? Loaded)>` | Return two values without making a class |
| Returning a tuple | `return (results, loaded);` | Build it with brackets |
| ⭐ Deconstruction | `var (results, loaded) = await RunModelAsync(…);` | Unpack both values into two variables in one line |

### LINQ — querying lists

| Syntax | From this code | Meaning |
|---|---|---|
| ⭐ Lambda | `r => r.Reply.TokensPerSecond` | A tiny inline function: "given `r`, return this" |
| ⭐ `Select` | `results.Select(r => r.Reply.TokensPerSecond)` | Turn each item into something else (here: a number) |
| ⭐ `Where` | `results.Where(r => r.Passed is not null)` | Keep only the items that match |
| `Count(…)` | `graded.Count(r => r.Passed == true)` | How many items match |
| `Min()` | `gen.Min()` | Smallest value |
| `Order()` | `values.Order()` | Sort ascending |
| `Single(…)` | `prompts.Single(p => p.Id == 10)` | The one item that matches — throws if there are zero or several |
| `FirstOrDefault(…)` | `ps?.Models.FirstOrDefault(m => m.Name == model)` | The first match, or `null` if none |
| `All(…)` | `lines.All(line => line.StartsWith("- "))` | True if every item matches |
| `ToList()` / `ToArray()` | `.ToList()` | Run the query now and store the result. LINQ is lazy until you do this |
| `Enumerable.Repeat` | `Enumerable.Repeat(summary.Prompt, 3)` | The same value three times |

### Strings

| Syntax | From this code | Meaning |
|---|---|---|
| ⭐ Interpolation | `$"{model} #{p.Id}"` | Put values inside a string |
| Format specifier | `{reply.TokensPerSecond:F1}` | 1 decimal place. `F0` = none, `F2` = two |
| Alignment | `{p.Id,-2}` | Pad to 2 characters, left-aligned (`-`). Keeps the console columns straight |
| Verbatim + interpolated | `$@"(?<![\d.]){Regex.Escape(Expected!)}"` | `@` = backslashes are literal (handy for regex); `$` = `{…}` still inserts values |
| `StringBuilder` | `report.AppendLine(…)` | Builds a long string piece by piece without copying it every time |
| `Split` options | `StringSplitOptions.RemoveEmptyEntries \| TrimEntries` | Drop empty lines and trim spaces. `\|` combines two flags |

### The .NET library calls

| Call | Meaning |
|---|---|
| `JsonSerializer.Deserialize<T>(text, options)` | JSON text → C# object |
| `http.PostAsJsonAsync(url, body, options)` | C# object → JSON → HTTP POST |
| `response.Content.ReadFromJsonAsync<T>(options)` | HTTP response body → C# object |
| `http.GetFromJsonAsync<T>(url, options)` | GET + read JSON in one call |
| `JsonNamingPolicy.SnakeCaseLower` | `EvalCount` ⇄ `eval_count` |
| `JsonDocument.Parse` | Read JSON without a C# type — used to check it is valid and has the keys |
| `Regex.IsMatch` / `Regex.Escape` | Pattern match / make text safe to put inside a pattern |
| `AppContext.BaseDirectory` | The folder the `.exe` runs from — where `prompt.json` is copied at build |
| `Path.Combine` / `Path.GetFullPath` / `Directory.CreateDirectory` | Build a path · make it absolute · create the folder if missing |

using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentRouter;
using Microsoft.AspNetCore.Http;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
// Secrets layering (later sources win): committed template -> optional
// git-ignored local override -> environment variables. Real API keys must
// ONLY live in appsetting.Local.json (dev) or env vars (all environments),
// never in the committed appsetting.json.
builder.Configuration
    .AddJsonFile("appsetting.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsetting.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

var app = builder.Build();
var runner = new AppRunner(builder.Configuration);
await runner.InitializeAsync(); // loads skills once at startup

var bindAddress = builder.Configuration["AgentRouter:BindAddress"] ?? "http://localhost:5080";

var WriteErrorAsync = async (HttpContext ctx, int status, string message) =>
{
    ctx.Response.StatusCode = status;
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { error = new { message, type = "agentrouter_error" } }), ctx.RequestAborted);
};

// Simple health check. Never exposes the API key — only whether one is set.
app.MapGet("/health", () =>
    Results.Json(new { status = "ok", skills = runner.SkillCount, defaultModel = runner.DefaultModel, hasApiKey = runner.HasApiKey }));

// OpenAI-compatible model listing, transparently proxied from LM Studio.
app.MapGet("/v1/models", async (HttpContext ctx) =>
{
    try
    {
        using var response = await runner.SendAsync(new HttpRequestMessage(HttpMethod.Get, runner.ModelsUri), ctx.RequestAborted);
        ctx.Response.StatusCode = (int)response.StatusCode;
        ctx.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
        await response.Content.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error listing models");
        await WriteErrorAsync(ctx, 502, "upstream chat server unavailable");
    }
});

// OpenAI-compatible chat completions router. The request body is forwarded as-is
// (temperature, stream, max_tokens, ...) with only the "model" field rewritten to the
// skill-routed target. Streaming responses (SSE) are piped through live.
app.MapPost("/v1/chat/completions", async (HttpContext ctx) =>
{
    JsonNode? body;
    try
    {
        body = await ctx.Request.ReadFromJsonAsync<JsonNode>(ctx.RequestAborted);
    }
    catch (JsonException ex)
    {
        Log.Warning("Invalid JSON request body: {error}", ex.Message);
        await WriteErrorAsync(ctx, 400, "invalid JSON request body");
        return;
    }

    if (body is not JsonObject payload || payload["messages"] is not JsonArray messages || messages.Count == 0)
    {
        await WriteErrorAsync(ctx, 400, "request must include a non-empty \"messages\" array");
        return;
    }

    var lastMessage = ExtractLastUserText(messages);
    var skill = runner.GetTargetSkill(lastMessage);
    payload["model"] = skill.Model;

    InjectSystemPrompt(messages, skill.Description);

    try
    {
        var request = new HttpRequestMessage(HttpMethod.Post, runner.GetChatCompletionsUri(skill));
        request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await runner.SendAsync(request, ctx.RequestAborted);
        ctx.Response.StatusCode = (int)response.StatusCode;
        ctx.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";

        if (ctx.RequestAborted.IsCancellationRequested)
        {
            return;
        }

        await response.Content.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
        await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error while calling chat server");
        await WriteErrorAsync(ctx, 502, "upstream chat server unavailable");
    }
});

app.Run(bindAddress);

static string ExtractLastUserText(JsonArray messages)
{
    // Walk backwards: prefer the last "user" message (matches OpenAI semantics),
    // fall back to the last message with any text content.
    string fallback = "";
    for (var i = messages.Count - 1; i >= 0; i--)
    {
        if (messages[i] is not JsonObject msg)
        {
            continue;
        }

        var text = GetContentText(msg["content"]);
        if (string.IsNullOrEmpty(text))
        {
            continue;
        }

        if (fallback.Length == 0)
        {
            fallback = text;
        }

        if (msg["role"]?.GetValue<string>() is string role
            && role.Equals("user", StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }
    }

    return fallback;
}

static string GetContentText(JsonNode? content) => content switch
{
    JsonValue value => value.TryGetValue<string>(out var text) ? text : value.ToString(),
    // OpenAI structured blocks: [{ "type": "text", "text": "..." }, ...]
    JsonArray parts => string.Join("", parts.OfType<JsonObject>().Select(p => p["text"]?.GetValue<string>() ?? "")),
    _ => "",
};

/// <summary>
/// Prepends the skill description as a system message so the routed model stays
/// in role, unless the caller already supplied a system prompt.
/// </summary>
static void InjectSystemPrompt(JsonArray messages, string? description)
{
    if (string.IsNullOrWhiteSpace(description))
    {
        return;
    }

    foreach (var msg in messages.OfType<JsonObject>())
    {
        if (msg["role"]?.GetValue<string>()?.Equals("system", StringComparison.OrdinalIgnoreCase) == true)
        {
            return; // caller already set a system prompt — don't override it
        }
    }

    messages.Insert(0, new JsonObject
    {
        ["role"] = "system",
        ["content"] = description,
    });
}
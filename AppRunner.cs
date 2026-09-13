using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgentRouter.businessLogic.skills;
using AgentRouter.models;
using AgentRouter.models.chat;
using Serilog;

namespace AgentRouter;

public class AppRunner
{
    private readonly IConfiguration _config;
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly string _defaultModel;

    private List<Skill> _skills = [];
    private HttpClient? _httpClient;

    public HttpClient HttpClient =>
        _httpClient ?? throw new InvalidOperationException("Call InitializeAsync() before using HttpClient.");

    /// <summary>Number of skills currently loaded, for health/status endpoints.</summary>
    public int SkillCount => _skills.Count;

    /// <summary>
    /// Returns true when an API key is configured (without ever exposing its value).
    /// Safe to use in logs / health output — never log <see cref="_apiKey"/> itself.
    /// </summary>
    public bool HasApiKey => !string.IsNullOrWhiteSpace(_apiKey);

    /// <summary>Model used when no skill matches the input.</summary>
    public string DefaultModel => _defaultModel;

    public string ChatCompletionsUri => $"{_baseUrl.TrimEnd('/')}/v1/chat/completions";

    public string ModelsUri => $"{_baseUrl.TrimEnd('/')}/v1/models";

    /// <summary>Base URL of the configured LM Studio instance.</summary>
    public string BaseUrl => _baseUrl;

    /// <summary>Resolves the endpoint for a routed skill (per-skill override wins).</summary>
    public string GetChatCompletionsUri(Skill skill) =>
        string.IsNullOrWhiteSpace(skill.BaseUrl)
            ? ChatCompletionsUri
            : $"{skill.BaseUrl.TrimEnd('/')}/v1/chat/completions";

    /// <summary>
    /// Builds the chat payload for a matched input: returns the routed skill, the
    /// target endpoint URI, and the request body. The skill description is injected
    /// as a system prompt so the model stays in its role.
    /// </summary>
    public (Skill Skill, string Uri, object Payload) BuildChatRequest(string input)
    {
        var skill = GetTargetSkill(input);
        var payload = new
        {
            model = skill.Model,
            messages = string.IsNullOrWhiteSpace(skill.Description)
                ? new object[] { new { role = "user", content = input } }
                : new object[]
                {
                    new { role = "system", content = skill.Description },
                    new { role = "user", content = input },
                },
        };
        return (skill, GetChatCompletionsUri(skill), payload);
    }

    public AppRunner(IConfiguration config)
    {
        _config = config;
        _baseUrl = _config["LmStudio:BaseUrl"] ?? "http://127.0.0.1:1234";

        // API key resolution order (first non-empty wins):
        //   1. LmStudio:ApiKey      — appsetting.Local.json (dev) or env LmStudio__ApiKey
        //   2. LmStudio:Key         — legacy config key, kept for backwards compat
        //   3. LMSTUDIO_API_KEY     — legacy env var (single underscore, kept for compat)
        // Never put a real key in the committed appsetting.json — it is a
        // placeholder-only template. Keep real keys in the git-ignored
        // appsetting.Local.json or environment variables so they can't leak to GitHub.
        _apiKey = FirstNonEmpty(
                _config["LmStudio:ApiKey"],
                _config["LmStudio:Key"],
                _config["LMSTUDIO_API_KEY"])
            ?? string.Empty;

        _defaultModel = _config["AgentRouter:DefaultModel"] ?? "qwen/qwen3.5-9b";
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>Loads skills and sets up the HttpClient once. Call this before HandleChatAsync.</summary>
    public Task InitializeAsync()
    {
        Log.Information("Starting up...");

        var skillsDirectory = ResolveSkillsDirectory();
        _skills = SkillsLoader.LoadAll(skillsDirectory) ?? new List<Skill>();

        Log.Information("Loaded {skillsCount} skill(s) from {skillsDirectory}", _skills.Count, skillsDirectory);
        Log.Information("Upstream: {baseUrl} (API key configured: {hasApiKey})", _baseUrl, HasApiKey);

        _httpClient = new HttpClient();
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Resolves the skills directory from AgentRouter:SkillsDirectory (config or environment,
    /// relative to the app base directory), falling back to the default console layout.
    /// </summary>
    private string ResolveSkillsDirectory()
    {
        var configured = _config["AgentRouter:SkillsDirectory"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var path = Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(AppContext.BaseDirectory, configured);

            if (Directory.Exists(path))
            {
                return path;
            }

            Log.Warning("Configured skills directory not found: {skillsDirectory}; falling back to default", configured);
        }

        return Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "skills");
    }

    /// <summary>
    /// Deterministically selects the best-matching skill for the input. A longer matching
    /// trigger keyword wins over a shorter one; ties are broken by skill name, so the
    /// result never depends on file-system order.
    /// </summary>
    public Skill? SelectSkill(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        Skill? best = null;
        var bestKeywordLength = -1;

        foreach (var skill in _skills)
        {
            foreach (var keyword in skill.TriggerKeywords)
            {
                if (input.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    if (keyword.Length > bestKeywordLength
                        || (keyword.Length == bestKeywordLength && best is not null
                            && string.Compare(skill.Name, best.Name, StringComparison.OrdinalIgnoreCase) < 0))
                    {
                        best = skill;
                        bestKeywordLength = keyword.Length;
                    }
                }
            }
        }

        return best;
    }

    /// <summary>Returns the routed skill, or a default skill when nothing matches.</summary>
    public Skill GetTargetSkill(string input) =>
        SelectSkill(input) ?? new Skill { Name = "default", Model = _defaultModel };

    /// <summary>Returns the model the input should be routed to.</summary>
    public string BuildTargetModel(string input)
    {
        var target = GetTargetSkill(input);
        Log.Information("Matched with model: {Name} - {Model}", target.Name, target.Model);
        return target.Model;
    }

    /// <summary>
    /// Sends a request to LM Studio and returns the raw response so callers can forward
    /// status, headers, and (for streaming) the body as-is.
    /// </summary>
    public Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        // ResponseHeadersRead keeps the body open so SSE streams can be forwarded live.
        return HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    /// <summary>
    /// Matches input to a skill, forwards to LM Studio, and returns the parsed response.
    /// </summary>
    public async Task<ChatResponse?> HandleChatAsync(string input)
    {
        var (skill, uri, payload) = BuildChatRequest(input);
        Log.Information("Routed to skill {Name} model {Model} at {Uri}", skill.Name, skill.Model, uri);

        try
        {
            var response = await HttpClient.PostAsJsonAsync(uri, payload);
            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("Request failed: {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ChatResponse>();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while calling chat server");
            return null;
        }
    }
    /// <summary>
    /// Original console REPL mode - still works standalone if you want to run it as a CLI.
    /// </summary>
    public async Task RunAsync()
    {
        await InitializeAsync();

        Console.WriteLine("Enter request or type in exit to quit.");

        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input) || input == "exit")
                break;

            var result = await HandleChatAsync(input);
            Console.WriteLine(result?.Choices?.FirstOrDefault()?.Message?.Content ?? "(no response)");
        }

        Log.Information("Shutting down");
    }
}
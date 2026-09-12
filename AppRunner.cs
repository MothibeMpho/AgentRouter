using System.Net.Http.Json;
using AgentRouter.businessLogic.skills;
using AgentRouter.models;
using AgentRouter.models.chat;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace AgentRouter;

public class AppRunner
{
    private readonly IConfiguration _config;
    private readonly string _baseUrl;

    public AppRunner(IConfiguration config)
    {
        _config = config;
        _baseUrl = _config["LmStudio:BaseUrl"] ?? "http://127.0.0.1:1234";
    }

    public async Task RunAsync()
    {
        Log.Information("Starting up...");
        Console.WriteLine("Starting up...");

        var skillsDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "skills");
        var skills = SkillsLoader.LoadAll(skillsDirectory) ?? new List<Skill>();

        Log.Information("Loaded {skillsCount} skill(s)", skills.Count);
        Console.WriteLine($"Loaded {skills.Count} skill(s)");
        Console.WriteLine("Enter request or type in exit to quit.");

        using var httpClient = new HttpClient();
        var apiKey = _config["LmStudio:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        }

        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input) || input == "exit")
                break;

            var matched = skills.FirstOrDefault(skill => skill.TriggerKeywords.Any(k => input.Contains(k, StringComparison.OrdinalIgnoreCase)));

            if (matched is null)
            {
                Console.WriteLine("No matching skill found - using default model");
                matched = new Skill
                {
                    Name = "default",
                    Model = "qwen/qwen3.5-9b"
                };
            }

            Console.WriteLine($"Matched with model: {matched.Name} - {matched.Model}");

            var payload = new
            {
                model = matched.Model,
                messages = new[] { new { role = "user", content = input } }
            };

            try
            {
                var response = await httpClient.PostAsJsonAsync($"{_baseUrl}/v1/chat/completions", payload);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Request failed: {response.StatusCode}");
                    continue;
                }

                var result = await response.Content.ReadFromJsonAsync<ChatResponse>();
                Console.WriteLine(result?.Choices?.FirstOrDefault()?.Message?.Content ?? "(no response)");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while calling chat server");
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        Log.Information("Shutting down");
    }
}

using System.Net.Http.Json;
using AgentRouter.businessLogic.skills;
using AgentRouter.models;
using AgentRouter.models.chat;
using Microsoft.Extensions.Configuration;
using Serilog;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsetting.json", optional: false, reloadOnChange: true)
    .Build();

var baseUrl = config["LmStudio:BaseUrl"] ?? "http://127.0.0.1:1234";

Log.Information("Loading skills from the director...");
Console.WriteLine("Starting up...");

var skillsDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "skills");
var skills = SkillsLoader.LoadAll(skillsDirectory);

Log.Information("Loaded {skillsCount} skill(s)", new { skillsCount = skills.Count });
Console.WriteLine($"Loaded {skills.Count} skill(s)");
Console.WriteLine("Enter request or press type in exit to quit.");

while (true)
{
    Console.WriteLine("> ");
    var input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input) || input == "exit") break;

    var matched = skills?.FirstOrDefault(skill => skill.TriggerKeywords.Any(k => input.Contains(k, StringComparison.OrdinalIgnoreCase)));

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

    using var httpClient = new HttpClient();
    var response = await httpClient.PostAsJsonAsync
    (
      $"{baseUrl}/v1/chat/completions",
      new
      {
          model = matched.Model,
          messages = new[]
          {
              new
              {
                 role = "user", content = input
              }
          }

      }
    );

    var result = await response.Content.ReadFromJsonAsync<ChatResponse>();
    Console.WriteLine(result?.Choices?[0].Message.Content ?? "(no response)");

}


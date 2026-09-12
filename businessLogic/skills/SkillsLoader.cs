using AgentRouter.models;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentRouter.businessLogic.skills;

public static class SkillsLoader
{
    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
    .WithNamingConvention(UnderscoredNamingConvention.Instance)
    .Build();

    /// <summary>
    ///  Loads skills from a JSON file and returns a list of Skill objects.
    /// </summary>
    /// <param name="jsonFilePath"></param>
    /// <returns></returns>
    /// <exception cref="FileNotFoundException"></exception>
    public static List<Skill> LoadSkillsFromJson(string jsonFilePath)
    {
        if (!File.Exists(jsonFilePath))
        {
            throw new FileNotFoundException($"The specified JSON file was not found: {jsonFilePath}");
        }

        var skillsJson = File.ReadAllText(jsonFilePath);
        var skills = _deserializer.Deserialize<List<Skill>>(skillsJson);

        return skills;
    }

    /// <summary>
    /// Loads all skills from JSON files in the specified directory.
    /// </summary>
    /// <param name="skillsDirectory"></param>
    /// <returns></returns>
    /// <exception cref="DirectoryNotFoundException"></exception>
    public static List<Skill> LoadAll(string skillsDirectory)
    {
        if (!Directory.Exists(skillsDirectory))
        {
            throw new DirectoryNotFoundException($"The specified skills directory was not found: {skillsDirectory}");
        }

        var allSkills = new List<Skill>();

        foreach (var file in Directory.GetFiles(skillsDirectory, "*.md"))
        {
            var skillsFromFile = ReadSkillsFromFile(file);
            allSkills.Add(skillsFromFile);
        }

        return allSkills;
    }

    /// <summary>
    /// Reads skills from a JSON file and returns a Skill objects.
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private static Skill ReadSkillsFromFile(string file)
    {
        var text = File.ReadAllText(file);
        if (string.IsNullOrWhiteSpace(text))
        {
            Log.Error("The file {FilePath} is empty or contains only whitespace.", file);
            throw new InvalidOperationException($"The file {file} is empty or contains only whitespace.");
        }

        var parts = text.Split("---", 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            Log.Error("Invalid YAML format in file {FilePath}: Missing document separator", file);
            throw new InvalidOperationException("Invalid YAML format: Missing document separator");
        }

        var skill = _deserializer.Deserialize<Skill>(parts[0]);
        skill.Description = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        return skill;
    }
}
namespace AgentRouter.models;

public class Skill
{

    public required string Name { get; set; }
    public List<string> TriggerKeywords { get; set; } = [];
    public required string Model { get; set; }
    /// <summary>
    /// Optional per-skill LM Studio endpoint override. When empty, the global
    /// LmStudio:BaseUrl is used. Useful when each model runs on its own
    /// LM Studio instance/port (e.g. one on :1234, another on :1235).
    /// </summary>
    public string? BaseUrl { get; set; }
    public string Description { get; set; } = null!;
}

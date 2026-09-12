namespace AgentRouter.models;

public class Skill
{

    public required string Name { get; set; }
    public List<string> TriggerKeywords { get; set; } = [];
    public required string Model { get; set; }
    public string Description { get; set; } = null!;
}

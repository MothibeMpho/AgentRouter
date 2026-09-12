using AgentRouter.models;

namespace AgentRouter.businessLogic.skills;

public interface ISkillLoader
{
    List<Skill> LoadSkillsFromJson(string jsonFilePath);
    List<Skill> LoadAll(string skillsDirectory);
}
using CodeForgeAPI.Models;

namespace CodeForgeAPI.Services.Generators;

public interface ITemplateGenerator
{
    Dictionary<string, string> Generate(Project project);
}

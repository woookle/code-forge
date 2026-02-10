using CodeForgeAPI.Models;

namespace CodeForgeAPI.Services;

public interface ICodeGeneratorService
{
    Task<byte[]> GenerateProjectZipAsync(Guid projectId);
}

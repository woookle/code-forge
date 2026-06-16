using System.IO.Compression;
using System.Text;
using CodeForgeAPI.Models;
using CodeForgeAPI.Services.Generators;
using Microsoft.EntityFrameworkCore;
using CodeForgeAPI.Data;

namespace CodeForgeAPI.Services;

public class CodeGeneratorService : ICodeGeneratorService
{
    private readonly ApplicationDbContext _context;

    public CodeGeneratorService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<byte[]> GenerateProjectZipAsync(Guid projectId)
    {
        // Load project with all related data.
        // Generators consume entity.Fields (with RelatedEntityId / RelationshipType)
        // and need all sibling entities to resolve cross-references.
        // SourceRelationships / TargetRelationships are NOT used by the generators,
        // so we only load Entities → Fields to keep the query lean.
        var project = await _context.Projects
            .Include(p => p.Entities)
                .ThenInclude(e => e.Fields)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
            throw new Exception("Project not found");

        if (!project.Entities.Any())
            throw new InvalidOperationException(
                "Project has no entities. Add at least one entity before generating.");

        // Select appropriate generator based on target stack and architecture type
        bool isMicroservices = project.ArchitectureType == "Microservices";
        ITemplateGenerator generator = (project.TargetStack, isMicroservices) switch
        {
            ("CSharp_PostgreSQL", false) => new CSharpPostgreSQLGenerator(),
            ("NodeJS_MongoDB",    false) => new NodeJSMongoDBGenerator(),
            ("CSharp_PostgreSQL", true)  => new CSharpPostgreSQLMicroservicesGenerator(),
            ("NodeJS_MongoDB",    true)  => new NodeJSMongoDBMicroservicesGenerator(),
            _ => throw new NotSupportedException(
                $"Target stack '{project.TargetStack}' is not supported. " +
                "Supported values: CSharp_PostgreSQL, NodeJS_MongoDB")
        };

        // Generate file tree
        var fileTree = generator.Generate(project);

        // Create ZIP archive in memory with UTF-8 content
        return CreateZipArchive(fileTree);
    }

    private static byte[] CreateZipArchive(Dictionary<string, string> fileTree)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in fileTree)
            {
                // Normalize path separators to forward-slash (zip standard)
                var normalizedPath = path.Replace('\\', '/');
                var entry = archive.CreateEntry(normalizedPath, CompressionLevel.Optimal);

                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                writer.Write(content);
            }
        }

        return memoryStream.ToArray();
    }
}

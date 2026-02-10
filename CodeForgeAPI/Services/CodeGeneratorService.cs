using System.IO.Compression;
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
        // Load project with all related data
        var project = await _context.Projects
            .Include(p => p.Entities)
                .ThenInclude(e => e.Fields)
            .Include(p => p.Entities)
                .ThenInclude(e => e.SourceRelationships)
                    .ThenInclude(r => r.TargetEntity)
            .Include(p => p.Entities)
                .ThenInclude(e => e.TargetRelationships)
                    .ThenInclude(r => r.SourceEntity)
            .FirstOrDefaultAsync(p => p.Id == projectId);
        
        if (project == null)
            throw new Exception("Project not found");
        
        // Select appropriate generator based on target stack
        ITemplateGenerator generator = project.TargetStack switch
        {
            "CSharp_PostgreSQL" => new CSharpPostgreSQLGenerator(),
            "NodeJS_MongoDB" => new NodeJSMongoDBGenerator(),
            _ => throw new NotSupportedException($"Target stack '{project.TargetStack}' is not supported")
        };
        
        // Generate file tree
        var fileTree = generator.Generate(project);
        
        // Create ZIP archive
        return CreateZipArchive(fileTree);
    }
    
    private byte[] CreateZipArchive(Dictionary<string, string> fileTree)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            foreach (var file in fileTree)
            {
                var entry = archive.CreateEntry(file.Key);
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream);
                writer.Write(file.Value);
            }
        }
        
        return memoryStream.ToArray();
    }
}

using System.Text;
using System.Text.Json;
using CodeForgeAPI.Models;

namespace CodeForgeAPI.Services.Generators;

/// <summary>
/// Generates a C# + PostgreSQL microservices project.
/// Each unique ServiceName groups entities into one independent ASP.NET Web API
/// with its own PostgreSQL database, communicating via RabbitMQ.
/// </summary>
public class CSharpPostgreSQLMicroservicesGenerator : ITemplateGenerator
{
    private static readonly Dictionary<string, string> Plurals = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Category", "Categories" }, { "Entity", "Entities" }, { "Property", "Properties" },
        { "Story", "Stories" }, { "City", "Cities" }, { "Country", "Countries" },
        { "Company", "Companies" }, { "Activity", "Activities" }, { "Library", "Libraries" },
        { "Query", "Queries" }, { "Policy", "Policies" }, { "Reply", "Replies" },
        { "Entry", "Entries" }, { "Person", "People" }, { "Status", "Statuses" },
        { "Leaf", "Leaves" }, { "Shelf", "Shelves" }, { "Half", "Halves" },
    };

    public Dictionary<string, string> Generate(Project project)
    {
        var files = new Dictionary<string, string>();
        var projectName = SanitizeName(project.Name);

        AuthConfig? auth = null;
        if (!string.IsNullOrEmpty(project.AuthConfig))
            auth = JsonSerializer.Deserialize<AuthConfig>(project.AuthConfig,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Group entities by service name
        var serviceGroups = project.Entities
            .GroupBy(e => string.IsNullOrWhiteSpace(e.ServiceName) ? e.Name : e.ServiceName)
            .ToList();

        int portBase = 5001;
        int pgPortBase = 5433;
        var serviceInfos = serviceGroups.Select((g, i) => new
        {
            ServiceName = g.Key,
            SafeName = SanitizeName(g.Key),
            Port = portBase + i,
            PgPort = pgPortBase + i,
            DbName = g.Key.ToLower().Replace(" ", "_") + "_db",
            Entities = g.ToList()
        }).ToList();

        foreach (var svc in serviceInfos)
        {
            var basePath = $"{projectName}/services/{svc.SafeName}Service";
            var svcName = svc.SafeName + "Service";
            var allProjectEntities = project.Entities.ToList();

            // Models
            foreach (var entity in svc.Entities)
                files[$"{basePath}/Models/{entity.Name}.cs"] = GenerateModel(entity, allProjectEntities, svcName);

            // DTOs
            foreach (var entity in svc.Entities)
                files[$"{basePath}/DTOs/{entity.Name}Dto.cs"] = GenerateDto(entity, allProjectEntities, svcName);

            // Controllers
            foreach (var entity in svc.Entities)
                files[$"{basePath}/Controllers/{entity.Name}Controller.cs"] =
                    GenerateController(entity, allProjectEntities, svcName, auth);

            // DbContext
            files[$"{basePath}/Data/ApplicationDbContext.cs"] = GenerateDbContext(svc.Entities, svcName);

            // Messaging
            files[$"{basePath}/Messaging/RabbitMqPublisher.cs"] = GeneratePublisher(svc.Entities, svcName);
            // Pass entity names from OTHER services so subscriber binds to entity-level routing keys (e.g. "orderItem.created")
            var otherEntityNames = serviceInfos
                .Where(s => s.SafeName != svc.SafeName)
                .SelectMany(s => s.Entities.Select(e => e.Name))
                .ToList();
            files[$"{basePath}/Messaging/RabbitMqSubscriber.cs"] = GenerateSubscriber(svc.SafeName, svc.Entities, otherEntityNames, svcName);
            files[$"{basePath}/Messaging/IEventPublisher.cs"] = GenerateIEventPublisher(svcName);

            // Middleware
            files[$"{basePath}/Middleware/ErrorHandlerMiddleware.cs"] = GenerateErrorMiddleware(svcName);

            // Program.cs
            files[$"{basePath}/Program.cs"] = GenerateProgram(svc.Entities, svcName, auth);

            // .csproj
            files[$"{basePath}/{svcName}.csproj"] = GenerateProjectFile();

            // appsettings
            files[$"{basePath}/appsettings.json"] = GenerateAppSettings(svc.DbName);
            files[$"{basePath}/appsettings.Development.json"] = GenerateAppSettingsDev();

            // launchSettings
            files[$"{basePath}/Properties/launchSettings.json"] = GenerateLaunchSettings(svcName, svc.Port);

            // Dockerfile
            files[$"{basePath}/Dockerfile"] = GenerateDockerfile(svcName);

            // .gitignore
            files[$"{basePath}/.gitignore"] = GenerateGitignore();
        }

        // Dedicated auth-service (created separately instead of injecting into every service)
        (int Port, int PgPort, string DbName)? authSvc = null;
        if (auth?.Enabled == true)
        {
            int authPort = portBase + serviceInfos.Count;
            int authPgPort = pgPortBase + serviceInfos.Count;
            const string authDbName = "auth_db";
            authSvc = (authPort, authPgPort, authDbName);
            GenerateAuthMicroserviceFiles(files, projectName, auth, authPort, authPgPort, authDbName);
        }

        // Root docker-compose.yml
        files[$"{projectName}/docker-compose.yml"] = GenerateDockerCompose(projectName,
            serviceInfos.Select(s => (s.SafeName, s.Port, s.PgPort, s.DbName)).ToList(), authSvc);

        // Root README
        files[$"{projectName}/README.md"] = GenerateReadme(project, projectName,
            serviceInfos.Select(s => (s.SafeName, s.Port, s.DbName, s.Entities.Select(e => e.Name).ToList())).ToList(),
            authSvc.HasValue ? authSvc.Value.Port : null);

        return files;
    }

    // ─────────────────────────── MODEL ───────────────────────────

    private string GenerateModel(Entity entity, List<Entity> allEntities, string projectName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.ComponentModel.DataAnnotations;");
        sb.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.Models;");
        sb.AppendLine();
        sb.AppendLine($"public class {entity.Name}");
        sb.AppendLine("{");

        bool hasPk = entity.Fields.Any(f => f.IsPrimaryKey);
        if (!hasPk)
        {
            sb.AppendLine("    [Key]");
            sb.AppendLine("    public Guid Id { get; set; } = Guid.NewGuid();");
            sb.AppendLine();
        }

        foreach (var field in entity.Fields.Where(f => f.DataType != "Relationship").OrderBy(f => f.DisplayOrder))
        {
            if (field.IsPrimaryKey) sb.AppendLine("    [Key]");
            if (field.IsRequired && field.DataType is "String" or "Text") sb.AppendLine("    [Required]");
            if (field.DataType is "String" or "Text") sb.AppendLine($"    [MaxLength({(field.DataType == "Text" ? 5000 : 500)})]");

            var csType = MapDataTypeToCSharp(field.DataType);
            var nullable = (!field.IsRequired && IsValueType(field.DataType)) ? "?" : "";
            var defaultVal = GetDefaultValue(field.DataType, field.IsPrimaryKey);
            sb.AppendLine($"    public {csType}{nullable} {field.Name} {{ get; set; }}{defaultVal}");
            sb.AppendLine();
        }

        var addedManyToMany = new HashSet<string>();
        foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship").OrderBy(f => f.DisplayOrder))
        {
            if (!field.RelatedEntityId.HasValue) continue;
            var related = allEntities.FirstOrDefault(e => e.Id == field.RelatedEntityId);
            if (related == null) continue;

            bool sameService = (entity.ServiceName ?? entity.Name) == (related.ServiceName ?? related.Name);

            if (!sameService)
            {
                sb.AppendLine($"    // Cross-service reference to {related.Name} — resolved via RabbitMQ events");
                sb.AppendLine($"    public Guid? {related.Name}RefId {{ get; set; }}");
                sb.AppendLine();
                continue;
            }

            if (field.RelationshipType == "ManyToMany")
            {
                var cn = Pluralize(related.Name);
                if (!addedManyToMany.Contains(cn))
                {
                    sb.AppendLine($"    public ICollection<{related.Name}> {cn} {{ get; set; }} = new List<{related.Name}>();");
                    sb.AppendLine();
                    addedManyToMany.Add(cn);
                }
            }
            else
            {
                sb.AppendLine($"    public Guid? {related.Name}Id {{ get; set; }}");
                sb.AppendLine($"    [ForeignKey(nameof({related.Name}Id))]");
                sb.AppendLine($"    public {related.Name}? {related.Name} {{ get; set; }}");
                sb.AppendLine();
            }
        }

        foreach (var otherEntity in allEntities.Where(e => e.Id != entity.Id))
        {
            foreach (var field in otherEntity.Fields.Where(f =>
                f.DataType == "Relationship" && f.RelatedEntityId == entity.Id && f.RelationshipType == "OneToMany"))
            {
                bool sameService = (entity.ServiceName ?? entity.Name) == (otherEntity.ServiceName ?? otherEntity.Name);
                if (!sameService) continue;
                sb.AppendLine($"    public ICollection<{otherEntity.Name}> {Pluralize(otherEntity.Name)} {{ get; set; }} = new List<{otherEntity.Name}>();");
                sb.AppendLine();
            }
        }

        sb.AppendLine("    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;");
        sb.AppendLine("    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;");
        sb.AppendLine("}");

        return sb.ToString();
    }

    // ─────────────────────────── DTO ───────────────────────────

    private string GenerateDto(Entity entity, List<Entity> allEntities, string projectName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.ComponentModel.DataAnnotations;");
        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.DTOs;");
        sb.AppendLine();

        // Helper: for a relationship field, FK prop name = relatedEntityName + "Id"
        string FkName(Field f)
        {
            var rel = allEntities.FirstOrDefault(e => e.Id == f.RelatedEntityId);
            return rel != null ? rel.Name + "Id" : f.Name + "Id";
        }

        // Relationship fields that have a FK column (no M2M)
        var fkFields = entity.Fields
            .Where(f => f.DataType == "Relationship" && f.RelationshipType != "ManyToMany")
            .OrderBy(f => f.DisplayOrder)
            .ToList();

        // Response
        sb.AppendLine($"public class {entity.Name}Response");
        sb.AppendLine("{");
        sb.AppendLine("    public Guid Id { get; set; }");
        foreach (var field in entity.Fields.Where(f => f.DataType != "Relationship" && !f.IsPrimaryKey).OrderBy(f => f.DisplayOrder))
        {
            var csType = MapDataTypeToCSharp(field.DataType);
            var nullable = (!field.IsRequired && IsValueType(field.DataType)) ? "?" : "";
            sb.AppendLine($"    public {csType}{nullable} {field.Name} {{ get; set; }}");
        }
        foreach (var field in fkFields)
            sb.AppendLine($"    public Guid? {FkName(field)} {{ get; set; }}");
        sb.AppendLine("    public DateTime CreatedAt { get; set; }");
        sb.AppendLine("    public DateTime UpdatedAt { get; set; }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Create
        sb.AppendLine($"public class Create{entity.Name}Request");
        sb.AppendLine("{");
        foreach (var field in entity.Fields.Where(f => f.DataType != "Relationship" && !f.IsPrimaryKey).OrderBy(f => f.DisplayOrder))
        {
            var csType = MapDataTypeToCSharp(field.DataType);
            var nullable = (!field.IsRequired && IsValueType(field.DataType)) ? "?" : "";
            if (field.IsRequired && field.DataType is "String" or "Text") sb.AppendLine("    [Required]");
            if (field.DataType is "String" or "Text") sb.AppendLine("    [MaxLength(500)]");
            sb.AppendLine($"    public {csType}{nullable} {field.Name} {{ get; set; }} = {GetInitDefault(field.DataType)};");
        }
        foreach (var field in fkFields)
        {
            if (field.IsRequired) sb.AppendLine("    [Required]");
            sb.AppendLine($"    public Guid? {FkName(field)} {{ get; set; }}");
        }
        sb.AppendLine("}");
        sb.AppendLine();

        // Update
        sb.AppendLine($"public class Update{entity.Name}Request");
        sb.AppendLine("{");
        foreach (var field in entity.Fields.Where(f => f.DataType != "Relationship" && !f.IsPrimaryKey).OrderBy(f => f.DisplayOrder))
        {
            var csType = MapDataTypeToCSharp(field.DataType);
            var nullable = IsValueType(field.DataType) ? "?" : "";
            if (field.DataType is "String" or "Text") sb.AppendLine("    [MaxLength(500)]");
            sb.AppendLine($"    public {csType}{nullable} {field.Name} {{ get; set; }}");
        }
        foreach (var field in fkFields)
            sb.AppendLine($"    public Guid? {FkName(field)} {{ get; set; }}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    // ─────────────────────────── CONTROLLER ───────────────────────────

    private string GenerateController(Entity entity, List<Entity> allEntities, string projectName, AuthConfig? auth)
    {
        var sb = new StringBuilder();
        var name = entity.Name;
        var nameLower = LowerFirst(name);
        var namePlural = Pluralize(name);
        var namePluralLower = LowerFirst(namePlural);

        var includes = entity.Fields
            .Where(f => f.DataType == "Relationship" && f.RelatedEntityId.HasValue)
            .Select(f => new { Field = f, Related = allEntities.FirstOrDefault(e => e.Id == f.RelatedEntityId) })
            .Where(x => x.Related != null && (entity.ServiceName ?? entity.Name) == (x.Related.ServiceName ?? x.Related.Name))
            .Select(x => x.Field.RelationshipType == "ManyToMany" ? Pluralize(x.Related!.Name) : x.Related!.Name)
            .Distinct().ToList();

        sb.AppendLine("using Microsoft.AspNetCore.Mvc;");
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        if (auth?.Enabled == true) sb.AppendLine("using Microsoft.AspNetCore.Authorization;");
        sb.AppendLine($"using {projectName}.Data;");
        sb.AppendLine($"using {projectName}.DTOs;");
        sb.AppendLine($"using {projectName}.Models;");
        sb.AppendLine($"using {projectName}.Messaging;");
        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.Controllers;");
        sb.AppendLine();
        sb.AppendLine("[ApiController]");
        sb.AppendLine("[Route(\"api/[controller]\")]");
        sb.AppendLine("[Produces(\"application/json\")]");
        if (auth?.Enabled == true)
        {
            var prot = auth.EntityProtection.GetValueOrDefault(name);
            bool allProt = prot != null && prot.Get && prot.Post && prot.Put && prot.Patch && prot.Delete;
            if (allProt) sb.AppendLine("[Authorize]");
        }
        sb.AppendLine($"public class {name}Controller : ControllerBase");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly ApplicationDbContext _context;");
        sb.AppendLine("    private readonly IEventPublisher _publisher;");
        sb.AppendLine($"    private readonly ILogger<{name}Controller> _logger;");
        sb.AppendLine();
        sb.AppendLine($"    public {name}Controller(ApplicationDbContext context, IEventPublisher publisher, ILogger<{name}Controller> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        _context = context;");
        sb.AppendLine("        _publisher = publisher;");
        sb.AppendLine("        _logger = logger;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Per-method auth helpers
        var entityProt2 = auth?.Enabled == true ? auth.EntityProtection.GetValueOrDefault(name) : null;
        bool classAuth2 = entityProt2 != null && entityProt2.Get && entityProt2.Post && entityProt2.Put && entityProt2.Patch && entityProt2.Delete;
        string MA(bool methodProt) => (auth?.Enabled == true && methodProt && !classAuth2) ? "    [Authorize]\n" : "";

        // GET all
        sb.Append(MA(entityProt2?.Get == true));
        sb.AppendLine($"    [HttpGet]");
        sb.AppendLine($"    [ProducesResponseType(typeof(IEnumerable<{name}Response>), StatusCodes.Status200OK)]");
        sb.AppendLine($"    public async Task<ActionResult<IEnumerable<{name}Response>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)");
        sb.AppendLine("    {");
        sb.AppendLine("        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);");
        sb.Append($"        var query = _context.{namePlural}");
        if (includes.Count > 0)
        {
            sb.AppendLine();
            foreach (var inc in includes) sb.AppendLine($"            .Include(x => x.{inc})");
            sb.AppendLine("            .AsNoTracking();");
        }
        else sb.AppendLine(".AsNoTracking();");
        sb.AppendLine("        var total = await query.CountAsync();");
        sb.AppendLine("        var rawItems = await query.OrderByDescending(x => x.CreatedAt).Skip((page-1)*pageSize).Take(pageSize).ToListAsync();");
        sb.AppendLine("        var items = rawItems.Select(MapToResponse).ToList();");
        sb.AppendLine("        Response.Headers.Append(\"X-Total-Count\", total.ToString());");
        sb.AppendLine("        return Ok(items);");
        sb.AppendLine("    }");
        sb.AppendLine();

        // GET by id
        sb.Append(MA(entityProt2?.Get == true));
        sb.AppendLine($"    [HttpGet(\"{{id:guid}}\")]");
        sb.AppendLine($"    [ProducesResponseType(typeof({name}Response), StatusCodes.Status200OK)]");
        sb.AppendLine($"    [ProducesResponseType(StatusCodes.Status404NotFound)]");
        sb.AppendLine($"    public async Task<ActionResult<{name}Response>> GetById(Guid id)");
        sb.AppendLine("    {");
        sb.Append($"        var {nameLower} = await _context.{namePlural}");
        if (includes.Count > 0)
        {
            sb.AppendLine();
            foreach (var inc in includes) sb.AppendLine($"            .Include(x => x.{inc})");
            sb.AppendLine($"            .AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);");
        }
        else sb.AppendLine($".AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);");
        sb.AppendLine($"        if ({nameLower} == null) return NotFound(new {{ message = \"{name} not found\" }});");
        sb.AppendLine($"        return Ok(MapToResponse({nameLower}));");
        sb.AppendLine("    }");
        sb.AppendLine();

        // POST
        sb.Append(MA(entityProt2?.Post == true));
        sb.AppendLine($"    [HttpPost]");
        sb.AppendLine($"    [ProducesResponseType(typeof({name}Response), StatusCodes.Status201Created)]");
        sb.AppendLine($"    public async Task<ActionResult<{name}Response>> Create([FromBody] Create{name}Request request)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (!ModelState.IsValid) return BadRequest(ModelState);");
        sb.AppendLine($"        var {nameLower} = new {name} {{ Id = Guid.NewGuid(),");
        foreach (var field in entity.Fields.Where(f => f.DataType != "Relationship" && !f.IsPrimaryKey).OrderBy(f => f.DisplayOrder))
            sb.AppendLine($"            {field.Name} = request.{field.Name},");
        foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship" && f.RelationshipType != "ManyToMany").OrderBy(f => f.DisplayOrder))
        {
            var rel = allEntities.FirstOrDefault(e => e.Id == field.RelatedEntityId);
            var fkName = rel != null ? rel.Name + "Id" : field.Name + "Id";
            sb.AppendLine($"            {fkName} = request.{fkName},");
        }
        sb.AppendLine("            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };");
        sb.AppendLine($"        _context.{namePlural}.Add({nameLower});");
        sb.AppendLine("        await _context.SaveChangesAsync();");
        sb.AppendLine($"        await _publisher.PublishAsync(\"{LowerFirst(name)}.created\", MapToResponse({nameLower}));");
        sb.AppendLine($"        return CreatedAtAction(nameof(GetById), new {{ id = {nameLower}.Id }}, MapToResponse({nameLower}));");
        sb.AppendLine("    }");
        sb.AppendLine();

        // PUT
        sb.Append(MA(entityProt2?.Put == true));
        sb.AppendLine($"    [HttpPut(\"{{id:guid}}\")]");
        sb.AppendLine($"    [ProducesResponseType(typeof({name}Response), StatusCodes.Status200OK)]");
        sb.AppendLine($"    public async Task<ActionResult<{name}Response>> Update(Guid id, [FromBody] Update{name}Request request)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (!ModelState.IsValid) return BadRequest(ModelState);");
        sb.AppendLine($"        var {nameLower} = await _context.{namePlural}.FindAsync(id);");
        sb.AppendLine($"        if ({nameLower} == null) return NotFound(new {{ message = \"{name} not found\" }});");
        foreach (var field in entity.Fields.Where(f => f.DataType != "Relationship" && !f.IsPrimaryKey).OrderBy(f => f.DisplayOrder))
            sb.AppendLine($"        {nameLower}.{field.Name} = request.{field.Name}{(IsValueType(field.DataType) ? $" ?? {nameLower}.{field.Name}" : "")};");
        foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship" && f.RelationshipType != "ManyToMany").OrderBy(f => f.DisplayOrder))
        {
            var rel = allEntities.FirstOrDefault(e => e.Id == field.RelatedEntityId);
            var fkName = rel != null ? rel.Name + "Id" : field.Name + "Id";
            sb.AppendLine($"        {nameLower}.{fkName} = request.{fkName};");
        }
        sb.AppendLine($"        {nameLower}.UpdatedAt = DateTime.UtcNow;");
        sb.AppendLine("        await _context.SaveChangesAsync();");
        sb.AppendLine($"        await _publisher.PublishAsync(\"{LowerFirst(name)}.updated\", MapToResponse({nameLower}));");
        sb.AppendLine($"        return Ok(MapToResponse({nameLower}));");
        sb.AppendLine("    }");
        sb.AppendLine();

        // DELETE
        sb.Append(MA(entityProt2?.Delete == true));
        sb.AppendLine($"    [HttpDelete(\"{{id:guid}}\")]");
        sb.AppendLine("    [ProducesResponseType(StatusCodes.Status204NoContent)]");
        sb.AppendLine($"    public async Task<IActionResult> Delete(Guid id)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var {nameLower} = await _context.{namePlural}.FindAsync(id);");
        sb.AppendLine($"        if ({nameLower} == null) return NotFound(new {{ message = \"{name} not found\" }});");
        sb.AppendLine($"        _context.{namePlural}.Remove({nameLower});");
        sb.AppendLine("        await _context.SaveChangesAsync();");
        sb.AppendLine($"        await _publisher.PublishAsync(\"{LowerFirst(name)}.deleted\", new {{ id }});");
        sb.AppendLine("        return NoContent();");
        sb.AppendLine("    }");
        sb.AppendLine();

        // MapToResponse
        sb.AppendLine($"    private static {name}Response MapToResponse({name} x) => new()");
        sb.AppendLine("    {");
        sb.AppendLine("        Id = x.Id,");
        foreach (var field in entity.Fields.Where(f => f.DataType != "Relationship" && !f.IsPrimaryKey).OrderBy(f => f.DisplayOrder))
            sb.AppendLine($"        {field.Name} = x.{field.Name},");
        foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship" && f.RelationshipType != "ManyToMany").OrderBy(f => f.DisplayOrder))
        {
            var rel = allEntities.FirstOrDefault(e => e.Id == field.RelatedEntityId);
            var fkName = rel != null ? rel.Name + "Id" : field.Name + "Id";
            sb.AppendLine($"        {fkName} = x.{fkName},");
        }
        sb.AppendLine("        CreatedAt = x.CreatedAt,");
        sb.AppendLine("        UpdatedAt = x.UpdatedAt");
        sb.AppendLine("    };");
        sb.AppendLine("}");

        return sb.ToString();
    }

    // ─────────────────────────── DB CONTEXT ───────────────────────────

    private string GenerateDbContext(List<Entity> entities, string projectName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine($"using {projectName}.Models;");
        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.Data;");
        sb.AppendLine();
        sb.AppendLine("public class ApplicationDbContext : DbContext");
        sb.AppendLine("{");
        sb.AppendLine("    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }");
        sb.AppendLine();
        foreach (var entity in entities)
            sb.AppendLine($"    public DbSet<{entity.Name}> {Pluralize(entity.Name)} {{ get; set; }}");
        sb.AppendLine();
        sb.AppendLine("    protected override void OnModelCreating(ModelBuilder modelBuilder)");
        sb.AppendLine("    {");
        sb.AppendLine("        base.OnModelCreating(modelBuilder);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public override int SaveChanges()");
        sb.AppendLine("    { UpdateTimestamps(); return base.SaveChanges(); }");
        sb.AppendLine("    public override Task<int> SaveChangesAsync(CancellationToken ct = default)");
        sb.AppendLine("    { UpdateTimestamps(); return base.SaveChangesAsync(ct); }");
        sb.AppendLine("    private void UpdateTimestamps()");
        sb.AppendLine("    {");
        sb.AppendLine("        foreach (var e in ChangeTracker.Entries().Where(e => e.State == EntityState.Modified))");
        sb.AppendLine("            if (e.Properties.Any(p => p.Metadata.Name == \"UpdatedAt\"))");
        sb.AppendLine("                e.Property(\"UpdatedAt\").CurrentValue = DateTime.UtcNow;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    // ─────────────────────────── MESSAGING ───────────────────────────

    private string GenerateIEventPublisher(string projectName) =>
$@"namespace {projectName}.Messaging;

/// <summary>Abstraction for publishing domain events to RabbitMQ.</summary>
public interface IEventPublisher
{{
    Task PublishAsync<T>(string routingKey, T payload) where T : class;
}}
";

    private string GeneratePublisher(List<Entity> entities, string projectName)
    {
        return $@"using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace {projectName}.Messaging;

/// <summary>
/// Publishes domain events to the RabbitMQ 'events' topic exchange.
/// Events: entity.created, entity.updated, entity.deleted
/// </summary>
public class RabbitMqPublisher : IEventPublisher, IDisposable
{{
    private const string Exchange = ""events"";
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqPublisher(IConfiguration config, ILogger<RabbitMqPublisher> logger)
    {{
        _logger = logger;
        var factory = new ConnectionFactory
        {{
            Uri = new Uri(config[""RabbitMQ:Url""] ?? ""amqp://guest:guest@rabbitmq:5672""),
            DispatchConsumersAsync = true
        }};
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.ExchangeDeclare(Exchange, ExchangeType.Topic, durable: true);
        logger.LogInformation(""✅ RabbitMQ publisher connected"");
    }}

    public Task PublishAsync<T>(string routingKey, T payload) where T : class
    {{
        try
        {{
            var json = JsonSerializer.Serialize(payload);
            var body = Encoding.UTF8.GetBytes(json);
            var props = _channel.CreateBasicProperties();
            props.Persistent = true;
            _channel.BasicPublish(Exchange, routingKey, props, body);
            _logger.LogInformation(""[Publisher] Event published: {{RoutingKey}}"", routingKey);
        }}
        catch (Exception ex)
        {{
            _logger.LogWarning(ex, ""[Publisher] Failed to publish event {{RoutingKey}}"", routingKey);
        }}
        return Task.CompletedTask;
    }}

    public void Dispose()
    {{
        _channel?.Dispose();
        _connection?.Dispose();
    }}
}}
";
    }

    // otherEntityNames: entity names from all other services (used as routing key prefixes)
    private string GenerateSubscriber(string serviceName, List<Entity> ownEntities,
        List<string> otherEntityNames, string projectName)
    {
        var bindingKeys = otherEntityNames.SelectMany(n => new[]
        {
            $"{LowerFirst(n)}.created", $"{LowerFirst(n)}.updated", $"{LowerFirst(n)}.deleted"
        }).ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"using System.Text;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using RabbitMQ.Client;");
        sb.AppendLine("using RabbitMQ.Client.Events;");
        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.Messaging;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Subscribes to events from other microservices and routes them to handlers.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public class RabbitMqSubscriber : BackgroundService");
        sb.AppendLine("{");
        sb.AppendLine("    private const string Exchange = \"events\";");
        sb.AppendLine($"    private const string Queue = \"{serviceName.ToLower()}-service-queue\";");
        sb.AppendLine();
        sb.AppendLine("    private static readonly string[] BindingKeys =");
        sb.AppendLine("    [");
        foreach (var key in bindingKeys) sb.AppendLine($"        \"{key}\",");
        sb.AppendLine("    ];");
        sb.AppendLine();
        sb.AppendLine("    private readonly IConfiguration _config;");
        sb.AppendLine("    private readonly ILogger<RabbitMqSubscriber> _logger;");
        sb.AppendLine();
        sb.AppendLine("    public RabbitMqSubscriber(IConfiguration config, ILogger<RabbitMqSubscriber> logger)");
        sb.AppendLine("    { _config = config; _logger = logger; }");
        sb.AppendLine();
        sb.AppendLine("    protected override Task ExecuteAsync(CancellationToken stoppingToken)");
        sb.AppendLine("    {");
        sb.AppendLine("        stoppingToken.Register(() => _logger.LogInformation(\"RabbitMQ subscriber stopping\"));");
        sb.AppendLine("        Task.Run(() => StartListening(stoppingToken), stoppingToken);");
        sb.AppendLine("        return Task.CompletedTask;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private void StartListening(CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            var factory = new ConnectionFactory");
        sb.AppendLine("            {");
        sb.AppendLine("                Uri = new Uri(_config[\"RabbitMQ:Url\"] ?? \"amqp://guest:guest@rabbitmq:5672\"),");
        sb.AppendLine("                DispatchConsumersAsync = true");
        sb.AppendLine("            };");
        sb.AppendLine("            using var conn = factory.CreateConnection();");
        sb.AppendLine("            using var channel = conn.CreateModel();");
        sb.AppendLine("            channel.ExchangeDeclare(Exchange, ExchangeType.Topic, durable: true);");
        sb.AppendLine("            channel.QueueDeclare(Queue, durable: true, exclusive: false, autoDelete: false);");
        sb.AppendLine("            foreach (var key in BindingKeys) channel.QueueBind(Queue, Exchange, key);");
        sb.AppendLine();
        sb.AppendLine("            _logger.LogInformation(\"✅ RabbitMQ subscriber listening on queue: {Queue}\", Queue);");
        sb.AppendLine();
        sb.AppendLine("            var consumer = new AsyncEventingBasicConsumer(channel);");
        sb.AppendLine("            consumer.Received += async (_, ea) =>");
        sb.AppendLine("            {");
        sb.AppendLine("                var key = ea.RoutingKey;");
        sb.AppendLine("                var body = Encoding.UTF8.GetString(ea.Body.ToArray());");
        sb.AppendLine("                _logger.LogInformation(\"[Subscriber] Received: {Key}\", key);");
        sb.AppendLine("                HandleEvent(key, body);");
        sb.AppendLine("                channel.BasicAck(ea.DeliveryTag, false);");
        sb.AppendLine("                await Task.CompletedTask;");
        sb.AppendLine("            };");
        sb.AppendLine("            channel.BasicConsume(Queue, false, consumer);");
        sb.AppendLine("            while (!ct.IsCancellationRequested) Thread.Sleep(1000);");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine("            _logger.LogError(ex, \"RabbitMQ subscriber error\");");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Handle incoming cross-service events. Add business logic here.</summary>");
        sb.AppendLine("    private void HandleEvent(string routingKey, string body)");
        sb.AppendLine("    {");
        sb.AppendLine("        switch (routingKey)");
        sb.AppendLine("        {");
        foreach (var entityName in otherEntityNames)
        {
            sb.AppendLine($"            case \"{LowerFirst(entityName)}.created\":");
            sb.AppendLine($"                // TODO: handle {entityName} created");
            sb.AppendLine($"                break;");
            sb.AppendLine($"            case \"{LowerFirst(entityName)}.updated\":");
            sb.AppendLine($"                // TODO: handle {entityName} updated");
            sb.AppendLine($"                break;");
            sb.AppendLine($"            case \"{LowerFirst(entityName)}.deleted\":");
            sb.AppendLine($"                // TODO: handle {entityName} deleted (e.g. cascade cleanup)");
            sb.AppendLine($"                break;");
        }
        sb.AppendLine("            default:");
        sb.AppendLine("                _logger.LogWarning(\"Unhandled event: {Key}\", routingKey);");
        sb.AppendLine("                break;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    // ─────────────────────────── PROGRAM.CS ───────────────────────────

    private string GenerateProgram(List<Entity> entities, string projectName, AuthConfig? auth)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine($"using {projectName}.Data;");
        sb.AppendLine($"using {projectName}.Messaging;");
        sb.AppendLine($"using {projectName}.Middleware;");
        if (auth?.Enabled == true)
        {
            sb.AppendLine("using Microsoft.AspNetCore.Authentication.JwtBearer;");
            sb.AppendLine("using Microsoft.IdentityModel.Tokens;");
            sb.AppendLine("using System.Text;");
        }
        sb.AppendLine();
        sb.AppendLine("var builder = WebApplication.CreateBuilder(args);");
        sb.AppendLine();
        sb.AppendLine("builder.Services.AddControllers();");
        sb.AppendLine("builder.Services.AddEndpointsApiExplorer();");
        sb.AppendLine("builder.Services.AddSwaggerGen(c =>");
        sb.AppendLine("{");
        sb.AppendLine($"    c.SwaggerDoc(\"v1\", new Microsoft.OpenApi.Models.OpenApiInfo");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        Title = \"{projectName} API\",");
        sb.AppendLine($"        Version = \"v1\",");
        sb.AppendLine($"        Description = \"Microservice generated by **CodeForge**. Entities: {string.Join(", ", entities.Select(e => e.Name))}.\"");
        sb.AppendLine($"    }});");
        if (auth?.Enabled == true)
        {
            sb.AppendLine("    c.AddSecurityDefinition(\"Bearer\", new Microsoft.OpenApi.Models.OpenApiSecurityScheme");
            sb.AppendLine("    {");
            sb.AppendLine("        In = Microsoft.OpenApi.Models.ParameterLocation.Header,");
            sb.AppendLine("        Description = \"JWT token issued by auth-service. Enter: Bearer {token}\",");
            sb.AppendLine("        Name = \"Authorization\",");
            sb.AppendLine("        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,");
            sb.AppendLine("        BearerFormat = \"JWT\", Scheme = \"bearer\"");
            sb.AppendLine("    });");
            sb.AppendLine("    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement");
            sb.AppendLine("    {");
            sb.AppendLine("        {");
            sb.AppendLine("            new Microsoft.OpenApi.Models.OpenApiSecurityScheme {");
            sb.AppendLine("                Reference = new Microsoft.OpenApi.Models.OpenApiReference");
            sb.AppendLine("                    { Id = \"Bearer\", Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme }");
            sb.AppendLine("            },");
            sb.AppendLine("            Array.Empty<string>()");
            sb.AppendLine("        }");
            sb.AppendLine("    });");
        }
        sb.AppendLine("});");
        sb.AppendLine();
        sb.AppendLine("// Database");
        sb.AppendLine("builder.Services.AddDbContext<ApplicationDbContext>(options =>");
        sb.AppendLine("    options.UseNpgsql(builder.Configuration.GetConnectionString(\"DefaultConnection\")));");
        sb.AppendLine();
        sb.AppendLine("// RabbitMQ");
        sb.AppendLine("builder.Services.AddSingleton<IEventPublisher, RabbitMqPublisher>();");
        sb.AppendLine("builder.Services.AddHostedService<RabbitMqSubscriber>();");
        sb.AppendLine();
        sb.AppendLine("builder.Services.AddCors(o => o.AddDefaultPolicy(p =>");
        sb.AppendLine("    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));");
        sb.AppendLine();
        if (auth?.Enabled == true)
        {
            sb.AppendLine("// JWT Auth");
            sb.AppendLine("var jwtKey = builder.Configuration[\"Jwt:Key\"] ?? throw new InvalidOperationException(\"Jwt:Key missing\");");
            sb.AppendLine("builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)");
            sb.AppendLine("    .AddJwtBearer(o => o.TokenValidationParameters = new()");
            sb.AppendLine("    {");
            sb.AppendLine("        ValidateIssuerSigningKey = true,");
            sb.AppendLine("        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),");
            sb.AppendLine("        ValidateIssuer = false, ValidateAudience = false");
            sb.AppendLine("    });");
            sb.AppendLine("builder.Services.AddAuthorization();");
        }
        sb.AppendLine();
        sb.AppendLine("var app = builder.Build();");
        sb.AppendLine();
        sb.AppendLine("// Auto-apply migrations on startup");
        sb.AppendLine("using (var scope = app.Services.CreateScope())");
        sb.AppendLine("{");
        sb.AppendLine("    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();");
        sb.AppendLine("    db.Database.Migrate();");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("app.UseSwagger();");
        sb.AppendLine($"app.UseSwaggerUI(c => {{ c.SwaggerEndpoint(\"/swagger/v1/swagger.json\", \"{projectName} v1\"); c.RoutePrefix = \"swagger\"; }});");
        sb.AppendLine();
        sb.AppendLine("app.UseMiddleware<ErrorHandlerMiddleware>();");
        sb.AppendLine("app.UseCors();");
        if (auth?.Enabled == true)
        {
            sb.AppendLine("app.UseAuthentication();");
            sb.AppendLine("app.UseAuthorization();");
        }
        sb.AppendLine("app.MapControllers();");
        sb.AppendLine($"app.MapGet(\"/health\", () => Results.Ok(new {{ service = \"{projectName}\", status = \"ok\" }}));");
        sb.AppendLine("app.Run();");

        return sb.ToString();
    }

    // ─────────────────────────── PROJECT FILE ───────────────────────────

    private string GenerateProjectFile() =>
@"<Project Sdk=""Microsoft.NET.Sdk.Web"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Microsoft.EntityFrameworkCore"" Version=""9.0.0"" />
    <PackageReference Include=""Microsoft.EntityFrameworkCore.Design"" Version=""9.0.0"">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include=""Npgsql.EntityFrameworkCore.PostgreSQL"" Version=""9.0.0"" />
    <PackageReference Include=""RabbitMQ.Client"" Version=""6.8.1"" />
    <PackageReference Include=""Microsoft.AspNetCore.Authentication.JwtBearer"" Version=""9.0.0"" />
    <PackageReference Include=""BCrypt.Net-Next"" Version=""4.0.3"" />
    <PackageReference Include=""Swashbuckle.AspNetCore"" Version=""6.9.0"" />
  </ItemGroup>
</Project>
";

    // ─────────────────────────── MIDDLEWARE ───────────────────────────

    private string GenerateErrorMiddleware(string projectName) =>
$@"using System.Net;
using System.Text.Json;

namespace {projectName}.Middleware;

public class ErrorHandlerMiddleware(RequestDelegate next, ILogger<ErrorHandlerMiddleware> logger)
{{
    public async Task Invoke(HttpContext context)
    {{
        try {{ await next(context); }}
        catch (Exception ex)
        {{
            logger.LogError(ex, ""Unhandled exception {{Method}} {{Path}}"", context.Request.Method, context.Request.Path);
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = ""application/json"";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new {{ message = ex.Message }}));
        }}
    }}
}}
";

    // ─────────────────────────── APPSETTINGS ───────────────────────────

    private string GenerateAppSettings(string dbName) =>
$@"{{
  ""ConnectionStrings"": {{
    ""DefaultConnection"": ""Host=localhost;Port=5432;Database={dbName};Username=postgres;Password=postgres""
  }},
  ""RabbitMQ"": {{
    ""Url"": ""amqp://guest:guest@rabbitmq:5672""
  }},
  ""Jwt"": {{
    ""Key"": ""CHANGE_ME_USE_A_LONG_RANDOM_SECRET_KEY_AT_LEAST_32_CHARS"",
    ""ExpiryMinutes"": 60
  }},
  ""Logging"": {{ ""LogLevel"": {{ ""Default"": ""Information"" }} }},
  ""AllowedHosts"": ""*""
}}
";

    private string GenerateAppSettingsDev() =>
@"{
  ""Logging"": { ""LogLevel"": { ""Default"": ""Debug"", ""Microsoft.AspNetCore"": ""Warning"" } }
}
";

    // ─────────────────────────── DOCKERFILE ───────────────────────────

    private string GenerateDockerfile(string projectName) =>
$@"FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app
COPY *.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /publish .
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENTRYPOINT [""dotnet"", ""{projectName}.dll""]
";

    // ─────────────────────────── LAUNCH SETTINGS ───────────────────────────

    private string GenerateLaunchSettings(string projectName, int port) =>
$@"{{
  ""profiles"": {{
    ""{projectName}"": {{
      ""commandName"": ""Project"",
      ""applicationUrl"": ""http://localhost:{port}"",
      ""environmentVariables"": {{ ""ASPNETCORE_ENVIRONMENT"": ""Development"" }}
    }}
  }}
}}
";

    // ─────────────────────────── GITIGNORE ───────────────────────────

    private string GenerateGitignore() =>
@"bin/
obj/
*.user
.env
.DS_Store
";

    // ─────────────────────────── DOCKER-COMPOSE ───────────────────────────

    private string GenerateDockerCompose(string projectName,
        List<(string SafeName, int Port, int PgPort, string DbName)> services,
        (int Port, int PgPort, string DbName)? authService = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("version: '3.8'");
        sb.AppendLine();
        sb.AppendLine("services:");
        sb.AppendLine();
        sb.AppendLine("  # ── Message Broker ──────────────────────────────────────────────────────────");
        sb.AppendLine("  rabbitmq:");
        sb.AppendLine("    image: rabbitmq:3.13-management-alpine");
        sb.AppendLine("    restart: unless-stopped");
        sb.AppendLine("    ports:");
        sb.AppendLine("      - \"5672:5672\"");
        sb.AppendLine("      - \"15672:15672\" # Management UI (guest/guest)");
        sb.AppendLine("    volumes:");
        sb.AppendLine("      - rabbitmq_data:/var/lib/rabbitmq");
        sb.AppendLine("    healthcheck:");
        sb.AppendLine("      test: [\"CMD\", \"rabbitmq-diagnostics\", \"ping\"]");
        sb.AppendLine("      interval: 10s");
        sb.AppendLine("      timeout: 5s");
        sb.AppendLine("      retries: 10");
        sb.AppendLine();
        sb.AppendLine("  # ── Databases ────────────────────────────────────────────────────────────────");
        if (authService.HasValue)
        {
            sb.AppendLine($"  {authService.Value.DbName}:");
            sb.AppendLine("    image: postgres:16-alpine");
            sb.AppendLine("    restart: unless-stopped");
            sb.AppendLine("    environment:");
            sb.AppendLine("      POSTGRES_USER: postgres");
            sb.AppendLine("      POSTGRES_PASSWORD: postgres");
            sb.AppendLine($"      POSTGRES_DB: {authService.Value.DbName}");
            sb.AppendLine("    volumes:");
            sb.AppendLine($"      - {authService.Value.DbName}_data:/var/lib/postgresql/data");
            sb.AppendLine("    healthcheck:");
            sb.AppendLine("      test: [\"CMD-SHELL\", \"pg_isready -U postgres\"]");
            sb.AppendLine("      interval: 10s");
            sb.AppendLine("      timeout: 5s");
            sb.AppendLine("      retries: 5");
            sb.AppendLine();
        }
        foreach (var svc in services)
        {
            sb.AppendLine($"  {svc.DbName}:");
            sb.AppendLine("    image: postgres:16-alpine");
            sb.AppendLine("    restart: unless-stopped");
            sb.AppendLine("    environment:");
            sb.AppendLine("      POSTGRES_USER: postgres");
            sb.AppendLine("      POSTGRES_PASSWORD: postgres");
            sb.AppendLine($"      POSTGRES_DB: {svc.DbName}");
            sb.AppendLine("    volumes:");
            sb.AppendLine($"      - {svc.DbName}_data:/var/lib/postgresql/data");
            sb.AppendLine("    healthcheck:");
            sb.AppendLine("      test: [\"CMD-SHELL\", \"pg_isready -U postgres\"]");
            sb.AppendLine("      interval: 10s");
            sb.AppendLine("      timeout: 5s");
            sb.AppendLine("      retries: 5");
            sb.AppendLine();
        }
        sb.AppendLine("  # ── Microservices ─────────────────────────────────────────────────────────────");
        if (authService.HasValue)
        {
            sb.AppendLine("  auth-service:");
            sb.AppendLine("    build:");
            sb.AppendLine("      context: ./services/AuthService");
            sb.AppendLine("      dockerfile: Dockerfile");
            sb.AppendLine("    restart: unless-stopped");
            sb.AppendLine($"    ports:");
            sb.AppendLine($"      - \"{authService.Value.Port}:8080\"");
            sb.AppendLine("    environment:");
            sb.AppendLine($"      - ConnectionStrings__DefaultConnection=Host={authService.Value.DbName};Port=5432;Database={authService.Value.DbName};Username=postgres;Password=postgres");
            sb.AppendLine("      - RabbitMQ__Url=amqp://guest:guest@rabbitmq:5672");
            sb.AppendLine("      - Jwt__Key=CHANGE_ME_USE_A_LONG_RANDOM_SECRET_KEY_AT_LEAST_32_CHARS");
            sb.AppendLine("      - ASPNETCORE_ENVIRONMENT=Development");
            sb.AppendLine("      - ASPNETCORE_HTTP_PORTS=8080");
            sb.AppendLine("    depends_on:");
            sb.AppendLine($"      {authService.Value.DbName}:");
            sb.AppendLine("        condition: service_healthy");
            sb.AppendLine("      rabbitmq:");
            sb.AppendLine("        condition: service_healthy");
            sb.AppendLine();
        }
        foreach (var svc in services)
        {
            var svcDirName = svc.SafeName + "Service";
            sb.AppendLine($"  {svc.SafeName.ToLower()}-service:");
            sb.AppendLine($"    build:");
            sb.AppendLine($"      context: ./services/{svcDirName}");
            sb.AppendLine("      dockerfile: Dockerfile");
            sb.AppendLine("    restart: unless-stopped");
            sb.AppendLine($"    ports:");
            sb.AppendLine($"      - \"{svc.Port}:8080\"");
            sb.AppendLine("    environment:");
            sb.AppendLine($"      - ConnectionStrings__DefaultConnection=Host={svc.DbName};Port=5432;Database={svc.DbName};Username=postgres;Password=postgres");
            sb.AppendLine("      - RabbitMQ__Url=amqp://guest:guest@rabbitmq:5672");
            if (authService.HasValue)
                sb.AppendLine("      - Jwt__Key=CHANGE_ME_USE_A_LONG_RANDOM_SECRET_KEY_AT_LEAST_32_CHARS");
            sb.AppendLine("      - ASPNETCORE_ENVIRONMENT=Development");
            sb.AppendLine("      - ASPNETCORE_HTTP_PORTS=8080");
            sb.AppendLine("    depends_on:");
            sb.AppendLine($"      {svc.DbName}:");
            sb.AppendLine("        condition: service_healthy");
            sb.AppendLine("      rabbitmq:");
            sb.AppendLine("        condition: service_healthy");
            if (authService.HasValue)
            {
                sb.AppendLine("      auth-service:");
                sb.AppendLine("        condition: service_started");
            }
            sb.AppendLine();
        }
        sb.AppendLine("volumes:");
        sb.AppendLine("  rabbitmq_data:");
        if (authService.HasValue) sb.AppendLine($"  {authService.Value.DbName}_data:");
        foreach (var svc in services) sb.AppendLine($"  {svc.DbName}_data:");

        return sb.ToString();
    }

    // ─────────────────────────── README ───────────────────────────

    private string GenerateReadme(Project project, string projectName,
        List<(string SafeName, int Port, string DbName, List<string> Entities)> services,
        int? authServicePort = null)
    {
        var authRow = authServicePort.HasValue
            ? $"\n| `AuthService` | `http://localhost:{authServicePort}` | `auth_db` | User (register/login/me) |"
            : "";
        var svcTable = authRow + "\n" + string.Join("\n", services.Select(s =>
            $"| `{s.SafeName}Service` | `http://localhost:{s.Port}` | `{s.DbName}` | {string.Join(", ", s.Entities)} |"));

        var authNote = authServicePort.HasValue
            ? $"\n> **Auth Service** (`http://localhost:{authServicePort}/swagger`): handles registration and login, issues JWT tokens used by all other services."
            : "";

        var swaggerLinks = authServicePort.HasValue
            ? $"- Auth Swagger: http://localhost:{authServicePort}/swagger\n" +
              string.Join("\n", services.Select(s => $"- {s.SafeName} Swagger: http://localhost:{s.Port}/swagger"))
            : string.Join("\n", services.Select(s => $"- {s.SafeName} Swagger: http://localhost:{s.Port}/swagger"));

        return $@"# {projectName} — C# Microservices

> Generated with **CodeForge** — ASP.NET Core + PostgreSQL + RabbitMQ Microservices

## Architecture

Each service is an independent ASP.NET Core Web API with its own PostgreSQL database.
Inter-service communication uses **RabbitMQ** (topic exchange `events`).{authNote}

## Services

| Service | URL | Database | Entities |
|---------|-----|----------|----------|
{svcTable}

## Quick Start

### 🐳 Docker (recommended)

```bash
cd {projectName}
docker-compose up --build
```

- RabbitMQ Management: http://localhost:15672 (guest / guest)
{swaggerLinks}

## Messaging Pattern

Events are published to the `events` topic exchange:
- `entity.created` — on POST
- `entity.updated` — on PUT
- `entity.deleted` — on DELETE

Each service subscribes via a durable queue in `Messaging/RabbitMqSubscriber.cs`.
Cross-service references use `EntityRefId` (string) instead of FK constraints.

## Tech Stack

- **Framework**: ASP.NET Core 8
- **ORM**: Entity Framework Core 8 (Npgsql)
- **Database**: PostgreSQL 16 (one instance per service)
- **Message Broker**: RabbitMQ 3.13
- **Messaging Library**: RabbitMQ.Client 6.x
";
    }

    // ─────────────────────────── AUTH MODULE (reused from CSharpPostgreSQLGenerator) ─────────

    private string GenerateAuthUserModel(string projectName, AuthConfig auth)
    {
        var roleField = auth.EnableRoles
            ? $"\n    public string Role {{ get; set; }} = \"{auth.Roles.FirstOrDefault() ?? "User"}\";"
            : "";
        return $@"using System.ComponentModel.DataAnnotations;
namespace {projectName}.Models;
public class User
{{
    public Guid Id {{ get; set; }} = Guid.NewGuid();
    [Required][MaxLength(255)] public string Email {{ get; set; }} = string.Empty;
    [Required] public string PasswordHash {{ get; set; }} = string.Empty;{roleField}
    public DateTime CreatedAt {{ get; set; }} = DateTime.UtcNow;
    public DateTime UpdatedAt {{ get; set; }} = DateTime.UtcNow;
}}
";
    }

    private string GenerateAuthDtos(string projectName) =>
$@"using System.ComponentModel.DataAnnotations;
namespace {projectName}.DTOs;
public class RegisterRequest {{ [Required][MaxLength(255)] public string Email {{ get; set; }} = string.Empty; [Required] public string Password {{ get; set; }} = string.Empty; }}
public class LoginRequest {{ [Required] public string Email {{ get; set; }} = string.Empty; [Required] public string Password {{ get; set; }} = string.Empty; }}
public class AuthResponse {{ public string Token {{ get; set; }} = string.Empty; public string Email {{ get; set; }} = string.Empty; public Guid Id {{ get; set; }} }}
";

    private string GenerateIAuthService(string projectName) =>
$@"using {projectName}.DTOs;
namespace {projectName}.Services;
public interface IAuthService {{ Task<AuthResponse> RegisterAsync(RegisterRequest req); Task<AuthResponse> LoginAsync(LoginRequest req); }}
";

    private string GenerateTokenService(string projectName, AuthConfig auth) =>
$@"using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using {projectName}.Models;
namespace {projectName}.Services;
public class TokenService(IConfiguration config)
{{
    public string Generate(User user)
    {{
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config[""Jwt:Key""]!));
        var token = new JwtSecurityToken(claims: [new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Email, user.Email)], expires: DateTime.UtcNow.AddMinutes({auth.TokenExpiryMinutes}), signingCredentials: new(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }}
}}
";

    private string GenerateAuthService(string projectName, AuthConfig auth) =>
$@"using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using {projectName}.Data;
using {projectName}.DTOs;
using {projectName}.Models;
namespace {projectName}.Services;
public class AuthService(ApplicationDbContext db, TokenService tokens) : IAuthService
{{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest req)
    {{
        if (await db.Users.AnyAsync(u => u.Email == req.Email)) throw new InvalidOperationException(""Email already registered"");
        var user = new User {{ Email = req.Email, PasswordHash = BCrypt.HashPassword(req.Password) }};
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return new AuthResponse {{ Id = user.Id, Email = user.Email, Token = tokens.Generate(user) }};
    }}
    public async Task<AuthResponse> LoginAsync(LoginRequest req)
    {{
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email)
            ?? throw new UnauthorizedAccessException(""Invalid credentials"");
        if (!BCrypt.Verify(req.Password, user.PasswordHash)) throw new UnauthorizedAccessException(""Invalid credentials"");
        return new AuthResponse {{ Id = user.Id, Email = user.Email, Token = tokens.Generate(user) }};
    }}
}}
";

    private string GenerateAuthController(string projectName, AuthConfig auth) =>
$@"using Microsoft.AspNetCore.Mvc;
using {projectName}.DTOs;
using {projectName}.Services;
namespace {projectName}.Controllers;
[ApiController][Route(""api/auth"")]
public class AuthController(IAuthService authService) : ControllerBase
{{
    [HttpPost(""register"")] public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest req) {{ try {{ return Ok(await authService.RegisterAsync(req)); }} catch (InvalidOperationException ex) {{ return Conflict(new {{ message = ex.Message }}); }} }}
    [HttpPost(""login"")] public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req) {{ try {{ return Ok(await authService.LoginAsync(req)); }} catch (UnauthorizedAccessException ex) {{ return Unauthorized(new {{ message = ex.Message }}); }} }}
}}
";

    // ─────────────────────────── AUTH MICROSERVICE ───────────────────────────

    private void GenerateAuthMicroserviceFiles(
        Dictionary<string, string> files,
        string projectName, AuthConfig auth, int port, int pgPort, string dbName)
    {
        var bp = $"{projectName}/services/AuthService";
        const string svcName = "AuthService";

        files[$"{bp}/Models/User.cs"] = GenerateAuthUserModel(svcName, auth);
        files[$"{bp}/DTOs/AuthDtos.cs"] = GenerateAuthDtos(svcName);
        files[$"{bp}/Services/IAuthService.cs"] = GenerateIAuthService(svcName);
        files[$"{bp}/Services/TokenService.cs"] = GenerateTokenService(svcName, auth);
        files[$"{bp}/Services/AuthService.cs"] = GenerateAuthService(svcName, auth);
        files[$"{bp}/Controllers/AuthController.cs"] = GenerateAuthMicroserviceController(svcName, auth);
        files[$"{bp}/Data/ApplicationDbContext.cs"] = GenerateAuthDbContext(svcName);
        files[$"{bp}/Messaging/IEventPublisher.cs"] = GenerateIEventPublisher(svcName);
        files[$"{bp}/Messaging/RabbitMqPublisher.cs"] = GeneratePublisher(new List<Entity>(), svcName);
        files[$"{bp}/Messaging/RabbitMqSubscriber.cs"] = GenerateAuthMicroserviceSubscriber(svcName);
        files[$"{bp}/Middleware/ErrorHandlerMiddleware.cs"] = GenerateErrorMiddleware(svcName);
        files[$"{bp}/Program.cs"] = GenerateAuthMicroserviceProgram(svcName, auth);
        files[$"{bp}/{svcName}.csproj"] = GenerateProjectFile();
        files[$"{bp}/appsettings.json"] = GenerateAppSettings(dbName);
        files[$"{bp}/appsettings.Development.json"] = GenerateAppSettingsDev();
        files[$"{bp}/Properties/launchSettings.json"] = GenerateLaunchSettings(svcName, port);
        files[$"{bp}/Dockerfile"] = GenerateDockerfile(svcName);
        files[$"{bp}/.gitignore"] = GenerateGitignore();
    }

    private string GenerateAuthMicroserviceController(string projectName, AuthConfig auth) =>
$@"using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using {projectName}.DTOs;
using {projectName}.Messaging;
using {projectName}.Services;
namespace {projectName}.Controllers;

[ApiController]
[Route(""api/auth"")]
public class AuthController(IAuthService authService, IEventPublisher publisher) : ControllerBase
{{
    [HttpPost(""register"")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest req)
    {{
        try
        {{
            var result = await authService.RegisterAsync(req);
            await publisher.PublishAsync(""user.registered"", new {{ result.Id, result.Email }});
            return Ok(result);
        }}
        catch (InvalidOperationException ex) {{ return Conflict(new {{ message = ex.Message }}); }}
    }}

    [HttpPost(""login"")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req)
    {{
        try
        {{
            var result = await authService.LoginAsync(req);
            await publisher.PublishAsync(""user.loggedin"", new {{ result.Id, result.Email }});
            return Ok(result);
        }}
        catch (UnauthorizedAccessException ex) {{ return Unauthorized(new {{ message = ex.Message }}); }}
    }}

    [Authorize]
    [HttpGet(""me"")]
    public IActionResult Me()
    {{
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);
        return Ok(new {{ id, email }});
    }}
}}
";

    private string GenerateAuthDbContext(string projectName) =>
$@"using Microsoft.EntityFrameworkCore;
using {projectName}.Models;

namespace {projectName}.Data;

public class ApplicationDbContext : DbContext
{{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) {{ }}

    public DbSet<User> Users {{ get; set; }}

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {{
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
    }}

    public override int SaveChanges()
    {{ UpdateTimestamps(); return base.SaveChanges(); }}
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {{ UpdateTimestamps(); return base.SaveChangesAsync(ct); }}
    private void UpdateTimestamps()
    {{
        foreach (var e in ChangeTracker.Entries().Where(e => e.State == EntityState.Modified))
            if (e.Properties.Any(p => p.Metadata.Name == ""UpdatedAt""))
                e.Property(""UpdatedAt"").CurrentValue = DateTime.UtcNow;
    }}
}}
";

    private string GenerateAuthMicroserviceProgram(string projectName, AuthConfig auth)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine($"using {projectName}.Data;");
        sb.AppendLine($"using {projectName}.Messaging;");
        sb.AppendLine($"using {projectName}.Middleware;");
        sb.AppendLine($"using {projectName}.Services;");
        sb.AppendLine("using Microsoft.AspNetCore.Authentication.JwtBearer;");
        sb.AppendLine("using Microsoft.IdentityModel.Tokens;");
        sb.AppendLine("using System.Text;");
        sb.AppendLine();
        sb.AppendLine("var builder = WebApplication.CreateBuilder(args);");
        sb.AppendLine();
        sb.AppendLine("builder.Services.AddControllers();");
        sb.AppendLine("builder.Services.AddEndpointsApiExplorer();");
        sb.AppendLine("builder.Services.AddSwaggerGen(c =>");
        sb.AppendLine("{");
        sb.AppendLine("    c.SwaggerDoc(\"v1\", new Microsoft.OpenApi.Models.OpenApiInfo");
        sb.AppendLine("    {");
        sb.AppendLine("        Title = \"Auth Service API\",");
        sb.AppendLine("        Version = \"v1\",");
        sb.AppendLine("        Description = \"Centralized authentication microservice. Issues JWT tokens used by all other services.\\n\\n\" +");
        sb.AppendLine("                      \"**Endpoints:** POST /api/auth/register, POST /api/auth/login, GET /api/auth/me\"");
        sb.AppendLine("    });");
        sb.AppendLine("    c.AddSecurityDefinition(\"Bearer\", new Microsoft.OpenApi.Models.OpenApiSecurityScheme");
        sb.AppendLine("    {");
        sb.AppendLine("        In = Microsoft.OpenApi.Models.ParameterLocation.Header,");
        sb.AppendLine("        Description = \"JWT token from POST /api/auth/login. Enter: Bearer {token}\",");
        sb.AppendLine("        Name = \"Authorization\",");
        sb.AppendLine("        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,");
        sb.AppendLine("        BearerFormat = \"JWT\", Scheme = \"bearer\"");
        sb.AppendLine("    });");
        sb.AppendLine("    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement");
        sb.AppendLine("    {");
        sb.AppendLine("        {");
        sb.AppendLine("            new Microsoft.OpenApi.Models.OpenApiSecurityScheme {");
        sb.AppendLine("                Reference = new Microsoft.OpenApi.Models.OpenApiReference");
        sb.AppendLine("                    { Id = \"Bearer\", Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme }");
        sb.AppendLine("            },");
        sb.AppendLine("            Array.Empty<string>()");
        sb.AppendLine("        }");
        sb.AppendLine("    });");
        sb.AppendLine("});");
        sb.AppendLine();
        sb.AppendLine("// Database");
        sb.AppendLine("builder.Services.AddDbContext<ApplicationDbContext>(options =>");
        sb.AppendLine("    options.UseNpgsql(builder.Configuration.GetConnectionString(\"DefaultConnection\")));");
        sb.AppendLine();
        sb.AppendLine("// RabbitMQ (publishes user.registered / user.loggedin events)");
        sb.AppendLine("builder.Services.AddSingleton<IEventPublisher, RabbitMqPublisher>();");
        sb.AppendLine("builder.Services.AddHostedService<RabbitMqSubscriber>();");
        sb.AppendLine();
        sb.AppendLine("builder.Services.AddCors(o => o.AddDefaultPolicy(p =>");
        sb.AppendLine("    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));");
        sb.AppendLine();
        sb.AppendLine("// JWT — this service ISSUES tokens; others only validate them");
        sb.AppendLine("var jwtKey = builder.Configuration[\"Jwt:Key\"] ?? throw new InvalidOperationException(\"Jwt:Key missing\");");
        sb.AppendLine("builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)");
        sb.AppendLine("    .AddJwtBearer(o => o.TokenValidationParameters = new()");
        sb.AppendLine("    {");
        sb.AppendLine("        ValidateIssuerSigningKey = true,");
        sb.AppendLine("        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),");
        sb.AppendLine("        ValidateIssuer = false, ValidateAudience = false");
        sb.AppendLine("    });");
        sb.AppendLine("builder.Services.AddAuthorization();");
        sb.AppendLine("builder.Services.AddScoped<IAuthService, AuthService>();");
        sb.AppendLine("builder.Services.AddSingleton<TokenService>();");
        sb.AppendLine();
        sb.AppendLine("var app = builder.Build();");
        sb.AppendLine();
        sb.AppendLine("// Auto-apply migrations on startup");
        sb.AppendLine("using (var scope = app.Services.CreateScope())");
        sb.AppendLine("{");
        sb.AppendLine("    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();");
        sb.AppendLine("    db.Database.Migrate();");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("app.UseSwagger();");
        sb.AppendLine("app.UseSwaggerUI(c => { c.SwaggerEndpoint(\"/swagger/v1/swagger.json\", \"Auth Service v1\"); c.RoutePrefix = \"swagger\"; });");
        sb.AppendLine();
        sb.AppendLine("app.UseMiddleware<ErrorHandlerMiddleware>();");
        sb.AppendLine("app.UseCors();");
        sb.AppendLine("app.UseAuthentication();");
        sb.AppendLine("app.UseAuthorization();");
        sb.AppendLine("app.MapControllers();");
        sb.AppendLine($"app.MapGet(\"/health\", () => Results.Ok(new {{ service = \"{projectName}\", status = \"ok\" }}));");
        sb.AppendLine("app.Run();");
        return sb.ToString();
    }

    private string GenerateAuthMicroserviceSubscriber(string projectName) =>
$@"using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace {projectName}.Messaging;

/// <summary>
/// Auth service event subscriber.
/// Add BindingKeys to subscribe to events from other services (e.g. to react to entity deletions).
/// </summary>
public class RabbitMqSubscriber : BackgroundService
{{
    private const string Exchange = ""events"";
    private const string Queue = ""auth-service-queue"";

    // Add routing keys here to subscribe to events from other services
    private static readonly string[] BindingKeys = [];

    private readonly IConfiguration _config;
    private readonly ILogger<RabbitMqSubscriber> _logger;

    public RabbitMqSubscriber(IConfiguration config, ILogger<RabbitMqSubscriber> logger)
    {{ _config = config; _logger = logger; }}

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {{
        stoppingToken.Register(() => _logger.LogInformation(""Auth RabbitMQ subscriber stopping""));
        if (BindingKeys.Length > 0)
            Task.Run(() => StartListening(stoppingToken), stoppingToken);
        return Task.CompletedTask;
    }}

    private void StartListening(CancellationToken ct)
    {{
        try
        {{
            var factory = new ConnectionFactory
            {{
                Uri = new Uri(_config[""RabbitMQ:Url""] ?? ""amqp://guest:guest@rabbitmq:5672""),
                DispatchConsumersAsync = true
            }};
            using var conn = factory.CreateConnection();
            using var channel = conn.CreateModel();
            channel.ExchangeDeclare(Exchange, ExchangeType.Topic, durable: true);
            channel.QueueDeclare(Queue, durable: true, exclusive: false, autoDelete: false);
            foreach (var key in BindingKeys) channel.QueueBind(Queue, Exchange, key);

            _logger.LogInformation(""✅ Auth RabbitMQ subscriber listening on queue: {{Queue}}"", Queue);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += async (_, ea) =>
            {{
                var key = ea.RoutingKey;
                var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                _logger.LogInformation(""[Auth Subscriber] Received: {{Key}}"", key);
                HandleEvent(key, body);
                channel.BasicAck(ea.DeliveryTag, false);
                await Task.CompletedTask;
            }};
            channel.BasicConsume(Queue, false, consumer);
            while (!ct.IsCancellationRequested) Thread.Sleep(1000);
        }}
        catch (Exception ex)
        {{
            _logger.LogError(ex, ""Auth RabbitMQ subscriber error"");
        }}
    }}

    private void HandleEvent(string routingKey, string body)
    {{
        switch (routingKey)
        {{
            default:
                _logger.LogWarning(""[Auth] Unhandled event: {{Key}}"", routingKey);
                break;
        }}
    }}
}}
";

    // ─────────────────────────── HELPERS ───────────────────────────

    private string Pluralize(string name)
    {
        if (Plurals.TryGetValue(name, out var p)) return p;
        if (name.EndsWith("y", StringComparison.OrdinalIgnoreCase)) return name[..^1] + "ies";
        if (name.EndsWith("s") || name.EndsWith("x") || name.EndsWith("ch") || name.EndsWith("sh")) return name + "es";
        return name + "s";
    }

    private string MapDataTypeToCSharp(string dt) => dt switch
    {
        "Integer" => "int", "Long" => "long", "Float" => "float",
        "Decimal" => "decimal", "Boolean" => "bool", "DateTime" => "DateTime",
        "Guid" => "Guid", "Text" => "string", _ => "string"
    };

    private bool IsValueType(string dt) => dt is "Integer" or "Long" or "Float" or "Decimal" or "Boolean" or "DateTime" or "Guid";

    private string GetDefaultValue(string dt, bool isPk) =>
        isPk ? " = Guid.NewGuid();" : dt switch
        {
            "String" or "Text" => " = string.Empty;",
            "Boolean" => " = false;",
            "DateTime" => " = DateTime.UtcNow;",
            _ => ""
        };

    private string GetInitDefault(string dt) => dt switch
    {
        "String" or "Text" => "string.Empty",
        "Boolean" => "false",
        "DateTime" => "DateTime.UtcNow",
        _ => "default"
    };

    private string LowerFirst(string str) =>
        string.IsNullOrEmpty(str) ? str : char.ToLower(str[0]) + str[1..];

    private string SanitizeName(string name) =>
        string.IsNullOrWhiteSpace(name) ? "GeneratedProject"
            : new string(name.Where(c => char.IsLetterOrDigit(c)).ToArray())
                .TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
}

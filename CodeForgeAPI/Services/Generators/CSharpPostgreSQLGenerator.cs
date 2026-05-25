using System.Text;
using CodeForgeAPI.Models;

namespace CodeForgeAPI.Services.Generators;

public class CSharpPostgreSQLGenerator : ITemplateGenerator
{
    // Simple irregular plurals dictionary
    private static readonly Dictionary<string, string> Plurals = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Category", "Categories" }, { "Entity", "Entities" }, { "Property", "Properties" },
        { "Story", "Stories" }, { "City", "Cities" }, { "Country", "Countries" },
        { "Company", "Companies" }, { "Activity", "Activities" }, { "Library", "Libraries" },
        { "Query", "Queries" }, { "Policy", "Policies" }, { "Reply", "Replies" },
        { "Entry", "Entries" }, { "Gallery", "Galleries" }, { "Address", "Addresses" },
        { "Status", "Statuses" }, { "Bus", "Buses" }, { "Box", "Boxes" },
        { "Person", "People" }, { "Man", "Men" }, { "Woman", "Women" }, { "Child", "Children" },
        { "Leaf", "Leaves" }, { "Shelf", "Shelves" }, { "Half", "Halves" },
        { "Knife", "Knives" }, { "Life", "Lives" }, { "Wife", "Wives" },
    };

    public Dictionary<string, string> Generate(Project project)
    {
        var files = new Dictionary<string, string>();
        var projectName = SanitizeName(project.Name);

        // Models
        foreach (var entity in project.Entities)
            files[$"{projectName}/Models/{entity.Name}.cs"] = GenerateModel(entity, project, projectName);

        // DTOs
        foreach (var entity in project.Entities)
            files[$"{projectName}/DTOs/{entity.Name}Dto.cs"] = GenerateDto(entity, projectName);

        // Controllers
        foreach (var entity in project.Entities)
            files[$"{projectName}/Controllers/{entity.Name}Controller.cs"] = GenerateController(entity, project, projectName);

        // DbContext
        files[$"{projectName}/Data/ApplicationDbContext.cs"] = GenerateDbContext(project, projectName);

        // Middleware
        files[$"{projectName}/Middleware/ErrorHandlerMiddleware.cs"] = GenerateErrorMiddleware(projectName);

        // appsettings
        files[$"{projectName}/appsettings.json"] = GenerateAppSettings(projectName);
        files[$"{projectName}/appsettings.Development.json"] = GenerateAppSettingsDevelopment();

        // Program.cs
        files[$"{projectName}/Program.cs"] = GenerateProgram(project, projectName);

        // .csproj
        files[$"{projectName}/{projectName}.csproj"] = GenerateProjectFile();

        // Dockerfile
        files[$"{projectName}/Dockerfile"] = GenerateDockerfile(projectName);

        // docker-compose.yml
        files["docker-compose.yml"] = GenerateDockerCompose(projectName);

        // .gitignore
        files[".gitignore"] = GenerateGitignore();

        // README.md
        files["README.md"] = GenerateReadme(project, projectName);

        return files;
    }

    // ─────────────────────────── MODEL ───────────────────────────

    private string GenerateModel(Entity entity, Project project, string projectName)
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

        // Always ensure there's an Id / PK
        bool hasPk = entity.Fields.Any(f => f.IsPrimaryKey);
        if (!hasPk)
        {
            sb.AppendLine("    [Key]");
            sb.AppendLine("    public Guid Id { get; set; } = Guid.NewGuid();");
            sb.AppendLine();
        }

        // Scalar fields first
        foreach (var field in entity.Fields.Where(f => f.DataType != "Relationship").OrderBy(f => f.DisplayOrder))
        {
            if (field.IsPrimaryKey)
                sb.AppendLine("    [Key]");

            if (field.IsRequired && field.DataType == "String")
                sb.AppendLine("    [Required]");

            if (field.DataType == "String")
            {
                var maxLen = field.IsUnique ? 255 : 500;
                sb.AppendLine($"    [MaxLength({maxLen})]");
            }

            if (field.IsUnique)
                sb.AppendLine($"    // Unique constraint is configured in DbContext");

            var csType = MapDataTypeToCSharp(field.DataType);
            var nullable = (!field.IsRequired && IsValueType(field.DataType)) ? "?" : "";
            var defaultVal = GetDefaultValue(field.DataType, field.IsPrimaryKey);

            sb.AppendLine($"    public {csType}{nullable} {field.Name} {{ get; set; }}{defaultVal}");
            sb.AppendLine();
        }

        // Navigation properties from Relationship fields
        foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship").OrderBy(f => f.DisplayOrder))
        {
            if (!field.RelatedEntityId.HasValue) continue;
            var related = project.Entities.FirstOrDefault(e => e.Id == field.RelatedEntityId);
            if (related == null) continue;

            if (field.RelationshipType == "ManyToMany")
            {
                sb.AppendLine($"    public ICollection<{related.Name}> {Pluralize(related.Name)} {{ get; set; }} = new List<{related.Name}>();");
            }
            else if (field.RelationshipType == "OneToOne")
            {
                sb.AppendLine($"    public Guid? {related.Name}Id {{ get; set; }}");
                sb.AppendLine($"    [ForeignKey(nameof({related.Name}Id))]");
                sb.AppendLine($"    public {related.Name}? {related.Name} {{ get; set; }}");
            }
            else // OneToMany — this entity holds the FK
            {
                sb.AppendLine($"    public Guid? {related.Name}Id {{ get; set; }}");
                sb.AppendLine($"    [ForeignKey(nameof({related.Name}Id))]");
                sb.AppendLine($"    public {related.Name}? {related.Name} {{ get; set; }}");
            }
            sb.AppendLine();
        }

        // Reverse navigation: if other entities have OneToMany pointing TO this entity
        foreach (var otherEntity in project.Entities.Where(e => e.Id != entity.Id))
        {
            foreach (var field in otherEntity.Fields.Where(f => f.DataType == "Relationship" && f.RelatedEntityId == entity.Id && f.RelationshipType == "OneToMany"))
            {
                sb.AppendLine($"    // Reverse navigation for {otherEntity.Name}.{field.Name} -> {entity.Name}");
                sb.AppendLine($"    public ICollection<{otherEntity.Name}> {Pluralize(otherEntity.Name)} {{ get; set; }} = new List<{otherEntity.Name}>();");
                sb.AppendLine();
            }
        }

        // Timestamps
        sb.AppendLine("    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;");
        sb.AppendLine("    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;");

        sb.AppendLine("}");
        return sb.ToString();
    }

    // ─────────────────────────── DTO ───────────────────────────

    private string GenerateDto(Entity entity, string projectName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.ComponentModel.DataAnnotations;");
        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.DTOs;");
        sb.AppendLine();

        // Response DTO
        sb.AppendLine($"public class {entity.Name}Response");
        sb.AppendLine("{");
        sb.AppendLine("    public Guid Id { get; set; }");
        foreach (var field in entity.Fields.Where(f => f.DataType != "Relationship").OrderBy(f => f.DisplayOrder))
        {
            var csType = MapDataTypeToCSharp(field.DataType);
            var nullable = (!field.IsRequired && IsValueType(field.DataType)) ? "?" : "";
            sb.AppendLine($"    public {csType}{nullable} {field.Name} {{ get; set; }}");
        }
        foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship").OrderBy(f => f.DisplayOrder))
            sb.AppendLine($"    public Guid? {field.Name}Id {{ get; set; }}");
        sb.AppendLine("    public DateTime CreatedAt { get; set; }");
        sb.AppendLine("    public DateTime UpdatedAt { get; set; }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Create Request DTO
        sb.AppendLine($"public class Create{entity.Name}Request");
        sb.AppendLine("{");
        foreach (var field in entity.Fields.Where(f => f.DataType != "Relationship" && !f.IsPrimaryKey).OrderBy(f => f.DisplayOrder))
        {
            var csType = MapDataTypeToCSharp(field.DataType);
            var nullable = (!field.IsRequired && IsValueType(field.DataType)) ? "?" : "";
            if (field.IsRequired && field.DataType == "String")
                sb.AppendLine("    [Required]");
            if (field.DataType == "String")
                sb.AppendLine("    [MaxLength(500)]");
            sb.AppendLine($"    public {csType}{nullable} {field.Name} {{ get; set; }} = {GetInitDefault(field.DataType)};");
        }
        foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship").OrderBy(f => f.DisplayOrder))
        {
            if (field.IsRequired)
                sb.AppendLine("    [Required]");
            sb.AppendLine($"    public Guid? {field.Name}Id {{ get; set; }}");
        }
        sb.AppendLine("}");
        sb.AppendLine();

        // Update Request DTO
        sb.AppendLine($"public class Update{entity.Name}Request");
        sb.AppendLine("{");
        foreach (var field in entity.Fields.Where(f => f.DataType != "Relationship" && !f.IsPrimaryKey).OrderBy(f => f.DisplayOrder))
        {
            var csType = MapDataTypeToCSharp(field.DataType);
            var nullable = IsValueType(field.DataType) ? "?" : "";
            if (field.DataType == "String")
                sb.AppendLine("    [MaxLength(500)]");
            sb.AppendLine($"    public {csType}{nullable} {field.Name} {{ get; set; }}");
        }
        foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship").OrderBy(f => f.DisplayOrder))
            sb.AppendLine($"    public Guid? {field.Name}Id {{ get; set; }}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    // ─────────────────────────── CONTROLLER ───────────────────────────

    private string GenerateController(Entity entity, Project project, string projectName)
    {
        var sb = new StringBuilder();
        var name = entity.Name;
        var nameLower = LowerFirst(name);
        var namePlural = Pluralize(name);
        var namePluralLower = LowerFirst(namePlural);

        // Collect navigation includes
        var includes = new List<string>();
        foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship"))
        {
            if (!field.RelatedEntityId.HasValue) continue;
            var related = project.Entities.FirstOrDefault(e => e.Id == field.RelatedEntityId);
            if (related != null)
                includes.Add(related.Name);
        }
        // reverse collection includes
        foreach (var otherEntity in project.Entities.Where(e => e.Id != entity.Id))
        {
            foreach (var field in otherEntity.Fields.Where(f => f.DataType == "Relationship" && f.RelatedEntityId == entity.Id && f.RelationshipType == "OneToMany"))
                includes.Add(Pluralize(otherEntity.Name));
        }

        sb.AppendLine("using Microsoft.AspNetCore.Mvc;");
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine($"using {projectName}.Data;");
        sb.AppendLine($"using {projectName}.DTOs;");
        sb.AppendLine($"using {projectName}.Models;");
        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.Controllers;");
        sb.AppendLine();
        sb.AppendLine($"[ApiController]");
        sb.AppendLine($"[Route(\"api/[controller]\")]");
        sb.AppendLine($"public class {name}Controller : ControllerBase");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly ApplicationDbContext _context;");
        sb.AppendLine($"    private readonly ILogger<{name}Controller> _logger;");
        sb.AppendLine();
        sb.AppendLine($"    public {name}Controller(ApplicationDbContext context, ILogger<{name}Controller> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        _context = context;");
        sb.AppendLine("        _logger = logger;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // GET all
        sb.AppendLine("    /// <summary>Get all " + namePluralLower + " with optional pagination</summary>");
        sb.AppendLine("    [HttpGet]");
        sb.AppendLine($"    public async Task<ActionResult<IEnumerable<{name}Response>>> GetAll(");
        sb.AppendLine("        [FromQuery] int page = 1,");
        sb.AppendLine("        [FromQuery] int pageSize = 20)");
        sb.AppendLine("    {");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.Append($"            var query = _context.{namePlural}");
        foreach (var inc in includes)
            sb.Append($"\n                .Include(x => x.{inc})");
        sb.AppendLine(";");
        sb.AppendLine();
        sb.AppendLine("            var total = await query.CountAsync();");
        sb.AppendLine("            var items = await query");
        sb.AppendLine("                .OrderByDescending(x => x.CreatedAt)");
        sb.AppendLine("                .Skip((page - 1) * pageSize)");
        sb.AppendLine("                .Take(pageSize)");
        sb.AppendLine($"                .Select(x => MapToResponse(x))");
        sb.AppendLine("                .ToListAsync();");
        sb.AppendLine();
        sb.AppendLine("            Response.Headers.Append(\"X-Total-Count\", total.ToString());");
        sb.AppendLine("            Response.Headers.Append(\"X-Page\", page.ToString());");
        sb.AppendLine("            Response.Headers.Append(\"X-Page-Size\", pageSize.ToString());");
        sb.AppendLine("            return Ok(items);");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine($"            _logger.LogError(ex, \"Error getting {namePluralLower}\");");
        sb.AppendLine("            throw;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // GET by id
        sb.AppendLine("    /// <summary>Get " + nameLower + " by ID</summary>");
        sb.AppendLine("    [HttpGet(\"{id:guid}\")]");
        sb.AppendLine($"    public async Task<ActionResult<{name}Response>> GetById(Guid id)");
        sb.AppendLine("    {");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.Append($"            var {nameLower} = await _context.{namePlural}");
        foreach (var inc in includes)
            sb.Append($"\n                .Include(x => x.{inc})");
        sb.AppendLine();
        sb.AppendLine($"                .FirstOrDefaultAsync(x => x.Id == id);");
        sb.AppendLine();
        sb.AppendLine($"            if ({nameLower} == null)");
        sb.AppendLine($"                return NotFound(new {{ message = \"{name} not found\" }});");
        sb.AppendLine();
        sb.AppendLine($"            return Ok(MapToResponse({nameLower}));");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine($"            _logger.LogError(ex, \"Error getting {nameLower} {{Id}}\", id);");
        sb.AppendLine("            throw;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // POST
        sb.AppendLine("    /// <summary>Create a new " + nameLower + "</summary>");
        sb.AppendLine("    [HttpPost]");
        sb.AppendLine($"    public async Task<ActionResult<{name}Response>> Create([FromBody] Create{name}Request request)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (!ModelState.IsValid)");
        sb.AppendLine("            return BadRequest(ModelState);");
        sb.AppendLine();
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine($"            var {nameLower} = new {name}");
        sb.AppendLine("            {");
        sb.AppendLine("                Id = Guid.NewGuid(),");
        foreach (var field in entity.Fields.Where(f => f.DataType != "Relationship" && !f.IsPrimaryKey).OrderBy(f => f.DisplayOrder))
            sb.AppendLine($"                {field.Name} = request.{field.Name},");
        foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship").OrderBy(f => f.DisplayOrder))
            sb.AppendLine($"                {field.Name}Id = request.{field.Name}Id,");
        sb.AppendLine("                CreatedAt = DateTime.UtcNow,");
        sb.AppendLine("                UpdatedAt = DateTime.UtcNow");
        sb.AppendLine("            };");
        sb.AppendLine();
        sb.AppendLine($"            _context.{namePlural}.Add({nameLower});");
        sb.AppendLine("            await _context.SaveChangesAsync();");
        sb.AppendLine();
        sb.AppendLine($"            return CreatedAtAction(nameof(GetById), new {{ id = {nameLower}.Id }}, MapToResponse({nameLower}));");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains(\"unique\") == true || ex.InnerException?.Message.Contains(\"duplicate\") == true)");
        sb.AppendLine("        {");
        sb.AppendLine($"            return Conflict(new {{ message = \"{name} with these values already exists\" }});");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine($"            _logger.LogError(ex, \"Error creating {nameLower}\");");
        sb.AppendLine("            throw;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // PUT
        sb.AppendLine("    /// <summary>Update " + nameLower + "</summary>");
        sb.AppendLine("    [HttpPut(\"{id:guid}\")]");
        sb.AppendLine($"    public async Task<ActionResult<{name}Response>> Update(Guid id, [FromBody] Update{name}Request request)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (!ModelState.IsValid)");
        sb.AppendLine("            return BadRequest(ModelState);");
        sb.AppendLine();
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine($"            var {nameLower} = await _context.{namePlural}.FindAsync(id);");
        sb.AppendLine($"            if ({nameLower} == null)");
        sb.AppendLine($"                return NotFound(new {{ message = \"{name} not found\" }});");
        sb.AppendLine();
        foreach (var field in entity.Fields.Where(f => f.DataType != "Relationship" && !f.IsPrimaryKey).OrderBy(f => f.DisplayOrder))
            sb.AppendLine($"            if (request.{field.Name} != null) {nameLower}.{field.Name} = request.{field.Name}{(IsValueType(field.DataType) ? ".Value" : "")};");
        foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship").OrderBy(f => f.DisplayOrder))
            sb.AppendLine($"            if (request.{field.Name}Id.HasValue) {nameLower}.{field.Name}Id = request.{field.Name}Id;");
        sb.AppendLine($"            {nameLower}.UpdatedAt = DateTime.UtcNow;");
        sb.AppendLine();
        sb.AppendLine("            await _context.SaveChangesAsync();");
        sb.AppendLine($"            return Ok(MapToResponse({nameLower}));");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains(\"unique\") == true || ex.InnerException?.Message.Contains(\"duplicate\") == true)");
        sb.AppendLine("        {");
        sb.AppendLine($"            return Conflict(new {{ message = \"{name} with these values already exists\" }});");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine($"            _logger.LogError(ex, \"Error updating {nameLower} {{Id}}\", id);");
        sb.AppendLine("            throw;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // DELETE
        sb.AppendLine("    /// <summary>Delete " + nameLower + "</summary>");
        sb.AppendLine("    [HttpDelete(\"{id:guid}\")]");
        sb.AppendLine($"    public async Task<IActionResult> Delete(Guid id)");
        sb.AppendLine("    {");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine($"            var {nameLower} = await _context.{namePlural}.FindAsync(id);");
        sb.AppendLine($"            if ({nameLower} == null)");
        sb.AppendLine($"                return NotFound(new {{ message = \"{name} not found\" }});");
        sb.AppendLine();
        sb.AppendLine($"            _context.{namePlural}.Remove({nameLower});");
        sb.AppendLine("            await _context.SaveChangesAsync();");
        sb.AppendLine("            return NoContent();");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (DbUpdateException)");
        sb.AppendLine("        {");
        sb.AppendLine($"            return Conflict(new {{ message = \"Cannot delete {nameLower}: it is referenced by other records\" }});");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine($"            _logger.LogError(ex, \"Error deleting {nameLower} {{Id}}\", id);");
        sb.AppendLine("            throw;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // MapToResponse private method
        sb.AppendLine($"    private static {name}Response MapToResponse({name} x) => new()");
        sb.AppendLine("    {");
        sb.AppendLine("        Id = x.Id,");
        foreach (var field in entity.Fields.Where(f => f.DataType != "Relationship" && !f.IsPrimaryKey).OrderBy(f => f.DisplayOrder))
            sb.AppendLine($"        {field.Name} = x.{field.Name},");
        foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship").OrderBy(f => f.DisplayOrder))
            sb.AppendLine($"        {field.Name}Id = x.{field.Name}Id,");
        sb.AppendLine("        CreatedAt = x.CreatedAt,");
        sb.AppendLine("        UpdatedAt = x.UpdatedAt");
        sb.AppendLine("    };");
        sb.AppendLine("}");

        return sb.ToString();
    }

    // ─────────────────────────── DB CONTEXT ───────────────────────────

    private string GenerateDbContext(Project project, string projectName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine($"using {projectName}.Models;");
        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.Data;");
        sb.AppendLine();
        sb.AppendLine("public class ApplicationDbContext : DbContext");
        sb.AppendLine("{");
        sb.AppendLine("    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)");
        sb.AppendLine("        : base(options) { }");
        sb.AppendLine();

        foreach (var entity in project.Entities)
            sb.AppendLine($"    public DbSet<{entity.Name}> {Pluralize(entity.Name)} {{ get; set; }}");

        sb.AppendLine();
        sb.AppendLine("    protected override void OnModelCreating(ModelBuilder modelBuilder)");
        sb.AppendLine("    {");
        sb.AppendLine("        base.OnModelCreating(modelBuilder);");
        sb.AppendLine();

        foreach (var entity in project.Entities)
        {
            var hasConfig = false;

            // Unique indexes
            foreach (var field in entity.Fields.Where(f => f.IsUnique && f.DataType != "Relationship"))
            {
                if (!hasConfig)
                {
                    sb.AppendLine($"        // {entity.Name} configuration");
                    sb.AppendLine($"        modelBuilder.Entity<{entity.Name}>(e =>");
                    sb.AppendLine("        {");
                    hasConfig = true;
                }
                sb.AppendLine($"            e.HasIndex(x => x.{field.Name}).IsUnique();");
            }

            // Relationships
            foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship"))
            {
                if (!field.RelatedEntityId.HasValue) continue;
                var related = project.Entities.FirstOrDefault(e => e.Id == field.RelatedEntityId);
                if (related == null) continue;

                if (!hasConfig)
                {
                    sb.AppendLine($"        // {entity.Name} configuration");
                    sb.AppendLine($"        modelBuilder.Entity<{entity.Name}>(e =>");
                    sb.AppendLine("        {");
                    hasConfig = true;
                }

                if (field.RelationshipType == "ManyToMany")
                {
                    sb.AppendLine($"            e.HasMany(x => x.{Pluralize(related.Name)})");
                    sb.AppendLine($"             .WithMany(x => x.{Pluralize(entity.Name)});");
                }
                else if (field.RelationshipType == "OneToOne")
                {
                    sb.AppendLine($"            e.HasOne(x => x.{related.Name})");
                    sb.AppendLine($"             .WithOne()");
                    sb.AppendLine($"             .HasForeignKey<{entity.Name}>(x => x.{related.Name}Id)");
                    sb.AppendLine($"             .OnDelete(DeleteBehavior.SetNull);");
                }
                else // OneToMany
                {
                    sb.AppendLine($"            e.HasOne(x => x.{related.Name})");
                    sb.AppendLine($"             .WithMany(x => x.{Pluralize(entity.Name)})");
                    sb.AppendLine($"             .HasForeignKey(x => x.{related.Name}Id)");
                    sb.AppendLine($"             .OnDelete(DeleteBehavior.SetNull);");
                }
            }

            if (hasConfig)
            {
                sb.AppendLine("        });");
                sb.AppendLine();
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine();

        // SaveChanges override to auto-update UpdatedAt
        sb.AppendLine("    public override int SaveChanges()");
        sb.AppendLine("    {");
        sb.AppendLine("        UpdateTimestamps();");
        sb.AppendLine("        return base.SaveChanges();");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        UpdateTimestamps();");
        sb.AppendLine("        return base.SaveChangesAsync(cancellationToken);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private void UpdateTimestamps()");
        sb.AppendLine("    {");
        sb.AppendLine("        var entries = ChangeTracker.Entries()");
        sb.AppendLine("            .Where(e => e.State == EntityState.Modified);");
        sb.AppendLine("        foreach (var entry in entries)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (entry.Properties.Any(p => p.Metadata.Name == \"UpdatedAt\"))");
        sb.AppendLine("                entry.Property(\"UpdatedAt\").CurrentValue = DateTime.UtcNow;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    // ─────────────────────────── MIDDLEWARE ───────────────────────────

    private string GenerateErrorMiddleware(string projectName)
    {
        return $@"using System.Net;
using System.Text.Json;

namespace {projectName}.Middleware;

public class ErrorHandlerMiddleware
{{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlerMiddleware> _logger;

    public ErrorHandlerMiddleware(RequestDelegate next, ILogger<ErrorHandlerMiddleware> logger)
    {{
        _next = next;
        _logger = logger;
    }}

    public async Task Invoke(HttpContext context)
    {{
        try
        {{
            await _next(context);
        }}
        catch (Exception ex)
        {{
            _logger.LogError(ex, ""Unhandled exception"");
            await HandleExceptionAsync(context, ex);
        }}
    }}

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {{
        var code = HttpStatusCode.InternalServerError;
        var message = ""An unexpected error occurred"";

        if (exception is ArgumentException) {{ code = HttpStatusCode.BadRequest; message = exception.Message; }}
        else if (exception is KeyNotFoundException) {{ code = HttpStatusCode.NotFound; message = exception.Message; }}
        else if (exception is InvalidOperationException) {{ code = HttpStatusCode.Conflict; message = exception.Message; }}

        var result = JsonSerializer.Serialize(new {{ message }});
        context.Response.ContentType = ""application/json"";
        context.Response.StatusCode = (int)code;
        return context.Response.WriteAsync(result);
    }}
}}
";
    }

    // ─────────────────────────── PROGRAM.CS ───────────────────────────

    private string GenerateProgram(Project project, string projectName)
    {
        return $@"using Microsoft.EntityFrameworkCore;
using {projectName}.Data;
using {projectName}.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ─── Services ───────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {{
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    }});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{{
    c.SwaggerDoc(""v1"", new() {{ Title = ""{projectName} API"", Version = ""v1"" }});
}});

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString(""DefaultConnection""),
        o => o.EnableRetryOnFailure(3)
    ));

// CORS
builder.Services.AddCors(options =>
{{
    options.AddPolicy(""AllowAll"", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
}});

// Health checks
builder.Services.AddHealthChecks()
    .AddNpgsql(builder.Configuration.GetConnectionString(""DefaultConnection"")!);

var app = builder.Build();

// ─── Auto-migrate on startup (dev only) ─────────────────────────────────────
if (app.Environment.IsDevelopment())
{{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}}

// ─── Middleware pipeline ─────────────────────────────────────────────────────
app.UseMiddleware<ErrorHandlerMiddleware>();

if (app.Environment.IsDevelopment())
{{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint(""/swagger/v1/swagger.json"", ""{projectName} v1""));
}}

app.UseHttpsRedirection();
app.UseCors(""AllowAll"");
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks(""/health"");

app.Run();
";
    }

    // ─────────────────────────── PROJECT FILE ───────────────────────────

    private string GenerateProjectFile()
    {
        return @"<Project Sdk=""Microsoft.NET.Sdk.Web"">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>$(MSBuildProjectName.Replace(""-"", ""_""))</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""Microsoft.EntityFrameworkCore"" Version=""8.0.8"" />
    <PackageReference Include=""Microsoft.EntityFrameworkCore.Design"" Version=""8.0.8"">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include=""Npgsql.EntityFrameworkCore.PostgreSQL"" Version=""8.0.8"" />
    <PackageReference Include=""AspNetCore.HealthChecks.NpgSql"" Version=""8.0.1"" />
    <PackageReference Include=""Swashbuckle.AspNetCore"" Version=""6.7.3"" />
  </ItemGroup>

</Project>
";
    }

    // ─────────────────────────── APPSETTINGS ───────────────────────────

    private string GenerateAppSettings(string projectName)
    {
        return $@"{{
  ""ConnectionStrings"": {{
    ""DefaultConnection"": ""Host=localhost;Port=5432;Database={projectName.ToLower()}db;Username=postgres;Password=yourpassword""
  }},
  ""Logging"": {{
    ""LogLevel"": {{
      ""Default"": ""Information"",
      ""Microsoft.AspNetCore"": ""Warning"",
      ""Microsoft.EntityFrameworkCore.Database.Command"": ""Warning""
    }}
  }},
  ""AllowedHosts"": ""*""
}}";
    }

    private string GenerateAppSettingsDevelopment()
    {
        return @"{
  ""Logging"": {
    ""LogLevel"": {
      ""Default"": ""Information"",
      ""Microsoft.AspNetCore"": ""Warning"",
      ""Microsoft.EntityFrameworkCore.Database.Command"": ""Information""
    }
  }
}";
    }

    // ─────────────────────────── DOCKER ───────────────────────────

    private string GenerateDockerfile(string projectName)
    {
        return $@"FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY [""{projectName}.csproj"", ""./""]
RUN dotnet restore ""{projectName}.csproj""
COPY . .
RUN dotnet build ""{projectName}.csproj"" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish ""{projectName}.csproj"" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT [""dotnet"", ""{projectName}.dll""]
";
    }

    private string GenerateDockerCompose(string projectName)
    {
        var dbName = projectName.ToLower() + "db";
        return $@"version: '3.8'

services:
  postgres:
    image: postgres:16-alpine
    restart: unless-stopped
    environment:
      POSTGRES_DB: {dbName}
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: yourpassword
    ports:
      - ""5432:5432""
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: [""CMD-SHELL"", ""pg_isready -U postgres""]
      interval: 10s
      timeout: 5s
      retries: 5

  api:
    build:
      context: ./{projectName}
      dockerfile: Dockerfile
    restart: unless-stopped
    ports:
      - ""5000:80""
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database={dbName};Username=postgres;Password=yourpassword
    depends_on:
      postgres:
        condition: service_healthy

volumes:
  postgres_data:
";
    }

    // ─────────────────────────── GITIGNORE ───────────────────────────

    private string GenerateGitignore()
    {
        return @"## .NET
*.user
*.suo
.vs/
bin/
obj/
*.swp
*.swo

## Build
[Bb]in/
[Oo]bj/
[Ll]og/
[Ll]ogs/

## NuGet
*.nupkg
*.snupkg
packages/
.nuget/

## Rider/ReSharper
.idea/
*.sln.iml

## VS Code
.vscode/
!.vscode/settings.json
!.vscode/tasks.json
!.vscode/launch.json
!.vscode/extensions.json

## Secrets (NEVER commit these)
appsettings.Production.json
appsettings.Local.json
*.pfx
*.p12
";
    }

    // ─────────────────────────── README ───────────────────────────

    private string GenerateReadme(Project project, string projectName)
    {
        var entityList = string.Join("\n", project.Entities.Select(e =>
            $"- **{e.Name}** — {e.Fields.Count} field(s): {string.Join(", ", e.Fields.Select(f => f.Name))}"));

        return $@"# {projectName}

> Generated with **CodeForge** — ASP.NET Core 8 + PostgreSQL Backend

## Tech Stack

- **Framework**: ASP.NET Core 8.0 (Minimal API style + Controllers)
- **ORM**: Entity Framework Core 8 with Npgsql
- **Database**: PostgreSQL 16
- **Docs**: Swagger / OpenAPI
- **Containerization**: Docker + Docker Compose

## Generated Entities

{entityList}

## Quick Start

### 🐳 Docker (recommended)

```bash
docker-compose up --build
```

API: `http://localhost:5000`  
Swagger: `http://localhost:5000/swagger`  
Health: `http://localhost:5000/health`

### 💻 Local Development

**Prerequisites**: .NET 8 SDK, PostgreSQL 16

1. Update connection string in `{projectName}/appsettings.json`

2. Install EF tools (once):
```bash
dotnet tool install --global dotnet-ef
```

3. Apply migrations:
```bash
cd {projectName}
dotnet ef migrations add InitialCreate
dotnet ef database update
```

4. Run:
```bash
dotnet run
```

## API Endpoints

Each entity exposes full CRUD with pagination:

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/{{entity}}?page=1&pageSize=20` | List (paginated) |
| GET | `/api/{{entity}}/{{id}}` | Get by ID |
| POST | `/api/{{entity}}` | Create |
| PUT | `/api/{{entity}}/{{id}}` | Update |
| DELETE | `/api/{{entity}}/{{id}}` | Delete |

Pagination headers returned: `X-Total-Count`, `X-Page`, `X-Page-Size`

## Notes

- All responses use **camelCase** JSON
- Auto-migration runs on startup in **Development** mode
- Duplicate/unique constraint violations return `409 Conflict`
- Foreign key violations on delete return `409 Conflict`
";
    }

    // ─────────────────────────── HELPERS ───────────────────────────

    private string MapDataTypeToCSharp(string dataType) => dataType switch
    {
        "String" => "string",
        "Integer" => "int",
        "Boolean" => "bool",
        "DateTime" => "DateTime",
        "Decimal" => "decimal",
        "Float" => "float",
        "Long" => "long",
        "Text" => "string",
        "Guid" => "Guid",
        _ => "string"
    };

    private bool IsValueType(string dataType) =>
        dataType is "Integer" or "Boolean" or "DateTime" or "Decimal" or "Float" or "Long" or "Guid";

    private string GetDefaultValue(string dataType, bool isPk) => dataType switch
    {
        "Guid" when isPk => " = Guid.NewGuid();",
        "String" => " = string.Empty;",
        "Text" => " = string.Empty;",
        "Boolean" => " = false;",
        _ => ";"
    };

    private string GetInitDefault(string dataType) => dataType switch
    {
        "String" => "string.Empty",
        "Text" => "string.Empty",
        "Boolean" => "false",
        "Integer" => "0",
        "Decimal" => "0m",
        "Float" => "0f",
        "Long" => "0L",
        _ => "default!"
    };

    private string Pluralize(string name)
    {
        if (Plurals.TryGetValue(name, out var plural)) return plural;
        if (name.EndsWith("y", StringComparison.OrdinalIgnoreCase))
            return name[..^1] + "ies";
        if (name.EndsWith("s") || name.EndsWith("x") || name.EndsWith("z") ||
            name.EndsWith("ch") || name.EndsWith("sh"))
            return name + "es";
        return name + "s";
    }

    private string LowerFirst(string str) =>
        string.IsNullOrEmpty(str) ? str : char.ToLower(str[0]) + str[1..];

    private string SanitizeName(string name) =>
        string.IsNullOrWhiteSpace(name) ? "GeneratedProject" :
        new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray())
            .TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
}

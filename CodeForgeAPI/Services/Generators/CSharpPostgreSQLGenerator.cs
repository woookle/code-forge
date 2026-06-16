using System.Text;
using System.Text.Json;
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

        // Parse auth config
        AuthConfig? auth = null;
        if (!string.IsNullOrEmpty(project.AuthConfig))
            auth = JsonSerializer.Deserialize<AuthConfig>(project.AuthConfig, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        bool authEnabled = auth?.Enabled == true;

        // Models
        foreach (var entity in project.Entities)
            files[$"{projectName}/Models/{entity.Name}.cs"] = GenerateModel(entity, project, projectName);

        // DTOs
        foreach (var entity in project.Entities)
            files[$"{projectName}/DTOs/{entity.Name}Dto.cs"] = GenerateDto(entity, projectName);

        // Controllers
        foreach (var entity in project.Entities)
            files[$"{projectName}/Controllers/{entity.Name}Controller.cs"] = GenerateController(entity, project, projectName, auth);

        // DbContext
        files[$"{projectName}/Data/ApplicationDbContext.cs"] = GenerateDbContext(project, projectName, authEnabled);

        // Middleware
        files[$"{projectName}/Middleware/ErrorHandlerMiddleware.cs"] = GenerateErrorMiddleware(projectName);

        // Global Usings
        files[$"{projectName}/GlobalUsings.cs"] = GenerateGlobalUsings(projectName);

        // appsettings
        files[$"{projectName}/appsettings.json"] = GenerateAppSettings(projectName, auth);
        files[$"{projectName}/appsettings.Development.json"] = GenerateAppSettingsDevelopment();

        // Program.cs
        files[$"{projectName}/Program.cs"] = GenerateProgram(project, projectName, auth);

        // .csproj
        files[$"{projectName}/{projectName}.csproj"] = GenerateProjectFile(authEnabled);

        // launchSettings
        files[$"{projectName}/Properties/launchSettings.json"] = GenerateLaunchSettings(projectName);

        // Dockerfile
        files[$"{projectName}/Dockerfile"] = GenerateDockerfile(projectName);

        // docker-compose.yml
        files[$"{projectName}/docker-compose.yml"] = GenerateDockerCompose(projectName);

        // .gitignore
        files[$"{projectName}/.gitignore"] = GenerateGitignore();

        // README.md
        files[$"{projectName}/README.md"] = GenerateReadme(project, projectName);

        // ── Auth module ──────────────────────────────────────────────────────────
        if (authEnabled)
        {
            files[$"{projectName}/Models/User.cs"]              = GenerateAuthUserModel(projectName, auth!);
            files[$"{projectName}/DTOs/AuthDtos.cs"]            = GenerateAuthDtos(projectName, auth!);
            files[$"{projectName}/Services/IAuthService.cs"]    = GenerateIAuthService(projectName, auth!);
            files[$"{projectName}/Services/TokenService.cs"]    = GenerateTokenService(projectName, auth!);
            files[$"{projectName}/Services/AuthService.cs"]     = GenerateAuthService(projectName, auth!);
            files[$"{projectName}/Controllers/AuthController.cs"] = GenerateAuthController(projectName, auth!);
        }

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

            if (field.IsRequired && field.DataType is "String" or "Text")
                sb.AppendLine("    [Required]");

            if (field.DataType is "String" or "Text")
            {
                var maxLen = field.DataType == "Text" ? 5000 : (field.IsUnique ? 255 : 500);
                sb.AppendLine($"    [MaxLength({maxLen})]");
            }

            if (field.IsUnique)
                sb.AppendLine("    // Unique constraint configured in DbContext → OnModelCreating");

            var csType = MapDataTypeToCSharp(field.DataType);
            var nullable = (!field.IsRequired && IsValueType(field.DataType)) ? "?" : "";
            var defaultVal = GetDefaultValue(field.DataType, field.IsPrimaryKey);

            sb.AppendLine($"    public {csType}{nullable} {field.Name} {{ get; set; }}{defaultVal}");
            sb.AppendLine();
        }

        // Navigation properties from Relationship fields
        var addedManyToMany = new HashSet<string>(); // track to avoid duplicate M2M collections
        foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship").OrderBy(f => f.DisplayOrder))
        {
            if (!field.RelatedEntityId.HasValue) continue;
            var related = project.Entities.FirstOrDefault(e => e.Id == field.RelatedEntityId);
            if (related == null) continue;

            if (field.RelationshipType == "ManyToMany")
            {
                var collectionName = Pluralize(related.Name);
                if (!addedManyToMany.Contains(collectionName))
                {
                    sb.AppendLine($"    public ICollection<{related.Name}> {collectionName} {{ get; set; }} = new List<{related.Name}>();");
                    sb.AppendLine();
                    addedManyToMany.Add(collectionName);
                }
            }
            else if (field.RelationshipType == "OneToOne")
            {
                var fkProp = FkPropName(field.Name);
                var navProp = NavPropName(field.Name);
                sb.AppendLine($"    public Guid? {fkProp} {{ get; set; }}");
                sb.AppendLine($"    [ForeignKey(nameof({fkProp}))]");
                sb.AppendLine($"    public {related.Name}? {navProp} {{ get; set; }}");
                sb.AppendLine();
            }
            else // OneToMany — this entity holds the FK
            {
                var fkProp = FkPropName(field.Name);
                var navProp = NavPropName(field.Name);
                sb.AppendLine($"    public Guid? {fkProp} {{ get; set; }}");
                sb.AppendLine($"    [ForeignKey(nameof({fkProp}))]");
                sb.AppendLine($"    public {related.Name}? {navProp} {{ get; set; }}");
                sb.AppendLine();
            }
        }

        // Reverse navigation: if other entities have OneToMany pointing TO this entity
        foreach (var otherEntity in project.Entities.Where(e => e.Id != entity.Id))
        {
            foreach (var field in otherEntity.Fields.Where(f =>
                f.DataType == "Relationship" &&
                f.RelatedEntityId == entity.Id &&
                f.RelationshipType == "OneToMany"))
            {
                sb.AppendLine($"    // Reverse navigation for {otherEntity.Name}.{field.Name} → {entity.Name}");
                sb.AppendLine($"    public ICollection<{otherEntity.Name}> {Pluralize(otherEntity.Name)} {{ get; set; }} = new List<{otherEntity.Name}>();");
                sb.AppendLine();
            }

            // Reverse navigation for ManyToMany (must exist on both sides)
            foreach (var field in otherEntity.Fields.Where(f =>
                f.DataType == "Relationship" &&
                f.RelatedEntityId == entity.Id &&
                f.RelationshipType == "ManyToMany"))
            {
                var collectionName = Pluralize(otherEntity.Name);
                if (!addedManyToMany.Contains(collectionName))
                {
                    sb.AppendLine($"    // Reverse navigation for ManyToMany with {otherEntity.Name}");
                    sb.AppendLine($"    public ICollection<{otherEntity.Name}> {collectionName} {{ get; set; }} = new List<{otherEntity.Name}>();");
                    sb.AppendLine();
                    addedManyToMany.Add(collectionName);
                }
            }
        }

        // Timestamps
        sb.AppendLine("    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;");
        sb.AppendLine("    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;");
        sb.AppendLine();

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
        foreach (var field in entity.Fields.Where(f => f.DataType != "Relationship" && !f.IsPrimaryKey).OrderBy(f => f.DisplayOrder))
        {
            var csType = MapDataTypeToCSharp(field.DataType);
            var nullable = (!field.IsRequired && IsValueType(field.DataType)) ? "?" : "";
            sb.AppendLine($"    public {csType}{nullable} {field.Name} {{ get; set; }}");
        }
        foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship" && f.RelationshipType != "ManyToMany").OrderBy(f => f.DisplayOrder))
            sb.AppendLine($"    public Guid? {FkPropName(field.Name)} {{ get; set; }}");
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
            if (field.IsRequired && field.DataType is "String" or "Text")
                sb.AppendLine("    [Required]");
            if (field.DataType is "String" or "Text")
                sb.AppendLine("    [MaxLength(500)]");
            sb.AppendLine($"    public {csType}{nullable} {field.Name} {{ get; set; }} = {GetInitDefault(field.DataType)};");
        }
        foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship" && f.RelationshipType != "ManyToMany").OrderBy(f => f.DisplayOrder))
        {
            if (field.IsRequired)
                sb.AppendLine("    [Required]");
            sb.AppendLine($"    public Guid? {FkPropName(field.Name)} {{ get; set; }}");
        }
        sb.AppendLine("}");
        sb.AppendLine();

        // Update Request DTO (full replace — all required)
        sb.AppendLine($"public class Update{entity.Name}Request");
        sb.AppendLine("{");
        foreach (var field in entity.Fields.Where(f => f.DataType != "Relationship" && !f.IsPrimaryKey).OrderBy(f => f.DisplayOrder))
        {
            var csType = MapDataTypeToCSharp(field.DataType);
            var nullable = IsValueType(field.DataType) ? "?" : "";
            if (field.DataType is "String" or "Text")
                sb.AppendLine("    [MaxLength(500)]");
            sb.AppendLine($"    public {csType}{nullable} {field.Name} {{ get; set; }}");
        }
        foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship" && f.RelationshipType != "ManyToMany").OrderBy(f => f.DisplayOrder))
            sb.AppendLine($"    public Guid? {FkPropName(field.Name)} {{ get; set; }}");
        sb.AppendLine("}");
        sb.AppendLine();

        // Patch Request DTO (partial update — all nullable)
        sb.AppendLine($"public class Patch{entity.Name}Request");
        sb.AppendLine("{");
        foreach (var field in entity.Fields.Where(f => f.DataType != "Relationship" && !f.IsPrimaryKey).OrderBy(f => f.DisplayOrder))
        {
            var csType = MapDataTypeToCSharp(field.DataType);
            // Always nullable for patch
            var nullable = IsValueType(field.DataType) ? "?" : "?";
            if (field.DataType is "String" or "Text")
                sb.AppendLine("    [MaxLength(500)]");
            sb.AppendLine($"    public {csType}{nullable} {field.Name} {{ get; set; }}");
        }
        foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship" && f.RelationshipType != "ManyToMany").OrderBy(f => f.DisplayOrder))
            sb.AppendLine($"    public Guid? {FkPropName(field.Name)} {{ get; set; }}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    // ─────────────────────────── CONTROLLER ───────────────────────────

    private string GenerateController(Entity entity, Project project, string projectName, AuthConfig? auth = null)
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
            {
                var navName = field.RelationshipType == "ManyToMany"
                    ? Pluralize(related.Name)
                    : related.Name;
                if (!includes.Contains(navName))
                    includes.Add(navName);
            }
        }
        // Reverse collection includes
        foreach (var otherEntity in project.Entities.Where(e => e.Id != entity.Id))
        {
            foreach (var field in otherEntity.Fields.Where(f =>
                f.DataType == "Relationship" &&
                f.RelatedEntityId == entity.Id &&
                f.RelationshipType == "OneToMany"))
            {
                var navName = Pluralize(otherEntity.Name);
                if (!includes.Contains(navName))
                    includes.Add(navName);
            }
        }

        sb.AppendLine("using Microsoft.AspNetCore.Mvc;");
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        if (auth?.Enabled == true)
            sb.AppendLine("using Microsoft.AspNetCore.Authorization;");
        sb.AppendLine($"using {projectName}.Data;");
        sb.AppendLine($"using {projectName}.DTOs;");
        sb.AppendLine($"using {projectName}.Models;");
        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.Controllers;");
        sb.AppendLine();
        sb.AppendLine($"[ApiController]");
        sb.AppendLine($"[Route(\"api/[controller]\")]");
        sb.AppendLine($"[Produces(\"application/json\")]");
        if (auth?.Enabled == true)
        {
            // Check if ALL methods for this entity are protected → class-level [Authorize]
            var prot = auth.EntityProtection.GetValueOrDefault(name);
            bool allProtected = prot != null && prot.Get && prot.Post && prot.Put && prot.Patch && prot.Delete;
            bool anyProtected = prot?.AnyProtected == true;
            if (allProtected || (auth.ProtectAllRoutes && anyProtected == false))
                sb.AppendLine("[Authorize]");
        }
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


        // ── Per-method auth helpers ──────────────────────────────────────────────
        var entityProt = auth?.Enabled == true ? auth.EntityProtection.GetValueOrDefault(name) : null;
        bool classAuthorize = auth?.Enabled == true && entityProt != null &&
                              entityProt.Get && entityProt.Post && entityProt.Put && entityProt.Patch && entityProt.Delete;
        // Returns [Authorize] line if this HTTP method needs protection and class-level not already applied
        string MethodAuth(bool methodProtected) =>
            (auth?.Enabled == true && methodProtected && !classAuthorize) ? "    [Authorize]\n" : "";

        // ── GET all ──
        sb.AppendLine($"    /// <summary>Get all {namePluralLower} with optional pagination</summary>");
        sb.Append(MethodAuth(entityProt?.Get == true));
        sb.AppendLine("    [HttpGet]");
        sb.AppendLine($"    [ProducesResponseType(typeof(IEnumerable<{name}Response>), StatusCodes.Status200OK)]");
        sb.AppendLine($"    public async Task<ActionResult<IEnumerable<{name}Response>>> GetAll(");
        sb.AppendLine("        [FromQuery] int page = 1,");
        sb.AppendLine("        [FromQuery] int pageSize = 20)");
        sb.AppendLine("    {");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            page = Math.Max(1, page);");
        sb.AppendLine("            pageSize = Math.Clamp(pageSize, 1, 100);");
        sb.AppendLine();
        sb.Append($"            var query = _context.{namePlural}");
        if (includes.Count > 0)
        {
            sb.AppendLine();
            foreach (var inc in includes)
                sb.AppendLine($"                .Include(x => x.{inc})");
            sb.AppendLine("                .AsNoTracking();");
        }
        else
        {
            sb.AppendLine(".AsNoTracking();");
        }
        sb.AppendLine();
        sb.AppendLine("            var total = await query.CountAsync();");
        sb.AppendLine();
        sb.AppendLine("            // ⚠ Fetch into memory first, then project — MapToResponse is not SQL-translatable");
        sb.AppendLine("            var rawItems = await query");
        sb.AppendLine("                .OrderByDescending(x => x.CreatedAt)");
        sb.AppendLine("                .Skip((page - 1) * pageSize)");
        sb.AppendLine("                .Take(pageSize)");
        sb.AppendLine("                .ToListAsync();");
        sb.AppendLine();
        sb.AppendLine("            var items = rawItems.Select(MapToResponse).ToList();");
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

        // ── GET by id ──
        sb.AppendLine($"    /// <summary>Get {nameLower} by ID</summary>");
        sb.Append(MethodAuth(entityProt?.Get == true));
        sb.AppendLine("    [HttpGet(\"{id:guid}\")]");
        sb.AppendLine($"    [ProducesResponseType(typeof({name}Response), StatusCodes.Status200OK)]");
        sb.AppendLine("    [ProducesResponseType(StatusCodes.Status404NotFound)]");
        sb.AppendLine($"    public async Task<ActionResult<{name}Response>> GetById(Guid id)");
        sb.AppendLine("    {");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.Append($"            var {nameLower} = await _context.{namePlural}");
        if (includes.Count > 0)
        {
            sb.AppendLine();
            foreach (var inc in includes)
                sb.AppendLine($"                .Include(x => x.{inc})");
            sb.AppendLine($"                .AsNoTracking()");
            sb.AppendLine($"                .FirstOrDefaultAsync(x => x.Id == id);");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine($"                .AsNoTracking()");
            sb.AppendLine($"                .FirstOrDefaultAsync(x => x.Id == id);");
        }
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

        // ── POST ──
        sb.AppendLine($"    /// <summary>Create a new {nameLower}</summary>");
        sb.Append(MethodAuth(entityProt?.Post == true));
        sb.AppendLine("    [HttpPost]");
        sb.AppendLine($"    [ProducesResponseType(typeof({name}Response), StatusCodes.Status201Created)]");
        sb.AppendLine("    [ProducesResponseType(StatusCodes.Status400BadRequest)]");
        sb.AppendLine("    [ProducesResponseType(StatusCodes.Status409Conflict)]");
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
        foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship" && f.RelationshipType != "ManyToMany").OrderBy(f => f.DisplayOrder))
            sb.AppendLine($"                {FkPropName(field.Name)} = request.{FkPropName(field.Name)},");
        sb.AppendLine("                CreatedAt = DateTime.UtcNow,");
        sb.AppendLine("                UpdatedAt = DateTime.UtcNow");
        sb.AppendLine("            };");
        sb.AppendLine();
        sb.AppendLine($"            _context.{namePlural}.Add({nameLower});");
        sb.AppendLine("            await _context.SaveChangesAsync();");
        sb.AppendLine();
        sb.AppendLine($"            return CreatedAtAction(nameof(GetById), new {{ id = {nameLower}.Id }}, MapToResponse({nameLower}));");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (DbUpdateException ex) when (IsUniqueViolation(ex))");
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

        // ── PUT ──
        sb.AppendLine($"    /// <summary>Fully replace {nameLower}</summary>");
        sb.Append(MethodAuth(entityProt?.Put == true));
        sb.AppendLine("    [HttpPut(\"{id:guid}\")]");
        sb.AppendLine($"    [ProducesResponseType(typeof({name}Response), StatusCodes.Status200OK)]");
        sb.AppendLine("    [ProducesResponseType(StatusCodes.Status404NotFound)]");
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
            sb.AppendLine($"            {nameLower}.{field.Name} = request.{field.Name}{(IsValueType(field.DataType) ? " ?? " + nameLower + "." + field.Name : "")};");
        foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship" && f.RelationshipType != "ManyToMany").OrderBy(f => f.DisplayOrder))
            sb.AppendLine($"            {nameLower}.{FkPropName(field.Name)} = request.{FkPropName(field.Name)};");
        sb.AppendLine($"            {nameLower}.UpdatedAt = DateTime.UtcNow;");
        sb.AppendLine();
        sb.AppendLine("            await _context.SaveChangesAsync();");
        sb.AppendLine($"            return Ok(MapToResponse({nameLower}));");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (DbUpdateException ex) when (IsUniqueViolation(ex))");
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

        // ── PATCH ──
        sb.AppendLine($"    /// <summary>Partially update {nameLower}</summary>");
        sb.Append(MethodAuth(entityProt?.Patch == true));
        sb.AppendLine("    [HttpPatch(\"{id:guid}\")]");
        sb.AppendLine($"    [ProducesResponseType(typeof({name}Response), StatusCodes.Status200OK)]");
        sb.AppendLine("    [ProducesResponseType(StatusCodes.Status404NotFound)]");
        sb.AppendLine($"    public async Task<ActionResult<{name}Response>> Patch(Guid id, [FromBody] Patch{name}Request request)");
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
        {
            if (IsValueType(field.DataType))
                sb.AppendLine($"            if (request.{field.Name}.HasValue) {nameLower}.{field.Name} = request.{field.Name}.Value;");
            else
                sb.AppendLine($"            if (request.{field.Name} != null) {nameLower}.{field.Name} = request.{field.Name};");
        }
        foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship" && f.RelationshipType != "ManyToMany").OrderBy(f => f.DisplayOrder))
            sb.AppendLine($"            if (request.{FkPropName(field.Name)}.HasValue) {nameLower}.{FkPropName(field.Name)} = request.{FkPropName(field.Name)};");
        sb.AppendLine($"            {nameLower}.UpdatedAt = DateTime.UtcNow;");
        sb.AppendLine();
        sb.AppendLine("            await _context.SaveChangesAsync();");
        sb.AppendLine($"            return Ok(MapToResponse({nameLower}));");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (DbUpdateException ex) when (IsUniqueViolation(ex))");
        sb.AppendLine("        {");
        sb.AppendLine($"            return Conflict(new {{ message = \"{name} with these values already exists\" }});");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine($"            _logger.LogError(ex, \"Error patching {nameLower} {{Id}}\", id);");
        sb.AppendLine("            throw;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // ── DELETE ──
        sb.AppendLine($"    /// <summary>Delete {nameLower}</summary>");
        sb.Append(MethodAuth(entityProt?.Delete == true));
        sb.AppendLine("    [HttpDelete(\"{id:guid}\")]");
        sb.AppendLine("    [ProducesResponseType(StatusCodes.Status204NoContent)]");
        sb.AppendLine("    [ProducesResponseType(StatusCodes.Status404NotFound)]");
        sb.AppendLine("    [ProducesResponseType(StatusCodes.Status409Conflict)]");
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

        // ── MapToResponse ──
        sb.AppendLine($"    private static {name}Response MapToResponse({name} x) => new()");
        sb.AppendLine("    {");
        sb.AppendLine("        Id = x.Id,");
        foreach (var field in entity.Fields.Where(f => f.DataType != "Relationship" && !f.IsPrimaryKey).OrderBy(f => f.DisplayOrder))
            sb.AppendLine($"        {field.Name} = x.{field.Name},");
        foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship" && f.RelationshipType != "ManyToMany").OrderBy(f => f.DisplayOrder))
            sb.AppendLine($"        {FkPropName(field.Name)} = x.{FkPropName(field.Name)},");
        sb.AppendLine("        CreatedAt = x.CreatedAt,");
        sb.AppendLine("        UpdatedAt = x.UpdatedAt");
        sb.AppendLine("    };");
        sb.AppendLine();

        // ── Helper ──
        sb.AppendLine("    private static bool IsUniqueViolation(DbUpdateException ex) =>");
        sb.AppendLine("        ex.InnerException?.Message.Contains(\"unique\", StringComparison.OrdinalIgnoreCase) == true ||");
        sb.AppendLine("        ex.InnerException?.Message.Contains(\"duplicate\", StringComparison.OrdinalIgnoreCase) == true;");
        sb.AppendLine("}");

        return sb.ToString();
    }

    // ─────────────────────────── DB CONTEXT ───────────────────────────

    private string GenerateDbContext(Project project, string projectName, bool authEnabled = false)
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

        if (authEnabled)
            sb.AppendLine("    public DbSet<User> Users { get; set; }");

        sb.AppendLine();
        sb.AppendLine("    protected override void OnModelCreating(ModelBuilder modelBuilder)");
        sb.AppendLine("    {");
        sb.AppendLine("        base.OnModelCreating(modelBuilder);");
        sb.AppendLine();

        // Track which M2M pairs have already been configured (EF Core configures M2M once from one side)
        var configuredM2M = new HashSet<string>();

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
                    // Only configure M2M once (sorted key to avoid duplicates)
                    var pairKey = string.Join("_", new[] { entity.Name, related.Name }.OrderBy(x => x));
                    if (!configuredM2M.Contains(pairKey))
                    {
                        sb.AppendLine($"            // Many-to-Many: {entity.Name} <-> {related.Name}");
                        sb.AppendLine($"            e.HasMany(x => x.{Pluralize(related.Name)})");
                        sb.AppendLine($"             .WithMany(x => x.{Pluralize(entity.Name)});");
                        configuredM2M.Add(pairKey);
                    }
                }
                else if (field.RelationshipType == "OneToOne")
                {
                    var fkProp = FkPropName(field.Name);
                    var navProp = NavPropName(field.Name);
                    sb.AppendLine($"            e.HasOne(x => x.{navProp})");
                    sb.AppendLine($"             .WithOne()");
                    sb.AppendLine($"             .HasForeignKey<{entity.Name}>(x => x.{fkProp})");
                    sb.AppendLine($"             .OnDelete(DeleteBehavior.SetNull);");
                }
                else // OneToMany
                {
                    var fkProp = FkPropName(field.Name);
                    var navProp = NavPropName(field.Name);
                    sb.AppendLine($"            e.HasOne(x => x.{navProp})");
                    sb.AppendLine($"             .WithMany(x => x.{Pluralize(entity.Name)})");
                    sb.AppendLine($"             .HasForeignKey(x => x.{fkProp})");
                    sb.AppendLine($"             .IsRequired(false)");
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
            _logger.LogError(ex, ""Unhandled exception for {{Method}} {{Path}}"",
                context.Request.Method, context.Request.Path);
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

    // ─────────────────────────── GLOBAL USINGS ───────────────────────────

    private string GenerateGlobalUsings(string projectName)
    {
        return $@"// Global using directives — applied to all files in the project
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using Microsoft.AspNetCore.Http;
global using Microsoft.Extensions.Logging;
global using {projectName}.Models;
global using {projectName}.DTOs;
global using {projectName}.Data;
";
    }

    // ─────────────────────────── LAUNCH SETTINGS ───────────────────────────

    private string GenerateLaunchSettings(string projectName)
    {
        return $@"{{
  ""$schema"": ""https://json.schemastore.org/launchsettings.json"",
  ""profiles"": {{
    ""{projectName}"": {{
      ""commandName"": ""Project"",
      ""dotnetRunMessages"": true,
      ""launchBrowser"": true,
      ""launchUrl"": ""swagger"",
      ""applicationUrl"": ""https://localhost:7000;http://localhost:5000"",
      ""environmentVariables"": {{
        ""ASPNETCORE_ENVIRONMENT"": ""Development""
      }}
    }},
    ""Docker"": {{
      ""commandName"": ""Docker"",
      ""launchBrowser"": true,
      ""launchUrl"": ""{{Scheme}}://{{ServiceHost}}:{{ServicePort}}/swagger"",
      ""publishAllPorts"": true
    }}
  }}
}}
";
    }

    // ─────────────────────────── PROGRAM.CS ───────────────────────────

    private string GenerateProgram(Project project, string projectName, AuthConfig? auth = null)
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
        options.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    }});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{{
    c.SwaggerDoc(""v1"", new Microsoft.OpenApi.Models.OpenApiInfo
    {{
        Title = ""{projectName} API"",
        Version = ""v1"",
        Description = ""Generated by **CodeForge**. Full CRUD for all entities{(auth?.Enabled == true ? " with JWT authentication" : "")}."",
    }});
{(auth?.Enabled == true ? @"    c.AddSecurityDefinition(""Bearer"", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = ""Enter: Bearer {your JWT token}"",
        Name = ""Authorization"",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        BearerFormat = ""JWT"",
        Scheme = ""bearer""
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                { Id = ""Bearer"", Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme }
            },
            Array.Empty<string>()
        }
    });" : "")}
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
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()
              .WithExposedHeaders(""X-Total-Count"", ""X-Page"", ""X-Page-Size""));
}});

// Health checks
builder.Services.AddHealthChecks()
    .AddNpgsql(builder.Configuration.GetConnectionString(""DefaultConnection"")!);
{(auth?.Enabled == true ? @"
// ── JWT Authentication ─────────────────────────────────────────────────
var jwtSettings = builder.Configuration.GetSection(""JwtSettings"");
builder.Services.AddAuthentication(options =>
{{
    options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
}})
.AddJwtBearer(options =>
{{
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {{
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings[""Issuer""],
        ValidAudience = jwtSettings[""Audience""],
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(jwtSettings[""Key""]!))
    }};
}});
builder.Services.AddAuthorization();
builder.Services.AddScoped<{projectName}.Services.IAuthService, {projectName}.Services.AuthService>();
builder.Services.AddScoped<{projectName}.Services.TokenService>();" : "")}

var app = builder.Build();

// ─── Auto-migrate on startup (Development) ──────────────────────────────────
if (app.Environment.IsDevelopment())
{{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}}

// ─── Middleware pipeline ─────────────────────────────────────────────────────
app.UseMiddleware<ErrorHandlerMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint(""/swagger/v1/swagger.json"", ""{projectName} v1""));

app.UseHttpsRedirection();
app.UseCors(""AllowAll"");
{(auth?.Enabled == true ? "app.UseAuthentication();" : "")}
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks(""/health"");

app.Run();
";
    }

    // ─────────────────────────── PROJECT FILE ───────────────────────────

    private string GenerateProjectFile(bool authEnabled = false)
    {
        var authPackages = authEnabled ? @"
    <PackageReference Include=""Microsoft.AspNetCore.Authentication.JwtBearer"" Version=""9.0.0"" />
    <PackageReference Include=""BCrypt.Net-Next"" Version=""4.0.3"" />" : "";
        return $@"<Project Sdk=""Microsoft.NET.Sdk.Web"">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>$(MSBuildProjectName.Replace(""-"", ""_""))</RootNamespace>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""Microsoft.EntityFrameworkCore"" Version=""9.0.0"" />
    <PackageReference Include=""Microsoft.EntityFrameworkCore.Design"" Version=""9.0.0"">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include=""Npgsql.EntityFrameworkCore.PostgreSQL"" Version=""9.0.0"" />
    <PackageReference Include=""AspNetCore.HealthChecks.NpgSql"" Version=""9.0.0"" />
    <PackageReference Include=""Swashbuckle.AspNetCore"" Version=""6.9.0"" />
    <PackageReference Include=""Swashbuckle.AspNetCore.Annotations"" Version=""6.9.0"" />{authPackages}
  </ItemGroup>

</Project>
";
    }

    // ─────────────────────────── APPSETTINGS ───────────────────────────

    private string GenerateAppSettings(string projectName, AuthConfig? auth = null)
    {
        var jwtSection = auth?.Enabled == true ? $@",
  ""JwtSettings"": {{
    ""Key"": ""CHANGE_ME_USE_A_LONG_RANDOM_SECRET_KEY_32_CHARS_MIN"",
    ""Issuer"": ""{projectName}"",
    ""Audience"": ""{projectName}-users"",
    ""AccessTokenExpiryMinutes"": {auth.TokenExpiryMinutes},
    ""RefreshTokenExpiryDays"": {auth.RefreshTokenExpiryDays}
  }}" : "";
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
  ""AllowedHosts"": ""*""{jwtSection}
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
        return $@"FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
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
      context: .
      dockerfile: Dockerfile
    restart: unless-stopped
    ports:
      - ""5000:8080""
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_HTTP_PORTS=8080
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

        var endpointsTable = string.Join("\n", project.Entities.Select(e =>
        {
            return $"| `{e.Name}` | `/api/{e.Name}` | GET (list), POST, GET/{{id}}, PUT/{{id}}, PATCH/{{id}}, DELETE/{{id}} |";
        }));

        return $@"# {projectName}

> Generated with **CodeForge** — ASP.NET Core 9 + PostgreSQL Backend

## Tech Stack

- **Framework**: ASP.NET Core 9.0 (Web API)
- **ORM**: Entity Framework Core 9 with Npgsql
- **Database**: PostgreSQL 16
- **Docs**: Swagger / OpenAPI (`http://localhost:5000/swagger`)
- **Health Checks**: `/health`
- **Containerization**: Docker + Docker Compose

## Generated Entities

{entityList}

## API Endpoints

| Entity | Base Route | Methods |
|--------|-----------|---------|
{endpointsTable}

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

1. Update connection string in `appsettings.json`

2. Install EF tools (once):
```bash
dotnet tool install --global dotnet-ef
```

3. Apply migrations:
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

4. Run:
```bash
dotnet run
```

## Pagination

```
GET /api/{{entity}}?page=1&pageSize=20
```

Response headers: `X-Total-Count`, `X-Page`, `X-Page-Size`

## Notes

- All responses use **camelCase** JSON
- Null fields are omitted from responses
- Auto-migration runs on startup in **Development** mode
- Duplicate/unique constraint violations return `409 Conflict`
- Foreign key violations on delete return `409 Conflict`
- PATCH supports partial updates (only provided fields are changed)
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
        "File" => "string",  // stores path/URL; handle upload separately
        _ => "string"
    };

    private bool IsValueType(string dataType) =>
        dataType is "Integer" or "Boolean" or "DateTime" or "Decimal" or "Float" or "Long" or "Guid";

    private string GetDefaultValue(string dataType, bool isPk) => dataType switch
    {
        "Guid" when isPk => " = Guid.NewGuid();",
        "String" => " = string.Empty;",
        "Text" => " = string.Empty;",
        "File" => " = string.Empty;",
        "Boolean" => " = false;",
        _ => ""
    };

    private string GetInitDefault(string dataType) => dataType switch
    {
        "String" => "string.Empty",
        "Text" => "string.Empty",
        "File" => "string.Empty",
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

    /// <summary>
    /// Returns the canonical FK property name for a relationship field.
    /// If the field is already named "CategoryId" it stays "CategoryId";
    /// if it's named "Category" it becomes "CategoryId".
    /// </summary>
    private static string FkPropName(string fieldName) =>
        fieldName.EndsWith("Id", StringComparison.OrdinalIgnoreCase) ? fieldName : fieldName + "Id";

    /// <summary>
    /// Returns the navigation property name (without "Id" suffix).
    /// "CategoryId" → "Category", "Author" → "Author"
    /// </summary>
    private static string NavPropName(string fieldName) =>
        fieldName.EndsWith("Id", StringComparison.OrdinalIgnoreCase) ? fieldName[..^2] : fieldName;

    private string SanitizeName(string name) =>
        string.IsNullOrWhiteSpace(name) ? "GeneratedProject" :
        new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray())
            .TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');

    // ═══════════════════ AUTH MODULE GENERATORS ═══════════════════════

    private string GenerateAuthUserModel(string ns, AuthConfig auth)
    {
        var roles = auth.EnableRoles
            ? $"\n    /// <summary>Comma-separated roles, e.g. \"User,Admin\"</summary>\n    [MaxLength(200)]\n    public string Role {{ get; set; }} = \"{(auth.Roles.FirstOrDefault() ?? "User")}\";"
            : "";
        var refreshToken = auth.EnableRefreshTokens
            ? "\n    public string? RefreshToken { get; set; }\n    public DateTime? RefreshTokenExpiresAt { get; set; }"
            : "";
        var emailVerif = auth.EnableEmailVerification
            ? "\n    public bool IsEmailVerified { get; set; } = false;\n    public string? EmailVerificationToken { get; set; }"
            : "";
        var username = auth.UserIdentifier is "username" or "both"
            ? "\n    [MaxLength(100)]\n    public string? Username { get; set; }"
            : "";

        return $@"using System.ComponentModel.DataAnnotations;

namespace {ns}.Models;

public class User
{{
    [Key]
    public Guid Id {{ get; set; }} = Guid.NewGuid();
{(auth.UserIdentifier is "email" or "both" ? "\n    [Required]\n    [MaxLength(255)]\n    public string Email { get; set; } = string.Empty;\n" : "")}{username}
    [Required]
    public string PasswordHash {{ get; set; }} = string.Empty;{roles}{refreshToken}{emailVerif}

    public DateTime CreatedAt {{ get; set; }} = DateTime.UtcNow;
    public DateTime UpdatedAt {{ get; set; }} = DateTime.UtcNow;
}}
";
    }

    private string GenerateAuthDtos(string ns, AuthConfig auth)
    {
        var refreshTokenProp = auth.EnableRefreshTokens
            ? "\n    public string? RefreshToken { get; set; }" : "";
        var refreshRequest = auth.EnableRefreshTokens ? $@"

public class RefreshRequest
{{
    [Required]
    public string RefreshToken {{ get; set; }} = string.Empty;
}}" : "";
        var roleProp = auth.EnableRoles
            ? "\n    public string? Role { get; set; }" : "";
        var emailProp = auth.UserIdentifier is "email" or "both"
            ? "\n    public string? Email { get; set; }" : "";
        var usernameProp = auth.UserIdentifier is "username" or "both"
            ? "\n    public string? Username { get; set; }" : "";
        var loginIdentifier = auth.UserIdentifier == "username"
            ? "    [Required]\n    public string Username { get; set; } = string.Empty;"
            : "    [Required, EmailAddress, MaxLength(255)]\n    public string Email { get; set; } = string.Empty;";
        var registerUsername = auth.UserIdentifier is "username" or "both"
            ? "\n    public string? Username { get; set; }" : "";

        return $@"using System.ComponentModel.DataAnnotations;

namespace {ns}.DTOs;

public class RegisterRequest
{{
    [Required, EmailAddress, MaxLength(255)]
    public string Email {{ get; set; }} = string.Empty;

    [Required, MinLength(6), MaxLength(100)]
    public string Password {{ get; set; }} = string.Empty;{registerUsername}
}}

public class LoginRequest
{{
{loginIdentifier}

    [Required]
    public string Password {{ get; set; }} = string.Empty;
}}

public class ChangePasswordRequest
{{
    [Required]
    public string CurrentPassword {{ get; set; }} = string.Empty;

    [Required, MinLength(6)]
    public string NewPassword {{ get; set; }} = string.Empty;
}}

public class TokenResponse
{{
    public string AccessToken {{ get; set; }} = string.Empty;{refreshTokenProp}
    public int ExpiresInSeconds {{ get; set; }}
    public UserResponse User {{ get; set; }} = null!;
}}

public class UserResponse
{{
    public Guid Id {{ get; set; }}{emailProp}{usernameProp}{roleProp}
    public DateTime CreatedAt {{ get; set; }}
}}{refreshRequest}
";
    }

    private string GenerateIAuthService(string ns, AuthConfig auth)
    {
        var refreshMethods = auth.EnableRefreshTokens ? @"
    Task<TokenResponse> RefreshAsync(string refreshToken);
    Task LogoutAsync(Guid userId);" : "";
        return $@"using {ns}.DTOs;

namespace {ns}.Services;

public interface IAuthService
{{
    Task<TokenResponse> RegisterAsync(RegisterRequest request);
    Task<TokenResponse> LoginAsync(LoginRequest request);{refreshMethods}
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
}}
";
    }

    private string GenerateTokenService(string ns, AuthConfig auth)
    {
        return $@"using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using {ns}.Models;

namespace {ns}.Services;

public class TokenService
{{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {{
        _config = config;
    }}

    public string GenerateAccessToken(User user)
    {{
        var jwtSettings = _config.GetSection(""JwtSettings"");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings[""Key""]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {{
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? """"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        }};

{(auth.EnableRoles ? "        if (!string.IsNullOrEmpty(user.Role))\n            claims.Add(new Claim(ClaimTypes.Role, user.Role));" : "")}

        var token = new JwtSecurityToken(
            issuer: jwtSettings[""Issuer""],
            audience: jwtSettings[""Audience""],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                int.TryParse(jwtSettings[""AccessTokenExpiryMinutes""], out var mins) ? mins : {auth.TokenExpiryMinutes}),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }}

    public string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public ClaimsPrincipal? ValidateAccessToken(string token)
    {{
        var jwtSettings = _config.GetSection(""JwtSettings"");
        try
        {{
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {{
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = false, // we handle expiry ourselves for refresh
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings[""Issuer""],
                ValidAudience = jwtSettings[""Audience""],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings[""Key""]!))
            }}, out _);
            return principal;
        }}
        catch
        {{
            return null;
        }}
    }}
}}
";
    }

    private string GenerateAuthService(string ns, AuthConfig auth)
    {
        var refreshExpiry = auth.EnableRefreshTokens ? auth.RefreshTokenExpiryDays : 7;
        var refreshLogic = auth.EnableRefreshTokens ? $@"
        var refreshToken = _tokenService.GenerateRefreshToken();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays({refreshExpiry});
        await _context.SaveChangesAsync();" : "";

        return $@"using Microsoft.EntityFrameworkCore;
using {ns}.Data;
using {ns}.DTOs;
using {ns}.Models;

namespace {ns}.Services;

public class AuthService : IAuthService
{{
    private readonly ApplicationDbContext _context;
    private readonly TokenService _tokenService;

    public AuthService(ApplicationDbContext context, TokenService tokenService)
    {{
        _context = context;
        _tokenService = tokenService;
    }}

    public async Task<TokenResponse> RegisterAsync(RegisterRequest request)
    {{
        if (await _context.Users.AnyAsync(u => u.Email == request.Email.ToLower()))
            throw new InvalidOperationException(""Email already in use"");

        var user = new User
        {{
            Email = request.Email.ToLower(),
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
        }};

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return BuildResponse(user);
    }}

    public async Task<TokenResponse> LoginAsync(LoginRequest request)
    {{
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email.ToLower())
            ?? throw new UnauthorizedAccessException(""Invalid credentials"");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException(""Invalid credentials"");
{refreshLogic}
        return BuildResponse(user);
    }}

    {(auth.EnableRefreshTokens ? $@"public async Task<TokenResponse> RefreshAsync(string refreshToken)
    {{
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken
                && u.RefreshTokenExpiresAt > DateTime.UtcNow)
            ?? throw new UnauthorizedAccessException(""Invalid or expired refresh token"");

        return BuildResponse(user);
    }}

    public async Task LogoutAsync(Guid userId)
    {{
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {{
            user.RefreshToken = null;
            user.RefreshTokenExpiresAt = null;
            await _context.SaveChangesAsync();
        }}
    }}

    " : "")}public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {{
        var user = await _context.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException(""User not found"");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException(""Current password is incorrect"");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }}

    private TokenResponse BuildResponse(User user) => new()
    {{
        AccessToken = _tokenService.GenerateAccessToken(user),
        {(auth.EnableRefreshTokens ? "RefreshToken = user.RefreshToken," : "")}
        ExpiresInSeconds = {auth.TokenExpiryMinutes * 60},
        User = new UserResponse
        {{
            Id = user.Id,
            {(auth.UserIdentifier is "email" or "both" ? "Email = user.Email," : "")}
            {(auth.UserIdentifier is "username" or "both" ? "Username = user.Username," : "")}
            {(auth.EnableRoles ? "Role = user.Role," : "")}
            CreatedAt = user.CreatedAt
        }}
    }};
}}
";
    }

    private string GenerateAuthController(string ns, AuthConfig auth)
    {
        return $@"using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using {ns}.DTOs;
using {ns}.Services;

namespace {ns}.Controllers;

[ApiController]
[Route(""api/auth"")]
[Produces(""application/json"")]
public class AuthController : ControllerBase
{{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {{
        _authService = authService;
    }}

    /// <summary>Register a new user</summary>
    [HttpPost(""register"")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TokenResponse>> Register([FromBody] RegisterRequest request)
    {{
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {{
            var result = await _authService.RegisterAsync(request);
            return StatusCode(201, result);
        }}
        catch (InvalidOperationException ex)
        {{
            return Conflict(new {{ message = ex.Message }});
        }}
    }}

    /// <summary>Login and receive JWT</summary>
    [HttpPost(""login"")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResponse>> Login([FromBody] LoginRequest request)
    {{
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {{
            var result = await _authService.LoginAsync(request);
            return Ok(result);
        }}
        catch (UnauthorizedAccessException ex)
        {{
            return Unauthorized(new {{ message = ex.Message }});
        }}
    }}

    {(auth.EnableRefreshTokens ? $@"/// <summary>Refresh access token</summary>
    [HttpPost(""refresh"")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TokenResponse>> Refresh([FromBody] RefreshRequest request)
    {{
        try
        {{
            return Ok(await _authService.RefreshAsync(request.RefreshToken));
        }}
        catch (UnauthorizedAccessException ex)
        {{
            return Unauthorized(new {{ message = ex.Message }});
        }}
    }}

    /// <summary>Logout (invalidates refresh token)</summary>
    [Authorize]
    [HttpPost(""logout"")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {{
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _authService.LogoutAsync(userId);
        return NoContent();
    }}

    " : "")}/// <summary>Get current user profile</summary>
    [Authorize]
    [HttpGet(""me"")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    public ActionResult<UserResponse> Me()
    {{
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        {(auth.UserIdentifier is "email" or "both" ? "var email = User.FindFirstValue(ClaimTypes.Email);" : "")}
        {(auth.EnableRoles ? "var role = User.FindFirstValue(ClaimTypes.Role);" : "")}
        return Ok(new UserResponse
        {{
            Id = userId,
            {(auth.UserIdentifier is "email" or "both" ? "Email = email," : "")}
            {(auth.EnableRoles ? "Role = role," : "")}
        }});
    }}

    /// <summary>Change password</summary>
    [Authorize]
    [HttpPut(""password"")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {{
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        try
        {{
            await _authService.ChangePasswordAsync(userId, request);
            return NoContent();
        }}
        catch (UnauthorizedAccessException ex)
        {{
            return Unauthorized(new {{ message = ex.Message }});
        }}
    }}
}}
";
    }
}

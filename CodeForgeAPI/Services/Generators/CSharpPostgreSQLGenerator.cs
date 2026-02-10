using System.Text;
using CodeForgeAPI.Models;

namespace CodeForgeAPI.Services.Generators;

public class CSharpPostgreSQLGenerator : ITemplateGenerator
{
    public Dictionary<string, string> Generate(Project project)
    {
        var files = new Dictionary<string, string>();
        var projectName = SanitizeName(project.Name);
        
        // Generate Models
        foreach (var entity in project.Entities)
        {
            files[$"{projectName}/Models/{entity.Name}.cs"] = GenerateModel(entity, projectName);
        }
        
        // Generate Controllers
        foreach (var entity in project.Entities)
        {
            files[$"{projectName}/Controllers/{entity.Name}Controller.cs"] = GenerateController(entity, projectName);
        }
        
        // Generate DbContext
        files[$"{projectName}/Data/ApplicationDbContext.cs"] = GenerateDbContext(project.Entities, projectName);
        
        // Generate appsettings.json
        files[$"{projectName}/appsettings.json"] = GenerateAppSettings();
        files[$"{projectName}/appsettings.Development.json"] = GenerateAppSettingsDevelopment();
        
        // Generate Program.cs
        files[$"{projectName}/Program.cs"] = GenerateProgram(projectName);
        
        // Generate project file
        files[$"{projectName}/{projectName}.csproj"] = GenerateProjectFile();
        
        // Generate Dockerfile
        files[$"{projectName}/Dockerfile"] = GenerateDockerfile(projectName);
        
        // Generate docker-compose.yml
        files["docker-compose.yml"] = GenerateDockerCompose(projectName);
        
        // Generate README.md
        files["README.md"] = GenerateReadme(projectName);
        
        return files;
    }
    
    private string GenerateModel(Entity entity, string projectName)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("using System;");
        sb.AppendLine("using System.ComponentModel.DataAnnotations;");
        sb.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.Models;");
        sb.AppendLine();
        sb.AppendLine($"public class {entity.Name}");
        sb.AppendLine("{");
        
        foreach (var field in entity.Fields.OrderBy(f => f.DisplayOrder))
        {
            if (field.DataType == "Relationship")
            {
                // Navigation properties handled separately
                continue;
            }
            
            if (field.IsPrimaryKey)
            {
                sb.AppendLine("    [Key]");
            }
            
            if (field.IsRequired)
            {
                sb.AppendLine("    [Required]");
            }
            
            if (field.IsUnique && field.DataType == "String")
            {
                sb.AppendLine("    [StringLength(255)]");
            }
            
            var csType = MapDataTypeToCSharp(field.DataType);
            var nullableMarker = (!field.IsRequired && IsValueType(field.DataType)) ? "?" : "";
            
            sb.AppendLine($"    public {csType}{nullableMarker} {field.Name} {{ get; set; }}");
            sb.AppendLine();
        }
        
        // Add navigation properties for relationships
        foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship"))
        {
            if (field.RelatedEntityId.HasValue)
            {
                var relatedEntity = entity.Project.Entities.FirstOrDefault(e => e.Id == field.RelatedEntityId);
                if (relatedEntity != null)
                {
                    if (field.RelationshipType == "ManyToMany")
                    {
                        sb.AppendLine($"    public ICollection<{relatedEntity.Name}> {field.Name} {{ get; set; }} = new List<{relatedEntity.Name}>();");
                    }
                    else // OneToMany or default
                    {
                        sb.AppendLine($"    [ForeignKey(\"{field.Name}Id\")]");
                        sb.AppendLine($"    public {relatedEntity.Name}? {field.Name} {{ get; set; }}");
                        sb.AppendLine($"    public Guid? {field.Name}Id {{ get; set; }}");
                    }
                    sb.AppendLine();
                }
            }
        }
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateController(Entity entity, string projectName)
    {
        var sb = new StringBuilder();
        var entityNameLower = entity.Name.ToLower();
        var entityNamePlural = entity.Name + "s";
        
        sb.AppendLine("using Microsoft.AspNetCore.Mvc;");
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine($"using {projectName}.Data;");
        sb.AppendLine($"using {projectName}.Models;");
        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.Controllers;");
        sb.AppendLine();
        sb.AppendLine("[Route(\"api/[controller]\")]");
        sb.AppendLine("[ApiController]");
        sb.AppendLine($"public class {entity.Name}Controller : ControllerBase");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly ApplicationDbContext _context;");
        sb.AppendLine();
        sb.AppendLine($"    public {entity.Name}Controller(ApplicationDbContext context)");
        sb.AppendLine("    {");
        sb.AppendLine("        _context = context;");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        var pkField = entity.Fields.FirstOrDefault(f => f.IsPrimaryKey);
        var pkType = pkField != null ? MapDataTypeToCSharp(pkField.DataType) : "Guid";
        var pkName = pkField?.Name ?? "Id";
        
        // GET all
        sb.AppendLine("    [HttpGet]");
        sb.AppendLine($"    public async Task<ActionResult<IEnumerable<{entity.Name}>>> Get{entityNamePlural}()");
        sb.AppendLine("    {");
        sb.AppendLine($"        return await _context.{entityNamePlural}.ToListAsync();");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // GET by id
        sb.AppendLine($"    [HttpGet(\"{{{entityNameLower}Id}}\")]");
        sb.AppendLine($"    public async Task<ActionResult<{entity.Name}>> Get{entity.Name}({pkType} {entityNameLower}Id)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var {entityNameLower} = await _context.{entityNamePlural}.FindAsync({entityNameLower}Id);");
        sb.AppendLine();
        sb.AppendLine($"        if ({entityNameLower} == null)");
        sb.AppendLine("        {");
        sb.AppendLine("            return NotFound();");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine($"        return {entityNameLower};");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // POST
        sb.AppendLine("    [HttpPost]");
        sb.AppendLine($"    public async Task<ActionResult<{entity.Name}>> Post{entity.Name}({entity.Name} {entityNameLower})");
        sb.AppendLine("    {");
        if (pkType == "Guid")
        {
            sb.AppendLine($"        if ({entityNameLower}.{pkName} == Guid.Empty)");
            sb.AppendLine($"            {entityNameLower}.{pkName} = Guid.NewGuid();");
            sb.AppendLine();
        }
        sb.AppendLine($"        _context.{entityNamePlural}.Add({entityNameLower});");
        sb.AppendLine("        await _context.SaveChangesAsync();");
        sb.AppendLine();
        sb.AppendLine($"        return CreatedAtAction(nameof(Get{entity.Name}), new {{ {entityNameLower}Id = {entityNameLower}.{pkName} }}, {entityNameLower});");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // PUT
        sb.AppendLine($"    [HttpPut(\"{{{entityNameLower}Id}}\")]");
        sb.AppendLine($"    public async Task<IActionResult> Put{entity.Name}({pkType} {entityNameLower}Id, {entity.Name} {entityNameLower})");
        sb.AppendLine("    {");
        sb.AppendLine($"        if ({entityNameLower}Id != {entityNameLower}.{pkName})");
        sb.AppendLine("        {");
        sb.AppendLine("            return BadRequest();");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine($"        _context.Entry({entityNameLower}).State = EntityState.Modified;");
        sb.AppendLine("        await _context.SaveChangesAsync();");
        sb.AppendLine();
        sb.AppendLine("        return NoContent();");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // DELETE
        sb.AppendLine($"    [HttpDelete(\"{{{entityNameLower}Id}}\")]");
        sb.AppendLine($"    public async Task<IActionResult> Delete{entity.Name}({pkType} {entityNameLower}Id)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var {entityNameLower} = await _context.{entityNamePlural}.FindAsync({entityNameLower}Id);");
        sb.AppendLine($"        if ({entityNameLower} == null)");
        sb.AppendLine("        {");
        sb.AppendLine("            return NotFound();");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine($"        _context.{entityNamePlural}.Remove({entityNameLower});");
        sb.AppendLine("        await _context.SaveChangesAsync();");
        sb.AppendLine();
        sb.AppendLine("        return NoContent();");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateDbContext(IEnumerable<Entity> entities, string projectName)
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
        sb.AppendLine("        : base(options)");
        sb.AppendLine("    {");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        foreach (var entity in entities)
        {
            sb.AppendLine($"    public DbSet<{entity.Name}> {entity.Name}s {{ get; set; }}");
        }
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateAppSettings()
    {
        return @"{
  ""ConnectionStrings"": {
    ""DefaultConnection"": ""Host=localhost;Database=myappdb;Username=postgres;Password=yourpassword""
  },
  ""Logging"": {
    ""LogLevel"": {
      ""Default"": ""Information"",
      ""Microsoft.AspNetCore"": ""Warning""
    }
  },
  ""AllowedHosts"": ""*""
}";
    }
    
    private string GenerateAppSettingsDevelopment()
    {
        return @"{
  ""Logging"": {
    ""LogLevel"": {
      ""Default"": ""Information"",
      ""Microsoft.AspNetCore"": ""Warning"",
      ""Microsoft.EntityFrameworkCore"": ""Information""
    }
  }
}";
    }
    
    private string GenerateProgram(string projectName)
    {
        return $@"using Microsoft.EntityFrameworkCore;
using {projectName}.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add database context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString(""DefaultConnection"")));

// Add CORS
builder.Services.AddCors(options =>
{{
    options.AddPolicy(""AllowAll"",
        builder => builder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
}});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{{
    app.UseSwagger();
    app.UseSwaggerUI();
}}

app.UseHttpsRedirection();
app.UseCors(""AllowAll"");
app.UseAuthorization();
app.MapControllers();

app.Run();
";
    }
    
    private string GenerateProjectFile()
    {
        return @"<Project Sdk=""Microsoft.NET.Sdk.Web"">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""Microsoft.EntityFrameworkCore"" Version=""8.0.0"" />
    <PackageReference Include=""Microsoft.EntityFrameworkCore.Design"" Version=""8.0.0"" />
    <PackageReference Include=""Npgsql.EntityFrameworkCore.PostgreSQL"" Version=""8.0.0"" />
    <PackageReference Include=""Swashbuckle.AspNetCore"" Version=""6.5.0"" />
  </ItemGroup>

</Project>
";
    }
    
    private string GenerateDockerfile(string projectName)
    {
        return $@"FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY [""{projectName}.csproj"", ""./""
RUN dotnet restore ""{projectName}.csproj""
COPY . .
RUN dotnet build ""{projectName}.csproj"" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish ""{projectName}.csproj"" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT [""dotnet"", ""{projectName}.dll""]
";
    }
    
    private string GenerateDockerCompose(string projectName)
    {
        return $@"version: '3.8'

services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: myappdb
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: yourpassword
    ports:
      - ""5432:5432""
    volumes:
      - postgres_data:/var/lib/postgresql/data

  api:
    build:
      context: ./{projectName}
      dockerfile: Dockerfile
    ports:
      - ""5000:80""
    environment:
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=myappdb;Username=postgres;Password=yourpassword
    depends_on:
      - postgres

volumes:
  postgres_data:
";
    }
    
    private string GenerateReadme(string projectName)
    {
        return $@"# {projectName}

This project was generated using the Backend Code Generator.

## Prerequisites

- .NET 8.0 SDK
- PostgreSQL 16+ or Docker

## Getting Started

### Using Docker

1. Run the application with Docker Compose:

```bash
docker-compose up
```

The API will be available at `http://localhost:5000`

### Local Development

1. Update the connection string in `appsettings.json`:

```json
""ConnectionStrings"": {{
  ""DefaultConnection"": ""Host=localhost;Database=myappdb;Username=postgres;Password=yourpassword""
}}
```

2. Install Entity Framework tools:

```bash
dotnet tool install --global dotnet-ef
```

3. Create the database:

```bash
cd {projectName}
dotnet ef migrations add InitialCreate
dotnet ef database update
```

4. Run the application:

```bash
dotnet run
```

The API will be available at `https://localhost:5001`

## API Documentation

Swagger UI is available at `/swagger` in development mode.

## Generated Entities

";
    }
    
    private string MapDataTypeToCSharp(string dataType)
    {
        return dataType switch
        {
            "String" => "string",
            "Integer" => "int",
            "Boolean" => "bool",
            "DateTime" => "DateTime",
            "Decimal" => "decimal",
            "Text" => "string",
            "Guid" => "Guid",
            _ => "string"
        };
    }
    
    private bool IsValueType(string dataType)
    {
        return dataType is "Integer" or "Boolean" or "DateTime" or "Decimal" or "Guid";
    }
    
    private string SanitizeName(string name)
    {
        return string.IsNullOrWhiteSpace(name) ? "GeneratedProject" : name.Replace(" ", "");
    }
}

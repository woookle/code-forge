using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CodeForgeAPI.Data;
using CodeForgeAPI.Models;
using CodeForgeAPI.Services;
using CodeForgeAPI.DTOs.Projects;

namespace CodeForgeAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ICodeGeneratorService _codeGeneratorService;
    
    public ProjectsController(ApplicationDbContext context, ICodeGeneratorService codeGeneratorService)
    {
        _context = context;
        _codeGeneratorService = codeGeneratorService;
    }
    
    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(userIdClaim!.Value);
    }
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Project>>> GetProjects()
    {
        var userId = GetCurrentUserId();
        var projects = await _context.Projects
            .Where(p => p.UserId == userId)
            .Include(p => p.Entities)
            .ToListAsync();
        
        return Ok(projects);
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<Project>> GetProject(Guid id)
    {
        var userId = GetCurrentUserId();
        var project = await _context.Projects
            .Include(p => p.Entities)
                .ThenInclude(e => e.Fields)
            .Include(p => p.Entities)
                .ThenInclude(e => e.SourceRelationships)
                    .ThenInclude(r => r.TargetEntity)
            .Include(p => p.Entities)
                .ThenInclude(e => e.TargetRelationships)
                    .ThenInclude(r => r.SourceEntity)
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        
        if (project == null)
        {
            return NotFound();
        }
        
        return Ok(project);
    }
    
    [HttpPost]
    public async Task<ActionResult<Project>> CreateProject([FromBody] CreateProjectRequest request)
    {
        var userId = GetCurrentUserId();
        
        var project = new Project
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name,
            Description = request.Description,
            TargetStack = request.TargetStack,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();
        
        return CreatedAtAction(nameof(GetProject), new { id = project.Id }, project);
    }
    
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProject(Guid id, [FromBody] UpdateProjectRequest request)
    {
        var userId = GetCurrentUserId();
        
        var existingProject = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        
        if (existingProject == null)
        {
            return NotFound();
        }
        
        existingProject.Name = request.Name;
        existingProject.Description = request.Description;
        existingProject.TargetStack = request.TargetStack;
        existingProject.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
        return NoContent();
    }
    
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProject(Guid id)
    {
        var userId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");
        
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == id && (isAdmin || p.UserId == userId));
        
        if (project == null)
        {
            return NotFound();
        }
        
        _context.Projects.Remove(project);
        await _context.SaveChangesAsync();
        
        return NoContent();
    }
    
    [HttpPost("{id}/generate")]
    public async Task<IActionResult> GenerateProject(Guid id)
    {
        var userId = GetCurrentUserId();
        
        // Verify project belongs to current user
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        
        if (project == null)
        {
            return NotFound();
        }
        
        try
        {
            var zipBytes = await _codeGeneratorService.GenerateProjectZipAsync(id);
            var fileName = $"{project.Name.Replace(" ", "_")}.zip";
            
            return File(zipBytes, "application/zip", fileName);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Code generation failed: {ex.Message}" });
        }
    }
}

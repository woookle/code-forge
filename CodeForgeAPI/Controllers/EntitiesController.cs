using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CodeForgeAPI.Data;
using CodeForgeAPI.Models;
using CodeForgeAPI.DTOs.Entities;
using System.Security.Claims;
using CodeForgeAPI.Utilities;

namespace CodeForgeAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EntitiesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    
    public EntitiesController(ApplicationDbContext context)
    {
        _context = context;
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null && Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
    
    [HttpGet("project/{projectId}")]
    public async Task<ActionResult<IEnumerable<Entity>>> GetEntitiesByProject(Guid projectId)
    {
        var userId = GetCurrentUserId();
        var projectOwned = await _context.Projects
            .AnyAsync(p => p.Id == projectId && p.UserId == userId);
        if (!projectOwned) return NotFound();

        var entities = await _context.Entities
            .Where(e => e.ProjectId == projectId)
            .Include(e => e.Fields)
            .OrderBy(e => e.DisplayOrder)
            .ToListAsync();
        
        return Ok(entities);
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<Entity>> GetEntity(Guid id)
    {
        var userId = GetCurrentUserId();
        var entity = await _context.Entities
            .Include(e => e.Fields)
            .Include(e => e.Project)
            .FirstOrDefaultAsync(e => e.Id == id);
        
        if (entity == null || entity.Project.UserId != userId)
            return NotFound();
        
        return Ok(entity);
    }
    
    [HttpPost("project/{projectId}")]
    public async Task<ActionResult<Entity>> CreateEntity(Guid projectId, [FromBody] CreateEntityRequest request)
    {
        var userId = GetCurrentUserId();
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == projectId && p.UserId == userId);
        if (project == null)
            return NotFound("Project not found");
        
        if (!NameValidator.IsValidIdentifier(request.Name, project.TargetStack))
            return BadRequest($"Invalid entity name '{request.Name}'. Must be a valid identifier.");

        var duplicate = await _context.Entities
            .AnyAsync(e => e.ProjectId == projectId && e.Name == request.Name);
        if (duplicate)
            return Conflict($"An entity named '{request.Name}' already exists in this project.");
        
        var entity = new Entity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = request.Name,
            Description = request.Description,
            DisplayOrder = request.DisplayOrder,
            ServiceName = string.IsNullOrWhiteSpace(request.ServiceName) ? null : request.ServiceName.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        
        _context.Entities.Add(entity);
        await _context.SaveChangesAsync();
        
        return CreatedAtAction(nameof(GetEntity), new { id = entity.Id }, entity);
    }
    
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEntity(Guid id, [FromBody] UpdateEntityRequest request)
    {
        var userId = GetCurrentUserId();
        var existingEntity = await _context.Entities
            .Include(e => e.Project)
            .FirstOrDefaultAsync(e => e.Id == id);
        
        if (existingEntity == null || existingEntity.Project.UserId != userId)
            return NotFound();
        
        if (!NameValidator.IsValidIdentifier(request.Name, existingEntity.Project.TargetStack))
            return BadRequest($"Invalid entity name '{request.Name}'. Must be a valid identifier.");

        if (existingEntity.Name != request.Name)
        {
            var duplicate = await _context.Entities
                .AnyAsync(e => e.ProjectId == existingEntity.ProjectId && e.Name == request.Name && e.Id != id);
            if (duplicate)
                return Conflict($"An entity named '{request.Name}' already exists in this project.");
        }
        
        existingEntity.Name = request.Name;
        existingEntity.Description = request.Description;
        existingEntity.DisplayOrder = request.DisplayOrder;
        existingEntity.ServiceName = string.IsNullOrWhiteSpace(request.ServiceName) ? null : request.ServiceName.Trim();
        
        await _context.SaveChangesAsync();
        
        return NoContent();
    }
    
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEntity(Guid id)
    {
        var userId = GetCurrentUserId();
        var entity = await _context.Entities
            .Include(e => e.Project)
            .FirstOrDefaultAsync(e => e.Id == id);
        
        if (entity == null || entity.Project.UserId != userId)
            return NotFound();
        
        // Remove Relationship rows where this entity is source or target
        var relationships = await _context.Relationships
            .Where(r => r.SourceEntityId == id || r.TargetEntityId == id)
            .ToListAsync();
        
        if (relationships.Any())
            _context.Relationships.RemoveRange(relationships);

        // Null out FK references in other entities' fields (SetNull — don't delete sibling fields)
        var referencingFields = await _context.Fields
            .Where(f => f.RelatedEntityId == id)
            .ToListAsync();
            
        foreach (var field in referencingFields)
        {
            field.RelatedEntityId = null;
            field.RelationshipType = null;
        }

        _context.Entities.Remove(entity);
        await _context.SaveChangesAsync();
        
        return NoContent();
    }
}

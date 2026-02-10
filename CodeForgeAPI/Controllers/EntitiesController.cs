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
    
    [HttpGet("project/{projectId}")]
    public async Task<ActionResult<IEnumerable<Entity>>> GetEntitiesByProject(Guid projectId)
    {
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
        var entity = await _context.Entities
            .Include(e => e.Fields)
            .FirstOrDefaultAsync(e => e.Id == id);
        
        if (entity == null)
        {
            return NotFound();
        }
        
        return Ok(entity);
    }
    
    [HttpPost("project/{projectId}")]
    public async Task<ActionResult<Entity>> CreateEntity(Guid projectId, [FromBody] CreateEntityRequest request)
    {
        // Validate entity name
        var project = await _context.Projects.FindAsync(projectId);
        if (project == null)
        {
            return NotFound("Project not found");
        }
        
        if (!NameValidator.IsValidIdentifier(request.Name, project.TargetStack))
        {
            return BadRequest($"Invalid entity name '{request.Name}'. Must be a valid identifier.");
        }
        
        var entity = new Entity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = request.Name,
            Description = request.Description,
            DisplayOrder = request.DisplayOrder,
            CreatedAt = DateTime.UtcNow
        };
        
        _context.Entities.Add(entity);
        await _context.SaveChangesAsync();
        
        return CreatedAtAction(nameof(GetEntity), new { id = entity.Id }, entity);
    }
    
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEntity(Guid id, [FromBody] UpdateEntityRequest request)
    {
        var existingEntity = await _context.Entities
            .Include(e => e.Project)
            .FirstOrDefaultAsync(e => e.Id == id);
        
        if (existingEntity == null)
        {
            return NotFound();
        }
        
        if (!NameValidator.IsValidIdentifier(request.Name, existingEntity.Project.TargetStack))
        {
            return BadRequest($"Invalid entity name '{request.Name}'. Must be a valid identifier.");
        }
        
        existingEntity.Name = request.Name;
        existingEntity.Description = request.Description;
        existingEntity.DisplayOrder = request.DisplayOrder;
        
        await _context.SaveChangesAsync();
        
        return NoContent();
    }
    
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEntity(Guid id)
    {
        var entity = await _context.Entities.FindAsync(id);
        
        if (entity == null)
        {
            return NotFound();
        }
        
        // Find and delete fields in other entities that reference this entity
        var referencingFields = await _context.Fields
            .Where(f => f.RelatedEntityId == id)
            .ToListAsync();
            
        if (referencingFields.Any())
        {
            _context.Fields.RemoveRange(referencingFields);
        }

        _context.Entities.Remove(entity);
        await _context.SaveChangesAsync();
        
        return NoContent();
    }
}

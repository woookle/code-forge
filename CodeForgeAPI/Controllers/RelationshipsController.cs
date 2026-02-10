using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CodeForgeAPI.Data;
using CodeForgeAPI.Models;
using CodeForgeAPI.DTOs.Relationships;
using System.Security.Claims;

namespace CodeForgeAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RelationshipsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    
    public RelationshipsController(ApplicationDbContext context)
    {
        _context = context;
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<Relationship>> GetRelationship(Guid id)
    {
        var relationship = await _context.Relationships
            .Include(r => r.SourceEntity)
            .Include(r => r.TargetEntity)
            .FirstOrDefaultAsync(r => r.Id == id);
            
        if (relationship == null)
        {
            return NotFound();
        }
        
        return Ok(relationship);
    }
    
    [HttpPost]
    public async Task<ActionResult<Relationship>> CreateRelationship([FromBody] CreateRelationshipRequest request)
    {
        // Validate entities exist
        var sourceEntity = await _context.Entities.FindAsync(request.SourceEntityId);
        var targetEntity = await _context.Entities.FindAsync(request.TargetEntityId);
        
        if (sourceEntity == null || targetEntity == null)

        {
            return BadRequest("Source or Target Entity not found.");
        }
        
        // Validate they belong to the same project (usually required, though cross-project could be interesting, sticking to same project for now)
        if (sourceEntity.ProjectId != targetEntity.ProjectId)
        {
            return BadRequest("Entities must belong to the same project.");
        }
        
        // Generate default field names if not provided
        var sourceFieldName = !string.IsNullOrEmpty(request.SourceFieldName) 
            ? request.SourceFieldName 
            : $"{targetEntity.Name}Id"; // e.g., "ProductId" in Order entity
            
        var targetFieldName = !string.IsNullOrEmpty(request.TargetFieldName)
            ? request.TargetFieldName
            : $"{sourceEntity.Name}s"; // e.g., "Orders" in Product entity
            
        // Adjust for OneToOne
        if (request.RelationshipType == "OneToOne")
        {
             targetFieldName = !string.IsNullOrEmpty(request.TargetFieldName)
                ? request.TargetFieldName
                : $"{sourceEntity.Name}";
        }

        var relationship = new Relationship
        {
            Id = Guid.NewGuid(),
            SourceEntityId = request.SourceEntityId,
            TargetEntityId = request.TargetEntityId,
            RelationshipType = request.RelationshipType,
            SourceFieldName = sourceFieldName,
            TargetFieldName = targetFieldName,
            CreatedAt = DateTime.UtcNow
        };
        
        _context.Relationships.Add(relationship);
        await _context.SaveChangesAsync();
        
        return CreatedAtAction(nameof(GetRelationship), new { id = relationship.Id }, relationship);
    }
    
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateRelationship(Guid id, [FromBody] UpdateRelationshipRequest request)
    {
        var relationship = await _context.Relationships.FindAsync(id);
        
        if (relationship == null)
        {
            return NotFound();
        }
        
        relationship.RelationshipType = request.RelationshipType;
        
        if (!string.IsNullOrEmpty(request.SourceFieldName))
            relationship.SourceFieldName = request.SourceFieldName;
            
        if (!string.IsNullOrEmpty(request.TargetFieldName))
            relationship.TargetFieldName = request.TargetFieldName;
            
        await _context.SaveChangesAsync();
        
        return NoContent();
    }
    
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRelationship(Guid id)
    {
        var relationship = await _context.Relationships.FindAsync(id);
        
        if (relationship == null)
        {
            return NotFound();
        }
        
        _context.Relationships.Remove(relationship);
        await _context.SaveChangesAsync();
        
        return NoContent();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CodeForgeAPI.Data;
using CodeForgeAPI.Models;
using CodeForgeAPI.DTOs.Fields;
using System.Security.Claims;
using CodeForgeAPI.Utilities;

namespace CodeForgeAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class FieldsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    
    public FieldsController(ApplicationDbContext context)
    {
        _context = context;
    }
    
    [HttpGet("entity/{entityId}")]
    public async Task<ActionResult<IEnumerable<Field>>> GetFieldsByEntity(Guid entityId)
    {
        var fields = await _context.Fields
            .Where(f => f.EntityId == entityId)
            .OrderBy(f => f.DisplayOrder)
            .ToListAsync();
        
        return Ok(fields);
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<Field>> GetField(Guid id)
    {
        var field = await _context.Fields.FindAsync(id);
        
        if (field == null)
        {
            return NotFound();
        }
        
        return Ok(field);
    }
    
    [HttpPost("entity/{entityId}")]
    public async Task<ActionResult<Field>> CreateField(Guid entityId, [FromBody] CreateFieldRequest request)
    {
        var entity = await _context.Entities
            .Include(e => e.Project)
            .FirstOrDefaultAsync(e => e.Id == entityId);
        
        if (entity == null)
        {
            return NotFound("Entity not found");
        }
        
        // Validate field name
        if (!NameValidator.IsValidIdentifier(request.Name, entity.Project.TargetStack))
        {
            return BadRequest($"Invalid field name '{request.Name}'. Must be a valid identifier.");
        }
        
        var field = new Field
        {
            Id = Guid.NewGuid(),
            EntityId = entityId,
            Name = request.Name,
            DataType = request.DataType,
            IsRequired = request.IsRequired,
            IsUnique = request.IsUnique,
            IsPrimaryKey = request.IsPrimaryKey,
            DisplayOrder = request.DisplayOrder,
            RelatedEntityId = request.RelatedEntityId,
            RelationshipType = request.RelationshipType,
            CreatedAt = DateTime.UtcNow
        };
        
        _context.Fields.Add(field);
        await _context.SaveChangesAsync();
        
        return CreatedAtAction(nameof(GetField), new { id = field.Id }, field);
    }
    
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateField(Guid id, [FromBody] UpdateFieldRequest request)
    {
        var existingField = await _context.Fields
            .Include(f => f.Entity)
                .ThenInclude(e => e.Project)
            .FirstOrDefaultAsync(f => f.Id == id);
        
        if (existingField == null)
        {
            return NotFound();
        }
        
        if (!NameValidator.IsValidIdentifier(request.Name, existingField.Entity.Project.TargetStack))
        {
            return BadRequest($"Invalid field name '{request.Name}'. Must be a valid identifier.");
        }
        
        existingField.Name = request.Name;
        existingField.DataType = request.DataType;
        existingField.IsRequired = request.IsRequired;
        existingField.IsUnique = request.IsUnique;
        existingField.IsPrimaryKey = request.IsPrimaryKey;
        existingField.DisplayOrder = request.DisplayOrder;
        existingField.RelatedEntityId = request.RelatedEntityId;
        existingField.RelationshipType = request.RelationshipType;
        
        await _context.SaveChangesAsync();
        
        return NoContent();
    }
    
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteField(Guid id)
    {
        var field = await _context.Fields.FindAsync(id);
        
        if (field == null)
        {
            return NotFound();
        }
        
        _context.Fields.Remove(field);
        await _context.SaveChangesAsync();
        
        return NoContent();
    }
}

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

        await SyncRelationshipForFieldAsync(field);

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

        var previousFieldName = existingField.Name;

        existingField.Name = request.Name;
        existingField.DataType = request.DataType;
        existingField.IsRequired = request.IsRequired;
        existingField.IsUnique = request.IsUnique;
        existingField.IsPrimaryKey = request.IsPrimaryKey;
        existingField.DisplayOrder = request.DisplayOrder;
        existingField.RelatedEntityId = request.RelatedEntityId;
        existingField.RelationshipType = request.RelationshipType;

        await SyncRelationshipForFieldAsync(existingField, previousFieldName);
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

        await RemoveRelationshipForFieldAsync(field);
        _context.Fields.Remove(field);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private async Task SyncRelationshipForFieldAsync(Field field, string? previousFieldName = null)
    {
        var lookupName = previousFieldName ?? field.Name;
        var existing = await _context.Relationships
            .FirstOrDefaultAsync(r => r.SourceEntityId == field.EntityId && r.SourceFieldName == lookupName);

        if (field.DataType != "Relationship" || !field.RelatedEntityId.HasValue)
        {
            if (existing != null)
                _context.Relationships.Remove(existing);
            return;
        }

        var sourceEntity = await _context.Entities.FindAsync(field.EntityId);
        var targetEntity = await _context.Entities.FindAsync(field.RelatedEntityId.Value);
        if (sourceEntity == null || targetEntity == null)
            return;

        var targetFieldName = field.RelationshipType == "OneToOne"
            ? sourceEntity.Name
            : $"{sourceEntity.Name}s";

        if (existing != null)
        {
            existing.TargetEntityId = field.RelatedEntityId.Value;
            existing.RelationshipType = field.RelationshipType ?? "OneToMany";
            existing.SourceFieldName = field.Name;
            existing.TargetFieldName = targetFieldName;
            return;
        }

        _context.Relationships.Add(new Relationship
        {
            Id = Guid.NewGuid(),
            SourceEntityId = field.EntityId,
            TargetEntityId = field.RelatedEntityId.Value,
            RelationshipType = field.RelationshipType ?? "OneToMany",
            SourceFieldName = field.Name,
            TargetFieldName = targetFieldName,
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task RemoveRelationshipForFieldAsync(Field field)
    {
        if (field.DataType != "Relationship") return;

        var existing = await _context.Relationships
            .FirstOrDefaultAsync(r => r.SourceEntityId == field.EntityId && r.SourceFieldName == field.Name);

        if (existing != null)
            _context.Relationships.Remove(existing);
    }
}

import { Entity } from '../types';

export interface OutgoingRelationship {
    /** ID записи в таблице Relationships (если есть) */
    id?: string;
    /** ID поля-связи (если связь только через Field) */
    fieldId?: string;
    relationshipType: string;
    targetEntityName: string;
    sourceFieldName: string;
}

export function formatRelationshipType(type: string): string {
    switch (type) {
        case 'ManyToMany': return 'M:N';
        case 'OneToOne': return '1:1';
        case 'OneToMany':
        default: return '1:N';
    }
}

/** Исходящие связи сущности: из таблицы Relationships и из полей типа Relationship */
export function getOutgoingRelationships(entity: Entity, allEntities: Entity[]): OutgoingRelationship[] {
    const result: OutgoingRelationship[] = [];
    const seen = new Set<string>();

    for (const rel of entity.sourceRelationships ?? []) {
        const key = `${rel.targetEntityId}:${rel.sourceFieldName}`;
        if (seen.has(key)) continue;
        seen.add(key);
        result.push({
            id: rel.id,
            relationshipType: rel.relationshipType,
            targetEntityName:
                rel.targetEntity?.name
                ?? allEntities.find(e => e.id === rel.targetEntityId)?.name
                ?? 'Неизвестно',
            sourceFieldName: rel.sourceFieldName,
        });
    }

    for (const field of entity.fields ?? []) {
        if (field.dataType !== 'Relationship' || !field.relatedEntityId) continue;
        const key = `${field.relatedEntityId}:${field.name}`;
        if (seen.has(key)) continue;
        seen.add(key);

        const existingRel = (entity.sourceRelationships ?? []).find(
            r => r.targetEntityId === field.relatedEntityId && r.sourceFieldName === field.name
        );

        result.push({
            id: existingRel?.id,
            fieldId: field.id,
            relationshipType: field.relationshipType ?? 'OneToMany',
            targetEntityName:
                allEntities.find(e => e.id === field.relatedEntityId)?.name ?? 'Неизвестно',
            sourceFieldName: field.name,
        });
    }

    return result;
}

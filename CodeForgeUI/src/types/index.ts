export type DataType =
    | 'String'
    | 'Integer'
    | 'Boolean'
    | 'DateTime'
    | 'Decimal'
    | 'Text'
    | 'File'
    | 'Guid'
    | 'Relationship';

export type TargetStack = 'CSharp_PostgreSQL' | 'NodeJS_MongoDB';

export interface Project {
    id: string;
    userId: string;
    name: string;
    description?: string;
    targetStack: TargetStack;
    createdAt: string;
    updatedAt: string;
    entities?: Entity[];
}

export interface Relationship {
    id: string;
    sourceEntityId: string;
    targetEntityId: string;
    relationshipType: 'OneToOne' | 'OneToMany' | 'ManyToMany';
    sourceFieldName: string;
    targetFieldName: string;
    sourceEntity?: Entity;
    targetEntity?: Entity;
}

export interface Entity {
    id: string;
    projectId: string;
    name: string;
    description?: string;
    displayOrder: number;
    createdAt: string;
    fields?: Field[];
    sourceRelationships?: Relationship[];
    targetRelationships?: Relationship[];
}

export interface Field {
    id: string;
    entityId: string;
    name: string;
    dataType: DataType;
    isRequired: boolean;
    isUnique: boolean;
    isPrimaryKey: boolean;
    displayOrder: number;
    relatedEntityId?: string;
    relationshipType?: 'OneToMany' | 'ManyToMany';
    createdAt: string;
}

export type DataType =
    | 'String'
    | 'Integer'
    | 'Float'
    | 'Long'
    | 'Decimal'
    | 'Boolean'
    | 'DateTime'
    | 'Text'
    | 'Guid'
    | 'Relationship';

export type TargetStack = 'CSharp_PostgreSQL' | 'NodeJS_MongoDB';

export type ArchitectureType = 'Monolith' | 'Microservices';

export interface EntityProtectionMethods {
    get: boolean;
    post: boolean;
    put: boolean;
    patch: boolean;
    delete: boolean;
}

export interface AuthConfig {
    enabled: boolean;
    type?: string;
    userIdentifier: 'email' | 'username' | 'both';
    enableRoles: boolean;
    roles: string[];
    enableRefreshTokens: boolean;
    enableEmailVerification: boolean;
    tokenExpiryMinutes: number;
    refreshTokenExpiryDays: number;
    /** Per-entity HTTP method protection map: entityName -> method flags */
    entityProtection: Record<string, EntityProtectionMethods>;
}

export interface Project {
    id: string;
    userId: string;
    name: string;
    description?: string;
    targetStack: TargetStack;
    architectureType: ArchitectureType;
    authConfig?: string | null;
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
    serviceName?: string | null;
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

import { DataType, TargetStack, ArchitectureType } from '../types';

export interface TemplateField {
    name: string;
    dataType: DataType;
    isRequired: boolean;
    isUnique: boolean;
    /** Имя связанной сущности (для dataType === 'Relationship') */
    relatedEntityName?: string;
    relationshipType?: 'OneToOne' | 'OneToMany' | 'ManyToMany';
}

export interface TemplateEntity {
    name: string;
    fields: TemplateField[];
}

export interface ProjectTemplate {
    id: string;
    name: string;
    /** English name for the generated project (Docker, folders, namespaces) */
    projectName: string;
    description: string;
    icon: string;
    color: string;
    tags: string[];
    entities: TemplateEntity[];
    defaultStack: TargetStack;
    defaultArch: ArchitectureType;
}

export function countTemplateRelationships(template: ProjectTemplate): number {
    return template.entities.reduce(
        (sum, entity) => sum + entity.fields.filter(f => f.dataType === 'Relationship').length,
        0
    );
}

export const PROJECT_TEMPLATES: ProjectTemplate[] = [
    {
        id: 'shop',
        name: 'Интернет-магазин',
        projectName: 'OnlineShop',
        description: 'Товары, категории, заказы, покупатели и корзина',
        icon: '🛒',
        color: '#10b981',
        tags: ['E-commerce', 'Продажи'],
        defaultStack: 'NodeJS_MongoDB',
        defaultArch: 'Monolith',
        entities: [
            {
                name: 'Category',
                fields: [
                    { name: 'name', dataType: 'String', isRequired: true, isUnique: true },
                    { name: 'description', dataType: 'Text', isRequired: false, isUnique: false },
                    { name: 'slug', dataType: 'String', isRequired: true, isUnique: true },
                ],
            },
            {
                name: 'Product',
                fields: [
                    { name: 'name', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'description', dataType: 'Text', isRequired: false, isUnique: false },
                    { name: 'price', dataType: 'Decimal', isRequired: true, isUnique: false },
                    { name: 'stock', dataType: 'Integer', isRequired: true, isUnique: false },
                    { name: 'imageUrl', dataType: 'String', isRequired: false, isUnique: false },
                    { name: 'isActive', dataType: 'Boolean', isRequired: true, isUnique: false },
                    { name: 'categoryId', dataType: 'Relationship', isRequired: true, isUnique: false, relatedEntityName: 'Category', relationshipType: 'OneToMany' },
                ],
            },
            {
                name: 'Customer',
                fields: [
                    { name: 'firstName', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'lastName', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'email', dataType: 'String', isRequired: true, isUnique: true },
                    { name: 'phone', dataType: 'String', isRequired: false, isUnique: false },
                    { name: 'address', dataType: 'Text', isRequired: false, isUnique: false },
                ],
            },
            {
                name: 'Order',
                fields: [
                    { name: 'status', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'totalAmount', dataType: 'Decimal', isRequired: true, isUnique: false },
                    { name: 'shippingAddress', dataType: 'Text', isRequired: false, isUnique: false },
                    { name: 'createdAt', dataType: 'DateTime', isRequired: true, isUnique: false },
                    { name: 'customerId', dataType: 'Relationship', isRequired: true, isUnique: false, relatedEntityName: 'Customer', relationshipType: 'OneToMany' },
                ],
            },
            {
                name: 'OrderItem',
                fields: [
                    { name: 'quantity', dataType: 'Integer', isRequired: true, isUnique: false },
                    { name: 'unitPrice', dataType: 'Decimal', isRequired: true, isUnique: false },
                    { name: 'orderId', dataType: 'Relationship', isRequired: true, isUnique: false, relatedEntityName: 'Order', relationshipType: 'OneToMany' },
                    { name: 'productId', dataType: 'Relationship', isRequired: true, isUnique: false, relatedEntityName: 'Product', relationshipType: 'OneToMany' },
                ],
            },
        ],
    },
    {
        id: 'blog',
        name: 'Блог / CMS',
        projectName: 'BlogCMS',
        description: 'Статьи, категории, теги и комментарии',
        icon: '📝',
        color: '#6366f1',
        tags: ['Контент', 'Медиа'],
        defaultStack: 'NodeJS_MongoDB',
        defaultArch: 'Monolith',
        entities: [
            {
                name: 'Category',
                fields: [
                    { name: 'name', dataType: 'String', isRequired: true, isUnique: true },
                    { name: 'slug', dataType: 'String', isRequired: true, isUnique: true },
                    { name: 'description', dataType: 'Text', isRequired: false, isUnique: false },
                ],
            },
            {
                name: 'Tag',
                fields: [
                    { name: 'name', dataType: 'String', isRequired: true, isUnique: true },
                    { name: 'slug', dataType: 'String', isRequired: true, isUnique: true },
                ],
            },
            {
                name: 'Post',
                fields: [
                    { name: 'title', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'slug', dataType: 'String', isRequired: true, isUnique: true },
                    { name: 'content', dataType: 'Text', isRequired: true, isUnique: false },
                    { name: 'excerpt', dataType: 'Text', isRequired: false, isUnique: false },
                    { name: 'coverImageUrl', dataType: 'String', isRequired: false, isUnique: false },
                    { name: 'publishedAt', dataType: 'DateTime', isRequired: false, isUnique: false },
                    { name: 'isPublished', dataType: 'Boolean', isRequired: true, isUnique: false },
                    { name: 'viewCount', dataType: 'Integer', isRequired: true, isUnique: false },
                    { name: 'categoryId', dataType: 'Relationship', isRequired: true, isUnique: false, relatedEntityName: 'Category', relationshipType: 'OneToMany' },
                    { name: 'tags', dataType: 'Relationship', isRequired: false, isUnique: false, relatedEntityName: 'Tag', relationshipType: 'ManyToMany' },
                ],
            },
            {
                name: 'Comment',
                fields: [
                    { name: 'content', dataType: 'Text', isRequired: true, isUnique: false },
                    { name: 'authorName', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'authorEmail', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'isApproved', dataType: 'Boolean', isRequired: true, isUnique: false },
                    { name: 'createdAt', dataType: 'DateTime', isRequired: true, isUnique: false },
                    { name: 'postId', dataType: 'Relationship', isRequired: true, isUnique: false, relatedEntityName: 'Post', relationshipType: 'OneToMany' },
                ],
            },
        ],
    },
    {
        id: 'crm',
        name: 'CRM-система',
        projectName: 'CRMSystem',
        description: 'Контакты, компании, сделки и задачи',
        icon: '👤',
        color: '#f59e0b',
        tags: ['Бизнес', 'Продажи'],
        defaultStack: 'CSharp_PostgreSQL',
        defaultArch: 'Monolith',
        entities: [
            {
                name: 'Company',
                fields: [
                    { name: 'name', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'website', dataType: 'String', isRequired: false, isUnique: false },
                    { name: 'industry', dataType: 'String', isRequired: false, isUnique: false },
                    { name: 'size', dataType: 'String', isRequired: false, isUnique: false },
                    { name: 'country', dataType: 'String', isRequired: false, isUnique: false },
                ],
            },
            {
                name: 'Contact',
                fields: [
                    { name: 'firstName', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'lastName', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'email', dataType: 'String', isRequired: true, isUnique: true },
                    { name: 'phone', dataType: 'String', isRequired: false, isUnique: false },
                    { name: 'position', dataType: 'String', isRequired: false, isUnique: false },
                    { name: 'companyId', dataType: 'Relationship', isRequired: true, isUnique: false, relatedEntityName: 'Company', relationshipType: 'OneToMany' },
                ],
            },
            {
                name: 'Deal',
                fields: [
                    { name: 'title', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'value', dataType: 'Decimal', isRequired: false, isUnique: false },
                    { name: 'stage', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'probability', dataType: 'Integer', isRequired: false, isUnique: false },
                    { name: 'closedAt', dataType: 'DateTime', isRequired: false, isUnique: false },
                    { name: 'companyId', dataType: 'Relationship', isRequired: true, isUnique: false, relatedEntityName: 'Company', relationshipType: 'OneToMany' },
                    { name: 'contactId', dataType: 'Relationship', isRequired: false, isUnique: false, relatedEntityName: 'Contact', relationshipType: 'OneToMany' },
                ],
            },
            {
                name: 'Task',
                fields: [
                    { name: 'title', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'description', dataType: 'Text', isRequired: false, isUnique: false },
                    { name: 'dueDate', dataType: 'DateTime', isRequired: false, isUnique: false },
                    { name: 'priority', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'isCompleted', dataType: 'Boolean', isRequired: true, isUnique: false },
                    { name: 'contactId', dataType: 'Relationship', isRequired: false, isUnique: false, relatedEntityName: 'Contact', relationshipType: 'OneToMany' },
                    { name: 'dealId', dataType: 'Relationship', isRequired: false, isUnique: false, relatedEntityName: 'Deal', relationshipType: 'OneToMany' },
                ],
            },
        ],
    },
    {
        id: 'booking',
        name: 'Система бронирования',
        projectName: 'BookingSystem',
        description: 'Услуги, клиенты, временные слоты и записи',
        icon: '📅',
        color: '#3b82f6',
        tags: ['Сервис', 'Расписание'],
        defaultStack: 'CSharp_PostgreSQL',
        defaultArch: 'Monolith',
        entities: [
            {
                name: 'Service',
                fields: [
                    { name: 'name', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'description', dataType: 'Text', isRequired: false, isUnique: false },
                    { name: 'duration', dataType: 'Integer', isRequired: true, isUnique: false },
                    { name: 'price', dataType: 'Decimal', isRequired: true, isUnique: false },
                    { name: 'isActive', dataType: 'Boolean', isRequired: true, isUnique: false },
                ],
            },
            {
                name: 'Client',
                fields: [
                    { name: 'firstName', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'lastName', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'email', dataType: 'String', isRequired: true, isUnique: true },
                    { name: 'phone', dataType: 'String', isRequired: true, isUnique: false },
                ],
            },
            {
                name: 'TimeSlot',
                fields: [
                    { name: 'startTime', dataType: 'DateTime', isRequired: true, isUnique: false },
                    { name: 'endTime', dataType: 'DateTime', isRequired: true, isUnique: false },
                    { name: 'isAvailable', dataType: 'Boolean', isRequired: true, isUnique: false },
                    { name: 'serviceId', dataType: 'Relationship', isRequired: true, isUnique: false, relatedEntityName: 'Service', relationshipType: 'OneToMany' },
                ],
            },
            {
                name: 'Booking',
                fields: [
                    { name: 'startTime', dataType: 'DateTime', isRequired: true, isUnique: false },
                    { name: 'endTime', dataType: 'DateTime', isRequired: true, isUnique: false },
                    { name: 'status', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'notes', dataType: 'Text', isRequired: false, isUnique: false },
                    { name: 'createdAt', dataType: 'DateTime', isRequired: true, isUnique: false },
                    { name: 'clientId', dataType: 'Relationship', isRequired: true, isUnique: false, relatedEntityName: 'Client', relationshipType: 'OneToMany' },
                    { name: 'serviceId', dataType: 'Relationship', isRequired: true, isUnique: false, relatedEntityName: 'Service', relationshipType: 'OneToMany' },
                    { name: 'timeSlotId', dataType: 'Relationship', isRequired: false, isUnique: false, relatedEntityName: 'TimeSlot', relationshipType: 'OneToMany' },
                ],
            },
        ],
    },
    {
        id: 'library',
        name: 'Библиотека',
        projectName: 'LibrarySystem',
        description: 'Книги, авторы, читатели и выдача',
        icon: '📚',
        color: '#8b5cf6',
        tags: ['Образование', 'Каталог'],
        defaultStack: 'CSharp_PostgreSQL',
        defaultArch: 'Monolith',
        entities: [
            {
                name: 'Author',
                fields: [
                    { name: 'firstName', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'lastName', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'biography', dataType: 'Text', isRequired: false, isUnique: false },
                    { name: 'birthDate', dataType: 'DateTime', isRequired: false, isUnique: false },
                ],
            },
            {
                name: 'Category',
                fields: [
                    { name: 'name', dataType: 'String', isRequired: true, isUnique: true },
                ],
            },
            {
                name: 'Book',
                fields: [
                    { name: 'title', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'isbn', dataType: 'String', isRequired: true, isUnique: true },
                    { name: 'description', dataType: 'Text', isRequired: false, isUnique: false },
                    { name: 'publishedYear', dataType: 'Integer', isRequired: false, isUnique: false },
                    { name: 'totalCopies', dataType: 'Integer', isRequired: true, isUnique: false },
                    { name: 'availableCopies', dataType: 'Integer', isRequired: true, isUnique: false },
                    { name: 'authorId', dataType: 'Relationship', isRequired: true, isUnique: false, relatedEntityName: 'Author', relationshipType: 'OneToMany' },
                    { name: 'categoryId', dataType: 'Relationship', isRequired: true, isUnique: false, relatedEntityName: 'Category', relationshipType: 'OneToMany' },
                ],
            },
            {
                name: 'Member',
                fields: [
                    { name: 'firstName', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'lastName', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'email', dataType: 'String', isRequired: true, isUnique: true },
                    { name: 'membershipDate', dataType: 'DateTime', isRequired: true, isUnique: false },
                    { name: 'expiryDate', dataType: 'DateTime', isRequired: true, isUnique: false },
                ],
            },
            {
                name: 'Loan',
                fields: [
                    { name: 'borrowedAt', dataType: 'DateTime', isRequired: true, isUnique: false },
                    { name: 'dueDate', dataType: 'DateTime', isRequired: true, isUnique: false },
                    { name: 'returnedAt', dataType: 'DateTime', isRequired: false, isUnique: false },
                    { name: 'isReturned', dataType: 'Boolean', isRequired: true, isUnique: false },
                    { name: 'bookId', dataType: 'Relationship', isRequired: true, isUnique: false, relatedEntityName: 'Book', relationshipType: 'OneToMany' },
                    { name: 'memberId', dataType: 'Relationship', isRequired: true, isUnique: false, relatedEntityName: 'Member', relationshipType: 'OneToMany' },
                ],
            },
        ],
    },
    {
        id: 'lms',
        name: 'Учебная платформа',
        projectName: 'LearningPlatform',
        description: 'Курсы, уроки, студенты и прогресс обучения',
        icon: '🎓',
        color: '#ec4899',
        tags: ['EdTech', 'Образование'],
        defaultStack: 'NodeJS_MongoDB',
        defaultArch: 'Monolith',
        entities: [
            {
                name: 'Course',
                fields: [
                    { name: 'title', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'description', dataType: 'Text', isRequired: false, isUnique: false },
                    { name: 'price', dataType: 'Decimal', isRequired: true, isUnique: false },
                    { name: 'level', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'isPublished', dataType: 'Boolean', isRequired: true, isUnique: false },
                    { name: 'thumbnailUrl', dataType: 'String', isRequired: false, isUnique: false },
                ],
            },
            {
                name: 'Lesson',
                fields: [
                    { name: 'title', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'content', dataType: 'Text', isRequired: false, isUnique: false },
                    { name: 'videoUrl', dataType: 'String', isRequired: false, isUnique: false },
                    { name: 'duration', dataType: 'Integer', isRequired: false, isUnique: false },
                    { name: 'orderIndex', dataType: 'Integer', isRequired: true, isUnique: false },
                    { name: 'isFree', dataType: 'Boolean', isRequired: true, isUnique: false },
                    { name: 'courseId', dataType: 'Relationship', isRequired: true, isUnique: false, relatedEntityName: 'Course', relationshipType: 'OneToMany' },
                ],
            },
            {
                name: 'Enrollment',
                fields: [
                    { name: 'enrolledAt', dataType: 'DateTime', isRequired: true, isUnique: false },
                    { name: 'completedAt', dataType: 'DateTime', isRequired: false, isUnique: false },
                    { name: 'progress', dataType: 'Integer', isRequired: true, isUnique: false },
                    { name: 'courseId', dataType: 'Relationship', isRequired: true, isUnique: false, relatedEntityName: 'Course', relationshipType: 'OneToMany' },
                ],
            },
            {
                name: 'Quiz',
                fields: [
                    { name: 'title', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'question', dataType: 'Text', isRequired: true, isUnique: false },
                    { name: 'options', dataType: 'Text', isRequired: true, isUnique: false },
                    { name: 'correctAnswer', dataType: 'String', isRequired: true, isUnique: false },
                    { name: 'points', dataType: 'Integer', isRequired: true, isUnique: false },
                    { name: 'lessonId', dataType: 'Relationship', isRequired: true, isUnique: false, relatedEntityName: 'Lesson', relationshipType: 'OneToMany' },
                ],
            },
        ],
    },
];

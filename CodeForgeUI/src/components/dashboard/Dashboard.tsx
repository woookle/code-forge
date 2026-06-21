import { useEffect, useState, useRef, useCallback } from 'react';
import { useAppDispatch, useAppSelector } from '../../app/hooks';
import { fetchProjects, fetchProjectById, createProject, deleteProject, generateProject, updateProjectAuth } from '../../features/projects/projectsSlice';
import { logout, updateUser } from '../../features/auth/authSlice';
import { Project, Entity, Field, AuthConfig } from '../../types';
import api from '../../utils/api';
import { Page } from '../../App';
import { toast } from 'react-toastify';
import { useConfirm } from '../../context/ConfirmContext';
import FAQWidget from './FAQWidget';
import MicroservicesPreview from './MicroservicesPreview';
import MonolithPreview from './MonolithPreview';
import ProjectTemplatesModal from './ProjectTemplatesModal';
import ERDiagram from './ERDiagram';
import GenerationHistoryModal from './GenerationHistoryModal';
import { ProjectTemplate } from '../../data/projectTemplates';
import { getOutgoingRelationships, formatRelationshipType, OutgoingRelationship } from '../../utils/entityRelationships';
import { showAchievements } from '../common/AchievementToast';
import {
    DndContext,
    closestCenter,
    KeyboardSensor,
    PointerSensor,
    useSensor,
    useSensors,
    DragEndEvent,
} from '@dnd-kit/core';
import {
    SortableContext,
    sortableKeyboardCoordinates,
    verticalListSortingStrategy,
    useSortable,
    arrayMove,
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';

// Обёртка для перетаскивания сущностей
function SortableEntityCard({ id, children }: { id: string; children: React.ReactNode }) {
    const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id });
    return (
        <div
            ref={setNodeRef}
            style={{
                transform: CSS.Transform.toString(transform),
                transition,
                opacity: isDragging ? 0.5 : 1,
                zIndex: isDragging ? 10 : undefined,
            }}
            {...attributes}
            {...listeners}
        >
            {children}
        </div>
    );
}

interface DashboardProps {
    onNavigate: (page: Page) => void;
}

function Dashboard({ onNavigate }: DashboardProps) {
    const dispatch = useAppDispatch();
    const { projects, currentProject } = useAppSelector((state) => state.projects);
    const { user } = useAppSelector((state) => state.auth);
    const { confirm } = useConfirm();
    const [showNewProject, setShowNewProject] = useState(false);
    const [showNewEntity, setShowNewEntity] = useState(false);
    const [showFieldModal, setShowFieldModal] = useState(false);
    const [editingField, setEditingField] = useState<Field | null>(null);
    const [showAuthModal, setShowAuthModal] = useState(false);

    const [selectedEntityId, setSelectedEntityId] = useState<string | null>(null);
    const [isSidebarOpen, setIsSidebarOpen] = useState(false);

    // Шаблоны / экспорт / ER / история
    const [showTemplates, setShowTemplates] = useState(false);
    const [showERDiagram, setShowERDiagram] = useState(false);
    const [showHistory, setShowHistory] = useState(false);
    const importFileRef = useRef<HTMLInputElement>(null);

    // Локальный порядок сущностей для DnD
    const [entityOrder, setEntityOrder] = useState<Entity[]>([]);

    const sensors = useSensors(
        useSensor(PointerSensor, { activationConstraint: { distance: 5 } }),
        useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates })
    );

    // Поиск и пагинация проектов
    const [projectSearch, setProjectSearch] = useState('');
    const [projectPage, setProjectPage] = useState(1);
    const PROJECT_PAGE_SIZE = 5;

    const filteredProjects = projects.filter(p =>
        p.name.toLowerCase().includes(projectSearch.toLowerCase())
    );
    const totalProjectPages = Math.ceil(filteredProjects.length / PROJECT_PAGE_SIZE);
    const pagedProjects = filteredProjects.slice(
        (projectPage - 1) * PROJECT_PAGE_SIZE,
        projectPage * PROJECT_PAGE_SIZE
    );

    // При изменении поиска — сбросить на первую страницу
    useEffect(() => {
        setProjectPage(1);
    }, [projectSearch]);

    useEffect(() => {
        dispatch(fetchProjects());
    }, [dispatch]);

    // Закрываем боковую панель при выборе проекта (мобильный вид)
    const handleSelectProject = (project: Project) => {
        dispatch(fetchProjectById(project.id));
        setIsSidebarOpen(false);
    };

    const showProjectAchievements = (newAchievements: { id: string; icon: string; title: string; description: string; color: string }[]) => {
        if (newAchievements.length > 0) {
            showAchievements(newAchievements);
        }
    };

    const handleNewProject = async (name: string, targetStack: 'CSharp_PostgreSQL' | 'NodeJS_MongoDB', architectureType: 'Monolith' | 'Microservices') => {
        try {
            const result = await dispatch(createProject({ name, description: '', targetStack, architectureType })).unwrap();
            setShowNewProject(false);
            setProjectSearch('');
            setProjectPage(1);
            showProjectAchievements(result.newAchievements);
            toast.success('Проект успешно создан');
        } catch (error) {
            toast.error('Ошибка создания проекта');
        }
    };

    const handleDeleteProject = async (id: string) => {
        if (await confirm({
            title: 'Удаление проекта',
            message: 'Вы уверены, что хотите удалить этот проект? Это действие необратимо.',
            confirmText: 'Удалить',
            type: 'danger'
        })) {
            try {
                await dispatch(deleteProject(id)).unwrap();
                toast.success('Проект удален');
            } catch (error) {
                toast.error('Ошибка удаления проекта');
            }
        }
    };

    const handleNewEntity = async (name: string, serviceName?: string) => {
        if (!currentProject) return;

        try {
            await api.post(`/entities/project/${currentProject.id}`, {
                name,
                description: '',
                displayOrder: currentProject.entities?.length || 0,
                serviceName: serviceName || null
            });
            dispatch(fetchProjectById(currentProject.id));
            setShowNewEntity(false);
            toast.success('Сущность создана');
        } catch (error) {
            toast.error('Ошибка создания сущности');
        }
    };

    const handleChangeServiceName = async (entityId: string, currentServiceName: string | null | undefined, entityName: string) => {
        const current = currentServiceName || entityName;
        const newName = window.prompt(`Имя микросервиса для сущности (текущее: ${current}):`, current);
        if (newName === null) return; // отменено пользователем
        const trimmed = newName.trim();
        try {
            await api.put(`/entities/${entityId}`, {
                name: entityName,
                serviceName: trimmed || null
            });
            dispatch(fetchProjectById(currentProject!.id));
            toast.success('Имя сервиса обновлено');
        } catch {
            toast.error('Ошибка обновления');
        }
    };

    const handleDeleteEntity = async (entityId: string) => {
        if (!currentProject) return;

        if (await confirm({
            title: 'Удаление сущности',
            message: 'Вы уверены, что хотите удалить эту сущность?',
            confirmText: 'Удалить',
            type: 'danger'
        })) {
            try {
                await api.delete(`/entities/${entityId}`);
                dispatch(fetchProjectById(currentProject.id));
                toast.success('Сущность удалена');
            } catch (error) {
                toast.error('Ошибка удаления');
            }
        }
    };

    const handleSaveField = async (entityId: string, fieldData: Partial<Field>) => {
        try {
            if (editingField) {
                await api.put(`/fields/${editingField.id}`, fieldData);
                toast.success('Поле обновлено');
            } else {
                await api.post(`/fields/entity/${entityId}`, fieldData);
                toast.success('Поле создано');
            }

            if (currentProject) {
                dispatch(fetchProjectById(currentProject.id));
            }
            setShowFieldModal(false);
            setEditingField(null);
            setSelectedEntityId(null);
        } catch (error) {
            toast.error(editingField ? 'Ошибка обновления поля' : 'Ошибка создания поля');
        }
    };

    const handleDeleteField = async (fieldId: string) => {
        if (await confirm({
            title: 'Удаление поля',
            message: 'Вы уверены, что хотите удалить это поле?',
            confirmText: 'Удалить',
            type: 'danger'
        })) {
            try {
                await api.delete(`/fields/${fieldId}`);
                if (currentProject) {
                    dispatch(fetchProjectById(currentProject.id));
                }
                toast.success('Поле удалено');
            } catch (error) {
                toast.error('Ошибка удаления поля');
            }
        }
    };

    const handleDeleteOutgoingRelationship = async (entity: Entity, rel: OutgoingRelationship) => {
        if (await confirm({
            title: 'Удаление связи',
            message: 'Удалить связь и поле-связь? Это нельзя отменить.',
            confirmText: 'Удалить',
            type: 'danger'
        })) {
            try {
                if (rel.fieldId) {
                    await api.delete(`/fields/${rel.fieldId}`);
                } else {
                    if (rel.id) await api.delete(`/relationships/${rel.id}`);
                    const field = entity.fields?.find(
                        f => f.dataType === 'Relationship' && f.name === rel.sourceFieldName
                    );
                    if (field) await api.delete(`/fields/${field.id}`);
                }
                if (currentProject) {
                    dispatch(fetchProjectById(currentProject.id));
                }
                toast.success('Связь удалена');
            } catch (error) {
                toast.error('Ошибка удаления связи');
            }
        }
    };

    const handleGenerate = async () => {
        if (!currentProject) return;

        try {   
            const blob = await dispatch(generateProject(currentProject.id)).unwrap();
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `${currentProject.name}.zip`;
            a.click();
            window.URL.revokeObjectURL(url);
            // Записываем историю в БД + проверяем новые достижения
            try {
                const genRes = await api.post('/generations', {
                    projectId: currentProject.id,
                    projectName: currentProject.name,
                    targetStack: currentProject.targetStack,
                    architectureType: currentProject.architectureType,
                    entityCount: currentProject.entities?.length ?? 0,
                });
                if (genRes.data?.newAchievements?.length) {
                    showAchievements(genRes.data.newAchievements);
                }
            } catch { /* не прерываем основной флоу */ }
            toast.success('Проект успешно сгенерирован');
        } catch (error) {
            toast.error('Ошибка генерации проекта');
        }
    };

    const handleSaveAuth = async (authConfig: AuthConfig | null) => {
        if (!currentProject) return;
        try {
            await dispatch(updateProjectAuth({ id: currentProject.id, authConfig })).unwrap();
            toast.success(authConfig?.enabled ? 'Аутентификация настроена' : 'Аутентификация отключена');
            setShowAuthModal(false);
        } catch {
            toast.error('Ошибка сохранения настроек аутентификации');
        }
    };

    const handleLogout = () => {
        dispatch(logout());
        toast.info('Вы вышли из системы');
    };

    // Синхронизируем локальный порядок при смене проекта
    useEffect(() => {
        setEntityOrder(currentProject?.entities ?? []);
        setShowERDiagram(false);
    }, [currentProject?.id, currentProject?.entities]);

    // Создание проекта из шаблона
    const handleCreateFromTemplate = useCallback(async (
        name: string,
        stack: import('../../types').TargetStack,
        arch: import('../../types').ArchitectureType,
        template: ProjectTemplate
    ) => {
        const result = await dispatch(createProject({ name, description: template.description, targetStack: stack, architectureType: arch })).unwrap();
        const projectId = result.project.id;
        setProjectSearch('');
        setProjectPage(1);
        showProjectAchievements(result.newAchievements);

        const entityIdByName: Record<string, string> = {};

        for (let i = 0; i < template.entities.length; i++) {
            const tplEntity = template.entities[i];
            const entityRes = await api.post(`/entities/project/${projectId}`, {
                name: tplEntity.name,
                description: '',
                displayOrder: i,
                serviceName: arch === 'Microservices' ? tplEntity.name.toLowerCase() : null,
            });
            entityIdByName[tplEntity.name] = entityRes.data.id;
        }

        for (const tplEntity of template.entities) {
            const entityId = entityIdByName[tplEntity.name];

            for (let j = 0; j < tplEntity.fields.length; j++) {
                const f = tplEntity.fields[j];
                const payload: Record<string, unknown> = {
                    name: f.name,
                    dataType: f.dataType,
                    isRequired: f.isRequired,
                    isUnique: f.isUnique,
                    isPrimaryKey: false,
                    displayOrder: j,
                };

                if (f.dataType === 'Relationship' && f.relatedEntityName) {
                    const relatedId = entityIdByName[f.relatedEntityName];
                    if (!relatedId) continue;
                    payload.relatedEntityId = relatedId;
                    payload.relationshipType = f.relationshipType ?? 'OneToMany';
                }

                await api.post(`/fields/entity/${entityId}`, payload);

                if (f.dataType === 'Relationship' && f.relatedEntityName) {
                    const relatedId = entityIdByName[f.relatedEntityName];
                    if (relatedId) {
                        await api.post('/relationships', {
                            sourceEntityId: entityId,
                            targetEntityId: relatedId,
                            relationshipType: f.relationshipType ?? 'OneToMany',
                            sourceFieldName: f.name,
                        });
                    }
                }
            }
        }

        dispatch(fetchProjectById(projectId));
        toast.success(`Проект "${name}" создан из шаблона`);
    }, [dispatch]);

    // Экспорт схемы
    const handleExportSchema = useCallback(() => {
        if (!currentProject) return;
        const schema = {
            name: currentProject.name,
            description: currentProject.description,
            targetStack: currentProject.targetStack,
            architectureType: currentProject.architectureType,
            authConfig: currentProject.authConfig,
            entities: (currentProject.entities ?? []).map(e => ({
                name: e.name,
                description: e.description,
                displayOrder: e.displayOrder,
                serviceName: e.serviceName,
                fields: (e.fields ?? []).map(f => ({
                    name: f.name,
                    dataType: f.dataType,
                    isRequired: f.isRequired,
                    isUnique: f.isUnique,
                    isPrimaryKey: f.isPrimaryKey,
                    displayOrder: f.displayOrder,
                })),
            })),
        };
        const blob = new Blob([JSON.stringify(schema, null, 2)], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${currentProject.name}-schema.json`;
        a.click();
        URL.revokeObjectURL(url);
        toast.success('Схема экспортирована');
    }, [currentProject]);

    // Импорт схемы из JSON
    const handleImportSchema = useCallback(async (file: File) => {
        try {
            const text = await file.text();
            const schema = JSON.parse(text);
            if (!schema.name || !schema.entities) throw new Error('Некорректный формат файла');

            const result = await dispatch(createProject({
                name: schema.name,
                description: schema.description ?? '',
                targetStack: schema.targetStack ?? 'NodeJS_MongoDB',
                architectureType: schema.architectureType ?? 'Monolith',
            })).unwrap();
            const projectId = result.project.id;
            showProjectAchievements(result.newAchievements);

            const entities = schema.entities ?? [];
            for (const e of entities) {
                const entityRes = await api.post(`/entities/project/${projectId}`, {
                    name: e.name,
                    description: e.description ?? '',
                    displayOrder: e.displayOrder ?? 0,
                    serviceName: e.serviceName ?? null,
                });
                const entityId = entityRes.data.id;
                for (const f of (e.fields ?? [])) {
                    await api.post(`/fields/entity/${entityId}`, {
                        name: f.name,
                        dataType: f.dataType,
                        isRequired: f.isRequired ?? false,
                        isUnique: f.isUnique ?? false,
                        isPrimaryKey: f.isPrimaryKey ?? false,
                        displayOrder: f.displayOrder ?? 0,
                    });
                }
            }

            dispatch(fetchProjectById(projectId));
            setProjectSearch('');
            setProjectPage(1);
            toast.success(`Схема "${schema.name}" импортирована`);
        } catch (err: any) {
            toast.error(err?.message ?? 'Ошибка импорта схемы');
        }
    }, [dispatch]);

    // Дублирование проекта
    const handleDuplicateProject = useCallback(async () => {
        if (!currentProject) return;
        const copyName = `${currentProject.name} (копия)`;
        try {
            const result = await dispatch(createProject({
                name: copyName,
                description: currentProject.description ?? '',
                targetStack: currentProject.targetStack,
                architectureType: currentProject.architectureType,
            })).unwrap();
            const newProjectId = result.project.id;
            showProjectAchievements(result.newAchievements);

            const entities = currentProject.entities ?? [];
            for (let i = 0; i < entities.length; i++) {
                const e = entities[i];
                const entityRes = await api.post(`/entities/project/${newProjectId}`, {
                    name: e.name,
                    description: e.description ?? '',
                    displayOrder: e.displayOrder,
                    serviceName: e.serviceName ?? null,
                });
                const newEntityId = entityRes.data.id;
                for (const f of (e.fields ?? [])) {
                    await api.post(`/fields/entity/${newEntityId}`, {
                        name: f.name,
                        dataType: f.dataType,
                        isRequired: f.isRequired,
                        isUnique: f.isUnique,
                        isPrimaryKey: f.isPrimaryKey,
                        displayOrder: f.displayOrder,
                    });
                }
            }

            // Копируем authConfig если есть
            if (currentProject.authConfig) {
                await api.put(`/projects/${newProjectId}/auth`, { authConfig: currentProject.authConfig });
            }

            dispatch(fetchProjectById(newProjectId));
            setProjectSearch('');
            setProjectPage(1);
            toast.success(`Проект дублирован: "${copyName}"`);
        } catch {
            toast.error('Ошибка при дублировании проекта');
        }
    }, [currentProject, dispatch]);

    // DnD: перетаскивание сущностей
    const handleDragEnd = useCallback(async (event: DragEndEvent) => {
        const { active, over } = event;
        if (!over || active.id === over.id || !currentProject) return;

        const oldIndex = entityOrder.findIndex(e => e.id === active.id);
        const newIndex = entityOrder.findIndex(e => e.id === over.id);
        if (oldIndex === -1 || newIndex === -1) return;

        const newOrder = arrayMove(entityOrder, oldIndex, newIndex).map((e, idx) => ({ ...e, displayOrder: idx }));
        setEntityOrder(newOrder);

        try {
            await api.post('/entities/reorder', {
                items: newOrder.map(e => ({ id: e.id, displayOrder: e.displayOrder })),
            });
        } catch {
            setEntityOrder(currentProject.entities ?? []);
            toast.error('Ошибка обновления порядка');
        }
    }, [currentProject, entityOrder]);

    const handleToggleTheme = async () => {
        if (!user) return;
        const newDarkMode = !user.isDarkMode;
        // Мгновенно применяем тему через DOM (App.tsx синхронизирует по user)
        if (newDarkMode) {
            document.body.classList.add('dark-mode');
        } else {
            document.body.classList.remove('dark-mode');
        }
        try {
            await dispatch(updateUser({ id: user.id, data: { isDarkMode: newDarkMode } })).unwrap();
        } catch {
            // Откатываем если запрос не удался
            if (newDarkMode) {
                document.body.classList.remove('dark-mode');
            } else {
                document.body.classList.add('dark-mode');
            }
            toast.error('Не удалось сохранить тему');
        }
    };

    return (
        <div className="dashboard animate-fade-in">
            {/* Мобильная шапка */}
            <div className="mobile-header">
                <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                    <img src={user?.isDarkMode ? "/logo_white.svg" : "/logo.svg"} alt="CodeForge" style={{ height: '32px', width: 'auto' }} />
                    <span style={{ fontSize: '1rem', fontWeight: 'bold' }}>CodeForge</span>
                </div>
                <button className="menu-btn" onClick={() => setIsSidebarOpen(true)}>
                    ☰
                </button>
            </div>

            {/* Затемнение при открытом сайдбаре */}
            <div
                className={`sidebar-overlay ${isSidebarOpen ? 'visible' : ''}`}
                onClick={() => setIsSidebarOpen(false)}
            />

            <div className={`sidebar animate-slide-up ${isSidebarOpen ? 'open' : ''}`}>
                <div className="sidebar-header" style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '1.75rem' }}>
                    <img src={user?.isDarkMode ? "/logo_white.svg" : "/logo.svg"} alt="CodeForge" style={{ height: '38px', width: 'auto' }} />
                    <span style={{ fontSize: '1.2rem', fontWeight: 800, letterSpacing: '-0.5px', background: 'var(--accent-gradient)', WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent', backgroundClip: 'text' }}>CodeForge</span>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '0.75rem' }}>
                    <h2 style={{ margin: 0, fontSize: '0.8rem', textTransform: 'uppercase', letterSpacing: '0.08em', color: 'var(--text-secondary)', fontWeight: 600 }}>Проекты</h2>
                    {projects.length > 0 && (
                        <span style={{ fontSize: '0.72rem', background: 'var(--accent-light)', color: 'var(--accent-color)', padding: '0.1rem 0.5rem', borderRadius: '999px', fontWeight: 600 }}>
                            {projectSearch ? `${filteredProjects.length} / ${projects.length}` : projects.length}
                        </span>
                    )}
                </div>
                <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '0.75rem' }}>
                    <button className="btn btn-primary hover-scale" onClick={() => setShowNewProject(true)} style={{ flex: 1, justifyContent: 'center' }}>
                        + Новый проект
                    </button>
                    <button className="btn btn-secondary hover-scale" onClick={() => setShowTemplates(true)} title="Создать из шаблона" style={{ flexShrink: 0, padding: '0 0.75rem' }}>
                        ✨
                    </button>
                </div>

                {/* Поиск и список проектов — прокручиваемая область */}
                <div className="sidebar-body">
                {projects.length > 0 && (
                    <div className="project-search-wrap">
                        <span className="project-search-icon">🔍</span>
                        <input
                            className="project-search-input"
                            type="text"
                            placeholder="Поиск проектов..."
                            value={projectSearch}
                            onChange={e => setProjectSearch(e.target.value)}
                        />
                        {projectSearch && (
                            <button className="project-search-clear" onClick={() => setProjectSearch('')} title="Очистить">✕</button>
                        )}
                    </div>
                )}

                <ul className="project-list">
                    {pagedProjects.length === 0 && (
                        <li style={{ padding: '1rem 0.5rem', color: 'var(--text-secondary)', fontSize: '0.85rem', textAlign: 'center' }}>
                            Ничего не найдено
                        </li>
                    )}
                    {pagedProjects.map((project, index) => (
                        <li
                            key={project.id}
                            className={`project-item ${currentProject?.id === project.id ? 'active' : ''}`}
                            onClick={() => handleSelectProject(project)}
                            style={{ animationDelay: `${index * 40}ms` }}
                        >
                            <h3>{project.name}</h3>
                            <div className={`stack-badge ${project.targetStack === 'CSharp_PostgreSQL' ? 'csharp' : 'nodejs'}${project.architectureType === 'Microservices' ? ' micro' : ''}`}>
                                {project.targetStack === 'CSharp_PostgreSQL' ? 'C# · PostgreSQL' : 'Node.js · MongoDB'}
                                {project.architectureType === 'Microservices' ? ' · μServices' : ''}
                            </div>
                        </li>
                    ))}
                </ul>

                {/* Пагинация */}
                {totalProjectPages > 1 && (
                    <div className="project-pagination">
                        <button
                            className="project-page-btn"
                            onClick={() => setProjectPage(p => Math.max(1, p - 1))}
                            disabled={projectPage === 1}
                            title="Предыдущая страница"
                        >‹</button>
                        <div className="project-page-dots">
                            {Array.from({ length: totalProjectPages }, (_, i) => i + 1).map(page => (
                                <button
                                    key={page}
                                    className={`project-page-dot ${page === projectPage ? 'active' : ''}`}
                                    onClick={() => setProjectPage(page)}
                                    title={`Страница ${page}`}
                                />
                            ))}
                        </div>
                        <button
                            className="project-page-btn"
                            onClick={() => setProjectPage(p => Math.min(totalProjectPages, p + 1))}
                            disabled={projectPage === totalProjectPages}
                            title="Следующая страница"
                        >›</button>
                    </div>
                )}
                </div>

                <div className="sidebar-footer">
                    <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', marginBottom: '0.5rem', paddingLeft: '0.5rem' }}>
                        Разработчик
                    </div>
                    <a
                        href="https://github.com/woookle"
                        target="_blank"
                        rel="noopener noreferrer"
                        className="github-banner hover-scale"
                        title="Visit GitHub Profile"
                    >
                        <img src="https://avatars.githubusercontent.com/u/168928590?v=4" alt="woookle" />
                        <div style={{ display: 'flex', flexDirection: 'column' }}>
                            <span>woookle</span>
                            <span style={{ fontSize: '0.7rem', color: 'var(--text-secondary)' }}>Full Stack Dev</span>
                        </div>
                    </a>
                </div>
            </div>

            <div className="main-content">
                <div className="header">
                    <div>
                        <h1 style={{ marginBottom: currentProject ? '0.25rem' : 0 }}>
                            {currentProject ? currentProject.name : 'Выберите проект'}
                        </h1>
                        {currentProject && (
                            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginTop: '0.35rem' }}>
                                <span className={`stack-badge ${currentProject.targetStack === 'CSharp_PostgreSQL' ? 'csharp' : 'nodejs'}`}>
                                    {currentProject.targetStack === 'CSharp_PostgreSQL' ? 'C# · PostgreSQL' : 'Node.js · MongoDB'}
                                </span>
                                {currentProject.architectureType === 'Microservices' && (
                                    <span className="stack-badge micro">Микросервисы</span>
                                )}
                                {currentProject.entities && currentProject.entities.length > 0 && (
                                    <span style={{ fontSize: '0.72rem', color: 'var(--text-secondary)' }}>
                                        {currentProject.entities.length} {currentProject.entities.length === 1 ? 'сущность' : currentProject.entities.length < 5 ? 'сущности' : 'сущностей'}
                                    </span>
                                )}
                            </div>
                        )}
                    </div>
                    <div className="user-controls">
                        {user?.role === 'Admin' && (
                            <button className="btn btn-warning btn-small" onClick={() => onNavigate('admin')}>
                                👑 Админ
                            </button>
                        )}

                        {/* Кнопка истории генераций */}
                        <button
                            className="theme-toggle-btn"
                            onClick={() => setShowHistory(true)}
                            title="История генераций"
                            aria-label="История генераций"
                        >
                            📜
                        </button>

                        {/* Кнопка переключения темы */}
                        <button
                            className="theme-toggle-btn"
                            onClick={handleToggleTheme}
                            title={user?.isDarkMode ? 'Светлая тема' : 'Тёмная тема'}
                            aria-label="Переключить тему"
                        >
                            {user?.isDarkMode ? '☀️' : '🌙'}
                        </button>

                        <div
                            className="user-profile-summary hover-scale"
                            onClick={() => onNavigate('profile')}
                            title="Перейти в профиль"
                            style={{
                                display: 'flex',
                                alignItems: 'center',
                                gap: '8px',
                                cursor: 'pointer',
                                padding: '5px 10px 5px 6px',
                                borderRadius: '99px',
                                border: '1.5px solid var(--border-color)',
                                backgroundColor: 'white',
                                transition: 'all 0.2s',
                                boxShadow: 'var(--shadow-sm)',
                            }}
                        >
                            <div className="avatar-circle" style={{
                                width: '28px',
                                height: '28px',
                                borderRadius: '50%',
                                overflow: 'hidden',
                                background: 'var(--accent-gradient)',
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                                flexShrink: 0,
                            }}>
                                {user?.avatarUrl ? (
                                    <img
                                        src={`${import.meta.env.VITE_IMG_URL}${user.avatarUrl}`}
                                        alt="Avatar"
                                        style={{ width: '100%', height: '100%', objectFit: 'cover' }}
                                    />
                                ) : (
                                    <span style={{ fontSize: '12px', fontWeight: 'bold', color: 'white' }}>
                                        {user?.email?.charAt(0).toUpperCase()}
                                    </span>
                                )}
                            </div>
                            <span style={{ fontWeight: 500, fontSize: '0.85rem', maxWidth: '140px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{user?.email}</span>
                        </div>

                        <button className="btn btn-secondary btn-small" onClick={handleLogout}>
                            Выйти
                        </button>
                    </div>
                </div>

                {currentProject && (
                    <>
                        <div className="flex gap-2 mb-4" style={{ flexWrap: 'wrap' }}>
                            <button className="btn btn-primary" onClick={() => setShowNewEntity(true)}>
                                + Новая сущность
                            </button>
                            <button className="btn btn-success" onClick={handleGenerate}>
                                📦 Скачать проект
                            </button>
                            <button
                                className="btn btn-secondary"
                                onClick={() => setShowAuthModal(true)}
                                title="Настройка аутентификации"
                            >
                                {(() => {
                                    try {
                                        const cfg = parseAuthConfig(currentProject.authConfig);
                                        return cfg?.enabled ? '🔐 Auth: Включена' : '🔓 Auth: Выключена';
                                    } catch { return '🔓 Auth'; }
                                })()}
                            </button>
                            {/* Дублировать */}
                            <button className="btn btn-secondary" onClick={handleDuplicateProject} title="Дублировать проект">
                                📋 Дублировать
                            </button>
                            {/* Экспорт / Импорт схемы */}
                            <button className="btn btn-secondary" onClick={handleExportSchema} title="Экспорт схемы в JSON">
                                ⬇ Экспорт
                            </button>
                            <button className="btn btn-secondary" onClick={() => importFileRef.current?.click()} title="Импорт схемы из JSON">
                                ⬆ Импорт
                            </button>
                            <input
                                ref={importFileRef}
                                type="file"
                                accept=".json"
                                style={{ display: 'none' }}
                                onChange={e => { if (e.target.files?.[0]) { handleImportSchema(e.target.files[0]); e.target.value = ''; } }}
                            />
                            {/* ER-диаграмма */}
                            <button
                                className={`btn ${showERDiagram ? 'btn-primary' : 'btn-secondary'}`}
                                onClick={() => setShowERDiagram(v => !v)}
                                title="Визуальная ER-диаграмма"
                            >
                                {showERDiagram ? '📋 Схема' : '🗺 ER-диаграмма'}
                            </button>
                            <button className="btn btn-danger" onClick={() => handleDeleteProject(currentProject.id)}>
                                🗑 Удалить
                            </button>
                            {/* Значок архитектуры */}
                            <span style={{
                                display: 'inline-flex', alignItems: 'center', padding: '0 12px',
                                borderRadius: '20px', fontSize: '0.78rem', fontWeight: 600,
                                background: currentProject.architectureType === 'Microservices' ? 'rgba(99,102,241,0.12)' : 'rgba(16,185,129,0.12)',
                                color: currentProject.architectureType === 'Microservices' ? '#6366f1' : '#10b981',
                                border: `1px solid ${currentProject.architectureType === 'Microservices' ? 'rgba(99,102,241,0.3)' : 'rgba(16,185,129,0.3)'}`,
                            }}>
                                {currentProject.architectureType === 'Microservices' ? '🔀 Microservices' : '🏗️ Monolith'}
                            </span>
                        </div>

                        {/* ER-диаграмма */}
                        {showERDiagram && entityOrder.length > 0 && (
                            <ERDiagram entities={entityOrder} isDarkMode={user?.isDarkMode} />
                        )}
                        {showERDiagram && entityOrder.length === 0 && (
                            <div className="empty-state" style={{ marginBottom: '1.5rem' }}>
                                <div style={{ fontSize: '2rem' }}>🗺</div>
                                <p>Нет сущностей для отображения на диаграмме</p>
                            </div>
                        )}

                        {/* Визуальный просмотр микросервисной архитектуры */}
                        {currentProject.architectureType === 'Microservices' && (() => {
                            const authCfgPre = (() => { try { return parseAuthConfig(currentProject.authConfig); } catch { return null; } })();
                            return (
                                <MicroservicesPreview
                                    project={currentProject}
                                    authEnabled={authCfgPre?.enabled === true}
                                />
                            );
                        })()}

                        {/* Визуальный просмотр монолитной структуры */}
                        {currentProject.architectureType !== 'Microservices' && (() => {
                            const authCfgMono = (() => { try { return parseAuthConfig(currentProject.authConfig); } catch { return null; } })();
                            return (
                                <MonolithPreview
                                    project={currentProject}
                                    authEnabled={authCfgMono?.enabled === true}
                                    rolesEnabled={authCfgMono?.enableRoles === true}
                                />
                            );
                        })()}

                        {/* Баннер группировки сервисов в микросервисах */}
                        {currentProject.architectureType === 'Microservices' && (() => {
                            const entityGroups = Array.from(new Set(
                                (currentProject.entities || []).map(e => e.serviceName || e.name)
                            ));
                            const authCfg = (() => { try { return parseAuthConfig(currentProject.authConfig); } catch { return null; } })();
                            const hasAuthService = authCfg?.enabled === true;
                            const groups = hasAuthService ? ['auth', ...entityGroups] : entityGroups;
                            const colors = ['#6366f1','#10b981','#f59e0b','#ef4444','#3b82f6','#8b5cf6','#ec4899','#14b8a6'];
                            const colorMap: Record<string, string> = {};
                            groups.forEach((g, i) => { colorMap[g] = colors[i % colors.length]; });
                            return (
                                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px', marginBottom: '12px', padding: '10px', background: 'rgba(99,102,241,0.06)', borderRadius: '8px', border: '1px solid rgba(99,102,241,0.15)' }}>
                                    <span style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', alignSelf: 'center', marginRight: '4px' }}>🔀 Сервисы:</span>
                                    {hasAuthService && (
                                        <span style={{ padding: '2px 10px', borderRadius: '12px', background: colorMap['auth'] + '22', color: colorMap['auth'], border: `1px solid ${colorMap['auth']}44`, fontSize: '0.78rem', fontWeight: 600 }}>
                                            🔐 auth-service
                                        </span>
                                    )}
                                    {entityGroups.map(g => (
                                        <span key={g} style={{ padding: '2px 10px', borderRadius: '12px', background: colorMap[g] + '22', color: colorMap[g], border: `1px solid ${colorMap[g]}44`, fontSize: '0.78rem', fontWeight: 600 }}>{g}-service</span>
                                    ))}
                                </div>
                            );
                        })()}

                        {!showERDiagram && (
                        <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
                            <SortableContext items={entityOrder.map(e => e.id)} strategy={verticalListSortingStrategy}>
                            {entityOrder.map((entity: Entity) => {
                                const svcName = entity.serviceName || entity.name;
                                const isMicro = currentProject.architectureType === 'Microservices';
                                const allSvcNames = Array.from(new Set((currentProject.entities || []).map(e => e.serviceName || e.name)));
                                const colors = ['#6366f1','#10b981','#f59e0b','#ef4444','#3b82f6','#8b5cf6','#ec4899','#14b8a6'];
                                const colorMap: Record<string, string> = {};
                                allSvcNames.forEach((g, i) => { colorMap[g] = colors[i % colors.length]; });
                                const svcColor = colorMap[svcName] || '#6366f1';
                                return (
                                    <SortableEntityCard key={entity.id} id={entity.id}>
                                    <div className="entity-card">
                                        <div className="entity-header">
                                            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                                <span className="drag-handle" title="Перетащить">⠿</span>
                                                <h3>{entity.name}</h3>
                                                {isMicro && (
                                                    <span style={{ padding: '1px 8px', borderRadius: '10px', background: svcColor + '22', color: svcColor, border: `1px solid ${svcColor}44`, fontSize: '0.72rem', fontWeight: 600 }}>
                                                        ⚙️ {svcName}-service
                                                    </span>
                                                )}
                                            </div>
                                            <div className="entity-actions">
                                                <button
                                                    className="btn btn-primary btn-small"
                                                    onClick={() => {
                                                        setSelectedEntityId(entity.id);
                                                        setEditingField(null);
                                                        setShowFieldModal(true);
                                                    }}
                                                >
                                                    + Поле
                                                </button>
                                                {isMicro && (
                                                    <button
                                                        className="btn btn-secondary btn-small"
                                                        title="Изменить имя микросервиса"
                                                        onClick={() => handleChangeServiceName(entity.id, entity.serviceName, entity.name)}
                                                    >
                                                        ⚙️ Сервис
                                                    </button>
                                                )}
                                                <button
                                                    className="btn btn-danger btn-small"
                                                    onClick={() => handleDeleteEntity(entity.id)}
                                                >
                                                    Удалить
                                                </button>
                                            </div>
                                        </div>

                                        {entity.fields && entity.fields.length > 0 && (
                                            <div className="table-container">
                                                <table style={{ marginBottom: '15px' }}>
                                                    <thead>
                                                        <tr>
                                                            <th>Имя</th>
                                                            <th>Тип</th>
                                                            <th>Атрибуты</th>
                                                            <th>Действия</th>
                                                        </tr>
                                                    </thead>
                                                    <tbody>
                                                        {entity.fields.map((field: Field) => (
                                                            <tr key={field.id}>
                                                                <td>{field.name}</td>
                                                                <td>{field.dataType}</td>
                                                                <td>
                                                                    {field.isRequired ? <span className="badge badge-warning">Обяз.</span> : ''}
                                                                    {field.isUnique ? <span className="badge badge-info">Уник.</span> : ''}
                                                                    {field.isPrimaryKey ? <span className="badge badge-success">Ключ</span> : ''}
                                                                </td>
                                                                <td>
                                                                    <div style={{ display: 'flex', gap: '5px' }}>
                                                                        <button
                                                                            className="btn btn-secondary btn-small"
                                                                            onClick={() => {
                                                                                setSelectedEntityId(entity.id);
                                                                                setEditingField(field);
                                                                                setShowFieldModal(true);
                                                                            }}
                                                                        >
                                                                            ✏️
                                                                        </button>
                                                                        <button
                                                                            className="btn btn-danger btn-small"
                                                                            onClick={() => handleDeleteField(field.id)}
                                                                        >
                                                                            🗑️
                                                                        </button>
                                                                    </div>
                                                                </td>
                                                            </tr>
                                                        ))}
                                                    </tbody>
                                                </table>
                                            </div>
                                        )}

                                        {/* Исходящие связи (из полей Relationship и таблицы Relationships) */}
                                        {(() => {
                                            const outgoing = getOutgoingRelationships(entity, currentProject?.entities ?? []);
                                            if (outgoing.length === 0) return null;
                                            return (
                                            <div className="relationships-list">
                                                <strong>Исходящие связи:</strong>
                                                <ul style={{ listStyle: 'none', margin: 0, padding: 0 }}>
                                                    {outgoing.map(rel => (
                                                        <li key={`${rel.sourceFieldName}-${rel.targetEntityName}`} className="relationship-item">
                                                            <span>
                                                                <span className="badge badge-info" style={{ marginRight: '6px' }}>
                                                                    {formatRelationshipType(rel.relationshipType)}
                                                                </span>
                                                                → {rel.targetEntityName}
                                                                <span className="text-secondary" style={{ marginLeft: '5px' }}>({rel.sourceFieldName})</span>
                                                            </span>
                                                            <button
                                                                className="btn btn-danger btn-small"
                                                                onClick={() => handleDeleteOutgoingRelationship(entity, rel)}
                                                            >
                                                                ×
                                                            </button>
                                                        </li>
                                                    ))}
                                                </ul>
                                            </div>
                                            );
                                        })()}
                                    </div>
                                    </SortableEntityCard>
                                );
                            })}
                            </SortableContext>
                        </DndContext>
                        )}

                        {/* Карточка auth-service (показывается при включённой аутентификации) */}
                        {(() => {
                            try {
                                const authCfg = parseAuthConfig(currentProject.authConfig);
                                if (!authCfg?.enabled) return null;
                                const isMicro = currentProject.architectureType === 'Microservices';
                                const identifier = authCfg.userIdentifier || 'email';
                                const hasRoles = authCfg.enableRoles;
                                const hasRefresh = authCfg.enableRefreshTokens;
                                const hasEmailVerif = authCfg.enableEmailVerification;
                                const isNode = currentProject.targetStack === 'NodeJS_MongoDB';
                                const userFields = [
                                    { name: 'id', type: isNode ? 'ObjectId' : 'Guid', key: true },
                                    ...(identifier === 'email' || identifier === 'both' ? [{ name: 'email', type: 'String', required: true }] : []),
                                    ...(identifier === 'username' || identifier === 'both' ? [{ name: 'username', type: 'String', required: true }] : []),
                                    { name: 'passwordHash', type: 'String', required: true },
                                    ...(hasRoles ? [{ name: 'role', type: 'String' }] : []),
                                    ...(hasRefresh ? [{ name: 'refreshToken', type: 'String' }] : []),
                                    ...(hasEmailVerif ? [{ name: 'isEmailVerified', type: 'Boolean' }] : []),
                                    { name: 'createdAt', type: 'DateTime' },
                                ];
                                const endpoints = [
                                    { method: 'POST', path: '/api/auth/register', desc: 'Регистрация' },
                                    { method: 'POST', path: '/api/auth/login', desc: 'Вход' },
                                    { method: 'GET',  path: '/api/auth/me', desc: 'Текущий пользователь', auth: true },
                                ];
                                return (
                                    <div key="__auth_user__" className="entity-card" style={{ border: '2px solid rgba(99,102,241,0.4)', background: 'rgba(99,102,241,0.04)' }}>
                                        <div className="entity-header">
                                            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap' }}>
                                                <h3>User</h3>
                                                <span style={{ padding: '1px 8px', borderRadius: '10px', background: 'rgba(99,102,241,0.15)', color: '#6366f1', border: '1px solid rgba(99,102,241,0.3)', fontSize: '0.72rem', fontWeight: 600 }}>
                                                    🔐 Auth
                                                </span>
                                {isMicro ? (
                                    <span style={{ padding: '1px 8px', borderRadius: '10px', background: 'rgba(99,102,241,0.15)', color: '#6366f1', border: '1px solid rgba(99,102,241,0.3)', fontSize: '0.72rem', fontWeight: 600 }}>
                                        ⚙️ auth-service
                                    </span>
                                ) : (
                                    <span style={{ fontSize: '0.72rem', color: 'var(--text-secondary)' }}>(автогенерация)</span>
                                )}
                                            </div>
                                            <div className="entity-actions">
                                                <button className="btn btn-secondary btn-small" onClick={() => setShowAuthModal(true)}>
                                                    ⚙️ Настройка
                                                </button>
                                            </div>
                                        </div>

                                        {/* Поля модели User */}
                                        <div className="table-container">
                                            <table style={{ marginBottom: '8px' }}>
                                                <thead>
                                                    <tr>
                                                        <th>Поле</th>
                                                        <th>Тип</th>
                                                        <th>Атрибуты</th>
                                                    </tr>
                                                </thead>
                                                <tbody>
                                                    {userFields.map((f: any) => (
                                                        <tr key={f.name}>
                                                            <td style={{ fontWeight: 500 }}>{f.name}</td>
                                                            <td>{f.type}</td>
                                                            <td>
                                                                {f.key && <span className="badge badge-success">PK</span>}
                                                                {f.required && <span className="badge badge-warning">Обяз.</span>}
                                                            </td>
                                                        </tr>
                                                    ))}
                                                </tbody>
                                            </table>
                                        </div>

                                        {/* REST API-эндпоинты */}
                                        <div style={{ marginTop: '8px', marginBottom: '4px' }}>
                                            <div style={{ fontSize: '0.72rem', color: 'var(--text-secondary)', marginBottom: '6px', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                                                REST API endpoints
                                            </div>
                                            <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                                                {endpoints.map(ep => (
                                                    <div key={ep.path} style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '0.78rem' }}>
                                                        <span style={{
                                                            padding: '1px 7px', borderRadius: '4px', fontWeight: 700, fontFamily: 'monospace', fontSize: '0.7rem',
                                                            background: ep.method === 'POST' ? 'rgba(16,185,129,0.15)' : 'rgba(59,130,246,0.15)',
                                                            color: ep.method === 'POST' ? '#10b981' : '#3b82f6',
                                                            border: `1px solid ${ep.method === 'POST' ? 'rgba(16,185,129,0.3)' : 'rgba(59,130,246,0.3)'}`,
                                                            minWidth: '42px', textAlign: 'center'
                                                        }}>{ep.method}</span>
                                                        <code style={{ color: 'var(--text-primary)', fontSize: '0.77rem' }}>{ep.path}</code>
                                                        <span style={{ color: 'var(--text-secondary)' }}>— {ep.desc}</span>
                                                        {ep.auth && <span style={{ fontSize: '0.68rem', padding: '1px 5px', borderRadius: '4px', background: 'rgba(99,102,241,0.12)', color: '#6366f1', border: '1px solid rgba(99,102,241,0.25)' }}>🔒 JWT</span>}
                                                    </div>
                                                ))}
                                            </div>
                                        </div>

                                        {/* Нижняя информационная строка */}
                                        <div style={{ fontSize: '0.73rem', color: 'var(--text-secondary)', padding: '6px 0 2px', borderTop: '1px solid var(--border-color)', marginTop: '8px' }}>
                                            {isMicro
                                                ? '📦 Отдельный сервис: services/auth-service · Собственная БД auth_db · Публикует события user.registered, user.loggedin в RabbitMQ'
                                                : 'AuthController + TokenService + AuthService встроены в основной проект'}
                                        </div>
                                    </div>
                                );
                            } catch { return null; }
                        })()}

                        {(!currentProject.entities || currentProject.entities.length === 0) && (() => {
                            try {
                                const cfg = parseAuthConfig(currentProject.authConfig);
                                if (cfg?.enabled) return null; // auth-сущность считается контентом
                            } catch {}
                            return (
                                <div className="empty-state">
                                    <div style={{ fontSize: '2.5rem', marginBottom: '1rem' }}>🧩</div>
                                    <h3>Нет сущностей</h3>
                                    <p>Создайте первую сущность (таблицу / модель) для вашего проекта, чтобы начать генерацию кода.</p>
                                    <button className="btn btn-primary" onClick={() => setShowNewEntity(true)}>
                                        + Добавить сущность
                                    </button>
                                </div>
                            );
                        })()}
                    </>
                )}

                {!currentProject && (
                    <div className="empty-state">
                        <div style={{ fontSize: '3rem', marginBottom: '1rem' }}>⚡</div>
                        <h3>Добро пожаловать в CodeForge</h3>
                        <p>Выберите проект из списка слева или создайте новый, чтобы начать генерацию backend-кода.</p>
                        <button className="btn btn-primary" onClick={() => setShowNewProject(true)}>
                            + Создать проект
                        </button>
                    </div>
                )}
            </div>

            {/* Модал нового проекта */}
            {showNewProject && <NewProjectModal onClose={() => setShowNewProject(false)} onCreate={handleNewProject} />}

            {/* Модал шаблонов */}
            {showTemplates && (
                <ProjectTemplatesModal
                    onClose={() => setShowTemplates(false)}
                    onCreate={handleCreateFromTemplate}
                />
            )}

            {/* Модал истории генераций */}
            {showHistory && (
                <GenerationHistoryModal
                    onClose={() => setShowHistory(false)}
                    filterProjectId={currentProject?.id}
                />
            )}

            {/* Модал новой сущности */}
            {showNewEntity && <NewEntityModal onClose={() => setShowNewEntity(false)} onCreate={handleNewEntity} isMicroservices={currentProject?.architectureType === 'Microservices'} />}

            {/* Модал поля */}
            {showFieldModal && selectedEntityId && (
                <FieldModal
                    entityId={selectedEntityId}
                    entities={currentProject?.entities || []}
                    initialValues={editingField}
                    onClose={() => {
                        setShowFieldModal(false);
                        setEditingField(null);
                        setSelectedEntityId(null);
                    }}
                    onSave={handleSaveField}
                />
            )}


            {/* Модал настройки аутентификации */}
            {showAuthModal && currentProject && (
                <AuthConfigModal
                    currentConfig={(() => {
                        try { return parseAuthConfig(currentProject.authConfig); } catch { return null; }
                    })()}
                    stack={currentProject.targetStack}
                    entities={currentProject.entities || []}
                    onClose={() => setShowAuthModal(false)}
                    onSave={handleSaveAuth}
                />
            )}

            {/* Виджет FAQ */}
            <FAQWidget />
        </div>
    );
}

// Компоненты модальных окон
function NewProjectModal({ onClose, onCreate }: any) {
    const [name, setName] = useState('');
    const [stack, setStack] = useState<'CSharp_PostgreSQL' | 'NodeJS_MongoDB'>('CSharp_PostgreSQL');
    const [architecture, setArchitecture] = useState<'Monolith' | 'Microservices'>('Monolith');

    return (
        <div className="modal-overlay" onClick={onClose}>
            <div className="modal" onClick={(e) => e.stopPropagation()}>
                <h2>Новый проект</h2>
                <div className="form-group">
                    <label>Название</label>
                    <input value={name} onChange={(e) => setName(e.target.value)} placeholder="E-Commerce API" />
                </div>
                <div className="form-group">
                    <label>Стек</label>
                    <select value={stack} onChange={(e) => setStack(e.target.value as any)}>
                        <option value="CSharp_PostgreSQL">C# + PostgreSQL</option>
                        <option value="NodeJS_MongoDB">Node.js + MongoDB</option>
                    </select>
                </div>
                <div className="form-group">
                    <label>Архитектура</label>
                    <select value={architecture} onChange={(e) => setArchitecture(e.target.value as any)}>
                        <option value="Monolith">🏗️ Монолит (один сервис)</option>
                        <option value="Microservices">🔀 Микросервисы (несколько сервисов + RabbitMQ)</option>
                    </select>
                </div>
                {architecture === 'Microservices' && (
                    <div style={{ padding: '10px 12px', background: 'rgba(99,102,241,0.08)', borderRadius: '8px', fontSize: '0.8rem', color: 'var(--text-secondary)', marginBottom: '12px' }}>
                        <strong>🔀 Режим микросервисов:</strong> каждой сущности назначается имя сервиса.
                        Сущности с одинаковым именем группируются в один сервис со своей базой данных.
                        Сервисы общаются через <strong>RabbitMQ</strong>.
                    </div>
                )}
                <div style={{ display: 'flex', gap: '10px' }}>
                    <button className="btn btn-primary" onClick={() => onCreate(name, stack, architecture)} disabled={!name}>
                        Создать
                    </button>
                    <button className="btn btn-secondary" onClick={onClose}>Отмена</button>
                </div>
            </div>
        </div>
    );
}

function NewEntityModal({ onClose, onCreate, isMicroservices }: any) {
    const [name, setName] = useState('');
    const [serviceName, setServiceName] = useState('');

    return (
        <div className="modal-overlay" onClick={onClose}>
            <div className="modal" onClick={(e) => e.stopPropagation()}>
                <h2>Новая сущность</h2>
                <div className="form-group">
                    <label>Название сущности</label>
                    <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Product" />
                </div>
                {isMicroservices && (
                    <div className="form-group">
                        <label>Имя микросервиса <span style={{ color: 'var(--text-secondary)', fontSize: '0.8rem' }}>(оставьте пустым = имя сущности)</span></label>
                        <input
                            value={serviceName}
                            onChange={(e) => setServiceName(e.target.value)}
                            placeholder={name || 'Product'}
                        />
                        {serviceName && (
                            <div style={{ marginTop: '4px', fontSize: '0.78rem', color: '#6366f1' }}>
                                Сервис: <strong>{serviceName || name}-service</strong>
                            </div>
                        )}
                    </div>
                )}
                <div style={{ display: 'flex', gap: '10px' }}>
                    <button className="btn btn-primary" onClick={() => onCreate(name, serviceName || undefined)} disabled={!name}>
                        Создать
                    </button>
                    <button className="btn btn-secondary" onClick={onClose}>Отмена</button>
                </div>
            </div>
        </div>
    );
}

function FieldModal({ entityId, entities, onClose, onSave, initialValues }: any) {
    const [name, setName] = useState(initialValues?.name || '');
    const [dataType, setDataType] = useState(initialValues?.dataType || 'String');
    const [isRequired, setIsRequired] = useState(initialValues?.isRequired || false);
    const [isUnique, setIsUnique] = useState(initialValues?.isUnique || false);

    const [relatedEntityId, setRelatedEntityId] = useState(initialValues?.relatedEntityId || '');
    const [relationshipType, setRelationshipType] = useState(initialValues?.relationshipType || 'OneToMany');

    const availableTargets = entities.filter((e: Entity) => e.id !== entityId);

    const handleRelatedEntityChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
        const selectedId = e.target.value;
        setRelatedEntityId(selectedId);
        if (selectedId && !initialValues) {
            const selectedEntity = entities.find((ent: Entity) => ent.id === selectedId);
            if (selectedEntity && (!name || name === 'Name')) {
                setName(`${selectedEntity.name}Id`);
            }
        }
    };

    return (
        <div className="modal-overlay" onClick={onClose}>
            <div className="modal" onClick={(e) => e.stopPropagation()}>
                <h2>{initialValues ? 'Редактировать поле' : 'Новое поле'}</h2>
                <div className="form-group">
                    <label>Название</label>
                    <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Name" />
                </div>
                <div className="form-group">
                    <label>Тип данных</label>
                    <select value={dataType} onChange={(e) => setDataType(e.target.value)}>
                        <option value="String">String</option>
                        <option value="Integer">Integer</option>
                        <option value="Float">Float</option>
                        <option value="Long">Long</option>
                        <option value="Decimal">Decimal</option>
                        <option value="Boolean">Boolean</option>
                        <option value="DateTime">DateTime</option>
                        <option value="Text">Text (long)</option>
                        <option value="Guid">Guid</option>
                        <option value="Relationship">Связь (Relationship)</option>
                    </select>
                </div>

                {dataType === 'Relationship' && (
                    <>
                        <div className="form-group">
                            <label>Связанная сущность</label>
                            <select value={relatedEntityId} onChange={handleRelatedEntityChange}>
                                <option value="">Выберите сущность...</option>
                                {availableTargets.map((e: Entity) => (
                                    <option key={e.id} value={e.id}>{e.name}</option>
                                ))}
                            </select>
                        </div>
                        <div className="form-group">
                            <label>Тип связи</label>
                            <select value={relationshipType} onChange={(e) => setRelationshipType(e.target.value)}>
                                <option value="OneToMany">One-to-Many</option>
                                <option value="OneToOne">One-to-One</option>
                                <option value="ManyToMany">Many-to-Many</option>
                            </select>
                        </div>
                    </>
                )}

                <div className="checkbox-group">
                    <label>
                        <input type="checkbox" checked={isRequired} onChange={(e) => setIsRequired(e.target.checked)} />
                        Обязательное
                    </label>
                    <label>
                        <input type="checkbox" checked={isUnique} onChange={(e) => setIsUnique(e.target.checked)} />
                        Уникальное
                    </label>
                </div>

                <div style={{ display: 'flex', gap: '10px', marginTop: '16px' }}>
                    <button
                        className="btn btn-primary"
                        onClick={() => onSave(entityId, {
                            name,
                            dataType,
                            isRequired,
                            isUnique,
                            isPrimaryKey: initialValues?.isPrimaryKey || false,
                            displayOrder: initialValues?.displayOrder ?? 0,
                            relatedEntityId: dataType === 'Relationship' ? relatedEntityId : undefined,
                            relationshipType: dataType === 'Relationship' ? relationshipType : undefined
                        })}
                        disabled={!name || (dataType === 'Relationship' && !relatedEntityId)}
                    >
                        {initialValues ? 'Сохранить' : 'Создать'}
                    </button>
                    <button className="btn btn-secondary" onClick={onClose}>Отмена</button>
                </div>
            </div>
        </div>
    );
}

export default Dashboard;

/**
 * Нормализует authConfig из PascalCase (старые записи в БД) или camelCase (новые)
 * в единый формат camelCase.
 * Решает проблему, когда auth казался отключённым после перезагрузки, потому что
 * старые записи хранились с ключами PascalCase ({"Enabled":true}), а фронт ожидал camelCase.
 */
function parseAuthConfig(authConfigJson: string | null | undefined): any | null {
    if (!authConfigJson) return null;
    try {
        const raw = JSON.parse(authConfigJson);
        if (!raw) return null;
        // Нормализация: поддержка как Enabled/enabled, EntityProtection/entityProtection и т.д.
        const get = (obj: any, camel: string, pascal: string) =>
            obj[camel] !== undefined ? obj[camel] : obj[pascal];

        // Нормализация записей entityProtection
        const rawProt = get(raw, 'entityProtection', 'EntityProtection') || {};
        const entityProtection: Record<string, any> = {};
        for (const [key, val] of Object.entries(rawProt as Record<string, any>)) {
            entityProtection[key] = {
                get: get(val, 'get', 'Get') ?? false,
                post: get(val, 'post', 'Post') ?? false,
                put: get(val, 'put', 'Put') ?? false,
                patch: get(val, 'patch', 'Patch') ?? false,
                delete: get(val, 'delete', 'Delete') ?? false,
            };
        }

        return {
            enabled: get(raw, 'enabled', 'Enabled') ?? false,
            type: get(raw, 'type', 'Type') ?? 'JWT',
            userIdentifier: get(raw, 'userIdentifier', 'UserIdentifier') ?? 'email',
            enableRoles: get(raw, 'enableRoles', 'EnableRoles') ?? false,
            roles: get(raw, 'roles', 'Roles') ?? ['User', 'Admin'],
            enableRefreshTokens: get(raw, 'enableRefreshTokens', 'EnableRefreshTokens') ?? true,
            enableEmailVerification: get(raw, 'enableEmailVerification', 'EnableEmailVerification') ?? false,
            tokenExpiryMinutes: get(raw, 'tokenExpiryMinutes', 'TokenExpiryMinutes') ?? 60,
            refreshTokenExpiryDays: get(raw, 'refreshTokenExpiryDays', 'RefreshTokenExpiryDays') ?? 7,
            entityProtection,
        };
    } catch {
        return null;
    }
}

function AuthConfigModal({ currentConfig, stack, entities, onClose, onSave }: {
    currentConfig: any | null;
    stack: string;
    entities: Entity[];
    onClose: () => void;
    onSave: (cfg: any | null) => void;
}) {
    // ── Время жизни токена с выбором единиц ─────────────────────────────
    type ExpiryUnit = 'minutes' | 'hours' | 'days' | 'months';
    const unitToMinutes = (val: number, unit: ExpiryUnit) => {
        if (unit === 'hours') return val * 60;
        if (unit === 'days') return val * 1440;
        if (unit === 'months') return val * 43200;
        return val;
    };
    const minutesToUnit = (mins: number): [number, ExpiryUnit] => {
        if (mins % 43200 === 0 && mins >= 43200) return [mins / 43200, 'months'];
        if (mins % 1440 === 0 && mins >= 1440) return [mins / 1440, 'days'];
        if (mins % 60 === 0 && mins >= 60) return [mins / 60, 'hours'];
        return [mins, 'minutes'];
    };
    const initMins = currentConfig?.tokenExpiryMinutes ?? 60;
    const [initVal, initUnit] = minutesToUnit(initMins);

    const [enabled, setEnabled] = useState<boolean>(currentConfig?.enabled ?? false);
    const [userIdentifier, setUserIdentifier] = useState<'email' | 'username' | 'both'>(currentConfig?.userIdentifier ?? 'email');
    const [enableRoles, setEnableRoles] = useState<boolean>(currentConfig?.enableRoles ?? false);
    const [roles, setRoles] = useState<string>(currentConfig?.roles?.join(', ') ?? 'User, Admin');
    const [enableRefreshTokens, setEnableRefreshTokens] = useState<boolean>(currentConfig?.enableRefreshTokens ?? true);
    const [enableEmailVerification, setEnableEmailVerification] = useState<boolean>(currentConfig?.enableEmailVerification ?? false);
    const [tokenExpiryValue, setTokenExpiryValue] = useState<number>(initVal);
    const [tokenExpiryUnit, setTokenExpiryUnit] = useState<ExpiryUnit>(initUnit);
    const [refreshTokenExpiryDays, setRefreshTokenExpiryDays] = useState<number>(currentConfig?.refreshTokenExpiryDays ?? 7);

    // ── Защита маршрутов по сущностям ────────────────────────────────────
    type Methods = { get: boolean; post: boolean; put: boolean; patch: boolean; delete: boolean };
    const allMethods: Methods = { get: true, post: true, put: true, patch: true, delete: true };
    const noMethods: Methods = { get: false, post: false, put: false, patch: false, delete: false };
    const initProtection = (): Record<string, Methods> => {
        const existing = currentConfig?.entityProtection || {};
        const result: Record<string, Methods> = {};
        entities.forEach(e => {
            result[e.name] = existing[e.name] ?? { ...noMethods };
        });
        return result;
    };
    const [entityProtection, setEntityProtection] = useState<Record<string, Methods>>(initProtection);

    const toggleMethod = (entityName: string, method: keyof Methods) => {
        setEntityProtection(prev => ({
            ...prev,
            [entityName]: { ...prev[entityName], [method]: !prev[entityName][method] }
        }));
    };
    const toggleAllForEntity = (entityName: string, val: boolean) => {
        setEntityProtection(prev => ({ ...prev, [entityName]: val ? { ...allMethods } : { ...noMethods } }));
    };

    const handleSave = () => {
        if (!enabled) { onSave(null); return; }
        onSave({
            enabled: true,
            userIdentifier,
            enableRoles,
            roles: roles.split(',').map((r: string) => r.trim()).filter(Boolean),
            entityProtection,
            enableRefreshTokens,
            enableEmailVerification,
            tokenExpiryMinutes: unitToMinutes(tokenExpiryValue, tokenExpiryUnit),
            refreshTokenExpiryDays,
        });
    };

    const methodLabels: { key: keyof Methods; label: string; color: string }[] = [
        { key: 'get',    label: 'GET',    color: '#10b981' },
        { key: 'post',   label: 'POST',   color: '#3b82f6' },
        { key: 'put',    label: 'PUT',    color: '#f59e0b' },
        { key: 'patch',  label: 'PATCH',  color: '#8b5cf6' },
        { key: 'delete', label: 'DELETE', color: '#ef4444' },
    ];

    return (
        <div className="modal-overlay" onClick={onClose}>
            <div className="modal" style={{ maxWidth: '600px', maxHeight: '92vh', overflowY: 'auto' }} onClick={(e) => e.stopPropagation()}>
                <h2>🔐 Настройка аутентификации</h2>
                <p style={{ color: 'var(--text-secondary)', fontSize: '0.875rem', marginBottom: '1rem' }}>
                    Стек: <strong>{stack === 'CSharp_PostgreSQL' ? 'C# + PostgreSQL (JWT Bearer)' : 'Node.js + MongoDB (JWT)'}</strong>
                </p>

                <div className="checkbox-group" style={{ marginBottom: '1.25rem' }}>
                    <label style={{ fontSize: '1rem', fontWeight: 600 }}>
                        <input type="checkbox" checked={enabled} onChange={(e) => setEnabled(e.target.checked)} />
                        Включить аутентификацию
                    </label>
                </div>

                {enabled && (
                    <>
                        {/* Идентификатор пользователя */}
                        <div className="form-group">
                            <label>Идентификатор пользователя</label>
                            <select value={userIdentifier} onChange={(e) => setUserIdentifier(e.target.value as any)}>
                                <option value="email">Email</option>
                                <option value="username">Username</option>
                                <option value="both">Email + Username</option>
                            </select>
                        </div>

                        {/* Время жизни access-токена */}
                        <div className="form-group">
                            <label>Время жизни Access Token</label>
                            <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                                <input
                                    type="number" min={1} max={999}
                                    value={tokenExpiryValue}
                                    onChange={(e) => setTokenExpiryValue(Math.max(1, parseInt(e.target.value) || 1))}
                                    style={{ width: '90px' }}
                                />
                                <select value={tokenExpiryUnit} onChange={(e) => setTokenExpiryUnit(e.target.value as ExpiryUnit)} style={{ width: '130px' }}>
                                    <option value="minutes">Минуты</option>
                                    <option value="hours">Часы</option>
                                    <option value="days">Дни</option>
                                    <option value="months">Месяцы</option>
                                </select>
                                <span style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
                                    = {unitToMinutes(tokenExpiryValue, tokenExpiryUnit)} мин.
                                </span>
                            </div>
                        </div>

                        <div className="checkbox-group" style={{ flexDirection: 'column', gap: '0.5rem', marginBottom: '1rem' }}>
                            <label>
                                <input type="checkbox" checked={enableRefreshTokens} onChange={(e) => setEnableRefreshTokens(e.target.checked)} />
                                Refresh Token (долгосрочный токен)
                            </label>
                            {enableRefreshTokens && (
                                <div className="form-group" style={{ marginLeft: '1.5rem', marginBottom: 0 }}>
                                    <label style={{ fontSize: '0.8rem' }}>Время жизни Refresh Token (дней)</label>
                                    <input type="number" min={1} max={365} value={refreshTokenExpiryDays}
                                        onChange={(e) => setRefreshTokenExpiryDays(parseInt(e.target.value) || 7)}
                                        style={{ width: '120px' }} />
                                </div>
                            )}
                            <label>
                                <input type="checkbox" checked={enableRoles} onChange={(e) => setEnableRoles(e.target.checked)} />
                                Роли пользователей
                            </label>
                            {enableRoles && (
                                <div className="form-group" style={{ marginLeft: '1.5rem', marginBottom: 0 }}>
                                    <label style={{ fontSize: '0.8rem' }}>Роли (через запятую)</label>
                                    <input value={roles} onChange={(e) => setRoles(e.target.value)} placeholder="User, Admin, Moderator" />
                                </div>
                            )}
                            <label>
                                <input type="checkbox" checked={enableEmailVerification} onChange={(e) => setEnableEmailVerification(e.target.checked)} />
                                Email верификация (поле в модели)
                            </label>
                        </div>

                        {/* Защита маршрутов по сущностям */}
                        {entities.length > 0 && (
                            <div style={{ marginBottom: '1rem' }}>
                                <label style={{ fontWeight: 600, display: 'block', marginBottom: '8px' }}>
                                    🔒 Защита маршрутов по сущностям
                                </label>
                                <p style={{ fontSize: '0.78rem', color: 'var(--text-secondary)', marginBottom: '10px' }}>
                                    Выберите какие HTTP-методы требуют авторизации для каждой сущности.
                                </p>
                                <div style={{ border: '1px solid var(--border-color)', borderRadius: '8px', overflow: 'hidden' }}>
                                    {/* Заголовок таблицы */}
                                    <div style={{ display: 'grid', gridTemplateColumns: '1fr repeat(5,56px) 56px', background: 'var(--bg-secondary)', padding: '6px 10px', fontSize: '0.75rem', fontWeight: 700, color: 'var(--text-secondary)', gap: '2px' }}>
                                        <span>Сущность</span>
                                        {methodLabels.map(m => <span key={m.key} style={{ textAlign: 'center', color: m.color }}>{m.label}</span>)}
                                        <span style={{ textAlign: 'center' }}>Все</span>
                                    </div>
                                    {entities.map((entity, idx) => {
                                        const prot = entityProtection[entity.name] || noMethods;
                                        const allOn = Object.values(prot).every(Boolean);
                                        return (
                                            <div key={entity.name} style={{ display: 'grid', gridTemplateColumns: '1fr repeat(5,56px) 56px', padding: '7px 10px', gap: '2px', alignItems: 'center', background: idx % 2 === 0 ? 'transparent' : 'rgba(0,0,0,0.03)', borderTop: '1px solid var(--border-color)' }}>
                                                <span style={{ fontWeight: 500, fontSize: '0.85rem' }}>{entity.name}</span>
                                                {methodLabels.map(m => (
                                                    <div key={m.key} style={{ display: 'flex', justifyContent: 'center' }}>
                                                        <input type="checkbox"
                                                            checked={prot[m.key]}
                                                            onChange={() => toggleMethod(entity.name, m.key)}
                                                            style={{ accentColor: m.color, width: '16px', height: '16px', cursor: 'pointer' }}
                                                        />
                                                    </div>
                                                ))}
                                                <div style={{ display: 'flex', justifyContent: 'center' }}>
                                                    <input type="checkbox"
                                                        checked={allOn}
                                                        onChange={(e) => toggleAllForEntity(entity.name, e.target.checked)}
                                                        style={{ width: '16px', height: '16px', cursor: 'pointer' }}
                                                    />
                                                </div>
                                            </div>
                                        );
                                    })}
                                </div>
                            </div>
                        )}

                        <div style={{ padding: '0.75rem', background: 'rgba(99,102,241,0.08)', borderRadius: '8px', fontSize: '0.8rem', color: 'var(--text-secondary)', marginBottom: '1rem' }}>
                            <strong>Будет сгенерировано:</strong> User модель, AuthController (register / login / refresh / me / logout / changePassword),
                            TokenService, AuthService, JWT в {stack === 'CSharp_PostgreSQL' ? 'Program.cs + appsettings.json' : 'app.js + .env'}.
                        </div>
                    </>
                )}

                <div style={{ display: 'flex', gap: '10px' }}>
                    <button className="btn btn-primary" onClick={handleSave}>
                        {enabled ? '💾 Сохранить' : '🔓 Отключить и сохранить'}
                    </button>
                    <button className="btn btn-secondary" onClick={onClose}>Отмена</button>
                </div>
            </div>
        </div>
    );
}


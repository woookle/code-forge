import { useEffect, useState } from 'react';
import { useAppDispatch, useAppSelector } from '../app/hooks';
import { fetchProjects, fetchProjectById, createProject, deleteProject, generateProject } from '../features/projects/projectsSlice';
import { logout } from '../features/auth/authSlice';
import { Project, Entity, Field } from '../types';
import api from '../utils/api';
import { Page } from '../App';
import { toast } from 'react-toastify';
import { useConfirm } from '../context/ConfirmContext';
import FAQWidget from './FAQWidget';

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

    const [selectedEntityId, setSelectedEntityId] = useState<string | null>(null);

    const [isSidebarOpen, setIsSidebarOpen] = useState(false);

    useEffect(() => {
        dispatch(fetchProjects());
    }, [dispatch]);

    // Close sidebar when project is selected (on mobile)
    const handleSelectProject = (project: Project) => {
        dispatch(fetchProjectById(project.id));
        setIsSidebarOpen(false);
    };

    const handleNewProject = async (name: string, targetStack: 'CSharp_PostgreSQL' | 'NodeJS_MongoDB') => {
        try {
            await dispatch(createProject({ name, description: '', targetStack })).unwrap();
            setShowNewProject(false);
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

    const handleNewEntity = async (name: string) => {
        if (!currentProject) return;

        try {
            await api.post(`/entities/project/${currentProject.id}`, {
                name,
                description: '',
                displayOrder: currentProject.entities?.length || 0
            });
            dispatch(fetchProjectById(currentProject.id));
            setShowNewEntity(false);
            toast.success('Сущность создана');
        } catch (error) {
            toast.error('Ошибка создания сущности');
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

    const handleDeleteRelationship = async (id: string) => {
        if (await confirm({
            title: 'Удаление связи',
            message: 'Вы уверены, что хотите удалить эту связь?',
            confirmText: 'Удалить',
            type: 'danger'
        })) {
            try {
                await api.delete(`/relationships/${id}`);
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
            toast.success('Проект успешно сгенерирован');
        } catch (error) {
            toast.error('Ошибка генерации проекта');
        }
    };

    const handleLogout = () => {
        dispatch(logout());
        toast.info('Вы вышли из системы');
    };

    return (
        <div className="dashboard animate-fade-in">
            {/* Mobile Header */}
            <div className="mobile-header">
                <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                    <img src={user?.isDarkMode ? "/logo_white.svg" : "/logo.svg"} alt="CodeForge" style={{ height: '32px', width: 'auto' }} />
                    <span style={{ fontSize: '1rem', fontWeight: 'bold' }}>CodeForge</span>
                </div>
                <button className="menu-btn" onClick={() => setIsSidebarOpen(true)}>
                    ☰
                </button>
            </div>

            {/* Sidebar Overlay */}
            <div
                className={`sidebar-overlay ${isSidebarOpen ? 'visible' : ''}`}
                onClick={() => setIsSidebarOpen(false)}
            />

            <div className={`sidebar animate-slide-up ${isSidebarOpen ? 'open' : ''}`}>
                <div className="sidebar-header" style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '2rem' }}>
                    <img src={user?.isDarkMode ? "/logo_white.svg" : "/logo.svg"} alt="CodeForge" style={{ height: '40px', width: 'auto' }} />
                    <span style={{ fontSize: '1.25rem', fontWeight: 'bold', letterSpacing: '-0.5px' }}>CodeForge</span>
                </div>
                <h2>Мои Проекты</h2>
                <button className="btn btn-primary hover-scale" onClick={() => setShowNewProject(true)}>
                    + Новый проект
                </button>

                <ul className="project-list">
                    {projects.map((project, index) => (
                        <li
                            key={project.id}
                            className={`project-item ${currentProject?.id === project.id ? 'active' : ''}`}
                            onClick={() => handleSelectProject(project)}
                            style={{ animationDelay: `${index * 50}ms` }}
                        >
                            <h3>{project.name}</h3>
                            <div className="stack-badge">{project.targetStack}</div>
                        </li>
                    ))}
                </ul>

                <div style={{ marginTop: 'auto', paddingTop: '1rem' }}>
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
                    <h1>{currentProject ? currentProject.name : 'Выберите проект'}</h1>
                    <div className="user-controls">
                        {user?.role === 'Admin' && (
                            <button className="btn btn-warning btn-small" onClick={() => onNavigate('admin')}>
                                Админ Панель
                            </button>
                        )}

                        <div
                            className="user-profile-summary hover-scale"
                            onClick={() => onNavigate('profile')}
                            title="Перейти в профиль"
                            style={{
                                display: 'flex',
                                alignItems: 'center',
                                gap: '10px',
                                cursor: 'pointer',
                                padding: '5px 10px',
                                borderRadius: '8px',
                                backgroundColor: 'rgba(255, 255, 255, 0.1)',
                                transition: 'all 0.2s',
                            }}
                        >
                            <div className="avatar-circle" style={{
                                width: '32px',
                                height: '32px',
                                borderRadius: '50%',
                                overflow: 'hidden',
                                backgroundColor: '#ddd', // Fallback color
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                                border: '2px solid rgba(255,255,255,0.2)'
                            }}>
                                {user?.avatarUrl ? (
                                    <img
                                        src={`${import.meta.env.VITE_IMG_URL}${user.avatarUrl}`}
                                        alt="Avatar"
                                        style={{ width: '100%', height: '100%', objectFit: 'cover' }}
                                    />
                                ) : (
                                    <span style={{ fontSize: '14px', fontWeight: 'bold', color: '#555' }}>
                                        {user?.email?.charAt(0).toUpperCase()}
                                    </span>
                                )}
                            </div>
                            <span style={{ fontWeight: '500' }}>{user?.email}</span>
                        </div>

                        <button className="btn btn-secondary btn-small" onClick={handleLogout}>
                            Выйти
                        </button>
                    </div>
                </div>

                {currentProject && (
                    <>
                        <div className="flex gap-2 mb-4">
                            <button className="btn btn-primary" onClick={() => setShowNewEntity(true)}>
                                + Новая сущность
                            </button>
                            <button className="btn btn-success" onClick={handleGenerate}>
                                📦 Скачать проект
                            </button>
                            <button className="btn btn-danger" onClick={() => handleDeleteProject(currentProject.id)}>
                                🗑 Удалить проект
                            </button>
                        </div>

                        {currentProject.entities?.map((entity: Entity) => (
                            <div key={entity.id} className="entity-card">
                                <div className="entity-header">
                                    <h3>{entity.name}</h3>
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

                                {/* Relationships Display */}
                                {entity.sourceRelationships && entity.sourceRelationships.length > 0 && (
                                    <div className="relationships-list">
                                        <strong>Исходящие связи:</strong>
                                        <ul>
                                            {entity.sourceRelationships.map((rel: any) => (
                                                <li key={rel.id} className="relationship-item">
                                                    <span>
                                                        {rel.relationshipType}: {rel.targetEntity?.name || 'Неизвестно'}
                                                        <span className="text-secondary" style={{ marginLeft: '5px' }}>({rel.sourceFieldName})</span>
                                                    </span>
                                                    <button
                                                        className="btn btn-danger btn-small"
                                                        onClick={() => handleDeleteRelationship(rel.id)}
                                                    >
                                                        x
                                                    </button>
                                                </li>
                                            ))}
                                        </ul>
                                    </div>
                                )}
                            </div>
                        ))}

                        {(!currentProject.entities || currentProject.entities.length === 0) && (
                            <div className="empty-state">
                                Нет сущностей. Создайте первую сущность для вашего проекта.
                            </div>
                        )}
                    </>
                )}

                {!currentProject && (
                    <div className="empty-state">
                        Выберите проект из списка слева или создайте новый проект.
                    </div>
                )}
            </div>

            {/* New Project Modal */}
            {showNewProject && <NewProjectModal onClose={() => setShowNewProject(false)} onCreate={handleNewProject} />}

            {/* New Entity Modal */}
            {showNewEntity && <NewEntityModal onClose={() => setShowNewEntity(false)} onCreate={handleNewEntity} />}

            {/* Field Modal */}
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


            {/* FAQ Widget */}
            <FAQWidget />
        </div>
    );
}

// Modal Components
function NewProjectModal({ onClose, onCreate }: any) {
    const [name, setName] = useState('');
    const [stack, setStack] = useState<'CSharp_PostgreSQL' | 'NodeJS_MongoDB'>('CSharp_PostgreSQL');

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
                <div style={{ display: 'flex', gap: '10px' }}>
                    <button className="btn btn-primary" onClick={() => onCreate(name, stack)} disabled={!name}>
                        Создать
                    </button>
                    <button className="btn btn-secondary" onClick={onClose}>Отмена</button>
                </div>
            </div>
        </div>
    );
}

function NewEntityModal({ onClose, onCreate }: any) {
    const [name, setName] = useState('');

    return (
        <div className="modal-overlay" onClick={onClose}>
            <div className="modal" onClick={(e) => e.stopPropagation()}>
                <h2>Новая сущность</h2>
                <div className="form-group">
                    <label>Название</label>
                    <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Product" />
                </div>
                <div style={{ display: 'flex', gap: '10px' }}>
                    <button className="btn btn-primary" onClick={() => onCreate(name)} disabled={!name}>
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

    // Filter out current entity from potential relationship targets (optional)
    const availableTargets = entities.filter((e: Entity) => e.id !== entityId);

    const handleRelatedEntityChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
        const selectedId = e.target.value;
        setRelatedEntityId(selectedId);

        // Auto-generate name if empty or default
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
                        <option value="Boolean">Boolean</option>
                        <option value="DateTime">DateTime</option>
                        <option value="Decimal">Decimal</option>
                        <option value="Text">Text</option>
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
                <div style={{ display: 'flex', gap: '10px' }}>
                    <button
                        className="btn btn-primary"
                        onClick={() => onSave(entityId, {
                            name,
                            dataType,
                            isRequired,
                            isUnique,
                            isPrimaryKey: initialValues?.isPrimaryKey || false,
                            displayOrder: initialValues?.displayOrder || 0,
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

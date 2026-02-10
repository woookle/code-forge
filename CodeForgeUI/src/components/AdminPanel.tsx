import { useEffect, useState } from 'react';
import api from '../utils/api';
import { User } from '../types/auth';
import { Project } from '../types';
import { toast } from 'react-toastify';
import { ClipLoader } from 'react-spinners';
import { Page } from '../App';
import { useConfirm } from '../context/ConfirmContext';

interface AdminPanelProps {
    onNavigate: (page: Page) => void;
}

function AdminPanel({ onNavigate }: AdminPanelProps) {
    const [users, setUsers] = useState<User[]>([]);
    const [loading, setLoading] = useState(true);
    const [selectedUser, setSelectedUser] = useState<User | null>(null);
    const [selectedAdminProject, setSelectedAdminProject] = useState<Project | null>(null);
    const [isEditUserModalOpen, setIsEditUserModalOpen] = useState(false);
    const [editingUser, setEditingUser] = useState<User | null>(null);
    const [editFormData, setEditFormData] = useState({ firstName: '', lastName: '', avatarUrl: '' });
    const { confirm } = useConfirm();

    // Sidebar state for mobile
    const [isSidebarOpen, setIsSidebarOpen] = useState(false);

    useEffect(() => {
        fetchUsers();
    }, []);

    const fetchUsers = async () => {
        try {
            const response = await api.get<User[]>('/users');
            setUsers(response.data);
        } catch (error) {
            toast.error('Не удалось загрузить пользователей');
        } finally {
            setLoading(false);
        }
    };

    const handleDeleteUser = async (id: string, email: string) => {
        if (await confirm({
            title: 'Удаление пользователя',
            message: `Вы уверены, что хотите удалить пользователя ${email}? Все его проекты будут удалены.`,
            confirmText: 'Удалить',
            type: 'danger'
        })) {
            try {
                await api.delete(`/users/${id}`);
                setUsers(users.filter(u => u.id !== id));
                toast.success('Пользователь успешно удален');
            } catch (error: any) {
                toast.error(error.response?.data || 'Не удалось удалить пользователя');
            }
        }
    };

    const handleDeleteProject = async (projectId: string) => {
        if (await confirm({
            title: 'Удаление проекта',
            message: 'Вы уверены, что хотите удалить этот проект?',
            confirmText: 'Удалить',
            type: 'danger'
        })) {
            try {
                await api.delete(`/projects/${projectId}`);
                if (selectedUser) {
                    const updatedProjects = selectedUser.projects?.filter(p => p.id !== projectId);
                    const updatedUser = { ...selectedUser, projects: updatedProjects };
                    setSelectedUser(updatedUser);
                    setUsers(users.map(u => u.id === selectedUser.id ? updatedUser : u));
                }
                toast.success('Проект успешно удален');
            } catch (error: any) {
                toast.error('Не удалось удалить проект');
            }
        }
    };

    const openEditUserModal = (user: User) => {
        console.log(user);
        setEditingUser(user);
        setEditFormData({
            firstName: user.firstName || '',
            lastName: user.lastName || '',
            avatarUrl: user.avatarUrl || ''
        });
        setIsEditUserModalOpen(true);
        setIsSidebarOpen(false); // Close sidebar if open
    };

    const handleUpdateUser = async () => {
        if (!editingUser) return;

        try {
            await api.put(`/users/${editingUser.id}`, editFormData);

            const updatedUsers = users.map(u =>
                u.id === editingUser.id
                    ? { ...u, ...editFormData }
                    : u
            );
            setUsers(updatedUsers);
            setIsEditUserModalOpen(false);
            setEditingUser(null);
            toast.success('Пользователь обновлен');
        } catch (error) {
            toast.error('Ошибка обновления пользователя');
        }
    };

    if (loading) {
        return (
            <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh' }}>
                <ClipLoader color="#000000" size={50} />
            </div>
        );
    }

    return (
        <div className="dashboard animate-fade-in">
            {/* Mobile Header */}
            <div className="mobile-header">
                <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                    <img src="/logo.svg" alt="CodeForge" style={{ height: '32px', width: 'auto' }} />
                    <span style={{ fontSize: '1rem', fontWeight: 'bold' }}>Admin</span>
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
                <h2>Админ Панель</h2>
                <button className="btn btn-secondary" onClick={() => onNavigate('dashboard')}>
                    ← Назад в дешборд
                </button>
            </div>

            <div className="main-content">
                <div className="header">
                    <h1>Управление пользователями</h1>
                    <div className="user-controls">
                        <span className="badge badge-primary" style={{ padding: '0.25rem 0.5rem', background: '#e5e7eb', borderRadius: '4px', fontSize: '0.8rem' }}>Режим Админа</span>
                    </div>
                </div>

                <div className="login-card animate-slide-up" style={{ maxWidth: '100%', padding: '1.5rem', overflowX: 'auto' }}>
                    <div className="table-container">
                        <table style={{ width: '100%', borderCollapse: 'collapse', minWidth: '800px' }}>
                            <thead>
                                <tr style={{ borderBottom: '1px solid #eee', textAlign: 'left' }}>
                                    <th style={{ padding: '10px' }}>Email</th>
                                    <th style={{ padding: '10px' }}>Имя</th>
                                    <th style={{ padding: '10px' }}>Роль</th>
                                    <th style={{ padding: '10px' }}>Дата регистрации</th>
                                    <th style={{ padding: '10px' }}>Проекты</th>
                                    <th style={{ padding: '10px' }}>Действия</th>
                                </tr>
                            </thead>
                            <tbody>
                                {users.map(user => (
                                    <tr key={user.id} style={{ borderBottom: '1px solid #f9f9f9' }}>
                                        <td style={{ padding: '10px' }}>
                                            <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                                                {user.avatarUrl ? (
                                                    <img src={'http://localhost:5123' + user.avatarUrl} alt="avatar" style={{ width: '30px', height: '30px', borderRadius: '50%', objectFit: 'cover' }} />
                                                ) : (
                                                    <div style={{ width: '30px', height: '30px', borderRadius: '50%', backgroundColor: '#eee' }} />
                                                )}
                                                {user.email}
                                            </div>
                                        </td>
                                        <td style={{ padding: '10px' }}>{user.firstName} {user.lastName}</td>
                                        <td style={{ padding: '10px' }}>
                                            <span style={{
                                                padding: '4px 8px',
                                                borderRadius: '4px',
                                                backgroundColor: user.role === 'Admin' ? '#000' : '#eee',
                                                color: user.role === 'Admin' ? '#fff' : '#000',
                                                fontSize: '0.8rem'
                                            }}>
                                                {user.role}
                                            </span>
                                        </td>
                                        <td style={{ padding: '10px' }}>{new Date(user.createdAt || Date.now()).toLocaleDateString()}</td>
                                        <td style={{ padding: '10px' }}>
                                            <button
                                                className="btn btn-secondary btn-small"
                                                onClick={() => {
                                                    setSelectedUser(user);
                                                    setIsSidebarOpen(false);
                                                }}
                                            >
                                                Просмотр ({user.projects?.length || 0})
                                            </button>
                                            <button
                                                className="btn btn-primary btn-small"
                                                onClick={() => openEditUserModal(user)}
                                                style={{ marginLeft: '5px' }}
                                            >
                                                Ред.
                                            </button>
                                        </td>
                                        <td style={{ padding: '10px' }}>
                                            {user.role !== 'Admin' && (
                                                <button
                                                    className="btn btn-danger btn-small"
                                                    onClick={() => handleDeleteUser(user.id, user.email)}
                                                >
                                                    Удалить
                                                </button>
                                            )}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </div>
            </div>

            {/* Modal for User Projects */}
            {selectedUser && (
                <div className="modal-overlay" onClick={() => {
                    setSelectedUser(null);
                    setSelectedAdminProject(null);
                }}>
                    <div className="modal" onClick={(e) => e.stopPropagation()} style={{ minWidth: '300px', width: '90%', maxWidth: '900px', maxHeight: '80vh', overflowY: 'auto' }}>

                        {!selectedAdminProject ? (
                            <>
                                <h2>Проекты пользователя {selectedUser.email}</h2>
                                {selectedUser.projects && selectedUser.projects.length > 0 ? (
                                    <div className="table-container" style={{ overflowX: 'auto' }}>
                                        <table style={{ width: '100%', borderCollapse: 'collapse', marginTop: '1rem', minWidth: '600px' }}>
                                            <thead>
                                                <tr style={{ borderBottom: '1px solid #eee', textAlign: 'left' }}>
                                                    <th style={{ padding: '10px' }}>Название</th>
                                                    <th style={{ padding: '10px' }}>Стек</th>
                                                    <th style={{ padding: '10px' }}>Сущностей</th>
                                                    <th style={{ padding: '10px' }}>Создан</th>
                                                    <th style={{ padding: '10px' }}>Действия</th>
                                                </tr>
                                            </thead>
                                            <tbody>
                                                {selectedUser.projects.map((project: Project) => (
                                                    <tr key={project.id} style={{ borderBottom: '1px solid #f9f9f9' }}>
                                                        <td style={{ padding: '10px' }}>{project.name}</td>
                                                        <td style={{ padding: '10px' }}>{project.targetStack}</td>
                                                        <td style={{ padding: '10px' }}>{project.entities?.length || 0}</td>
                                                        <td style={{ padding: '10px' }}>{new Date(project.createdAt).toLocaleDateString()}</td>
                                                        <td style={{ padding: '10px' }}>
                                                            <div style={{ display: 'flex', gap: '5px' }}>
                                                                <button
                                                                    className="btn btn-primary btn-small"
                                                                    onClick={() => setSelectedAdminProject(project)}
                                                                >
                                                                    Сущности
                                                                </button>
                                                                <button
                                                                    className="btn btn-danger btn-small"
                                                                    onClick={() => handleDeleteProject(project.id)}
                                                                >
                                                                    Удалить
                                                                </button>
                                                            </div>
                                                        </td>
                                                    </tr>
                                                ))}
                                            </tbody>
                                        </table>
                                    </div>
                                ) : (
                                    <p style={{ padding: '1rem', textAlign: 'center', color: '#666' }}>У этого пользователя нет проектов</p>
                                )}
                            </>
                        ) : (
                            <>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '1rem' }}>
                                    <button
                                        className="btn btn-secondary btn-small"
                                        onClick={() => setSelectedAdminProject(null)}
                                    >
                                        ← Назад
                                    </button>
                                    <h2>Сущности проекта: {selectedAdminProject.name}</h2>
                                </div>

                                {selectedAdminProject.entities && selectedAdminProject.entities.length > 0 ? (
                                    <div className="table-container" style={{ overflowX: 'auto' }}>
                                        <table style={{ width: '100%', borderCollapse: 'collapse', minWidth: '500px' }}>
                                            <thead>
                                                <tr style={{ borderBottom: '1px solid #eee', textAlign: 'left' }}>
                                                    <th style={{ padding: '10px' }}>Название</th>
                                                    <th style={{ padding: '10px' }}>Полей</th>
                                                    <th style={{ padding: '10px' }}>Связей</th>
                                                    <th style={{ padding: '10px' }}>Создана</th>
                                                </tr>
                                            </thead>
                                            <tbody>
                                                {selectedAdminProject.entities.map(entity => (
                                                    <tr key={entity.id} style={{ borderBottom: '1px solid #f9f9f9' }}>
                                                        <td style={{ padding: '10px' }}>{entity.name}</td>
                                                        <td style={{ padding: '10px' }}>{entity.fields?.length || 0}</td>
                                                        <td style={{ padding: '10px' }}>{(entity.sourceRelationships?.length || 0) + (entity.targetRelationships?.length || 0)}</td>
                                                        <td style={{ padding: '10px' }}>{new Date(entity.createdAt).toLocaleDateString()}</td>
                                                    </tr>
                                                ))}
                                            </tbody>
                                        </table>
                                    </div>
                                ) : (
                                    <p style={{ padding: '1rem', textAlign: 'center', color: '#666' }}>В этом проекте нет сущностей</p>
                                )}
                            </>
                        )}

                        <div style={{ marginTop: '20px', textAlign: 'right' }}>
                            <button className="btn btn-secondary" onClick={() => {
                                setSelectedUser(null);
                                setSelectedAdminProject(null);
                            }}>
                                Закрыть
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Edit User Modal */}
            {isEditUserModalOpen && editingUser && (
                <div className="modal-overlay" onClick={() => setIsEditUserModalOpen(false)}>
                    <div className="modal" onClick={(e) => e.stopPropagation()}>
                        <h2>Редактирование пользователя</h2>
                        <div className="form-group">
                            <label>Email</label>
                            <input value={editingUser.email} disabled style={{ backgroundColor: '#f0f0f0' }} />
                        </div>
                        <div className="form-group">
                            <label>Имя</label>
                            <input
                                value={editFormData.firstName}
                                onChange={(e) => setEditFormData({ ...editFormData, firstName: e.target.value })}
                            />
                        </div>
                        <div className="form-group">
                            <label>Фамилия</label>
                            <input
                                value={editFormData.lastName}
                                onChange={(e) => setEditFormData({ ...editFormData, lastName: e.target.value })}
                            />
                        </div>
                        <div className="form-group">
                            <label>Аватар</label>
                            <div style={{ display: 'flex', alignItems: 'center', gap: '15px' }}>
                                <div style={{
                                    maxWidth: '60px',
                                    height: '60px',
                                    borderRadius: '50%',
                                    overflow: 'hidden',
                                    backgroundColor: '#eee',
                                    border: '1px solid #ddd'
                                }}>
                                    {(editFormData.avatarUrl || editingUser.avatarUrl) ? (
                                        <img
                                            src={`http://localhost:5123${editFormData.avatarUrl || editingUser.avatarUrl}`}
                                            alt="avatar"
                                            style={{ width: '100%', height: '100%', objectFit: 'cover' }}
                                        />
                                    ) : (
                                        <div style={{ width: '100%', height: '100%', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                                            👤
                                        </div>
                                    )}
                                </div>
                                <div>
                                    <input
                                        type="file"
                                        accept="image/*"
                                        onChange={async (e) => {
                                            const file = e.target.files?.[0];
                                            if (!file) return;

                                            const formData = new FormData();
                                            formData.append('file', file);

                                            try {
                                                const response = await api.post(`/users/${editingUser.id}/avatar`, formData, {
                                                    headers: { 'Content-Type': 'multipart/form-data' }
                                                });
                                                setEditFormData({ ...editFormData, avatarUrl: response.data.avatarUrl });
                                                toast.success('Аватар загружен');
                                            } catch (error) {
                                                toast.error('Ошибка загрузки аватара');
                                            }
                                        }}
                                    />
                                    <p style={{ fontSize: '0.8rem', color: '#666', marginTop: '5px' }}>
                                        Выберите файл для загрузки нового аватара
                                    </p>
                                </div>
                            </div>
                        </div>
                        <div style={{ display: 'flex', gap: '10px' }}>
                            <button className="btn btn-primary" onClick={handleUpdateUser}>
                                Сохранить
                            </button>
                            <button className="btn btn-secondary" onClick={() => setIsEditUserModalOpen(false)}>
                                Отмена
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}

export default AdminPanel;

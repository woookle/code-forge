import { useEffect, useState, useMemo } from 'react';
import api from '../../utils/api';
import { User } from '../../types/auth';
import { Project } from '../../types';
import { toast } from 'react-toastify';
import { ClipLoader } from 'react-spinners';
import { Page } from '../../App';
import { useConfirm } from '../../context/ConfirmContext';

interface AdminPanelProps {
    onNavigate: (page: Page) => void;
}

const VITE_IMG_URL = import.meta.env.VITE_IMG_URL || 'http://localhost:5123';

// ── Вспомогательные функции ───────────────────────────────────────────────────

function getInitials(user: User) {
    if (user.firstName || user.lastName)
        return `${user.firstName?.[0] ?? ''}${user.lastName?.[0] ?? ''}`.toUpperCase();
    return user.email[0].toUpperCase();
}

function stackLabel(stack: string) {
    if (stack === 'CSharp_PostgreSQL') return { label: 'C# / PG', color: '#6366f1' };
    if (stack === 'NodeJS_MongoDB')    return { label: 'Node / Mongo', color: '#10b981' };
    return { label: stack, color: '#6b7280' };
}

function archLabel(arch: string) {
    if (arch === 'Microservices') return { label: 'Microservices', color: '#8b5cf6' };
    return { label: 'Monolith', color: '#0ea5e9' };
}

function StatCard({ icon, label, value, color }: { icon: string; label: string; value: number | string; color: string }) {
    return (
        <div style={{
            background: 'var(--bg-primary)', borderRadius: '12px', padding: '20px 24px',
            border: '1px solid var(--border-color)', boxShadow: 'var(--shadow-sm)',
            display: 'flex', alignItems: 'center', gap: '16px', flex: '1', minWidth: '160px'
        }}>
            <div style={{
                width: '48px', height: '48px', borderRadius: '12px',
                background: color + '18', display: 'flex', alignItems: 'center',
                justifyContent: 'center', fontSize: '1.4rem', flexShrink: 0
            }}>{icon}</div>
            <div>
                <div style={{ fontSize: '1.6rem', fontWeight: 800, color: 'var(--text-primary)', lineHeight: 1 }}>{value}</div>
                <div style={{ fontSize: '0.78rem', color: 'var(--text-secondary)', marginTop: '4px' }}>{label}</div>
            </div>
        </div>
    );
}

function Avatar({ user, size = 36 }: { user: User; size?: number }) {
    const colors = ['#6366f1','#10b981','#f59e0b','#ef4444','#3b82f6','#8b5cf6','#ec4899'];
    const color = colors[user.email.charCodeAt(0) % colors.length];

    if (user.avatarUrl) {
        return (
            <img
                src={VITE_IMG_URL + user.avatarUrl}
                alt="avatar"
                style={{ width: size, height: size, borderRadius: '50%', objectFit: 'cover', flexShrink: 0 }}
            />
        );
    }
    return (
        <div style={{
            width: size, height: size, borderRadius: '50%', background: color + '22',
            border: `2px solid ${color}44`, display: 'flex', alignItems: 'center',
            justifyContent: 'center', fontSize: size * 0.4, fontWeight: 700,
            color, flexShrink: 0
        }}>
            {getInitials(user)}
        </div>
    );
}

// ── Основной компонент ────────────────────────────────────────────────────────

function AdminPanel({ onNavigate }: AdminPanelProps) {
    const [users, setUsers] = useState<User[]>([]);
    const [loading, setLoading] = useState(true);
    const [search, setSearch] = useState('');
    const [roleFilter, setRoleFilter] = useState<'all' | 'Admin' | 'User'>('all');
    const [selectedUser, setSelectedUser] = useState<User | null>(null);
    const [selectedAdminProject, setSelectedAdminProject] = useState<Project | null>(null);
    const [activeTab, setActiveTab] = useState<'projects' | 'profile'>('projects');
    const [isEditUserModalOpen, setIsEditUserModalOpen] = useState(false);
    const [editingUser, setEditingUser] = useState<User | null>(null);
    const [editFormData, setEditFormData] = useState({ firstName: '', lastName: '', avatarUrl: '' });
    const [isSidebarOpen, setIsSidebarOpen] = useState(false);
    const { confirm } = useConfirm();

    useEffect(() => { fetchUsers(); }, []);

    const fetchUsers = async () => {
        try {
            const response = await api.get<User[]>('/users');
            setUsers(response.data);
        } catch {
            toast.error('Не удалось загрузить пользователей');
        } finally {
            setLoading(false);
        }
    };

    const filteredUsers = useMemo(() => {
        return users.filter(u => {
            const matchesSearch = search === '' ||
                u.email.toLowerCase().includes(search.toLowerCase()) ||
                `${u.firstName} ${u.lastName}`.toLowerCase().includes(search.toLowerCase());
            const matchesRole = roleFilter === 'all' || u.role === roleFilter;
            return matchesSearch && matchesRole;
        });
    }, [users, search, roleFilter]);

    const stats = useMemo(() => ({
        totalUsers: users.length,
        totalProjects: users.reduce((acc, u) => acc + (u.projects?.length || 0), 0),
        admins: users.filter(u => u.role === 'Admin').length,
        with2FA: users.filter(u => u.twoFactorEnabled).length,
    }), [users]);

    const handleDeleteUser = async (user: User) => {
        const projectCount = user.projects?.length ?? 0;
        const entityCount = user.projects?.reduce((sum, p) => sum + (p.entities?.length ?? 0), 0) ?? 0;

        if (await confirm({
            title: 'Удаление аккаунта',
            message: `Удалить пользователя ${user.email}?\n\nБудут безвозвратно удалены:\n• ${projectCount} проект(ов)\n• ${entityCount} сущностей со всеми полями и связями\n• история генераций, достижения и активность аккаунта\n• аватар и токены верификации`,
            confirmText: 'Удалить навсегда',
            type: 'danger'
        })) {
            try {
                await api.delete(`/users/${user.id}`);
                setUsers(prev => prev.filter(u => u.id !== user.id));
                if (selectedUser?.id === user.id) setSelectedUser(null);
                toast.success('Аккаунт и все связанные данные удалены');
            } catch (error: any) {
                const msg = error.response?.data?.message ?? error.response?.data ?? 'Ошибка удаления';
                toast.error(typeof msg === 'string' ? msg : 'Ошибка удаления');
            }
        }
    };

    const handleDeleteProject = async (projectId: string) => {
        if (await confirm({
            title: 'Удаление проекта',
            message: 'Удалить этот проект?',
            confirmText: 'Удалить',
            type: 'danger'
        })) {
            try {
                await api.delete(`/projects/${projectId}`);
                if (selectedUser) {
                    const updated = { ...selectedUser, projects: selectedUser.projects?.filter(p => p.id !== projectId) };
                    setSelectedUser(updated);
                    setUsers(prev => prev.map(u => u.id === selectedUser.id ? updated : u));
                }
                toast.success('Проект удалён');
            } catch {
                toast.error('Ошибка удаления проекта');
            }
        }
    };

    const openEditUserModal = (user: User) => {
        setEditingUser(user);
        setEditFormData({ firstName: user.firstName || '', lastName: user.lastName || '', avatarUrl: user.avatarUrl || '' });
        setIsEditUserModalOpen(true);
    };

    const handleUpdateUser = async () => {
        if (!editingUser) return;
        try {
            await api.put(`/users/${editingUser.id}`, editFormData);
            const updated = { ...editingUser, ...editFormData };
            setUsers(prev => prev.map(u => u.id === editingUser.id ? updated : u));
            if (selectedUser?.id === editingUser.id) setSelectedUser(updated);
            setIsEditUserModalOpen(false);
            toast.success('Пользователь обновлён');
        } catch {
            toast.error('Ошибка обновления');
        }
    };

    if (loading) return (
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh', flexDirection: 'column', gap: '16px' }}>
            <ClipLoader color="#6366f1" size={40} />
            <p style={{ color: 'var(--text-secondary)', fontSize: '0.9rem' }}>Загрузка данных…</p>
        </div>
    );

    return (
        <div className="dashboard animate-fade-in">
            {/* Мобильная шапка */}
            <div className="mobile-header">
                <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                    <img src="/logo.svg" alt="CodeForge" style={{ height: '32px', width: 'auto' }} />
                    <span style={{ fontSize: '1rem', fontWeight: 'bold' }}>Admin</span>
                </div>
                <button className="menu-btn" onClick={() => setIsSidebarOpen(true)}>☰</button>
            </div>

            <div className={`sidebar-overlay ${isSidebarOpen ? 'visible' : ''}`} onClick={() => setIsSidebarOpen(false)} />

            {/* Боковая панель */}
            <div className={`sidebar animate-slide-up ${isSidebarOpen ? 'open' : ''}`}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '24px' }}>
                    <div style={{ width: '36px', height: '36px', borderRadius: '10px', background: 'rgba(239,68,68,0.12)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '1.1rem' }}>🛡️</div>
                    <div>
                        <div style={{ fontWeight: 700, fontSize: '0.95rem' }}>Панель администратора</div>
                        <div style={{ fontSize: '0.72rem', color: 'var(--text-secondary)' }}>CodeForge</div>
                    </div>
                </div>

                <button
                    className="btn btn-secondary"
                    style={{ width: '100%', marginBottom: '8px', justifyContent: 'flex-start', gap: '8px' }}
                    onClick={() => onNavigate('dashboard')}
                >
                    ← Дашборд
                </button>
                <button
                    className="btn btn-secondary"
                    style={{ width: '100%', justifyContent: 'flex-start', gap: '8px' }}
                    onClick={() => onNavigate('profile')}
                >
                    👤 Профиль
                </button>

                <div style={{ marginTop: '24px', padding: '16px', background: 'var(--bg-secondary)', borderRadius: '12px', border: '1px solid var(--border-color)' }}>
                    <div style={{ fontSize: '0.72rem', color: 'var(--text-secondary)', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '12px' }}>Статистика</div>
                    {[
                        { icon: '👥', label: 'Пользователи', val: stats.totalUsers },
                        { icon: '📁', label: 'Проекты', val: stats.totalProjects },
                        { icon: '🛡️', label: 'Администраторов', val: stats.admins },
                        { icon: '🔐', label: 'С 2FA', val: stats.with2FA },
                    ].map(s => (
                        <div key={s.label} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '6px 0', borderBottom: '1px solid var(--border-color)' }}>
                            <span style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>{s.icon} {s.label}</span>
                            <span style={{ fontWeight: 700, fontSize: '0.9rem' }}>{s.val}</span>
                        </div>
                    ))}
                </div>
            </div>

            {/* Основное содержимое */}
            <div className="main-content">
                {/* Заголовок страницы */}
                <div className="header" style={{ marginBottom: '24px' }}>
                    <div>
                        <h1 style={{ fontSize: '1.5rem', fontWeight: 800 }}>Управление пользователями</h1>
                        <p style={{ color: 'var(--text-secondary)', fontSize: '0.85rem', marginTop: '2px' }}>
                            {users.length} пользователей · {stats.totalProjects} проектов
                        </p>
                    </div>
                    <span style={{
                        padding: '4px 12px', borderRadius: '20px', fontSize: '0.75rem', fontWeight: 700,
                        background: 'rgba(239,68,68,0.1)', color: '#ef4444', border: '1px solid rgba(239,68,68,0.2)'
                    }}>
                        🛡️ Режим администратора
                    </span>
                </div>

                {/* Строка статистики */}
                <div style={{ display: 'flex', gap: '16px', flexWrap: 'wrap', marginBottom: '24px' }}>
                    <StatCard icon="👥" label="Всего пользователей" value={stats.totalUsers} color="#6366f1" />
                    <StatCard icon="📁" label="Всего проектов" value={stats.totalProjects} color="#10b981" />
                    <StatCard icon="🛡️" label="Администраторов" value={stats.admins} color="#ef4444" />
                    <StatCard icon="🔐" label="С двухфакторной" value={stats.with2FA} color="#f59e0b" />
                </div>

                {/* Поиск и фильтрация */}
                <div style={{
                    display: 'flex', gap: '12px', marginBottom: '16px', flexWrap: 'wrap',
                    background: 'var(--bg-primary)', padding: '14px 16px', borderRadius: '12px',
                    border: '1px solid var(--border-color)', boxShadow: 'var(--shadow-sm)'
                }}>
                    <div style={{ position: 'relative', flex: 1, minWidth: '200px' }}>
                        <span style={{ position: 'absolute', left: '12px', top: '50%', transform: 'translateY(-50%)', color: 'var(--text-secondary)', fontSize: '0.9rem', pointerEvents: 'none' }}>🔍</span>
                        <input
                            placeholder="Поиск по email или имени…"
                            value={search}
                            onChange={e => setSearch(e.target.value)}
                            style={{
                                width: '100%', padding: '8px 12px 8px 34px', border: '1px solid var(--border-color)',
                                borderRadius: '8px', fontSize: '0.875rem', background: 'var(--bg-secondary)', outline: 'none',
                                color: 'var(--text-primary)'
                            }}
                        />
                    </div>
                    <div style={{ display: 'flex', gap: '6px' }}>
                        {(['all', 'User', 'Admin'] as const).map(r => (
                            <button
                                key={r}
                                onClick={() => setRoleFilter(r)}
                                style={{
                                    padding: '8px 14px', borderRadius: '8px', fontSize: '0.8rem', fontWeight: 600, cursor: 'pointer',
                                    border: '1px solid',
                                    borderColor: roleFilter === r ? (r === 'Admin' ? '#ef4444' : r === 'User' ? '#10b981' : '#6366f1') : 'var(--border-color)',
                                    background: roleFilter === r ? (r === 'Admin' ? 'rgba(239,68,68,0.1)' : r === 'User' ? 'rgba(16,185,129,0.1)' : 'rgba(99,102,241,0.1)') : 'transparent',
                                    color: roleFilter === r ? (r === 'Admin' ? '#ef4444' : r === 'User' ? '#10b981' : '#6366f1') : 'var(--text-secondary)',
                                    transition: 'all 0.15s'
                                }}
                            >
                                {r === 'all' ? 'Все' : r}
                            </button>
                        ))}
                    </div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
                        Показано: <strong style={{ color: 'var(--text-primary)' }}>{filteredUsers.length}</strong>
                    </div>
                </div>

                {/* Таблица пользователей */}
                <div style={{
                    background: 'var(--bg-primary)', borderRadius: '12px', border: '1px solid var(--border-color)',
                    boxShadow: 'var(--shadow-sm)', overflow: 'hidden'
                }}>
                    <div style={{ overflowX: 'auto' }}>
                        <table style={{ width: '100%', borderCollapse: 'collapse', minWidth: '760px' }}>
                            <thead>
                                <tr style={{ background: 'var(--bg-secondary)', borderBottom: '1px solid var(--border-color)' }}>
                                    {['Пользователь', 'Роль', 'Статус', 'Проектов', 'Регистрация', 'Действия'].map(h => (
                                        <th key={h} style={{ padding: '12px 16px', textAlign: 'left', fontSize: '0.75rem', fontWeight: 700, color: 'var(--text-secondary)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                                            {h}
                                        </th>
                                    ))}
                                </tr>
                            </thead>
                            <tbody>
                                {filteredUsers.length === 0 ? (
                                    <tr>
                                        <td colSpan={6} style={{ textAlign: 'center', padding: '40px', color: 'var(--text-secondary)', fontSize: '0.9rem' }}>
                                            🔍 Пользователи не найдены
                                        </td>
                                    </tr>
                                ) : filteredUsers.map((user, idx) => (
                                    <tr
                                        key={user.id}
                                        style={{
                                            borderBottom: idx < filteredUsers.length - 1 ? '1px solid var(--border-color)' : 'none',
                                            transition: 'background 0.1s'
                                        }}
                                        onMouseEnter={e => (e.currentTarget.style.background = 'var(--bg-secondary)')}
                                        onMouseLeave={e => (e.currentTarget.style.background = 'transparent')}
                                    >
                                        {/* Пользователь */}
                                        <td style={{ padding: '14px 16px' }}>
                                            <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                                                <Avatar user={user} size={38} />
                                                <div>
                                                    <div style={{ fontWeight: 600, fontSize: '0.875rem', color: 'var(--text-primary)' }}>
                                                        {user.firstName || user.lastName
                                                            ? `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim()
                                                            : <span style={{ color: 'var(--text-secondary)', fontStyle: 'italic' }}>Без имени</span>
                                                        }
                                                    </div>
                                                    <div style={{ fontSize: '0.77rem', color: 'var(--text-secondary)' }}>{user.email}</div>
                                                </div>
                                            </div>
                                        </td>

                                        {/* Роль */}
                                        <td style={{ padding: '14px 16px' }}>
                                            <span style={{
                                                padding: '3px 10px', borderRadius: '20px', fontSize: '0.75rem', fontWeight: 700,
                                                background: user.role === 'Admin' ? 'rgba(239,68,68,0.1)' : 'rgba(16,185,129,0.1)',
                                                color: user.role === 'Admin' ? '#ef4444' : '#10b981',
                                                border: `1px solid ${user.role === 'Admin' ? 'rgba(239,68,68,0.25)' : 'rgba(16,185,129,0.25)'}`,
                                            }}>
                                                {user.role === 'Admin' ? '🛡️ Admin' : '👤 User'}
                                            </span>
                                        </td>

                                        {/* Значки статуса */}
                                        <td style={{ padding: '14px 16px' }}>
                                            <div style={{ display: 'flex', gap: '5px', flexWrap: 'wrap' }}>
                                                {user.twoFactorEnabled && (
                                                    <span style={{ padding: '2px 7px', borderRadius: '8px', fontSize: '0.7rem', fontWeight: 600, background: 'rgba(99,102,241,0.1)', color: '#6366f1', border: '1px solid rgba(99,102,241,0.2)' }}>
                                                        🔐 2FA
                                                    </span>
                                                )}
                                                {user.isDarkMode && (
                                                    <span style={{ padding: '2px 7px', borderRadius: '8px', fontSize: '0.7rem', fontWeight: 600, background: 'rgba(107,114,128,0.1)', color: 'var(--text-secondary)', border: '1px solid var(--border-color)' }}>
                                                        🌙
                                                    </span>
                                                )}
                                                {!user.twoFactorEnabled && !user.isDarkMode && (
                                                    <span style={{ color: 'var(--text-secondary)', fontSize: '0.75rem' }}>—</span>
                                                )}
                                            </div>
                                        </td>

                                        {/* Количество проектов */}
                                        <td style={{ padding: '14px 16px' }}>
                                            <button
                                                onClick={() => { setSelectedUser(user); setActiveTab('projects'); }}
                                                style={{
                                                    padding: '4px 12px', borderRadius: '8px', fontSize: '0.8rem', fontWeight: 600,
                                                    background: (user.projects?.length || 0) > 0 ? 'rgba(99,102,241,0.1)' : 'var(--bg-secondary)',
                                                    color: (user.projects?.length || 0) > 0 ? '#6366f1' : 'var(--text-secondary)',
                                                    border: `1px solid ${(user.projects?.length || 0) > 0 ? 'rgba(99,102,241,0.25)' : 'var(--border-color)'}`,
                                                    cursor: 'pointer'
                                                }}
                                            >
                                                📁 {user.projects?.length || 0}
                                            </button>
                                        </td>

                                        {/* Дата регистрации */}
                                        <td style={{ padding: '14px 16px', fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
                                            {user.createdAt ? new Date(user.createdAt).toLocaleDateString('ru-RU', { day: '2-digit', month: 'short', year: 'numeric' }) : '—'}
                                        </td>

                                        {/* Действия */}
                                        <td style={{ padding: '14px 16px' }}>
                                            <div style={{ display: 'flex', gap: '6px' }}>
                                                <button
                                                    className="btn btn-secondary btn-small"
                                                    onClick={() => { setSelectedUser(user); setActiveTab('profile'); }}
                                                    title="Профиль пользователя"
                                                >
                                                    👁️
                                                </button>
                                                <button
                                                    className="btn btn-secondary btn-small"
                                                    onClick={() => openEditUserModal(user)}
                                                    title="Редактировать"
                                                >
                                                    ✏️
                                                </button>
                                                {user.role !== 'Admin' && (
                                                    <button
                                                        className="btn btn-danger btn-small"
                                                        onClick={() => handleDeleteUser(user)}
                                                        title="Удалить"
                                                    >
                                                        🗑️
                                                    </button>
                                                )}
                                            </div>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </div>
            </div>

            {/* ── Модал детальной информации о пользователе ────────────────────────────── */}
            {selectedUser && (
                <div className="modal-overlay" onClick={() => { setSelectedUser(null); setSelectedAdminProject(null); }}>
                    <div
                        className="modal"
                        onClick={e => e.stopPropagation()}
                        style={{ width: '95%', maxWidth: '860px', maxHeight: '85vh', overflow: 'hidden', display: 'flex', flexDirection: 'column', padding: 0, borderRadius: '16px' }}
                    >
                        {/* Заголовок модала */}
                        <div style={{ padding: '20px 24px', borderBottom: '1px solid var(--border-color)', display: 'flex', alignItems: 'center', gap: '16px', flexShrink: 0 }}>
                            <Avatar user={selectedUser} size={48} />
                            <div style={{ flex: 1 }}>
                                <div style={{ fontWeight: 800, fontSize: '1.1rem' }}>
                                    {selectedUser.firstName || selectedUser.lastName
                                        ? `${selectedUser.firstName ?? ''} ${selectedUser.lastName ?? ''}`.trim()
                                        : selectedUser.email}
                                </div>
                                <div style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>{selectedUser.email}</div>
                            </div>
                            <span style={{
                                padding: '3px 12px', borderRadius: '20px', fontSize: '0.75rem', fontWeight: 700,
                                background: selectedUser.role === 'Admin' ? 'rgba(239,68,68,0.1)' : 'rgba(16,185,129,0.1)',
                                color: selectedUser.role === 'Admin' ? '#ef4444' : '#10b981',
                                border: `1px solid ${selectedUser.role === 'Admin' ? 'rgba(239,68,68,0.25)' : 'rgba(16,185,129,0.25)'}`,
                            }}>
                                {selectedUser.role === 'Admin' ? '🛡️ Admin' : '👤 User'}
                            </span>
                            <button
                                style={{ background: 'none', border: 'none', cursor: 'pointer', fontSize: '1.2rem', color: 'var(--text-secondary)', padding: '4px', borderRadius: '6px' }}
                                onClick={() => { setSelectedUser(null); setSelectedAdminProject(null); }}
                            >✕</button>
                        </div>

                        {/* Вкладки */}
                        {!selectedAdminProject && (
                            <div style={{ display: 'flex', padding: '0 24px', borderBottom: '1px solid var(--border-color)', gap: '4px', flexShrink: 0 }}>
                                {(['projects', 'profile'] as const).map(tab => (
                                    <button
                                        key={tab}
                                        onClick={() => setActiveTab(tab)}
                                        style={{
                                            padding: '12px 16px', background: 'none', border: 'none', cursor: 'pointer',
                                            fontSize: '0.85rem', fontWeight: activeTab === tab ? 700 : 500,
                                            color: activeTab === tab ? '#6366f1' : 'var(--text-secondary)',
                                            borderBottom: `2px solid ${activeTab === tab ? '#6366f1' : 'transparent'}`,
                                            transition: 'all 0.15s'
                                        }}
                                    >
                                        {tab === 'projects' ? `📁 Проекты (${selectedUser.projects?.length || 0})` : '👤 Профиль'}
                                    </button>
                                ))}
                            </div>
                        )}

                        {/* Тело модала */}
                        <div style={{ flex: 1, overflowY: 'auto', padding: '20px 24px' }}>

                            {/* Детальный просмотр проекта */}
                            {selectedAdminProject ? (
                                <>
                                    <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '16px' }}>
                                        <button className="btn btn-secondary btn-small" onClick={() => setSelectedAdminProject(null)}>← Назад</button>
                                        <div>
                                            <h3 style={{ margin: 0 }}>{selectedAdminProject.name}</h3>
                                            <div style={{ display: 'flex', gap: '6px', marginTop: '4px' }}>
                                                {(() => { const s = stackLabel(selectedAdminProject.targetStack); return <span style={{ padding: '1px 8px', borderRadius: '8px', fontSize: '0.72rem', fontWeight: 600, background: s.color + '18', color: s.color, border: `1px solid ${s.color}30` }}>{s.label}</span>; })()}
                                                {(() => { const a = archLabel(selectedAdminProject.architectureType); return <span style={{ padding: '1px 8px', borderRadius: '8px', fontSize: '0.72rem', fontWeight: 600, background: a.color + '18', color: a.color, border: `1px solid ${a.color}30` }}>{a.label}</span>; })()}
                                            </div>
                                        </div>
                                    </div>
                                    {selectedAdminProject.entities && selectedAdminProject.entities.length > 0 ? (
                                        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                                            {selectedAdminProject.entities.map(entity => (
                                                <div key={entity.id} style={{
                                                    padding: '14px 16px', borderRadius: '10px', border: '1px solid var(--border-color)',
                                                    background: 'var(--bg-secondary)', display: 'flex', alignItems: 'center', gap: '12px'
                                                }}>
                                                    <div style={{ width: '36px', height: '36px', borderRadius: '8px', background: 'rgba(99,102,241,0.1)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '1rem', flexShrink: 0 }}>📦</div>
                                                    <div style={{ flex: 1 }}>
                                                        <div style={{ fontWeight: 600, fontSize: '0.9rem' }}>{entity.name}</div>
                                                        {entity.serviceName && <div style={{ fontSize: '0.75rem', color: '#6366f1' }}>⚙️ {entity.serviceName}-service</div>}
                                                    </div>
                                                    <div style={{ display: 'flex', gap: '12px', fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
                                                        <span title="Поля">🏷️ {entity.fields?.length || 0} полей</span>
                                                        <span title="Связи">🔗 {(entity.sourceRelationships?.length || 0) + (entity.targetRelationships?.length || 0)} связей</span>
                                                    </div>
                                                </div>
                                            ))}
                                        </div>
                                    ) : (
                                        <div style={{ textAlign: 'center', padding: '32px', color: 'var(--text-secondary)' }}>
                                            <div style={{ fontSize: '2rem', marginBottom: '8px' }}>📭</div>
                                            <p>В этом проекте нет сущностей</p>
                                        </div>
                                    )}
                                </>
                            ) : activeTab === 'projects' ? (
                                /* Вкладка проектов */
                                selectedUser.projects && selectedUser.projects.length > 0 ? (
                                    <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                                        {selectedUser.projects.map((project: Project) => {
                                            const s = stackLabel(project.targetStack);
                                            const a = archLabel(project.architectureType);
                                            return (
                                                <div key={project.id} style={{
                                                    padding: '16px', borderRadius: '12px', border: '1px solid var(--border-color)',
                                                    background: 'var(--bg-secondary)', display: 'flex', alignItems: 'center', gap: '16px'
                                                }}>
                                                    <div style={{ width: '44px', height: '44px', borderRadius: '10px', background: s.color + '18', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '1.2rem', flexShrink: 0 }}>
                                                        {project.targetStack === 'CSharp_PostgreSQL' ? '⚙️' : '🟢'}
                                                    </div>
                                                    <div style={{ flex: 1, minWidth: 0 }}>
                                                        <div style={{ fontWeight: 700, fontSize: '0.95rem', marginBottom: '4px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{project.name}</div>
                                                        <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
                                                            <span style={{ padding: '1px 8px', borderRadius: '8px', fontSize: '0.72rem', fontWeight: 600, background: s.color + '18', color: s.color, border: `1px solid ${s.color}30` }}>{s.label}</span>
                                                            <span style={{ padding: '1px 8px', borderRadius: '8px', fontSize: '0.72rem', fontWeight: 600, background: a.color + '18', color: a.color, border: `1px solid ${a.color}30` }}>{a.label}</span>
                                                            <span style={{ padding: '1px 8px', borderRadius: '8px', fontSize: '0.72rem', color: 'var(--text-secondary)', border: '1px solid var(--border-color)', background: 'var(--bg-primary)' }}>
                                                                📦 {project.entities?.length || 0} сущностей
                                                            </span>
                                                        </div>
                                                    </div>
                                                    <div style={{ fontSize: '0.78rem', color: 'var(--text-secondary)', flexShrink: 0 }}>
                                                        {new Date(project.createdAt).toLocaleDateString('ru-RU')}
                                                    </div>
                                                    <div style={{ display: 'flex', gap: '6px', flexShrink: 0 }}>
                                                        {(project.entities?.length || 0) > 0 && (
                                                            <button className="btn btn-secondary btn-small" onClick={() => setSelectedAdminProject(project)}>Сущности</button>
                                                        )}
                                                        <button className="btn btn-danger btn-small" onClick={() => handleDeleteProject(project.id)}>🗑️</button>
                                                    </div>
                                                </div>
                                            );
                                        })}
                                    </div>
                                ) : (
                                    <div style={{ textAlign: 'center', padding: '40px', color: 'var(--text-secondary)' }}>
                                        <div style={{ fontSize: '2.5rem', marginBottom: '8px' }}>📂</div>
                                        <p>У этого пользователя нет проектов</p>
                                    </div>
                                )
                            ) : (
                                /* Вкладка профиля */
                                <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
                                    <div style={{ display: 'flex', gap: '16px', flexWrap: 'wrap' }}>
                                        {[
                                            { label: 'Email', value: selectedUser.email, icon: '📧' },
                                            { label: 'Имя', value: `${selectedUser.firstName ?? '—'} ${selectedUser.lastName ?? ''}`.trim(), icon: '👤' },
                                            { label: 'Роль', value: selectedUser.role, icon: '🛡️' },
                                            { label: 'Дата регистрации', value: selectedUser.createdAt ? new Date(selectedUser.createdAt).toLocaleDateString('ru-RU', { day: '2-digit', month: 'long', year: 'numeric' }) : '—', icon: '📅' },
                                        ].map(item => (
                                            <div key={item.label} style={{ flex: '1', minWidth: '180px', padding: '14px 16px', borderRadius: '10px', border: '1px solid var(--border-color)', background: 'var(--bg-secondary)' }}>
                                                <div style={{ fontSize: '0.72rem', color: 'var(--text-secondary)', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '6px' }}>{item.icon} {item.label}</div>
                                                <div style={{ fontWeight: 600, fontSize: '0.9rem', wordBreak: 'break-all' }}>{item.value || '—'}</div>
                                            </div>
                                        ))}
                                    </div>
                                    <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap' }}>
                                        {selectedUser.twoFactorEnabled && (
                                            <div style={{ padding: '10px 14px', borderRadius: '10px', background: 'rgba(99,102,241,0.08)', border: '1px solid rgba(99,102,241,0.2)', fontSize: '0.82rem', color: '#6366f1', fontWeight: 600 }}>
                                                🔐 Двухфакторная аутентификация включена
                                            </div>
                                        )}
                                        {selectedUser.isDarkMode && (
                                            <div style={{ padding: '10px 14px', borderRadius: '10px', background: 'rgba(107,114,128,0.08)', border: '1px solid var(--border-color)', fontSize: '0.82rem', color: 'var(--text-secondary)', fontWeight: 600 }}>
                                                🌙 Тёмная тема включена
                                            </div>
                                        )}
                                    </div>
                                    <div style={{ display: 'flex', gap: '8px', paddingTop: '8px', flexWrap: 'wrap' }}>
                                        <button className="btn btn-secondary" onClick={() => openEditUserModal(selectedUser)}>✏️ Редактировать</button>
                                        {selectedUser.role !== 'Admin' && (
                                            <button className="btn btn-danger" onClick={() => { handleDeleteUser(selectedUser); }}>🗑️ Удалить аккаунт</button>
                                        )}
                                    </div>
                                    {selectedUser.role !== 'Admin' && (
                                        <div style={{
                                            marginTop: '8px', padding: '14px 16px', borderRadius: '10px',
                                            border: '1px solid rgba(239,68,68,0.25)', background: 'rgba(239,68,68,0.05)',
                                        }}>
                                            <div style={{ fontSize: '0.78rem', fontWeight: 700, color: '#ef4444', marginBottom: '4px' }}>⚠️ Опасная зона</div>
                                            <div style={{ fontSize: '0.8rem', color: 'var(--text-secondary)', lineHeight: 1.5 }}>
                                                Удаление аккаунта каскадно удалит все проекты пользователя, сущности, поля, связи,
                                                историю генераций, достижения и записи активности из базы данных.
                                            </div>
                                        </div>
                                    )}
                                </div>
                            )}
                        </div>
                    </div>
                </div>
            )}

            {/* ── Модал редактирования пользователя ────────────────────────────────────── */}
            {isEditUserModalOpen && editingUser && (
                <div className="modal-overlay" onClick={() => setIsEditUserModalOpen(false)}>
                    <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: '480px', width: '95%' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '20px' }}>
                            <Avatar user={editingUser} size={44} />
                            <div>
                                <h2 style={{ margin: 0, fontSize: '1.1rem' }}>Редактирование</h2>
                                <div style={{ fontSize: '0.78rem', color: 'var(--text-secondary)' }}>{editingUser.email}</div>
                            </div>
                        </div>

                        <div className="form-group">
                            <label>Email</label>
                            <input value={editingUser.email} disabled style={{ background: 'var(--bg-secondary)', color: 'var(--text-secondary)', cursor: 'not-allowed' }} />
                        </div>
                        <div style={{ display: 'flex', gap: '12px' }}>
                            <div className="form-group" style={{ flex: 1 }}>
                                <label>Имя</label>
                                <input
                                    placeholder="Имя"
                                    value={editFormData.firstName}
                                    onChange={e => setEditFormData({ ...editFormData, firstName: e.target.value })}
                                />
                            </div>
                            <div className="form-group" style={{ flex: 1 }}>
                                <label>Фамилия</label>
                                <input
                                    placeholder="Фамилия"
                                    value={editFormData.lastName}
                                    onChange={e => setEditFormData({ ...editFormData, lastName: e.target.value })}
                                />
                            </div>
                        </div>

                        <div className="form-group">
                            <label>Аватар</label>
                            <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
                                <div style={{ width: '64px', height: '64px', borderRadius: '50%', overflow: 'hidden', background: 'var(--bg-secondary)', border: '2px solid var(--border-color)', flexShrink: 0, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                                    {(editFormData.avatarUrl || editingUser.avatarUrl) ? (
                                        <img src={`${VITE_IMG_URL}${editFormData.avatarUrl || editingUser.avatarUrl}`} alt="avatar" style={{ width: '100%', height: '100%', objectFit: 'cover' }} />
                                    ) : (
                                        <span style={{ fontSize: '1.5rem' }}>👤</span>
                                    )}
                                </div>
                                <div style={{ flex: 1 }}>
                                    <label style={{ display: 'inline-block', padding: '8px 14px', borderRadius: '8px', border: '1px solid var(--border-color)', cursor: 'pointer', fontSize: '0.82rem', background: 'var(--bg-secondary)', color: 'var(--text-primary)' }}>
                                        📷 Выбрать файл
                                        <input type="file" accept="image/*" style={{ display: 'none' }} onChange={async (e) => {
                                            const file = e.target.files?.[0];
                                            if (!file) return;
                                            const formData = new FormData();
                                            formData.append('file', file);
                                            try {
                                                const response = await api.post(`/users/${editingUser.id}/avatar`, formData, { headers: { 'Content-Type': 'multipart/form-data' } });
                                                setEditFormData({ ...editFormData, avatarUrl: response.data.avatarUrl });
                                                toast.success('Аватар загружен');
                                            } catch {
                                                toast.error('Ошибка загрузки');
                                            }
                                        }} />
                                    </label>
                                    <p style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', marginTop: '6px' }}>JPG, PNG или WebP до 5MB</p>
                                </div>
                            </div>
                        </div>

                        <div style={{ display: 'flex', gap: '10px', marginTop: '8px' }}>
                            <button className="btn btn-primary" style={{ flex: 1 }} onClick={handleUpdateUser}>Сохранить</button>
                            <button className="btn btn-secondary" onClick={() => setIsEditUserModalOpen(false)}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}

export default AdminPanel;

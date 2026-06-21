import React, { useRef, useState, useMemo, useEffect, useCallback } from 'react';
import { useAppDispatch, useAppSelector } from '../../app/hooks';
import { setUser, updateUser, disable2FA } from '../../features/auth/authSlice';
import { Page } from '../../App';
import api from '../../utils/api';
import { toast } from 'react-toastify';
import { ClipLoader } from 'react-spinners';
import TwoFactorSetupModal from '../auth/TwoFactorSetupModal';
import { GenerationRecord, AchievementInfo, formatTimestamp } from '../../utils/generationHistory';
import { showAchievements } from '../common/AchievementToast';

interface ProfileProps {
    onNavigate: (page: Page) => void;
}

// ─── Достижения: данные берутся из API ────────────────────────────────────────

// ─── Heatmap ─────────────────────────────────────────────────────────────────
function ActivityHeatmap({ history }: { history: GenerationRecord[] }) {

    const today = new Date();
    today.setHours(0, 0, 0, 0);

    // Строим карту дат за последние 365 дней
    const countByDate: Record<string, number> = {};
    for (const r of history) {
        const d = new Date(r.createdAt);
        d.setHours(0, 0, 0, 0);
        const key = d.toISOString().slice(0, 10);
        countByDate[key] = (countByDate[key] ?? 0) + 1;
    }

    // 52 недели + хвостик
    const weeks: Array<Array<{ date: Date; key: string; count: number }>> = [];
    // Начинаем с воскресенья 53 недель назад
    const start = new Date(today);
    start.setDate(today.getDate() - 364);
    const dayOfWeek = start.getDay(); // 0=вс
    start.setDate(start.getDate() - dayOfWeek);

    let cur = new Date(start);
    while (cur <= today) {
        const week: typeof weeks[0] = [];
        for (let d = 0; d < 7; d++) {
            const key = cur.toISOString().slice(0, 10);
            week.push({ date: new Date(cur), key, count: countByDate[key] ?? 0 });
            cur.setDate(cur.getDate() + 1);
        }
        weeks.push(week);
    }

    const maxCount = Math.max(1, ...Object.values(countByDate));
    const getColor = (count: number) => {
        if (count === 0) return 'var(--heatmap-empty)';
        const ratio = count / maxCount;
        if (ratio <= 0.25) return '#bbf7d0';
        if (ratio <= 0.5) return '#4ade80';
        if (ratio <= 0.75) return '#16a34a';
        return '#15803d';
    };

    const DAYS = ['Вс', 'Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб'];
    const totalGens = history.length;

    return (
        <div className="profile-section">
            <div className="profile-section__title">
                <span>📅</span> Активность генераций
                <span className="profile-section__badge">{totalGens} всего</span>
            </div>
            <div className="heatmap-wrap">
                <div className="heatmap-days">
                    {DAYS.map((d, i) => (
                        <span key={d} style={{ visibility: i % 2 === 0 ? 'visible' : 'hidden' }}>{d}</span>
                    ))}
                </div>
                <div className="heatmap-grid">
                    {weeks.map((week, wi) => (
                        <div key={wi} className="heatmap-col">
                            {week.map(cell => (
                                <div
                                    key={cell.key}
                                    className="heatmap-cell"
                                    style={{ background: getColor(cell.count) }}
                                    title={cell.count > 0 ? `${cell.key}: ${cell.count} ген.` : cell.key}
                                />
                            ))}
                        </div>
                    ))}
                </div>
            </div>
            {totalGens === 0 && (
                <p style={{ margin: '0.5rem 0 0', fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
                    Здесь появится ваша активность после первой генерации
                </p>
            )}
        </div>
    );
}

// ─── Компонент профиля ────────────────────────────────────────────────────────
const Profile: React.FC<ProfileProps> = ({ onNavigate }) => {
    const dispatch = useAppDispatch();
    const { user } = useAppSelector((state) => state.auth);
    const { projects } = useAppSelector((state) => state.projects);
    const fileInputRef = useRef<HTMLInputElement>(null);
    const [uploading, setUploading] = useState(false);
    const [activeTab, setActiveTab] = useState<'info' | 'security' | 'stats'>('info');

    // Данные из API
    const [achievements, setAchievements] = useState<AchievementInfo[]>([]);
    const [genHistory, setGenHistory] = useState<GenerationRecord[]>([]);
    const [loadingStats, setLoadingStats] = useState(false);

    const [firstName, setFirstName] = useState(user?.firstName || '');
    const [lastName, setLastName] = useState(user?.lastName || '');

    // Смена пароля
    const [currentPw, setCurrentPw] = useState('');
    const [newPw, setNewPw] = useState('');
    const [confirmPw, setConfirmPw] = useState('');
    const [changingPw, setChangingPw] = useState(false);
    const [showCurrentPw, setShowCurrentPw] = useState(false);
    const [showNewPw, setShowNewPw] = useState(false);

    // 2FA
    const [show2FASetup, setShow2FASetup] = useState(false);
    const [showDisable2FA, setShowDisable2FA] = useState(false);
    const [disable2FACode, setDisable2FACode] = useState('');
    const [disabling2FA, setDisabling2FA] = useState(false);

    React.useEffect(() => {
        if (user) {
            setFirstName(user.firstName || '');
            setLastName(user.lastName || '');
        }
    }, [user]);

    // Загружаем достижения и историю из API при открытии вкладки Stats
    const loadStatsData = useCallback(async () => {
        setLoadingStats(true);
        try {
            const [achRes, histRes] = await Promise.all([
                api.get<AchievementInfo[]>('/achievements'),
                api.get<GenerationRecord[]>('/generations', { params: { limit: 365 } }),
            ]);
            setAchievements(achRes.data);
            setGenHistory(histRes.data);
        } catch {
            // показываем что есть
        } finally {
            setLoadingStats(false);
        }
    }, []);

    useEffect(() => {
        if (activeTab === 'stats') {
            loadStatsData();
        }
    }, [activeTab, loadStatsData]);

    // Статистика
    const stats = useMemo(() => {
        const totalEntities = projects.reduce((s, p) => s + (p.entities?.length ?? 0), 0);
        const totalFields = projects.reduce((s, p) =>
            s + (p.entities ?? []).reduce((es, e) => es + (e.fields?.length ?? 0), 0), 0);
        return {
            projects: projects.length,
            entities: totalEntities,
            fields: totalFields,
            generations: genHistory.length,
            lastGen: genHistory[0] ? formatTimestamp(genHistory[0].createdAt) : null,
        };
    }, [projects, genHistory]);

    const unlockedCount = achievements.filter(a => a.unlocked).length;

    const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
        const file = e.target.files?.[0];
        if (!file) return;
        const formData = new FormData();
        formData.append('file', file);
        setUploading(true);
        try {
            const response = await api.post('/auth/avatar', formData, {
                headers: { 'Content-Type': 'multipart/form-data' },
            });
            if (user) {
                dispatch(setUser({ ...user, avatarUrl: response.data.avatarUrl }));
                toast.success('Аватар обновлён');
            }
            if (response.data?.newAchievements?.length) {
                showAchievements(response.data.newAchievements);
            }
        } catch {
            toast.error('Ошибка загрузки аватара');
        } finally {
            setUploading(false);
        }
    };

    const handleSave = async () => {
        if (!user) return;
        try {
            await dispatch(updateUser({ id: user.id, data: { firstName, lastName, isDarkMode: user.isDarkMode } })).unwrap();
            toast.success('Профиль обновлён');
        } catch {
            toast.error('Ошибка обновления профиля');
        }
    };

    const handleChangePassword = async (e: React.FormEvent) => {
        e.preventDefault();
        if (newPw !== confirmPw) { toast.error('Новые пароли не совпадают'); return; }
        if (newPw.length < 6) { toast.error('Минимум 6 символов'); return; }
        setChangingPw(true);
        try {
            await api.post('/auth/change-password', { currentPassword: currentPw, newPassword: newPw });
            toast.success('Пароль успешно изменён');
            setCurrentPw(''); setNewPw(''); setConfirmPw('');
        } catch (err: any) {
            toast.error(err?.response?.data?.message ?? 'Ошибка смены пароля');
        } finally {
            setChangingPw(false);
        }
    };

    const handleDisable2FA = async (e: React.FormEvent) => {
        e.preventDefault();
        if (disable2FACode.length !== 6) { toast.error('Введите 6-значный код'); return; }
        setDisabling2FA(true);
        try {
            await dispatch(disable2FA(disable2FACode)).unwrap();
            toast.success('2FA отключена');
            setShowDisable2FA(false);
            setDisable2FACode('');
        } catch (err: any) {
            toast.error(err || 'Неверный код');
        } finally {
            setDisabling2FA(false);
        }
    };

    const is2FAEnabled = user?.twoFactorEnabled === true;
    const displayName = [user?.firstName, user?.lastName].filter(Boolean).join(' ') || user?.email || '';

    return (
        <div className="profile-page animate-fade-in">
            <div className="profile-container">

                {/* ── Шапка профиля ── */}
                <div className="profile-hero">
                    <div className="profile-avatar-wrap" onClick={() => fileInputRef.current?.click()} title="Изменить фото">
                        {user?.avatarUrl ? (
                            <img
                                src={`${import.meta.env.VITE_IMG_URL}${user.avatarUrl}`}
                                alt="Avatar"
                                className="profile-avatar-img"
                            />
                        ) : (
                            <span className="profile-avatar-initials">
                                {user?.firstName?.[0] || user?.email?.[0]?.toUpperCase() || '?'}
                            </span>
                        )}
                        {uploading && (
                            <div className="profile-avatar-overlay">
                                <ClipLoader color="#fff" size={28} />
                            </div>
                        )}
                        <div className="profile-avatar-edit">✏️</div>
                    </div>
                    <input type="file" ref={fileInputRef} onChange={handleFileChange} style={{ display: 'none' }} accept="image/*" />

                    <div className="profile-hero-info">
                        <h1 className="profile-hero-name">{displayName}</h1>
                        <p className="profile-hero-email">{user?.email}</p>
                        <div className="profile-hero-badges">
                            {user?.role === 'Admin' && <span className="profile-role-badge profile-role-badge--admin">👑 Администратор</span>}
                            {is2FAEnabled && <span className="profile-role-badge profile-role-badge--secure">🔐 2FA включена</span>}
                            <span className="profile-role-badge">🏆 {unlockedCount}/{achievements.length} достижений</span>
                        </div>
                    </div>

                    {/* Мини-статы в шапке */}
                    <div className="profile-hero-stats">
                        {[
                            { label: 'Проектов', value: stats.projects, icon: '📁' },
                            { label: 'Сущностей', value: stats.entities, icon: '🧩' },
                            { label: 'Генераций', value: stats.generations, icon: '📦' },
                        ].map(s => (
                            <div key={s.label} className="profile-mini-stat">
                                <span className="profile-mini-stat__icon">{s.icon}</span>
                                <span className="profile-mini-stat__value">{s.value}</span>
                                <span className="profile-mini-stat__label">{s.label}</span>
                            </div>
                        ))}
                    </div>
                </div>

                {/* ── Табы ── */}
                <div className="profile-tabs">
                    {(['info', 'security', 'stats'] as const).map(tab => (
                        <button
                            key={tab}
                            className={`profile-tab ${activeTab === tab ? 'profile-tab--active' : ''}`}
                            onClick={() => setActiveTab(tab)}
                        >
                            {tab === 'info' && '👤 Основное'}
                            {tab === 'security' && '🔒 Безопасность'}
                            {tab === 'stats' && '📊 Статистика'}
                        </button>
                    ))}
                </div>

                {/* ══ Таб: Основное ══ */}
                {activeTab === 'info' && (
                    <div className="profile-tab-content">
                        <div className="profile-section">
                            <div className="profile-section__title"><span>✏️</span> Личные данные</div>
                            <div className="form-group">
                                <label>Email</label>
                                <input type="email" value={user?.email || ''} readOnly disabled className="input-disabled" />
                            </div>
                            <div className="flex gap-2">
                                <div className="form-group" style={{ flex: 1 }}>
                                    <label>Имя</label>
                                    <input type="text" value={firstName} onChange={e => setFirstName(e.target.value)} placeholder="Иван" />
                                </div>
                                <div className="form-group" style={{ flex: 1 }}>
                                    <label>Фамилия</label>
                                    <input type="text" value={lastName} onChange={e => setLastName(e.target.value)} placeholder="Иванов" />
                                </div>
                            </div>
                            <div style={{ marginTop: '0.5rem', display: 'flex', gap: '0.75rem', justifyContent: 'flex-end' }}>
                                <button className="btn btn-primary" onClick={handleSave}>Сохранить</button>
                                <button className="btn btn-secondary" onClick={() => onNavigate('dashboard')}>← Назад</button>
                            </div>
                        </div>

                        <div style={{ padding: '0.75rem 1rem', background: 'var(--bg-secondary)', borderRadius: 'var(--radius-md)', border: '1px solid var(--border-color)', display: 'flex', alignItems: 'center', gap: '0.75rem', fontSize: '0.84rem', color: 'var(--text-secondary)', marginTop: '0.5rem' }}>
                            <span>🌙</span>
                            <span>Переключить тему можно кнопкой <strong>🌙/☀️</strong> в шапке главной страницы</span>
                        </div>
                    </div>
                )}

                {/* ══ Таб: Безопасность ══ */}
                {activeTab === 'security' && (
                    <div className="profile-tab-content">

                        {/* Смена пароля */}
                        <div className="profile-section">
                            <div className="profile-section__title"><span>🔑</span> Смена пароля</div>
                            <form onSubmit={handleChangePassword}>
                                <div className="form-group">
                                    <label>Текущий пароль</label>
                                    <div className="pw-input-wrap">
                                        <input
                                            type={showCurrentPw ? 'text' : 'password'}
                                            value={currentPw}
                                            onChange={e => setCurrentPw(e.target.value)}
                                            placeholder="Введите текущий пароль"
                                            required
                                        />
                                        <button type="button" className="pw-eye" onClick={() => setShowCurrentPw(v => !v)}>
                                            {showCurrentPw ? '🙈' : '👁'}
                                        </button>
                                    </div>
                                </div>
                                <div className="form-group">
                                    <label>Новый пароль</label>
                                    <div className="pw-input-wrap">
                                        <input
                                            type={showNewPw ? 'text' : 'password'}
                                            value={newPw}
                                            onChange={e => setNewPw(e.target.value)}
                                            placeholder="Минимум 6 символов"
                                            required
                                            minLength={6}
                                        />
                                        <button type="button" className="pw-eye" onClick={() => setShowNewPw(v => !v)}>
                                            {showNewPw ? '🙈' : '👁'}
                                        </button>
                                    </div>
                                    {/* Индикатор силы */}
                                    {newPw && (
                                        <div className="pw-strength">
                                            {[1,2,3,4].map(i => (
                                                <div key={i} className={`pw-strength__bar ${newPw.length >= i * 3 ? 'pw-strength__bar--filled' : ''}`}
                                                    style={{ '--bar-color': newPw.length >= 12 ? '#10b981' : newPw.length >= 8 ? '#f59e0b' : '#ef4444' } as React.CSSProperties}
                                                />
                                            ))}
                                            <span className="pw-strength__label">
                                                {newPw.length >= 12 ? 'Надёжный' : newPw.length >= 8 ? 'Средний' : 'Слабый'}
                                            </span>
                                        </div>
                                    )}
                                </div>
                                <div className="form-group">
                                    <label>Подтвердите новый пароль</label>
                                    <input
                                        type="password"
                                        value={confirmPw}
                                        onChange={e => setConfirmPw(e.target.value)}
                                        placeholder="Повторите новый пароль"
                                        required
                                        style={{ borderColor: confirmPw && confirmPw !== newPw ? 'var(--danger-color)' : undefined }}
                                    />
                                    {confirmPw && confirmPw !== newPw && (
                                        <span style={{ fontSize: '0.78rem', color: 'var(--danger-color)', marginTop: '0.3rem', display: 'block' }}>
                                            Пароли не совпадают
                                        </span>
                                    )}
                                </div>
                                <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                                    <button
                                        type="submit"
                                        className="btn btn-primary"
                                        disabled={changingPw || !currentPw || !newPw || newPw !== confirmPw}
                                    >
                                        {changingPw ? <ClipLoader size={16} color="#fff" /> : '🔑 Сменить пароль'}
                                    </button>
                                </div>
                            </form>
                        </div>

                        {/* 2FA */}
                        <div className="profile-section">
                            <div className="profile-section__title"><span>📱</span> Двухфакторная аутентификация</div>
                            <div className="security-row">
                                <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', flex: 1 }}>
                                    <div style={{
                                        width: 40, height: 40, borderRadius: 10, flexShrink: 0,
                                        background: is2FAEnabled ? 'linear-gradient(135deg,#10b981,#059669)' : 'var(--bg-primary)',
                                        border: is2FAEnabled ? 'none' : '1px solid var(--border-color)',
                                        display: 'flex', alignItems: 'center', justifyContent: 'center',
                                    }}>
                                        <svg width="18" height="18" fill="none" viewBox="0 0 24 24" stroke={is2FAEnabled ? 'white' : 'var(--text-secondary)'} strokeWidth="2">
                                            <path strokeLinecap="round" strokeLinejoin="round" d="M10.5 1.5H8.25A2.25 2.25 0 006 3.75v16.5a2.25 2.25 0 002.25 2.25h7.5A2.25 2.25 0 0018 20.25V3.75a2.25 2.25 0 00-2.25-2.25H13.5m-3 0V3h3V1.5m-3 0h3m-3 8.25h3m-3 3h3m-3 3h3" />
                                        </svg>
                                    </div>
                                    <div>
                                        <div style={{ fontWeight: 600, fontSize: '0.9rem', display: 'flex', alignItems: 'center', gap: 8 }}>
                                            Google Authenticator
                                            {is2FAEnabled && <span style={{ fontSize: '0.7rem', padding: '1px 7px', borderRadius: 99, background: 'rgba(16,185,129,0.15)', color: '#10b981', border: '1px solid rgba(16,185,129,0.3)', fontWeight: 700 }}>ВКЛЮЧЕНА</span>}
                                        </div>
                                        <p style={{ margin: 0, fontSize: '0.78rem', color: 'var(--text-secondary)' }}>
                                            {is2FAEnabled ? 'Аккаунт защищён приложением-аутентификатором' : 'Добавьте дополнительный уровень защиты'}
                                        </p>
                                    </div>
                                </div>
                                {is2FAEnabled ? (
                                    <button className="btn btn-secondary btn-small" onClick={() => setShowDisable2FA(!showDisable2FA)}>Отключить</button>
                                ) : (
                                    <button className="btn btn-primary btn-small" onClick={() => setShow2FASetup(true)}>Включить</button>
                                )}
                            </div>
                            {showDisable2FA && is2FAEnabled && (
                                <form onSubmit={handleDisable2FA} style={{ marginTop: 12, padding: 16, borderRadius: 12, border: '1px solid rgba(239,68,68,0.3)', background: 'rgba(239,68,68,0.05)' }}>
                                    <p style={{ margin: '0 0 12px', fontSize: '0.875rem', color: 'var(--text-secondary)' }}>
                                        Введите код из приложения:
                                    </p>
                                    <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
                                        <input type="text" inputMode="numeric" placeholder="000000" value={disable2FACode}
                                            onChange={e => setDisable2FACode(e.target.value.replace(/\D/g,'').slice(0,6))}
                                            maxLength={6} autoFocus style={{ flex: 1, minWidth: 120, textAlign: 'center', fontSize: '1.25rem', letterSpacing: '0.4rem', fontWeight: 700, fontFamily: 'monospace' }}
                                        />
                                        <button type="button" className="btn btn-secondary btn-small" onClick={() => { setShowDisable2FA(false); setDisable2FACode(''); }} disabled={disabling2FA}>Отмена</button>
                                        <button type="submit" disabled={disabling2FA || disable2FACode.length !== 6} style={{ padding: '8px 16px', borderRadius: 8, border: 'none', background: '#ef4444', color: 'white', fontWeight: 600, cursor: 'pointer', opacity: disabling2FA || disable2FACode.length !== 6 ? 0.5 : 1 }}>
                                            {disabling2FA ? <ClipLoader color="#fff" size={14} /> : 'Отключить'}
                                        </button>
                                    </div>
                                </form>
                            )}
                        </div>
                    </div>
                )}

                {/* ══ Таб: Статистика ══ */}
                {activeTab === 'stats' && (
                    <div className="profile-tab-content">
                    {loadingStats && (
                        <div style={{ display: 'flex', justifyContent: 'center', padding: '2rem' }}>
                            <ClipLoader size={32} color="var(--accent-color)" />
                        </div>
                    )}
                    {!loadingStats && (<>

                        {/* Детальная статистика */}
                        <div className="profile-section">
                            <div className="profile-section__title"><span>📊</span> Детальная статистика</div>
                            <div className="stats-grid">
                                {[
                                    { label: 'Проектов', value: stats.projects, icon: '📁', color: '#6366f1' },
                                    { label: 'Сущностей', value: stats.entities, icon: '🧩', color: '#10b981' },
                                    { label: 'Полей', value: stats.fields, icon: '🔧', color: '#f59e0b' },
                                    { label: 'Генераций', value: stats.generations, icon: '📦', color: '#3b82f6' },
                                ].map(s => (
                                    <div key={s.label} className="stat-card" style={{ '--stat-color': s.color } as React.CSSProperties}>
                                        <div className="stat-card__icon">{s.icon}</div>
                                        <div className="stat-card__value">{s.value}</div>
                                        <div className="stat-card__label">{s.label}</div>
                                    </div>
                                ))}
                            </div>
                            {stats.lastGen && (
                                <p style={{ margin: '0.75rem 0 0', fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
                                    🕒 Последняя генерация: <strong>{stats.lastGen}</strong>
                                </p>
                            )}
                        </div>

                        {/* Heatmap */}
                        <ActivityHeatmap history={genHistory} />

                        {/* Достижения */}
                        <div className="profile-section">
                            <div className="profile-section__title">
                                <span>🏆</span> Достижения
                                <span className="profile-section__badge">{unlockedCount} / {achievements.length}</span>
                            </div>
                            <div className="achievements-grid">
                                {achievements.map(a => (
                                    <div key={a.id} className={`achievement-card ${a.unlocked ? 'achievement-card--unlocked' : ''}`}
                                        style={{ '--ach-color': a.color } as React.CSSProperties}
                                    >
                                        <div className="achievement-card__icon">{a.unlocked ? a.icon : '🔒'}</div>
                                        <div className="achievement-card__title">{a.title}</div>
                                        <div className="achievement-card__desc">{a.description}</div>
                                        {a.unlocked && <div className="achievement-card__check">✓</div>}
                                    </div>
                                ))}
                            </div>
                        </div>
                    </>)}
                    </div>
                )}
            </div>

            {show2FASetup && (
                <TwoFactorSetupModal onClose={() => setShow2FASetup(false)} onEnabled={() => setShow2FASetup(false)} />
            )}
        </div>
    );
};

export default Profile;

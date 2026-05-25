import React, { useRef, useState } from 'react';
import { useAppDispatch, useAppSelector } from '../app/hooks';
import { setUser, updateUser, disable2FA } from '../features/auth/authSlice';
import { Page } from '../App';
import api from '../utils/api';
import { toast } from 'react-toastify';
import { ClipLoader } from 'react-spinners';
import TwoFactorSetupModal from './TwoFactorSetupModal';

interface ProfileProps {
    onNavigate: (page: Page) => void;
}

const Profile: React.FC<ProfileProps> = ({ onNavigate }) => {
    const dispatch = useAppDispatch();
    const { user } = useAppSelector((state) => state.auth);
    const fileInputRef = useRef<HTMLInputElement>(null);
    const [uploading, setUploading] = useState(false);

    const [firstName, setFirstName] = useState(user?.firstName || '');
    const [lastName, setLastName] = useState(user?.lastName || '');
    const [isDarkMode, setIsDarkMode] = useState(user?.isDarkMode || false);

    // 2FA state
    const [show2FASetup, setShow2FASetup] = useState(false);
    const [showDisable2FA, setShowDisable2FA] = useState(false);
    const [disable2FACode, setDisable2FACode] = useState('');
    const [disabling2FA, setDisabling2FA] = useState(false);

    // Sync local state when user data changes (e.g. after initial load)
    React.useEffect(() => {
        if (user) {
            setFirstName(user.firstName || '');
            setLastName(user.lastName || '');
            setIsDarkMode(user.isDarkMode || false);
        }
    }, [user]);



    const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
        const file = e.target.files?.[0];
        if (!file) return;

        const formData = new FormData();
        formData.append('file', file);

        setUploading(true);
        try {
            const response = await api.post('/auth/avatar', formData, {
                headers: {
                    'Content-Type': 'multipart/form-data',
                },
            });

            if (user) {
                dispatch(setUser({ ...user, avatarUrl: response.data.avatarUrl }));
                toast.success('Аватар успешно обновлен');
            }
        } catch (error) {
            console.error('Error uploading avatar:', error);
            toast.error('Ошибка загрузки аватара');
        } finally {
            setUploading(false);
        }
    };

    const handleAvatarClick = () => {
        fileInputRef.current?.click();
    };

    const handleSave = async () => {
        if (!user) return;

        try {
            await dispatch(updateUser({
                id: user.id,
                data: {
                    firstName,
                    lastName,
                    isDarkMode
                }
            })).unwrap();
            toast.success('Профиль обновлен');
        } catch (error) {
            toast.error('Ошибка обновления профиля');
        }
    };

    const handleDisable2FA = async (e: React.FormEvent) => {
        e.preventDefault();
        if (disable2FACode.length !== 6) {
            toast.error('Введите 6-значный код');
            return;
        }
        setDisabling2FA(true);
        try {
            await dispatch(disable2FA(disable2FACode)).unwrap();
            toast.success('Двухфакторная аутентификация отключена');
            setShowDisable2FA(false);
            setDisable2FACode('');
        } catch (err: any) {
            toast.error(err || 'Неверный код. Попробуйте ещё раз.');
        } finally {
            setDisabling2FA(false);
        }
    };

    const is2FAEnabled = user?.twoFactorEnabled === true;

    return (
        <div className="login-container animate-fade-in">
            <div className="login-card animate-slide-up" style={{ maxWidth: '600px' }}>
                <h1>Профиль</h1>

                {/* Avatar Section */}
                <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', marginBottom: '2rem' }}>
                    <div
                        onClick={handleAvatarClick}
                        style={{
                            width: '120px',
                            height: '120px',
                            borderRadius: '50%',
                            backgroundColor: '#f3f4f6',
                            backgroundImage: user?.avatarUrl ? `url(${import.meta.env.VITE_IMG_URL}${user.avatarUrl})` : 'none',
                            backgroundSize: 'cover',
                            backgroundPosition: 'center',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            cursor: 'pointer',
                            border: '4px solid white',
                            boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.1)',
                            position: 'relative',
                            overflow: 'hidden'
                        }}
                    >
                        {!user?.avatarUrl && (
                            <span style={{ fontSize: '3rem', color: '#9ca3af' }}>
                                {user?.firstName?.[0] || user?.email?.[0] || '?'}
                            </span>
                        )}
                        {uploading && (
                            <div style={{
                                position: 'absolute',
                                inset: 0,
                                backgroundColor: 'rgba(0,0,0,0.5)',
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                                color: 'white'
                            }}>
                                <ClipLoader color="#ffffff" size={30} />
                            </div>
                        )}
                    </div>
                    <button
                        onClick={handleAvatarClick}
                        className="btn btn-secondary btn-small"
                        style={{ marginTop: '1rem' }}
                    >
                        Изменить фото
                    </button>
                    <input
                        type="file"
                        ref={fileInputRef}
                        onChange={handleFileChange}
                        style={{ display: 'none' }}
                        accept="image/*"
                    />
                </div>

                <div className="form-group">
                    <label>Email</label>
                    <input
                        type="text"
                        value={user?.email || ''}
                        readOnly
                        disabled
                        className="input-disabled"
                    />
                </div>

                <div className="flex gap-2 mb-4">
                    <div className="form-group" style={{ flex: 1 }}>
                        <label>Имя</label>
                        <input
                            type="text"
                            value={firstName}
                            onChange={(e) => setFirstName(e.target.value)}
                            placeholder="Иван"
                        />
                    </div>
                    <div className="form-group" style={{ flex: 1 }}>
                        <label>Фамилия</label>
                        <input
                            type="text"
                            value={lastName}
                            onChange={(e) => setLastName(e.target.value)}
                            placeholder="Иванов"
                        />
                    </div>
                </div>

                <div className="form-group checkbox-group" style={{ marginBottom: '2rem' }}>
                    <label style={{ display: 'flex', alignItems: 'center', gap: '10px', cursor: 'pointer' }}>
                        <input
                            type="checkbox"
                            checked={isDarkMode}
                            onChange={(e) => setIsDarkMode(e.target.checked)}
                        />
                        <span>Темный режим</span>
                    </label>
                </div>

                {/* ── Security Section ── */}
                <div style={{
                    borderTop: '1px solid var(--border-color)',
                    paddingTop: '1.5rem',
                    marginBottom: '1.5rem'
                }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '1rem' }}>
                        <svg width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="var(--accent-color)" strokeWidth="2">
                            <path strokeLinecap="round" strokeLinejoin="round" d="M9 12.75L11.25 15 15 9.75m-3-7.036A11.959 11.959 0 013.598 6 11.99 11.99 0 003 9.749c0 5.592 3.824 10.29 9 11.623 5.176-1.332 9-6.03 9-11.622 0-1.31-.21-2.571-.598-3.751h-.152c-3.196 0-6.1-1.248-8.25-3.285z" />
                        </svg>
                        <h3 style={{ margin: 0, fontSize: '1rem', fontWeight: 600 }}>Безопасность</h3>
                    </div>

                    {/* 2FA Row */}
                    <div style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        padding: '14px 16px',
                        borderRadius: '12px',
                        border: '1px solid var(--border-color)',
                        background: 'var(--bg-secondary)',
                        gap: '12px',
                        flexWrap: 'wrap'
                    }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '12px', flex: 1, minWidth: '200px' }}>
                            <div style={{
                                width: '40px',
                                height: '40px',
                                borderRadius: '10px',
                                background: is2FAEnabled
                                    ? 'linear-gradient(135deg, #10b981, #059669)'
                                    : 'var(--bg-primary)',
                                border: is2FAEnabled ? 'none' : '1px solid var(--border-color)',
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                                flexShrink: 0,
                                transition: 'background 0.3s ease'
                            }}>
                                <svg width="18" height="18" fill="none" viewBox="0 0 24 24"
                                    stroke={is2FAEnabled ? 'white' : 'var(--text-secondary)'} strokeWidth="2">
                                    <path strokeLinecap="round" strokeLinejoin="round" d="M10.5 1.5H8.25A2.25 2.25 0 006 3.75v16.5a2.25 2.25 0 002.25 2.25h7.5A2.25 2.25 0 0018 20.25V3.75a2.25 2.25 0 00-2.25-2.25H13.5m-3 0V3h3V1.5m-3 0h3m-3 8.25h3m-3 3h3m-3 3h3" />
                                </svg>
                            </div>
                            <div>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap' }}>
                                    <span style={{ fontWeight: 600, fontSize: '0.925rem' }}>
                                        Двухфакторная аутентификация
                                    </span>
                                    {is2FAEnabled && (
                                        <span style={{
                                            fontSize: '0.72rem',
                                            fontWeight: 600,
                                            padding: '2px 8px',
                                            borderRadius: '20px',
                                            background: 'rgba(16, 185, 129, 0.15)',
                                            color: '#10b981',
                                            border: '1px solid rgba(16,185,129,0.3)',
                                            letterSpacing: '0.03em'
                                        }}>
                                            ВКЛЮЧЕНА
                                        </span>
                                    )}
                                </div>
                                <p style={{ margin: 0, fontSize: '0.8rem', color: 'var(--text-secondary)', marginTop: '2px' }}>
                                    {is2FAEnabled
                                        ? 'Ваш аккаунт защищён Google Authenticator'
                                        : 'Добавьте защиту через Google Authenticator'}
                                </p>
                            </div>
                        </div>

                        {/* Toggle button */}
                        {is2FAEnabled ? (
                            <button
                                onClick={() => setShowDisable2FA(!showDisable2FA)}
                                className="btn btn-secondary btn-small"
                                style={{ flexShrink: 0 }}
                            >
                                Отключить
                            </button>
                        ) : (
                            <button
                                onClick={() => setShow2FASetup(true)}
                                className="btn btn-primary btn-small"
                                style={{ flexShrink: 0 }}
                            >
                                Включить
                            </button>
                        )}
                    </div>

                    {/* Disable 2FA form (inline) */}
                    {showDisable2FA && is2FAEnabled && (
                        <form
                            onSubmit={handleDisable2FA}
                            style={{
                                marginTop: '12px',
                                padding: '16px',
                                borderRadius: '12px',
                                border: '1px solid rgba(239,68,68,0.3)',
                                background: 'rgba(239,68,68,0.05)',
                                animation: 'fadeIn 0.2s ease'
                            }}
                        >
                            <p style={{ margin: '0 0 12px', fontSize: '0.875rem', color: 'var(--text-secondary)' }}>
                                Введите код из Google Authenticator для подтверждения отключения 2FA:
                            </p>
                            <div style={{ display: 'flex', gap: '10px', flexWrap: 'wrap' }}>
                                <input
                                    type="text"
                                    inputMode="numeric"
                                    placeholder="000000"
                                    value={disable2FACode}
                                    onChange={(e) => setDisable2FACode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                                    maxLength={6}
                                    autoFocus
                                    style={{
                                        flex: 1,
                                        minWidth: '120px',
                                        textAlign: 'center',
                                        fontSize: '1.25rem',
                                        letterSpacing: '0.4rem',
                                        fontWeight: 700,
                                        fontFamily: 'monospace'
                                    }}
                                />
                                <button
                                    type="button"
                                    onClick={() => { setShowDisable2FA(false); setDisable2FACode(''); }}
                                    className="btn btn-secondary btn-small"
                                    disabled={disabling2FA}
                                    style={{ alignSelf: 'center' }}
                                >
                                    Отмена
                                </button>
                                <button
                                    type="submit"
                                    disabled={disabling2FA || disable2FACode.length !== 6}
                                    style={{
                                        alignSelf: 'center',
                                        padding: '8px 16px',
                                        borderRadius: '8px',
                                        border: 'none',
                                        background: '#ef4444',
                                        color: 'white',
                                        fontWeight: 600,
                                        cursor: 'pointer',
                                        fontSize: '0.875rem',
                                        opacity: disabling2FA || disable2FACode.length !== 6 ? 0.5 : 1
                                    }}
                                >
                                    {disabling2FA ? <ClipLoader color="#fff" size={14} /> : 'Отключить'}
                                </button>
                            </div>
                        </form>
                    )}
                </div>

                <div className="flex gap-2" style={{ marginTop: '1rem', justifyContent: 'flex-end' }}>
                    <button
                        onClick={handleSave}
                        className="btn btn-primary"
                    >
                        Сохранить
                    </button>
                    <button
                        onClick={() => onNavigate('dashboard')}
                        className="btn btn-secondary"
                    >
                        Назад
                    </button>
                </div>
            </div>

            {/* 2FA Setup Modal */}
            {show2FASetup && (
                <TwoFactorSetupModal
                    onClose={() => setShow2FASetup(false)}
                    onEnabled={() => setShow2FASetup(false)}
                />
            )}
        </div>
    );
};

export default Profile;

import React, { useRef, useState } from 'react';
import { useAppDispatch, useAppSelector } from '../app/hooks';
import { logout, setUser } from '../features/auth/authSlice';
import { Page } from '../App';
import api from '../utils/api';
import { toast } from 'react-toastify';
import { ClipLoader } from 'react-spinners';

interface ProfileProps {
    onNavigate: (page: Page) => void;
}

const Profile: React.FC<ProfileProps> = ({ onNavigate }) => {
    const dispatch = useAppDispatch();
    const { user } = useAppSelector((state) => state.auth);
    const fileInputRef = useRef<HTMLInputElement>(null);
    const [uploading, setUploading] = useState(false);

    const handleLogout = () => {
        dispatch(logout());
        toast.info('Вы вышли из системы');
    };

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
                            backgroundImage: user?.avatarUrl ? `url(http://localhost:5123${user.avatarUrl})` : 'none',
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
                        style={{ backgroundColor: '#f9fafb' }}
                    />
                </div>

                <div className="flex gap-2 mb-4">
                    <div className="form-group" style={{ flex: 1 }}>
                        <label>Имя</label>
                        <input
                            type="text"
                            value={user?.firstName || ''}
                            readOnly
                            disabled
                            style={{ backgroundColor: '#f9fafb' }}
                        />
                    </div>
                    <div className="form-group" style={{ flex: 1 }}>
                        <label>Фамилия</label>
                        <input
                            type="text"
                            value={user?.lastName || ''}
                            readOnly
                            disabled
                            style={{ backgroundColor: '#f9fafb' }}
                        />
                    </div>
                </div>

                <div className="flex gap-2" style={{ marginTop: '2rem', justifyContent: 'flex-end' }}>
                    <button
                        onClick={() => onNavigate('dashboard')}
                        className="btn btn-secondary"
                    >
                        Назад
                    </button>
                    <button
                        onClick={handleLogout}
                        className="btn btn-danger"
                    >
                        Выйти
                    </button>
                </div>
            </div>
        </div>
    );
};

export default Profile;

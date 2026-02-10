import { useState } from 'react';
import { useAppDispatch } from '../app/hooks';
import { forgotPassword, resetPassword } from '../features/auth/authSlice';
import { toast } from 'react-toastify';
import { ClipLoader } from 'react-spinners';

interface ResetPasswordModalProps {
    onClose: () => void;
}

export default function ResetPasswordModal({ onClose }: ResetPasswordModalProps) {
    const dispatch = useAppDispatch();
    const [step, setStep] = useState(1); // 1: Email, 2: Code & New Password
    const [email, setEmail] = useState('');
    const [code, setCode] = useState('');
    const [newPassword, setNewPassword] = useState('');
    const [loading, setLoading] = useState(false);

    const handleSendCode = async (e: React.FormEvent) => {
        e.preventDefault();
        setLoading(true);
        try {
            await dispatch(forgotPassword(email)).unwrap();
            toast.success('Код для сброса пароля отправлен на почту');
            setStep(2);
        } catch (error: any) {
            toast.error(error || 'Ошибка отправки кода');
        } finally {
            setLoading(false);
        }
    };

    const handleResetPassword = async (e: React.FormEvent) => {
        e.preventDefault();
        setLoading(true);
        try {
            await dispatch(resetPassword({ email, code, newPassword })).unwrap();
            toast.success('Пароль успешно изменен');
            onClose();
        } catch (error: any) {
            toast.error(error || 'Ошибка сброса пароля');
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="modal-overlay animate-fade-in" onClick={onClose}>
            <div className="modal animate-scale-in" onClick={(e) => e.stopPropagation()}>
                <button
                    onClick={onClose}
                    style={{ position: 'absolute', top: '1rem', right: '1rem', background: 'none', border: 'none', cursor: 'pointer', fontSize: '1.5rem' }}
                >
                    ✕
                </button>

                <h2>{step === 1 ? 'Сброс пароля' : 'Новый пароль'}</h2>

                {step === 1 ? (
                    <form onSubmit={handleSendCode}>
                        <div className="form-group">
                            <label>Email</label>
                            <input
                                type="email"
                                value={email}
                                onChange={(e) => setEmail(e.target.value)}
                                placeholder="user@example.com"
                                required
                            />
                        </div>
                        <button type="submit" className="btn btn-primary" disabled={loading} style={{ width: '100%' }}>
                            {loading ? <ClipLoader size={20} color="#fff" /> : 'Отправить код'}
                        </button>
                    </form>
                ) : (
                    <form onSubmit={handleResetPassword}>
                        <div className="form-group">
                            <label>Код из письма</label>
                            <input
                                type="text"
                                value={code}
                                onChange={(e) => setCode(e.target.value)}
                                placeholder="123456"
                                required
                                maxLength={6}
                                style={{ textAlign: 'center', letterSpacing: '0.2rem' }}
                            />
                        </div>
                        <div className="form-group">
                            <label>Новый пароль</label>
                            <input
                                type="password"
                                value={newPassword}
                                onChange={(e) => setNewPassword(e.target.value)}
                                placeholder="••••••••"
                                required
                                minLength={6}
                            />
                        </div>
                        <button type="submit" className="btn btn-primary" disabled={loading} style={{ width: '100%' }}>
                            {loading ? <ClipLoader size={20} color="#fff" /> : 'Сменить пароль'}
                        </button>
                    </form>
                )}
            </div>
        </div>
    );
}

import { useState, useEffect } from 'react';
import { useAppDispatch, useAppSelector } from '../app/hooks';
import { login, register, clearError, sendVerificationCode } from '../features/auth/authSlice';
import { toast } from 'react-toastify';
import { ClipLoader } from 'react-spinners';
import VerificationModal from './VerificationModal';
import ResetPasswordModal from './ResetPasswordModal';
import PersonalDataModal from './PersonalDataModal';

function Login() {
    const dispatch = useAppDispatch();
    const { loading, error } = useAppSelector((state) => state.auth);
    const [isLogin, setIsLogin] = useState(true);
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [firstName, setFirstName] = useState('');
    const [lastName, setLastName] = useState('');

    const [showVerification, setShowVerification] = useState(false);
    const [showResetPassword, setShowResetPassword] = useState(false);
    const [verificationEmail, setVerificationEmail] = useState('');
    const [showPersonalDataModal, setShowPersonalDataModal] = useState(false);
    const [acceptedPersonalData, setAcceptedPersonalData] = useState(false);

    useEffect(() => {
        if (error) {
            toast.error(error);
            dispatch(clearError());
        }
    }, [error, dispatch]);

    const handleRegister = async (code: string) => {
        try {
            await dispatch(register({ email, password, firstName, lastName, code })).unwrap();
            toast.success('Регистрация успешна!');
            setShowVerification(false);
            // Login handled by redux
        } catch (err) {
            // Error handled by redux
            toast.error('Ошибка регистрации. Возможно, неверный код.');
            throw err;
        }
    };

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();

        if (isLogin) {
            dispatch(login({ email, password }));
        } else {
            // Send verification code first
            try {
                // @ts-ignore
                const action = await dispatch(sendVerificationCode(email));
                if (sendVerificationCode.fulfilled.match(action)) {
                    setVerificationEmail(email);
                    setShowVerification(true);
                } else {
                    if (action.payload) toast.error(action.payload as string);
                }
            } catch (err) {
                console.error(err);
            }
        }
    };

    return (
        <div className="login-container animate-fade-in">
            <div className="login-card animate-slide-up">
                <div style={{ display: 'flex', justifyContent: 'center', marginBottom: '1.5rem' }}>
                    <img src="/logo.svg" alt="CodeForge" style={{ height: '60px', width: 'auto' }} />
                </div>
                <h1>{isLogin ? 'Вход' : 'Регистрация'}</h1>

                <form onSubmit={handleSubmit}>
                    {!isLogin && (
                        <>
                            <div className="form-group">
                                <label>Имя</label>
                                <input
                                    type="text"
                                    value={firstName}
                                    onChange={(e) => setFirstName(e.target.value)}
                                    placeholder="Иван"
                                />
                            </div>

                            <div className="form-group">
                                <label>Фамилия</label>
                                <input
                                    type="text"
                                    value={lastName}
                                    onChange={(e) => setLastName(e.target.value)}
                                    placeholder="Иванов"
                                />
                            </div>
                        </>
                    )}

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



                    <div className="form-group">
                        <label>Пароль</label>
                        <input
                            type="password"
                            value={password}
                            onChange={(e) => setPassword(e.target.value)}
                            placeholder="••••••••"
                            required
                        />
                    </div>

                    {!isLogin && (
                        <div className="form-group" style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                            <input
                                type="checkbox"
                                id="personalData"
                                checked={acceptedPersonalData}
                                onChange={(e) => setAcceptedPersonalData(e.target.checked)}
                                style={{ width: 'auto', margin: 0 }}
                            />
                            <label htmlFor="personalData" style={{ margin: 0, fontSize: '0.9rem', cursor: 'pointer' }}>
                                Я согласен на{' '}
                                <span
                                    onClick={(e) => {
                                        e.preventDefault();
                                        setShowPersonalDataModal(true);
                                    }}
                                    style={{ color: 'var(--accent-color)', textDecoration: 'underline', cursor: 'pointer' }}
                                >
                                    обработку персональных данных
                                </span>
                            </label>
                        </div>
                    )}

                    {isLogin && (
                        <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: '1rem' }}>
                            <button
                                type="button"
                                onClick={() => setShowResetPassword(true)}
                                style={{
                                    background: 'none',
                                    border: 'none',
                                    color: 'var(--accent-color)',
                                    cursor: 'pointer',
                                    fontSize: '0.875rem',
                                    padding: 0,
                                    textDecoration: 'underline'
                                }}
                            >
                                Забыли пароль?
                            </button>
                        </div>
                    )}

                    <button type="submit" className="btn btn-primary" disabled={loading || (!isLogin && !acceptedPersonalData)} style={{ width: '100%' }}>
                        {loading ? <ClipLoader color="#ffffff" size={20} /> : isLogin ? 'Войти' : 'Зарегистрироваться'}
                    </button>
                </form>

                <div style={{ marginTop: '1.5rem', textAlign: 'center' }}>
                    <p style={{ marginBottom: '0.5rem', color: 'var(--text-secondary)', fontSize: '0.9rem' }}>
                        {isLogin ? 'Нет аккаунта?' : 'Уже есть аккаунт?'}
                    </p>
                    <button
                        className="btn btn-secondary"
                        onClick={() => setIsLogin(!isLogin)}
                        style={{ width: '100%' }}
                    >
                        {isLogin ? 'Создать аккаунт' : 'Войти в аккаунт'}
                    </button>
                </div>
            </div>

            {showVerification && (
                <VerificationModal
                    email={verificationEmail}
                    onClose={() => setShowVerification(false)}
                    onConfirm={handleRegister}
                />
            )}

            {showResetPassword && (
                <ResetPasswordModal
                    onClose={() => setShowResetPassword(false)}
                />
            )}

            {showPersonalDataModal && (
                <PersonalDataModal
                    onClose={() => setShowPersonalDataModal(false)}
                />
            )}
        </div>
    );
}

export default Login;

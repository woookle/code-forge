import React, { useState, useRef, useEffect } from 'react';
import { useAppDispatch, useAppSelector } from '../app/hooks';
import { loginWith2FA, clearTwoFactorState } from '../features/auth/authSlice';
import { toast } from 'react-toastify';
import { ClipLoader } from 'react-spinners';

interface TotpModalProps {
    email: string;
    password: string;
    onClose: () => void;
}

const TotpModal: React.FC<TotpModalProps> = ({ email, password, onClose }) => {
    const dispatch = useAppDispatch();
    const { loading } = useAppSelector((state) => state.auth);
    const [digits, setDigits] = useState<string[]>(['', '', '', '', '', '']);
    const inputRefs = useRef<(HTMLInputElement | null)[]>([]);

    useEffect(() => {
        // Auto-focus first input
        setTimeout(() => inputRefs.current[0]?.focus(), 100);
    }, []);

    const getCode = () => digits.join('');

    const handleDigitChange = (index: number, value: string) => {
        const digit = value.replace(/\D/g, '').slice(-1);
        const newDigits = [...digits];
        newDigits[index] = digit;
        setDigits(newDigits);

        // Move to next input automatically
        if (digit && index < 5) {
            inputRefs.current[index + 1]?.focus();
        }
    };

    const handleKeyDown = (index: number, e: React.KeyboardEvent<HTMLInputElement>) => {
        if (e.key === 'Backspace') {
            if (digits[index] === '' && index > 0) {
                const newDigits = [...digits];
                newDigits[index - 1] = '';
                setDigits(newDigits);
                inputRefs.current[index - 1]?.focus();
            } else {
                const newDigits = [...digits];
                newDigits[index] = '';
                setDigits(newDigits);
            }
        } else if (e.key === 'ArrowLeft' && index > 0) {
            inputRefs.current[index - 1]?.focus();
        } else if (e.key === 'ArrowRight' && index < 5) {
            inputRefs.current[index + 1]?.focus();
        } else if (e.key === 'Enter') {
            handleSubmit();
        }
    };

    const handlePaste = (e: React.ClipboardEvent) => {
        e.preventDefault();
        const pasted = e.clipboardData.getData('text').replace(/\D/g, '').slice(0, 6);
        if (pasted.length > 0) {
            const newDigits = ['', '', '', '', '', ''];
            for (let i = 0; i < 6; i++) {
                newDigits[i] = pasted[i] || '';
            }
            setDigits(newDigits);
            const lastIndex = Math.min(pasted.length - 1, 5);
            inputRefs.current[lastIndex]?.focus();
        }
    };

    const handleSubmit = async () => {
        const code = getCode().trim();
        if (code.length !== 6) {
            toast.error('Введите полный 6-значный код');
            return;
        }
        try {
            await dispatch(loginWith2FA({ email, password, totpCode: code })).unwrap();
            // Success — redux sets isAuthenticated, modal unmounts automatically
        } catch (err: any) {
            toast.error(err || 'Неверный код. Попробуйте ещё раз.');
            setDigits(['', '', '', '', '', '']);
            setTimeout(() => inputRefs.current[0]?.focus(), 50);
        }
    };

    const handleClose = () => {
        dispatch(clearTwoFactorState());
        onClose();
    };

    return (
        <div
            className="modal-overlay"
            onClick={handleClose}
            style={{ zIndex: 1200 }}
        >
            <div
                className="modal"
                onClick={(e) => e.stopPropagation()}
                style={{ maxWidth: '420px', width: '90%', textAlign: 'center', padding: '2.5rem 2rem' }}
            >
                {/* Shield icon */}
                <div style={{ display: 'flex', justifyContent: 'center', marginBottom: '1.25rem' }}>
                    <div style={{
                        width: '68px',
                        height: '68px',
                        borderRadius: '20px',
                        background: 'linear-gradient(135deg, #6366f1, #8b5cf6)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        boxShadow: '0 8px 28px rgba(99,102,241,0.35)'
                    }}>
                        <svg width="32" height="32" fill="none" viewBox="0 0 24 24" stroke="white" strokeWidth="1.5">
                            <path strokeLinecap="round" strokeLinejoin="round" d="M10.5 1.5H8.25A2.25 2.25 0 006 3.75v16.5a2.25 2.25 0 002.25 2.25h7.5A2.25 2.25 0 0018 20.25V3.75a2.25 2.25 0 00-2.25-2.25H13.5m-3 0V3h3V1.5m-3 0h3m-3 8.25h3m-3 3h3m-3 3h3" />
                        </svg>
                    </div>
                </div>

                <h2 style={{ marginBottom: '0.5rem', fontSize: '1.3rem', fontWeight: 700 }}>
                    Двухфакторная аутентификация
                </h2>
                <p style={{ color: 'var(--text-secondary)', marginBottom: '2rem', lineHeight: 1.6, fontSize: '0.875rem' }}>
                    Введите 6-значный код из приложения<br />
                    <strong style={{ color: 'var(--text-primary)' }}>Google Authenticator</strong>
                </p>

                {/* 6 separate digit inputs */}
                <div
                    style={{ display: 'flex', gap: '8px', justifyContent: 'center', marginBottom: '2rem' }}
                    onPaste={handlePaste}
                >
                    {digits.map((digit, index) => (
                        <React.Fragment key={index}>
                            <input
                                ref={(el) => { inputRefs.current[index] = el; }}
                                type="text"
                                inputMode="numeric"
                                value={digit}
                                onChange={(e) => handleDigitChange(index, e.target.value)}
                                onKeyDown={(e) => handleKeyDown(index, e)}
                                maxLength={1}
                                style={{
                                    width: '46px',
                                    height: '58px',
                                    textAlign: 'center',
                                    fontSize: '1.5rem',
                                    fontWeight: 700,
                                    fontFamily: 'monospace',
                                    borderRadius: '12px',
                                    border: `2px solid ${digit ? 'var(--accent-color)' : 'var(--border-color)'}`,
                                    background: digit ? 'rgba(99, 102, 241, 0.06)' : 'white',
                                    color: 'var(--text-primary)',
                                    outline: 'none',
                                    transition: 'border-color 0.2s, background 0.2s',
                                    caretColor: 'transparent',
                                    cursor: 'text',
                                    padding: 0,
                                    boxSizing: 'border-box'
                                }}
                            />
                            {/* Visual separator between groups */}
                            {index === 2 && (
                                <div style={{
                                    display: 'flex',
                                    alignItems: 'center',
                                    color: 'var(--text-secondary)',
                                    fontSize: '1.25rem',
                                    fontWeight: 300,
                                    flexShrink: 0,
                                    userSelect: 'none'
                                }}>
                                    –
                                </div>
                            )}
                        </React.Fragment>
                    ))}
                </div>

                {/* Buttons */}
                <div style={{ display: 'flex', gap: '10px' }}>
                    <button
                        onClick={handleClose}
                        className="btn btn-secondary"
                        style={{ flex: 1 }}
                        disabled={loading}
                    >
                        Отмена
                    </button>
                    <button
                        onClick={handleSubmit}
                        className="btn btn-primary"
                        style={{ flex: 2 }}
                        disabled={loading || getCode().length !== 6}
                    >
                        {loading ? <ClipLoader color="#fff" size={18} /> : 'Войти'}
                    </button>
                </div>

                <p style={{ marginTop: '1.25rem', fontSize: '0.78rem', color: 'var(--text-secondary)' }}>
                    🔄 Код обновляется каждые 30 секунд
                </p>
            </div>
        </div>
    );
};

export default TotpModal;

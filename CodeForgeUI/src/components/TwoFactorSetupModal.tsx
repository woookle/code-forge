import React, { useState, useEffect } from 'react';
import { useAppDispatch } from '../app/hooks';
import { setup2FA, enable2FA } from '../features/auth/authSlice';
import { Enable2FAResponse } from '../types/auth';
import { toast } from 'react-toastify';
import { ClipLoader } from 'react-spinners';

interface TwoFactorSetupModalProps {
    onClose: () => void;
    onEnabled: () => void;
}

const TwoFactorSetupModal: React.FC<TwoFactorSetupModalProps> = ({ onClose, onEnabled }) => {
    const dispatch = useAppDispatch();
    const [step, setStep] = useState<'loading' | 'qr' | 'verify'>('loading');
    const [setupData, setSetupData] = useState<Enable2FAResponse | null>(null);
    const [code, setCode] = useState('');
    const [verifying, setVerifying] = useState(false);
    const [showManualKey, setShowManualKey] = useState(false);

    useEffect(() => {
        const fetchSetup = async () => {
            try {
                const result = await dispatch(setup2FA()).unwrap();
                setSetupData(result);
                setStep('qr');
            } catch (err: any) {
                toast.error('Ошибка при настройке 2FA');
                onClose();
            }
        };
        fetchSetup();
    }, []);

    const handleVerify = async (e: React.FormEvent) => {
        e.preventDefault();
        const trimmedCode = code.trim();
        if (trimmedCode.length !== 6) {
            toast.error('Введите 6-значный код');
            return;
        }
        setVerifying(true);
        try {
            await dispatch(enable2FA(trimmedCode)).unwrap();
            toast.success('Двухфакторная аутентификация включена!');
            onEnabled();
            onClose();
        } catch (err: any) {
            toast.error(err || 'Неверный код. Попробуйте ещё раз.');
        } finally {
            setVerifying(false);
        }
    };

    const handleCodeChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        const val = e.target.value.replace(/\D/g, '').slice(0, 6);
        setCode(val);
    };

    return (
        <div className="modal-overlay" onClick={onClose} style={{ zIndex: 1100 }}>
            <div
                className="modal"
                onClick={(e) => e.stopPropagation()}
                style={{ maxWidth: '480px', width: '90%', padding: '2rem' }}
            >
                {/* Header */}
                <div style={{ display: 'flex', alignItems: 'center', gap: '14px', marginBottom: '1.5rem' }}>
                    <div style={{
                        width: '46px',
                        height: '46px',
                        borderRadius: '12px',
                        background: 'linear-gradient(135deg, #10b981, #059669)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        flexShrink: 0,
                        boxShadow: '0 4px 14px rgba(16,185,129,0.3)'
                    }}>
                        <svg width="22" height="22" fill="none" viewBox="0 0 24 24" stroke="white" strokeWidth="2">
                            <path strokeLinecap="round" strokeLinejoin="round" d="M9 12.75L11.25 15 15 9.75m-3-7.036A11.959 11.959 0 013.598 6 11.99 11.99 0 003 9.749c0 5.592 3.824 10.29 9 11.623 5.176-1.332 9-6.03 9-11.622 0-1.31-.21-2.571-.598-3.751h-.152c-3.196 0-6.1-1.248-8.25-3.285z" />
                        </svg>
                    </div>
                    <div style={{ flex: 1 }}>
                        <h2 style={{ margin: 0, fontSize: '1.2rem', fontWeight: 700 }}>
                            Двухфакторная аутентификация
                        </h2>
                        <p style={{ margin: 0, fontSize: '0.82rem', color: 'var(--text-secondary)', marginTop: '2px' }}>
                            {step === 'loading' ? 'Подготовка...' : step === 'qr' ? 'Шаг 1 из 2 — Сканирование' : 'Шаг 2 из 2 — Подтверждение'}
                        </p>
                    </div>
                    <button
                        onClick={onClose}
                        style={{
                            background: 'none',
                            border: 'none',
                            cursor: 'pointer',
                            color: 'var(--text-secondary)',
                            padding: '6px',
                            display: 'flex',
                            alignItems: 'center',
                            borderRadius: '8px',
                            transition: 'background 0.2s'
                        }}
                    >
                        <svg width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2.5">
                            <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
                        </svg>
                    </button>
                </div>

                {/* Step indicator */}
                {step !== 'loading' && (
                    <div style={{ display: 'flex', gap: '6px', marginBottom: '1.75rem' }}>
                        {[0, 1].map((i) => (
                            <div key={i} style={{
                                flex: 1,
                                height: '3px',
                                borderRadius: '2px',
                                background: (step === 'qr' && i === 0) || (step === 'verify')
                                    ? 'linear-gradient(90deg, #10b981, #059669)'
                                    : 'var(--border-color)',
                                transition: 'background 0.3s ease'
                            }} />
                        ))}
                    </div>
                )}

                {/* Loading */}
                {step === 'loading' && (
                    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', padding: '3rem 0' }}>
                        <ClipLoader color="#6366f1" size={40} />
                        <p style={{ marginTop: '1.25rem', color: 'var(--text-secondary)', fontSize: '0.9rem' }}>
                            Генерация QR-кода...
                        </p>
                    </div>
                )}

                {/* QR Step */}
                {step === 'qr' && setupData && (
                    <div>
                        <p style={{ color: 'var(--text-secondary)', marginBottom: '1.5rem', lineHeight: 1.65, fontSize: '0.9rem' }}>
                            Откройте приложение <strong style={{ color: 'var(--text-primary)' }}>Google Authenticator</strong> на
                            телефоне и отсканируйте QR-код:
                        </p>

                        {/* QR Code */}
                        <div style={{ display: 'flex', justifyContent: 'center', marginBottom: '1.5rem' }}>
                            <div style={{
                                padding: '14px',
                                background: 'white',
                                borderRadius: '16px',
                                boxShadow: '0 4px 24px rgba(0,0,0,0.12)',
                                border: '1px solid #e5e7eb',
                                display: 'inline-flex'
                            }}>
                                <img
                                    src={setupData.qrCodeBase64}
                                    alt="QR Code для Google Authenticator"
                                    style={{ width: '200px', height: '200px', display: 'block' }}
                                />
                            </div>
                        </div>

                        {/* Manual entry key */}
                        <div style={{
                            background: 'var(--bg-secondary)',
                            borderRadius: '10px',
                            padding: '12px 14px',
                            marginBottom: '1.5rem',
                            border: '1px solid var(--border-color)'
                        }}>
                            <button
                                onClick={() => setShowManualKey(!showManualKey)}
                                style={{
                                    background: 'none',
                                    border: 'none',
                                    cursor: 'pointer',
                                    color: 'var(--accent-color)',
                                    fontSize: '0.85rem',
                                    padding: 0,
                                    display: 'flex',
                                    alignItems: 'center',
                                    gap: '6px',
                                    fontWeight: 500
                                }}
                            >
                                <svg width="15" height="15" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2">
                                    <path strokeLinecap="round" strokeLinejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                                    <path strokeLinecap="round" strokeLinejoin="round" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                                </svg>
                                {showManualKey ? 'Скрыть ключ' : 'Ввести ключ вручную'}
                            </button>
                            {showManualKey && (
                                <div style={{ marginTop: '10px' }}>
                                    <p style={{ fontSize: '0.78rem', color: 'var(--text-secondary)', margin: '0 0 6px' }}>
                                        Если не можете отсканировать QR — введите этот ключ вручную (нажмите для копирования):
                                    </p>
                                    <div
                                        style={{
                                            fontFamily: 'monospace',
                                            fontSize: '0.85rem',
                                            letterSpacing: '1.5px',
                                            background: 'white',
                                            padding: '8px 12px',
                                            borderRadius: '8px',
                                            wordBreak: 'break-all',
                                            border: '1px solid var(--border-color)',
                                            cursor: 'pointer',
                                            userSelect: 'all',
                                            color: '#374151'
                                        }}
                                        onClick={() => {
                                            navigator.clipboard.writeText(setupData.manualEntryKey);
                                            toast.info('Ключ скопирован в буфер обмена');
                                        }}
                                    >
                                        {setupData.manualEntryKey}
                                    </div>
                                </div>
                            )}
                        </div>

                        <button
                            onClick={() => setStep('verify')}
                            className="btn btn-primary"
                            style={{ width: '100%' }}
                        >
                            Я отсканировал QR-код →
                        </button>
                    </div>
                )}

                {/* Verify Step */}
                {step === 'verify' && (
                    <div>
                        <p style={{ color: 'var(--text-secondary)', marginBottom: '1.5rem', lineHeight: 1.65, fontSize: '0.9rem' }}>
                            Введите <strong style={{ color: 'var(--text-primary)' }}>6-значный код</strong> из Google
                            Authenticator для подтверждения:
                        </p>

                        <form onSubmit={handleVerify}>
                            <div className="form-group">
                                <label>Код подтверждения</label>
                                <input
                                    type="text"
                                    inputMode="numeric"
                                    placeholder="000000"
                                    value={code}
                                    onChange={handleCodeChange}
                                    maxLength={6}
                                    autoFocus
                                    style={{
                                        textAlign: 'center',
                                        fontSize: '1.8rem',
                                        letterSpacing: '0.55rem',
                                        fontWeight: 700,
                                        fontFamily: 'monospace',
                                        padding: '0.75rem'
                                    }}
                                />
                            </div>

                            <div style={{ display: 'flex', gap: '10px', marginTop: '1.5rem' }}>
                                <button
                                    type="button"
                                    onClick={() => { setStep('qr'); setCode(''); }}
                                    className="btn btn-secondary"
                                    style={{ flex: 1 }}
                                    disabled={verifying}
                                >
                                    ← Назад
                                </button>
                                <button
                                    type="submit"
                                    className="btn btn-primary"
                                    style={{ flex: 2 }}
                                    disabled={verifying || code.trim().length !== 6}
                                >
                                    {verifying
                                        ? <ClipLoader color="#fff" size={18} />
                                        : 'Подтвердить и включить'
                                    }
                                </button>
                            </div>
                        </form>
                    </div>
                )}
            </div>
        </div>
    );
};

export default TwoFactorSetupModal;

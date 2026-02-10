import { useState } from 'react';
import { ClipLoader } from 'react-spinners';

interface VerificationModalProps {
    email: string;
    onClose: () => void;
    onConfirm: (code: string) => Promise<void>;
}

export default function VerificationModal({ email, onClose, onConfirm }: VerificationModalProps) {
    const [code, setCode] = useState('');
    const [loading, setLoading] = useState(false);

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setLoading(true);
        try {
            await onConfirm(code);
        } catch (error: any) {
            // Error handled by parent
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="modal-overlay animate-fade-in" onClick={onClose}>
            <div className="modal animate-scale-in" onClick={(e) => e.stopPropagation()}>
                <h2>Подтверждение Email</h2>
                <p>Мы отправили шестизначный код на <strong>{email}</strong></p>
                <form onSubmit={handleSubmit}>
                    <div className="form-group" style={{ textAlign: 'center', margin: '2rem 0' }}>
                        <input
                            type="text"
                            value={code}
                            onChange={(e) => {
                                const val = e.target.value.replace(/\D/g, '');
                                if (val.length <= 6) setCode(val);
                            }}
                            placeholder="000000"
                            maxLength={6}
                            required
                            style={{
                                textAlign: 'center',
                                letterSpacing: '0.5em',
                                fontSize: '2rem',
                                padding: '1rem',
                                width: '100%',
                                maxWidth: '300px'
                            }}
                        />
                    </div>
                    <div style={{ display: 'flex', gap: '10px' }}>
                        <button type="submit" className="btn btn-primary" disabled={loading || code.length < 6} style={{ flex: 1 }}>
                            {loading ? <ClipLoader size={20} color="#fff" /> : 'Подтвердить'}
                        </button>
                        <button type="button" className="btn btn-secondary" onClick={onClose} disabled={loading}>
                            Отмена
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
}

import React, { useEffect, useState } from 'react';

interface ConfirmModalProps {
    isOpen: boolean;
    title?: string;
    message: string;
    confirmText?: string;
    cancelText?: string;
    type?: 'danger' | 'info' | 'warning';
    onConfirm: () => void;
    onCancel: () => void;
}

const ConfirmModal: React.FC<ConfirmModalProps> = ({
    isOpen,
    title = 'Подтверждение',
    message,
    confirmText = 'Да',
    cancelText = 'Отмена',
    type = 'danger',
    onConfirm,
    onCancel
}) => {
    const [visible, setVisible] = useState(false);

    useEffect(() => {
        if (isOpen) {
            setVisible(true);
        } else {
            const timer = setTimeout(() => setVisible(false), 300); // Wait for animation
            return () => clearTimeout(timer);
        }
    }, [isOpen]);

    if (!visible) return null;

    const getBtnClass = () => {
        switch (type) {
            case 'danger': return 'btn-danger';
            case 'warning': return 'btn-warning';
            case 'info': return 'btn-primary';
            default: return 'btn-primary';
        }
    };

    return (
        <div
            className={`modal-overlay ${isOpen ? 'animate-fade-in' : 'animate-fade-out'}`}
            onClick={onCancel}
            style={{
                opacity: isOpen ? 1 : 0,
                transition: 'opacity 0.2s ease-in-out',
                pointerEvents: isOpen ? 'auto' : 'none'
            }}
        >
            <div
                className="modal"
                onClick={(e) => e.stopPropagation()}
                style={{
                    transform: isOpen ? 'scale(1)' : 'scale(0.95)',
                    opacity: isOpen ? 1 : 0,
                    transition: 'all 0.3s cubic-bezier(0.16, 1, 0.3, 1)',
                    maxWidth: '400px'
                }}
            >
                <h2 style={{ marginBottom: '1rem' }}>{title}</h2>
                <p style={{ color: '#4b5563', marginBottom: '2rem', lineHeight: '1.5' }}>
                    {message}
                </p>
                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                    <button className="btn btn-secondary" onClick={onCancel}>
                        {cancelText}
                    </button>
                    <button className={`btn ${getBtnClass()}`} onClick={onConfirm}>
                        {confirmText}
                    </button>
                </div>
            </div>
        </div>
    );
};

export default ConfirmModal;

import { useState, useEffect } from 'react';
import { GenerationRecord, formatTimestamp } from '../../utils/generationHistory';
import { useConfirm } from '../../context/ConfirmContext';
import api from '../../utils/api';
import { ClipLoader } from 'react-spinners';
import { toast } from 'react-toastify';

interface Props {
    onClose: () => void;
    filterProjectId?: string;
}

const STACK_LABELS: Record<string, string> = {
    CSharp_PostgreSQL: 'C# + PostgreSQL',
    NodeJS_MongoDB: 'Node.js + MongoDB',
};
const STACK_COLORS: Record<string, string> = {
    CSharp_PostgreSQL: '#6366f1',
    NodeJS_MongoDB: '#10b981',
};
const ARCH_COLORS: Record<string, string> = {
    Microservices: '#8b5cf6',
    Monolith: '#f59e0b',
};

function GenerationHistoryModal({ onClose, filterProjectId }: Props) {
    const { confirm } = useConfirm();
    const [allRecords, setAllRecords] = useState<GenerationRecord[]>([]);
    const [loading, setLoading] = useState(true);
    const [showAll, setShowAll] = useState(!filterProjectId);

    useEffect(() => {
        const load = async () => {
            setLoading(true);
            try {
                const res = await api.get<GenerationRecord[]>('/generations', {
                    params: { limit: 100 },
                });
                setAllRecords(res.data);
            } catch {
                setAllRecords([]);
            } finally {
                setLoading(false);
            }
        };
        load();
    }, []);

    const filtered = showAll || !filterProjectId
        ? allRecords
        : allRecords.filter(r => r.projectId === filterProjectId);

    const handleClear = async () => {
        if (await confirm({
            title: 'Очистить историю',
            message: 'Это действие невозможно отменить. Все записи будут удалены из базы данных.',
            confirmText: 'Очистить',
            type: 'danger',
        })) {
            try {
                await api.delete('/generations');
                setAllRecords([]);
                toast.success('История генераций очищена');
            } catch {
                toast.error('Не удалось очистить историю');
            }
        }
    };

    return (
        <div className="modal-overlay" onClick={onClose}>
            <div className="modal" style={{ maxWidth: 660, maxHeight: '80vh', display: 'flex', flexDirection: 'column' }} onClick={e => e.stopPropagation()}>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '1.25rem', flexShrink: 0 }}>
                    <div>
                        <h2 style={{ margin: 0 }}>📜 История генераций</h2>
                        <p style={{ margin: '0.2rem 0 0', fontSize: '0.82rem', color: 'var(--text-secondary)' }}>
                            {loading ? 'Загрузка...' : `${filtered.length} ${filtered.length === 1 ? 'запись' : filtered.length < 5 ? 'записи' : 'записей'}`}
                        </p>
                    </div>
                    <button className="btn btn-secondary btn-small" onClick={onClose}>✕</button>
                </div>

                {filterProjectId && (
                    <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '1rem', flexShrink: 0 }}>
                        <button className={`btn btn-small ${!showAll ? 'btn-primary' : 'btn-secondary'}`} onClick={() => setShowAll(false)}>Этот проект</button>
                        <button className={`btn btn-small ${showAll ? 'btn-primary' : 'btn-secondary'}`} onClick={() => setShowAll(true)}>Все проекты</button>
                    </div>
                )}

                <div style={{ flex: 1, overflowY: 'auto', marginRight: '-0.5rem', paddingRight: '0.5rem' }}>
                    {loading ? (
                        <div style={{ display: 'flex', justifyContent: 'center', padding: '2rem' }}>
                            <ClipLoader size={32} color="var(--accent-color)" />
                        </div>
                    ) : filtered.length === 0 ? (
                        <div className="empty-state" style={{ padding: '2.5rem 1rem' }}>
                            <div style={{ fontSize: '2.5rem', marginBottom: '0.75rem' }}>📭</div>
                            <h3 style={{ margin: 0 }}>История пуста</h3>
                            <p style={{ margin: '0.4rem 0 0' }}>Сгенерируйте проект, чтобы здесь появилась запись</p>
                        </div>
                    ) : (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.6rem' }}>
                            {filtered.map((r, i) => (
                                <div key={r.id} className="history-record">
                                    <div className="history-record__index">{i + 1}</div>
                                    <div className="history-record__body">
                                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', flexWrap: 'wrap' }}>
                                            <span style={{ fontWeight: 600, fontSize: '0.9rem' }}>{r.projectName}</span>
                                            <span style={{
                                                padding: '1px 8px', borderRadius: '999px', fontSize: '0.7rem', fontWeight: 600,
                                                background: (STACK_COLORS[r.targetStack] ?? '#6366f1') + '18',
                                                color: STACK_COLORS[r.targetStack] ?? '#6366f1',
                                                border: `1px solid ${(STACK_COLORS[r.targetStack] ?? '#6366f1')}33`,
                                            }}>
                                                {STACK_LABELS[r.targetStack] ?? r.targetStack}
                                            </span>
                                            <span style={{
                                                padding: '1px 8px', borderRadius: '999px', fontSize: '0.7rem', fontWeight: 600,
                                                background: (ARCH_COLORS[r.architectureType] ?? '#6366f1') + '18',
                                                color: ARCH_COLORS[r.architectureType] ?? '#6366f1',
                                                border: `1px solid ${(ARCH_COLORS[r.architectureType] ?? '#6366f1')}33`,
                                            }}>
                                                {r.architectureType === 'Microservices' ? '🔀 Microservices' : '🏗 Monolith'}
                                            </span>
                                        </div>
                                        <div style={{ display: 'flex', gap: '1.25rem', marginTop: '0.3rem', fontSize: '0.78rem', color: 'var(--text-secondary)' }}>
                                            <span>🧩 {r.entityCount} сущ.</span>
                                            <span>🕒 {formatTimestamp(r.createdAt)}</span>
                                        </div>
                                    </div>
                                </div>
                            ))}
                        </div>
                    )}
                </div>

                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '1.25rem', paddingTop: '1rem', borderTop: '1px solid var(--border-color)', flexShrink: 0 }}>
                    <button className="btn btn-danger btn-small" onClick={handleClear} disabled={allRecords.length === 0 || loading}>
                        🗑 Очистить историю
                    </button>
                    <button className="btn btn-secondary" onClick={onClose}>Закрыть</button>
                </div>
            </div>
        </div>
    );
}

export default GenerationHistoryModal;

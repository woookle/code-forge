import { useState } from 'react';
import { PROJECT_TEMPLATES, ProjectTemplate, countTemplateRelationships } from '../../data/projectTemplates';
import { TargetStack, ArchitectureType } from '../../types';
import { ClipLoader } from 'react-spinners';

interface Props {
    onClose: () => void;
    onCreate: (
        name: string,
        stack: TargetStack,
        arch: ArchitectureType,
        template: ProjectTemplate
    ) => Promise<void>;
}

function ProjectTemplatesModal({ onClose, onCreate }: Props) {
    const [selected, setSelected] = useState<ProjectTemplate | null>(null);
    const [name, setName] = useState('');
    const [stack, setStack] = useState<TargetStack>('NodeJS_MongoDB');
    const [arch, setArch] = useState<ArchitectureType>('Monolith');
    const [loading, setLoading] = useState(false);
    const [step, setStep] = useState<'pick' | 'configure'>('pick');

    const handlePickTemplate = (t: ProjectTemplate) => {
        setSelected(t);
        setName(t.projectName);
        setStack(t.defaultStack);
        setArch(t.defaultArch);
        setStep('configure');
    };

    const sanitizeProjectName = (value: string) =>
        value.replace(/[^A-Za-z0-9_-]/g, '');

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!selected || !name.trim()) return;
        const safeName = sanitizeProjectName(name.trim());
        if (!safeName) return;
        setLoading(true);
        try {
            await onCreate(safeName, stack, arch, selected);
            onClose();
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="modal-overlay" onClick={onClose}>
            <div className="modal templates-modal" style={{ maxWidth: step === 'pick' ? 680 : 480 }} onClick={e => e.stopPropagation()}>
                {step === 'pick' ? (
                    <>
                        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '1.5rem' }}>
                            <div>
                                <h2 style={{ margin: 0 }}>✨ Шаблоны проектов</h2>
                                <p style={{ margin: '0.25rem 0 0', fontSize: '0.85rem', color: 'var(--text-secondary)' }}>
                                    Выберите готовую схему для быстрого старта
                                </p>
                            </div>
                            <button className="btn btn-secondary btn-small" onClick={onClose}>✕</button>
                        </div>

                        <div className="templates-grid">
                            {PROJECT_TEMPLATES.map(t => {
                                const relCount = countTemplateRelationships(t);
                                return (
                                <button
                                    key={t.id}
                                    className="template-card"
                                    onClick={() => handlePickTemplate(t)}
                                    style={{ '--tpl-color': t.color } as React.CSSProperties}
                                >
                                    <span className="template-icon">{t.icon}</span>
                                    <div className="template-info">
                                        <div className="template-name">{t.name}</div>
                                        <div className="template-desc">{t.description}</div>
                                        <div className="template-meta">
                                            <span className="template-count">
                                                {t.entities.length} {t.entities.length < 5 ? 'сущности' : 'сущностей'}
                                            </span>
                                            {relCount > 0 && (
                                                <span className="template-count">🔗 {relCount} связей</span>
                                            )}
                                            {t.tags.map(tag => (
                                                <span key={tag} className="template-tag">{tag}</span>
                                            ))}
                                        </div>
                                    </div>
                                </button>
                            );})}
                        </div>
                    </>
                ) : (
                    <>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', marginBottom: '1.5rem' }}>
                            <button className="btn btn-secondary btn-small" onClick={() => setStep('pick')}>← Назад</button>
                            <span style={{ fontSize: '1.5rem' }}>{selected?.icon}</span>
                            <h2 style={{ margin: 0 }}>{selected?.name}</h2>
                        </div>

                        <form onSubmit={handleSubmit}>
                            <div className="form-group">
                                <label>Название проекта</label>
                                <input
                                    type="text"
                                    value={name}
                                    onChange={e => setName(sanitizeProjectName(e.target.value))}
                                    placeholder="OnlineShop"
                                    required
                                    autoFocus
                                    pattern="[A-Za-z0-9_-]+"
                                />
                                <p style={{ margin: '0.35rem 0 0', fontSize: '0.78rem', color: 'var(--text-secondary)' }}>
                                    Только латиница — используется в Docker, папках и namespace сгенерированного кода
                                </p>
                            </div>

                            <div className="form-group">
                                <label>Стек технологий</label>
                                <select value={stack} onChange={e => setStack(e.target.value as TargetStack)}>
                                    <option value="NodeJS_MongoDB">Node.js + MongoDB</option>
                                    <option value="CSharp_PostgreSQL">C# + PostgreSQL</option>
                                </select>
                            </div>

                            <div className="form-group">
                                <label>Архитектура</label>
                                <select value={arch} onChange={e => setArch(e.target.value as ArchitectureType)}>
                                    <option value="Monolith">Монолит</option>
                                    <option value="Microservices">Микросервисы</option>
                                </select>
                            </div>

                            {/* Предпросмотр сущностей */}
                            <div style={{ marginBottom: '1.5rem' }}>
                                <div style={{ fontSize: '0.8rem', fontWeight: 600, color: 'var(--text-secondary)', textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: '0.6rem' }}>
                                    Будет создано {selected?.entities.length} сущностей
                                    {selected && countTemplateRelationships(selected) > 0 && (
                                        <> · {countTemplateRelationships(selected)} связей</>
                                    )}
                                </div>
                                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.4rem' }}>
                                    {selected?.entities.map(e => (
                                        <span key={e.name} style={{
                                            padding: '0.2rem 0.65rem',
                                            borderRadius: '999px',
                                            background: (selected as any)?.color + '18',
                                            color: selected?.color,
                                            border: `1px solid ${selected?.color}33`,
                                            fontSize: '0.78rem',
                                            fontWeight: 600,
                                        }}>
                                            {e.name}
                                        </span>
                                    ))}
                                </div>
                            </div>

                            <div className="modal-actions">
                                <button type="button" className="btn btn-secondary" onClick={onClose}>
                                    Отмена
                                </button>
                                <button type="submit" className="btn btn-primary" disabled={loading || !sanitizeProjectName(name).trim()}>
                                    {loading ? <ClipLoader size={16} color="#fff" /> : '✨ Создать из шаблона'}
                                </button>
                            </div>
                        </form>
                    </>
                )}
            </div>
        </div>
    );
}

export default ProjectTemplatesModal;

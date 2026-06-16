import { useMemo, useState } from 'react';
import { Project, Entity } from '../types';

interface MicroservicesPreviewProps {
    project: Project;
    authEnabled: boolean;
}

// ── Color palette for services ────────────────────────────────────────────────
const SERVICE_COLORS = [
    '#6366f1', '#10b981', '#f59e0b', '#ef4444',
    '#3b82f6', '#8b5cf6', '#ec4899', '#14b8a6',
    '#f97316', '#84cc16',
];
const AUTH_COLOR = '#6366f1';
const RABBITMQ_COLOR = '#f97316';
const DB_COLOR_CS = '#336791';   // Синий PostgreSQL
const DB_COLOR_NODE = '#4db33d'; // Зелёный MongoDB

function sanitize(name: string) {
    return name.replace(/[^a-zA-Z0-9]/g, '').replace(/^[0-9]+/, '') || 'Service';
}

interface ServiceInfo {
    name: string;
    safeName: string;
    color: string;
    entities: Entity[];
    port: number;
    dbName: string;
    isAuth?: boolean;
}

// ── Sub-components ────────────────────────────────────────────────────────────

function DbBox({ name, stack }: { name: string; stack: string }) {
    const isNode = stack === 'NodeJS_MongoDB';
    const color = isNode ? DB_COLOR_NODE : DB_COLOR_CS;
    const icon = isNode ? '🍃' : '🐘';
    const label = isNode ? 'MongoDB' : 'PostgreSQL';
    return (
        <div style={{
            marginTop: '8px', padding: '6px 10px', borderRadius: '8px',
            background: color + '15', border: `1px dashed ${color}50`,
            fontSize: '0.72rem', color, fontWeight: 600,
            display: 'flex', alignItems: 'center', gap: '5px'
        }}>
            <span>{icon}</span>
            <span style={{ fontFamily: 'monospace' }}>{name}</span>
            <span style={{ marginLeft: 'auto', opacity: 0.7, fontWeight: 400 }}>{label}</span>
        </div>
    );
}

function ServiceCard({ svc, stack, expanded, onToggle }: {
    svc: ServiceInfo;
    stack: string;
    expanded: boolean;
    onToggle: () => void;
}) {
    const isNode = stack === 'NodeJS_MongoDB';
    const stackIcon = isNode ? '🟢' : '⚙️';
    const stackLabel = isNode ? 'Node.js' : 'C#';

    const endpoints = svc.isAuth
        ? ['POST /api/auth/register', 'POST /api/auth/login', 'GET /api/auth/me']
        : svc.entities.flatMap(e => [
            `GET /api/${e.name.toLowerCase()}s`,
            `POST /api/${e.name.toLowerCase()}s`,
            `GET /api/${e.name.toLowerCase()}s/:id`,
            `PUT /api/${e.name.toLowerCase()}s/:id`,
            `DELETE /api/${e.name.toLowerCase()}s/:id`,
        ]).slice(0, 6);

    return (
        <div style={{
            borderRadius: '14px',
            border: `2px solid ${svc.color}40`,
            background: `linear-gradient(145deg, ${svc.color}08, ${svc.color}03)`,
            overflow: 'hidden',
            transition: 'box-shadow 0.2s, transform 0.15s',
            boxShadow: `0 2px 12px ${svc.color}15`,
            flex: '1',
            minWidth: '220px',
            maxWidth: '320px',
        }}
            onMouseEnter={e => { (e.currentTarget as HTMLElement).style.boxShadow = `0 6px 24px ${svc.color}30`; (e.currentTarget as HTMLElement).style.transform = 'translateY(-2px)'; }}
            onMouseLeave={e => { (e.currentTarget as HTMLElement).style.boxShadow = `0 2px 12px ${svc.color}15`; (e.currentTarget as HTMLElement).style.transform = 'none'; }}
        >
            {/* Заголовок карточки */}
            <div style={{ padding: '14px 16px 10px', borderBottom: `1px solid ${svc.color}20` }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '8px' }}>
                    {svc.isAuth ? (
                        <div style={{ width: '32px', height: '32px', borderRadius: '8px', background: svc.color + '20', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '1rem' }}>🔐</div>
                    ) : (
                        <div style={{ width: '32px', height: '32px', borderRadius: '8px', background: svc.color + '20', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '1rem' }}>📦</div>
                    )}
                    <div style={{ flex: 1 }}>
                        <div style={{ fontWeight: 800, fontSize: '0.9rem', color: svc.color }}>{svc.safeName}-service</div>
                        <div style={{ fontSize: '0.7rem', color: 'var(--text-secondary)', fontFamily: 'monospace' }}>:{svc.port}</div>
                    </div>
                    <span style={{
                        padding: '2px 7px', borderRadius: '6px', fontSize: '0.68rem', fontWeight: 700,
                        background: svc.color + '18', color: svc.color, border: `1px solid ${svc.color}30`
                    }}>{stackIcon} {stackLabel}</span>
                </div>

                {/* Список сущностей */}
                {!svc.isAuth && svc.entities.length > 0 && (
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: '4px', marginBottom: '6px' }}>
                        {svc.entities.map(e => (
                            <span key={e.id} style={{
                                padding: '2px 8px', borderRadius: '6px', fontSize: '0.7rem', fontWeight: 600,
                                background: 'var(--bg-primary)', border: '1px solid var(--border-color)',
                                color: 'var(--text-primary)'
                            }}>
                                {e.name}
                                {e.fields && e.fields.length > 0 && (
                                    <span style={{ color: 'var(--text-secondary)', marginLeft: '3px' }}>({e.fields.length})</span>
                                )}
                            </span>
                        ))}
                    </div>
                )}
                {svc.isAuth && (
                    <div style={{ fontSize: '0.72rem', color: 'var(--text-secondary)' }}>
                        JWT · register · login · me
                    </div>
                )}
            </div>

            {/* База данных */}
            <div style={{ padding: '8px 16px' }}>
                <DbBox name={svc.dbName} stack={stack} />
            </div>

            {/* Swagger и переключатель эндпоинтов */}
            <div style={{ padding: '0 16px 12px' }}>
                <button
                    onClick={onToggle}
                    style={{
                        width: '100%', padding: '6px 8px', borderRadius: '8px', fontSize: '0.72rem',
                        fontWeight: 600, cursor: 'pointer', border: `1px solid ${svc.color}30`,
                        background: expanded ? svc.color + '15' : 'transparent',
                        color: svc.color, transition: 'all 0.15s',
                        display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '4px'
                    }}
                >
                    {expanded ? '▲' : '▼'} {expanded ? 'Скрыть' : 'REST эндпоинты'}
                </button>

                {expanded && (
                    <div style={{ marginTop: '8px', display: 'flex', flexDirection: 'column', gap: '3px' }}>
                        {endpoints.map((ep, i) => {
                            const [method, path] = ep.split(' ');
                            const mc: Record<string, string> = { GET: '#3b82f6', POST: '#10b981', PUT: '#f59e0b', PATCH: '#f59e0b', DELETE: '#ef4444' };
                            return (
                                <div key={i} style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '0.7rem' }}>
                                    <span style={{
                                        padding: '1px 5px', borderRadius: '4px', fontWeight: 700, fontFamily: 'monospace', fontSize: '0.65rem',
                                        minWidth: '38px', textAlign: 'center',
                                        background: (mc[method] || '#6b7280') + '18',
                                        color: mc[method] || '#6b7280',
                                        border: `1px solid ${(mc[method] || '#6b7280')}30`
                                    }}>{method}</span>
                                    <code style={{ color: 'var(--text-secondary)', fontSize: '0.68rem' }}>{path}</code>
                                </div>
                            );
                        })}
                        <div style={{ marginTop: '4px', fontSize: '0.68rem', color: svc.color, opacity: 0.8 }}>
                            📖 Swagger: :{svc.port}/swagger
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
}

// ── Main Preview Component ────────────────────────────────────────────────────

export default function MicroservicesPreview({ project, authEnabled }: MicroservicesPreviewProps) {
    const [expandedService, setExpandedService] = useState<string | null>(null);
    const [isCollapsed, setIsCollapsed] = useState(false);

    const isCSharp = project.targetStack === 'CSharp_PostgreSQL';
    const portBase = isCSharp ? 5001 : 3001;

    const services = useMemo<ServiceInfo[]>(() => {
        const entities = project.entities || [];
        const groups = new Map<string, Entity[]>();
        for (const e of entities) {
            const key = e.serviceName || e.name;
            if (!groups.has(key)) groups.set(key, []);
            groups.get(key)!.push(e);
        }

        const result: ServiceInfo[] = [];
        let idx = 0;
        groups.forEach((ents, name) => {
            result.push({
                name,
                safeName: sanitize(name),
                color: SERVICE_COLORS[idx % SERVICE_COLORS.length],
                entities: ents,
                port: portBase + idx,
                dbName: name.toLowerCase().replace(/[^a-z0-9]/g, '_') + '_db',
            });
            idx++;
        });

        if (authEnabled) {
            result.unshift({
                name: 'auth',
                safeName: 'Auth',
                color: AUTH_COLOR,
                entities: [],
                port: portBase + idx,
                dbName: 'auth_db',
                isAuth: true,
            });
        }

        return result;
    }, [project.entities, authEnabled, portBase]);

    const hasEntities = (project.entities?.length || 0) > 0 || authEnabled;
    if (!hasEntities) return null;

    const msgProtocol = 'RabbitMQ (AMQP · topic exchange "events")';
    const msgPort = ':5672';

    return (
        <div style={{
            marginBottom: '24px',
            borderRadius: '14px',
            border: '1px solid rgba(99,102,241,0.2)',
            background: 'linear-gradient(160deg, rgba(99,102,241,0.04) 0%, rgba(16,185,129,0.02) 100%)',
            overflow: 'hidden',
        }}>
            {/* Заголовок */}
            <div
                onClick={() => setIsCollapsed(c => !c)}
                style={{
                    padding: '14px 20px', cursor: 'pointer',
                    display: 'flex', alignItems: 'center', gap: '12px',
                    borderBottom: isCollapsed ? 'none' : '1px solid rgba(99,102,241,0.15)',
                    userSelect: 'none',
                }}
            >
                <div style={{ width: '36px', height: '36px', borderRadius: '10px', background: 'rgba(99,102,241,0.12)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '1.1rem', flexShrink: 0 }}>🔀</div>
                <div style={{ flex: 1 }}>
                    <div style={{ fontWeight: 800, fontSize: '0.95rem', color: 'var(--text-primary)' }}>Предпросмотр архитектуры микросервисов</div>
                    <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', marginTop: '1px' }}>
                        {services.length} сервис{services.length === 1 ? '' : services.length < 5 ? 'а' : 'ов'} · {project.entities?.length || 0} сущност{(project.entities?.length || 0) === 1 ? 'ь' : (project.entities?.length || 0) < 5 ? 'и' : 'ей'}{authEnabled ? ' · Auth включена' : ''}
                    </div>
                </div>
                <div style={{ display: 'flex', gap: '6px', alignItems: 'center' }}>
                    <span style={{ padding: '2px 10px', borderRadius: '10px', fontSize: '0.72rem', fontWeight: 600, background: 'rgba(99,102,241,0.12)', color: '#6366f1', border: '1px solid rgba(99,102,241,0.25)' }}>
                        {isCSharp ? '⚙️ C# + PostgreSQL' : '🟢 Node.js + MongoDB'}
                    </span>
                    <span style={{ color: 'var(--text-secondary)', fontSize: '0.8rem' }}>{isCollapsed ? '▼' : '▲'}</span>
                </div>
            </div>

            {!isCollapsed && (
                <div style={{ padding: '20px' }}>

                    {/* Строка с RabbitMQ брокером */}
                    <div style={{ display: 'flex', justifyContent: 'center', marginBottom: '12px' }}>
                        <div style={{
                            display: 'inline-flex', alignItems: 'center', gap: '10px',
                            padding: '10px 20px', borderRadius: '12px',
                            background: `${RABBITMQ_COLOR}12`, border: `2px solid ${RABBITMQ_COLOR}35`,
                            boxShadow: `0 2px 12px ${RABBITMQ_COLOR}20`,
                        }}>
                            <span style={{ fontSize: '1.2rem' }}>🐇</span>
                            <div>
                                <div style={{ fontWeight: 800, fontSize: '0.85rem', color: RABBITMQ_COLOR }}>RabbitMQ Message Broker</div>
                                <div style={{ fontSize: '0.7rem', color: 'var(--text-secondary)', fontFamily: 'monospace' }}>{msgProtocol} · {msgPort}</div>
                            </div>
                            <div style={{ marginLeft: '8px', display: 'flex', flexDirection: 'column', gap: '2px' }}>
                                {['entity.created', 'entity.updated', 'entity.deleted'].map(ev => (
                                    <span key={ev} style={{ padding: '1px 7px', borderRadius: '4px', fontSize: '0.62rem', fontWeight: 600, background: RABBITMQ_COLOR + '18', color: RABBITMQ_COLOR, border: `1px solid ${RABBITMQ_COLOR}30`, fontFamily: 'monospace' }}>{ev}</span>
                                ))}
                                {authEnabled && ['user.registered', 'user.loggedin'].map(ev => (
                                    <span key={ev} style={{ padding: '1px 7px', borderRadius: '4px', fontSize: '0.62rem', fontWeight: 600, background: AUTH_COLOR + '18', color: AUTH_COLOR, border: `1px solid ${AUTH_COLOR}30`, fontFamily: 'monospace' }}>{ev}</span>
                                ))}
                            </div>
                        </div>
                    </div>

                    {/* Соединительные линии вниз */}
                    <div style={{ display: 'flex', justifyContent: 'center', gap: '0', marginBottom: '0', height: '20px', position: 'relative' }}>
                        <div style={{ display: 'flex', gap: '0', width: '100%', justifyContent: 'space-around', paddingLeft: '60px', paddingRight: '60px' }}>
                            {services.map(svc => (
                                <div key={svc.name} style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', flex: 1 }}>
                                    <div style={{ width: '2px', height: '20px', background: `linear-gradient(to bottom, ${RABBITMQ_COLOR}60, ${svc.color}60)`, borderRadius: '1px' }} />
                                </div>
                            ))}
                        </div>
                    </div>

                    {/* Карточки сервисов */}
                    <div style={{ display: 'flex', gap: '14px', flexWrap: 'wrap', justifyContent: 'center' }}>
                        {services.map(svc => (
                            <ServiceCard
                                key={svc.name}
                                svc={svc}
                                stack={project.targetStack}
                                expanded={expandedService === svc.name}
                                onToggle={() => setExpandedService(prev => prev === svc.name ? null : svc.name)}
                            />
                        ))}
                    </div>

                    {/* Легенда межсервисного взаимодействия */}
                    {services.length > 1 && (
                        <div style={{
                            marginTop: '16px', padding: '12px 16px', borderRadius: '10px',
                            background: 'var(--bg-secondary)', border: '1px solid var(--border-color)',
                            fontSize: '0.75rem', color: 'var(--text-secondary)'
                        }}>
                            <div style={{ fontWeight: 700, color: 'var(--text-primary)', marginBottom: '8px' }}>
                                📡 Межсервисное взаимодействие
                            </div>
                            <div style={{ display: 'flex', gap: '16px', flexWrap: 'wrap' }}>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                                    <div style={{ width: '20px', height: '2px', background: `${RABBITMQ_COLOR}80`, borderRadius: '1px' }} />
                                    <span>Publisher — публикует события при CREATE / UPDATE / DELETE</span>
                                </div>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                                    <div style={{ width: '20px', height: '2px', background: '#6366f180', borderRadius: '1px', borderTop: '1px dashed #6366f1' }} />
                                    <span>Subscriber — слушает события других сервисов</span>
                                </div>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                                    <span>🔗</span>
                                    <span>Кросс-сервисные ссылки через <code style={{ background: 'var(--bg-primary)', padding: '1px 4px', borderRadius: '3px' }}>EntityRefId</code></span>
                                </div>
                            </div>
                        </div>
                    )}

                    {/* Итоговая сводка генерации */}
                    <div style={{
                        marginTop: '12px', padding: '12px 16px', borderRadius: '10px',
                        background: 'rgba(16,185,129,0.05)', border: '1px solid rgba(16,185,129,0.2)',
                        fontSize: '0.75rem'
                    }}>
                        <div style={{ fontWeight: 700, color: '#10b981', marginBottom: '6px' }}>📦 Будет сгенерировано в ZIP</div>
                        <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', color: 'var(--text-secondary)' }}>
                            {services.map(svc => (
                                <span key={svc.name} style={{ padding: '2px 9px', borderRadius: '6px', background: svc.color + '12', color: svc.color, border: `1px solid ${svc.color}25`, fontWeight: 600 }}>
                                    services/{svc.safeName.toLowerCase()}-service/
                                </span>
                            ))}
                            <span style={{ padding: '2px 9px', borderRadius: '6px', background: `${RABBITMQ_COLOR}12`, color: RABBITMQ_COLOR, border: `1px solid ${RABBITMQ_COLOR}25`, fontWeight: 600 }}>
                                docker-compose.yml
                            </span>
                            <span style={{ padding: '2px 9px', borderRadius: '6px', background: 'rgba(107,114,128,0.1)', color: 'var(--text-secondary)', border: '1px solid var(--border-color)', fontWeight: 600 }}>
                                README.md
                            </span>
                        </div>
                    </div>

                </div>
            )}
        </div>
    );
}

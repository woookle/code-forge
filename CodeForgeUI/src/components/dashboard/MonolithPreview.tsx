import { useMemo, useState } from 'react';
import { Project } from '../../types';

interface MonolithPreviewProps {
    project: Project;
    authEnabled: boolean;
    rolesEnabled?: boolean;
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function sanitize(name: string) {
    return name.replace(/[^a-zA-Z0-9]/g, '') || 'Project';
}
function lower(s: string) { return s.charAt(0).toLowerCase() + s.slice(1); }
function plural(s: string) { return s.endsWith('s') ? s + 'es' : s + 's'; }

// ── Sub-components ─────────────────────────────────────────────────────────────

interface FileNode {
    name: string;
    type: 'file' | 'dir';
    icon?: string;
    color?: string;
    tag?: string;
    children?: FileNode[];
}

function FileTree({ nodes, depth = 0 }: { nodes: FileNode[]; depth?: number }) {
    const [open, setOpen] = useState<Record<string, boolean>>(() => {
        const init: Record<string, boolean> = {};
        nodes.forEach(n => { if (n.type === 'dir') init[n.name] = true; });
        return init;
    });

    return (
        <div style={{ paddingLeft: depth === 0 ? 0 : '18px' }}>
            {nodes.map(node => (
                <div key={node.name}>
                    <div
                        onClick={() => node.type === 'dir' && setOpen(o => ({ ...o, [node.name]: !o[node.name] }))}
                        style={{
                            display: 'flex', alignItems: 'center', gap: '5px',
                            padding: '2px 4px', borderRadius: '5px', cursor: node.type === 'dir' ? 'pointer' : 'default',
                            transition: 'background 0.12s',
                            userSelect: 'none',
                        }}
                        onMouseEnter={e => { (e.currentTarget as HTMLElement).style.background = 'var(--bg-secondary)'; }}
                        onMouseLeave={e => { (e.currentTarget as HTMLElement).style.background = 'transparent'; }}
                    >
                        {node.type === 'dir' && (
                            <span style={{ fontSize: '0.65rem', color: 'var(--text-secondary)', width: '10px', display: 'inline-block', flexShrink: 0 }}>
                                {open[node.name] ? '▾' : '▸'}
                            </span>
                        )}
                        {node.type === 'file' && <span style={{ width: '10px', display: 'inline-block', flexShrink: 0 }} />}
                        <span style={{ fontSize: '0.82rem' }}>{node.icon || (node.type === 'dir' ? '📂' : '📄')}</span>
                        <span style={{
                            fontSize: '0.75rem', fontFamily: 'monospace',
                            color: node.color || (node.type === 'dir' ? 'var(--text-primary)' : 'var(--text-secondary)'),
                            fontWeight: node.type === 'dir' ? 700 : 400,
                        }}>{node.name}</span>
                        {node.tag && (
                            <span style={{
                                marginLeft: '4px', padding: '0px 5px', borderRadius: '4px', fontSize: '0.6rem', fontWeight: 700,
                                background: node.color ? node.color + '18' : 'rgba(99,102,241,0.1)',
                                color: node.color || '#6366f1',
                                border: `1px solid ${node.color || '#6366f1'}30`,
                            }}>{node.tag}</span>
                        )}
                    </div>
                    {node.type === 'dir' && open[node.name] && node.children && (
                        <FileTree nodes={node.children} depth={depth + 1} />
                    )}
                </div>
            ))}
        </div>
    );
}

interface EndpointRowProps {
    method: string;
    path: string;
    desc: string;
    auth?: boolean;
}

function EndpointRow({ method, path, desc, auth }: EndpointRowProps) {
    const mc: Record<string, string> = { GET: '#3b82f6', POST: '#10b981', PUT: '#f59e0b', PATCH: '#f59e0b', DELETE: '#ef4444' };
    const color = mc[method] || '#6b7280';
    return (
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', padding: '4px 0', borderBottom: '1px solid var(--border-color)' }}>
            <span style={{
                padding: '2px 6px', borderRadius: '4px', fontWeight: 700, fontFamily: 'monospace', fontSize: '0.65rem',
                minWidth: '42px', textAlign: 'center',
                background: color + '18', color, border: `1px solid ${color}30`,
            }}>{method}</span>
            <code style={{ fontSize: '0.72rem', color: 'var(--text-primary)', flex: 1 }}>{path}</code>
            <span style={{ fontSize: '0.68rem', color: 'var(--text-secondary)', textAlign: 'right', maxWidth: '140px' }}>{desc}</span>
            {auth && <span style={{ fontSize: '0.62rem', padding: '1px 5px', borderRadius: '4px', background: 'rgba(99,102,241,0.1)', color: '#6366f1', border: '1px solid rgba(99,102,241,0.2)', whiteSpace: 'nowrap' }}>🔐 JWT</span>}
        </div>
    );
}

// ── Main Component ─────────────────────────────────────────────────────────────

export default function MonolithPreview({ project, authEnabled, rolesEnabled }: MonolithPreviewProps) {
    const [activeTab, setActiveTab] = useState<'tree' | 'endpoints' | 'stack'>('tree');
    const [isCollapsed, setIsCollapsed] = useState(false);

    const isCSharp = project.targetStack === 'CSharp_PostgreSQL';
    const projName = sanitize(project.name || 'MyProject');
    const entities = useMemo(() => project.entities || [], [project.entities]);

    // ── File tree ──────────────────────────────────────────────────────────────

    const fileTree = useMemo<FileNode[]>(() => {
        if (isCSharp) {
            const modelsChildren: FileNode[] = entities.map(e => ({
                name: `${e.name}.cs`, type: 'file', icon: '⚙️', color: '#10b981',
            }));
            if (authEnabled) modelsChildren.push({ name: 'User.cs', type: 'file', icon: '👤', color: '#6366f1', tag: 'auth' });

            const dtosChildren: FileNode[] = entities.map(e => ({
                name: `${e.name}Dto.cs`, type: 'file', icon: '📋', color: '#f59e0b',
            }));
            if (authEnabled) dtosChildren.push({ name: 'AuthDtos.cs', type: 'file', icon: '🔐', color: '#6366f1', tag: 'auth' });

            const controllersChildren: FileNode[] = entities.map(e => ({
                name: `${e.name}Controller.cs`, type: 'file', icon: '🎮', color: '#3b82f6',
            }));
            if (authEnabled) controllersChildren.push({ name: 'AuthController.cs', type: 'file', icon: '🔐', color: '#6366f1', tag: 'auth' });

            const servicesChildren: FileNode[] = authEnabled ? [
                { name: 'IAuthService.cs', type: 'file', icon: '📄', color: '#6366f1', tag: 'auth' },
                { name: 'TokenService.cs', type: 'file', icon: '🔑', color: '#6366f1', tag: 'auth' },
                { name: 'AuthService.cs', type: 'file', icon: '🔐', color: '#6366f1', tag: 'auth' },
            ] : [];

            const tree: FileNode[] = [
                { name: projName + '/', type: 'dir', icon: '📁', children: [
                    { name: 'Models/', type: 'dir', icon: '📂', color: '#10b981', children: modelsChildren },
                    { name: 'DTOs/', type: 'dir', icon: '📂', color: '#f59e0b', children: dtosChildren },
                    { name: 'Controllers/', type: 'dir', icon: '📂', color: '#3b82f6', children: controllersChildren },
                    { name: 'Data/', type: 'dir', icon: '📂', color: '#336791', children: [
                        { name: 'ApplicationDbContext.cs', type: 'file', icon: '🐘', color: '#336791' },
                    ]},
                    { name: 'Middleware/', type: 'dir', icon: '📂', color: '#8b5cf6', children: [
                        { name: 'ErrorHandlerMiddleware.cs', type: 'file', icon: '🛡️', color: '#8b5cf6' },
                    ]},
                    ...(servicesChildren.length > 0 ? [{
                        name: 'Services/', type: 'dir' as const, icon: '📂', color: '#6366f1',
                        children: servicesChildren,
                    }] : []),
                    { name: 'Properties/', type: 'dir', icon: '📂', color: 'var(--text-secondary)', children: [
                        { name: 'launchSettings.json', type: 'file', icon: '⚙️' },
                    ]},
                    { name: 'GlobalUsings.cs', type: 'file', icon: '🌐' },
                    { name: 'Program.cs', type: 'file', icon: '🚀', color: '#10b981' },
                    { name: `${projName}.csproj`, type: 'file', icon: '📦', color: '#6366f1' },
                    { name: 'appsettings.json', type: 'file', icon: '⚙️' },
                    { name: 'appsettings.Development.json', type: 'file', icon: '⚙️' },
                    { name: 'Dockerfile', type: 'file', icon: '🐳', color: '#0ea5e9' },
                    { name: 'docker-compose.yml', type: 'file', icon: '🐳', color: '#0ea5e9' },
                    { name: '.gitignore', type: 'file', icon: '🚫' },
                    { name: 'README.md', type: 'file', icon: '📖', color: '#f59e0b' },
                ]},
            ];
            return tree;
        } else {
            // Node.js стек
            const modelsChildren: FileNode[] = entities.map(e => ({
                name: `${e.name}.js`, type: 'file', icon: '⚙️', color: '#10b981',
            }));
            if (authEnabled) modelsChildren.push({ name: 'User.js', type: 'file', icon: '👤', color: '#6366f1', tag: 'auth' });

            const controllersChildren: FileNode[] = entities.map(e => ({
                name: `${lower(e.name)}Controller.js`, type: 'file', icon: '🎮', color: '#3b82f6',
            }));
            if (authEnabled) controllersChildren.push({ name: 'authController.js', type: 'file', icon: '🔐', color: '#6366f1', tag: 'auth' });

            const routesChildren: FileNode[] = entities.map(e => ({
                name: `${lower(e.name)}Routes.js`, type: 'file', icon: '🔗', color: '#f59e0b',
            }));
            if (authEnabled) routesChildren.push({ name: 'authRoutes.js', type: 'file', icon: '🔐', color: '#6366f1', tag: 'auth' });

            const middlewareChildren: FileNode[] = [
                { name: 'errorHandler.js', type: 'file', icon: '🛡️', color: '#ef4444' },
                { name: 'notFound.js', type: 'file', icon: '❓' },
                { name: 'validate.js', type: 'file', icon: '✅' },
                { name: 'asyncHandler.js', type: 'file', icon: '⚡' },
            ];
            if (authEnabled) {
                middlewareChildren.push({ name: 'authMiddleware.js', type: 'file', icon: '🔐', color: '#6366f1', tag: 'auth' });
                if (rolesEnabled) middlewareChildren.push({ name: 'roleMiddleware.js', type: 'file', icon: '👑', color: '#6366f1', tag: 'roles' });
            }

            const validationChildren: FileNode[] = entities.map(e => ({
                name: `${lower(e.name)}Validation.js`, type: 'file', icon: '✅', color: '#10b981',
            }));

            const utilsChildren: FileNode[] = [
                { name: 'paginate.js', type: 'file', icon: '📄' },
            ];
            if (authEnabled) utilsChildren.push({ name: 'generateTokens.js', type: 'file', icon: '🔑', color: '#6366f1', tag: 'auth' });

            const testsChildren: FileNode[] = entities.map(e => ({
                name: `${lower(e.name)}.test.js`, type: 'file', icon: '🧪', color: '#f59e0b',
            }));

            const tree: FileNode[] = [
                { name: projName + '/', type: 'dir', icon: '📁', children: [
                    { name: 'src/', type: 'dir', icon: '📂', children: [
                        { name: 'models/', type: 'dir', icon: '📂', color: '#10b981', children: modelsChildren },
                        { name: 'controllers/', type: 'dir', icon: '📂', color: '#3b82f6', children: controllersChildren },
                        { name: 'routes/', type: 'dir', icon: '📂', color: '#f59e0b', children: routesChildren },
                        { name: 'middleware/', type: 'dir', icon: '📂', color: '#8b5cf6', children: middlewareChildren },
                        { name: 'validation/', type: 'dir', icon: '📂', color: '#10b981', children: validationChildren },
                        { name: 'config/', type: 'dir', icon: '📂', color: 'var(--text-secondary)', children: [
                            { name: 'database.js', type: 'file', icon: '🍃', color: '#4db33d' },
                            { name: 'swagger.js', type: 'file', icon: '📖', color: '#85ea2d' },
                        ]},
                        { name: 'utils/', type: 'dir', icon: '📂', color: 'var(--text-secondary)', children: utilsChildren },
                        { name: 'app.js', type: 'file', icon: '⚡', color: '#f59e0b' },
                    ]},
                    { name: '__tests__/', type: 'dir', icon: '🧪', color: '#f59e0b', children: testsChildren },
                    { name: 'server.js', type: 'file', icon: '🚀', color: '#10b981' },
                    { name: 'package.json', type: 'file', icon: '📦', color: '#6366f1' },
                    { name: '.env.example', type: 'file', icon: '🔒', color: '#ef4444' },
                    { name: '.gitignore', type: 'file', icon: '🚫' },
                    { name: '.eslintrc.js', type: 'file', icon: '🔍' },
                    { name: 'jest.config.js', type: 'file', icon: '🧪', color: '#f59e0b' },
                    { name: 'Dockerfile', type: 'file', icon: '🐳', color: '#0ea5e9' },
                    { name: 'docker-compose.yml', type: 'file', icon: '🐳', color: '#0ea5e9' },
                    { name: 'README.md', type: 'file', icon: '📖', color: '#f59e0b' },
                ]},
            ];
            return tree;
        }
    }, [isCSharp, projName, entities, authEnabled, rolesEnabled]);

    // ── Endpoints ──────────────────────────────────────────────────────────────

    const entityEndpoints = useMemo(() => {
        const basePath = isCSharp ? '/api' : '/api';
        return entities.flatMap(e => {
            const r = isCSharp ? `/${plural(e.name).toLowerCase()}` : `/${plural(e.name).toLowerCase()}`;
            return [
                { method: 'GET',    path: `${basePath}${r}`,         desc: `Список всех ${e.name}`, auth: authEnabled },
                { method: 'GET',    path: `${basePath}${r}/{id}`,     desc: `Получить ${e.name} по ID`, auth: authEnabled },
                { method: 'POST',   path: `${basePath}${r}`,         desc: `Создать ${e.name}`, auth: authEnabled },
                { method: 'PUT',    path: `${basePath}${r}/{id}`,     desc: `Обновить ${e.name}`, auth: authEnabled },
                { method: 'DELETE', path: `${basePath}${r}/{id}`,     desc: `Удалить ${e.name}`, auth: authEnabled },
            ];
        });
    }, [entities, isCSharp, authEnabled]);

    const authEndpoints = useMemo(() => {
        if (!authEnabled) return [];
        return [
            { method: 'POST', path: '/api/auth/register', desc: 'Регистрация', auth: false },
            { method: 'POST', path: '/api/auth/login',    desc: 'Вход, возвращает JWT', auth: false },
            { method: 'GET',  path: '/api/auth/me',       desc: 'Профиль текущего пользователя', auth: true },
        ] as EndpointRowProps[];
    }, [authEnabled]);

    // ── Stack details ─────────────────────────────────────────────────────────

    const stackDetails = useMemo(() => {
        if (isCSharp) {
            return [
                { icon: '⚙️', label: 'ASP.NET Core 9', sub: 'Web API framework', color: '#512bd4' },
                { icon: '🐘', label: 'PostgreSQL', sub: 'Реляционная БД', color: '#336791' },
                { icon: '📊', label: 'Entity Framework Core', sub: 'ORM, code-first migrations', color: '#6366f1' },
                { icon: '📖', label: 'Swagger / OpenAPI', sub: 'Доступно на /swagger', color: '#85ea2d' },
                { icon: '🐳', label: 'Docker + Compose', sub: 'Containerization', color: '#0ea5e9' },
                ...(authEnabled ? [
                    { icon: '🔐', label: 'JWT Bearer Auth', sub: 'Access + Refresh tokens', color: '#6366f1' },
                    ...(rolesEnabled ? [{ icon: '👑', label: 'Role-based Access', sub: 'Admin / User roles', color: '#8b5cf6' }] : []),
                ] : []),
            ];
        } else {
            return [
                { icon: '🟢', label: 'Node.js + Express', sub: 'Web API framework', color: '#8cc84b' },
                { icon: '🍃', label: 'MongoDB', sub: 'Документная БД', color: '#4db33d' },
                { icon: '🐱', label: 'Mongoose', sub: 'ODM, Schema validation', color: '#880000' },
                { icon: '📖', label: 'Swagger (swagger-jsdoc)', sub: 'Доступно на /api-docs', color: '#85ea2d' },
                { icon: '🧪', label: 'Jest', sub: 'Unit-тесты для каждой сущности', color: '#f59e0b' },
                { icon: '🔍', label: 'ESLint', sub: 'Lint rules configured', color: '#4b32c3' },
                { icon: '🐳', label: 'Docker + Compose', sub: 'Containerization', color: '#0ea5e9' },
                ...(authEnabled ? [
                    { icon: '🔐', label: 'JWT Auth', sub: 'Access + Refresh tokens', color: '#6366f1' },
                    ...(rolesEnabled ? [{ icon: '👑', label: 'Role Middleware', sub: 'Admin / User roles', color: '#8b5cf6' }] : []),
                ] : []),
            ];
        }
    }, [isCSharp, authEnabled, rolesEnabled]);

    const totalFiles = useMemo(() => {
        let count = entities.length * (isCSharp ? 3 : 4); // модели + dtos/маршруты + контроллеры + (валидация для Node)
        if (isCSharp) count += 8; // Data, Middleware, Properties, GlobalUsings, Program, csproj, appsettings×2
        else count += 10; // config×2, utils, app, server, package.json, .env, .gitignore, .eslintrc, jest.config
        count += 3; // Dockerfile, docker-compose, README
        if (authEnabled) count += isCSharp ? 6 : 4;
        return count;
    }, [entities, isCSharp, authEnabled]);

    const ACCENT = isCSharp ? '#512bd4' : '#8cc84b';

    const tabs = [
        { id: 'tree' as const, label: '📁 Файловая структура' },
        { id: 'endpoints' as const, label: `🔗 REST API (${entityEndpoints.length + authEndpoints.length})` },
        { id: 'stack' as const, label: '🛠 Стек технологий' },
    ];

    return (
        <div style={{
            marginBottom: '24px',
            borderRadius: '14px',
            border: `1px solid ${ACCENT}30`,
            background: `linear-gradient(160deg, ${ACCENT}05 0%, rgba(16,185,129,0.02) 100%)`,
            overflow: 'hidden',
        }}>
            {/* Заголовок */}
            <div
                onClick={() => setIsCollapsed(c => !c)}
                style={{
                    padding: '14px 20px', cursor: 'pointer',
                    display: 'flex', alignItems: 'center', gap: '12px',
                    borderBottom: isCollapsed ? 'none' : `1px solid ${ACCENT}18`,
                    userSelect: 'none',
                }}
            >
                <div style={{ width: '36px', height: '36px', borderRadius: '10px', background: ACCENT + '15', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '1.1rem', flexShrink: 0 }}>
                    {isCSharp ? '⚙️' : '🟢'}
                </div>
                <div style={{ flex: 1 }}>
                    <div style={{ fontWeight: 800, fontSize: '0.95rem', color: 'var(--text-primary)' }}>Предпросмотр монолитной структуры</div>
                    <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', marginTop: '1px' }}>
                        ~{totalFiles} файлов · {entities.length} сущност{entities.length === 1 ? 'ь' : entities.length < 5 ? 'и' : 'ей'}{authEnabled ? ' · Auth включена' : ''}
                    </div>
                </div>
                <div style={{ display: 'flex', gap: '6px', alignItems: 'center' }}>
                    <span style={{ padding: '2px 10px', borderRadius: '10px', fontSize: '0.72rem', fontWeight: 600, background: ACCENT + '15', color: ACCENT, border: `1px solid ${ACCENT}30` }}>
                        {isCSharp ? '⚙️ C# + PostgreSQL' : '🟢 Node.js + MongoDB'}
                    </span>
                    <span style={{ color: 'var(--text-secondary)', fontSize: '0.8rem' }}>{isCollapsed ? '▼' : '▲'}</span>
                </div>
            </div>

            {!isCollapsed && (
                <div style={{ padding: '0' }}>
                    {/* Вкладки */}
                    <div style={{ display: 'flex', borderBottom: `1px solid ${ACCENT}18`, padding: '0 20px', gap: '0' }}>
                        {tabs.map(tab => (
                            <button
                                key={tab.id}
                                onClick={() => setActiveTab(tab.id)}
                                style={{
                                    padding: '10px 16px', border: 'none', background: 'transparent', cursor: 'pointer',
                                    fontSize: '0.78rem', fontWeight: activeTab === tab.id ? 700 : 400,
                                    color: activeTab === tab.id ? ACCENT : 'var(--text-secondary)',
                                    borderBottom: activeTab === tab.id ? `2px solid ${ACCENT}` : '2px solid transparent',
                                    transition: 'all 0.15s',
                                    marginBottom: '-1px',
                                }}
                            >
                                {tab.label}
                            </button>
                        ))}
                    </div>

                    <div style={{ padding: '16px 20px' }}>

                        {/* ── Tab: File Tree ── */}
                        {activeTab === 'tree' && (
                            <div>
                                <div style={{ marginBottom: '10px', display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
                                    {[
                                        { icon: '⚙️', label: 'Model', color: '#10b981' },
                                        { icon: '📋', label: 'DTO / Schema', color: '#f59e0b' },
                                        { icon: '🎮', label: 'Controller', color: '#3b82f6' },
                                        { icon: '🔐', label: 'Auth', color: '#6366f1' },
                                        { icon: '🐳', label: 'Docker', color: '#0ea5e9' },
                                    ].map(l => (
                                        <span key={l.label} style={{ display: 'flex', alignItems: 'center', gap: '4px', fontSize: '0.68rem', color: l.color, padding: '2px 7px', borderRadius: '5px', background: l.color + '10', border: `1px solid ${l.color}25` }}>
                                            {l.icon} {l.label}
                                        </span>
                                    ))}
                                </div>
                                <div style={{ background: 'var(--bg-secondary)', borderRadius: '10px', padding: '12px 14px', maxHeight: '380px', overflowY: 'auto' }}>
                                    <FileTree nodes={fileTree} />
                                </div>
                                <div style={{ marginTop: '10px', padding: '8px 12px', borderRadius: '8px', background: 'rgba(16,185,129,0.06)', border: '1px solid rgba(16,185,129,0.2)', fontSize: '0.72rem', color: 'var(--text-secondary)' }}>
                                    💡 Нажмите на папку, чтобы свернуть/развернуть. Все файлы будут упакованы в <strong>.zip</strong> архив.
                                </div>
                            </div>
                        )}

                        {/* ── Tab: Endpoints ── */}
                        {activeTab === 'endpoints' && (
                            <div>
                                {authEnabled && (
                                    <div style={{ marginBottom: '16px' }}>
                                        <div style={{ fontWeight: 700, fontSize: '0.8rem', color: '#6366f1', marginBottom: '8px', display: 'flex', alignItems: 'center', gap: '6px' }}>
                                            <span>🔐</span> Auth эндпоинты
                                        </div>
                                        <div style={{ background: 'var(--bg-secondary)', borderRadius: '10px', padding: '4px 12px' }}>
                                            {authEndpoints.map((ep, i) => <EndpointRow key={i} {...ep} />)}
                                        </div>
                                    </div>
                                )}

                                {entities.length > 0 ? (
                                    <div>
                                        <div style={{ fontWeight: 700, fontSize: '0.8rem', color: 'var(--text-primary)', marginBottom: '8px', display: 'flex', alignItems: 'center', gap: '6px' }}>
                                            <span>📦</span> CRUD эндпоинты ({entities.length} сущност{entities.length === 1 ? 'ь' : entities.length < 5 ? 'и' : 'ей'})
                                        </div>
                                        <div style={{ background: 'var(--bg-secondary)', borderRadius: '10px', padding: '4px 12px', maxHeight: '320px', overflowY: 'auto' }}>
                                            {entityEndpoints.map((ep, i) => <EndpointRow key={i} {...ep} />)}
                                        </div>
                                    </div>
                                ) : (
                                    <div style={{ textAlign: 'center', padding: '24px', color: 'var(--text-secondary)', fontSize: '0.8rem' }}>
                                        Добавьте сущности, чтобы увидеть эндпоинты
                                    </div>
                                )}

                                <div style={{ marginTop: '10px', padding: '8px 12px', borderRadius: '8px', background: ACCENT + '08', border: `1px solid ${ACCENT}20`, fontSize: '0.72rem', color: 'var(--text-secondary)' }}>
                                    📖 Swagger UI: <code style={{ color: ACCENT }}>{isCSharp ? 'http://localhost:5000/swagger' : 'http://localhost:3000/api-docs'}</code>
                                </div>
                            </div>
                        )}

                        {/* ── Tab: Stack ── */}
                        {activeTab === 'stack' && (
                            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))', gap: '10px' }}>
                                {stackDetails.map((s, i) => (
                                    <div key={i} style={{
                                        padding: '12px 14px', borderRadius: '10px',
                                        background: s.color + '08', border: `1px solid ${s.color}25`,
                                        display: 'flex', alignItems: 'flex-start', gap: '10px',
                                        transition: 'box-shadow 0.15s',
                                    }}
                                        onMouseEnter={e => { (e.currentTarget as HTMLElement).style.boxShadow = `0 2px 10px ${s.color}20`; }}
                                        onMouseLeave={e => { (e.currentTarget as HTMLElement).style.boxShadow = 'none'; }}
                                    >
                                        <span style={{ fontSize: '1.2rem', flexShrink: 0, marginTop: '1px' }}>{s.icon}</span>
                                        <div>
                                            <div style={{ fontWeight: 700, fontSize: '0.8rem', color: s.color }}>{s.label}</div>
                                            <div style={{ fontSize: '0.7rem', color: 'var(--text-secondary)', marginTop: '1px' }}>{s.sub}</div>
                                        </div>
                                    </div>
                                ))}
                            </div>
                        )}

                    </div>
                </div>
            )}
        </div>
    );
}

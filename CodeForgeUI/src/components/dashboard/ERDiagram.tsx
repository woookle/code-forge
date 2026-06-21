import { useCallback, useMemo } from 'react';
import {
    ReactFlow,
    Node,
    Edge,
    Background,
    Controls,
    MiniMap,
    useNodesState,
    useEdgesState,
    BackgroundVariant,
    MarkerType,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import { Entity, Field } from '../../types';

interface Props {
    entities: Entity[];
    isDarkMode?: boolean;
}

const CARD_WIDTH = 220;
const FIELD_HEIGHT = 26;
const HEADER_HEIGHT = 40;
const PADDING = 16;

const COLORS = ['#6366f1', '#10b981', '#f59e0b', '#ef4444', '#3b82f6', '#8b5cf6', '#ec4899', '#14b8a6'];

function ERDiagram({ entities, isDarkMode }: Props) {
    // Рассчитываем позиции узлов по кругу / сетке
    const { nodes: initialNodes, edges: initialEdges } = useMemo(() => {
        const nodes: Node[] = [];
        const edges: Edge[] = [];
        const edgeSet = new Set<string>();

        const cols = Math.ceil(Math.sqrt(entities.length));
        const xGap = 300;
        const yGap = 80;

        entities.forEach((entity, idx) => {
            const col = idx % cols;
            const row = Math.floor(idx / cols);
            const color = COLORS[idx % COLORS.length];
            const fields = entity.fields ?? [];
            const height = HEADER_HEIGHT + fields.length * FIELD_HEIGHT + PADDING;

            nodes.push({
                id: entity.id,
                type: 'default',
                position: { x: col * (CARD_WIDTH + xGap) + 60, y: row * (height + yGap) + 60 },
                data: {
                    label: (
                        <div className="er-node" style={{ '--node-color': color } as React.CSSProperties}>
                            <div className="er-node-header">
                                <span className="er-node-title">{entity.name}</span>
                                <span className="er-node-count">{fields.length} полей</span>
                            </div>
                            <div className="er-node-fields">
                                {fields.map((f: Field) => (
                                    <div key={f.id} className={`er-field ${f.isPrimaryKey ? 'er-field--pk' : ''} ${f.isRequired ? 'er-field--req' : ''}`}>
                                        <span className="er-field-icon">
                                            {f.isPrimaryKey ? '🔑' : f.dataType === 'Relationship' ? '🔗' : '·'}
                                        </span>
                                        <span className="er-field-name">{f.name}</span>
                                        <span className="er-field-type">{f.dataType}</span>
                                    </div>
                                ))}
                                {fields.length === 0 && (
                                    <div className="er-field" style={{ color: 'var(--text-secondary)', fontStyle: 'italic' }}>
                                        <span>Нет полей</span>
                                    </div>
                                )}
                            </div>
                        </div>
                    ),
                },
                style: {
                    width: CARD_WIDTH,
                    padding: 0,
                    border: `1.5px solid ${color}55`,
                    borderRadius: 10,
                    background: isDarkMode ? '#1a1a1a' : '#fff',
                    boxShadow: `0 2px 12px ${color}22`,
                },
            });

            // Рёбра из relationship-полей
            fields.forEach((f: Field) => {
                if (f.dataType === 'Relationship' && f.relatedEntityId) {
                    const edgeId = `${entity.id}→${f.relatedEntityId}`;
                    if (!edgeSet.has(edgeId)) {
                        edgeSet.add(edgeId);
                        const relType = f.relationshipType ?? 'OneToMany';
                        edges.push({
                            id: edgeId,
                            source: entity.id,
                            target: f.relatedEntityId,
                            animated: false,
                            label: relType === 'ManyToMany' ? 'M:N' : relType === 'OneToMany' ? '1:N' : '1:1',
                            style: { stroke: color, strokeWidth: 1.5 },
                            labelStyle: { fill: color, fontSize: 10, fontWeight: 600 },
                            labelBgStyle: { fill: isDarkMode ? '#222' : '#fff' },
                            markerEnd: { type: MarkerType.ArrowClosed, color },
                        });
                    }
                }
            });

            // Рёбра из sourceRelationships
            (entity.sourceRelationships ?? []).forEach(rel => {
                const edgeId = `rel-${rel.id}`;
                if (!edgeSet.has(edgeId)) {
                    edgeSet.add(edgeId);
                    edges.push({
                        id: edgeId,
                        source: rel.sourceEntityId,
                        target: rel.targetEntityId,
                        animated: false,
                        label: rel.relationshipType === 'ManyToMany' ? 'M:N' : rel.relationshipType === 'OneToMany' ? '1:N' : '1:1',
                        style: { stroke: color, strokeWidth: 1.5 },
                        labelStyle: { fill: color, fontSize: 10, fontWeight: 600 },
                        labelBgStyle: { fill: isDarkMode ? '#222' : '#fff' },
                        markerEnd: { type: MarkerType.ArrowClosed, color },
                    });
                }
            });
        });

        return { nodes, edges };
    }, [entities, isDarkMode]);

    const [nodes, , onNodesChange] = useNodesState(initialNodes);
    const [edges, , onEdgesChange] = useEdgesState(initialEdges);

    const onInit = useCallback(() => {}, []);

    return (
        <div className="er-diagram-wrap">
            <ReactFlow
                nodes={nodes}
                edges={edges}
                onNodesChange={onNodesChange}
                onEdgesChange={onEdgesChange}
                onInit={onInit}
                fitView
                fitViewOptions={{ padding: 0.2 }}
                minZoom={0.3}
                maxZoom={2}
                colorMode={isDarkMode ? 'dark' : 'light'}
            >
                <Background
                    variant={BackgroundVariant.Dots}
                    gap={20}
                    size={1}
                    color={isDarkMode ? '#2a2a2a' : '#e2e8f0'}
                />
                <Controls showInteractive={false} />
                <MiniMap
                    nodeColor={(n) => {
                        const border = (n.style?.border as string) ?? '';
                        const match = border.match(/#[0-9a-f]{6}/i);
                        return match ? match[0] : '#6366f1';
                    }}
                    maskColor={isDarkMode ? 'rgba(0,0,0,0.6)' : 'rgba(255,255,255,0.7)'}
                    style={{ background: isDarkMode ? '#111' : '#f8fafc' }}
                />
            </ReactFlow>
        </div>
    );
}

export default ERDiagram;

/** Формат одной записи истории генераций (соответствует API-ответу) */
export interface GenerationRecord {
    id: string;
    projectId?: string;
    projectName: string;
    targetStack: string;
    architectureType: string;
    entityCount: number;
    createdAt: string; // ISO
}

export interface AchievementInfo {
    id: string;
    icon: string;
    title: string;
    description: string;
    color: string;
    unlocked?: boolean;
    unlockedAt?: string;
}

export function formatTimestamp(iso: string): string {
    try {
        return new Intl.DateTimeFormat('ru-RU', {
            day: '2-digit', month: '2-digit', year: 'numeric',
            hour: '2-digit', minute: '2-digit',
        }).format(new Date(iso));
    } catch {
        return iso;
    }
}

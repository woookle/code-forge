import { toast } from 'react-toastify';

interface AchievementInfo {
    id: string;
    icon: string;
    title: string;
    description: string;
    color: string;
}

export function showAchievementToast(achievement: AchievementInfo) {
    toast(
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.85rem', padding: '0.15rem 0' }}>
            <div style={{
                width: 46, height: 46, borderRadius: 12, flexShrink: 0,
                background: `${achievement.color}22`,
                border: `2px solid ${achievement.color}55`,
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                fontSize: '1.5rem',
            }}>
                {achievement.icon}
            </div>
            <div>
                <div style={{ fontSize: '0.68rem', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.06em', color: achievement.color, marginBottom: '1px' }}>
                    🏆 Новое достижение!
                </div>
                <div style={{ fontWeight: 700, fontSize: '0.9rem', color: 'var(--text-primary)', lineHeight: 1.2 }}>
                    {achievement.title}
                </div>
                <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', marginTop: '2px' }}>
                    {achievement.description}
                </div>
            </div>
        </div>,
        {
            position: 'bottom-right',
            autoClose: 5000,
            style: {
                borderLeft: `4px solid ${achievement.color}`,
                background: 'var(--bg-secondary)',
            },
        }
    );
}

export function showAchievements(achievements: AchievementInfo[]) {
    achievements.forEach((a, i) => {
        setTimeout(() => showAchievementToast(a), i * 800);
    });
}

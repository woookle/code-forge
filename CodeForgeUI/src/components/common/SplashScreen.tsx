import { useEffect, useState } from 'react';

interface Props {
    onDone: () => void;
}

function SplashScreen({ onDone }: Props) {
    const [phase, setPhase] = useState<'enter' | 'idle' | 'exit'>('enter');

    useEffect(() => {
        const t1 = setTimeout(() => setPhase('idle'), 60);
        const t2 = setTimeout(() => setPhase('exit'), 2800);
        const t3 = setTimeout(() => onDone(), 3400);
        return () => {
            clearTimeout(t1);
            clearTimeout(t2);
            clearTimeout(t3);
        };
    }, [onDone]);

    return (
        <div className={`splash-overlay splash-overlay--${phase}`}>
            <div className="splash-center">
                <div className="splash-icon-box">
                    <span className="splash-icon">⚡</span>
                </div>
                <h1 className="splash-title">CodeForge</h1>
                <p className="splash-subtitle">Генерация backend-кода нового поколения</p>
            </div>
            <div className="splash-progress-track">
                <div className="splash-progress-fill" />
            </div>
        </div>
    );
}

export default SplashScreen;

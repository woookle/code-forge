import React, { useState, useEffect, useRef } from 'react';

// ─── FAQ Data ────────────────────────────────────────────────────────────────

interface FAQItem {
    id: string;
    category: string;
    categoryIcon: string;
    question: string;
    answer: string;
}

const faqs: FAQItem[] = [
    {
        id: 'f1',
        category: 'Начало работы',
        categoryIcon: '🚀',
        question: 'Как создать первый проект?',
        answer: 'Нажмите кнопку «+ Новый проект» в левой боковой панели. Введите название, выберите стек (C# + PostgreSQL или Node.js + MongoDB) и архитектуру (Монолит или Микросервисы). Проект появится в списке слева — кликните на него, чтобы начать работу.',
    },
    {
        id: 'f2',
        category: 'Начало работы',
        categoryIcon: '🚀',
        question: 'Как добавить сущность в проект?',
        answer: 'Откройте проект из боковой панели, затем нажмите «+ Новая сущность» вверху. Дайте сущности имя в PascalCase (например, Product, BlogPost). Для микросервисной архитектуры также укажите имя сервиса.',
    },
    {
        id: 'f3',
        category: 'Поля и связи',
        categoryIcon: '🔗',
        question: 'Как добавить поля к сущности?',
        answer: 'Нажмите кнопку «+ Поле» на карточке нужной сущности. Введите название поля, выберите тип данных (String, Integer, DateTime и др.) и отметьте атрибуты: «Обязательное» или «Уникальное».',
    },
    {
        id: 'f4',
        category: 'Поля и связи',
        categoryIcon: '🔗',
        question: 'Как настроить связи между сущностями?',
        answer: 'При добавлении поля выберите тип «Связь (Relationship)». Затем выберите связанную сущность и тип связи: One-to-Many (один ко многим), One-to-One (один к одному) или Many-to-Many (многие ко многим). Генератор автоматически создаст внешние ключи и навигационные свойства.',
    },
    {
        id: 'f5',
        category: 'Аутентификация',
        categoryIcon: '🔐',
        question: 'Как добавить JWT аутентификацию?',
        answer: 'Нажмите кнопку «🔓 Auth» в панели управления проектом. Включите аутентификацию, настройте: идентификатор пользователя (email/username), роли, время жизни токена и защиту маршрутов. После сохранения в списке сущностей появится карточка User с автосгенерированными полями.',
    },
    {
        id: 'f6',
        category: 'Аутентификация',
        categoryIcon: '🔐',
        question: 'Как защитить конкретные маршруты?',
        answer: 'В настройках Auth откройте раздел «Защита маршрутов». Для каждой сущности вы можете отметить конкретные HTTP-методы (GET, POST, PUT, PATCH, DELETE), которые будут требовать авторизации. Используйте колонку «Все» для быстрого выбора.',
    },
    {
        id: 'f7',
        category: 'Генерация',
        categoryIcon: '📦',
        question: 'Как скачать сгенерированный проект?',
        answer: 'Нажмите кнопку «📦 Скачать проект» в верхней панели. Генератор создаст ZIP-архив со всем исходным кодом: контроллеры, модели, миграции, docker-compose, README и полностью настроенное приложение.',
    },
    {
        id: 'f8',
        category: 'Генерация',
        categoryIcon: '📦',
        question: 'Что включает сгенерированный код?',
        answer: 'Сгенерированный проект содержит: CRUD-контроллеры со Swagger-документацией, модели с валидацией, Entity Framework (для C#) или Mongoose (для Node.js), Docker и docker-compose файлы, README с инструкцией запуска, .env конфигурации и опционально — полный модуль аутентификации (JWT, регистрация, логин, refresh-токены).',
    },
    {
        id: 'f9',
        category: 'Микросервисы',
        categoryIcon: '🔀',
        question: 'Как работают микросервисы?',
        answer: 'При выборе архитектуры «Микросервисы» каждая группа сущностей выделяется в отдельный сервис. Нажмите «⚙️ Сервис» на карточке сущности, чтобы задать имя сервиса. Сервисы общаются через RabbitMQ (события), каждый имеет собственную БД и Dockerfile.',
    },
];

// ─── Tutorial Steps ───────────────────────────────────────────────────────────

interface TutorialStep {
    icon: string;
    title: string;
    description: string;
    tip?: string;
}

const tutorialSteps: TutorialStep[] = [
    {
        icon: '📁',
        title: 'Создайте проект',
        description: 'Нажмите «+ Новый проект» в боковой панели. Выберите технологический стек (C# или Node.js) и тип архитектуры.',
        tip: 'Совет: для учебных проектов начните с монолита — его проще понять и настроить.',
    },
    {
        icon: '🏗️',
        title: 'Добавьте сущности',
        description: 'Сущности — это таблицы вашей БД. Нажмите «+ Новая сущность» и создайте нужные модели данных (User, Product, Order и т.д.).',
        tip: 'Совет: используйте PascalCase для имён сущностей (например, BlogPost, не blogpost).',
    },
    {
        icon: '🔧',
        title: 'Настройте поля',
        description: 'Для каждой сущности добавьте поля с помощью кнопки «+ Поле». Выберите тип данных, отметьте обязательные и уникальные поля.',
        tip: 'Совет: поле Id (Guid) создаётся автоматически — добавляйте только бизнес-поля.',
    },
    {
        icon: '🔗',
        title: 'Создайте связи',
        description: 'Выберите тип поля «Relationship» для связи между сущностями. Укажите связанную сущность и тип связи (1:1, 1:N, N:N).',
        tip: 'Совет: для блога связь «Post → User» это One-to-Many (один автор — много постов).',
    },
    {
        icon: '🔐',
        title: 'Настройте Auth (опционально)',
        description: 'Нажмите кнопку «Auth» для добавления JWT аутентификации. Настройте роли, защиту маршрутов и время жизни токена.',
        tip: 'Совет: Auth автоматически добавит User-сущность с полями email, passwordHash и т.д.',
    },
    {
        icon: '📦',
        title: 'Скачайте проект',
        description: 'Нажмите «📦 Скачать проект». Получите полностью рабочий ZIP-архив с документацией, Docker и готовым к запуску кодом!',
        tip: 'Совет: следуйте README.md в архиве — там есть инструкция запуска за 3 команды.',
    },
];

// ─── Component ────────────────────────────────────────────────────────────────

type TabType = 'faq' | 'tutorial';

const FAQWidget: React.FC = () => {
    const [isOpen, setIsOpen] = useState(false);
    const [activeTab, setActiveTab] = useState<TabType>('tutorial');
    const [openFaqId, setOpenFaqId] = useState<string | null>(null);
    const [searchQuery, setSearchQuery] = useState('');
    const [tutorialStep, setTutorialStep] = useState(0);
    const [animating, setAnimating] = useState(false);
    const [stepDirection, setStepDirection] = useState<'next' | 'prev'>('next');
    const panelRef = useRef<HTMLDivElement>(null);

    // Закрытие по клику вне панели
    useEffect(() => {
        if (!isOpen) return;
        const handleKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setIsOpen(false); };
        window.addEventListener('keydown', handleKey);
        return () => window.removeEventListener('keydown', handleKey);
    }, [isOpen]);

    // Сброс туториала при открытии
    useEffect(() => {
        if (isOpen) {
            setTutorialStep(0);
            setSearchQuery('');
        }
    }, [isOpen]);

    const filteredFaqs = faqs.filter(f =>
        searchQuery === '' ||
        f.question.toLowerCase().includes(searchQuery.toLowerCase()) ||
        f.answer.toLowerCase().includes(searchQuery.toLowerCase()) ||
        f.category.toLowerCase().includes(searchQuery.toLowerCase())
    );

    const categories = Array.from(new Set(filteredFaqs.map(f => f.category)));

    const goToStep = (dir: 'next' | 'prev') => {
        if (animating) return;
        const next = dir === 'next' ? tutorialStep + 1 : tutorialStep - 1;
        if (next < 0 || next >= tutorialSteps.length) return;
        setStepDirection(dir);
        setAnimating(true);
        setTimeout(() => {
            setTutorialStep(next);
            setAnimating(false);
        }, 220);
    };

    const step = tutorialSteps[tutorialStep];
    const progress = ((tutorialStep + 1) / tutorialSteps.length) * 100;

    return (
        <>
            {/* Плавающая кнопка */}
            <button
                className="faq-fab"
                onClick={() => setIsOpen(v => !v)}
                title="Справка и обучение"
                aria-label="Открыть справку"
            >
                <span className={`faq-fab-icon ${isOpen ? 'faq-fab-icon--open' : ''}`}>
                    {isOpen ? '✕' : '?'}
                </span>
                {!isOpen && <span className="faq-fab-pulse" />}
            </button>

            {/* Панель */}
            {isOpen && (
                <div className="faq-backdrop" onClick={() => setIsOpen(false)}>
                    <div
                        ref={panelRef}
                        className="faq-panel animate-faq-in"
                        onClick={e => e.stopPropagation()}
                        role="dialog"
                        aria-modal="true"
                        aria-label="Справка CodeForge"
                    >
                        {/* Заголовок */}
                        <div className="faq-header">
                            <div className="faq-header-brand">
                                <div className="faq-header-icon">💡</div>
                                <div>
                                    <h2 className="faq-header-title">Справочный центр</h2>
                                    <p className="faq-header-sub">CodeForge • Документация</p>
                                </div>
                            </div>
                            <button className="faq-close-btn" onClick={() => setIsOpen(false)} aria-label="Закрыть">
                                ✕
                            </button>
                        </div>

                        {/* Вкладки */}
                        <div className="faq-tabs">
                            <button
                                className={`faq-tab ${activeTab === 'tutorial' ? 'faq-tab--active' : ''}`}
                                onClick={() => setActiveTab('tutorial')}
                            >
                                <span>🎓</span> Обучение
                            </button>
                            <button
                                className={`faq-tab ${activeTab === 'faq' ? 'faq-tab--active' : ''}`}
                                onClick={() => setActiveTab('faq')}
                            >
                                <span>❓</span> FAQ
                            </button>
                        </div>

                        {/* ── TUTORIAL TAB ── */}
                        {activeTab === 'tutorial' && (
                            <div className="faq-body">
                                {/* Полоса прогресса */}
                                <div className="tutorial-progress-wrap">
                                    <div className="tutorial-progress-track">
                                        <div
                                            className="tutorial-progress-bar"
                                            style={{ width: `${progress}%` }}
                                        />
                                    </div>
                                    <span className="tutorial-progress-label">
                                        {tutorialStep + 1} / {tutorialSteps.length}
                                    </span>
                                </div>

                                {/* Точки шагов */}
                                <div className="tutorial-dots">
                                    {tutorialSteps.map((_, i) => (
                                        <button
                                            key={i}
                                            className={`tutorial-dot ${i === tutorialStep ? 'tutorial-dot--active' : ''} ${i < tutorialStep ? 'tutorial-dot--done' : ''}`}
                                            onClick={() => setTutorialStep(i)}
                                            aria-label={`Шаг ${i + 1}`}
                                        />
                                    ))}
                                </div>

                                {/* Содержимое шага */}
                                <div
                                    className={`tutorial-card ${animating ? (stepDirection === 'next' ? 'tutorial-card--exit-left' : 'tutorial-card--exit-right') : ''}`}
                                >
                                    <div className="tutorial-card-icon">{step.icon}</div>
                                    <div className="tutorial-step-num">Шаг {tutorialStep + 1}</div>
                                    <h3 className="tutorial-card-title">{step.title}</h3>
                                    <p className="tutorial-card-desc">{step.description}</p>
                                    {step.tip && (
                                        <div className="tutorial-tip">
                                            <span className="tutorial-tip-icon">💡</span>
                                            <span>{step.tip}</span>
                                        </div>
                                    )}
                                </div>

                                {/* Навигация */}
                                <div className="tutorial-nav">
                                    <button
                                        className="tutorial-nav-btn tutorial-nav-btn--prev"
                                        onClick={() => goToStep('prev')}
                                        disabled={tutorialStep === 0}
                                    >
                                        ← Назад
                                    </button>

                                    {tutorialStep < tutorialSteps.length - 1 ? (
                                        <button
                                            className="tutorial-nav-btn tutorial-nav-btn--next"
                                            onClick={() => goToStep('next')}
                                        >
                                            Далее →
                                        </button>
                                    ) : (
                                        <button
                                            className="tutorial-nav-btn tutorial-nav-btn--finish"
                                            onClick={() => { setActiveTab('faq'); setTutorialStep(0); }}
                                        >
                                            🎉 Готово!
                                        </button>
                                    )}
                                </div>
                            </div>
                        )}

                        {/* ── FAQ TAB ── */}
                        {activeTab === 'faq' && (
                            <div className="faq-body">
                                {/* Поиск */}
                                <div className="faq-search-wrap">
                                    <span className="faq-search-icon">🔍</span>
                                    <input
                                        className="faq-search"
                                        type="text"
                                        placeholder="Поиск по вопросам..."
                                        value={searchQuery}
                                        onChange={e => setSearchQuery(e.target.value)}
                                        autoFocus
                                    />
                                    {searchQuery && (
                                        <button className="faq-search-clear" onClick={() => setSearchQuery('')}>✕</button>
                                    )}
                                </div>

                                {/* Результаты */}
                                {filteredFaqs.length === 0 ? (
                                    <div className="faq-empty">
                                        <span className="faq-empty-icon">🤷</span>
                                        <p>Ничего не найдено по запросу «{searchQuery}»</p>
                                    </div>
                                ) : (
                                    <div className="faq-accordion">
                                        {categories.map(cat => {
                                            const catFaqs = filteredFaqs.filter(f => f.category === cat);
                                            const catIcon = catFaqs[0]?.categoryIcon || '📌';
                                            return (
                                                <div key={cat} className="faq-category">
                                                    <div className="faq-category-label">
                                                        <span>{catIcon}</span> {cat}
                                                    </div>
                                                    {catFaqs.map((faq, idx) => (
                                                        <FAQAccordionItem
                                                            key={faq.id}
                                                            faq={faq}
                                                            isOpen={openFaqId === faq.id}
                                                            onToggle={() => setOpenFaqId(openFaqId === faq.id ? null : faq.id)}
                                                            delay={idx * 60}
                                                        />
                                                    ))}
                                                </div>
                                            );
                                        })}
                                    </div>
                                )}
                            </div>
                        )}

                        {/* Нижняя часть панели */}
                        <div className="faq-footer">
                            <span>CodeForge — генератор кода</span>
                            <button className="faq-footer-btn" onClick={() => setIsOpen(false)}>Закрыть</button>
                        </div>
                    </div>
                </div>
            )}
        </>
    );
};

// ─── Accordion Item ───────────────────────────────────────────────────────────

interface FAQAccordionItemProps {
    faq: FAQItem;
    isOpen: boolean;
    onToggle: () => void;
    delay: number;
}

const FAQAccordionItem: React.FC<FAQAccordionItemProps> = ({ faq, isOpen, onToggle, delay }) => {
    const answerRef = useRef<HTMLDivElement>(null);
    const [height, setHeight] = useState(0);

    useEffect(() => {
        if (answerRef.current) {
            setHeight(isOpen ? answerRef.current.scrollHeight : 0);
        }
    }, [isOpen]);

    return (
        <div
            className={`faq-item-new ${isOpen ? 'faq-item-new--open' : ''}`}
            style={{ animationDelay: `${delay}ms` }}
        >
            <button className="faq-item-trigger" onClick={onToggle} aria-expanded={isOpen}>
                <span className="faq-item-q">{faq.question}</span>
                <span className={`faq-item-chevron ${isOpen ? 'faq-item-chevron--open' : ''}`}>
                    ›
                </span>
            </button>
            <div
                className="faq-item-body"
                style={{ height: `${height}px` }}
            >
                <div ref={answerRef} className="faq-item-answer">
                    {faq.answer}
                </div>
            </div>
        </div>
    );
};

export default FAQWidget;

import React, { useState } from 'react';

interface FAQItem {
    question: string;
    answer: string;
}

const faqs: FAQItem[] = [
    {
        question: "Как создать проект?",
        answer: "Нажмите кнопку '+ Новый проект' в левой боковой панели. Введите название проекта и выберите целевой стек (C# .NET или Java Spring)."
    },
    {
        question: "Как добавить сущности?",
        answer: "Выберите проект в боковом меню. В основном окне нажмите кнопку '+ Сущность'. Заполните имя сущности и добавьте необходимые поля."
    },
    {
        question: "Как настроить связи?",
        answer: "При создании поля выберите тип данных 'Relationship'. Затем выберите связанную сущность и укажите тип связи (One-to-Many, Many-to-Many и т.д.)."
    },
    {
        question: "Как скачать проект?",
        answer: "В верхнем правом углу карточки проекта нажмите кнопку 'Скачать проект'. Генератор создаст архив со всем исходным кодом."
    }
];

const FAQWidget: React.FC = () => {
    const [isOpen, setIsOpen] = useState(false);

    return (
        <>
            <button
                className="faq-fab"
                onClick={() => setIsOpen(true)}
                title="Часто задаваемые вопросы"
            >
                ?
            </button>

            {isOpen && (
                <div className="modal-overlay" onClick={() => setIsOpen(false)}>
                    <div className="modal faq-modal" onClick={(e) => e.stopPropagation()}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
                            <h2 style={{ margin: 0 }}>Частые вопросы</h2>
                            <button
                                className="btn btn-secondary btn-small"
                                onClick={() => setIsOpen(false)}
                                style={{ border: 'none', fontSize: '1.2rem', padding: '0.2rem 0.5rem' }}
                            >
                                ✕
                            </button>
                        </div>

                        <div className="faq-list">
                            {faqs.map((faq, index) => (
                                <div key={index} className="faq-item">
                                    <h3 className="faq-question">{faq.question}</h3>
                                    <p className="faq-answer">{faq.answer}</p>
                                </div>
                            ))}
                        </div>

                        <div style={{ marginTop: '2rem', textAlign: 'right' }}>
                            <button className="btn btn-primary" onClick={() => setIsOpen(false)}>
                                Понятно
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </>
    );
};

export default FAQWidget;

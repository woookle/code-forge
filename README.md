# 🚀 CodeForge

<p align="center">
  <img src="./CodeForgeUI/public/logo_white.svg" alt="CodeForge Logo" width="200">
</p>

> **Visual Entity Designer & Code Generator for .NET and Node.js**

![ASP.NET](https://img.shields.io/badge/ASP.NET-9.0-black?style=for-the-badge&logo=dotnet)
![REACT](https://img.shields.io/badge/React-18.2.0-black?style=for-the-badge&logo=react)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16.0-black?style=for-the-badge&logo=postgresql)
![Vite](https://img.shields.io/badge/Vite-5.0.8-black?style=for-the-badge&logo=vite)
![Version](https://img.shields.io/badge/Version-1.0.0-black?style=for-the-badge&logo=github)
![Status](https://img.shields.io/badge/Status-Active-success?style=for-the-badge)

CodeForge - веб-приложение для генерации шаблонного кода бэкенда на основе визуального проектирования сущностей данных. Проектируйте связи, настраивайте поля и получайте готовый к запуску проект за секунды.

---

## 🌟 Основные возможности

### 🛠 Проектирование и Генерация
*   **Визуальный редактор**: Создание сущностей и полей через интуитивный UI.
*   **Поддержка связей**: Простая настройка связей *One-to-Many*, *One-to-One*, *Many-to-Many*.
*   **Мультистековая генерация**:
    *   🔵 **C# + PostgreSQL** (ASP.NET Core Web API + Entity Framework Core)
    *   🟢 **Node.js + MongoDB** (Express + Mongoose)
*   **Валидация**: Автоматическая проверка имен и типов данных перед генерацией.
*   **Скачивание**: Получение готового проекта в формате ZIP архива.

### 🔐 Безопасность и Доступ
*   **Аутентификация**: JWT Access + Refresh Tokens.
*   **Верификация**: Подтверждение регистрации через Email (6-значный код).
*   **Сброс пароля**: Безопасный флоу восстановления доступа через Email.
*   **Ролевая модель**: Разделение прав доступа (User / Admin).
*   **Admin Panel**: Управление пользователями, просмотр статистики и проектов.

### 🎨 Интерфейс
*   **Современный дизайн**: Чистый UI с анимациями и отзывчивостью.
*   **Dashboard**: Удобное управление списком проектов.
*   **FAQ Widget**: Быстрый доступ к справке.

---

## 🏗 Технологический стек

### Core
*   **Frontend**: React, TypeScript, Vite
*   **Backend**: ASP.NET Core 9.0 Web API
*   **Database**: PostgreSQL 16
*   **Containerization**: Docker, Docker Compose

### Libraries & Tools
*   **ORM**: Entity Framework Core
*   **Auth**: `System.IdentityModel.Tokens.Jwt`, `BCrypt.Net`
*   **Email**: `MailKit` (Gmail SMTP)
*   **Documentation**: Swagger / OpenAPI
*   **Validation**: FluentValidation

---

## 🚀 Установка и запуск

### Предварительные требования
*   [Node.js](https://nodejs.org/) (v16+)
*   [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
*   [PostgreSQL](https://www.postgresql.org/) (или Docker)

### Вариант 1: Docker Compose (Рекомендуется)

Запустите все сервисы одной командой:
```bash
docker-compose up --build
```
*   📱 **Frontend**: `http://localhost:5173`
*   ⚙️ **Backend API**: `http://localhost:5123`
*   📄 **Swagger UI**: `http://localhost:5123/swagger`

### Вариант 2: Локальная разработка

#### 1. Настройка Backend
```bash
cd CodeGeneratorAPI
# Настройте строку подключения в appsettings.json
dotnet ef migrations add InitialCreate
dotnet ef database update
dotnet run
```

#### 2. Настройка Frontend
```bash
cd code-generator-ui
npm install
npm run dev
```

---

## 📂 Структура сгенерированного проекта

#### C# + PostgreSQL
Генерируется полноценный Web API проект с соблюдением Clean Architecture:
*   `Models/` - Entity Framework модели
*   `Controllers/` - REST API контроллеры с CRUD
*   `Data/` - Конфигурация DbContext
*   `Dto/` - Data Transfer Objects (опционально)
*   `docker-compose.yml` - Готовый файл для деплоя

#### Node.js + MongoDB
Генерируется Express приложение:
*   `models/` - Mongoose схемы
*   `routes/` - Express роуты
*   `controllers/` - Логика обработки запросов
*   `app.js` - Точка входа
*   `.env` - Конфигурация


---

<div>
  <p align='center'>
    <img src='https://media1.tenor.com/m/oKZVauJ1LWEAAAAd/anime-fern.gif' />
  </p>
  <h2 align='center'>хорошего дня 😊</h2>
</div>
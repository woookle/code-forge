<div align="center">

<img src="./CodeForgeUI/public/logo_white.svg" alt="CodeForge" width="120" />

# CodeForge

**Визуальный дизайнер сущностей и генератор бэкенда**

[![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-9.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/React-18.2-61DAFB?style=for-the-badge&logo=react&logoColor=black)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5.2-3178C6?style=for-the-badge&logo=typescript&logoColor=white)](https://www.typescriptlang.org/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-336791?style=for-the-badge&logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?style=for-the-badge&logo=docker&logoColor=white)](https://www.docker.com/)
[![Status](https://img.shields.io/badge/Статус-Активен-22c55e?style=for-the-badge)](.)

> Проектируйте модели данных через браузер — получайте готовый к запуску бэкенд за секунды.  
> Поддерживаются **монолит** и **микросервисная** архитектура для стеков **C# + PostgreSQL** и **Node.js + MongoDB**.

</div>

---

## 📋 Содержание

- [О проекте](#-о-проекте)
- [Возможности](#-возможности)
- [Технологический стек](#-технологический-стек)
- [Архитектура приложения](#-архитектура-приложения)
- [Установка и запуск](#-установка-и-запуск)
- [Структура проекта](#-структура-проекта)
- [API приложения](#-api-приложения)
- [Генерация кода](#-генерация-кода)
- [Настройка окружения](#-настройка-окружения)

---

## 🧩 О проекте

**CodeForge** — это full-stack веб-приложение, которое позволяет разработчикам быстро проектировать структуру данных через удобный визуальный интерфейс и мгновенно получать полностью рабочий backend-проект.

Вместо написания однотипного шаблонного кода — просто создайте сущности, настройте поля и связи, выберите стек и архитектуру, и скачайте готовый ZIP с проектом.

```
Создал сущности → Настроил поля → Нажал «Скачать» → Запустил docker-compose up
```

---

## ✨ Возможности

### 🏗 Проектирование и генерация

| Возможность | Описание |
|---|---|
| **Визуальный редактор** | Создание сущностей и полей через интуитивный UI без написания кода |
| **Связи между сущностями** | Поддержка *One-to-Many*, *One-to-One*, *Many-to-Many* |
| **Мультистековая генерация** | C# + PostgreSQL (ASP.NET Core + EF Core) и Node.js + MongoDB (Express + Mongoose) |
| **Два типа архитектуры** | Монолит и микросервисная архитектура |
| **Визуальный предпросмотр** | Интерактивная схема архитектуры прямо в Dashboard |
| **Скачивание ZIP** | Готовый к запуску проект в одном архиве |
| **Swagger-документация** | Авто-сгенерированная OpenAPI спецификация в каждом проекте |

### 🔐 Аутентификация (генерируемая)

При включении аутентификации в проекте генерируется:

- **JWT Access + Refresh Tokens** с настраиваемым временем жизни
- **Ролевая модель** (User, Admin, кастомные роли)
- **Email-верификация** при регистрации
- **Сброс пароля** через Email
- **Идентификатор пользователя**: email / username / оба
- **Защита маршрутов** по сущностям и HTTP-методам
- **Отдельный auth-service** в режиме микросервисов

### 🔀 Микросервисная архитектура

- Каждой сущности назначается **имя сервиса** — сущности с одинаковым именем группируются
- Каждый сервис получает **собственную базу данных**
- Межсервисное взаимодействие через **RabbitMQ** (topic exchange, durable queues)
- **Автоматический routing** событий: `entity.created`, `entity.updated`, `entity.deleted`
- Генерируется `docker-compose.yml` со всеми сервисами, базами и RabbitMQ

### 👤 Платформа CodeForge

- **Регистрация и вход** с подтверждением Email
- **Двухфакторная аутентификация (2FA)** через Google Authenticator (TOTP)
- **Профиль пользователя** с аватаром и темной темой
- **Admin Panel** для управления пользователями и их проектами
- **Тёмная / светлая тема** с сохранением предпочтения

---

## 🛠 Технологический стек

### Платформа CodeForge

<table>
<tr>
<th>Слой</th>
<th>Технология</th>
<th>Версия</th>
</tr>
<tr>
<td><strong>Frontend</strong></td>
<td>React + TypeScript + Vite</td>
<td>18.2 / 5.2 / 5.0</td>
</tr>
<tr>
<td><strong>State</strong></td>
<td>Redux Toolkit</td>
<td>2.0</td>
</tr>
<tr>
<td><strong>HTTP</strong></td>
<td>Axios</td>
<td>1.6</td>
</tr>
<tr>
<td><strong>Backend</strong></td>
<td>ASP.NET Core Web API</td>
<td>9.0</td>
</tr>
<tr>
<td><strong>ORM</strong></td>
<td>Entity Framework Core + Npgsql</td>
<td>9.0</td>
</tr>
<tr>
<td><strong>База данных</strong></td>
<td>PostgreSQL</td>
<td>16</td>
</tr>
<tr>
<td><strong>Аутентификация</strong></td>
<td>JWT Bearer + BCrypt.Net + Otp.NET</td>
<td>—</td>
</tr>
<tr>
<td><strong>Email</strong></td>
<td>MailKit (Gmail SMTP)</td>
<td>4.3</td>
</tr>
<tr>
<td><strong>Документация</strong></td>
<td>Swashbuckle / OpenAPI</td>
<td>6.9</td>
</tr>
<tr>
<td><strong>QR-коды</strong></td>
<td>QRCoder</td>
<td>1.6</td>
</tr>
<tr>
<td><strong>Контейнеризация</strong></td>
<td>Docker + Docker Compose</td>
<td>—</td>
</tr>
</table>

### Что генерируется для C# + PostgreSQL

```
ASP.NET Core 9.0 · Entity Framework Core 9.0 · Npgsql 9.0
Swashbuckle 6.9 · JWT Bearer · BCrypt.Net · HealthChecks
Docker (.NET 9 + port 8080) · docker-compose с PostgreSQL
```

### Что генерируется для Node.js + MongoDB

```
Express 4 · Mongoose 8 · jsonwebtoken · bcryptjs · amqplib (RabbitMQ)
swagger-jsdoc + swagger-ui-express · express-validator · dotenv
Docker (node:18-alpine) · docker-compose с MongoDB (+ RabbitMQ для микросервисов)
```

---

## 🏛 Архитектура приложения

```
┌─────────────────────────────────────────────────────────────────┐
│                        БРАУЗЕР                                  │
│                                                                 │
│   ┌──────────────────────────────────────────────────────┐     │
│   │  React SPA  (Vite + TypeScript + Redux Toolkit)       │     │
│   │                                                       │     │
│   │  Dashboard · Profile · Admin Panel                    │     │
│   │  Редактор сущностей · Предпросмотр архитектуры        │     │
│   └───────────────────────┬──────────────────────────────┘     │
└───────────────────────────┼─────────────────────────────────────┘
                            │ HTTP/JSON (axios + cookie JWT)
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│               ASP.NET Core 9.0 Web API                         │
│                                                                 │
│  ┌──────────────┐  ┌─────────────┐  ┌───────────────────────┐  │
│  │  AuthController│  │ProjectsCtrl │  │   EntitiesController  │  │
│  │  /api/auth   │  │/api/projects│  │   /api/entities       │  │
│  └──────────────┘  └──────┬──────┘  └───────────┬───────────┘  │
│                            │                      │             │
│  ┌─────────────────────────▼──────────────────────▼──────────┐  │
│  │            CodeGeneratorService                            │  │
│  │   CSharpPostgreSQLGenerator  · NodeJSMongoDBGenerator      │  │
│  │   CSharpMicroservicesGenerator · NodeMicroservicesGenerator │  │
│  └────────────────────────────────────────────────────────────┘  │
└───────────────────────────┬─────────────────────────────────────┘
                            │ EF Core
                            ▼
                   ┌─────────────────┐
                   │   PostgreSQL 16  │
                   │  (AppDbContext)  │
                   └─────────────────┘
```

---

## 🚀 Установка и запуск

### Предварительные требования

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) — для запуска через Compose
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) — для локальной разработки
- [Node.js 18+](https://nodejs.org/) — для локальной разработки фронтенда

---

### ⚡ Вариант 1: Docker Compose (рекомендуется)

```bash
# Клонируйте репозиторий
git clone <url> CodeForge
cd CodeForge

# Запустите все сервисы
docker-compose up --build
```

| Сервис | URL |
|---|---|
| 🌐 Frontend | http://localhost:5173 |
| ⚙️ Backend API | http://localhost:5123 |
| 📄 Swagger UI | http://localhost:5123/swagger |

---

### 🔧 Вариант 2: Локальная разработка

#### 1. Backend

```bash
cd CodeForgeAPI

# Настройте переменные окружения
cp appsettings.Development.json appsettings.Development.json.local
# Укажите ConnectionStrings, JWT:Secret, Email:* в appsettings.Development.json

# Примените миграции
dotnet ef database update

# Запустите API
dotnet run
# → http://localhost:5123
# → Swagger: http://localhost:5123/swagger
```

#### 2. Frontend

```bash
cd CodeForgeUI

# Скопируйте и настройте переменные окружения
cp .env.example .env
# VITE_API_URL=http://localhost:5123/api
# VITE_IMG_URL=http://localhost:5123

npm install
npm run dev
# → http://localhost:5173
```

---

## 📂 Структура проекта

```
CodeForge/
├── 📁 CodeForgeAPI/                  # Backend — ASP.NET Core 9.0
│   ├── Controllers/
│   │   ├── AuthController.cs         # Аутентификация, 2FA, Email-верификация
│   │   ├── ProjectsController.cs     # CRUD проектов, генерация ZIP
│   │   ├── EntitiesController.cs     # CRUD сущностей
│   │   ├── FieldsController.cs       # CRUD полей
│   │   ├── RelationshipsController.cs
│   │   └── UsersController.cs        # Управление пользователями (Admin)
│   ├── Models/
│   │   ├── User.cs · Project.cs · Entity.cs
│   │   ├── Field.cs · Relationship.cs
│   │   └── AuthConfig.cs             # JSON-конфиг аутентификации
│   ├── DTOs/                         # Data Transfer Objects
│   ├── Services/
│   │   ├── CodeGeneratorService.cs   # Оркестратор генерации
│   │   └── Generators/
│   │       ├── CSharpPostgreSQLGenerator.cs          # C# монолит
│   │       ├── CSharpPostgreSQLMicroservicesGenerator.cs
│   │       ├── NodeJSMongoDBGenerator.cs             # Node.js монолит
│   │       └── NodeJSMongoDBMicroservicesGenerator.cs
│   ├── Data/
│   │   └── ApplicationDbContext.cs
│   ├── Migrations/                   # EF Core миграции
│   ├── Utilities/
│   │   └── NameValidator.cs
│   ├── Dockerfile
│   └── Program.cs
│
├── 📁 CodeForgeUI/                   # Frontend — React + TypeScript
│   ├── src/
│   │   ├── app/
│   │   │   ├── store.ts              # Redux store
│   │   │   └── hooks.ts
│   │   ├── features/
│   │   │   ├── auth/authSlice.ts     # Аутентификация
│   │   │   └── projects/projectsSlice.ts
│   │   ├── components/
│   │   │   ├── Dashboard.tsx         # Главная страница
│   │   │   ├── Login.tsx
│   │   │   ├── Profile.tsx
│   │   │   ├── AdminPanel.tsx
│   │   │   ├── MicroservicesPreview.tsx  # Схема микросервисов
│   │   │   ├── MonolithPreview.tsx       # Схема монолита
│   │   │   ├── TwoFactorSetupModal.tsx
│   │   │   ├── TotpModal.tsx
│   │   │   └── FAQWidget.tsx
│   │   ├── context/
│   │   │   └── ConfirmContext.tsx
│   │   ├── types/
│   │   │   ├── index.ts
│   │   │   └── auth.ts
│   │   └── utils/
│   │       └── api.ts                # Axios instance
│   └── public/
│       ├── logo.svg
│       └── logo_white.svg
│
├── docker-compose.yml
└── README.md
```

---

## 📡 API приложения

Базовый URL: `http://localhost:5123/api`

### Аутентификация

| Метод | Маршрут | Описание |
|---|---|---|
| `POST` | `/auth/register` | Регистрация с Email-подтверждением |
| `POST` | `/auth/login` | Вход (возвращает JWT или требует 2FA) |
| `POST` | `/auth/login-2fa` | Вход с TOTP-кодом |
| `POST` | `/auth/logout` | Выход (инвалидация refresh-токена) |
| `GET`  | `/auth/me` | Данные текущего пользователя |
| `POST` | `/auth/send-code` | Отправка кода верификации Email |
| `POST` | `/auth/forgot-password` | Запрос сброса пароля |
| `POST` | `/auth/reset-password` | Сброс пароля по коду |
| `POST` | `/auth/2fa/setup` | Инициализация 2FA (генерация QR) |
| `POST` | `/auth/2fa/enable` | Активация 2FA |
| `POST` | `/auth/2fa/disable` | Отключение 2FA |
| `POST` | `/auth/avatar` | Загрузка аватара |

### Проекты

| Метод | Маршрут | Описание |
|---|---|---|
| `GET`    | `/projects` | Список проектов пользователя |
| `GET`    | `/projects/{id}` | Детали проекта с сущностями |
| `POST`   | `/projects` | Создание проекта |
| `PUT`    | `/projects/{id}` | Обновление проекта / настройка auth |
| `DELETE` | `/projects/{id}` | Удаление проекта |
| `POST`   | `/projects/{id}/generate` | Генерация ZIP-архива |

### Сущности, Поля, Связи

| Метод | Маршрут | Описание |
|---|---|---|
| `GET`    | `/entities/project/{projectId}` | Сущности проекта |
| `POST`   | `/entities/project/{projectId}` | Создание сущности |
| `PUT`    | `/entities/{id}` | Редактирование сущности |
| `DELETE` | `/entities/{id}` | Удаление (FK → null, не каскад) |
| `POST`   | `/fields/entity/{entityId}` | Создание поля |
| `PUT`    | `/fields/{id}` | Редактирование поля |
| `DELETE` | `/fields/{id}` | Удаление поля |
| `POST`   | `/relationships` | Создание связи |
| `DELETE` | `/relationships/{id}` | Удаление связи |

---

## ⚙️ Генерация кода

### C# + PostgreSQL — Монолит

```
MyProject/
├── Models/
│   ├── Product.cs              # EF Core модель с аннотациями
│   └── Category.cs
├── Controllers/
│   ├── ProductController.cs    # CRUD + фильтрация + пагинация
│   └── AuthController.cs       # (если auth включён)
├── DTOs/
│   ├── ProductDto.cs           # Request / Response DTO
│   └── AuthDtos.cs
├── Data/
│   └── ApplicationDbContext.cs
├── Services/
│   ├── IAuthService.cs + AuthService.cs
│   └── ITokenService.cs + TokenService.cs
├── Middleware/
│   └── ErrorHandlerMiddleware.cs
├── Properties/
│   └── launchSettings.json
├── appsettings.json            # JWT, ConnectionStrings
├── Program.cs                  # DI, Swagger, Auth, EF
├── MyProject.csproj            # .NET 9.0 + NuGet-пакеты
├── Dockerfile                  # aspnet:9.0 · EXPOSE 8080
├── docker-compose.yml          # API + PostgreSQL
└── README.md
```

### Node.js + MongoDB — Монолит

```
MyProject/
├── src/
│   ├── models/
│   │   ├── Product.js          # Mongoose-схема с валидацией
│   │   └── User.js             # (если auth включён)
│   ├── controllers/
│   │   ├── productController.js
│   │   └── authController.js
│   ├── routes/
│   │   ├── productRoutes.js    # Swagger JSDoc-аннотации
│   │   └── authRoutes.js
│   ├── middleware/
│   │   ├── authMiddleware.js   # JWT verify
│   │   ├── roleMiddleware.js   # Ролевая защита
│   │   ├── errorHandler.js
│   │   └── notFound.js
│   ├── config/
│   │   ├── database.js
│   │   └── swagger.js
│   └── app.js
├── server.js
├── package.json                # Express, Mongoose, JWT, Swagger
├── .env.example
├── Dockerfile
├── docker-compose.yml
└── README.md
```

### Микросервисная архитектура

```
MyProject/
├── services/
│   ├── product-service/        # Сущности группы "product"
│   │   ├── src/
│   │   │   ├── models/
│   │   │   ├── controllers/
│   │   │   ├── routes/
│   │   │   ├── messaging/
│   │   │   │   ├── publisher.js    # Публикация событий в RabbitMQ
│   │   │   │   └── subscriber.js   # Подписка на события других сервисов
│   │   │   ├── config/
│   │   │   │   ├── database.js
│   │   │   │   └── swagger.js
│   │   │   └── app.js
│   │   ├── server.js
│   │   ├── package.json
│   │   └── Dockerfile
│   │
│   ├── order-service/          # Другая группа сервисов
│   │   └── ...
│   │
│   └── auth-service/           # Если включена аутентификация
│       ├── src/
│       │   ├── models/User.js
│       │   ├── controllers/authController.js
│       │   ├── routes/authRoutes.js
│       │   ├── messaging/
│       │   │   ├── publisher.js
│       │   │   └── subscriber.js
│       │   └── app.js
│       ├── server.js
│       ├── package.json        # Включает swagger-jsdoc
│       └── Dockerfile
│
├── docker-compose.yml          # Все сервисы + MongoDB×N + RabbitMQ
└── README.md
```

### Поддерживаемые типы данных

| Тип | C# | Node.js / MongoDB |
|---|---|---|
| `String` | `string` | `String` |
| `Integer` | `int` | `Number` |
| `Long` | `long` | `Number` |
| `Float` | `float` | `Number` |
| `Decimal` | `decimal` | `Number` (mongoose) |
| `Boolean` | `bool` | `Boolean` |
| `DateTime` | `DateTime` | `Date` |
| `Text` | `string [MaxLength(5000)]` | `String` |
| `Guid` | `Guid` | `String` (UUID) |
| `Relationship` | FK + Navigation | `ObjectId` (ref) |

---

## 🔑 Настройка окружения

### Backend — `appsettings.Development.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=codeforge;Username=postgres;Password=yourpassword"
  },
  "Jwt": {
    "Secret": "your-super-secret-key-min-32-chars",
    "Issuer": "CodeForgeAPI",
    "Audience": "CodeForgeUI",
    "ExpiryMinutes": 60
  },
  "Email": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "SenderEmail": "your-email@gmail.com",
    "SenderPassword": "your-app-password",
    "SenderName": "CodeForge"
  },
  "FileStorage": {
    "UploadPath": "wwwroot/avatars",
    "BaseUrl": "http://localhost:5123"
  }
}
```

### Frontend — `.env`

```env
VITE_API_URL=http://localhost:5123/api
VITE_IMG_URL=http://localhost:5123
```

---

## 🗄 База данных

### Схема (основные таблицы)

```
Users ──────────< Projects ──────────< Entities ──────────< Fields
  id               id                   id                   id
  email            userId               projectId            entityId
  passwordHash     name                 name                 name
  role             targetStack          description          dataType
  firstName        architectureType     serviceName          isRequired
  lastName         authConfig           displayOrder         isUnique
  twoFactorEnabled createdAt            createdAt            relatedEntityId
  avatarUrl                                                  relationshipType
  isDarkMode                                                 displayOrder

                              Relationships
                              id · sourceEntityId · targetEntityId
                              relationshipType · sourceFieldName
```

### Применение миграций

```bash
cd CodeForgeAPI
dotnet ef migrations add <Название>
dotnet ef database update
```

---

<div align="center">

<img src="https://media1.tenor.com/m/oKZVauJ1LWEAAAAd/anime-fern.gif" width="260" alt="fern" />

<h3>хорошего дня 😊</h3>

</div>

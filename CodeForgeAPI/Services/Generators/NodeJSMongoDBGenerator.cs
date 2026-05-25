using System.Text;
using CodeForgeAPI.Models;

namespace CodeForgeAPI.Services.Generators;

public class NodeJSMongoDBGenerator : ITemplateGenerator
{
    public Dictionary<string, string> Generate(Project project)
    {
        var files = new Dictionary<string, string>();
        var projectName = SanitizeName(project.Name);

        // Models (Mongoose schemas)
        foreach (var entity in project.Entities)
            files[$"{projectName}/src/models/{entity.Name}.js"] = GenerateModel(entity, project);

        // Controllers
        foreach (var entity in project.Entities)
            files[$"{projectName}/src/controllers/{LowerFirst(entity.Name)}Controller.js"] = GenerateController(entity, project);

        // Routes
        foreach (var entity in project.Entities)
            files[$"{projectName}/src/routes/{LowerFirst(entity.Name)}Routes.js"] = GenerateRoute(entity);

        // Middleware
        files[$"{projectName}/src/middleware/errorHandler.js"] = GenerateErrorHandler();
        files[$"{projectName}/src/middleware/notFound.js"] = GenerateNotFound();
        files[$"{projectName}/src/middleware/validate.js"] = GenerateValidateMiddleware();

        // DB config
        files[$"{projectName}/src/config/database.js"] = GenerateDbConfig();

        // app.js
        files[$"{projectName}/src/app.js"] = GenerateApp(project.Entities);

        // server.js
        files[$"{projectName}/server.js"] = GenerateServer();

        // package.json
        files[$"{projectName}/package.json"] = GeneratePackageJson(projectName);

        // .env.example
        files[$"{projectName}/.env.example"] = GenerateEnvExample(projectName);

        // .gitignore
        files[$"{projectName}/.gitignore"] = GenerateGitignore();

        // Dockerfile
        files[$"{projectName}/Dockerfile"] = GenerateDockerfile();

        // docker-compose.yml
        files["docker-compose.yml"] = GenerateDockerCompose(projectName);

        // README.md
        files["README.md"] = GenerateReadme(project, projectName);

        return files;
    }

    // ─────────────────────────── MODEL ───────────────────────────

    private string GenerateModel(Entity entity, Project project)
    {
        var sb = new StringBuilder();
        var name = entity.Name;

        sb.AppendLine("const mongoose = require('mongoose');");
        sb.AppendLine();
        sb.AppendLine($"const {name}Schema = new mongoose.Schema(");
        sb.AppendLine("  {");

        foreach (var field in entity.Fields.OrderBy(f => f.DisplayOrder))
        {
            if (field.IsPrimaryKey) continue; // MongoDB uses _id by default

            if (field.DataType == "Relationship")
            {
                if (!field.RelatedEntityId.HasValue) continue;
                var related = project.Entities.FirstOrDefault(e => e.Id == field.RelatedEntityId);
                if (related == null) continue;

                var fieldName = LowerFirst(field.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
                    ? field.Name[..^2]
                    : field.Name);

                if (field.RelationshipType == "ManyToMany")
                {
                    sb.AppendLine($"    {fieldName}s: [{{");
                    sb.AppendLine("      type: mongoose.Schema.Types.ObjectId,");
                    sb.AppendLine($"      ref: '{related.Name}',");
                    sb.AppendLine("    }],");
                }
                else
                {
                    sb.AppendLine($"    {fieldName}: {{");
                    sb.AppendLine("      type: mongoose.Schema.Types.ObjectId,");
                    sb.AppendLine($"      ref: '{related.Name}',");
                    if (field.IsRequired)
                        sb.AppendLine("      required: [true, 'This reference is required'],");
                    sb.AppendLine("    },");
                }
            }
            else
            {
                var jsFieldName = LowerFirst(field.Name);
                var mongoType = MapDataTypeToMongoose(field.DataType);

                sb.AppendLine($"    {jsFieldName}: {{");
                sb.AppendLine($"      type: {mongoType},");

                if (field.IsRequired)
                    sb.AppendLine($"      required: [true, '{field.Name} is required'],");

                if (field.IsUnique)
                    sb.AppendLine("      unique: true,");

                if (field.DataType is "String" or "Text")
                {
                    var maxLen = field.DataType == "Text" ? 5000 : 500;
                    sb.AppendLine($"      maxlength: [{maxLen}, '{field.Name} must be at most {maxLen} characters'],");
                    if (field.IsRequired)
                        sb.AppendLine($"      trim: true,");
                }

                if (field.DataType == "Integer" || field.DataType == "Float" || field.DataType == "Long" || field.DataType == "Decimal")
                {
                    sb.AppendLine("      // Add min/max validation if needed: min: 0, max: 999999");
                }

                if (field.DataType == "Boolean")
                    sb.AppendLine("      default: false,");

                sb.AppendLine("    },");
            }
        }

        sb.AppendLine("  },");
        sb.AppendLine("  {");
        sb.AppendLine("    timestamps: true, // adds createdAt + updatedAt automatically");
        sb.AppendLine("    versionKey: false,");
        sb.AppendLine("    toJSON: {");
        sb.AppendLine("      virtuals: true,");
        sb.AppendLine("      transform: (_doc, ret) => {");
        sb.AppendLine("        ret.id = ret._id;");
        sb.AppendLine("        delete ret._id;");
        sb.AppendLine("        return ret;");
        sb.AppendLine("      },");
        sb.AppendLine("    },");
        sb.AppendLine("  }");
        sb.AppendLine(");");
        sb.AppendLine();

        // Virtuals or indexes
        var uniqueFields = entity.Fields.Where(f => f.IsUnique && f.DataType != "Relationship").ToList();
        if (uniqueFields.Count > 0)
        {
            foreach (var uf in uniqueFields)
                sb.AppendLine($"// Unique index already defined via schema; additional compound indexes can go here");
        }

        sb.AppendLine($"const {name} = mongoose.model('{name}', {name}Schema);");
        sb.AppendLine();
        sb.AppendLine($"module.exports = {name};");

        return sb.ToString();
    }

    // ─────────────────────────── CONTROLLER ───────────────────────────

    private string GenerateController(Entity entity, Project project)
    {
        var sb = new StringBuilder();
        var name = entity.Name;
        var nameLower = LowerFirst(name);
        var namePlural = Pluralize(name);
        var namePluralLower = LowerFirst(namePlural);

        // Collect populate paths
        var populatePaths = new List<string>();
        foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship"))
        {
            if (!field.RelatedEntityId.HasValue) continue;
            var related = project.Entities.FirstOrDefault(e => e.Id == field.RelatedEntityId);
            if (related == null) continue;

            var fieldName = LowerFirst(field.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
                ? field.Name[..^2]
                : field.Name);

            if (field.RelationshipType == "ManyToMany")
                populatePaths.Add($"'{fieldName}s'");
            else
                populatePaths.Add($"'{fieldName}'");
        }

        // Build populate chain
        string PopulateChain(string baseExpr) =>
            populatePaths.Count == 0
                ? baseExpr
                : populatePaths.Aggregate(baseExpr, (acc, p) => $"{acc}\n    .populate({p})");

        sb.AppendLine($"const {name} = require('../models/{name}');");
        sb.AppendLine();

        // ── getAll ──
        sb.AppendLine($"// @desc  Get all {namePluralLower}");
        sb.AppendLine($"// @route GET /api/{namePluralLower}");
        sb.AppendLine($"// @access Public");
        sb.AppendLine($"const getAll{namePlural} = async (req, res, next) => {{");
        sb.AppendLine("  try {");
        sb.AppendLine("    const page = Math.max(1, parseInt(req.query.page) || 1);");
        sb.AppendLine("    const limit = Math.min(100, parseInt(req.query.limit) || 20);");
        sb.AppendLine("    const skip = (page - 1) * limit;");
        sb.AppendLine();
        sb.AppendLine($"    const [items, total] = await Promise.all([");
        sb.AppendLine($"      {PopulateChain($"{name}.find()")}.sort({{ createdAt: -1 }}).skip(skip).limit(limit),");
        sb.AppendLine($"      {name}.countDocuments(),");
        sb.AppendLine("    ]);");
        sb.AppendLine();
        sb.AppendLine("    res.set('X-Total-Count', total);");
        sb.AppendLine("    res.set('X-Page', page);");
        sb.AppendLine("    res.set('X-Page-Size', limit);");
        sb.AppendLine($"    res.json({{ data: items, total, page, limit }});");
        sb.AppendLine("  } catch (err) {");
        sb.AppendLine("    next(err);");
        sb.AppendLine("  }");
        sb.AppendLine("};");
        sb.AppendLine();

        // ── getById ──
        sb.AppendLine($"// @desc  Get {nameLower} by ID");
        sb.AppendLine($"// @route GET /api/{namePluralLower}/:id");
        sb.AppendLine($"const get{name}ById = async (req, res, next) => {{");
        sb.AppendLine("  try {");
        sb.AppendLine($"    const {nameLower} = await {PopulateChain($"{name}.findById(req.params.id)")};");
        sb.AppendLine($"    if (!{nameLower}) {{");
        sb.AppendLine($"      return res.status(404).json({{ message: '{name} not found' }});");
        sb.AppendLine("    }");
        sb.AppendLine($"    res.json({nameLower});");
        sb.AppendLine("  } catch (err) {");
        sb.AppendLine("    if (err.name === 'CastError') {");
        sb.AppendLine($"      return res.status(400).json({{ message: 'Invalid ID format' }});");
        sb.AppendLine("    }");
        sb.AppendLine("    next(err);");
        sb.AppendLine("  }");
        sb.AppendLine("};");
        sb.AppendLine();

        // ── create ──
        sb.AppendLine($"// @desc  Create {nameLower}");
        sb.AppendLine($"// @route POST /api/{namePluralLower}");
        sb.AppendLine($"const create{name} = async (req, res, next) => {{");
        sb.AppendLine("  try {");
        sb.AppendLine($"    const {nameLower} = new {name}(req.body);");
        sb.AppendLine($"    const saved = await {nameLower}.save();");
        sb.AppendLine($"    res.status(201).json(saved);");
        sb.AppendLine("  } catch (err) {");
        sb.AppendLine("    if (err.name === 'ValidationError') {");
        sb.AppendLine("      const messages = Object.values(err.errors).map(e => e.message);");
        sb.AppendLine("      return res.status(400).json({ message: 'Validation failed', errors: messages });");
        sb.AppendLine("    }");
        sb.AppendLine("    if (err.code === 11000) {");
        sb.AppendLine("      const field = Object.keys(err.keyPattern)[0];");
        sb.AppendLine("      return res.status(409).json({ message: `${field} already exists` });");
        sb.AppendLine("    }");
        sb.AppendLine("    next(err);");
        sb.AppendLine("  }");
        sb.AppendLine("};");
        sb.AppendLine();

        // ── update ──
        sb.AppendLine($"// @desc  Update {nameLower}");
        sb.AppendLine($"// @route PUT /api/{namePluralLower}/:id");
        sb.AppendLine($"const update{name} = async (req, res, next) => {{");
        sb.AppendLine("  try {");
        sb.AppendLine($"    const {nameLower} = await {name}.findByIdAndUpdate(");
        sb.AppendLine("      req.params.id,");
        sb.AppendLine("      req.body,");
        sb.AppendLine("      { new: true, runValidators: true }");
        sb.AppendLine("    );");
        sb.AppendLine($"    if (!{nameLower}) {{");
        sb.AppendLine($"      return res.status(404).json({{ message: '{name} not found' }});");
        sb.AppendLine("    }");
        sb.AppendLine($"    res.json({nameLower});");
        sb.AppendLine("  } catch (err) {");
        sb.AppendLine("    if (err.name === 'ValidationError') {");
        sb.AppendLine("      const messages = Object.values(err.errors).map(e => e.message);");
        sb.AppendLine("      return res.status(400).json({ message: 'Validation failed', errors: messages });");
        sb.AppendLine("    }");
        sb.AppendLine("    if (err.code === 11000) {");
        sb.AppendLine("      const field = Object.keys(err.keyPattern)[0];");
        sb.AppendLine("      return res.status(409).json({ message: `${field} already exists` });");
        sb.AppendLine("    }");
        sb.AppendLine("    if (err.name === 'CastError') {");
        sb.AppendLine("      return res.status(400).json({ message: 'Invalid ID format' });");
        sb.AppendLine("    }");
        sb.AppendLine("    next(err);");
        sb.AppendLine("  }");
        sb.AppendLine("};");
        sb.AppendLine();

        // ── delete ──
        sb.AppendLine($"// @desc  Delete {nameLower}");
        sb.AppendLine($"// @route DELETE /api/{namePluralLower}/:id");
        sb.AppendLine($"const delete{name} = async (req, res, next) => {{");
        sb.AppendLine("  try {");
        sb.AppendLine($"    const {nameLower} = await {name}.findByIdAndDelete(req.params.id);");
        sb.AppendLine($"    if (!{nameLower}) {{");
        sb.AppendLine($"      return res.status(404).json({{ message: '{name} not found' }});");
        sb.AppendLine("    }");
        sb.AppendLine("    res.status(204).send();");
        sb.AppendLine("  } catch (err) {");
        sb.AppendLine("    if (err.name === 'CastError') {");
        sb.AppendLine("      return res.status(400).json({ message: 'Invalid ID format' });");
        sb.AppendLine("    }");
        sb.AppendLine("    next(err);");
        sb.AppendLine("  }");
        sb.AppendLine("};");
        sb.AppendLine();

        // exports
        sb.AppendLine($"module.exports = {{");
        sb.AppendLine($"  getAll{namePlural},");
        sb.AppendLine($"  get{name}ById,");
        sb.AppendLine($"  create{name},");
        sb.AppendLine($"  update{name},");
        sb.AppendLine($"  delete{name},");
        sb.AppendLine("};");

        return sb.ToString();
    }

    // ─────────────────────────── ROUTES ───────────────────────────

    private string GenerateRoute(Entity entity)
    {
        var sb = new StringBuilder();
        var name = entity.Name;
        var nameLower = LowerFirst(name);
        var namePlural = Pluralize(name);

        sb.AppendLine("const express = require('express');");
        sb.AppendLine("const router = express.Router();");
        sb.AppendLine($"const {{");
        sb.AppendLine($"  getAll{namePlural},");
        sb.AppendLine($"  get{name}ById,");
        sb.AppendLine($"  create{name},");
        sb.AppendLine($"  update{name},");
        sb.AppendLine($"  delete{name},");
        sb.AppendLine($"}} = require('../controllers/{nameLower}Controller');");
        sb.AppendLine();
        sb.AppendLine($"router.route('/')");
        sb.AppendLine($"  .get(getAll{namePlural})");
        sb.AppendLine($"  .post(create{name});");
        sb.AppendLine();
        sb.AppendLine($"router.route('/:id')");
        sb.AppendLine($"  .get(get{name}ById)");
        sb.AppendLine($"  .put(update{name})");
        sb.AppendLine($"  .delete(delete{name});");
        sb.AppendLine();
        sb.AppendLine("module.exports = router;");

        return sb.ToString();
    }

    // ─────────────────────────── MIDDLEWARE ───────────────────────────

    private string GenerateErrorHandler()
    {
        return @"// Global error handler — must have 4 parameters (err, req, res, next)
// eslint-disable-next-line no-unused-vars
const errorHandler = (err, req, res, next) => {
  const statusCode = err.statusCode || 500;
  const message = err.message || 'Internal Server Error';

  console.error(`[ERROR] ${req.method} ${req.url} — ${message}`);
  if (process.env.NODE_ENV === 'development') {
    console.error(err.stack);
  }

  res.status(statusCode).json({
    message,
    ...(process.env.NODE_ENV === 'development' && { stack: err.stack }),
  });
};

module.exports = errorHandler;
";
    }

    private string GenerateNotFound()
    {
        return @"const notFound = (req, res) => {
  res.status(404).json({ message: `Route ${req.originalUrl} not found` });
};

module.exports = notFound;
";
    }

    private string GenerateValidateMiddleware()
    {
        return @"/**
 * Validate that required fields exist in req.body.
 * Usage: validate(['name', 'email'])
 */
const validate = (fields) => (req, res, next) => {
  const missing = fields.filter((f) => req.body[f] === undefined || req.body[f] === null || req.body[f] === '');
  if (missing.length > 0) {
    return res.status(400).json({
      message: 'Missing required fields',
      fields: missing,
    });
  }
  next();
};

module.exports = validate;
";
    }

    // ─────────────────────────── DB CONFIG ───────────────────────────

    private string GenerateDbConfig()
    {
        return @"const mongoose = require('mongoose');

const connectDB = async () => {
  const uri = process.env.MONGODB_URI || 'mongodb://localhost:27017/myappdb';

  try {
    const conn = await mongoose.connect(uri);
    console.log(`✅ MongoDB connected: ${conn.connection.host}`);
  } catch (error) {
    console.error(`❌ MongoDB connection error: ${error.message}`);
    process.exit(1);
  }
};

// Handle connection events
mongoose.connection.on('disconnected', () => {
  console.warn('⚠️  MongoDB disconnected. Reconnecting...');
});

mongoose.connection.on('error', (err) => {
  console.error('MongoDB error:', err);
});

module.exports = connectDB;
";
    }

    // ─────────────────────────── APP.JS ───────────────────────────

    private string GenerateApp(IEnumerable<Entity> entities)
    {
        var sb = new StringBuilder();

        sb.AppendLine("const express = require('express');");
        sb.AppendLine("const cors = require('cors');");
        sb.AppendLine("const helmet = require('helmet');");
        sb.AppendLine("const morgan = require('morgan');");
        sb.AppendLine("const notFound = require('./middleware/notFound');");
        sb.AppendLine("const errorHandler = require('./middleware/errorHandler');");
        sb.AppendLine();

        foreach (var entity in entities)
        {
            var nameLower = LowerFirst(entity.Name);
            sb.AppendLine($"const {nameLower}Routes = require('./routes/{nameLower}Routes');");
        }

        sb.AppendLine();
        sb.AppendLine("const app = express();");
        sb.AppendLine();
        sb.AppendLine("// ── Security & Parsing Middleware ───────────────────────────────────────────");
        sb.AppendLine("app.use(helmet());");
        sb.AppendLine("app.use(cors({");
        sb.AppendLine("  origin: process.env.ALLOWED_ORIGIN || '*',");
        sb.AppendLine("  methods: ['GET', 'POST', 'PUT', 'DELETE', 'PATCH', 'OPTIONS'],");
        sb.AppendLine("  allowedHeaders: ['Content-Type', 'Authorization'],");
        sb.AppendLine("  exposedHeaders: ['X-Total-Count', 'X-Page', 'X-Page-Size'],");
        sb.AppendLine("}));");
        sb.AppendLine("app.use(express.json({ limit: '10mb' }));");
        sb.AppendLine("app.use(express.urlencoded({ extended: true }));");
        sb.AppendLine();
        sb.AppendLine("if (process.env.NODE_ENV !== 'test') {");
        sb.AppendLine("  app.use(morgan(process.env.NODE_ENV === 'development' ? 'dev' : 'combined'));");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("// ── Health Check ────────────────────────────────────────────────────────────");
        sb.AppendLine("app.get('/health', (_req, res) => {");
        sb.AppendLine("  const mongoose = require('mongoose');");
        sb.AppendLine("  const dbState = mongoose.connection.readyState;");
        sb.AppendLine("  res.json({");
        sb.AppendLine("    status: 'ok',");
        sb.AppendLine("    timestamp: new Date().toISOString(),");
        sb.AppendLine("    db: dbState === 1 ? 'connected' : 'disconnected',");
        sb.AppendLine("  });");
        sb.AppendLine("});");
        sb.AppendLine();
        sb.AppendLine("// ── API Routes ──────────────────────────────────────────────────────────────");

        foreach (var entity in entities)
        {
            var nameLower = LowerFirst(entity.Name);
            var routeName = LowerFirst(Pluralize(entity.Name));
            sb.AppendLine($"app.use('/api/{routeName}', {nameLower}Routes);");
        }

        sb.AppendLine();
        sb.AppendLine("// ── 404 + Error Handlers ────────────────────────────────────────────────────");
        sb.AppendLine("app.use(notFound);");
        sb.AppendLine("app.use(errorHandler);");
        sb.AppendLine();
        sb.AppendLine("module.exports = app;");

        return sb.ToString();
    }

    // ─────────────────────────── SERVER.JS ───────────────────────────

    private string GenerateServer()
    {
        return @"require('dotenv').config();
const app = require('./src/app');
const connectDB = require('./src/config/database');

const PORT = process.env.PORT || 3000;

const start = async () => {
  await connectDB();

  const server = app.listen(PORT, () => {
    console.log(`🚀 Server running on http://localhost:${PORT}`);
    console.log(`   Environment: ${process.env.NODE_ENV || 'development'}`);
    console.log(`   Health check: http://localhost:${PORT}/health`);
  });

  // Graceful shutdown
  const shutdown = (signal) => {
    console.log(`\n${signal} received. Shutting down gracefully...`);
    server.close(() => {
      console.log('HTTP server closed.');
      process.exit(0);
    });
  };

  process.on('SIGTERM', () => shutdown('SIGTERM'));
  process.on('SIGINT', () => shutdown('SIGINT'));

  process.on('unhandledRejection', (reason) => {
    console.error('Unhandled Promise Rejection:', reason);
    server.close(() => process.exit(1));
  });
};

start();
";
    }

    // ─────────────────────────── PACKAGE.JSON ───────────────────────────

    private string GeneratePackageJson(string projectName)
    {
        return $@"{{
  ""name"": ""{projectName.ToLower()}"",
  ""version"": ""1.0.0"",
  ""description"": ""Generated Node.js + MongoDB backend"",
  ""main"": ""server.js"",
  ""scripts"": {{
    ""start"": ""node server.js"",
    ""dev"": ""nodemon server.js"",
    ""lint"": ""eslint src/""
  }},
  ""keywords"": [],
  ""author"": """",
  ""license"": ""ISC"",
  ""engines"": {{
    ""node"": "">=18.0.0""
  }},
  ""dependencies"": {{
    ""cors"": ""^2.8.5"",
    ""dotenv"": ""^16.4.5"",
    ""express"": ""^4.19.2"",
    ""helmet"": ""^7.1.0"",
    ""mongoose"": ""^8.5.0"",
    ""morgan"": ""^1.10.0""
  }},
  ""devDependencies"": {{
    ""nodemon"": ""^3.1.4""
  }}
}}
";
    }

    // ─────────────────────────── ENV / GITIGNORE ───────────────────────────

    private string GenerateEnvExample(string projectName)
    {
        return $@"# Server
PORT=3000
NODE_ENV=development

# MongoDB
MONGODB_URI=mongodb://localhost:27017/{projectName.ToLower()}db

# CORS — set to your frontend URL in production
ALLOWED_ORIGIN=*
";
    }

    private string GenerateGitignore()
    {
        return @"# Dependencies
node_modules/
.npm

# Environment
.env
.env.local
.env.*.local

# Logs
logs/
*.log
npm-debug.log*

# OS
.DS_Store
Thumbs.db

# IDE
.vscode/
.idea/
*.swp
*.swo

# Build output
dist/
build/
";
    }

    // ─────────────────────────── DOCKER ───────────────────────────

    private string GenerateDockerfile()
    {
        return @"FROM node:20-alpine AS base
WORKDIR /app

# Install dependencies first (layer cache optimization)
COPY package*.json ./
RUN npm ci --only=production

# Copy source
COPY . .

EXPOSE 3000

# Use non-root user for security
USER node

CMD [""node"", ""server.js""]
";
    }

    private string GenerateDockerCompose(string projectName)
    {
        var dbName = projectName.ToLower() + "db";
        return $@"version: '3.8'

services:
  mongodb:
    image: mongo:7-jammy
    restart: unless-stopped
    ports:
      - ""27017:27017""
    volumes:
      - mongodb_data:/data/db
    environment:
      MONGO_INITDB_DATABASE: {dbName}
    healthcheck:
      test: [""CMD"", ""mongosh"", ""--eval"", ""db.adminCommand('ping')""]
      interval: 10s
      timeout: 5s
      retries: 5

  api:
    build:
      context: ./{projectName}
      dockerfile: Dockerfile
    restart: unless-stopped
    ports:
      - ""3000:3000""
    environment:
      - NODE_ENV=production
      - PORT=3000
      - MONGODB_URI=mongodb://mongodb:27017/{dbName}
    depends_on:
      mongodb:
        condition: service_healthy

volumes:
  mongodb_data:
";
    }

    // ─────────────────────────── README ───────────────────────────

    private string GenerateReadme(Project project, string projectName)
    {
        var entityList = string.Join("\n", project.Entities.Select(e =>
            $"- **{e.Name}** — {e.Fields.Count} field(s): {string.Join(", ", e.Fields.Select(f => f.Name))}"));

        return $@"# {projectName}

> Generated with **CodeForge** — Node.js + Express + MongoDB Backend

## Tech Stack

- **Runtime**: Node.js 20 (LTS)
- **Framework**: Express 4
- **ODM**: Mongoose 8
- **Database**: MongoDB 7
- **Containerization**: Docker + Docker Compose

## Project Structure

```
{projectName}/
├── server.js            # Entry point — starts HTTP server
├── src/
│   ├── app.js           # Express app setup
│   ├── config/
│   │   └── database.js  # MongoDB connection
│   ├── models/          # Mongoose schemas
│   ├── controllers/     # Business logic
│   ├── routes/          # API route definitions
│   └── middleware/
│       ├── errorHandler.js
│       ├── notFound.js
│       └── validate.js
├── .env.example
├── Dockerfile
└── package.json
```

## Generated Entities

{entityList}

## Quick Start

### 🐳 Docker (recommended)

```bash
docker-compose up --build
```

- API: `http://localhost:3000`
- Health: `http://localhost:3000/health`

### 💻 Local Development

**Prerequisites**: Node.js 18+, MongoDB 7 (or Docker)

1. Install dependencies:
```bash
cd {projectName}
npm install
```

2. Copy and configure `.env`:
```bash
cp .env.example .env
# Edit .env with your settings
```

3. Start MongoDB (if not using Docker):
```bash
docker run -d -p 27017:27017 --name mongo mongo:7
```

4. Run dev server:
```bash
npm run dev
```

## API Endpoints

Each entity exposes full CRUD with pagination support:

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/{{entity}}?page=1&limit=20` | List (paginated) |
| GET | `/api/{{entity}}/:id` | Get by ID |
| POST | `/api/{{entity}}` | Create |
| PUT | `/api/{{entity}}/:id` | Update |
| DELETE | `/api/{{entity}}/:id` | Delete (returns 204) |

### Pagination Response

```json
{{
  ""data"": [...],
  ""total"": 42,
  ""page"": 1,
  ""limit"": 20
}}
```

Headers: `X-Total-Count`, `X-Page`, `X-Page-Size`

## Error Responses

| Status | Meaning |
|--------|---------|
| 400 | Validation error / Bad request |
| 404 | Resource not found |
| 409 | Duplicate key conflict |
| 500 | Internal server error |

## Notes

- `_id` is mapped to `id` in all JSON responses
- Mongoose `timestamps: true` automatically adds `createdAt` / `updatedAt`
- Duplicate unique field → `409 Conflict`
- Invalid ObjectId format → `400 Bad Request`
";
    }

    // ─────────────────────────── HELPERS ───────────────────────────

    private static readonly Dictionary<string, string> PluralMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Category", "categories" }, { "Entity", "entities" }, { "Property", "properties" },
        { "Story", "stories" }, { "City", "cities" }, { "Country", "countries" },
        { "Company", "companies" }, { "Activity", "activities" }, { "Library", "libraries" },
        { "Query", "queries" }, { "Policy", "policies" }, { "Reply", "replies" },
        { "Entry", "entries" }, { "Gallery", "galleries" }, { "Address", "addresses" },
        { "Status", "statuses" }, { "Bus", "buses" }, { "Box", "boxes" },
        { "Person", "people" }, { "Man", "men" }, { "Woman", "women" }, { "Child", "children" },
    };

    private string Pluralize(string name)
    {
        if (PluralMap.TryGetValue(name, out var p)) return p;
        if (name.EndsWith("y", StringComparison.OrdinalIgnoreCase))
            return name[..^1] + "ies";
        if (name.EndsWith("s") || name.EndsWith("x") || name.EndsWith("z") ||
            name.EndsWith("ch") || name.EndsWith("sh"))
            return name + "es";
        return name + "s";
    }

    private string MapDataTypeToMongoose(string dataType) => dataType switch
    {
        "String" => "String",
        "Integer" => "Number",
        "Float" => "Number",
        "Decimal" => "Number",
        "Long" => "Number",
        "Boolean" => "Boolean",
        "DateTime" => "Date",
        "Text" => "String",
        "Guid" => "String",
        _ => "String"
    };

    private string LowerFirst(string str) =>
        string.IsNullOrEmpty(str) ? str : char.ToLower(str[0]) + str[1..];

    private string SanitizeName(string name) =>
        string.IsNullOrWhiteSpace(name) ? "generated-project" :
        new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray())
            .Trim('-').ToLower();
}

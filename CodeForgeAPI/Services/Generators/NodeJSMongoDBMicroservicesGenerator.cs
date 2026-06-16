using System.Text;
using System.Text.Json;
using CodeForgeAPI.Models;

namespace CodeForgeAPI.Services.Generators;

/// <summary>
/// Generates a Node.js + MongoDB microservices project.
/// Each unique ServiceName groups entities into one independent Express service
/// with its own MongoDB database, communicating via RabbitMQ.
/// </summary>
public class NodeJSMongoDBMicroservicesGenerator : ITemplateGenerator
{
    public Dictionary<string, string> Generate(Project project)
    {
        var files = new Dictionary<string, string>();
        var projectName = SanitizeDirName(project.Name);

        // Parse auth config
        AuthConfig? auth = null;
        if (!string.IsNullOrEmpty(project.AuthConfig))
            auth = JsonSerializer.Deserialize<AuthConfig>(project.AuthConfig,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Group entities by service name (null ServiceName → entity name)
        var serviceGroups = project.Entities
            .GroupBy(e => string.IsNullOrWhiteSpace(e.ServiceName) ? e.Name : e.ServiceName)
            .ToList();

        int portBase = 3001;
        var serviceInfos = serviceGroups.Select((g, i) => new
        {
            ServiceName = g.Key,
            SafeName = SanitizeDirName(g.Key),
            PackageName = SanitizePackageName(g.Key),
            Port = portBase + i,
            DbName = SanitizePackageName(g.Key).Replace("-", "_") + "_db",
            Entities = g.ToList()
        }).ToList();

        // Generate files for each service
        foreach (var svc in serviceInfos)
        {
            var basePath = $"{projectName}/services/{svc.SafeName.ToLower()}-service";
            var allProjectEntities = project.Entities.ToList();

            // Models
            foreach (var entity in svc.Entities)
                files[$"{basePath}/src/models/{entity.Name}.js"] = GenerateModel(entity, allProjectEntities);

            // Controllers
            foreach (var entity in svc.Entities)
                files[$"{basePath}/src/controllers/{LowerFirst(entity.Name)}Controller.js"] =
                    GenerateController(entity, allProjectEntities);

            // Routes
            foreach (var entity in svc.Entities)
                files[$"{basePath}/src/routes/{LowerFirst(entity.Name)}Routes.js"] =
                    GenerateRoute(entity, auth);

            // Validation
            foreach (var entity in svc.Entities)
                files[$"{basePath}/src/validation/{LowerFirst(entity.Name)}Validation.js"] =
                    GenerateValidationRules(entity, allProjectEntities);

            // Messaging
            files[$"{basePath}/src/messaging/publisher.js"] =
                GeneratePublisher(svc.Entities);
            // Pass entity names from OTHER services so subscriber binds to entity-level routing keys (e.g. "orderItem.created")
            var otherEntityNames = serviceInfos
                .Where(s => s.SafeName != svc.SafeName)
                .SelectMany(s => s.Entities.Select(e => e.Name))
                .ToList();
            files[$"{basePath}/src/messaging/subscriber.js"] =
                GenerateSubscriber(svc.SafeName, svc.Entities, otherEntityNames);

            // Middleware
            files[$"{basePath}/src/middleware/errorHandler.js"] = GenerateErrorHandler();
            files[$"{basePath}/src/middleware/notFound.js"] = GenerateNotFound();
            files[$"{basePath}/src/middleware/asyncHandler.js"] = GenerateAsyncHandler();

            // Config
            files[$"{basePath}/src/config/database.js"] = GenerateDbConfig(svc.DbName);
            files[$"{basePath}/src/config/rabbitmq.js"] = GenerateRabbitMQConfig(svc.SafeName.ToLower());
            files[$"{basePath}/src/config/swagger.js"] = GenerateMicroserviceSwaggerConfig(svc.SafeName, svc.Port, svc.Entities, auth);

            // Utils
            files[$"{basePath}/src/utils/paginate.js"] = GeneratePaginateUtil();

            // App + Server
            files[$"{basePath}/src/app.js"] = GenerateApp(svc.Entities, svc.SafeName, auth);
            files[$"{basePath}/server.js"] = GenerateServer(svc.SafeName);

            // Package.json
            files[$"{basePath}/package.json"] = GeneratePackageJson(svc.PackageName + "-service", auth?.Enabled == true);

            // .env.example
            files[$"{basePath}/.env.example"] = GenerateEnvExample(svc.PackageName, svc.Port, svc.DbName, auth);

            // Dockerfile
            files[$"{basePath}/Dockerfile"] = GenerateDockerfile();

            // .gitignore
            files[$"{basePath}/.gitignore"] = GenerateGitignore();

        }

        // Dedicated auth-service (created separately instead of injecting into every service)
        (string SafeName, string PackageName, int Port, string DbName)? authSvc = null;
        if (auth?.Enabled == true)
        {
            int authPort = portBase + serviceInfos.Count;
            const string authSafeName = "Auth";
            const string authPackageName = "auth";
            const string authDbName = "auth_db";
            authSvc = (authSafeName, authPackageName, authPort, authDbName);
            GenerateAuthMicroserviceFiles(files, projectName, auth, authPort, authPackageName, authDbName);
        }

        // Root docker-compose.yml
        files[$"{projectName}/docker-compose.yml"] = GenerateDockerCompose(projectName, serviceInfos
            .Select(s => (s.SafeName, s.PackageName, s.Port, s.DbName))
            .ToList(), authSvc);

        // Root README.md
        files[$"{projectName}/README.md"] = GenerateReadme(project, projectName, serviceInfos
            .Select(s => (s.SafeName, s.Port, s.Entities.Select(e => e.Name).ToList()))
            .ToList(), authSvc.HasValue ? authSvc.Value.Port : null);

        return files;
    }

    // ─────────────────────────── MODEL ───────────────────────────

    private string GenerateModel(Entity entity, List<Entity> allEntities)
    {
        var sb = new StringBuilder();
        var name = entity.Name;

        sb.AppendLine("const mongoose = require('mongoose');");
        sb.AppendLine();
        sb.AppendLine($"const {name}Schema = new mongoose.Schema(");
        sb.AppendLine("  {");

        foreach (var field in entity.Fields.OrderBy(f => f.DisplayOrder))
        {
            if (field.IsPrimaryKey) continue;

            if (field.DataType == "Relationship")
            {
                if (!field.RelatedEntityId.HasValue) continue;
                var related = allEntities.FirstOrDefault(e => e.Id == field.RelatedEntityId);
                if (related == null) continue;

                // Check if related entity is in same service
                bool sameService = (entity.ServiceName ?? entity.Name) == (related.ServiceName ?? related.Name);

                var fieldName = LowerFirst(field.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
                    ? field.Name[..^2] : field.Name);

                if (sameService)
                {
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
                        if (field.IsRequired) sb.AppendLine($"      required: [true, '{field.Name} reference is required'],");
                        sb.AppendLine("    },");
                    }
                }
                else
                {
                    // Cross-service: store as string ID (no populate — resolved via events)
                    sb.AppendLine($"    // Cross-service reference to {related.Name} (resolved via RabbitMQ events)");
                    sb.AppendLine($"    {fieldName}Ref: {{");
                    sb.AppendLine("      type: String, // External service ID");
                    if (field.IsRequired) sb.AppendLine($"      required: [true, '{field.Name} reference is required'],");
                    sb.AppendLine("    },");
                }
            }
            else
            {
                var jsFieldName = LowerFirst(field.Name);
                var mongoType = MapDataTypeToMongoose(field.DataType);

                sb.AppendLine($"    {jsFieldName}: {{");
                sb.AppendLine($"      type: {mongoType},");
                if (field.IsRequired) sb.AppendLine($"      required: [true, '{field.Name} is required'],");
                if (field.IsUnique) sb.AppendLine("      unique: true,");
                if (field.DataType is "String" or "Text")
                {
                    var maxLen = field.DataType == "Text" ? 5000 : 500;
                    sb.AppendLine($"      maxlength: [{maxLen}, '{field.Name} must be at most {maxLen} characters'],");
                    if (field.IsRequired) sb.AppendLine("      trim: true,");
                }
                if (field.DataType == "Boolean") sb.AppendLine("      default: false,");
                sb.AppendLine("    },");
            }
        }

        sb.AppendLine("  },");
        sb.AppendLine("  { timestamps: true, versionKey: false, toJSON: { virtuals: true, transform: (_doc, ret) => { ret.id = ret._id; delete ret._id; return ret; } } }");
        sb.AppendLine(");");
        sb.AppendLine();
        sb.AppendLine($"const {name} = mongoose.model('{name}', {name}Schema);");
        sb.AppendLine($"module.exports = {name};");

        return sb.ToString();
    }

    // ─────────────────────────── CONTROLLER ───────────────────────────

    private string GenerateController(Entity entity, List<Entity> allEntities)
    {
        var sb = new StringBuilder();
        var name = entity.Name;
        var nameLower = LowerFirst(name);
        var namePlural = Pluralize(name);
        var namePluralLower = LowerFirst(namePlural);

        // Same-service populate paths only
        var populatePaths = entity.Fields
            .Where(f => f.DataType == "Relationship" && f.RelatedEntityId.HasValue)
            .Select(f => new { Field = f, Related = allEntities.FirstOrDefault(e => e.Id == f.RelatedEntityId) })
            .Where(x => x.Related != null && (entity.ServiceName ?? entity.Name) == (x.Related.ServiceName ?? x.Related.Name))
            .Select(x =>
            {
                var fn = LowerFirst(x.Field.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
                    ? x.Field.Name[..^2] : x.Field.Name);
                return x.Field.RelationshipType == "ManyToMany" ? $"'{fn}s'" : $"'{fn}'";
            })
            .ToList();

        sb.AppendLine($"const {name} = require('../models/{name}');");
        sb.AppendLine("const asyncHandler = require('../middleware/asyncHandler');");
        sb.AppendLine("const paginate = require('../utils/paginate');");
        sb.AppendLine("const { publish } = require('../messaging/publisher');");
        sb.AppendLine();

        // getAll
        sb.AppendLine($"const getAll{namePlural} = asyncHandler(async (req, res) => {{");
        sb.AppendLine("  const { page, limit, skip } = paginate(req.query);");
        sb.AppendLine($"  const [items, total] = await Promise.all([");
        sb.Append($"    {name}.find()");
        foreach (var p in populatePaths) sb.Append($".populate({p})");
        sb.AppendLine(".sort({ createdAt: -1 }).skip(skip).limit(limit),");
        sb.AppendLine($"    {name}.countDocuments(),");
        sb.AppendLine("  ]);");
        sb.AppendLine("  res.set('X-Total-Count', total);");
        sb.AppendLine($"  res.json({{ data: items, total, page, limit }});");
        sb.AppendLine("});");
        sb.AppendLine();

        // getById
        sb.AppendLine($"const get{name}ById = asyncHandler(async (req, res) => {{");
        sb.Append($"  const {nameLower} = await {name}.findById(req.params.id)");
        foreach (var p in populatePaths) sb.Append($".populate({p})");
        sb.AppendLine(";");
        sb.AppendLine($"  if (!{nameLower}) return res.status(404).json({{ message: '{name} not found' }});");
        sb.AppendLine($"  res.json({nameLower});");
        sb.AppendLine("});");
        sb.AppendLine();

        // create
        sb.AppendLine($"const create{name} = asyncHandler(async (req, res) => {{");
        sb.AppendLine($"  const {nameLower} = new {name}(req.body);");
        sb.AppendLine($"  const saved = await {nameLower}.save();");
        sb.AppendLine($"  await publish('{LowerFirst(name)}.created', saved.toJSON());");
        sb.AppendLine($"  res.status(201).json(saved);");
        sb.AppendLine("});");
        sb.AppendLine();

        // update
        sb.AppendLine($"const update{name} = asyncHandler(async (req, res) => {{");
        sb.AppendLine($"  const {nameLower} = await {name}.findByIdAndUpdate(req.params.id, req.body, {{ new: true, runValidators: true }});");
        sb.AppendLine($"  if (!{nameLower}) return res.status(404).json({{ message: '{name} not found' }});");
        sb.AppendLine($"  await publish('{LowerFirst(name)}.updated', {nameLower}.toJSON());");
        sb.AppendLine($"  res.json({nameLower});");
        sb.AppendLine("});");
        sb.AppendLine();

        // patch
        sb.AppendLine($"const patch{name} = asyncHandler(async (req, res) => {{");
        sb.AppendLine($"  const {nameLower} = await {name}.findByIdAndUpdate(req.params.id, {{ $set: req.body }}, {{ new: true, runValidators: true }});");
        sb.AppendLine($"  if (!{nameLower}) return res.status(404).json({{ message: '{name} not found' }});");
        sb.AppendLine($"  await publish('{LowerFirst(name)}.updated', {nameLower}.toJSON());");
        sb.AppendLine($"  res.json({nameLower});");
        sb.AppendLine("});");
        sb.AppendLine();

        // delete
        sb.AppendLine($"const delete{name} = asyncHandler(async (req, res) => {{");
        sb.AppendLine($"  const {nameLower} = await {name}.findByIdAndDelete(req.params.id);");
        sb.AppendLine($"  if (!{nameLower}) return res.status(404).json({{ message: '{name} not found' }});");
        sb.AppendLine($"  await publish('{LowerFirst(name)}.deleted', {{ id: req.params.id }});");
        sb.AppendLine("  res.status(204).send();");
        sb.AppendLine("});");
        sb.AppendLine();

        sb.AppendLine($"module.exports = {{ getAll{namePlural}, get{name}ById, create{name}, update{name}, patch{name}, delete{name} }};");

        return sb.ToString();
    }

    // ─────────────────────────── ROUTES ───────────────────────────

    private string GenerateRoute(Entity entity, AuthConfig? auth = null)
    {
        var sb = new StringBuilder();
        var name = entity.Name;
        var nameLower = LowerFirst(name);
        var namePlural = Pluralize(name);

        // Per-method auth helpers
        var nodeProt = auth?.Enabled == true ? auth.EntityProtection.GetValueOrDefault(name) : null;
        bool anyProt = nodeProt?.AnyProtected == true;
        string T(bool protect) => (auth?.Enabled == true && protect) ? "verifyToken, " : "";

        var secTag = (auth?.Enabled == true) ? "\n *     security:\n *       - bearerAuth: []" : "";
        var secNote = (auth?.Enabled == true) ? " Requires JWT token from auth-service." : "";

        sb.AppendLine("const express = require('express');");
        sb.AppendLine("const router = express.Router();");
        sb.AppendLine("const { validationResult } = require('express-validator');");
        if (auth?.Enabled == true && anyProt)
            sb.AppendLine("const { verifyToken } = require('../../../auth-service/src/middleware/authMiddleware');");
        sb.AppendLine($"const {{ getAll{namePlural}, get{name}ById, create{name}, update{name}, patch{name}, delete{name} }} = require('../controllers/{nameLower}Controller');");
        sb.AppendLine($"const {{ create{name}Rules, update{name}Rules }} = require('../validation/{nameLower}Validation');");
        sb.AppendLine();

        // Swagger annotations
        sb.AppendLine($"/**");
        sb.AppendLine($" * @swagger");
        sb.AppendLine($" * tags:");
        sb.AppendLine($" *   name: {namePlural}");
        sb.AppendLine($" *   description: CRUD operations for {name} entities");
        sb.AppendLine($" *");
        sb.AppendLine($" * components:");
        sb.AppendLine($" *   schemas:");
        sb.AppendLine($" *     {name}:");
        sb.AppendLine($" *       type: object");
        sb.AppendLine($" *       properties:");
        sb.AppendLine($" *         id:");
        sb.AppendLine($" *           type: string");
        sb.AppendLine($" *         createdAt:");
        sb.AppendLine($" *           type: string");
        sb.AppendLine($" *           format: date-time");
        sb.AppendLine($" *         updatedAt:");
        sb.AppendLine($" *           type: string");
        sb.AppendLine($" *           format: date-time");
        foreach (var field in entity.Fields.Where(f => !f.IsPrimaryKey && f.DataType != "Relationship").OrderBy(f => f.DisplayOrder))
        {
            var jsType = field.DataType switch { "Boolean" => "boolean", "Integer" or "Long" or "Float" or "Decimal" => "number", "DateTime" => "string", _ => "string" };
            var jsFormat = field.DataType == "DateTime" ? "\n *           format: date-time" : "";
            sb.AppendLine($" *         {LowerFirst(field.Name)}:");
            sb.AppendLine($" *           type: {jsType}{jsFormat}");
            if (field.IsRequired) sb.AppendLine($" *           description: Required");
        }
        sb.AppendLine($" */");
        sb.AppendLine();

        sb.AppendLine($"/**");
        sb.AppendLine($" * @swagger");
        sb.AppendLine($" * /api/{LowerFirst(namePlural)}:");
        sb.AppendLine($" *   get:");
        sb.AppendLine($" *     summary: Get all {namePlural} (paginated){secNote}");
        sb.AppendLine($" *     tags: [{namePlural}]");
        if (nodeProt?.Get == true) sb.AppendLine($" *     security:");
        if (nodeProt?.Get == true) sb.AppendLine($" *       - bearerAuth: []");
        sb.AppendLine($" *     parameters:");
        sb.AppendLine($" *       - in: query");
        sb.AppendLine($" *         name: page");
        sb.AppendLine($" *         schema:");
        sb.AppendLine($" *           type: integer");
        sb.AppendLine($" *           default: 1");
        sb.AppendLine($" *       - in: query");
        sb.AppendLine($" *         name: limit");
        sb.AppendLine($" *         schema:");
        sb.AppendLine($" *           type: integer");
        sb.AppendLine($" *           default: 20");
        sb.AppendLine($" *     responses:");
        sb.AppendLine($" *       200:");
        sb.AppendLine($" *         description: List of {namePlural}");
        sb.AppendLine($" *         headers:");
        sb.AppendLine($" *           X-Total-Count:");
        sb.AppendLine($" *             schema:");
        sb.AppendLine($" *               type: integer");
        sb.AppendLine($" *   post:");
        sb.AppendLine($" *     summary: Create a new {name}");
        sb.AppendLine($" *     tags: [{namePlural}]");
        if (nodeProt?.Post == true) sb.AppendLine($" *     security:");
        if (nodeProt?.Post == true) sb.AppendLine($" *       - bearerAuth: []");
        sb.AppendLine($" *     requestBody:");
        sb.AppendLine($" *       required: true");
        sb.AppendLine($" *       content:");
        sb.AppendLine($" *         application/json:");
        sb.AppendLine($" *           schema:");
        sb.AppendLine($" *             $ref: '#/components/schemas/{name}'");
        sb.AppendLine($" *     responses:");
        sb.AppendLine($" *       201:");
        sb.AppendLine($" *         description: {name} created");
        sb.AppendLine($" *       400:");
        sb.AppendLine($" *         description: Validation error");
        sb.AppendLine($" */");
        sb.AppendLine();

        sb.AppendLine($"/**");
        sb.AppendLine($" * @swagger");
        sb.AppendLine($" * /api/{LowerFirst(namePlural)}/{{id}}:");
        sb.AppendLine($" *   get:");
        sb.AppendLine($" *     summary: Get {name} by ID");
        sb.AppendLine($" *     tags: [{namePlural}]");
        if (nodeProt?.Get == true) sb.AppendLine($" *     security:");
        if (nodeProt?.Get == true) sb.AppendLine($" *       - bearerAuth: []");
        sb.AppendLine($" *     parameters:");
        sb.AppendLine($" *       - in: path");
        sb.AppendLine($" *         name: id");
        sb.AppendLine($" *         required: true");
        sb.AppendLine($" *         schema:");
        sb.AppendLine($" *           type: string");
        sb.AppendLine($" *     responses:");
        sb.AppendLine($" *       200:");
        sb.AppendLine($" *         description: {name} object");
        sb.AppendLine($" *       404:");
        sb.AppendLine($" *         description: {name} not found");
        sb.AppendLine($" *   put:");
        sb.AppendLine($" *     summary: Update {name} (full replace)");
        sb.AppendLine($" *     tags: [{namePlural}]");
        if (nodeProt?.Put == true) sb.AppendLine($" *     security:");
        if (nodeProt?.Put == true) sb.AppendLine($" *       - bearerAuth: []");
        sb.AppendLine($" *     parameters:");
        sb.AppendLine($" *       - in: path");
        sb.AppendLine($" *         name: id");
        sb.AppendLine($" *         required: true");
        sb.AppendLine($" *         schema:");
        sb.AppendLine($" *           type: string");
        sb.AppendLine($" *     responses:");
        sb.AppendLine($" *       200:");
        sb.AppendLine($" *         description: {name} updated");
        sb.AppendLine($" *   patch:");
        sb.AppendLine($" *     summary: Partially update {name}");
        sb.AppendLine($" *     tags: [{namePlural}]");
        if (nodeProt?.Patch == true) sb.AppendLine($" *     security:");
        if (nodeProt?.Patch == true) sb.AppendLine($" *       - bearerAuth: []");
        sb.AppendLine($" *     parameters:");
        sb.AppendLine($" *       - in: path");
        sb.AppendLine($" *         name: id");
        sb.AppendLine($" *         required: true");
        sb.AppendLine($" *         schema:");
        sb.AppendLine($" *           type: string");
        sb.AppendLine($" *     responses:");
        sb.AppendLine($" *       200:");
        sb.AppendLine($" *         description: {name} patched");
        sb.AppendLine($" *   delete:");
        sb.AppendLine($" *     summary: Delete {name}");
        sb.AppendLine($" *     tags: [{namePlural}]");
        if (nodeProt?.Delete == true) sb.AppendLine($" *     security:");
        if (nodeProt?.Delete == true) sb.AppendLine($" *       - bearerAuth: []");
        sb.AppendLine($" *     parameters:");
        sb.AppendLine($" *       - in: path");
        sb.AppendLine($" *         name: id");
        sb.AppendLine($" *         required: true");
        sb.AppendLine($" *         schema:");
        sb.AppendLine($" *           type: string");
        sb.AppendLine($" *     responses:");
        sb.AppendLine($" *       204:");
        sb.AppendLine($" *         description: {name} deleted");
        sb.AppendLine($" *       404:");
        sb.AppendLine($" *         description: {name} not found");
        sb.AppendLine($" */");
        sb.AppendLine();

        sb.AppendLine("const handleValidation = (req, res, next) => {");
        sb.AppendLine("  const errors = validationResult(req);");
        sb.AppendLine("  if (!errors.isEmpty()) return res.status(400).json({ message: 'Validation failed', errors: errors.array() });");
        sb.AppendLine("  next();");
        sb.AppendLine("};");
        sb.AppendLine();
        sb.AppendLine($"router.route('/')");
        sb.AppendLine($"  .get({T(nodeProt?.Get == true)}getAll{namePlural})");
        sb.AppendLine($"  .post({T(nodeProt?.Post == true)}create{name}Rules, handleValidation, create{name});");
        sb.AppendLine($"router.route('/:id')");
        sb.AppendLine($"  .get({T(nodeProt?.Get == true)}get{name}ById)");
        sb.AppendLine($"  .put({T(nodeProt?.Put == true)}update{name}Rules, handleValidation, update{name})");
        sb.AppendLine($"  .patch({T(nodeProt?.Patch == true)}update{name}Rules, handleValidation, patch{name})");
        sb.AppendLine($"  .delete({T(nodeProt?.Delete == true)}delete{name});");
        sb.AppendLine("module.exports = router;");

        return sb.ToString();
    }

    // ─────────────────────────── VALIDATION ───────────────────────────

    private string GenerateValidationRules(Entity entity, List<Entity> allEntities)
    {
        var sb = new StringBuilder();
        var name = entity.Name;

        sb.AppendLine("const { body } = require('express-validator');");
        sb.AppendLine();
        sb.AppendLine($"const create{name}Rules = [");
        foreach (var field in entity.Fields.Where(f => !f.IsPrimaryKey && f.DataType != "Relationship").OrderBy(f => f.DisplayOrder))
        {
            var jsName = LowerFirst(field.Name);
            var chainStart = field.IsRequired
                ? $"  body('{jsName}').notEmpty().withMessage('{field.Name} is required')"
                : $"  body('{jsName}').optional()";
            var chainEnd = field.DataType switch
            {
                "Integer" or "Long" => $"\n    .isInt().withMessage('{field.Name} must be an integer'),",
                "Float" or "Decimal" => $"\n    .isFloat().withMessage('{field.Name} must be a number'),",
                "Boolean" => $"\n    .isBoolean().withMessage('{field.Name} must be boolean'),",
                "DateTime" => $"\n    .isISO8601().toDate().withMessage('{field.Name} must be a valid date'),",
                "String" => $"\n    .isString().trim().isLength({{ max: 500 }}).withMessage('{field.Name} max 500 chars'),",
                "Text" => $"\n    .isString().trim().isLength({{ max: 5000 }}).withMessage('{field.Name} max 5000 chars'),",
                _ => ","
            };
            sb.AppendLine(chainStart + chainEnd);
        }
        sb.AppendLine("];");
        sb.AppendLine();
        sb.AppendLine($"const update{name}Rules = [");
        foreach (var field in entity.Fields.Where(f => !f.IsPrimaryKey && f.DataType != "Relationship").OrderBy(f => f.DisplayOrder))
        {
            var jsName = LowerFirst(field.Name);
            var chainEnd = field.DataType switch
            {
                "Integer" or "Long" => $"\n    .isInt().withMessage('{field.Name} must be an integer'),",
                "Float" or "Decimal" => $"\n    .isFloat().withMessage('{field.Name} must be a number'),",
                "Boolean" => $"\n    .isBoolean().withMessage('{field.Name} must be boolean'),",
                "DateTime" => $"\n    .isISO8601().toDate().withMessage('{field.Name} must be a valid date'),",
                "String" => $"\n    .isString().trim().isLength({{ max: 500 }}).withMessage('{field.Name} max 500 chars'),",
                "Text" => $"\n    .isString().trim().isLength({{ max: 5000 }}).withMessage('{field.Name} max 5000 chars'),",
                _ => ","
            };
            sb.AppendLine($"  body('{jsName}').optional()" + chainEnd);
        }
        sb.AppendLine("];");
        sb.AppendLine();
        sb.AppendLine($"module.exports = {{ create{name}Rules, update{name}Rules }};");

        return sb.ToString();
    }

    // ─────────────────────────── MESSAGING ───────────────────────────

    private string GeneratePublisher(List<Entity> entities)
    {
        var sb = new StringBuilder();
        sb.AppendLine("const amqp = require('amqplib');");
        sb.AppendLine();
        sb.AppendLine("let channel = null;");
        sb.AppendLine("const EXCHANGE = 'events';");
        sb.AppendLine();
        sb.AppendLine("async function connect() {");
        sb.AppendLine("  const url = process.env.RABBITMQ_URL || 'amqp://guest:guest@rabbitmq:5672';");
        sb.AppendLine("  try {");
        sb.AppendLine("    const conn = await amqp.connect(url);");
        sb.AppendLine("    channel = await conn.createChannel();");
        sb.AppendLine("    await channel.assertExchange(EXCHANGE, 'topic', { durable: true });");
        sb.AppendLine("    console.log('✅ RabbitMQ publisher connected');");
        sb.AppendLine("  } catch (err) {");
        sb.AppendLine("    console.error('❌ RabbitMQ publisher error:', err.message);");
        sb.AppendLine("    // Retry after 5s");
        sb.AppendLine("    setTimeout(connect, 5000);");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/**");
        sb.AppendLine(" * Publish an event to RabbitMQ.");
        sb.AppendLine(" * @param {string} routingKey  e.g. 'user.created'");
        sb.AppendLine(" * @param {object} payload      JSON-serializable object");
        sb.AppendLine(" */");
        sb.AppendLine("async function publish(routingKey, payload) {");
        sb.AppendLine("  if (!channel) {");
        sb.AppendLine("    console.warn(`[Publisher] Channel not ready, skipping event: ${routingKey}`);");
        sb.AppendLine("    return;");
        sb.AppendLine("  }");
        sb.AppendLine("  channel.publish(EXCHANGE, routingKey, Buffer.from(JSON.stringify(payload)), { persistent: true });");
        sb.AppendLine("  console.log(`[Publisher] Event published: ${routingKey}`);");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("module.exports = { connect, publish };");

        return sb.ToString();
    }

    // otherEntityNames: entity names from all other services (used as routing key prefixes)
    private string GenerateSubscriber(string serviceName, List<Entity> ownEntities, List<string> otherEntityNames)
    {
        var sb = new StringBuilder();

        sb.AppendLine("const amqp = require('amqplib');");
        sb.AppendLine();
        sb.AppendLine("const EXCHANGE = 'events';");
        sb.AppendLine($"const QUEUE = '{serviceName.ToLower()}-service-queue';");
        sb.AppendLine();

        // Binding keys: subscribe to entity-level events from other services
        var bindingKeys = new List<string>();
        foreach (var name in otherEntityNames)
            bindingKeys.AddRange(new[] { $"{LowerFirst(name)}.created", $"{LowerFirst(name)}.updated", $"{LowerFirst(name)}.deleted" });

        var bindingKeysStr = string.Join(", ", bindingKeys.Select(k => $"'{k}'"));

        sb.AppendLine("// Routing keys to subscribe to (events from other services)");
        sb.AppendLine($"const BINDING_KEYS = [{bindingKeysStr}];");
        sb.AppendLine();
        sb.AppendLine("async function start() {");
        sb.AppendLine("  const url = process.env.RABBITMQ_URL || 'amqp://guest:guest@rabbitmq:5672';");
        sb.AppendLine("  try {");
        sb.AppendLine("    const conn = await amqp.connect(url);");
        sb.AppendLine("    const channel = await conn.createChannel();");
        sb.AppendLine("    await channel.assertExchange(EXCHANGE, 'topic', { durable: true });");
        sb.AppendLine("    const q = await channel.assertQueue(QUEUE, { durable: true });");
        sb.AppendLine("    for (const key of BINDING_KEYS) {");
        sb.AppendLine("      await channel.bindQueue(q.queue, EXCHANGE, key);");
        sb.AppendLine("    }");
        sb.AppendLine("    console.log(`✅ RabbitMQ subscriber listening on queue: ${QUEUE}`);");
        sb.AppendLine("    channel.consume(q.queue, (msg) => {");
        sb.AppendLine("      if (!msg) return;");
        sb.AppendLine("      const key = msg.fields.routingKey;");
        sb.AppendLine("      let payload;");
        sb.AppendLine("      try { payload = JSON.parse(msg.content.toString()); } catch { payload = {}; }");
        sb.AppendLine("      console.log(`[Subscriber] Received event: ${key}`, payload);");
        sb.AppendLine("      handleEvent(key, payload);");
        sb.AppendLine("      channel.ack(msg);");
        sb.AppendLine("    });");
        sb.AppendLine("  } catch (err) {");
        sb.AppendLine("    console.error('❌ RabbitMQ subscriber error:', err.message);");
        sb.AppendLine("    setTimeout(start, 5000);");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/**");
        sb.AppendLine(" * Handle incoming cross-service events.");
        sb.AppendLine(" * Add your business logic here (e.g. cache updates, cascades).");
        sb.AppendLine(" */");
        sb.AppendLine("function handleEvent(routingKey, payload) {");
        if (otherEntityNames.Count == 0)
        {
            sb.AppendLine("  // No other services defined yet.");
        }
        else
        {
            sb.AppendLine("  switch (routingKey) {");
            foreach (var entityName in otherEntityNames)
            {
                sb.AppendLine($"    case '{LowerFirst(entityName)}.created':");
                sb.AppendLine($"      // TODO: handle {entityName} created event");
                sb.AppendLine("      break;");
                sb.AppendLine($"    case '{LowerFirst(entityName)}.updated':");
                sb.AppendLine($"      // TODO: handle {entityName} updated event");
                sb.AppendLine("      break;");
                sb.AppendLine($"    case '{LowerFirst(entityName)}.deleted':");
                sb.AppendLine($"      // TODO: handle {entityName} deleted event (e.g. remove references)");
                sb.AppendLine("      break;");
            }
            sb.AppendLine("    default:");
            sb.AppendLine("      console.log(`[Subscriber] Unhandled event: ${routingKey}`);");
            sb.AppendLine("  }");
        }
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("module.exports = { start };");

        return sb.ToString();
    }

    // ─────────────────────────── CONFIG ───────────────────────────

    private string GenerateDbConfig(string dbName)
    {
        return $@"const mongoose = require('mongoose');

const connectDB = async () => {{
  const uri = process.env.MONGODB_URI || 'mongodb://localhost:27017/{dbName}';
  try {{
    const conn = await mongoose.connect(uri);
    console.log(`✅ MongoDB connected: ${{conn.connection.host}}`);
  }} catch (error) {{
    console.error(`❌ MongoDB connection error: ${{error.message}}`);
    process.exit(1);
  }}
}};

mongoose.connection.on('disconnected', () => console.warn('⚠️ MongoDB disconnected'));
mongoose.connection.on('error', (err) => console.error('MongoDB error:', err.message));

module.exports = connectDB;
";
    }

    private string GenerateRabbitMQConfig(string serviceName)
    {
        return $@"/**
 * RabbitMQ connection factory for {serviceName}-service.
 * Used internally by publisher.js and subscriber.js.
 * Connection URL is configured via RABBITMQ_URL environment variable.
 */
const RABBITMQ_URL = process.env.RABBITMQ_URL || 'amqp://guest:guest@rabbitmq:5672';
module.exports = {{ RABBITMQ_URL }};
";
    }

    // ─────────────────────────── APP.JS ───────────────────────────

    private string GenerateApp(List<Entity> entities, string serviceName, AuthConfig? auth = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine("const express = require('express');");
        sb.AppendLine("const cors = require('cors');");
        sb.AppendLine("const helmet = require('helmet');");
        sb.AppendLine("const morgan = require('morgan');");
        if (auth?.Enabled == true) sb.AppendLine("const cookieParser = require('cookie-parser');");
        sb.AppendLine("const swaggerUi = require('swagger-ui-express');");
        sb.AppendLine("const swaggerSpec = require('./config/swagger');");
        sb.AppendLine("const notFound = require('./middleware/notFound');");
        sb.AppendLine("const errorHandler = require('./middleware/errorHandler');");
        sb.AppendLine();

        foreach (var entity in entities)
            sb.AppendLine($"const {LowerFirst(entity.Name)}Routes = require('./routes/{LowerFirst(entity.Name)}Routes');");

        sb.AppendLine();
        sb.AppendLine("const app = express();");
        sb.AppendLine();
        sb.AppendLine("app.use(helmet());");
        sb.AppendLine("app.use(cors({ origin: process.env.ALLOWED_ORIGIN || '*', methods: ['GET','POST','PUT','PATCH','DELETE','OPTIONS'], allowedHeaders: ['Content-Type','Authorization'] }));");
        sb.AppendLine("app.use(express.json({ limit: '10mb' }));");
        sb.AppendLine("app.use(express.urlencoded({ extended: true }));");
        if (auth?.Enabled == true) sb.AppendLine("app.use(cookieParser());");
        sb.AppendLine("if (process.env.NODE_ENV !== 'test') app.use(morgan('dev'));");
        sb.AppendLine();
        sb.AppendLine("app.get('/health', (_req, res) => {");
        sb.AppendLine("  const mongoose = require('mongoose');");
        sb.AppendLine($"  res.json({{ service: '{serviceName.ToLower()}-service', status: 'ok', db: mongoose.connection.readyState === 1 ? 'connected' : 'disconnected' }});");
        sb.AppendLine("});");
        sb.AppendLine();
        sb.AppendLine($"app.use('/api-docs', swaggerUi.serve, swaggerUi.setup(swaggerSpec, {{ customSiteTitle: '{serviceName} API Docs' }}));");
        sb.AppendLine($"app.get('/swagger.json', (_req, res) => res.json(swaggerSpec));");
        sb.AppendLine();
        foreach (var entity in entities)
            sb.AppendLine($"app.use('/api/{LowerFirst(Pluralize(entity.Name))}', {LowerFirst(entity.Name)}Routes);");
        sb.AppendLine();
        sb.AppendLine("app.use(notFound);");
        sb.AppendLine("app.use(errorHandler);");
        sb.AppendLine("module.exports = app;");

        return sb.ToString();
    }

    // ─────────────────────────── SERVER.JS ───────────────────────────

    private string GenerateServer(string serviceName)
    {
        return $@"require('dotenv').config();
const app = require('./src/app');
const connectDB = require('./src/config/database');
const {{ connect: connectPublisher }} = require('./src/messaging/publisher');
const {{ start: startSubscriber }} = require('./src/messaging/subscriber');

const PORT = process.env.PORT || 3000;

const start = async () => {{
  await connectDB();
  await connectPublisher();
  await startSubscriber();

  const server = app.listen(PORT, () => {{
    console.log(`🚀 {serviceName}-service running on http://localhost:${{PORT}}`);
    console.log(`   Health: http://localhost:${{PORT}}/health`);
  }});

  const shutdown = (signal) => {{
    console.log(`\n${{signal}} received. Shutting down...`);
    server.close(() => process.exit(0));
  }};
  process.on('SIGTERM', () => shutdown('SIGTERM'));
  process.on('SIGINT', () => shutdown('SIGINT'));
  process.on('unhandledRejection', (reason) => {{
    console.error('Unhandled rejection:', reason);
    server.close(() => process.exit(1));
  }});
}};

start();
";
    }

    // ─────────────────────────── PACKAGE.JSON ───────────────────────────

    private string GeneratePackageJson(string packageName, bool authEnabled)
    {
        var authDeps = authEnabled ? ",\n    \"bcryptjs\": \"^2.4.3\",\n    \"jsonwebtoken\": \"^9.0.2\",\n    \"cookie-parser\": \"^1.4.6\"" : "";
        return $@"{{
  ""name"": ""{packageName}"",
  ""version"": ""1.0.0"",
  ""description"": ""Generated Node.js + MongoDB microservice by CodeForge"",
  ""main"": ""server.js"",
  ""scripts"": {{
    ""start"": ""node server.js"",
    ""dev"": ""nodemon server.js""
  }},
  ""license"": ""ISC"",
  ""engines"": {{ ""node"": "">=18.0.0"" }},
  ""dependencies"": {{
    ""amqplib"": ""^0.10.4"",
    ""cors"": ""^2.8.5"",
    ""dotenv"": ""^16.4.5"",
    ""express"": ""^4.19.2"",
    ""express-validator"": ""^7.2.0"",
    ""helmet"": ""^7.1.0"",
    ""mongoose"": ""^8.5.0"",
    ""morgan"": ""^1.10.0"",
    ""swagger-jsdoc"": ""^6.2.8"",
    ""swagger-ui-express"": ""^5.0.1""{authDeps}
  }},
  ""devDependencies"": {{
    ""nodemon"": ""^3.1.4""
  }}
}}
";
    }

    // ─────────────────────────── ENV EXAMPLE ───────────────────────────

    private string GenerateEnvExample(string packageName, int port, string dbName, AuthConfig? auth)
    {
        var jwtSection = auth?.Enabled == true
            ? $"\n# JWT Auth\nJWT_SECRET=CHANGE_ME_USE_A_LONG_RANDOM_SECRET\nJWT_EXPIRES_IN={auth.TokenExpiryMinutes}m"
            : "";
        return $@"# Service
PORT={port}
NODE_ENV=development

# MongoDB (this service's own database)
MONGODB_URI=mongodb://localhost:27017/{dbName}

# RabbitMQ
RABBITMQ_URL=amqp://guest:guest@rabbitmq:5672

# CORS
ALLOWED_ORIGIN=*{jwtSection}
";
    }

    // ─────────────────────────── DOCKERFILE ───────────────────────────

    private string GenerateDockerfile()
    {
        return @"FROM node:20-alpine AS base
WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .
EXPOSE 3000
USER node
CMD [""node"", ""server.js""]
";
    }

    // ─────────────────────────── GITIGNORE ───────────────────────────

    private string GenerateGitignore()
    {
        return @"node_modules/
.env
.env.local
logs/
*.log
.DS_Store
coverage/
";
    }

    // ─────────────────────────── DOCKER-COMPOSE ───────────────────────────

    private string GenerateDockerCompose(string projectName,
        List<(string SafeName, string PackageName, int Port, string DbName)> services,
        (string SafeName, string PackageName, int Port, string DbName)? authService = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("version: '3.8'");
        sb.AppendLine();
        sb.AppendLine("services:");
        sb.AppendLine();
        sb.AppendLine("  # ── Message Broker ──────────────────────────────────────────────────────────");
        sb.AppendLine("  rabbitmq:");
        sb.AppendLine("    image: rabbitmq:3.13-management-alpine");
        sb.AppendLine("    restart: unless-stopped");
        sb.AppendLine("    ports:");
        sb.AppendLine("      - \"5672:5672\"   # AMQP");
        sb.AppendLine("      - \"15672:15672\" # Management UI (guest/guest)");
        sb.AppendLine("    volumes:");
        sb.AppendLine("      - rabbitmq_data:/var/lib/rabbitmq");
        sb.AppendLine("    healthcheck:");
        sb.AppendLine("      test: [\"CMD\", \"rabbitmq-diagnostics\", \"ping\"]");
        sb.AppendLine("      interval: 10s");
        sb.AppendLine("      timeout: 5s");
        sb.AppendLine("      retries: 10");
        sb.AppendLine();
        sb.AppendLine("  # ── Databases ────────────────────────────────────────────────────────────────");
        if (authService.HasValue)
        {
            sb.AppendLine($"  {authService.Value.DbName}:");
            sb.AppendLine("    image: mongo:7-jammy");
            sb.AppendLine("    restart: unless-stopped");
            sb.AppendLine($"    volumes:");
            sb.AppendLine($"      - {authService.Value.DbName}_data:/data/db");
            sb.AppendLine("    healthcheck:");
            sb.AppendLine("      test: [\"CMD\", \"mongosh\", \"--eval\", \"db.adminCommand('ping')\"]");
            sb.AppendLine("      interval: 10s");
            sb.AppendLine("      timeout: 5s");
            sb.AppendLine("      retries: 5");
            sb.AppendLine();
        }
        foreach (var svc in services)
        {
            sb.AppendLine($"  {svc.DbName}:");
            sb.AppendLine("    image: mongo:7-jammy");
            sb.AppendLine("    restart: unless-stopped");
            sb.AppendLine($"    volumes:");
            sb.AppendLine($"      - {svc.DbName}_data:/data/db");
            sb.AppendLine("    healthcheck:");
            sb.AppendLine("      test: [\"CMD\", \"mongosh\", \"--eval\", \"db.adminCommand('ping')\"]");
            sb.AppendLine("      interval: 10s");
            sb.AppendLine("      timeout: 5s");
            sb.AppendLine("      retries: 5");
            sb.AppendLine();
        }
        sb.AppendLine("  # ── Microservices ─────────────────────────────────────────────────────────────");
        if (authService.HasValue)
        {
            sb.AppendLine("  auth-service:");
            sb.AppendLine("    build:");
            sb.AppendLine("      context: ./services/auth-service");
            sb.AppendLine("      dockerfile: Dockerfile");
            sb.AppendLine("    restart: unless-stopped");
            sb.AppendLine($"    ports:");
            sb.AppendLine($"      - \"{authService.Value.Port}:{authService.Value.Port}\"");
            sb.AppendLine("    environment:");
            sb.AppendLine($"      - PORT={authService.Value.Port}");
            sb.AppendLine("      - NODE_ENV=production");
            sb.AppendLine($"      - MONGODB_URI=mongodb://{authService.Value.DbName}:27017/{authService.Value.DbName}");
            sb.AppendLine("      - RABBITMQ_URL=amqp://guest:guest@rabbitmq:5672");
            sb.AppendLine("      - JWT_SECRET=CHANGE_ME_USE_A_LONG_RANDOM_SECRET");
            sb.AppendLine("    depends_on:");
            sb.AppendLine($"      {authService.Value.DbName}:");
            sb.AppendLine("        condition: service_healthy");
            sb.AppendLine("      rabbitmq:");
            sb.AppendLine("        condition: service_healthy");
            sb.AppendLine();
        }
        foreach (var svc in services)
        {
            var svcDirName = svc.SafeName.ToLower() + "-service";
            sb.AppendLine($"  {svcDirName}:");
            sb.AppendLine($"    build:");
            sb.AppendLine($"      context: ./services/{svcDirName}");
            sb.AppendLine("      dockerfile: Dockerfile");
            sb.AppendLine("    restart: unless-stopped");
            sb.AppendLine($"    ports:");
            sb.AppendLine($"      - \"{svc.Port}:{svc.Port}\"");
            sb.AppendLine("    environment:");
            sb.AppendLine($"      - PORT={svc.Port}");
            sb.AppendLine("      - NODE_ENV=production");
            sb.AppendLine($"      - MONGODB_URI=mongodb://{svc.DbName}:27017/{svc.DbName}");
            sb.AppendLine("      - RABBITMQ_URL=amqp://guest:guest@rabbitmq:5672");
            if (authService.HasValue)
                sb.AppendLine("      - JWT_SECRET=CHANGE_ME_USE_A_LONG_RANDOM_SECRET");
            sb.AppendLine("    depends_on:");
            sb.AppendLine($"      {svc.DbName}:");
            sb.AppendLine("        condition: service_healthy");
            sb.AppendLine("      rabbitmq:");
            sb.AppendLine("        condition: service_healthy");
            if (authService.HasValue)
            {
                sb.AppendLine("      auth-service:");
                sb.AppendLine("        condition: service_started");
            }
            sb.AppendLine();
        }
        sb.AppendLine("volumes:");
        sb.AppendLine("  rabbitmq_data:");
        if (authService.HasValue) sb.AppendLine($"  {authService.Value.DbName}_data:");
        foreach (var svc in services)
            sb.AppendLine($"  {svc.DbName}_data:");

        return sb.ToString();
    }

    // ─────────────────────────── README ───────────────────────────

    private string GenerateReadme(Project project, string projectName,
        List<(string ServiceName, int Port, List<string> Entities)> services,
        int? authServicePort = null)
    {
        var authRow = authServicePort.HasValue
            ? $"| `auth-service` | `http://localhost:{authServicePort}` | User (register/login/me) |\n"
            : "";
        var svcTable = authRow + string.Join("\n", services.Select(s =>
            $"| `{s.ServiceName.ToLower()}-service` | `http://localhost:{s.Port}` | {string.Join(", ", s.Entities)} |"));

        var authNote = authServicePort.HasValue
            ? $"\n> **Auth Service** (`http://localhost:{authServicePort}/health`): centralized JWT authentication — handles register, login, and token issuance. All other services validate tokens using the shared `JWT_SECRET`."
            : "";

        var healthLinks = authServicePort.HasValue
            ? $"- Auth service health: http://localhost:{authServicePort}/health\n" +
              string.Join("\n", services.Select(s => $"- {s.ServiceName}-service health: http://localhost:{s.Port}/health"))
            : string.Join("\n", services.Select(s => $"- {s.ServiceName}-service health: http://localhost:{s.Port}/health"));

        return $@"# {projectName} — Microservices

> Generated with **CodeForge** — Node.js + MongoDB + RabbitMQ Microservices

## Architecture

Each service owns its own MongoDB database and communicates with other services via **RabbitMQ topic exchange**.{authNote}

```
┌─────────────────────────────────────────────────┐
│                  RabbitMQ                        │
│           (topic exchange: events)               │
└─────┬───────────────────────────┬───────────────┘
      │  events.*                 │  events.*
      ▼                           ▼
{string.Join("\n      ▲                           ▲\n", services.Select(s => $"┌────────────────┐\n│ {s.ServiceName.ToLower()}-service  │\n│ :port {s.Port}        │\n│ own MongoDB    │\n└────────────────┘"))}
```

## Services

| Service | URL | Entities |
|---------|-----|----------|
{svcTable}

## Quick Start

### 🐳 Docker (recommended)

```bash
cd {projectName}
docker-compose up --build
```

- RabbitMQ Management: http://localhost:15672 (guest / guest)
{healthLinks}

### 💻 Local Development

Run each service independently:

```bash
# In separate terminals:
{string.Join("\n", services.Select(s => $"cd services/{s.ServiceName.ToLower()}-service && cp .env.example .env && npm install && npm run dev"))}
```

## Messaging (RabbitMQ)

Each service publishes events on entity mutations:
- `<entity>.created` — when a record is created
- `<entity>.updated` — when a record is updated
- `<entity>.deleted` — when a record is deleted

Cross-service subscriptions are configured in `src/messaging/subscriber.js`.  
Add your business logic in the `handleEvent` function.

## Tech Stack

- **Runtime**: Node.js 20
- **Framework**: Express 4
- **ODM**: Mongoose 8
- **Database**: MongoDB 7 (one instance per service)
- **Message Broker**: RabbitMQ 3.13
- **Messaging Library**: amqplib
";
    }

    // ─────────────────────────── AUTH MICROSERVICE ───────────────────────────

    private void GenerateAuthMicroserviceFiles(
        Dictionary<string, string> files,
        string projectName, AuthConfig auth, int port, string packageName, string dbName)
    {
        var bp = $"{projectName}/services/auth-service";

        files[$"{bp}/src/models/User.js"] = GenerateAuthUserModel(auth);
        files[$"{bp}/src/controllers/authController.js"] = GenerateAuthMicroserviceController(auth);
        files[$"{bp}/src/routes/authRoutes.js"] = GenerateAuthRoutes();
        files[$"{bp}/src/middleware/authMiddleware.js"] = GenerateAuthMiddleware();
        files[$"{bp}/src/utils/generateTokens.js"] = GenerateTokensUtil(auth);
        if (auth.EnableRoles)
            files[$"{bp}/src/middleware/roleMiddleware.js"] = GenerateRoleMiddleware();
        files[$"{bp}/src/messaging/publisher.js"] = GeneratePublisher(new List<Entity>());
        files[$"{bp}/src/messaging/subscriber.js"] = GenerateAuthMicroserviceSubscriber();
        files[$"{bp}/src/middleware/errorHandler.js"] = GenerateErrorHandler();
        files[$"{bp}/src/middleware/notFound.js"] = GenerateNotFound();
        files[$"{bp}/src/middleware/asyncHandler.js"] = GenerateAsyncHandler();
        files[$"{bp}/src/config/database.js"] = GenerateDbConfig(dbName);
        files[$"{bp}/src/config/rabbitmq.js"] = GenerateRabbitMQConfig("auth");
        files[$"{bp}/src/config/swagger.js"] = GenerateAuthMicroserviceSwaggerConfig(port);
        files[$"{bp}/src/app.js"] = GenerateAuthMicroserviceApp(auth);
        files[$"{bp}/server.js"] = GenerateServer("Auth");
        files[$"{bp}/package.json"] = GenerateAuthMicroservicePackageJson(packageName);
        files[$"{bp}/.env.example"] = GenerateAuthMicroserviceEnvExample(port, dbName, auth);
        files[$"{bp}/Dockerfile"] = GenerateDockerfile();
        files[$"{bp}/.gitignore"] = GenerateGitignore();
    }

    private string GenerateAuthMicroserviceController(AuthConfig auth) =>
$@"const bcrypt = require('bcryptjs');
const User = require('../models/User');
const {{ generateAccessToken }} = require('../utils/generateTokens');
const asyncHandler = require('../middleware/asyncHandler');
const {{ publish }} = require('../messaging/publisher');

const register = asyncHandler(async (req, res) => {{
  const {{ email, password }} = req.body;
  const existing = await User.findOne({{ email }});
  if (existing) return res.status(409).json({{ message: 'Email already registered' }});
  const passwordHash = await bcrypt.hash(password, 12);
  const user = await User.create({{ email, passwordHash }});
  const token = generateAccessToken(user);
  await publish('user.registered', {{ id: user._id, email: user.email }});
  res.status(201).json({{ token, user }});
}});

const login = asyncHandler(async (req, res) => {{
  const {{ email, password }} = req.body;
  const user = await User.findOne({{ email }});
  if (!user || !(await bcrypt.compare(password, user.passwordHash)))
    return res.status(401).json({{ message: 'Invalid credentials' }});
  const token = generateAccessToken(user);
  await publish('user.loggedin', {{ id: user._id, email: user.email }});
  res.json({{ token, user }});
}});

const me = asyncHandler(async (req, res) => res.json(req.user));

module.exports = {{ register, login, me }};
";

    private string GenerateAuthMicroserviceSubscriber() =>
@"const amqp = require('amqplib');

const EXCHANGE = 'events';
const QUEUE = 'auth-service-queue';

// Add routing keys here to subscribe to events from other services
const BINDING_KEYS = [];

async function start() {
  if (BINDING_KEYS.length === 0) return; // nothing to subscribe to
  const url = process.env.RABBITMQ_URL || 'amqp://guest:guest@rabbitmq:5672';
  try {
    const conn = await amqp.connect(url);
    const channel = await conn.createChannel();
    await channel.assertExchange(EXCHANGE, 'topic', { durable: true });
    const q = await channel.assertQueue(QUEUE, { durable: true });
    for (const key of BINDING_KEYS) {
      await channel.bindQueue(q.queue, EXCHANGE, key);
    }
    console.log(`✅ Auth RabbitMQ subscriber listening on queue: ${QUEUE}`);
    channel.consume(q.queue, (msg) => {
      if (!msg) return;
      const key = msg.fields.routingKey;
      let payload;
      try { payload = JSON.parse(msg.content.toString()); } catch { payload = {}; }
      console.log(`[Auth Subscriber] Received event: ${key}`, payload);
      handleEvent(key, payload);
      channel.ack(msg);
    });
  } catch (err) {
    console.error('❌ Auth RabbitMQ subscriber error:', err.message);
    setTimeout(start, 5000);
  }
}

function handleEvent(routingKey, payload) {
  // Add handlers for events from other services here
  console.log(`[Auth] Unhandled event: ${routingKey}`);
}

module.exports = { start };
";

    private string GenerateAuthMicroserviceSwaggerConfig(int port) =>
$@"const swaggerJsdoc = require('swagger-jsdoc');

const options = {{
  definition: {{
    openapi: '3.0.0',
    info: {{
      title: 'Auth Service API',
      version: '1.0.0',
      description: 'Centralized authentication microservice generated by **CodeForge**.\n\n' +
        'Issues JWT tokens used by all other services in the system.\n\n' +
        '**Flow:** Register → Login → Use the returned `token` as `Authorization: Bearer <token>` in other services.',
    }},
    servers: [{{ url: `http://localhost:{port}` }}],
    components: {{
      securitySchemes: {{
        bearerAuth: {{
          type: 'http',
          scheme: 'bearer',
          bearerFormat: 'JWT',
          description: 'JWT token from POST /api/auth/login',
        }},
      }},
    }},
  }},
  apis: ['./src/routes/*.js'],
}};

const swaggerSpec = swaggerJsdoc(options);
module.exports = swaggerSpec;
";

    private string GenerateMicroserviceSwaggerConfig(string serviceName, int port, List<Entity> entities, AuthConfig? auth)
    {
        var entityList = string.Join(", ", entities.Select(e => $"'{LowerFirst(e.Name)}Routes.js'"));
        var authScheme = auth?.Enabled == true
            ? @"    components: {
      securitySchemes: {
        bearerAuth: {
          type: 'http',
          scheme: 'bearer',
          bearerFormat: 'JWT',
          description: 'JWT token issued by auth-service. Obtain via POST /api/auth/login on auth-service.',
        },
      },
    },"
            : "";
        return $@"const swaggerJsdoc = require('swagger-jsdoc');

const options = {{
  definition: {{
    openapi: '3.0.0',
    info: {{
      title: '{serviceName} Service API',
      version: '1.0.0',
      description: 'Microservice generated by **CodeForge**. Entities: {string.Join(", ", entities.Select(e => e.Name))}.',
    }},
    servers: [{{ url: 'http://localhost:{port}' }}],
{authScheme}
  }},
  apis: ['./src/routes/*.js'],
}};

const swaggerSpec = swaggerJsdoc(options);
module.exports = swaggerSpec;
";
    }

    private string GenerateAuthMicroserviceApp(AuthConfig auth)
    {
        var sb = new StringBuilder();
        sb.AppendLine("const express = require('express');");
        sb.AppendLine("const cors = require('cors');");
        sb.AppendLine("const helmet = require('helmet');");
        sb.AppendLine("const morgan = require('morgan');");
        sb.AppendLine("const cookieParser = require('cookie-parser');");
        sb.AppendLine("const swaggerUi = require('swagger-ui-express');");
        sb.AppendLine("const swaggerSpec = require('./config/swagger');");
        sb.AppendLine("const notFound = require('./middleware/notFound');");
        sb.AppendLine("const errorHandler = require('./middleware/errorHandler');");
        sb.AppendLine("const authRoutes = require('./routes/authRoutes');");
        sb.AppendLine();
        sb.AppendLine("const app = express();");
        sb.AppendLine();
        sb.AppendLine("app.use(helmet());");
        sb.AppendLine("app.use(cors({ origin: process.env.ALLOWED_ORIGIN || '*', methods: ['GET','POST','PUT','PATCH','DELETE','OPTIONS'], allowedHeaders: ['Content-Type','Authorization'] }));");
        sb.AppendLine("app.use(express.json({ limit: '10mb' }));");
        sb.AppendLine("app.use(express.urlencoded({ extended: true }));");
        sb.AppendLine("app.use(cookieParser());");
        sb.AppendLine("if (process.env.NODE_ENV !== 'test') app.use(morgan('dev'));");
        sb.AppendLine();
        sb.AppendLine("app.get('/health', (_req, res) => {");
        sb.AppendLine("  const mongoose = require('mongoose');");
        sb.AppendLine("  res.json({ service: 'auth-service', status: 'ok', db: mongoose.connection.readyState === 1 ? 'connected' : 'disconnected' });");
        sb.AppendLine("});");
        sb.AppendLine();
        sb.AppendLine("app.use('/api-docs', swaggerUi.serve, swaggerUi.setup(swaggerSpec, { customSiteTitle: 'Auth Service API Docs' }));");
        sb.AppendLine("app.get('/swagger.json', (_req, res) => res.json(swaggerSpec));");
        sb.AppendLine();
        sb.AppendLine("app.use('/api/auth', authRoutes);");
        sb.AppendLine();
        sb.AppendLine("app.use(notFound);");
        sb.AppendLine("app.use(errorHandler);");
        sb.AppendLine("module.exports = app;");
        return sb.ToString();
    }

    private string GenerateAuthMicroservicePackageJson(string packageName) =>
$@"{{
  ""name"": ""{packageName}-auth-service"",
  ""version"": ""1.0.0"",
  ""description"": ""Auth microservice generated by CodeForge"",
  ""main"": ""server.js"",
  ""scripts"": {{
    ""start"": ""node server.js"",
    ""dev"": ""nodemon server.js""
  }},
  ""license"": ""ISC"",
  ""engines"": {{ ""node"": "">=18.0.0"" }},
  ""dependencies"": {{
    ""amqplib"": ""^0.10.4"",
    ""bcryptjs"": ""^2.4.3"",
    ""cookie-parser"": ""^1.4.6"",
    ""cors"": ""^2.8.5"",
    ""dotenv"": ""^16.4.5"",
    ""express"": ""^4.19.2"",
    ""helmet"": ""^7.1.0"",
    ""jsonwebtoken"": ""^9.0.2"",
    ""mongoose"": ""^8.5.0"",
    ""morgan"": ""^1.10.0"",
    ""swagger-jsdoc"": ""^6.2.8"",
    ""swagger-ui-express"": ""^5.0.1""
  }},
  ""devDependencies"": {{
    ""nodemon"": ""^3.1.4"",
    ""eslint"": ""^8.57.0""
  }}
}}
";

    private string GenerateAuthMicroserviceEnvExample(int port, string dbName, AuthConfig auth) =>
$@"# Auth Service
PORT={port}
NODE_ENV=development

# MongoDB
MONGODB_URI=mongodb://localhost:27017/{dbName}

# RabbitMQ
RABBITMQ_URL=amqp://guest:guest@rabbitmq:5672

# JWT — use the SAME secret in all other services for token validation
JWT_SECRET=CHANGE_ME_USE_A_LONG_RANDOM_SECRET
JWT_EXPIRES_IN={auth.TokenExpiryMinutes}m

# CORS
ALLOWED_ORIGIN=*
";

    // ─────────────────────────── MIDDLEWARE ───────────────────────────

    private string GenerateAsyncHandler() =>
        @"const asyncHandler = (fn) => (req, res, next) =>
  Promise.resolve(fn(req, res, next)).catch((err) => {
    if (err.name === 'ValidationError') {
      const messages = Object.values(err.errors).map((e) => e.message);
      return res.status(400).json({ message: 'Validation failed', errors: messages });
    }
    if (err.code === 11000) return res.status(409).json({ message: 'Duplicate key conflict' });
    if (err.name === 'CastError') return res.status(400).json({ message: 'Invalid ID format' });
    next(err);
  });
module.exports = asyncHandler;
";

    private string GenerateErrorHandler() =>
        @"// eslint-disable-next-line no-unused-vars
const errorHandler = (err, req, res, next) => {
  const statusCode = err.statusCode || 500;
  console.error(`[ERROR] ${req.method} ${req.url} — ${err.message}`);
  res.status(statusCode).json({ message: err.message || 'Internal Server Error' });
};
module.exports = errorHandler;
";

    private string GenerateNotFound() =>
        @"const notFound = (req, res) => res.status(404).json({ message: `Route ${req.originalUrl} not found` });
module.exports = notFound;
";

    // ─────────────────────────── UTILS ───────────────────────────

    private string GeneratePaginateUtil() =>
        @"const paginate = (query = {}) => {
  const page = Math.max(1, parseInt(query.page, 10) || 1);
  const limit = Math.min(100, Math.max(1, parseInt(query.limit, 10) || 20));
  return { page, limit, skip: (page - 1) * limit };
};
module.exports = paginate;
";

    // ─────────────────────────── AUTH MODULE ───────────────────────────

    private string GenerateAuthUserModel(AuthConfig auth)
    {
        var roleField = auth.EnableRoles
            ? $"\n  role: {{ type: String, enum: [{string.Join(", ", auth.Roles.Select(r => $"'{r}'"))}], default: '{auth.Roles.FirstOrDefault() ?? "User"}' }},"
            : "";
        var emailField = auth.UserIdentifier is "email" or "both"
            ? "\n  email: { type: String, required: true, unique: true, lowercase: true, trim: true },"
            : "";

        return $@"const {{ Schema, model }} = require('mongoose');
const userSchema = new Schema(
  {{{emailField}
    passwordHash: {{ type: String, required: true }},{roleField}
  }},
  {{ timestamps: true }}
);
userSchema.set('toJSON', {{ transform: (_doc, ret) => {{ delete ret.passwordHash; return ret; }} }});
module.exports = model('User', userSchema);
";
    }

    private string GenerateAuthController(AuthConfig auth) =>
$@"const bcrypt = require('bcryptjs');
const User = require('../models/User');
const {{ generateAccessToken }} = require('../utils/generateTokens');
const asyncHandler = require('../middleware/asyncHandler');

const register = asyncHandler(async (req, res) => {{
  const {{ email, password }} = req.body;
  const existing = await User.findOne({{ email }});
  if (existing) return res.status(409).json({{ message: 'Email already registered' }});
  const passwordHash = await bcrypt.hash(password, 12);
  const user = await User.create({{ email, passwordHash }});
  const token = generateAccessToken(user);
  res.status(201).json({{ token, user }});
}});

const login = asyncHandler(async (req, res) => {{
  const {{ email, password }} = req.body;
  const user = await User.findOne({{ email }});
  if (!user || !(await bcrypt.compare(password, user.passwordHash)))
    return res.status(401).json({{ message: 'Invalid credentials' }});
  const token = generateAccessToken(user);
  res.json({{ token, user }});
}});

const me = asyncHandler(async (req, res) => {{
  const user = await User.findById(req.user.id).select('-passwordHash');
  if (!user) return res.status(404).json({{ message: 'User not found' }});
  res.json(user);
}});

module.exports = {{ register, login, me }};
";

    private string GenerateAuthRoutes() =>
@"const express = require('express');
const router = express.Router();
const { register, login, me } = require('../controllers/authController');
const { verifyToken } = require('../middleware/authMiddleware');

/**
 * @swagger
 * tags:
 *   name: Auth
 *   description: Authentication — register, login, and get current user
 *
 * components:
 *   schemas:
 *     RegisterRequest:
 *       type: object
 *       required: [email, password]
 *       properties:
 *         email:
 *           type: string
 *           format: email
 *           example: user@example.com
 *         password:
 *           type: string
 *           minLength: 6
 *           example: secret123
 *     LoginRequest:
 *       type: object
 *       required: [email, password]
 *       properties:
 *         email:
 *           type: string
 *           format: email
 *           example: user@example.com
 *         password:
 *           type: string
 *           example: secret123
 *     AuthResponse:
 *       type: object
 *       properties:
 *         token:
 *           type: string
 *           description: JWT access token — use as `Authorization: Bearer <token>`
 *         user:
 *           type: object
 *           properties:
 *             _id:
 *               type: string
 *             email:
 *               type: string
 */

/**
 * @swagger
 * /api/auth/register:
 *   post:
 *     summary: Register a new user
 *     tags: [Auth]
 *     requestBody:
 *       required: true
 *       content:
 *         application/json:
 *           schema:
 *             $ref: '#/components/schemas/RegisterRequest'
 *     responses:
 *       201:
 *         description: User created — returns JWT token
 *         content:
 *           application/json:
 *             schema:
 *               $ref: '#/components/schemas/AuthResponse'
 *       409:
 *         description: Email already registered
 */
router.post('/register', register);

/**
 * @swagger
 * /api/auth/login:
 *   post:
 *     summary: Login with email and password
 *     tags: [Auth]
 *     requestBody:
 *       required: true
 *       content:
 *         application/json:
 *           schema:
 *             $ref: '#/components/schemas/LoginRequest'
 *     responses:
 *       200:
 *         description: Login successful — returns JWT token
 *         content:
 *           application/json:
 *             schema:
 *               $ref: '#/components/schemas/AuthResponse'
 *       401:
 *         description: Invalid credentials
 */
router.post('/login', login);

/**
 * @swagger
 * /api/auth/me:
 *   get:
 *     summary: Get current authenticated user
 *     tags: [Auth]
 *     security:
 *       - bearerAuth: []
 *     responses:
 *       200:
 *         description: Current user object
 *       401:
 *         description: Unauthorized — missing or invalid token
 */
router.get('/me', verifyToken, me);

module.exports = router;
";

    private string GenerateAuthMiddleware() =>
@"const jwt = require('jsonwebtoken');

// Stateless JWT verification — читаем claims из payload без запроса к auth_db.
// Token содержит { sub, email, role }, выданный auth-service с тем же JWT_SECRET.
const verifyToken = (req, res, next) => {
  const authHeader = req.headers.authorization;
  if (!authHeader?.startsWith('Bearer ')) return res.status(401).json({ message: 'No token provided' });
  try {
    const payload = jwt.verify(authHeader.slice(7), process.env.JWT_SECRET);
    req.user = { id: payload.sub, email: payload.email, role: payload.role };
    next();
  } catch {
    res.status(401).json({ message: 'Invalid token' });
  }
};

module.exports = { verifyToken };
";

    private string GenerateTokensUtil(AuthConfig auth) =>
$@"const jwt = require('jsonwebtoken');
// Включаем role в payload — entity-сервисы читают роль из токена без обращения к БД
const generateAccessToken = (user) =>
  jwt.sign({{ sub: user._id, email: user.email, role: user.role ?? 'user' }}, process.env.JWT_SECRET, {{ expiresIn: '{auth.TokenExpiryMinutes}m' }});
module.exports = {{ generateAccessToken }};
";

    private string GenerateRoleMiddleware() =>
@"const requireRole = (...roles) => (req, res, next) => {
  if (!roles.includes(req.user?.role)) return res.status(403).json({ message: 'Forbidden' });
  next();
};
module.exports = { requireRole };
";

    // ─────────────────────────── HELPERS ───────────────────────────

    private static readonly Dictionary<string, string> PluralMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Category", "categories" }, { "Entity", "entities" }, { "Property", "properties" },
        { "Story", "stories" }, { "City", "cities" }, { "Country", "countries" },
        { "Company", "companies" }, { "Activity", "activities" }, { "Library", "libraries" },
        { "Query", "queries" }, { "Policy", "policies" }, { "Reply", "replies" },
        { "Entry", "entries" }, { "Person", "people" }, { "Status", "statuses" },
    };

    private string Pluralize(string name)
    {
        if (PluralMap.TryGetValue(name, out var p)) return p;
        if (name.EndsWith("y", StringComparison.OrdinalIgnoreCase)) return name[..^1] + "ies";
        if (name.EndsWith("s") || name.EndsWith("x") || name.EndsWith("ch") || name.EndsWith("sh")) return name + "es";
        return name + "s";
    }

    private string MapDataTypeToMongoose(string dataType) => dataType switch
    {
        "Integer" or "Float" or "Decimal" or "Long" => "Number",
        "Boolean" => "Boolean",
        "DateTime" => "Date",
        _ => "String"
    };

    private string LowerFirst(string str) =>
        string.IsNullOrEmpty(str) ? str : char.ToLower(str[0]) + str[1..];

    private string SanitizeDirName(string name) =>
        string.IsNullOrWhiteSpace(name) ? "GeneratedProject"
            : new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray())
                .TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');

    private string SanitizePackageName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "generated-project";
        return new string(name.Select(c => char.IsLetterOrDigit(c) ? char.ToLower(c) : '-').ToArray())
            .Trim('-').Replace("--", "-");
    }
}

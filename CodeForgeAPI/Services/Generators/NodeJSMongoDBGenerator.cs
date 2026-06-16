using System.Text;
using System.Text.Json;
using CodeForgeAPI.Models;

namespace CodeForgeAPI.Services.Generators;

public class NodeJSMongoDBGenerator : ITemplateGenerator
{
    public Dictionary<string, string> Generate(Project project)
    {
        var files = new Dictionary<string, string>();
        var projectName = SanitizeDirName(project.Name);   // PascalCase / safe dir name
        var packageName = SanitizePackageName(project.Name); // kebab-case for package.json

        // Parse auth config
        AuthConfig? auth = null;
        if (!string.IsNullOrEmpty(project.AuthConfig))
            auth = JsonSerializer.Deserialize<AuthConfig>(project.AuthConfig, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        bool authEnabled = auth?.Enabled == true;

        // Models (Mongoose schemas)
        foreach (var entity in project.Entities)
            files[$"{projectName}/src/models/{entity.Name}.js"] = GenerateModel(entity, project);

        // Controllers
        foreach (var entity in project.Entities)
            files[$"{projectName}/src/controllers/{LowerFirst(entity.Name)}Controller.js"] = GenerateController(entity, project);

        // Routes
        foreach (var entity in project.Entities)
            files[$"{projectName}/src/routes/{LowerFirst(entity.Name)}Routes.js"] = GenerateRoute(entity, auth);

        // Middleware
        files[$"{projectName}/src/middleware/errorHandler.js"] = GenerateErrorHandler();
        files[$"{projectName}/src/middleware/notFound.js"] = GenerateNotFound();
        files[$"{projectName}/src/middleware/validate.js"] = GenerateValidateMiddleware();
        files[$"{projectName}/src/middleware/asyncHandler.js"] = GenerateAsyncHandler();

        // Validation rules per entity
        foreach (var entity in project.Entities)
            files[$"{projectName}/src/validation/{LowerFirst(entity.Name)}Validation.js"] = GenerateValidationRules(entity, project);

        // DB config
        files[$"{projectName}/src/config/database.js"] = GenerateDbConfig();
        files[$"{projectName}/src/config/swagger.js"] = GenerateSwaggerConfig(projectName, project.Entities);

        // Utils
        files[$"{projectName}/src/utils/paginate.js"] = GeneratePaginateUtil();

        // app.js
        files[$"{projectName}/src/app.js"] = GenerateApp(project.Entities, auth);

        // server.js
        files[$"{projectName}/server.js"] = GenerateServer();

        // package.json
        files[$"{projectName}/package.json"] = GeneratePackageJson(packageName, authEnabled);

        // .env.example
        files[$"{projectName}/.env.example"] = GenerateEnvExample(packageName, auth);

        // .gitignore
        files[$"{projectName}/.gitignore"] = GenerateGitignore();

        // .eslintrc.js
        files[$"{projectName}/.eslintrc.js"] = GenerateEslintConfig();

        // jest.config.js
        files[$"{projectName}/jest.config.js"] = GenerateJestConfig();

        // Basic tests per entity
        foreach (var entity in project.Entities)
            files[$"{projectName}/__tests__/{LowerFirst(entity.Name)}.test.js"] = GenerateEntityTest(entity, project.Entities);

        // Dockerfile
        files[$"{projectName}/Dockerfile"] = GenerateDockerfile();

        // docker-compose.yml
        files[$"{projectName}/docker-compose.yml"] = GenerateDockerCompose(projectName, packageName);

        // README.md
        files[$"{projectName}/README.md"] = GenerateReadme(project, projectName, packageName);

        // ── Auth module ──────────────────────────────────────────────────────────
        if (authEnabled)
        {
            files[$"{projectName}/src/models/User.js"]                    = GenerateAuthUserModel(auth!);
            files[$"{projectName}/src/controllers/authController.js"]     = GenerateAuthController(auth!);
            files[$"{projectName}/src/routes/authRoutes.js"]              = GenerateAuthRoutes(auth!);
            files[$"{projectName}/src/middleware/authMiddleware.js"]      = GenerateAuthMiddleware();
            files[$"{projectName}/src/utils/generateTokens.js"]           = GenerateTokensUtil(auth!);
            if (auth!.EnableRoles)
                files[$"{projectName}/src/middleware/roleMiddleware.js"]  = GenerateRoleMiddleware();
        }

        return files;
    }

    // ─────────────────────────── MODEL ───────────────────────────

    private string GenerateModel(Entity entity, Project project)
    {
        var sb = new StringBuilder();
        var name = entity.Name;

        sb.AppendLine("const mongoose = require('mongoose');");
        sb.AppendLine();
        sb.AppendLine($"/** @typedef {{import('mongoose').Document}} Document */");
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
                        sb.AppendLine($"      required: [true, '{field.Name} reference is required'],");
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
                        sb.AppendLine("      trim: true,");
                }

                if (field.DataType is "Integer" or "Float" or "Long" or "Decimal")
                {
                    sb.AppendLine("      // Uncomment to add range validation: min: 0, max: 999999");
                }

                if (field.DataType == "Boolean")
                    sb.AppendLine("      default: false,");

                if (field.DataType == "File")
                {
                    sb.AppendLine("      // Stores the file path or URL (handle upload separately with multer/S3)");
                    sb.AppendLine("      default: null,");
                }

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
        sb.AppendLine("    toObject: { virtuals: true },");
        sb.AppendLine("  }");
        sb.AppendLine(");");
        sb.AppendLine();

        // Compound indexes
        var uniqueFields = entity.Fields.Where(f => f.IsUnique && f.DataType != "Relationship").ToList();
        foreach (var uf in uniqueFields)
            sb.AppendLine($"// Unique index on '{LowerFirst(uf.Name)}' is enforced via schema option above.");

        sb.AppendLine();
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

        // Collect populate paths as a simple list of strings (no newlines inside expressions)
        var populatePaths = new List<string>();
        foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship"))
        {
            if (!field.RelatedEntityId.HasValue) continue;
            var related = project.Entities.FirstOrDefault(e => e.Id == field.RelatedEntityId);
            if (related == null) continue;

            var fieldName = LowerFirst(field.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
                ? field.Name[..^2]
                : field.Name);

            populatePaths.Add(field.RelationshipType == "ManyToMany"
                ? $"'{fieldName}s'"
                : $"'{fieldName}'");
        }

        sb.AppendLine($"const {name} = require('../models/{name}');");
        sb.AppendLine($"const asyncHandler = require('../middleware/asyncHandler');");
        sb.AppendLine($"const paginate = require('../utils/paginate');");
        sb.AppendLine();

        // ── getAll ──
        sb.AppendLine($"// @desc  Get all {namePluralLower}");
        sb.AppendLine($"// @route GET /api/{namePluralLower}");
        sb.AppendLine($"// @access Public");
        sb.AppendLine($"const getAll{namePlural} = asyncHandler(async (req, res) => {{");
        sb.AppendLine("  const { page, limit, skip } = paginate(req.query);");
        sb.AppendLine();
        sb.AppendLine($"  const [items, total] = await Promise.all([");
        if (populatePaths.Count == 0)
        {
            sb.AppendLine($"    {name}.find()");
            sb.AppendLine("      .sort({ createdAt: -1 })");
            sb.AppendLine("      .skip(skip)");
            sb.AppendLine("      .limit(limit),");
        }
        else
        {
            sb.AppendLine($"    {name}.find()");
            foreach (var p in populatePaths)
                sb.AppendLine($"      .populate({p})");
            sb.AppendLine("      .sort({ createdAt: -1 })");
            sb.AppendLine("      .skip(skip)");
            sb.AppendLine("      .limit(limit),");
        }
        sb.AppendLine($"    {name}.countDocuments(),");
        sb.AppendLine("  ]);");
        sb.AppendLine();
        sb.AppendLine("  res.set('X-Total-Count', total);");
        sb.AppendLine("  res.set('X-Page', page);");
        sb.AppendLine("  res.set('X-Page-Size', limit);");
        sb.AppendLine($"  res.json({{ data: items, total, page, limit }});");
        sb.AppendLine("});");
        sb.AppendLine();

        // ── getById ──
        sb.AppendLine($"// @desc  Get {nameLower} by ID");
        sb.AppendLine($"// @route GET /api/{namePluralLower}/:id");
        sb.AppendLine($"const get{name}ById = asyncHandler(async (req, res) => {{");
        if (populatePaths.Count == 0)
        {
            sb.AppendLine($"  const {nameLower} = await {name}.findById(req.params.id);");
        }
        else
        {
            sb.AppendLine($"  const {nameLower} = await {name}.findById(req.params.id)");
            foreach (var p in populatePaths)
                sb.AppendLine($"    .populate({p});");
        }
        sb.AppendLine();
        sb.AppendLine($"  if (!{nameLower}) {{");
        sb.AppendLine($"    return res.status(404).json({{ message: '{name} not found' }});");
        sb.AppendLine("  }");
        sb.AppendLine($"  res.json({nameLower});");
        sb.AppendLine("});");
        sb.AppendLine();

        // ── create ──
        sb.AppendLine($"// @desc  Create {nameLower}");
        sb.AppendLine($"// @route POST /api/{namePluralLower}");
        sb.AppendLine($"const create{name} = asyncHandler(async (req, res) => {{");
        sb.AppendLine($"  const {nameLower} = new {name}(req.body);");
        sb.AppendLine($"  const saved = await {nameLower}.save();");
        sb.AppendLine($"  res.status(201).json(saved);");
        sb.AppendLine("});");
        sb.AppendLine();

        // ── update ──
        sb.AppendLine($"// @desc  Update {nameLower} (full replace)");
        sb.AppendLine($"// @route PUT /api/{namePluralLower}/:id");
        sb.AppendLine($"const update{name} = asyncHandler(async (req, res) => {{");
        sb.AppendLine($"  const {nameLower} = await {name}.findByIdAndUpdate(");
        sb.AppendLine("    req.params.id,");
        sb.AppendLine("    req.body,");
        sb.AppendLine("    { new: true, runValidators: true }");
        sb.AppendLine("  );");
        sb.AppendLine($"  if (!{nameLower}) {{");
        sb.AppendLine($"    return res.status(404).json({{ message: '{name} not found' }});");
        sb.AppendLine("  }");
        sb.AppendLine($"  res.json({nameLower});");
        sb.AppendLine("});");
        sb.AppendLine();

        // ── patch ──
        sb.AppendLine($"// @desc  Partially update {nameLower}");
        sb.AppendLine($"// @route PATCH /api/{namePluralLower}/:id");
        sb.AppendLine($"const patch{name} = asyncHandler(async (req, res) => {{");
        sb.AppendLine($"  const {nameLower} = await {name}.findByIdAndUpdate(");
        sb.AppendLine("    req.params.id,");
        sb.AppendLine("    { $set: req.body },");
        sb.AppendLine("    { new: true, runValidators: true }");
        sb.AppendLine("  );");
        sb.AppendLine($"  if (!{nameLower}) {{");
        sb.AppendLine($"    return res.status(404).json({{ message: '{name} not found' }});");
        sb.AppendLine("  }");
        sb.AppendLine($"  res.json({nameLower});");
        sb.AppendLine("});");
        sb.AppendLine();

        // ── delete ──
        sb.AppendLine($"// @desc  Delete {nameLower}");
        sb.AppendLine($"// @route DELETE /api/{namePluralLower}/:id");
        sb.AppendLine($"const delete{name} = asyncHandler(async (req, res) => {{");
        sb.AppendLine($"  const {nameLower} = await {name}.findByIdAndDelete(req.params.id);");
        sb.AppendLine($"  if (!{nameLower}) {{");
        sb.AppendLine($"    return res.status(404).json({{ message: '{name} not found' }});");
        sb.AppendLine("  }");
        sb.AppendLine("  res.status(204).send();");
        sb.AppendLine("});");
        sb.AppendLine();

        // exports
        sb.AppendLine($"module.exports = {{");
        sb.AppendLine($"  getAll{namePlural},");
        sb.AppendLine($"  get{name}ById,");
        sb.AppendLine($"  create{name},");
        sb.AppendLine($"  update{name},");
        sb.AppendLine($"  patch{name},");
        sb.AppendLine($"  delete{name},");
        sb.AppendLine("};");

        return sb.ToString();
    }

    // ─────────────────────────── ROUTES ───────────────────────────

    private string GenerateRoute(Entity entity, AuthConfig? auth = null)
    {
        var sb = new StringBuilder();
        var name = entity.Name;
        var nameLower = LowerFirst(name);
        var namePlural = Pluralize(name);
        var namePluralLower = LowerFirst(namePlural);

        // Required fields for swagger
        var requiredScalarFields = entity.Fields
            .Where(f => f.IsRequired && !f.IsPrimaryKey && f.DataType != "Relationship")
            .Select(f => LowerFirst(f.Name))
            .ToList();

        sb.AppendLine("const express = require('express');");
        sb.AppendLine("const router = express.Router();");
        sb.AppendLine("const { validationResult } = require('express-validator');");
        if (auth?.Enabled == true)
            sb.AppendLine("const { verifyToken } = require('../middleware/authMiddleware');");
        sb.AppendLine($"const {{");
        sb.AppendLine($"  getAll{namePlural},");
        sb.AppendLine($"  get{name}ById,");
        sb.AppendLine($"  create{name},");
        sb.AppendLine($"  update{name},");
        sb.AppendLine($"  patch{name},");
        sb.AppendLine($"  delete{name},");
        sb.AppendLine($"}} = require('../controllers/{nameLower}Controller');");
        sb.AppendLine($"const {{ create{name}Rules, update{name}Rules }} = require('../validation/{nameLower}Validation');");
        sb.AppendLine();
        sb.AppendLine("/** Inline validation error handler */");
        sb.AppendLine("const handleValidation = (req, res, next) => {");
        sb.AppendLine("  const errors = validationResult(req);");
        sb.AppendLine("  if (!errors.isEmpty()) {");
        sb.AppendLine("    return res.status(400).json({ message: 'Validation failed', errors: errors.array() });");
        sb.AppendLine("  }");
        sb.AppendLine("  next();");
        sb.AppendLine("};");
        sb.AppendLine();

        // JSDoc swagger annotation
        sb.AppendLine("/**");
        sb.AppendLine($" * @swagger");
        sb.AppendLine($" * tags:");
        sb.AppendLine($" *   name: {namePlural}");
        sb.AppendLine($" *   description: {name} management");
        sb.AppendLine(" */");
        sb.AppendLine();

        sb.AppendLine("/**");
        sb.AppendLine($" * @swagger");
        sb.AppendLine($" * /api/{namePluralLower}:");
        sb.AppendLine($" *   get:");
        sb.AppendLine($" *     summary: Get all {namePluralLower}");
        sb.AppendLine($" *     tags: [{namePlural}]");
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
        sb.AppendLine($" *         description: Paginated list of {namePluralLower}");
        sb.AppendLine($" *   post:");
        sb.AppendLine($" *     summary: Create a new {nameLower}");
        sb.AppendLine($" *     tags: [{namePlural}]");
        sb.AppendLine($" *     requestBody:");
        sb.AppendLine($" *       required: true");
        sb.AppendLine($" *       content:");
        sb.AppendLine($" *         application/json:");
        sb.AppendLine($" *           schema:");
        sb.AppendLine($" *             type: object");
        if (requiredScalarFields.Any())
        {
            sb.AppendLine($" *             required: [{string.Join(", ", requiredScalarFields.Select(f => $"'{f}'"))}]");
        }
        sb.AppendLine($" *     responses:");
        sb.AppendLine($" *       201:");
        sb.AppendLine($" *         description: Created {nameLower}");
        sb.AppendLine($" *       400:");
        sb.AppendLine($" *         description: Validation error");
        sb.AppendLine(" */");
        // Per-method auth helpers for Node.js
        var nodeProt = auth?.Enabled == true ? auth.EntityProtection.GetValueOrDefault(name) : null;
        string T(bool protect) => (auth?.Enabled == true && protect) ? "verifyToken, " : "";

        var protect = T(nodeProt?.Get == true || nodeProt?.Post == true);
        sb.AppendLine($"router.route('/')");
        sb.AppendLine($"  .get({T(nodeProt?.Get == true)}getAll{namePlural})");
        sb.AppendLine($"  .post({T(nodeProt?.Post == true)}create{name}Rules, handleValidation, create{name});");
        sb.AppendLine();

        sb.AppendLine("/**");
        sb.AppendLine($" * @swagger");
        sb.AppendLine($" * /api/{namePluralLower}/{{id}}:");
        sb.AppendLine($" *   get:");
        sb.AppendLine($" *     summary: Get {nameLower} by ID");
        sb.AppendLine($" *     tags: [{namePlural}]");
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
        sb.AppendLine($" *         description: Not found");
        sb.AppendLine(" */");
        sb.AppendLine($"router.route('/:id')");
        sb.AppendLine($"  .get({T(nodeProt?.Get == true)}get{name}ById)");
        sb.AppendLine($"  .put({T(nodeProt?.Put == true)}update{name}Rules, handleValidation, update{name})");
        sb.AppendLine($"  .patch({T(nodeProt?.Patch == true)}update{name}Rules, handleValidation, patch{name})");
        sb.AppendLine($"  .delete({T(nodeProt?.Delete == true)}delete{name});");
        sb.AppendLine();
        sb.AppendLine("module.exports = router;");

        return sb.ToString();
    }

    // ─────────────────────────── VALIDATION ───────────────────────────

    private string GenerateValidationRules(Entity entity, Project project)
    {
        var sb = new StringBuilder();
        var name = entity.Name;

        sb.AppendLine("const { body } = require('express-validator');");
        sb.AppendLine();

        // Create rules
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
                "Boolean" => $"\n    .isBoolean().withMessage('{field.Name} must be a boolean'),",
                "DateTime" => $"\n    .isISO8601().toDate().withMessage('{field.Name} must be a valid date'),",
                "String" => $"\n    .isString().trim().isLength({{ max: 500 }}).withMessage('{field.Name} must be a string up to 500 chars'),",
                "Text" => $"\n    .isString().trim().isLength({{ max: 5000 }}).withMessage('{field.Name} must be text up to 5000 chars'),",
                "Guid" => $"\n    .isUUID().withMessage('{field.Name} must be a valid UUID'),",
                _ => ","
            };
            sb.AppendLine(chainStart + chainEnd);
        }
        // Relationship fields
        foreach (var field in entity.Fields.Where(f => f.DataType == "Relationship").OrderBy(f => f.DisplayOrder))
        {
            if (!field.RelatedEntityId.HasValue) continue;
            var related = project.Entities.FirstOrDefault(e => e.Id == field.RelatedEntityId);
            if (related == null) continue;

            var fieldName = LowerFirst(field.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
                ? field.Name[..^2]
                : field.Name);
            var keyName = field.RelationshipType == "ManyToMany" ? $"{fieldName}s" : fieldName;
            var rule = field.IsRequired
                ? $"  body('{keyName}').notEmpty().withMessage('{keyName} is required')"
                : $"  body('{keyName}').optional()";
            var m2m = field.RelationshipType == "ManyToMany";
            var mongoRule = m2m
                ? $"\n    .isArray().withMessage('{keyName} must be an array')\n    .custom((v) => v.every((id) => /^[0-9a-fA-F]{{24}}$/.test(id))).withMessage('{keyName} must be an array of valid ObjectIds'),"
                : $"\n    .isMongoId().withMessage('{keyName} must be a valid MongoDB ObjectId'),";
            sb.AppendLine(rule + mongoRule);
        }
        sb.AppendLine("];");
        sb.AppendLine();

        // Update rules (all optional)
        sb.AppendLine($"const update{name}Rules = [");
        foreach (var field in entity.Fields.Where(f => !f.IsPrimaryKey && f.DataType != "Relationship").OrderBy(f => f.DisplayOrder))
        {
            var jsName = LowerFirst(field.Name);
            var chainEnd = field.DataType switch
            {
                "Integer" or "Long" => $"\n    .isInt().withMessage('{field.Name} must be an integer'),",
                "Float" or "Decimal" => $"\n    .isFloat().withMessage('{field.Name} must be a number'),",
                "Boolean" => $"\n    .isBoolean().withMessage('{field.Name} must be a boolean'),",
                "DateTime" => $"\n    .isISO8601().toDate().withMessage('{field.Name} must be a valid date'),",
                "String" => $"\n    .isString().trim().isLength({{ max: 500 }}).withMessage('{field.Name} must be at most 500 chars'),",
                "Text" => $"\n    .isString().trim().isLength({{ max: 5000 }}).withMessage('{field.Name} must be text up to 5000 chars'),",
                "Guid" => $"\n    .isUUID().withMessage('{field.Name} must be a valid UUID'),",
                _ => ","
            };
            sb.AppendLine($"  body('{jsName}').optional()" + chainEnd);
        }
        sb.AppendLine("];");
        sb.AppendLine();

        sb.AppendLine($"module.exports = {{ create{name}Rules, update{name}Rules }};");

        return sb.ToString();
    }

    // ─────────────────────────── MIDDLEWARE ───────────────────────────

    private string GenerateAsyncHandler()
    {
        return @"/**
 * Wraps async route handlers to catch rejected promises and forward to Express error handler.
 * Eliminates the need for try/catch in every controller method.
 *
 * Usage:
 *   const asyncHandler = require('./asyncHandler');
 *   const myRoute = asyncHandler(async (req, res) => { ... });
 */
const asyncHandler = (fn) => (req, res, next) => {
  Promise.resolve(fn(req, res, next)).catch((err) => {
    // Handle known Mongoose errors before passing to global handler
    if (err.name === 'ValidationError') {
      const messages = Object.values(err.errors).map((e) => e.message);
      return res.status(400).json({ message: 'Validation failed', errors: messages });
    }
    if (err.code === 11000) {
      const field = Object.keys(err.keyPattern || {})[0] || 'field';
      return res.status(409).json({ message: `${field} already exists` });
    }
    if (err.name === 'CastError') {
      return res.status(400).json({ message: 'Invalid ID format' });
    }
    next(err);
  });
};

module.exports = asyncHandler;
";
    }

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
 * Legacy body-field presence check middleware (use express-validator rules in routes for new code).
 * validate(['name', 'email']) — returns 400 if any listed field is missing/empty.
 */
const validate = (fields) => (req, res, next) => {
  const missing = fields.filter(
    (f) => req.body[f] === undefined || req.body[f] === null || req.body[f] === '',
  );
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

    // ─────────────────────────── UTILS ───────────────────────────

    private string GeneratePaginateUtil()
    {
        return @"/**
 * Parse pagination query params.
 * @param {object} query - req.query
 * @returns {{ page: number, limit: number, skip: number }}
 */
const paginate = (query = {}) => {
  const page = Math.max(1, parseInt(query.page, 10) || 1);
  const limit = Math.min(100, Math.max(1, parseInt(query.limit, 10) || 20));
  const skip = (page - 1) * limit;
  return { page, limit, skip };
};

module.exports = paginate;
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
  console.error('MongoDB error:', err.message);
});

module.exports = connectDB;
";
    }

    // ─────────────────────────── SWAGGER CONFIG ───────────────────────────

    private string GenerateSwaggerConfig(string projectName, IEnumerable<Entity> entities)
    {
        var entityList = string.Join(", ", entities.Select(e => $"'/{LowerFirst(Pluralize(e.Name))}'"));
        return $@"const swaggerJsdoc = require('swagger-jsdoc');

const options = {{
  definition: {{
    openapi: '3.0.0',
    info: {{
      title: '{projectName} API',
      version: '1.0.0',
      description: 'Generated by CodeForge — Node.js + MongoDB',
    }},
    servers: [{{ url: `http://localhost:${{process.env.PORT || 3000}}` }}],
  }},
  apis: ['./src/routes/*.js'],
}};

const swaggerSpec = swaggerJsdoc(options);

module.exports = swaggerSpec;
";
    }

    // ─────────────────────────── APP.JS ───────────────────────────

    private string GenerateApp(IEnumerable<Entity> entities, AuthConfig? auth = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine("const express = require('express');");
        sb.AppendLine("const cors = require('cors');");
        sb.AppendLine("const helmet = require('helmet');");
        sb.AppendLine("const morgan = require('morgan');");
        if (auth?.Enabled == true)
            sb.AppendLine("const cookieParser = require('cookie-parser');");
        sb.AppendLine("const swaggerUi = require('swagger-ui-express');");
        sb.AppendLine("const swaggerSpec = require('./config/swagger');");
        sb.AppendLine("const notFound = require('./middleware/notFound');");
        sb.AppendLine("const errorHandler = require('./middleware/errorHandler');");
        sb.AppendLine();

        foreach (var entity in entities)
        {
            var nameLower = LowerFirst(entity.Name);
            sb.AppendLine($"const {nameLower}Routes = require('./routes/{nameLower}Routes');");
        }
        if (auth?.Enabled == true)
            sb.AppendLine("const authRoutes = require('./routes/authRoutes');");

        sb.AppendLine();
        sb.AppendLine("const app = express();");
        sb.AppendLine();
        sb.AppendLine("// ── Security & Parsing Middleware ───────────────────────────────────────────");
        sb.AppendLine("app.use(helmet());");
        sb.AppendLine("app.use(cors({");
        sb.AppendLine("  origin: process.env.ALLOWED_ORIGIN || '*',");
        sb.AppendLine("  methods: ['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'OPTIONS'],");
        sb.AppendLine("  allowedHeaders: ['Content-Type', 'Authorization'],");
        sb.AppendLine("  exposedHeaders: ['X-Total-Count', 'X-Page', 'X-Page-Size'],");
        sb.AppendLine("}));");
        sb.AppendLine("app.use(express.json({ limit: '10mb' }));");
        sb.AppendLine("app.use(express.urlencoded({ extended: true }));");
        if (auth?.Enabled == true)
            sb.AppendLine("app.use(cookieParser());");
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
        sb.AppendLine("// ── Swagger Docs ─────────────────────────────────────────────────────────────");
        sb.AppendLine("if (process.env.NODE_ENV !== 'production') {");
        sb.AppendLine("  app.use('/api-docs', swaggerUi.serve, swaggerUi.setup(swaggerSpec));");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("// ── API Routes ──────────────────────────────────────────────────────────────");
        if (auth?.Enabled == true)
            sb.AppendLine("app.use('/api/auth', authRoutes);");

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
    if (process.env.NODE_ENV !== 'production') {
      console.log(`   Swagger docs: http://localhost:${PORT}/api-docs`);
    }
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

    // ─────────────────────────── TESTS ───────────────────────────

    private string GenerateEntityTest(Entity entity, IEnumerable<Entity> allEntities)
    {
        var name = entity.Name;
        var nameLower = LowerFirst(name);
        var namePlural = Pluralize(name);
        var namePluralLower = LowerFirst(namePlural);

        // Pick a required scalar field for create test
        var requiredField = entity.Fields
            .FirstOrDefault(f => f.IsRequired && !f.IsPrimaryKey && f.DataType is "String" or "Text");

        var createBody = requiredField != null
            ? $"{{ {LowerFirst(requiredField.Name)}: 'test-value' }}"
            : "{}";

        return $@"/**
 * Smoke tests for {name} CRUD endpoints.
 * Run: npm test
 *
 * NOTE: Requires a running MongoDB instance (set MONGODB_URI in .env or environment).
 */
const request = require('supertest');
const mongoose = require('mongoose');
const app = require('../src/app');

let createdId;

beforeAll(async () => {{
  const uri = process.env.MONGODB_URI || 'mongodb://localhost:27017/{nameLower}_test';
  await mongoose.connect(uri);
}});

afterAll(async () => {{
  // Clean up test data
  if (createdId) {{
    await mongoose.model('{name}').findByIdAndDelete(createdId).catch(() => {{}});
  }}
  await mongoose.connection.close();
}});

describe('{name} API', () => {{
  it('GET /api/{namePluralLower} — returns paginated list', async () => {{
    const res = await request(app).get('/api/{namePluralLower}');
    expect(res.status).toBe(200);
    expect(res.body).toHaveProperty('data');
    expect(Array.isArray(res.body.data)).toBe(true);
    expect(res.body).toHaveProperty('total');
  }});

  it('POST /api/{namePluralLower} — creates a record', async () => {{
    const res = await request(app)
      .post('/api/{namePluralLower}')
      .send({createBody});
    expect([200, 201, 400]).toContain(res.status); // 400 if required fields missing
    if (res.status === 201) {{
      createdId = res.body.id || res.body._id;
      expect(createdId).toBeDefined();
    }}
  }});

  it('GET /api/{namePluralLower}/:id — 404 for unknown id', async () => {{
    const fakeId = new mongoose.Types.ObjectId().toString();
    const res = await request(app).get(`/api/{namePluralLower}/${{fakeId}}`);
    expect(res.status).toBe(404);
  }});

  it('GET /api/{namePluralLower}/:id — 400 for invalid id format', async () => {{
    const res = await request(app).get('/api/{namePluralLower}/not-a-valid-id');
    expect(res.status).toBe(400);
  }});
}});
";
    }

    // ─────────────────────────── PACKAGE.JSON ───────────────────────────

    private string GeneratePackageJson(string packageName, bool authEnabled = false)
    {
        var authDeps = authEnabled ? ",\r\n    \"bcryptjs\": \"^2.4.3\",\r\n    \"jsonwebtoken\": \"^9.0.2\",\r\n    \"cookie-parser\": \"^1.4.6\"" : "";
        return $@"{{
  ""name"": ""{packageName}"",
  ""version"": ""1.0.0"",
  ""description"": ""Generated Node.js + MongoDB backend by CodeForge"",
  ""main"": ""server.js"",
  ""scripts"": {{
    ""start"": ""node server.js"",
    ""dev"": ""nodemon server.js"",
    ""lint"": ""eslint src/ --fix"",
    ""test"": ""jest --forceExit""
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
    ""express-validator"": ""^7.2.0"",
    ""helmet"": ""^7.1.0"",
    ""mongoose"": ""^8.5.0"",
    ""morgan"": ""^1.10.0"",
    ""swagger-jsdoc"": ""^6.2.8"",
    ""swagger-ui-express"": ""^5.0.1""{authDeps}
  }},
  ""devDependencies"": {{
    ""nodemon"": ""^3.1.4"",
    ""jest"": ""^29.7.0"",
    ""supertest"": ""^7.0.0"",
    ""eslint"": ""^8.57.0""
  }}
}}
";
    }

    // ─────────────────────────── ESLINT ───────────────────────────

    private string GenerateEslintConfig()
    {
        return @"module.exports = {
  env: {
    node: true,
    es2022: true,
    jest: true,
  },
  extends: ['eslint:recommended'],
  parserOptions: {
    ecmaVersion: 2022,
  },
  rules: {
    'no-console': 'off',
    'no-unused-vars': ['warn', { argsIgnorePattern: '^_' }],
    'no-var': 'error',
    'prefer-const': 'error',
  },
};
";
    }

    // ─────────────────────────── JEST CONFIG ───────────────────────────

    private string GenerateJestConfig()
    {
        return @"/** @type {import('jest').Config} */
module.exports = {
  testEnvironment: 'node',
  testMatch: ['**/__tests__/**/*.test.js'],
  verbose: true,
  testTimeout: 30000,
};
";
    }

    // ─────────────────────────── ENV / GITIGNORE ───────────────────────────

    private string GenerateEnvExample(string packageName, AuthConfig? auth = null)
    {
        var jwtSection = auth?.Enabled == true ? $@"
# JWT Authentication
JWT_SECRET=CHANGE_ME_USE_A_LONG_RANDOM_SECRET_KEY_AT_LEAST_32_CHARS
JWT_EXPIRES_IN={auth.TokenExpiryMinutes}m
JWT_REFRESH_EXPIRES_IN={auth.RefreshTokenExpiryDays}d" : "";
        return $@"# Server
PORT=3000
NODE_ENV=development

# MongoDB
MONGODB_URI=mongodb://localhost:27017/{packageName.Replace("-", "_")}db

# CORS — set to your frontend URL in production
ALLOWED_ORIGIN=*{jwtSection}
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

# Test coverage
coverage/
";
    }

    // ─────────────────────────── DOCKER ───────────────────────────

    private string GenerateDockerfile()
    {
        return @"FROM node:20-alpine AS base
WORKDIR /app

# Install dependencies first (layer cache optimization)
COPY package*.json ./
RUN npm install

# Copy source
COPY . .

EXPOSE 3000

# Use non-root user for security
USER node

CMD [""node"", ""server.js""]
";
    }

    private string GenerateDockerCompose(string projectName, string packageName)
    {
        var dbName = packageName.Replace("-", "_") + "db";
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
      context: .
      dockerfile: Dockerfile
    restart: unless-stopped
    ports:
      - ""3000:3000""
    environment:
      - NODE_ENV=development
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

    private string GenerateReadme(Project project, string projectName, string packageName)
    {
        var entityList = string.Join("\n", project.Entities.Select(e =>
            $"- **{e.Name}** — {e.Fields.Count} field(s): {string.Join(", ", e.Fields.Select(f => f.Name))}"));

        var endpointsTable = string.Join("\n", project.Entities.Select(e =>
        {
            var plural = LowerFirst(Pluralize(e.Name));
            return $"| `{e.Name}` | `/api/{plural}` | GET (list), POST, GET/:id, PUT/:id, PATCH/:id, DELETE/:id |";
        }));

        return $@"# {projectName}

> Generated with **CodeForge** — Node.js + Express + MongoDB Backend

## Tech Stack

- **Runtime**: Node.js 20 (LTS)
- **Framework**: Express 4
- **ODM**: Mongoose 8
- **Database**: MongoDB 7
- **Validation**: express-validator
- **Docs**: Swagger UI (`/api-docs`)
- **Testing**: Jest + Supertest
- **Containerization**: Docker + Docker Compose

## Project Structure

```
{projectName}/
├── server.js              # Entry point — starts HTTP server
├── src/
│   ├── app.js             # Express app setup
│   ├── config/
│   │   ├── database.js    # MongoDB connection
│   │   └── swagger.js     # Swagger/OpenAPI configuration
│   ├── models/            # Mongoose schemas
│   ├── controllers/       # Business logic (uses asyncHandler)
│   ├── routes/            # API route definitions + swagger annotations
│   ├── validation/        # express-validator rule sets per entity
│   ├── middleware/
│   │   ├── asyncHandler.js  # Async wrapper (no try/catch in controllers)
│   │   ├── errorHandler.js  # Global error handler
│   │   ├── notFound.js
│   │   └── validate.js
│   └── utils/
│       └── paginate.js    # Pagination helper
├── __tests__/             # Jest smoke tests per entity
├── .env.example
├── .eslintrc.js
├── jest.config.js
├── Dockerfile
└── package.json
```

## Generated Entities

{entityList}

## API Endpoints

| Entity | Base Route | Methods |
|--------|-----------|---------|
{endpointsTable}

## Quick Start

### 🐳 Docker (recommended)

```bash
cd {projectName}
docker-compose up --build
```

- API: `http://localhost:3000`
- Swagger: `http://localhost:3000/api-docs`
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
# Edit MONGODB_URI with your connection string
```

3. Start MongoDB (if not using Docker):
```bash
docker run -d -p 27017:27017 --name mongo mongo:7
```

4. Run dev server:
```bash
npm run dev
```

## Running Tests

```bash
npm test
```

## Pagination

All list endpoints support pagination via query params:

```
GET /api/{{entity}}?page=1&limit=20
```

Response headers: `X-Total-Count`, `X-Page`, `X-Page-Size`

Response body:
```json
{{
  ""data"": [...],
  ""total"": 42,
  ""page"": 1,
  ""limit"": 20
}}
```

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
        "File" => "String",  // stores path/URL; handle upload separately
        _ => "String"
    };

    private string LowerFirst(string str) =>
        string.IsNullOrEmpty(str) ? str : char.ToLower(str[0]) + str[1..];

    /// <summary>Safe directory/variable name: PascalCase, only letters and digits.</summary>
    private string SanitizeDirName(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? "GeneratedProject"
            : new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray())
                .TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');

    /// <summary>Safe npm package name: lowercase kebab-case.</summary>
    private string SanitizePackageName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "generated-project";
        return new string(name
            .Select(c => char.IsLetterOrDigit(c) ? char.ToLower(c) : '-')
            .ToArray())
            .Trim('-')
            .Replace("--", "-");
    }

    // ═══════════════════ AUTH MODULE GENERATORS ═══════════════════════

    private string GenerateAuthUserModel(AuthConfig auth)
    {
        var roleField = auth.EnableRoles
            ? $"\n  role: {{ type: String, enum: [{string.Join(", ", auth.Roles.Select(r => $"'{r}'"))}], default: '{auth.Roles.FirstOrDefault() ?? "User"}' }},"
            : "";
        var refreshFields = auth.EnableRefreshTokens
            ? "\n  refreshToken: { type: String, default: null },\n  refreshTokenExpiresAt: { type: Date, default: null },"
            : "";
        var emailVerifFields = auth.EnableEmailVerification
            ? "\n  isEmailVerified: { type: Boolean, default: false },\n  emailVerificationToken: { type: String, default: null },"
            : "";
        var usernameField = auth.UserIdentifier is "username" or "both"
            ? "\n  username: { type: String, maxlength: 100, trim: true },"
            : "";
        var emailField = auth.UserIdentifier is "email" or "both"
            ? "\n  email: { type: String, required: true, unique: true, lowercase: true, trim: true, maxlength: 255 },"
            : "";

        return $@"const {{ Schema, model }} = require('mongoose');

const userSchema = new Schema(
  {{{emailField}{usernameField}
    passwordHash: {{ type: String, required: true }},{roleField}{refreshFields}{emailVerifFields}
  }},
  {{ timestamps: true }}
);

// Remove sensitive fields from JSON output
userSchema.set('toJSON', {{
  transform: (_doc, ret) => {{
    delete ret.passwordHash;
    delete ret.refreshToken;
    delete ret.refreshTokenExpiresAt;
    delete ret.emailVerificationToken;
    return ret;
  }},
}});

module.exports = model('User', userSchema);
";
    }

    private string GenerateAuthController(AuthConfig auth)
    {
        var refreshExpiry = auth.EnableRefreshTokens ? auth.RefreshTokenExpiryDays : 7;
        return $@"const bcrypt = require('bcryptjs');
const User = require('../models/User');
const {{ generateAccessToken, generateRefreshToken }} = require('../utils/generateTokens');
const asyncHandler = require('../middleware/asyncHandler');

// POST /api/auth/register
exports.register = asyncHandler(async (req, res) => {{
  const {{ email, password, username }} = req.body;
  if (!email || !password) return res.status(400).json({{ message: 'Email and password are required' }});

  const exists = await User.findOne({{ email: email.toLowerCase() }});
  if (exists) return res.status(409).json({{ message: 'Email already in use' }});

  const passwordHash = await bcrypt.hash(password, 12);
  const user = await User.create({{ email: email.toLowerCase(), password: password, passwordHash, username }});

  const accessToken = generateAccessToken(user);
  const refreshToken = generateRefreshToken(user);

  user.refreshToken = refreshToken;
  user.refreshTokenExpiresAt = new Date(Date.now() + {refreshExpiry} * 24 * 60 * 60 * 1000);
  await user.save();

  res.status(201).json({{
    accessToken,
    refreshToken,
    expiresInSeconds: {auth.TokenExpiryMinutes * 60},
    user,
  }});
}});

// POST /api/auth/login
exports.login = asyncHandler(async (req, res) => {{
  const {{ email, password }} = req.body;
  if (!email || !password) return res.status(400).json({{ message: 'Email and password are required' }});

  const user = await User.findOne({{ email: email.toLowerCase() }}).select('+passwordHash');
  if (!user) return res.status(401).json({{ message: 'Invalid credentials' }});

  const match = await bcrypt.compare(password, user.passwordHash);
  if (!match) return res.status(401).json({{ message: 'Invalid credentials' }});

  const accessToken = generateAccessToken(user);
  const refreshToken = generateRefreshToken(user);

  user.refreshToken = refreshToken;
  user.refreshTokenExpiresAt = new Date(Date.now() + {refreshExpiry} * 24 * 60 * 60 * 1000);
  await user.save();

  res.json({{ accessToken, refreshToken, expiresInSeconds: {auth.TokenExpiryMinutes * 60}, user }});
}});

// POST /api/auth/refresh
exports.refresh = asyncHandler(async (req, res) => {{
  const {{ refreshToken }} = req.body;
  if (!refreshToken) return res.status(400).json({{ message: 'Refresh token required' }});

  const user = await User.findOne({{
    refreshToken,
    refreshTokenExpiresAt: {{ $gt: new Date() }},
  }});
  if (!user) return res.status(401).json({{ message: 'Invalid or expired refresh token' }});

  const accessToken = generateAccessToken(user);
  const newRefreshToken = generateRefreshToken(user);

  user.refreshToken = newRefreshToken;
  user.refreshTokenExpiresAt = new Date(Date.now() + {refreshExpiry} * 24 * 60 * 60 * 1000);
  await user.save();

  res.json({{ accessToken, refreshToken: newRefreshToken, expiresInSeconds: {auth.TokenExpiryMinutes * 60} }});
}});

// GET /api/auth/me
exports.me = asyncHandler(async (req, res) => {{
  const user = await User.findById(req.user.id);
  if (!user) return res.status(404).json({{ message: 'User not found' }});
  res.json(user);
}});

// POST /api/auth/logout
exports.logout = asyncHandler(async (req, res) => {{
  const user = await User.findById(req.user.id);
  if (user) {{
    user.refreshToken = null;
    user.refreshTokenExpiresAt = null;
    await user.save();
  }}
  res.status(204).send();
}});

// PUT /api/auth/password
exports.changePassword = asyncHandler(async (req, res) => {{
  const {{ currentPassword, newPassword }} = req.body;
  if (!currentPassword || !newPassword) return res.status(400).json({{ message: 'Both passwords required' }});

  const user = await User.findById(req.user.id).select('+passwordHash');
  if (!user) return res.status(404).json({{ message: 'User not found' }});

  const match = await bcrypt.compare(currentPassword, user.passwordHash);
  if (!match) return res.status(401).json({{ message: 'Current password incorrect' }});

  user.passwordHash = await bcrypt.hash(newPassword, 12);
  await user.save();
  res.status(204).send();
}});
";
    }

    private string GenerateAuthRoutes(AuthConfig auth)
    {
        var refreshImport = auth.EnableRefreshTokens ? "\n  refresh," : "";
        var logoutImport = auth.EnableRefreshTokens ? "\n  logout," : "";
        var refreshRoute = auth.EnableRefreshTokens ? "\nrouter.post('/refresh', refresh);\nrouter.post('/logout', verifyToken, logout);" : "";
        return $@"const express = require('express');
const router = express.Router();
const {{ verifyToken }} = require('../middleware/authMiddleware');
const {{
  register,
  login,{refreshImport}
  me,{logoutImport}
  changePassword,
}} = require('../controllers/authController');

/** @swagger
 * tags:
 *   name: Auth
 *   description: Authentication endpoints
 */

router.post('/register', register);
router.post('/login', login);{refreshRoute}
router.get('/me', verifyToken, me);
router.put('/password', verifyToken, changePassword);

module.exports = router;
";
    }

    private string GenerateAuthMiddleware()
    {
        return @"const jwt = require('jsonwebtoken');

/**
 * verifyToken — attaches decoded user payload to req.user
 */
exports.verifyToken = (req, res, next) => {
  const authHeader = req.headers.authorization;
  const token = authHeader?.startsWith('Bearer ') ? authHeader.slice(7) : null;
  if (!token) return res.status(401).json({ message: 'No token provided' });

  try {
    const decoded = jwt.verify(token, process.env.JWT_SECRET);
    req.user = decoded;
    next();
  } catch (err) {
    return res.status(401).json({ message: 'Invalid or expired token' });
  }
};
";
    }

    private string GenerateRoleMiddleware()
    {
        return @"/**
 * requireRole(...roles) — must be used AFTER verifyToken
 * Example: router.get('/admin', verifyToken, requireRole('Admin'), handler)
 */
exports.requireRole = (...roles) => (req, res, next) => {
  if (!req.user) return res.status(401).json({ message: 'Unauthorized' });
  if (!roles.includes(req.user.role)) {
    return res.status(403).json({ message: 'Forbidden: insufficient role' });
  }
  next();
};
";
    }

    private string GenerateTokensUtil(AuthConfig auth)
    {
        return $@"const jwt = require('jsonwebtoken');
const crypto = require('crypto');

/**
 * Generate short-lived JWT access token
 */
exports.generateAccessToken = (user) => {{
  return jwt.sign(
    {{
      id: user._id,
      email: user.email,
      role: user.role,
    }},
    process.env.JWT_SECRET,
    {{ expiresIn: process.env.JWT_EXPIRES_IN || '{auth.TokenExpiryMinutes}m' }}
  );
}};

/**
 * Generate opaque refresh token (stored in DB)
 */
exports.generateRefreshToken = (_user) =>
  crypto.randomBytes(64).toString('hex');
";
    }
}


using System.Text;
using CodeForgeAPI.Models;

namespace CodeForgeAPI.Services.Generators;

public class NodeJSMongoDBGenerator : ITemplateGenerator
{
    public Dictionary<string, string> Generate(Project project)
    {
        var files = new Dictionary<string, string>();
        var projectName = SanitizeName(project.Name);
        
        // Generate Models
        foreach (var entity in project.Entities)
        {
            files[$"{projectName}/models/{entity.Name}.js"] = GenerateModel(entity);
        }
        
        // Generate Routes
        foreach (var entity in project.Entities)
        {
            files[$"{projectName}/routes/{LowerFirst(entity.Name)}Routes.js"] = GenerateRoutes(entity);
        }
        
        // Generate main app file
        files[$"{projectName}/app.js"] = GenerateApp(project.Entities);
        
        // Generate package.json
        files[$"{projectName}/package.json"] = GeneratePackageJson(projectName);
        
        // Generate .env.example
        files[$"{projectName}/.env.example"] = GenerateEnvExample();
        
        // Generate Dockerfile
        files[$"{projectName}/Dockerfile"] = GenerateDockerfile();
        
        // Generate docker-compose.yml
        files["docker-compose.yml"] = GenerateDockerCompose(projectName);
        
        // Generate README.md
        files["README.md"] = GenerateReadme(projectName);
        
        return files;
    }
    
    private string GenerateModel(Entity entity)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("const mongoose = require('mongoose');");
        sb.AppendLine();
        sb.AppendLine($"const {entity.Name}Schema = new mongoose.Schema({{");
        
        foreach (var field in entity.Fields.OrderBy(f => f.DisplayOrder))
        {
            if (field.DataType == "Relationship")
            {
                if (field.RelatedEntityId.HasValue)
                {
                    var relatedEntity = entity.Project.Entities.FirstOrDefault(e => e.Id == field.RelatedEntityId);
                    if (relatedEntity != null)
                    {
                        if (field.RelationshipType == "ManyToMany")
                        {
                            sb.AppendLine($"  {LowerFirst(field.Name)}: [{{");
                            sb.AppendLine("    type: mongoose.Schema.Types.ObjectId,");
                            sb.AppendLine($"    ref: '{relatedEntity.Name}'");
                            sb.AppendLine("  }],");
                        }
                        else
                        {
                            sb.AppendLine($"  {LowerFirst(field.Name)}: {{");
                            sb.AppendLine("    type: mongoose.Schema.Types.ObjectId,");
                            sb.AppendLine($"    ref: '{relatedEntity.Name}',");
                            if (field.IsRequired)
                                sb.AppendLine("    required: true");
                            sb.AppendLine("  },");
                        }
                    }
                }
            }
            else
            {
                sb.AppendLine($"  {LowerFirst(field.Name)}: {{");
                sb.AppendLine($"    type: {MapDataTypeToMongoose(field.DataType)},");
                
                if (field.IsRequired)
                    sb.AppendLine("    required: true,");
                
                if (field.IsUnique)
                    sb.AppendLine("    unique: true,");
                
                if (field.DataType == "String")
                    sb.AppendLine("    maxlength: 255");
                
                sb.AppendLine("  },");
            }
        }
        
        sb.AppendLine("}, {");
        sb.AppendLine("  timestamps: true");
        sb.AppendLine("});");
        sb.AppendLine();
        sb.AppendLine($"module.exports = mongoose.model('{entity.Name}', {entity.Name}Schema);");
        
        return sb.ToString();
    }
    
    private string GenerateRoutes(Entity entity)
    {
        var sb = new StringBuilder();
        var entityNameLower = LowerFirst(entity.Name);
        var entityNamePlural = entityNameLower + "s";
        
        sb.AppendLine("const express = require('express');");
        sb.AppendLine("const router = express.Router();");
        sb.AppendLine($"const {entity.Name} = require('../models/{entity.Name}');");
        sb.AppendLine();
        
        // GET all
        sb.AppendLine("// GET all");
        sb.AppendLine("router.get('/', async (req, res) => {");
        sb.AppendLine("  try {");
        sb.AppendLine($"    const {entityNamePlural} = await {entity.Name}.find();");
        sb.AppendLine($"    res.json({entityNamePlural});");
        sb.AppendLine("  } catch (err) {");
        sb.AppendLine("    res.status(500).json({ message: err.message });");
        sb.AppendLine("  }");
        sb.AppendLine("});");
        sb.AppendLine();
        
        // GET by id
        sb.AppendLine("// GET by id");
        sb.AppendLine("router.get('/:id', async (req, res) => {");
        sb.AppendLine("  try {");
        sb.AppendLine($"    const {entityNameLower} = await {entity.Name}.findById(req.params.id);");
        sb.AppendLine($"    if (!{entityNameLower}) {{");
        sb.AppendLine("      return res.status(404).json({ message: 'Not found' });");
        sb.AppendLine("    }");
        sb.AppendLine($"    res.json({entityNameLower});");
        sb.AppendLine("  } catch (err) {");
        sb.AppendLine("    res.status(500).json({ message: err.message });");
        sb.AppendLine("  }");
        sb.AppendLine("});");
        sb.AppendLine();
        
        // POST
        sb.AppendLine("// POST create new");
        sb.AppendLine("router.post('/', async (req, res) => {");
        sb.AppendLine($"  const {entityNameLower} = new {entity.Name}(req.body);");
        sb.AppendLine();
        sb.AppendLine("  try {");
        sb.AppendLine($"    const new{entity.Name} = await {entityNameLower}.save();");
        sb.AppendLine($"    res.status(201).json(new{entity.Name});");
        sb.AppendLine("  } catch (err) {");
        sb.AppendLine("    res.status(400).json({ message: err.message });");
        sb.AppendLine("  }");
        sb.AppendLine("});");
        sb.AppendLine();
        
        // PUT
        sb.AppendLine("// PUT update");
        sb.AppendLine("router.put('/:id', async (req, res) => {");
        sb.AppendLine("  try {");
        sb.AppendLine($"    const {entityNameLower} = await {entity.Name}.findByIdAndUpdate(");
        sb.AppendLine("      req.params.id,");
        sb.AppendLine("      req.body,");
        sb.AppendLine("      { new: true, runValidators: true }");
        sb.AppendLine("    );");
        sb.AppendLine($"    if (!{entityNameLower}) {{");
        sb.AppendLine("      return res.status(404).json({ message: 'Not found' });");
        sb.AppendLine("    }");
        sb.AppendLine($"    res.json({entityNameLower});");
        sb.AppendLine("  } catch (err) {");
        sb.AppendLine("    res.status(400).json({ message: err.message });");
        sb.AppendLine("  }");
        sb.AppendLine("});");
        sb.AppendLine();
        
        // DELETE
        sb.AppendLine("// DELETE");
        sb.AppendLine("router.delete('/:id', async (req, res) => {");
        sb.AppendLine("  try {");
        sb.AppendLine($"    const {entityNameLower} = await {entity.Name}.findByIdAndDelete(req.params.id);");
        sb.AppendLine($"    if (!{entityNameLower}) {{");
        sb.AppendLine("      return res.status(404).json({ message: 'Not found' });");
        sb.AppendLine("    }");
        sb.AppendLine("    res.json({ message: 'Deleted successfully' });");
        sb.AppendLine("  } catch (err) {");
        sb.AppendLine("    res.status(500).json({ message: err.message });");
        sb.AppendLine("  }");
        sb.AppendLine("});");
        sb.AppendLine();
        sb.AppendLine("module.exports = router;");
        
        return sb.ToString();
    }
    
    private string GenerateApp(IEnumerable<Entity> entities)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("require('dotenv').config();");
        sb.AppendLine("const express = require('express');");
        sb.AppendLine("const mongoose = require('mongoose');");
        sb.AppendLine("const cors = require('cors');");
        sb.AppendLine();
        sb.AppendLine("const app = express();");
        sb.AppendLine();
        sb.AppendLine("// Middleware");
        sb.AppendLine("app.use(cors());");
        sb.AppendLine("app.use(express.json());");
        sb.AppendLine();
        sb.AppendLine("// Database connection");
        sb.AppendLine("mongoose.connect(process.env.MONGODB_URI || 'mongodb://localhost:27017/myappdb', {");
        sb.AppendLine("  useNewUrlParser: true,");
        sb.AppendLine("  useUnifiedTopology: true");
        sb.AppendLine("});");
        sb.AppendLine();
        sb.AppendLine("const db = mongoose.connection;");
        sb.AppendLine("db.on('error', (error) => console.error(error));");
        sb.AppendLine("db.once('open', () => console.log('Connected to database'));");
        sb.AppendLine();
        sb.AppendLine("// Routes");
        
        foreach (var entity in entities)
        {
            var entityNameLower = LowerFirst(entity.Name);
            var routeName = entityNameLower + "s";
            sb.AppendLine($"const {entityNameLower}Routes = require('./routes/{entityNameLower}Routes');");
            sb.AppendLine($"app.use('/api/{routeName}', {entityNameLower}Routes);");
        }
        
        sb.AppendLine();
        sb.AppendLine("const PORT = process.env.PORT || 3000;");
        sb.AppendLine("app.listen(PORT, () => console.log(`Server running on port ${PORT}`));");
        
        return sb.ToString();
    }
    
    private string GeneratePackageJson(string projectName)
    {
        return $@"{{
  ""name"": ""{projectName.ToLower()}"",
  ""version"": ""1.0.0"",
  ""description"": ""Generated backend application"",
  ""main"": ""app.js"",
  ""scripts"": {{
    ""start"": ""node app.js"",
    ""dev"": ""nodemon app.js""
  }},
  ""keywords"": [],
  ""author"": """",
  ""license"": ""ISC"",
  ""dependencies"": {{
    ""express"": ""^4.18.2"",
    ""mongoose"": ""^8.0.0"",
    ""dotenv"": ""^16.3.1"",
    ""cors"": ""^2.8.5""
  }},
  ""devDependencies"": {{
    ""nodemon"": ""^3.0.1""
  }}
}}
";
    }
    
    private string GenerateEnvExample()
    {
        return @"PORT=3000
MONGODB_URI=mongodb://localhost:27017/myappdb
";
    }
    
    private string GenerateDockerfile()
    {
        return @"FROM node:20-alpine

WORKDIR /app

COPY package*.json ./

RUN npm install

COPY . .

EXPOSE 3000

CMD [""npm"", ""start""]
";
    }
    
    private string GenerateDockerCompose(string projectName)
    {
        return $@"version: '3.8'

services:
  mongodb:
    image: mongo:7
    ports:
      - ""27017:27017""
    volumes:
      - mongodb_data:/data/db
    environment:
      MONGO_INITDB_DATABASE: myappdb

  api:
    build:
      context: ./{projectName}
      dockerfile: Dockerfile
    ports:
      - ""3000:3000""
    environment:
      - MONGODB_URI=mongodb://mongodb:27017/myappdb
      - PORT=3000
    depends_on:
      - mongodb

volumes:
  mongodb_data:
";
    }
    
    private string GenerateReadme(string projectName)
    {
        return $@"# {projectName}

This project was generated using the Backend Code Generator.

## Prerequisites

- Node.js 18+ 
- MongoDB 7+ or Docker

## Getting Started

### Using Docker

1. Run the application with Docker Compose:

```bash
docker-compose up
```

The API will be available at `http://localhost:3000`

### Local Development

1. Install dependencies:

```bash
cd {projectName}
npm install
```

2. Create a `.env` file based on `.env.example`:

```bash
cp .env.example .env
```

3. Update the `.env` file with your MongoDB connection string if needed.

4. Start MongoDB locally or use Docker:

```bash
# Using Docker
docker run -d -p 27017:27017 --name mongodb mongo:7
```

5. Run the application:

```bash
npm run dev
```

The API will be available at `http://localhost:3000`

## API Endpoints

All endpoints follow the pattern `/api/{{entityName}}s`:

- `GET /api/{{entityName}}s` - Get all items
- `GET /api/{{entityName}}s/:id` - Get item by ID
- `POST /api/{{entityName}}s` - Create new item
- `PUT /api/{{entityName}}s/:id` - Update item
- `DELETE /api/{{entityName}}s/:id` - Delete item

## Generated Entities

";
    }
    
    private string MapDataTypeToMongoose(string dataType)
    {
        return dataType switch
        {
            "String" => "String",
            "Integer" => "Number",
            "Boolean" => "Boolean",
            "DateTime" => "Date",
            "Decimal" => "Number",
            "Text" => "String",
            _ => "String"
        };
    }
    
    private string LowerFirst(string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;
        
        return char.ToLower(str[0]) + str.Substring(1);
    }
    
    private string SanitizeName(string name)
    {
        return string.IsNullOrWhiteSpace(name) ? "generated-project" : name.Replace(" ", "-").ToLower();
    }
}

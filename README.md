# Todo Application

A production-ready .NET 10.0 application demonstrating **Clean Architecture**, **Domain-Driven Design (DDD)**, and **Vertical Slice Architecture** patterns with modern best practices.

## 🚀 Quick Start

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://www.docker.com/get-started) (for database)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/) with C# extension

### Running the Application

1. **Clone the repository**

   ```bash
   git clone <repository-url>
   cd Todo
   ```

2. **Restore dependencies**

   ```bash
   dotnet restore
   ```

3. **Build the solution**

   ```bash
   dotnet build
   ```

4. **Run the application**

   ```bash
   cd Todo.WebHost
   dotnet run
   ```

5. **Access the API**

   - Swagger UI: <https://localhost:5001/swagger>
   - API Base: <https://localhost:5001/api>

## 📁 Project Structure

```text
Todo/
├── Shared/                  # Cross-cutting concerns
│   └── Todo.Shared.Kernel/  # Shared primitives, base classes
│
├── Tasks/                   # Tasks Bounded Context
│   ├── Todo.Tasks.Contracts/    # Public API (DTOs, Interfaces)
│   ├── Todo.Tasks.Domain/       # Business logic & entities
│   ├── Todo.Tasks.Database/     # Data access & migrations
│   ├── Todo.Tasks.Logic/        # CQRS handlers & services
│   ├── Todo.Tasks.Api/          # API endpoints
│   ├── Todo.Tasks.Module/       # DI registration
│   └── Todo.Tasks.Tests/        # Comprehensive tests
│
└── Todo.WebHost/           # Application entry point
```

## 🏗️ Architecture

This project uses a **modular monolith** architecture that can evolve into microservices:

- **Clean Architecture**: Dependencies flow inward (Domain ← Logic ← API)
- **DDD**: Each module is a bounded context with its own domain
- **Vertical Slices**: Features are self-contained within modules
- **CQRS**: Commands and queries separated via MediatR

### Module Communication

**Within a module**: MediatR commands/queries  
**Between modules**: Service interfaces (`ITaskService`)  
**Between services**: HTTP/gRPC (configuration-driven)

See [ARCHITECTURE.md](./ARCHITECTURE.md) for detailed documentation.

## 🛠️ Technology Stack

### Core

- **.NET 10.0** - Latest framework
- **C# Latest** - Modern language features
- **FastEndpoints 7.1.1** - High-performance API endpoints

### Application

- **MediatR 13.1.0** - CQRS implementation
- **FluentValidation 12.1.0** - Request validation
- **Mapster 7.4.0** - Object mapping
- **ErrorOr 2.0.1** - Type-safe error handling

### Data

- **Entity Framework Core 10.0** - ORM for complex queries
- **Dapper 2.1.66** - Micro-ORM for performance
- **FluentMigrator 7.1.0** - Database migrations
- **SQL Server / PostgreSQL / MySQL** - Multi-database support

### Observability

- **Serilog 4.1.0** - Structured logging
- **OpenTelemetry 1.10.0** - Distributed tracing

### Testing

- **xUnit** - Test framework
- **Moq** - Mocking
- **Testcontainers** - Real database testing
- **Bogus** - Test data generation
- **Verify** - Snapshot testing
- **NetArchTest** - Architecture testing

## ⚙️ Configuration

### appsettings.json

```json
{
  "Modules": {
    "Tasks": {
      "Enabled": true,
      "ServiceMode": "InProcess",
      "Features": {
        "EnableNotifications": true,
        "EnableAuditLog": false,
        "UseRedisCache": false,
        "DatabaseProvider": "SqlServer"
      }
    }
  },
  "ConnectionStrings": {
    "TasksDb": "Server=localhost;Database=TodoTasks;Trusted_Connection=true;"
  }
}
```

### Service Modes

Configure how modules communicate:

- **InProcess**: Direct MediatR calls (monolith)
- **Http**: REST API calls (microservices)
- **MessageBus**: Async messaging (event-driven)

Change mode without code changes:

```json
{
  "Modules": {
    "Tasks": {
      "ServiceMode": "Http",
      "HttpServiceUrl": "https://tasks-service.example.com"
    }
  }
}
```

## 🗄️ Database Setup

### Using SQL Server (Docker)

```bash
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong@Passw0rd" \
  -p 1433:1433 --name sql-server \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

### Using PostgreSQL (Docker)

```bash
docker run --name postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=TodoTasks \
  -p 5432:5432 \
  -d postgres:16
```

### Update Connection String

**SQL Server**:

```json
{
  "ConnectionStrings": {
    "TasksDb": "Server=localhost;Database=TodoTasks;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true;"
  },
  "Modules": {
    "Tasks": {
      "Features": {
        "DatabaseProvider": "SqlServer"
      }
    }
  }
}
```

**PostgreSQL**:

```json
{
  "ConnectionStrings": {
    "TasksDb": "Host=localhost;Database=TodoTasks;Username=postgres;Password=postgres"
  },
  "Modules": {
    "Tasks": {
      "Features": {
        "DatabaseProvider": "PostgreSQL"
      }
    }
  }
}
```

### Run Migrations

```bash
cd Tasks/Todo.Tasks.Database
dotnet fm migrate -p SqlServer -c "YourConnectionString"
```

## 🧪 Testing

### Run All Tests

```bash
dotnet test
```

### Run Specific Test Project

```bash
dotnet test Tasks/Todo.Tasks.Tests/Todo.Tasks.Tests.csproj
```

### Run with Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Architecture Tests

Enforce architectural rules:

```csharp
[Fact]
public void Domain_Should_Not_Reference_Infrastructure()
{
    var result = Types.InAssembly(typeof(TaskEntity).Assembly)
        .Should().NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
        .GetResult();
    
    result.IsSuccessful.Should().BeTrue();
}
```

## 📝 Adding a New Feature

### 1. Create Contract (DTO)

```csharp
// Todo.Tasks.Contracts/Requests/CreateTaskRequest.cs
public record CreateTaskRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
}
```

### 2. Create Command

```csharp
// Todo.Tasks.Logic/Commands/CreateTaskCommand.cs
public record CreateTaskCommand(CreateTaskRequest Request) 
    : IRequest<ErrorOr<TaskResponse>>;
```

### 3. Create Handler

```csharp
// Todo.Tasks.Logic/Handlers/CreateTaskCommandHandler.cs
public class CreateTaskCommandHandler 
    : IRequestHandler<CreateTaskCommand, ErrorOr<TaskResponse>>
{
    public async Task<ErrorOr<TaskResponse>> Handle(
        CreateTaskCommand command, 
        CancellationToken ct)
    {
        // Business logic here
    }
}
```

### 4. Create Endpoint

```csharp
// Todo.Tasks.Api/Endpoints/CreateTaskEndpoint.cs
public class CreateTaskEndpoint : Endpoint<CreateTaskRequest, TaskResponse>
{
    private readonly ISender _sender;
    
    public override void Configure()
    {
        Post("/api/tasks");
        AllowAnonymous();
    }
    
    public override async Task HandleAsync(
        CreateTaskRequest req, 
        CancellationToken ct)
    {
        var result = await _sender.Send(new CreateTaskCommand(req), ct);
        
        await result.MatchAsync(
            success => SendOkAsync(success, ct),
            errors => SendErrorsAsync(errors, ct)
        );
    }
}
```

### 5. Add Tests

```csharp
// Todo.Tasks.Tests/Unit/CreateTaskHandlerTests.cs
public class CreateTaskHandlerTests
{
    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var handler = new CreateTaskCommandHandler(/* deps */);
        var command = new CreateTaskCommand(
            new CreateTaskRequest { Title = "Test" }
        );
        
        // Act
        var result = await handler.Handle(command, CancellationToken.None);
        
        // Assert
        result.IsError.Should().BeFalse();
    }
}
```

## 🔄 Migration to Microservices

### Current State: Modular Monolith

All modules run in-process:

```json
{
  "Modules": {
    "Tasks": { "ServiceMode": "InProcess" },
    "Orders": { "ServiceMode": "InProcess" }
  }
}
```

### Step 1: Extract Tasks Service

Deploy Tasks as separate service, update config:

```json
{
  "Modules": {
    "Tasks": {
      "ServiceMode": "Http",
      "HttpServiceUrl": "https://tasks-service.example.com"
    },
    "Orders": { "ServiceMode": "InProcess" }
  }
}
```

**No code changes required!** The `ITaskService` interface now uses HTTP client instead of direct MediatR.

### Step 2: Extract Remaining Services

Repeat for other modules as needed.

## 📊 Observability

### Structured Logging with Serilog

```csharp
_logger.LogInformation(
    "Task {TaskId} created by {UserId}", 
    task.Id, 
    userId
);
```

Logs are output to:

- Console (Development)
- File (All environments)
- Seq (if configured)

### Distributed Tracing with OpenTelemetry

Automatic tracing for:

- HTTP requests
- Database calls (SQL Server)
- MediatR commands/queries

View traces in your APM tool (Jaeger, Zipkin, Azure Monitor, etc.)

### Health Checks

```text
GET /health
```

Returns status of:

- Database connection
- Redis cache (if enabled)
- Other dependencies

## 🔒 Security

### Authentication

Configure JWT authentication:

```json
{
  "Authentication": {
    "Jwt": {
      "Secret": "your-secret-key-here",
      "Issuer": "todo-api",
      "Audience": "todo-client"
    }
  }
}
```

### Authorization

Use FastEndpoints security:

```csharp
public class CreateTaskEndpoint : Endpoint<CreateTaskRequest, TaskResponse>
{
    public override void Configure()
    {
        Post("/api/tasks");
        Roles("Admin", "User");  // Require authentication
    }
}
```

## 📦 Package Management

This project uses **Central Package Management (CPM)**:

- **Directory.Packages.props**: All versions defined once
- **Project files**: Reference packages without versions
- **Benefits**: No version conflicts, easy updates

### Update All Packages

```bash
dotnet list package --outdated
```

Update versions in `Directory.Packages.props`, then:

```bash
dotnet restore
dotnet build
```

## 🎯 Best Practices

### ✅ DO

- Keep Contracts layer dependency-free (pure DTOs)
- Use ErrorOr for error handling (avoid exceptions for flow control)
- Use MediatR within modules, interfaces between modules
- Write tests at all levels (unit, integration, architecture)
- Use configuration for environment differences

### ❌ DON'T

- Reference another module's Logic layer directly
- Put MediatR commands in Contracts
- Run database migrations at application startup (use pipeline)
- Couple modules through shared implementation details

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Code Standards

- Follow .editorconfig rules (enforced by analyzers)
- All warnings as errors for nullable reference types
- Run `dotnet format` before committing
- Ensure all tests pass

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 📚 Additional Resources

- [Architecture Documentation](./ARCHITECTURE.md) - Detailed architecture guide
- [FastEndpoints Documentation](https://fast-endpoints.com/)
- [MediatR Wiki](https://github.com/jbogard/MediatR/wiki)
- [Clean Architecture Blog](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Domain-Driven Design](https://www.domainlanguage.com/ddd/)

## 💬 Support

For questions and support:

- Open an issue
- Check existing documentation
- Review architecture patterns in ARCHITECTURE.md

---

**Built with ❤️ using .NET 10.0 and Clean Architecture principles**

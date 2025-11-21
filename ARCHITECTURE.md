# Architecture Documentation

## Overview

This project implements a **Clean Architecture + Domain-Driven Design (DDD) + Vertical Slice Architecture** hybrid approach using .NET 10.0. It's designed as a modular monolith that can evolve into microservices without requiring architectural changes.

## Table of Contents

- [Architecture Patterns](#architecture-patterns)
- [Project Structure](#project-structure)
- [Module Communication](#module-communication)
- [Layer Responsibilities](#layer-responsibilities)
- [Technology Stack](#technology-stack)
- [Design Patterns](#design-patterns)
- [Configuration Strategy](#configuration-strategy)
- [Database Strategy](#database-strategy)
- [Testing Strategy](#testing-strategy)

---

## Architecture Patterns

### Clean Architecture

- **Dependency Rule**: Dependencies point inward (Domain ← Logic ← Api ← Module)
- **Core Principle**: Business logic has no dependencies on infrastructure
- **Benefit**: Easy to test, framework-independent, database-independent

### Domain-Driven Design (DDD)

- **Bounded Contexts**: Each module (Tasks, Orders, etc.) represents a bounded context
- **Ubiquitous Language**: Code uses business terminology
- **Domain Layer**: Pure business rules and entities with no infrastructure dependencies

### Vertical Slice Architecture

- **Feature-Based Organization**: Each feature is self-contained within its module
- **CQRS with MediatR**: Commands and queries separated, handled independently
- **Minimal Coupling**: Modules communicate through well-defined contracts

---

## Project Structure

```text
Todo/
├── Shared/
│   └── Todo.Shared.Kernel/              # Cross-cutting primitives
│       ├── (No packages - pure C#)
│       └── Base classes, value objects, common interfaces
│
├── Tasks/                                # Bounded Context: Task Management
│   ├── Todo.Tasks.Contracts/            # Public API (DTOs + Interfaces)
│   │   ├── Requests/                    # Input DTOs
│   │   ├── Responses/                   # Output DTOs
│   │   ├── Events/                      # Domain events (optional)
│   │   └── Services/                    # Service interfaces (ITaskService)
│   │
│   ├── Todo.Tasks.Domain/               # Business Logic & Entities
│   │   ├── Entities/                    # Domain entities
│   │   ├── ValueObjects/                # Value objects
│   │   ├── Enums/                       # Business enums
│   │   └── Packages: ErrorOr, NodaTime
│   │
│   ├── Todo.Tasks.Database/             # Data Access Layer
│   │   ├── DbContext/                   # EF Core DbContext
│   │   ├── Configurations/              # EF entity configurations
│   │   ├── Repositories/                # Repository implementations
│   │   ├── Migrations/                  # FluentMigrator migrations
│   │   └── Packages: EF Core, Dapper, FluentMigrator
│   │
│   ├── Todo.Tasks.Logic/                # Application Logic (CQRS)
│   │   ├── Commands/                    # MediatR commands
│   │   ├── Queries/                     # MediatR queries
│   │   ├── Handlers/                    # Command/Query handlers
│   │   ├── Validators/                  # FluentValidation validators
│   │   ├── Mappings/                    # Mapster mappings
│   │   ├── Services/                    # Service implementations
│   │   └── Packages: MediatR, FluentValidation, Mapster, ErrorOr
│   │
│   ├── Todo.Tasks.Api/                  # API Endpoints
│   │   ├── Endpoints/                   # FastEndpoints definitions
│   │   └── Packages: FastEndpoints, MediatR
│   │
│   ├── Todo.Tasks.Module/               # Vertical Slice Orchestrator
│   │   ├── DependencyInjection/         # Service registration
│   │   ├── Configuration/               # Module options
│   │   └── Packages: MediatR, FluentValidation DI, Mapster DI
│   │
│   └── Todo.Tasks.Tests/                # Comprehensive Testing
│       ├── Unit/                        # Unit tests
│       ├── Integration/                 # Integration tests
│       ├── Architecture/                # Architecture constraint tests
│       └── Packages: xUnit, Moq, Testcontainers, Bogus, Verify
│
└── Todo.WebHost/                        # Composition Root
    ├── Program.cs                       # Application entry point
    ├── appsettings.json                 # Configuration
    └── Packages: Serilog, OpenTelemetry, Auth, Health Checks
```

---

## Module Communication

### Within a Module (Internal)

Use MediatR commands/queries:

```csharp
// Todo.Tasks.Api/Endpoints/CreateTaskEndpoint.cs
public class CreateTaskEndpoint : Endpoint<CreateTaskRequest, TaskResponse>
{
    private readonly ISender _sender;  // MediatR
    
    public override async Task HandleAsync(CreateTaskRequest req, CancellationToken ct)
    {
        var command = new CreateTaskCommand(req);  // Internal command
        var result = await _sender.Send(command, ct);  // MediatR within module
        
        await result.MatchAsync(
            success => SendOkAsync(success, ct),
            errors => SendErrorsAsync(errors, ct)
        );
    }
}
```

### Between Modules (In-Process)

Use Service Interfaces from Contracts:

```csharp
// Orders module calling Tasks module
public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, ErrorOr<OrderResponse>>
{
    private readonly ITaskService _taskService;  // From Tasks.Contracts
    
    public async Task<ErrorOr<OrderResponse>> Handle(...)
    {
        var task = await _taskService.CreateTaskAsync(
            new CreateTaskRequest { Title = "Process Order" },
            ct
        );  // Cross-module via interface
    }
}
```

### Between Services (Microservices)

Use HTTP/gRPC or Message Bus:

```csharp
// Configuration determines implementation
// Dev:  ServiceMode = "InProcess"  → Uses MediatR
// Prod: ServiceMode = "Http"       → Calls remote API
```

### Event-Driven Communication

Use Domain Events for decoupling:

```csharp
// Tasks module publishes
public record TaskCompletedEvent(Guid TaskId, string Title);

// Orders module subscribes
public class TaskCompletedEventHandler : INotificationHandler<TaskCompletedEvent>
{
    public async Task Handle(TaskCompletedEvent evt, CancellationToken ct)
    {
        // React to task completion
        var command = new UpdateOrderStatusCommand(evt.TaskId);
        await _sender.Send(command, ct);
    }
}
```

---

## Layer Responsibilities

### Contracts Layer

- **Purpose**: Public API surface (DTOs + Service Interfaces)
- **Dependencies**: ZERO packages (pure C#)
- **Contains**: Request/Response DTOs, Events, Service Interfaces
- **Used By**: Other modules, external clients
- **Never Contains**: MediatR commands, validation, mapping, business logic

```csharp
// ✅ Good: Pure DTO
public record CreateTaskRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
}

// ✅ Good: Service interface
public interface ITaskService
{
    Task<TaskResponse> CreateTaskAsync(CreateTaskRequest request, CancellationToken ct);
}

// ❌ Bad: Don't put MediatR in Contracts
public record CreateTaskCommand : IRequest<TaskResponse> { }  // WRONG LAYER
```

### Domain Layer

- **Purpose**: Core business logic and entities
- **Dependencies**: Minimal (ErrorOr, NodaTime)
- **Contains**: Entities, Value Objects, Domain Events, Business Rules
- **Used By**: Logic, Database layers
- **Never Contains**: Infrastructure, database, HTTP concerns

### Database Layer

- **Purpose**: Data access implementation
- **Dependencies**: EF Core, Dapper, FluentMigrator, DB providers
- **Contains**: DbContext, Repositories, Migrations, Entity Configurations
- **Strategy**: Database-first with EF Core, FluentMigrator for schema
- **Dual ORM**: EF Core for writes, Dapper for complex reads

### Logic Layer

- **Purpose**: Application orchestration (CQRS)
- **Dependencies**: MediatR, FluentValidation, Mapster, ErrorOr
- **Contains**: Commands, Queries, Handlers, Validators, Mappings, Service implementations
- **Pattern**: Each command/query is a vertical slice with its handler

```csharp
// Command wraps the contract
public record CreateTaskCommand(CreateTaskRequest Request) 
    : IRequest<ErrorOr<TaskResponse>>;

// Handler implements business logic
public class CreateTaskCommandHandler 
    : IRequestHandler<CreateTaskCommand, ErrorOr<TaskResponse>>
{
    // Business logic here
}
```

### API Layer

- **Purpose**: HTTP endpoint definitions
- **Dependencies**: FastEndpoints, MediatR
- **Contains**: Endpoint classes
- **Responsibility**: Receive HTTP, wrap in command, send via MediatR, return HTTP

### Module Layer

- **Purpose**: Dependency injection and configuration
- **Dependencies**: DI abstractions, FluentValidation DI, Mapster DI
- **Contains**: Service registration, module configuration
- **Pattern**: Extension methods for `IServiceCollection`

---

## Technology Stack

### Core Framework

- **.NET 10.0** - Latest framework
- **C# Latest** - Latest language features
- **Nullable Reference Types** - Enabled for null safety

### API Layer

- **FastEndpoints 7.1.1** - REPR pattern, high-performance endpoints
- **FastEndpoints.Swagger** - API documentation
- **FastEndpoints.Security** - JWT authentication helpers

### Application Layer

- **MediatR 13.1.0** - CQRS implementation
- **FluentValidation 12.1.0** - Request validation
- **Mapster 7.4.0** - Object mapping (faster than AutoMapper)
- **ErrorOr 2.0.1** - Type-safe error handling

### Data Access

- **Entity Framework Core 10.0** - ORM for complex queries
- **Dapper 2.1.66** - Micro-ORM for performance-critical reads
- **FluentMigrator 7.1.0** - Database migrations
- **Multi-Database Support**: SQL Server, PostgreSQL, MySQL

### Observability

- **Serilog 4.1.0** - Structured logging
- **OpenTelemetry 1.10.0** - Distributed tracing
- **Health Checks** - Endpoint monitoring

### Resilience

- **Polly 8.5.0** - Retry, circuit breaker, timeout policies

### Date/Time

- **NodaTime 3.2.2** - Better date/time handling than `DateTime`

### Testing

- **xUnit 2.9.3** - Test framework
- **Moq 4.20.72** - Mocking
- **FluentAssertions 7.0.0** - Readable assertions
- **Testcontainers** - Real database integration tests
- **Bogus 35.6.1** - Test data generation
- **AutoFixture 4.18.1** - Automatic test fixtures
- **Respawn 6.2.1** - Database cleanup between tests
- **Verify 28.5.1** - Snapshot testing
- **NetArchTest.Rules 1.3.2** - Architecture constraint testing

---

## Design Patterns

### CQRS (Command Query Responsibility Segregation)

```csharp
// Commands (writes)
public record CreateTaskCommand(CreateTaskRequest Request) 
    : IRequest<ErrorOr<TaskResponse>>;

// Queries (reads)
public record GetTaskQuery(Guid TaskId) 
    : IRequest<ErrorOr<TaskResponse>>;
```

### Repository Pattern

```csharp
public interface ITaskRepository
{
    Task<TaskEntity?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(TaskEntity task, CancellationToken ct);
    Task UpdateAsync(TaskEntity task, CancellationToken ct);
}
```

### Result Pattern (Railway-Oriented Programming)

```csharp
public async Task<ErrorOr<TaskResponse>> Handle(...)
{
    // Validation
    if (string.IsNullOrEmpty(request.Title))
        return Error.Validation("Title is required");
    
    // Business logic
    var task = CreateTask(request);
    
    // Success
    return MapToResponse(task);
}
```

### Decorator Pattern (MediatR Pipeline Behaviors)

```csharp
// Automatic validation for all commands
public class ValidationBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    
    public async Task<TResponse> Handle(...)
    {
        var failures = _validators
            .Select(v => v.Validate(request))
            .SelectMany(result => result.Errors)
            .Where(f => f != null)
            .ToList();
        
        if (failures.Any())
            throw new ValidationException(failures);
        
        return await next();
    }
}
```

### Strategy Pattern (Configurable Service Implementation)

```csharp
// Same interface, different implementations
services.AddScoped<ITaskService>(sp =>
{
    var config = sp.GetRequiredService<IOptions<TasksModuleOptions>>().Value;
    
    return config.ServiceMode switch
    {
        "InProcess" => new TaskService(sp.GetRequiredService<ISender>()),
        "Http" => new TaskHttpService(sp.GetRequiredService<HttpClient>()),
        "MessageBus" => new TaskMessageBusService(sp.GetRequiredService<IMessageBus>()),
        _ => throw new InvalidOperationException()
    };
});
```

---

## Configuration Strategy

### Module Configuration

```json
{
  "Modules": {
    "Tasks": {
      "Enabled": true,
      "ServiceMode": "InProcess",  // or "Http", "MessageBus"
      "HttpServiceUrl": "https://tasks-api.example.com",
      "Features": {
        "EnableNotifications": true,
        "EnableAuditLog": false,
        "UseRedisCache": true,
        "DatabaseProvider": "SqlServer"
      }
    }
  }
}
```

### Environment-Specific Configuration

```text
appsettings.json                 # Base settings
appsettings.Development.json     # Local development
appsettings.Staging.json         # Staging environment
appsettings.Production.json      # Production environment
```

### Module Registration

```csharp
// Todo.WebHost/Program.cs
var builder = WebApplication.CreateBuilder(args);

// Modules register themselves based on configuration
builder.Services.AddTasksModule(builder.Configuration);
builder.Services.AddOrdersModule(builder.Configuration);

var app = builder.Build();
app.Run();
```

---

## Database Strategy

### Database-First Approach

1. **Design Schema**: Use FluentMigrator or SQL scripts
2. **Apply Migrations**: Pipeline deploys DDL changes
3. **Scaffold Entities**: `dotnet ef dbcontext scaffold` generates entity classes
4. **Use EF + Dapper**: EF for writes, Dapper for complex reads

### Migration Strategy

- **FluentMigrator**: Migrations as C# classes (version controlled)
- **Pipeline Deployment**: Migrations run via CI/CD, NOT at application startup
- **Database-First**: Schema is authoritative, code follows

```csharp
// FluentMigrator migration example
[Migration(20251120001)]
public class CreateTasksTable : Migration
{
    public override void Up()
    {
        Create.Table("Tasks")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("Title").AsString(200).NotNullable()
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable();
    }
    
    public override void Down()
    {
        Delete.Table("Tasks");
    }
}
```

### Multi-Database Support

Configuration determines which database provider is used:

- **SQL Server**: Production default
- **PostgreSQL**: Alternative for Linux deployments
- **MySQL**: Alternative for specific requirements

---

## Testing Strategy

### Unit Tests

- **Scope**: Individual handlers, validators, domain logic
- **Tools**: xUnit, Moq, FluentAssertions, AutoFixture
- **Pattern**: AAA (Arrange, Act, Assert)

```csharp
public class CreateTaskHandlerTests
{
    [Fact]
    public async Task Handle_ValidRequest_ReturnsTaskResponse()
    {
        // Arrange
        var repository = new Mock<ITaskRepository>();
        var handler = new CreateTaskCommandHandler(repository.Object);
        var command = new CreateTaskCommand(new CreateTaskRequest { Title = "Test" });
        
        // Act
        var result = await handler.Handle(command, CancellationToken.None);
        
        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Title.Should().Be("Test");
    }
}
```

### Integration Tests

- **Scope**: Full request/response cycles, database interactions
- **Tools**: Testcontainers, Respawn, Microsoft.AspNetCore.Mvc.Testing
- **Pattern**: Real database in Docker, clean between tests

```csharp
public class TasksApiTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly SqlServerContainer _dbContainer;
    
    [Fact]
    public async Task CreateTask_ValidRequest_Returns201()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new CreateTaskRequest { Title = "Integration Test" };
        
        // Act
        var response = await client.PostAsJsonAsync("/api/tasks", request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
```

### Architecture Tests

- **Scope**: Enforce architectural rules
- **Tools**: NetArchTest.Rules
- **Purpose**: Prevent architectural violations

```csharp
[Fact]
public void Domain_Should_Not_HaveDependencyOn_Infrastructure()
{
    var result = Types.InAssembly(typeof(TaskEntity).Assembly)
        .Should().NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
        .GetResult();
    
    result.IsSuccessful.Should().BeTrue();
}
```

### Snapshot Tests

- **Scope**: API response verification
- **Tools**: Verify
- **Purpose**: Detect unintended changes

---

## Build Configuration

### Central Package Management

- **Directory.Packages.props**: All package versions defined once
- **Project files**: Reference packages without versions
- **Benefit**: No version conflicts, easy updates

### Code Quality

- **Analyzers**: StyleCop, Roslynator, SonarAnalyzer, AsyncFixer, SecurityCodeScan
- **Nullable**: Warnings as errors
- **EditorConfig**: Consistent code style enforced

### Deterministic Builds

- Enabled for reproducible CI/CD builds
- Same source = same binary output

---

## Migration Paths

### Modular Monolith → Microservices

**Phase 1: Current State**

```text
All modules in-process, communicate via IService interfaces
```

**Phase 2: Extract First Service**

```json
{
  "Modules": {
    "Tasks": {
      "ServiceMode": "Http",
      "HttpServiceUrl": "https://tasks-service.example.com"
    },
    "Orders": {
      "ServiceMode": "InProcess"  // Still in monolith
    }
  }
}
```

**Phase 3: Full Microservices**

```text
All modules extracted, communicate via HTTP/gRPC
No code changes required, only configuration
```

---

## Key Principles

1. **Dependency Inversion**: Abstractions over implementations
2. **Single Responsibility**: Each layer has one reason to change
3. **Open/Closed**: Open for extension, closed for modification
4. **Interface Segregation**: Clients depend on minimal interfaces
5. **Don't Repeat Yourself**: Share via contracts, not implementation
6. **Separation of Concerns**: Each module manages its own domain
7. **Configuration over Code**: Behavior driven by configuration
8. **Testability**: Every layer independently testable

---

## References

- [Clean Architecture by Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Domain-Driven Design by Eric Evans](https://www.domainlanguage.com/ddd/)
- [Vertical Slice Architecture](https://jimmybogard.com/vertical-slice-architecture/)
- [FastEndpoints Documentation](https://fast-endpoints.com/)
- [MediatR Documentation](https://github.com/jbogard/MediatR)

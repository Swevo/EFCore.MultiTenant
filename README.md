# Swevo.EFCore.MultiTenant

[![NuGet](https://img.shields.io/nuget/v/Swevo.EFCore.MultiTenant.svg)](https://www.nuget.org/packages/Swevo.EFCore.MultiTenant)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Swevo.EFCore.MultiTenant.svg)](https://www.nuget.org/packages/Swevo.EFCore.MultiTenant)
[![CI](https://github.com/Swevo/EFCore.MultiTenant/actions/workflows/ci.yml/badge.svg)](https://github.com/Swevo/EFCore.MultiTenant/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Compile-time multi-tenancy for EF Core. Stamp a `[Tenant]` attribute on any entity and the source generator wires up `TenantId`, a save interceptor, and global query filters automatically.

## Quick-start

```bash
dotnet add package Swevo.EFCore.MultiTenant
```

### 1 — Mark your entities

```csharp
using EFCore.MultiTenant;

[Tenant]
public partial class Order
{
    public int Id { get; set; }
    public string Description { get; set; } = "";
    // TenantId { get; set; } is generated automatically
}
```

> The class **must** be `partial`. Non-partial classes produce diagnostic `MTNT001`.

### 2 — Implement `ITenantContext`

```csharp
public class HttpTenantContext(IHttpContextAccessor http) : ITenantContext
{
    public string TenantId =>
        http.HttpContext?.Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "";
}
```

Register it as scoped in your DI container:

```csharp
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
```

### 3 — Configure `DbContext`

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant)
    : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.AddTenantQueryFilters(tenant);

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.AddTenantInterceptor(tenant);
}
```

That's it. Every `SaveChanges` / `SaveChangesAsync` call stamps `TenantId` on new entities, and every query automatically filters by the current tenant.

## How it works

| Piece | What it does |
|-------|-------------|
| `[Tenant]` attribute | Marks the entity for code generation |
| Source generator | Emits `TenantId` property + `ITenantEntity` implementation |
| `TenantInterceptor` | `ISaveChangesInterceptor` — stamps `TenantId` on `Added` entries |
| `AddTenantQueryFilters` | Adds a global `WHERE TenantId = @current` filter to every tenant entity |

## Bypass the filter

Use `IgnoreQueryFilters()` for admin/reporting queries:

```csharp
var allOrders = await db.Orders.IgnoreQueryFilters().ToListAsync();
```

## Stacking with other Swevo packages

```csharp
[Tenant]   // Swevo.EFCore.MultiTenant — adds TenantId
[Auditable] // Swevo.AutoAudit — adds CreatedAt / UpdatedAt
[SoftDelete] // Swevo.EFCore.SoftDelete — adds IsDeleted
public partial class Order { ... }
```

```csharp
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    optionsBuilder
        .AddTenantInterceptor(tenant)
        .AddAuditInterceptor(clock)
        .AddSoftDeleteInterceptor();
}

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.AddTenantQueryFilters(tenant);
    modelBuilder.AddSoftDeleteQueryFilters();
}
```

## Requirements

- .NET 8+
- EF Core 8+


## Also by the same author

> 🌐 Full suite overview: **[swevo.github.io](https://swevo.github.io/)**

| Package | Description |
|---|---|
| [**AutoLog.Generator**](https://github.com/Swevo/AutoLog.Generator) | Compile-time high-performance logging — `[Log(Level, Message)]` generates `LoggerMessage.Define`. AOT-safe. |
| [**AutoHttpClient.Generator**](https://github.com/Swevo/AutoHttpClient.Generator) | Compile-time typed HTTP client — `[HttpClient]` on an interface generates a strongly-typed client. AOT-safe Refit alternative. |
| [**AutoDispatch.Generator**](https://github.com/Swevo/AutoDispatch.Generator) | Compile-time CQRS dispatcher — `[Handler]` generates a strongly-typed `IDispatcher`. No MediatR, no reflection. |
| [**AutoWire**](https://github.com/Swevo/AutoWire) | Compile-time DI auto-registration — `[Scoped]`/`[Singleton]`/`[Transient]` generates `IServiceCollection` registration code. |
| [**AutoMap.Generator**](https://github.com/Swevo/AutoMap.Generator) | Compile-time object mapping with generated extension methods. AOT-safe AutoMapper alternative. |

## Related Packages

| Package | Downloads | Description |
|---|---|---|
| [Swevo.EFCore.Outbox](https://www.nuget.org/packages/Swevo.EFCore.Outbox) | [![Downloads](https://img.shields.io/nuget/dt/Swevo.EFCore.Outbox.svg)](https://www.nuget.org/packages/Swevo.EFCore.Outbox) | Transactional outbox pattern for EF Core + AutoBus |
| [Swevo.EFCore.StronglyTyped](https://www.nuget.org/packages/Swevo.EFCore.StronglyTyped) | [![Downloads](https://img.shields.io/nuget/dt/Swevo.EFCore.StronglyTyped.svg)](https://www.nuget.org/packages/Swevo.EFCore.StronglyTyped) | Compile-time strongly-typed ID generation for  |
| [Swevo.EFCore.SoftDelete](https://www.nuget.org/packages/Swevo.EFCore.SoftDelete) | [![Downloads](https://img.shields.io/nuget/dt/Swevo.EFCore.SoftDelete.svg)](https://www.nuget.org/packages/Swevo.EFCore.SoftDelete) | Compile-time soft-delete generation for EF Core entities using Roslyn source generators |
| [Swevo.EFCore.Seeding](https://www.nuget.org/packages/Swevo.EFCore.Seeding) | [![Downloads](https://img.shields.io/nuget/dt/Swevo.EFCore.Seeding.svg)](https://www.nuget.org/packages/Swevo.EFCore.Seeding) | Fluent, idempotent, dependency-ordered seed data for EF Core |
| [Swevo.EFCore.Pagination](https://www.nuget.org/packages/Swevo.EFCore.Pagination) | [![Downloads](https://img.shields.io/nuget/dt/Swevo.EFCore.Pagination.svg)](https://www.nuget.org/packages/Swevo.EFCore.Pagination) | Offset and cursor-based pagination for EF Core |
| [Swevo.EFCore.JsonColumn](https://www.nuget.org/packages/Swevo.EFCore.JsonColumn) | [![Downloads](https://img.shields.io/nuget/dt/Swevo.EFCore.JsonColumn.svg)](https://www.nuget.org/packages/Swevo.EFCore.JsonColumn) | Compile-time JSON column configuration for EF Core 8+ — [JsonColumn] on owned navigation properties generates ConfigureJsonColumns(ModelBuilder) with OwnsOne( |
| [Swevo.EFCore.BulkOperations](https://www.nuget.org/packages/Swevo.EFCore.BulkOperations) | [![Downloads](https://img.shields.io/nuget/dt/Swevo.EFCore.BulkOperations.svg)](https://www.nuget.org/packages/Swevo.EFCore.BulkOperations) | Free, MIT-licensed bulk insert/update/delete for EF Core |
| [Swevo.EFCore.RowVersion](https://www.nuget.org/packages/Swevo.EFCore.RowVersion) | [![Downloads](https://img.shields.io/nuget/dt/Swevo.EFCore.RowVersion.svg)](https://www.nuget.org/packages/Swevo.EFCore.RowVersion) | Compile-time optimistic concurrency for EF Core — [Optimistic] source generator adds RowVersion property, IOptimisticEntity, and SaveChangesClientWinsAsync / SaveChangesDatabaseWinsAsync retry extensions |

---

## License

MIT

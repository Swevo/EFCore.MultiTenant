# Swevo.EFCore.MultiTenant

[![NuGet](https://img.shields.io/nuget/v/Swevo.EFCore.MultiTenant
[![NuGet Downloads](https://img.shields.io/nuget/dt/Swevo.EFCore.MultiTenant.svg)](https://www.nuget.org/packages/Swevo.EFCore.MultiTenant).svg)](https://www.nuget.org/packages/Swevo.EFCore.MultiTenant)
[![CI](https://github.com/Swevo/EFCore.MultiTenant/actions/workflows/ci.yml/badge.svg)](https://github.com/Swevo/EFCore.MultiTenant/actions)

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

## License

MIT

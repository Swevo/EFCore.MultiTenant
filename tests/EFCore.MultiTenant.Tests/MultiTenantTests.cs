using EFCore.MultiTenant;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace EFCore.MultiTenant.Tests;

// ── Test model ────────────────────────────────────────────────────────────────

[Tenant]
public partial class Order
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

[Tenant]
public partial class Invoice
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
}

public class FakeTenantContext(string tenantId) : ITenantContext
{
    public string TenantId { get; set; } = tenantId;
}

public class TestDbContext(DbContextOptions<TestDbContext> options, ITenantContext tenantContext)
    : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.AddTenantQueryFilters(tenantContext);

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.AddTenantInterceptor(tenantContext);
}

file static class Helpers
{
    // Root per-database so shared InMemory data survives separate service providers
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, InMemoryDatabaseRoot> s_roots = new();

    public static (TestDbContext ctx, FakeTenantContext tenant) CreateContext(
        string tenantId, string? dbName = null)
    {
        var name = dbName ?? System.Guid.NewGuid().ToString();
        var root = s_roots.GetOrAdd(name, _ => new InMemoryDatabaseRoot());
        var tenant = new FakeTenantContext(tenantId);
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(name, root)
            .EnableServiceProviderCaching(false) // prevent model cache sharing across test instances
            .Options;
        return (new TestDbContext(options, tenant), tenant);
    }
}

// ── Generator output tests ────────────────────────────────────────────────────

public class GeneratorOutputTests
{
    private static Dictionary<string, string> RunGenerator(string source)
    {
        var refs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
        };
        try { refs.Add(MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location)); }
        catch { /* best-effort */ }

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [CSharpSyntaxTree.ParseText(source)],
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new MultiTenantGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        return driver.GetRunResult().GeneratedTrees
            .ToDictionary(
                t => System.IO.Path.GetFileName(t.FilePath),
                t => t.GetText().ToString());
    }

    private static IReadOnlyList<Diagnostic> GetDiagnostics(string source)
    {
        var refs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
        };
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [CSharpSyntaxTree.ParseText(source)],
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new MultiTenantGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        return diagnostics;
    }

    [Fact]
    public void CoreTypes_AreAlwaysEmitted()
    {
        var files = RunGenerator("");
        files.Should().ContainKey("EFCore.MultiTenant.Core.g.cs");
    }

    [Fact]
    public void CoreTypes_ContainsTenantAttribute()
    {
        var files = RunGenerator("");
        files["EFCore.MultiTenant.Core.g.cs"].Should().Contain("class TenantAttribute");
    }

    [Fact]
    public void CoreTypes_ContainsITenantContext()
    {
        var files = RunGenerator("");
        files["EFCore.MultiTenant.Core.g.cs"].Should().Contain("interface ITenantContext");
    }

    [Fact]
    public void CoreTypes_ContainsITenantEntity()
    {
        var files = RunGenerator("");
        files["EFCore.MultiTenant.Core.g.cs"].Should().Contain("interface ITenantEntity");
    }

    [Fact]
    public void CoreTypes_ContainsTenantInterceptor()
    {
        var files = RunGenerator("");
        files["EFCore.MultiTenant.Core.g.cs"].Should().Contain("class TenantInterceptor");
    }

    [Fact]
    public void CoreTypes_ContainsAddTenantInterceptorExtension()
    {
        var files = RunGenerator("");
        files["EFCore.MultiTenant.Core.g.cs"].Should().Contain("AddTenantInterceptor");
    }

    [Fact]
    public void CoreTypes_ContainsAddTenantQueryFiltersExtension()
    {
        var files = RunGenerator("");
        files["EFCore.MultiTenant.Core.g.cs"].Should().Contain("AddTenantQueryFilters");
    }

    [Fact]
    public void PartialClass_GeneratesTenantIdProperty()
    {
        var source = @"
using EFCore.MultiTenant;
[Tenant]
public partial class Order { public int Id { get; set; } }";
        var output = RunGenerator(source)["EFCore.MultiTenant.Order.g.cs"];
        output.Should().Contain("public string TenantId { get; set; }");
    }

    [Fact]
    public void PartialClass_ImplementsITenantEntity()
    {
        var source = @"
using EFCore.MultiTenant;
[Tenant]
public partial class Order { public int Id { get; set; } }";
        var output = RunGenerator(source)["EFCore.MultiTenant.Order.g.cs"];
        output.Should().Contain(": global::EFCore.MultiTenant.ITenantEntity");
    }

    [Fact]
    public void NamespacedClass_WrapsInNamespace()
    {
        var source = @"
using EFCore.MultiTenant;
namespace MyApp.Domain
{
    [Tenant]
    public partial class Order { public int Id { get; set; } }
}";
        var output = RunGenerator(source)["EFCore.MultiTenant.Order.g.cs"];
        output.Should().Contain("namespace MyApp.Domain");
        output.Should().Contain("public partial class Order");
    }

    [Fact]
    public void NonPartialClass_ReportsMTNT001()
    {
        var source = @"
using EFCore.MultiTenant;
[Tenant]
public class Order { public int Id { get; set; } }";
        var diagnostics = GetDiagnostics(source);
        diagnostics.Should().ContainSingle(d => d.Id == "MTNT001");
    }

    [Fact]
    public void NonPartialClass_DoesNotGenerateFile()
    {
        var source = @"
using EFCore.MultiTenant;
[Tenant]
public class Order { public int Id { get; set; } }";
        var files = RunGenerator(source);
        files.Should().NotContainKey("EFCore.MultiTenant.Order.g.cs");
    }

    [Fact]
    public void PartialClass_HasAutoGeneratedComment()
    {
        var source = @"
using EFCore.MultiTenant;
[Tenant]
public partial class Order { }";
        var output = RunGenerator(source)["EFCore.MultiTenant.Order.g.cs"];
        output.Should().Contain("// <auto-generated by Swevo.EFCore.MultiTenant/>");
    }

    [Fact]
    public void ValidClass_NoMTNT001Diagnostic()
    {
        var source = @"
using EFCore.MultiTenant;
[Tenant]
public partial class Order { }";
        var diagnostics = GetDiagnostics(source);
        diagnostics.Should().NotContain(d => d.Id == "MTNT001");
    }
}

// ── Runtime / EF Core integration tests ──────────────────────────────────────

public class TenantInterceptorTests
{
    [Fact]
    public async Task AddedEntity_TenantIdIsStamped()
    {
        var dbName = System.Guid.NewGuid().ToString();
        var (ctx, _) = Helpers.CreateContext("tenant-a", dbName);
        using (ctx)
        {
            ctx.Orders.Add(new Order { Id = 1, Name = "Order A" });
            await ctx.SaveChangesAsync();
        }

        var (verify, _) = Helpers.CreateContext("tenant-a", dbName);
        using (verify)
        {
            var order = await verify.Orders.IgnoreQueryFilters().SingleAsync();
            order.TenantId.Should().Be("tenant-a");
        }
    }

    [Fact]
    public async Task AddedEntity_TenantIdReflectsCurrentTenant()
    {
        var dbName = System.Guid.NewGuid().ToString();
        // Use one context with mutable tenant to avoid EF Core model caching issues
        var (ctx, tenant) = Helpers.CreateContext("tenant-a", dbName);
        using (ctx)
        {
            ctx.Orders.Add(new Order { Id = 1, Name = "A's Order" });
            await ctx.SaveChangesAsync();

            tenant.TenantId = "tenant-b";
            ctx.Orders.Add(new Order { Id = 2, Name = "B's Order" });
            await ctx.SaveChangesAsync();

            var all = await ctx.Orders.IgnoreQueryFilters().ToListAsync();
            all.Should().HaveCount(2);
            all.Single(o => o.Id == 1).TenantId.Should().Be("tenant-a");
            all.Single(o => o.Id == 2).TenantId.Should().Be("tenant-b");
        }
    }

    [Fact]
    public async Task AsyncSave_TenantIdIsStamped()
    {
        var dbName = System.Guid.NewGuid().ToString();
        var (ctx, _) = Helpers.CreateContext("tenant-async", dbName);
        using (ctx)
        {
            ctx.Orders.Add(new Order { Id = 10, Name = "Async" });
            await ctx.SaveChangesAsync();
        }

        var (verify, _) = Helpers.CreateContext("tenant-async", dbName);
        using (verify)
        {
            var order = await verify.Orders.IgnoreQueryFilters().SingleAsync();
            order.TenantId.Should().Be("tenant-async");
        }
    }
}

public class QueryFilterTests
{
    [Fact]
    public async Task QueryFilter_ReturnsOnlyCurrentTenantRows()
    {
        var (ctx, tenant) = Helpers.CreateContext("a");
        using (ctx)
        {
            ctx.Orders.Add(new Order { Id = 1, Name = "A1" });
            ctx.Orders.Add(new Order { Id = 2, Name = "A2" });
            await ctx.SaveChangesAsync();

            tenant.TenantId = "b";
            ctx.Orders.Add(new Order { Id = 3, Name = "B1" });
            await ctx.SaveChangesAsync();

            tenant.TenantId = "a";
            var ordersA = await ctx.Orders.ToListAsync();
            ordersA.Should().HaveCount(2);
            ordersA.Should().AllSatisfy(o => o.TenantId.Should().Be("a"));
        }
    }

    [Fact]
    public async Task QueryFilter_OtherTenantRowsAreHidden()
    {
        var (ctx, tenant) = Helpers.CreateContext("a");
        using (ctx)
        {
            ctx.Orders.Add(new Order { Id = 1, Name = "A1" });
            await ctx.SaveChangesAsync();

            tenant.TenantId = "b";
            var orders = await ctx.Orders.ToListAsync();
            orders.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task IgnoreQueryFilters_ReturnsAllRows()
    {
        var (ctx, tenant) = Helpers.CreateContext("a");
        using (ctx)
        {
            ctx.Orders.Add(new Order { Id = 1, Name = "A" });
            await ctx.SaveChangesAsync();

            tenant.TenantId = "b";
            ctx.Orders.Add(new Order { Id = 2, Name = "B" });
            await ctx.SaveChangesAsync();

            var all = await ctx.Orders.IgnoreQueryFilters().ToListAsync();
            all.Should().HaveCount(2);
        }
    }

    [Fact]
    public async Task QueryFilter_WorksAcrossMultipleEntityTypes()
    {
        var (ctx, tenant) = Helpers.CreateContext("a");
        using (ctx)
        {
            ctx.Orders.Add(new Order { Id = 1 });
            ctx.Invoices.Add(new Invoice { Id = 1, Amount = 99.99m });
            await ctx.SaveChangesAsync();

            tenant.TenantId = "b";
            var orders   = await ctx.Orders.ToListAsync();
            var invoices = await ctx.Invoices.ToListAsync();
            orders.Should().BeEmpty();
            invoices.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task QueryFilter_ChangingTenant_FiltersCorrectly()
    {
        var (ctx, tenant) = Helpers.CreateContext("x");
        using (ctx)
        {
            ctx.Orders.Add(new Order { Id = 1, Name = "X" });
            await ctx.SaveChangesAsync();

            tenant.TenantId = "y";
            ctx.Orders.Add(new Order { Id = 2, Name = "Y" });
            await ctx.SaveChangesAsync();

            tenant.TenantId = "x";
            (await ctx.Orders.CountAsync()).Should().Be(1);

            tenant.TenantId = "y";
            (await ctx.Orders.CountAsync()).Should().Be(1);
        }
    }
}

public class GeneratedTypeTests
{
    [Fact]
    public void Order_ImplementsITenantEntity()
        => typeof(Order).Should().Implement<ITenantEntity>();

    [Fact]
    public void Order_HasTenantIdProperty()
    {
        var order = new Order();
        order.TenantId.Should().BeEmpty();
        order.TenantId = "abc";
        order.TenantId.Should().Be("abc");
    }

    [Fact]
    public void Invoice_ImplementsITenantEntity()
        => typeof(Invoice).Should().Implement<ITenantEntity>();
}

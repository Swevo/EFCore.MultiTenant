# Changelog

All notable changes to `Swevo.EFCore.MultiTenant` will be documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-01-01

### Added
- `[Tenant]` attribute for marking tenant-scoped entities
- Source generator that emits `TenantId` property and `ITenantEntity` implementation on decorated partial classes
- `TenantInterceptor` (`ISaveChangesInterceptor`) that stamps `TenantId` on new entities at save time
- `AddTenantInterceptor(ITenantContext)` extension on `DbContextOptionsBuilder`
- `AddTenantQueryFilters(ITenantContext)` extension on `ModelBuilder` — registers a global `WHERE TenantId = @current` filter for every entity implementing `ITenantEntity`
- `ITenantContext` and `ITenantEntity` interfaces
- Diagnostic `MTNT001` for non-partial classes decorated with `[Tenant]`
- Support for namespaced entities
- Compatible with `IgnoreQueryFilters()` for admin/cross-tenant queries

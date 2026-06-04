using CivicOps.Domain.Entities;
using CivicOps.Domain.Enums;

namespace CivicOps.Domain.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(T entity, CancellationToken ct = default);
}

public interface IVehicleRepository : IRepository<Vehicle>
{
    Task<IEnumerable<Vehicle>> GetByTenantAsync(Guid tenantId, bool activeOnly = true, CancellationToken ct = default);
    Task<Vehicle?> GetByRegistrationAsync(Guid tenantId, string registration, CancellationToken ct = default);
    Task<IEnumerable<Vehicle>> GetAvailableForDispatchAsync(Guid tenantId, CancellationToken ct = default);
    Task<IEnumerable<Vehicle>> GetWithinRadiusAsync(Guid tenantId, double lat, double lng, double radiusKm, CancellationToken ct = default);
    Task<Vehicle?> GetByGpsDeviceIdAsync(string deviceId, CancellationToken ct = default);
}

public interface IIncidentRepository : IRepository<Incident>
{
    Task<IEnumerable<Incident>> GetByTenantAsync(Guid tenantId, IncidentStatus? status = null, CancellationToken ct = default);
    Task<Incident?> GetByReferenceNoAsync(Guid tenantId, string referenceNo, CancellationToken ct = default);
    Task<IEnumerable<Incident>> GetOpenIncidentsAsync(Guid tenantId, CancellationToken ct = default);
    Task<IEnumerable<Incident>> GetSlaAtRiskAsync(Guid tenantId, CancellationToken ct = default);
    Task<string> GenerateReferenceNoAsync(Guid tenantId, CancellationToken ct = default);
}

public interface IDispatchRepository : IRepository<DispatchAssignment>
{
    Task<IEnumerable<DispatchAssignment>> GetActiveByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<IEnumerable<DispatchAssignment>> GetByVehicleAsync(Guid vehicleId, CancellationToken ct = default);
    Task<DispatchAssignment?> GetActiveByVehicleAsync(Guid vehicleId, CancellationToken ct = default);
}

public interface IGpsRepository
{
    Task AddEventAsync(VehicleGpsEvent gpsEvent, CancellationToken ct = default);
    Task AddBatchAsync(IEnumerable<VehicleGpsEvent> events, CancellationToken ct = default);
    Task<IEnumerable<VehicleGpsEvent>> GetHistoryAsync(Guid vehicleId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<VehicleGpsEvent?> GetLatestAsync(Guid vehicleId, CancellationToken ct = default);
}

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<IEnumerable<User>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<IEnumerable<User>> GetByRoleAsync(Guid tenantId, UserRole role, CancellationToken ct = default);
    Task<RefreshToken?> GetRefreshTokenAsync(string token, CancellationToken ct = default);
    Task AddRefreshTokenAsync(RefreshToken token, CancellationToken ct = default);
}

public interface ITenantRepository : IRepository<Tenant>
{
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Tenant?> GetByDomainAsync(string domain, CancellationToken ct = default);
}

public interface IMaintenanceRepository : IRepository<MaintenanceSchedule>
{
    Task<IEnumerable<MaintenanceSchedule>> GetDueSchedulesAsync(Guid tenantId, CancellationToken ct = default);
    Task<IEnumerable<MaintenanceSchedule>> GetByVehicleAsync(Guid vehicleId, CancellationToken ct = default);
    Task<IEnumerable<MaintenanceRecord>> GetRecordsByVehicleAsync(Guid vehicleId, int take = 20, CancellationToken ct = default);
    Task AddRecordAsync(MaintenanceRecord record, CancellationToken ct = default);
}

public interface IGeofenceRepository : IRepository<Geofence>
{
    Task<IEnumerable<Geofence>> GetActiveByTenantAsync(Guid tenantId, CancellationToken ct = default);
}

public interface IUnitOfWork : IAsyncDisposable
{
    IVehicleRepository Vehicles { get; }
    IIncidentRepository Incidents { get; }
    IDispatchRepository Dispatches { get; }
    IGpsRepository GpsEvents { get; }
    IUserRepository Users { get; }
    ITenantRepository Tenants { get; }
    IMaintenanceRepository Maintenance { get; }
    IGeofenceRepository Geofences { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}

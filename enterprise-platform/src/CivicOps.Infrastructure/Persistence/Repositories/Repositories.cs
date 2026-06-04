using CivicOps.Domain.Entities;
using CivicOps.Domain.Enums;
using CivicOps.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CivicOps.Infrastructure.Persistence.Repositories;

public class VehicleRepository : IVehicleRepository
{
    private readonly CivicOpsDbContext _db;
    public VehicleRepository(CivicOpsDbContext db) => _db = db;

    public async Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Vehicles
            .Include(v => v.AssignedDriver)
            .Include(v => v.Region)
            .FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task<IEnumerable<Vehicle>> GetAllAsync(CancellationToken ct = default)
        => await _db.Vehicles.ToListAsync(ct);

    public async Task<IEnumerable<Vehicle>> GetByTenantAsync(Guid tenantId, bool activeOnly = true, CancellationToken ct = default)
    {
        var q = _db.Vehicles.Include(v => v.AssignedDriver).AsQueryable();
        if (activeOnly) q = q.Where(v => v.IsActive);
        return await q.OrderBy(v => v.Registration).ToListAsync(ct);
    }

    public async Task<Vehicle?> GetByRegistrationAsync(Guid tenantId, string registration, CancellationToken ct = default)
        => await _db.Vehicles.FirstOrDefaultAsync(v =>
            v.Registration == registration.ToUpperInvariant(), ct);

    public async Task<IEnumerable<Vehicle>> GetAvailableForDispatchAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.Vehicles
            .Include(v => v.AssignedDriver)
            .Where(v => v.IsActive && v.Status != VehicleStatus.Maintenance
                                   && v.Status != VehicleStatus.OutOfService
                                   && v.Status != VehicleStatus.Offline)
            .ToListAsync(ct);

    public async Task<IEnumerable<Vehicle>> GetWithinRadiusAsync(Guid tenantId, double lat, double lng,
        double radiusKm, CancellationToken ct = default)
    {
        // Use bounding box first for DB efficiency, then Haversine filter in memory
        var latDelta = radiusKm / 111.0;
        var lngDelta = radiusKm / (111.0 * Math.Cos(lat * Math.PI / 180.0));

        var candidates = await _db.Vehicles
            .Where(v => v.IsActive
                && v.LastLatitude >= (decimal)(lat - latDelta)
                && v.LastLatitude <= (decimal)(lat + latDelta)
                && v.LastLongitude >= (decimal)(lng - lngDelta)
                && v.LastLongitude <= (decimal)(lng + lngDelta))
            .ToListAsync(ct);

        return candidates.Where(v =>
            v.LastLatitude.HasValue && v.LastLongitude.HasValue &&
            GeoCalculator.HaversineKm(lat, lng,
                (double)v.LastLatitude.Value, (double)v.LastLongitude.Value) <= radiusKm);
    }

    public async Task<Vehicle?> GetByGpsDeviceIdAsync(string deviceId, CancellationToken ct = default)
        => await _db.Vehicles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.GpsDeviceId == deviceId, ct);

    public async Task AddAsync(Vehicle entity, CancellationToken ct = default)
        => await _db.Vehicles.AddAsync(entity, ct);

    public Task UpdateAsync(Vehicle entity, CancellationToken ct = default)
    {
        _db.Vehicles.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Vehicle entity, CancellationToken ct = default)
    {
        entity.Deactivate();
        _db.Vehicles.Update(entity);
        return Task.CompletedTask;
    }
}

public class IncidentRepository : IIncidentRepository
{
    private readonly CivicOpsDbContext _db;
    public IncidentRepository(CivicOpsDbContext db) => _db = db;

    public async Task<Incident?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Incidents
            .Include(i => i.ReportedBy)
            .Include(i => i.AssignedTo)
            .Include(i => i.AssignedVehicle)
            .Include(i => i.EscalatedTo)
            .Include(i => i.Updates.OrderByDescending(u => u.CreatedAt).Take(5))
                .ThenInclude(u => u.Author)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<IEnumerable<Incident>> GetAllAsync(CancellationToken ct = default)
        => await _db.Incidents.ToListAsync(ct);

    public async Task<IEnumerable<Incident>> GetByTenantAsync(Guid tenantId,
        IncidentStatus? status = null, CancellationToken ct = default)
    {
        var q = _db.Incidents
            .Include(i => i.AssignedTo)
            .Include(i => i.AssignedVehicle)
            .AsQueryable();

        if (status.HasValue)
            q = q.Where(i => i.Status == status.Value);

        return await q.OrderByDescending(i => i.OpenedAt).ToListAsync(ct);
    }

    public async Task<Incident?> GetByReferenceNoAsync(Guid tenantId, string referenceNo, CancellationToken ct = default)
        => await _db.Incidents.FirstOrDefaultAsync(i => i.ReferenceNo == referenceNo, ct);

    public async Task<IEnumerable<Incident>> GetOpenIncidentsAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.Incidents
            .Where(i => i.Status != IncidentStatus.Closed && i.Status != IncidentStatus.Cancelled)
            .OrderBy(i => i.Priority).ThenBy(i => i.OpenedAt)
            .ToListAsync(ct);

    public async Task<IEnumerable<Incident>> GetSlaAtRiskAsync(Guid tenantId, CancellationToken ct = default)
    {
        var incidents = await GetOpenIncidentsAsync(tenantId, ct);
        return incidents.Where(i => i.IsSlaAtRisk());
    }

    public async Task<string> GenerateReferenceNoAsync(Guid tenantId, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow;
        var prefix = $"INC-{today:yyyyMMdd}-";
        var count = await _db.Incidents
            .CountAsync(i => i.ReferenceNo.StartsWith(prefix), ct);
        return $"{prefix}{(count + 1):D4}";
    }

    public async Task AddAsync(Incident entity, CancellationToken ct = default)
        => await _db.Incidents.AddAsync(entity, ct);

    public Task UpdateAsync(Incident entity, CancellationToken ct = default)
    {
        _db.Incidents.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Incident entity, CancellationToken ct = default)
    {
        entity.UpdateStatus(IncidentStatus.Cancelled);
        return Task.CompletedTask;
    }
}

public class DispatchRepository : IDispatchRepository
{
    private readonly CivicOpsDbContext _db;
    public DispatchRepository(CivicOpsDbContext db) => _db = db;

    public async Task<DispatchAssignment?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.DispatchAssignments
            .Include(d => d.Vehicle)
            .Include(d => d.Driver)
            .Include(d => d.Incident)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IEnumerable<DispatchAssignment>> GetAllAsync(CancellationToken ct = default)
        => await _db.DispatchAssignments.ToListAsync(ct);

    public async Task<IEnumerable<DispatchAssignment>> GetActiveByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.DispatchAssignments
            .Include(d => d.Vehicle)
            .Include(d => d.Driver)
            .Include(d => d.Incident)
            .Where(d => d.Status != DispatchStatus.Completed && d.Status != DispatchStatus.Cancelled)
            .OrderByDescending(d => d.Priority).ThenBy(d => d.DispatchedAt)
            .ToListAsync(ct);

    public async Task<IEnumerable<DispatchAssignment>> GetByVehicleAsync(Guid vehicleId, CancellationToken ct = default)
        => await _db.DispatchAssignments
            .Where(d => d.VehicleId == vehicleId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

    public async Task<DispatchAssignment?> GetActiveByVehicleAsync(Guid vehicleId, CancellationToken ct = default)
        => await _db.DispatchAssignments
            .Where(d => d.VehicleId == vehicleId
                        && d.Status != DispatchStatus.Completed
                        && d.Status != DispatchStatus.Cancelled)
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(DispatchAssignment entity, CancellationToken ct = default)
        => await _db.DispatchAssignments.AddAsync(entity, ct);

    public Task UpdateAsync(DispatchAssignment entity, CancellationToken ct = default)
    {
        _db.DispatchAssignments.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(DispatchAssignment entity, CancellationToken ct = default)
    {
        entity.Cancel("Deleted");
        return Task.CompletedTask;
    }
}

public class UserRepository : IUserRepository
{
    private readonly CivicOpsDbContext _db;
    public UserRepository(CivicOpsDbContext db) => _db = db;

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<IEnumerable<User>> GetAllAsync(CancellationToken ct = default)
        => await _db.Users.ToListAsync(ct);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), ct);

    public async Task<IEnumerable<User>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.Users.Where(u => u.IsActive)
            .OrderBy(u => u.FullName).ToListAsync(ct);

    public async Task<IEnumerable<User>> GetByRoleAsync(Guid tenantId, UserRole role, CancellationToken ct = default)
        => await _db.Users.Where(u => u.Role == role && u.IsActive).ToListAsync(ct);

    public async Task<RefreshToken?> GetRefreshTokenAsync(string token, CancellationToken ct = default)
        => await _db.RefreshTokens.Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == token, ct);

    public async Task AddRefreshTokenAsync(RefreshToken token, CancellationToken ct = default)
        => await _db.RefreshTokens.AddAsync(token, ct);

    public async Task AddAsync(User entity, CancellationToken ct = default)
        => await _db.Users.AddAsync(entity, ct);

    public Task UpdateAsync(User entity, CancellationToken ct = default)
    {
        _db.Users.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(User entity, CancellationToken ct = default)
    {
        entity.Deactivate();
        return Task.CompletedTask;
    }
}

public class TenantRepository : ITenantRepository
{
    private readonly CivicOpsDbContext _db;
    public TenantRepository(CivicOpsDbContext db) => _db = db;

    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IEnumerable<Tenant>> GetAllAsync(CancellationToken ct = default)
        => await _db.Tenants.OrderBy(t => t.Name).ToListAsync(ct);

    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => await _db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, ct);

    public async Task<Tenant?> GetByDomainAsync(string domain, CancellationToken ct = default)
        => await _db.Tenants.FirstOrDefaultAsync(t => t.CustomDomain == domain, ct);

    public async Task AddAsync(Tenant entity, CancellationToken ct = default)
        => await _db.Tenants.AddAsync(entity, ct);

    public Task UpdateAsync(Tenant entity, CancellationToken ct = default)
    {
        _db.Tenants.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Tenant entity, CancellationToken ct = default)
    {
        entity.Deactivate();
        return Task.CompletedTask;
    }
}

public class MaintenanceRepository : IMaintenanceRepository
{
    private readonly CivicOpsDbContext _db;
    public MaintenanceRepository(CivicOpsDbContext db) => _db = db;

    public async Task<MaintenanceSchedule?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.MaintenanceSchedules.Include(s => s.Vehicle)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IEnumerable<MaintenanceSchedule>> GetAllAsync(CancellationToken ct = default)
        => await _db.MaintenanceSchedules.ToListAsync(ct);

    public async Task<IEnumerable<MaintenanceSchedule>> GetDueSchedulesAsync(Guid tenantId, CancellationToken ct = default)
    {
        var schedules = await _db.MaintenanceSchedules
            .Include(s => s.Vehicle)
            .Where(s => s.IsActive)
            .ToListAsync(ct);

        return schedules.Where(s =>
            s.Vehicle is not null && s.IsDue(s.Vehicle.OdometerKm));
    }

    public async Task<IEnumerable<MaintenanceSchedule>> GetByVehicleAsync(Guid vehicleId, CancellationToken ct = default)
        => await _db.MaintenanceSchedules
            .Where(s => s.VehicleId == vehicleId && s.IsActive)
            .ToListAsync(ct);

    public async Task<IEnumerable<MaintenanceRecord>> GetRecordsByVehicleAsync(Guid vehicleId,
        int take = 20, CancellationToken ct = default)
        => await _db.MaintenanceRecords
            .Where(r => r.VehicleId == vehicleId)
            .OrderByDescending(r => r.ServicedAt)
            .Take(take)
            .ToListAsync(ct);

    public async Task AddRecordAsync(MaintenanceRecord record, CancellationToken ct = default)
        => await _db.MaintenanceRecords.AddAsync(record, ct);

    public async Task AddAsync(MaintenanceSchedule entity, CancellationToken ct = default)
        => await _db.MaintenanceSchedules.AddAsync(entity, ct);

    public Task UpdateAsync(MaintenanceSchedule entity, CancellationToken ct = default)
    {
        _db.MaintenanceSchedules.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(MaintenanceSchedule entity, CancellationToken ct = default)
    {
        _db.MaintenanceSchedules.Update(entity);
        return Task.CompletedTask;
    }
}

public class GpsRepository : IGpsRepository
{
    private readonly CivicOpsDbContext _db;
    public GpsRepository(CivicOpsDbContext db) => _db = db;

    public async Task AddEventAsync(VehicleGpsEvent gpsEvent, CancellationToken ct = default)
        => await _db.VehicleGpsEvents.AddAsync(gpsEvent, ct);

    public async Task AddBatchAsync(IEnumerable<VehicleGpsEvent> events, CancellationToken ct = default)
        => await _db.VehicleGpsEvents.AddRangeAsync(events, ct);

    public async Task<IEnumerable<VehicleGpsEvent>> GetHistoryAsync(Guid vehicleId,
        DateTime from, DateTime to, CancellationToken ct = default)
        => await _db.VehicleGpsEvents
            .Where(e => e.VehicleId == vehicleId && e.RecordedAt >= from && e.RecordedAt <= to)
            .OrderBy(e => e.RecordedAt)
            .ToListAsync(ct);

    public async Task<VehicleGpsEvent?> GetLatestAsync(Guid vehicleId, CancellationToken ct = default)
        => await _db.VehicleGpsEvents
            .Where(e => e.VehicleId == vehicleId)
            .OrderByDescending(e => e.RecordedAt)
            .FirstOrDefaultAsync(ct);
}

public class GeofenceRepository : IGeofenceRepository
{
    private readonly CivicOpsDbContext _db;
    public GeofenceRepository(CivicOpsDbContext db) => _db = db;

    public async Task<Geofence?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Geofences.FirstOrDefaultAsync(g => g.Id == id, ct);

    public async Task<IEnumerable<Geofence>> GetAllAsync(CancellationToken ct = default)
        => await _db.Geofences.ToListAsync(ct);

    public async Task<IEnumerable<Geofence>> GetActiveByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.Geofences.Where(g => g.IsActive).ToListAsync(ct);

    public async Task AddAsync(Geofence entity, CancellationToken ct = default)
        => await _db.Geofences.AddAsync(entity, ct);

    public Task UpdateAsync(Geofence entity, CancellationToken ct = default)
    {
        _db.Geofences.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Geofence entity, CancellationToken ct = default)
    {
        _db.Geofences.Update(entity);
        return Task.CompletedTask;
    }
}

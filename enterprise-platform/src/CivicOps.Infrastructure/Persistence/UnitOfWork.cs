using CivicOps.Domain.Interfaces;
using CivicOps.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace CivicOps.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly CivicOpsDbContext _db;
    private IDbContextTransaction? _transaction;

    public IVehicleRepository Vehicles { get; }
    public IIncidentRepository Incidents { get; }
    public IDispatchRepository Dispatches { get; }
    public IGpsRepository GpsEvents { get; }
    public IUserRepository Users { get; }
    public ITenantRepository Tenants { get; }
    public IMaintenanceRepository Maintenance { get; }
    public IGeofenceRepository Geofences { get; }

    public UnitOfWork(CivicOpsDbContext db)
    {
        _db = db;
        Vehicles = new VehicleRepository(db);
        Incidents = new IncidentRepository(db);
        Dispatches = new DispatchRepository(db);
        GpsEvents = new GpsRepository(db);
        Users = new UserRepository(db);
        Tenants = new TenantRepository(db);
        Maintenance = new MaintenanceRepository(db);
        Geofences = new GeofenceRepository(db);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(CancellationToken ct = default)
        => _transaction = await _db.Database.BeginTransactionAsync(ct);

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
        {
            await _transaction.CommitAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
            await _transaction.DisposeAsync();
        await _db.DisposeAsync();
    }
}

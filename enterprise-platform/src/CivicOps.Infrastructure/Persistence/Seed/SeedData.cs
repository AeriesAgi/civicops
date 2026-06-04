using CivicOps.Domain.Entities;
using CivicOps.Domain.Enums;
using CivicOps.Infrastructure.Persistence;
using CivicOps.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CivicOps.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds a complete demo operational environment.
/// Simulates Metro Security Solutions operating in Durban, South Africa.
/// Creates realistic GPS tracks, incidents, and assignments for demo/testing.
/// </summary>
public static class SeedData
{
    private static readonly Random _rng = new(42);

    // Durban metro area bounding box
    private const double LatMin = -29.95;
    private const double LatMax = -29.75;
    private const double LngMin = 30.90;
    private const double LngMax = 31.10;

    public static async Task SeedDemoTenantAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicOpsDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CivicOpsDbContext>>();

        // Only seed if no tenants exist
        if (await db.Tenants.IgnoreQueryFilters().AnyAsync())
        {
            logger.LogInformation("Seed data already exists — skipping.");
            return;
        }

        logger.LogInformation("Seeding demo tenant: Metro Security Solutions...");

        // ── TENANT ─────────────────────────────────────────────────
        var tenant = Tenant.Create("Metro Security Solutions", "metro-security", "enterprise");
        tenant.UpdateLimits(50, 100);
        tenant.UpdateBranding(null, "#00D4FF", "#0A0C10", null,
            "Metro Security Solutions", "ops@metrosecurity.co.za");
        db.Tenants.Add(tenant);

        // ── REGIONS ────────────────────────────────────────────────
        var regions = new[]
        {
            Region.Create(tenant.Id, "CBD North", "CBD-N"),
            Region.Create(tenant.Id, "Berea / Musgrave", "BER"),
            Region.Create(tenant.Id, "Pinetown", "PIN"),
            Region.Create(tenant.Id, "Westville", "WV"),
            Region.Create(tenant.Id, "Chatsworth", "CHAT"),
        };
        db.Regions.AddRange(regions);

        // ── USERS ──────────────────────────────────────────────────
        var pw = new PasswordService();

        var adminUser = User.Create(tenant.Id, "admin@demo.civicops.io", "System Administrator",
            UserRole.SuperAdmin, "ADM-001");
        adminUser.SetPasswordHash(pw.HashPassword("Admin@123!"));
        adminUser.RecordLogin();

        var opsManager = User.Create(tenant.Id, "ops@demo.civicops.io", "Sipho Nkosi",
            UserRole.OperationsManager, "OPS-001", "+27831234001");
        opsManager.SetPasswordHash(pw.HashPassword("Ops@123!"));

        var dispatcher1 = User.Create(tenant.Id, "dispatcher@demo.civicops.io", "Thembi Dlamini",
            UserRole.Dispatcher, "DIS-001", "+27831234002");
        dispatcher1.SetPasswordHash(pw.HashPassword("Ops@123!"));

        var dispatcher2 = User.Create(tenant.Id, "dispatch2@demo.civicops.io", "Ravi Pillay",
            UserRole.Dispatcher, "DIS-002", "+27831234003");
        dispatcher2.SetPasswordHash(pw.HashPassword("Ops@123!"));

        var supervisors = new[]
        {
            CreateUser(tenant.Id, "sup1@demo.civicops.io", "Mbuso Cele", UserRole.Supervisor, "SUP-001", pw),
            CreateUser(tenant.Id, "sup2@demo.civicops.io", "Priya Govender", UserRole.Supervisor, "SUP-002", pw),
        };

        var officers = Enumerable.Range(1, 12).Select(i =>
            CreateUser(tenant.Id, $"officer{i}@demo.civicops.io",
                GetOfficerName(i), UserRole.PatrolOfficer, $"OFF-{i:D3}", pw)
        ).ToList();

        var drivers = Enumerable.Range(1, 4).Select(i =>
            CreateUser(tenant.Id, $"driver{i}@demo.civicops.io",
                GetDriverName(i), UserRole.Driver, $"DRV-{i:D3}", pw)
        ).ToList();

        var clientViewer = User.Create(tenant.Id, "client@demo.civicops.io", "Client Portal",
            UserRole.ClientViewer, "CLT-001");
        clientViewer.SetPasswordHash(pw.HashPassword("Client@123!"));

        var allUsers = new List<User>
        {
            adminUser, opsManager, dispatcher1, dispatcher2, clientViewer
        };
        allUsers.AddRange(supervisors);
        allUsers.AddRange(officers);
        allUsers.AddRange(drivers);

        db.Users.AddRange(allUsers);

        // ── VEHICLES ───────────────────────────────────────────────
        var vehicles = CreateVehicles(tenant.Id, officers, drivers);
        db.Vehicles.AddRange(vehicles);

        // Save users and vehicles first (needed for FK refs)
        await db.SaveChangesAsync();

        // ── GPS HISTORY (90 days) ──────────────────────────────────
        logger.LogInformation("Generating GPS history for {Count} vehicles...", vehicles.Count);
        var gpsEvents = GenerateGpsHistory(vehicles, tenant.Id);
        await db.VehicleGpsEvents.AddRangeAsync(gpsEvents);

        // ── INCIDENTS ──────────────────────────────────────────────
        logger.LogInformation("Creating incident history...");
        var incidents = CreateIncidents(tenant.Id, officers, vehicles, regions);
        db.Incidents.AddRange(incidents);

        // ── MAINTENANCE SCHEDULES ──────────────────────────────────
        var maintenanceSchedules = CreateMaintenanceSchedules(tenant.Id, vehicles);
        db.MaintenanceSchedules.AddRange(maintenanceSchedules);

        await db.SaveChangesAsync();

        logger.LogInformation(
            "✅ Demo seed complete — Tenant: {Name} | Users: {Users} | Vehicles: {Vehicles} | Incidents: {Inc}",
            tenant.Name, allUsers.Count, vehicles.Count, incidents.Count);
    }

    private static User CreateUser(Guid tenantId, string email, string name,
        UserRole role, string employeeId, PasswordService pw)
    {
        var user = User.Create(tenantId, email, name, role, employeeId);
        user.SetPasswordHash(pw.HashPassword("Ops@123!"));
        return user;
    }

    private static List<Vehicle> CreateVehicles(Guid tenantId, List<User> officers, List<User> drivers)
    {
        var vehicles = new List<Vehicle>();
        var specs = new[]
        {
            ("VH-101", VehicleType.Patrol, "Toyota", "Fortuner", 2022, "White"),
            ("VH-103", VehicleType.Response, "Ford", "Ranger Raptor", 2023, "Black"),
            ("VH-105", VehicleType.Patrol, "Toyota", "Hilux", 2021, "Silver"),
            ("VH-107", VehicleType.Patrol, "Nissan", "Navara", 2020, "White"),
            ("VH-109", VehicleType.Response, "Toyota", "Land Cruiser", 2023, "Black"),
            ("VH-111", VehicleType.Command, "BMW", "X5", 2022, "Black"),
            ("VH-113", VehicleType.Patrol, "Toyota", "Hilux", 2021, "White"),
            ("VH-115", VehicleType.Patrol, "Ford", "Ranger", 2022, "Silver"),
            ("VH-117", VehicleType.Response, "Toyota", "Fortuner", 2023, "Black"),
            ("VH-119", VehicleType.Logistics, "Toyota", "Quantum", 2020, "White"),
            ("VH-121", VehicleType.Patrol, "Nissan", "Navara", 2021, "White"),
            ("VH-123", VehicleType.Patrol, "Toyota", "Hilux", 2022, "Silver"),
            ("VH-125", VehicleType.Recovery, "Mercedes", "Actros", 2019, "Yellow"),
            ("VH-127", VehicleType.Logistics, "Toyota", "Land Cruiser 79", 2021, "White"),
        };

        for (int i = 0; i < specs.Length; i++)
        {
            var (reg, type, make, model, year, color) = specs[i];
            var v = Vehicle.Create(tenantId, reg, type, make, model, year);
            v.UpdateDetails(null, color, null, FuelType.Diesel, 80);
            v.SetGpsDevice($"GPS{reg.Replace("-", "")}", "MiX Telematics");

            // Assign driver/officer
            if (i < officers.Count)
                v.AssignDriver(officers[i].Id);

            // Set initial GPS position (random within Durban)
            var lat = (decimal)(LatMin + _rng.NextDouble() * (LatMax - LatMin));
            var lng = (decimal)(LngMin + _rng.NextDouble() * (LngMax - LngMin));
            var speed = _rng.Next(0, 2) == 0 ? 0 : (decimal)_rng.Next(30, 90);
            v.UpdateLastGps(lat, lng, speed, null, true, (decimal)_rng.Next(30, 90));
            v.UpdateOdometer(_rng.Next(15000, 120000));
            v.UpdateHealthScore(_rng.Next(60, 100));

            vehicles.Add(v);
        }

        return vehicles;
    }

    private static List<VehicleGpsEvent> GenerateGpsHistory(List<Vehicle> vehicles, Guid tenantId)
    {
        var events = new List<VehicleGpsEvent>();
        var from = DateTime.UtcNow.AddDays(-90);

        foreach (var vehicle in vehicles.Take(5)) // limit for seed performance
        {
            var lat = (double)(vehicle.LastLatitude ?? (decimal)(LatMin + _rng.NextDouble() * (LatMax - LatMin)));
            var lng = (double)(vehicle.LastLongitude ?? (decimal)(LngMin + _rng.NextDouble() * (LngMax - LngMin)));

            for (var dt = from; dt < DateTime.UtcNow; dt = dt.AddMinutes(5))
            {
                // Simulate movement
                lat += (_rng.NextDouble() - 0.5) * 0.005;
                lng += (_rng.NextDouble() - 0.5) * 0.005;
                lat = Math.Clamp(lat, LatMin, LatMax);
                lng = Math.Clamp(lng, LngMin, LngMax);

                var speed = dt.Hour >= 7 && dt.Hour <= 21
                    ? (decimal)_rng.Next(0, 90)
                    : (decimal)_rng.Next(0, 10);

                events.Add(VehicleGpsEvent.Create(
                    vehicle.Id, tenantId,
                    (decimal)lat, (decimal)lng, speed, dt,
                    "position", null, null,
                    speed > 5, (decimal)_rng.Next(40, 90)
                ));
            }
        }

        return events;
    }

    private static List<Incident> CreateIncidents(Guid tenantId, List<User> officers,
        List<Vehicle> vehicles, Region[] regions)
    {
        var incidents = new List<Incident>();
        var categories = Enum.GetValues<IncidentCategory>();
        var priorities = new[] { IncidentPriority.Critical, IncidentPriority.High,
            IncidentPriority.Medium, IncidentPriority.Low };
        var weights = new[] { 0.05, 0.20, 0.45, 0.30 }; // probability weights

        // Create 90 days of historical incidents (avg ~12/day)
        for (int i = 0; i < 1080; i++)
        {
            var daysAgo = _rng.Next(1, 91);
            var openedAt = DateTime.UtcNow.AddDays(-daysAgo)
                .AddHours(_rng.Next(0, 24))
                .AddMinutes(_rng.Next(0, 60));

            var category = categories[_rng.Next(categories.Length)];
            var priority = WeightedPick(priorities, weights);
            var officer = officers[_rng.Next(officers.Count)];
            var vehicle = vehicles[_rng.Next(vehicles.Count)];

            var lat = (decimal)(LatMin + _rng.NextDouble() * (LatMax - LatMin));
            var lng = (decimal)(LngMin + _rng.NextDouble() * (LngMax - LngMin));
            var refNo = $"INC-{openedAt:yyyyMMdd}-{(i + 1):D4}";

            var incident = Incident.Create(
                tenantId, refNo,
                GetIncidentTitle(category),
                category, priority,
                officer.Id, lat, lng,
                GetDurbanAddress(),
                $"Incident reported by field unit. Category: {category}."
            );

            incident.SetSlaTarget(priority switch
            {
                IncidentPriority.Critical => 5,
                IncidentPriority.High => 10,
                IncidentPriority.Medium => 20,
                _ => 45
            });

            // Assign and resolve most historical incidents
            var responseTimeMin = _rng.Next(2, 20);
            incident.Assign(officer.Id, vehicle.Id);

            var resolved = _rng.NextDouble() > 0.05; // 95% resolved
            if (resolved)
            {
                incident.UpdateStatus(IncidentStatus.Closed);
                if (responseTimeMin > incident.SlaTargetMinutes)
                    incident.MarkSlaBreach();
            }

            incidents.Add(incident);
        }

        // Add 7 live open incidents
        var liveIncidentData = new[]
        {
            ("Armed robbery in progress — suspects on foot", IncidentCategory.ArmedResponse, IncidentPriority.Critical),
            ("Vehicle accident with injuries — N3 Southbound", IncidentCategory.MotorVehicleAccident, IncidentPriority.High),
            ("Alarm activation — commercial premises", IncidentCategory.AlarmActivation, IncidentPriority.High),
            ("Suspicious vehicle — parked for 3h", IncidentCategory.SuspiciousActivity, IncidentPriority.Medium),
            ("Medical assist required — pedestrian", IncidentCategory.MedicalAssist, IncidentPriority.High),
            ("Perimeter breach — warehouse zone", IncidentCategory.Trespassing, IncidentPriority.Medium),
            ("Escort required — cash transit", IncidentCategory.Escort, IncidentPriority.Low),
        };

        for (int i = 0; i < liveIncidentData.Length; i++)
        {
            var (title, cat, pri) = liveIncidentData[i];
            var lat = (decimal)(LatMin + _rng.NextDouble() * (LatMax - LatMin));
            var lng = (decimal)(LngMin + _rng.NextDouble() * (LngMax - LngMin));
            var refNo = $"INC-{DateTime.UtcNow:yyyyMMdd}-{(9900 + i):D4}";

            var live = Incident.Create(tenantId, refNo, title, cat, pri,
                officers[i % officers.Count].Id, lat, lng, GetDurbanAddress());
            live.SetSlaTarget(pri switch
            {
                IncidentPriority.Critical => 5,
                IncidentPriority.High => 10,
                _ => 20
            });
            incidents.Add(live);
        }

        return incidents;
    }

    private static List<MaintenanceSchedule> CreateMaintenanceSchedules(
        Guid tenantId, List<Vehicle> vehicles)
    {
        var schedules = new List<MaintenanceSchedule>();
        foreach (var v in vehicles)
        {
            schedules.Add(MaintenanceSchedule.Create(tenantId, v.Id,
                "Engine Oil & Filter", "15W-40 full synthetic", 10000, 180, 850));
            schedules.Add(MaintenanceSchedule.Create(tenantId, v.Id,
                "Tyre Rotation", null, 15000, 365, 400));
            schedules.Add(MaintenanceSchedule.Create(tenantId, v.Id,
                "Brake Inspection", null, 20000, 365, 600));
            schedules.Add(MaintenanceSchedule.Create(tenantId, v.Id,
                "Annual Service", "Full 90k service", null, 365, 3500));
        }
        return schedules;
    }

    private static T WeightedPick<T>(T[] items, double[] weights)
    {
        var r = _rng.NextDouble();
        var cumulative = 0.0;
        for (int i = 0; i < items.Length; i++)
        {
            cumulative += weights[i];
            if (r < cumulative) return items[i];
        }
        return items[^1];
    }

    private static string GetIncidentTitle(IncidentCategory cat) => cat switch
    {
        IncidentCategory.ArmedResponse => "Armed response activation",
        IncidentCategory.AlarmActivation => "Alarm activation — premises",
        IncidentCategory.MotorVehicleAccident => "Motor vehicle accident",
        IncidentCategory.MedicalAssist => "Medical assistance required",
        IncidentCategory.SuspiciousActivity => "Suspicious activity reported",
        IncidentCategory.Escort => "Escort / transit request",
        IncidentCategory.Trespassing => "Trespassing — perimeter breach",
        IncidentCategory.Theft => "Theft in progress",
        IncidentCategory.Assault => "Assault reported",
        IncidentCategory.VehicleBreakdown => "Vehicle breakdown — assistance",
        _ => $"{cat} incident"
    };

    private static string GetDurbanAddress() =>
    [
        "123 Dr Pixley KaSeme St, Durban CBD",
        "45 Florida Rd, Morningside",
        "N3 Highway, Pinetown Interchange",
        "15 Esplanade Rd, Bluff",
        "Westwood Mall, Westville",
        "78 Umgeni Rd, Berea",
        "Pavilion Shopping Centre, Westville",
        "Springfield Park Industrial, Durban North",
        "240 Dr Yusuf Dadoo St, Greyville",
        "Chatsworth Shopping Centre, Chatsworth",
    ][_rng.Next(10)];

    private static string GetOfficerName(int i) => new[]
    {
        "Kabelo Dlamini", "Themba Nkosi", "Sanele Pillay",
        "Mpho Govender", "Lungelo Cele", "Anele Singh",
        "Dumisani Zulu", "Refilwe Maharaj", "Phiwayinkosi Khumalo",
        "Nompumelelo Naidoo", "Siyanda Bhengu", "Zanele Mbatha"
    }[Math.Clamp(i - 1, 0, 11)];

    private static string GetDriverName(int i) => new[]
    {
        "Rajan Moodley", "Siphamandla Ntuli", "Kagiso Sithole", "Avesh Chetty"
    }[Math.Clamp(i - 1, 0, 3)];
}

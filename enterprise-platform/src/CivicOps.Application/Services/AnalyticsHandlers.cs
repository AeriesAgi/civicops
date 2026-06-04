using CivicOps.Application.DTOs.Analytics;
using CivicOps.Application.DTOs.Common;
using CivicOps.Application.Interfaces;
using CivicOps.Application.Queries.Analytics;
using CivicOps.Domain.Enums;
using CivicOps.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CivicOps.Application.Services;

public class GetDashboardKpisHandler : IRequestHandler<GetDashboardKpisQuery, DashboardKpiDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ILiveFleetCache _fleetCache;
    private readonly ICacheService _cache;

    public GetDashboardKpisHandler(IUnitOfWork uow, ILiveFleetCache fleetCache, ICacheService cache)
    {
        _uow = uow;
        _fleetCache = fleetCache;
        _cache = cache;
    }

    public async Task<DashboardKpiDto> Handle(GetDashboardKpisQuery request, CancellationToken ct)
    {
        var cacheKey = $"dashboard_kpis:{request.TenantId}";
        var cached = await _cache.GetAsync<DashboardKpiDto>(cacheKey, ct);
        if (cached is not null) return cached;

        var livePositions = (await _fleetCache.GetAllPositionsAsync(request.TenantId, ct)).ToList();
        var vehicles = (await _uow.Vehicles.GetByTenantAsync(request.TenantId, true, ct)).ToList();
        var openIncidents = (await _uow.Incidents.GetOpenIncidentsAsync(request.TenantId, ct)).ToList();
        var activeDispatches = (await _uow.Dispatches.GetActiveByTenantAsync(request.TenantId, ct)).ToList();
        var maintenanceAlerts = (await _uow.Maintenance.GetDueSchedulesAsync(request.TenantId, ct)).ToList();

        var today = DateTime.UtcNow.Date;
        var allTodayIncidents = (await _uow.Incidents.GetByTenantAsync(request.TenantId, null, ct))
            .Where(i => i.OpenedAt >= today).ToList();

        var closedToday = allTodayIncidents.Count(i => i.Status == IncidentStatus.Closed);
        var responseTimes = allTodayIncidents
            .Where(i => i.ResponseTimeMin.HasValue)
            .Select(i => (double)i.ResponseTimeMin!.Value)
            .ToList();

        var avgResponseTime = responseTimes.Any() ? responseTimes.Average() : 0;

        var totalUnits = vehicles.Count;
        var onlineUnits = livePositions.Count(p =>
            (DateTime.UtcNow - p.UpdatedAt).TotalMinutes < 5);
        var activeUnits = livePositions.Count(p =>
            p.Status is "Active" or "Dispatched" or "OnScene");

        var utilization = totalUnits > 0
            ? Math.Round((double)activeUnits / totalUnits * 100, 1)
            : 0;

        var slaTotal = allTodayIncidents.Count;
        var slaWithin = allTodayIncidents.Count(i => !i.SlaBreached);
        var slaPct = slaTotal > 0 ? Math.Round((double)slaWithin / slaTotal * 100, 1) : 100;

        var dto = new DashboardKpiDto(
            TotalActiveUnits: activeUnits,
            TotalOnlineUnits: onlineUnits,
            TotalUnits: totalUnits,
            OpenIncidents: openIncidents.Count,
            CriticalIncidents: openIncidents.Count(i => i.Priority == IncidentPriority.Critical),
            HighIncidents: openIncidents.Count(i => i.Priority == IncidentPriority.High),
            PendingDispatch: activeDispatches.Count(d => d.Status == DispatchStatus.Pending),
            AverageResponseTimeMin: Math.Round(avgResponseTime, 1),
            FleetUtilizationPct: utilization,
            SlaCompliancePct: slaPct,
            IncidentsTodayTotal: allTodayIncidents.Count,
            IncidentsTodayClosed: closedToday,
            PanicAlertsActive: 0, // populated by panic service
            MaintenanceDue: maintenanceAlerts.Count,
            TotalKmToday: 0, // from trips aggregation
            TotalFuelUsedToday: 0,
            GeneratedAt: DateTime.UtcNow
        );

        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromSeconds(30), ct);
        return dto;
    }
}

public class GetResponseTimeAnalyticsHandler
    : IRequestHandler<GetResponseTimeAnalyticsQuery, ResponseTimeAnalyticsDto>
{
    private readonly IUnitOfWork _uow;

    public GetResponseTimeAnalyticsHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<ResponseTimeAnalyticsDto> Handle(
        GetResponseTimeAnalyticsQuery request, CancellationToken ct)
    {
        var incidents = (await _uow.Incidents.GetByTenantAsync(request.TenantId, null, ct))
            .Where(i => i.OpenedAt >= request.From && i.OpenedAt <= request.To
                        && i.ResponseTimeMin.HasValue)
            .ToList();

        var dailyAverages = incidents
            .GroupBy(i => i.OpenedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyMetricDto(
                g.Key,
                Math.Round(g.Average(i => (double)i.ResponseTimeMin!.Value), 1),
                g.Count()
            )).ToList();

        var byCategory = incidents
            .GroupBy(i => i.Category.ToString())
            .Select(g => new CategoryBreakdownDto(
                g.Key,
                g.Count(),
                Math.Round((double)g.Count() / incidents.Count * 100, 1),
                Math.Round(g.Average(i => (double)i.ResponseTimeMin!.Value), 1)
            )).ToList();

        var byHour = incidents
            .GroupBy(i => i.OpenedAt.Hour)
            .OrderBy(g => g.Key)
            .Select(g => new HourlyBreakdownDto(
                g.Key,
                g.Count(),
                Math.Round(g.Average(i => (double)i.ResponseTimeMin!.Value), 1)
            )).ToList();

        var allTimes = incidents.Select(i => (double)i.ResponseTimeMin!.Value).ToList();

        return new ResponseTimeAnalyticsDto(
            DailyAverages: dailyAverages,
            OverallAverageMin: allTimes.Any() ? Math.Round(allTimes.Average(), 1) : 0,
            BestResponseMin: allTimes.Any() ? Math.Round(allTimes.Min(), 1) : 0,
            WorstResponseMin: allTimes.Any() ? Math.Round(allTimes.Max(), 1) : 0,
            SlaCompliancePct: incidents.Any()
                ? Math.Round((double)incidents.Count(i => !i.SlaBreached) / incidents.Count * 100, 1)
                : 100,
            ByCategory: byCategory,
            ByHour: byHour
        );
    }
}

public class GetIncidentTrendsHandler : IRequestHandler<GetIncidentTrendsQuery, IncidentTrendsDto>
{
    private readonly IUnitOfWork _uow;

    public GetIncidentTrendsHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IncidentTrendsDto> Handle(GetIncidentTrendsQuery request, CancellationToken ct)
    {
        var incidents = (await _uow.Incidents.GetByTenantAsync(request.TenantId, null, ct))
            .Where(i => i.OpenedAt >= request.From && i.OpenedAt <= request.To)
            .ToList();

        var dailyTrend = incidents
            .GroupBy(i => i.OpenedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyIncidentMetricDto(
                g.Key,
                g.Count(),
                g.Count(i => i.Status == IncidentStatus.Closed),
                g.Count(i => i.Priority == IncidentPriority.Critical),
                g.Count(i => i.Priority == IncidentPriority.High)
            )).ToList();

        var byCategory = incidents
            .GroupBy(i => i.Category.ToString())
            .Select(g => new CategoryBreakdownDto(
                g.Key, g.Count(),
                incidents.Count > 0 ? Math.Round((double)g.Count() / incidents.Count * 100, 1) : 0,
                null
            )).OrderByDescending(c => c.Count).ToList();

        var byPriority = incidents
            .GroupBy(i => i.Priority.ToString())
            .Select(g => new CategoryBreakdownDto(
                g.Key, g.Count(),
                incidents.Count > 0 ? Math.Round((double)g.Count() / incidents.Count * 100, 1) : 0,
                null
            )).ToList();

        var byHour = incidents
            .GroupBy(i => i.OpenedAt.Hour)
            .OrderBy(g => g.Key)
            .Select(g => new HourlyBreakdownDto(g.Key, g.Count(), 0))
            .ToList();

        var heatmap = incidents
            .Where(i => i.Latitude.HasValue && i.Longitude.HasValue)
            .Select(i => new HeatmapPointDto(
                (double)i.Latitude!.Value,
                (double)i.Longitude!.Value,
                (int)i.Priority,
                i.Category.ToString()
            )).ToList();

        return new IncidentTrendsDto(dailyTrend, byCategory, byPriority, byHour, heatmap);
    }
}

public class GetSlaComplianceHandler : IRequestHandler<GetSlaComplianceQuery, SlaComplianceDto>
{
    private readonly IUnitOfWork _uow;

    public GetSlaComplianceHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<SlaComplianceDto> Handle(GetSlaComplianceQuery request, CancellationToken ct)
    {
        var incidents = (await _uow.Incidents.GetByTenantAsync(request.TenantId, null, ct))
            .Where(i => i.OpenedAt >= request.From && i.OpenedAt <= request.To
                        && i.SlaTargetMinutes.HasValue)
            .ToList();

        var within = incidents.Count(i => !i.SlaBreached);
        var breached = incidents.Count(i => i.SlaBreached);
        var compliancePct = incidents.Any()
            ? Math.Round((double)within / incidents.Count * 100, 1) : 100;

        var byCategory = incidents
            .GroupBy(i => i.Category.ToString())
            .Select(g => new CategoryBreakdownDto(
                g.Key, g.Count(),
                Math.Round((double)g.Count(i => !i.SlaBreached) / g.Count() * 100, 1),
                g.Any(i => i.ResponseTimeMin.HasValue)
                    ? Math.Round(g.Where(i => i.ResponseTimeMin.HasValue)
                        .Average(i => (double)i.ResponseTimeMin!.Value), 1)
                    : null
            )).ToList();

        var dailyTrend = incidents
            .GroupBy(i => i.OpenedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyMetricDto(
                g.Key,
                g.Any() ? Math.Round((double)g.Count(i => !i.SlaBreached) / g.Count() * 100, 1) : 100,
                g.Count()
            )).ToList();

        return new SlaComplianceDto(compliancePct, incidents.Count, within, breached, byCategory, dailyTrend);
    }
}

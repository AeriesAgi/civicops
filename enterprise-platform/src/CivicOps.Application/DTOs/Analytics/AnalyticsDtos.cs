using MediatR;
using CivicOps.Application.DTOs.Common;

namespace CivicOps.Application.DTOs.Analytics;

public record DashboardKpiDto(
    int TotalActiveUnits,
    int TotalOnlineUnits,
    int TotalUnits,
    int OpenIncidents,
    int CriticalIncidents,
    int HighIncidents,
    int PendingDispatch,
    double AverageResponseTimeMin,
    double FleetUtilizationPct,
    double SlaCompliancePct,
    int IncidentsTodayTotal,
    int IncidentsTodayClosed,
    int PanicAlertsActive,
    int MaintenanceDue,
    double TotalKmToday,
    double TotalFuelUsedToday,
    DateTime GeneratedAt
);

public record ResponseTimeAnalyticsDto(
    IEnumerable<DailyMetricDto> DailyAverages,
    double OverallAverageMin,
    double BestResponseMin,
    double WorstResponseMin,
    double SlaCompliancePct,
    IEnumerable<CategoryBreakdownDto> ByCategory,
    IEnumerable<HourlyBreakdownDto> ByHour
);

public record FleetUtilizationDto(
    double OverallUtilizationPct,
    double AverageKmPerVehicle,
    double AverageFuelEfficiency,
    double AverageIdlePct,
    IEnumerable<VehicleUtilizationDto> VehicleBreakdown,
    IEnumerable<DailyMetricDto> DailyUtilization
);

public record VehicleUtilizationDto(
    Guid VehicleId,
    string Registration,
    string? Alias,
    double UtilizationPct,
    double TotalKm,
    double TotalFuelL,
    double AvgSpeedKmh,
    int TripCount,
    int IdleMinutes
);

public record IncidentTrendsDto(
    IEnumerable<DailyIncidentMetricDto> DailyTrend,
    IEnumerable<CategoryBreakdownDto> ByCategory,
    IEnumerable<CategoryBreakdownDto> ByPriority,
    IEnumerable<HourlyBreakdownDto> ByHour,
    IEnumerable<HeatmapPointDto> Heatmap
);

public record OperatorPerformanceDto(
    Guid UserId,
    string FullName,
    string Role,
    int IncidentsHandled,
    int DispatchesMade,
    double AvgResponseTimeMin,
    double SlaCompliancePct,
    int PanicResponsed,
    int ShiftHours
);

public record SlaComplianceDto(
    double OverallCompliancePct,
    int TotalIncidents,
    int WithinSla,
    int BreachedSla,
    IEnumerable<CategoryBreakdownDto> ByCategory,
    IEnumerable<DailyMetricDto> DailyTrend
);

public record HeatmapPointDto(double Lat, double Lng, int Weight, string? Category);
public record DailyMetricDto(DateTime Date, double Value, int Count);
public record DailyIncidentMetricDto(DateTime Date, int Total, int Closed, int Critical, int High);
public record CategoryBreakdownDto(string Category, int Count, double Percentage, double? AvgMin);
public record HourlyBreakdownDto(int Hour, int Count, double AvgMin);

public record ExportRequest(
    string ReportType,
    string Format,
    DateTime? From = null,
    DateTime? To = null,
    Dictionary<string, string>? Filters = null
);

namespace CivicOps.Application.Queries.Analytics;

using CivicOps.Application.DTOs.Analytics;

public record GetDashboardKpisQuery(Guid TenantId)
    : IRequest<DashboardKpiDto>;

public record GetResponseTimeAnalyticsQuery(Guid TenantId, DateTime From, DateTime To)
    : IRequest<ResponseTimeAnalyticsDto>;

public record GetFleetUtilizationQuery(Guid TenantId, DateTime From, DateTime To)
    : IRequest<FleetUtilizationDto>;

public record GetIncidentTrendsQuery(Guid TenantId, DateTime From, DateTime To)
    : IRequest<IncidentTrendsDto>;

public record GetIncidentHeatmapQuery(Guid TenantId, DateTime From, DateTime To, string? Category = null)
    : IRequest<IEnumerable<HeatmapPointDto>>;

public record GetOperatorPerformanceQuery(Guid TenantId, DateTime From, DateTime To)
    : IRequest<IEnumerable<OperatorPerformanceDto>>;

public record GetSlaComplianceQuery(Guid TenantId, DateTime From, DateTime To)
    : IRequest<SlaComplianceDto>;

public record ExportReportCommand(Guid TenantId, ExportRequest Request)
    : IRequest<Result<byte[]>>;

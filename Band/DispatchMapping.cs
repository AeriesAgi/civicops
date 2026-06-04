using CivicOps.Models;

namespace CivicOps.Band
{
    /// <summary>Maps civic classification onto the Command response fleet.</summary>
    public static class DispatchMapping
    {
        public static UnitType ToUnitType(Department department) => department switch
        {
            Department.FireAndRescue => UnitType.FireRescue,
            Department.EMSMedicalReferral => UnitType.Ambulance,
            Department.MetroPolicePublicSafety => UnitType.MetroPolice,
            Department.SAPSLiaisonPoliceReferral => UnitType.ArmedResponse,
            Department.DisasterManagement => UnitType.DisasterManagement,
            Department.WaterAndSanitation => UnitType.UtilityCrew,
            Department.Electricity => UnitType.UtilityCrew,
            Department.RoadsAndStormwater => UnitType.UtilityCrew,
            Department.WasteManagement => UnitType.UtilityCrew,
            _ => UnitType.MetroPolice
        };

        public static int SlaTargetMinutes(IncidentPriority priority) => priority switch
        {
            IncidentPriority.Critical => 8,
            IncidentPriority.Urgent => 12,
            IncidentPriority.High => 20,
            IncidentPriority.Medium => 45,
            _ => 90
        };

        public static bool RequiresEscalationPath(IncidentPriority priority) =>
            priority is IncidentPriority.Urgent or IncidentPriority.Critical or IncidentPriority.High;
    }
}

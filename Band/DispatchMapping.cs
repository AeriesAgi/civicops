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

        /// <summary>Mutual-aid / backup resources the ResourceLogisticsAgent stages
        /// for a given required unit type, modelling how a real ops room pre-arranges
        /// supporting capacity alongside the primary responder.</summary>
        public static string MutualAidFor(UnitType type) => type switch
        {
            UnitType.FireRescue => "second pumper + water tanker on standby, EMS co-response requested",
            UnitType.Ambulance => "ALS backup ambulance staged, receiving-hospital trauma bay pre-alerted",
            UnitType.ArmedResponse => "SAPS joint response + K9 unit on standby",
            UnitType.DisasterManagement => "evacuation transport + Red Cross shelter coordination",
            UnitType.UtilityCrew => "heavy-plant crew + traffic management on standby",
            UnitType.MetroPolice => "additional patrol + traffic control units on standby",
            _ => "supporting units placed on standby"
        };
    }
}

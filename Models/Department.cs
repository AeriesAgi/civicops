namespace CivicOps.Models
{
    public enum Department
    {
        WaterAndSanitation,
        Electricity,
        RoadsAndStormwater,
        WasteManagement,
        ParksAndPublicSpaces,
        HousingInformalSettlements,
        EnvironmentalHealth,
        DisasterManagement,
        FireAndRescue,
        MetroPolicePublicSafety,
        SAPSLiaisonPoliceReferral,
        EMSMedicalReferral,
        WardCouncillorWardCommittee
    }

    public static class DepartmentExtensions
    {
        public static string GetDisplayName(this Department department)
        {
            return department switch
            {
                Department.WaterAndSanitation => "Water & Sanitation",
                Department.Electricity => "Electricity & Power",
                Department.RoadsAndStormwater => "Roads & Infrastructure",
                Department.WasteManagement => "Waste Management",
                Department.ParksAndPublicSpaces => "Parks & Public Spaces",
                Department.HousingInformalSettlements => "Housing Services",
                Department.EnvironmentalHealth => "Environmental Health",
                Department.DisasterManagement => "Disaster Management",
                Department.FireAndRescue => "Fire & Rescue",
                Department.MetroPolicePublicSafety => "Public Safety",
                Department.SAPSLiaisonPoliceReferral => "SAPS Liaison/Police Referral",
                Department.EMSMedicalReferral => "EMS/Medical Referral",
                Department.WardCouncillorWardCommittee => "Community Liaison",
                _ => department.ToString()
            };
        }
    }
}

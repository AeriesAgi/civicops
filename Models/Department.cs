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
                Department.Electricity => "Electricity",
                Department.RoadsAndStormwater => "Roads & Stormwater",
                Department.WasteManagement => "Waste Management",
                Department.ParksAndPublicSpaces => "Parks & Public Spaces",
                Department.HousingInformalSettlements => "Housing/Informal Settlements",
                Department.EnvironmentalHealth => "Environmental Health",
                Department.DisasterManagement => "Disaster Management",
                Department.FireAndRescue => "Fire & Rescue",
                Department.MetroPolicePublicSafety => "Metro Police/Public Safety",
                Department.SAPSLiaisonPoliceReferral => "SAPS Liaison/Police Referral",
                Department.EMSMedicalReferral => "EMS/Medical Referral",
                Department.WardCouncillorWardCommittee => "Ward Councillor/Ward Committee",
                _ => department.ToString()
            };
        }
    }
}

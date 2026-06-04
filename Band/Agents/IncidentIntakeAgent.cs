using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CivicOps.Models;
using CivicOps.Services;
using Microsoft.Extensions.Logging;

namespace CivicOps.Band.Agents
{
    /// <summary>
    /// AGENT 1 — receives raw incident reports (citizen app, WhatsApp, call centre,
    /// walk-in), classifies them with the CivicOps Gemini/deterministic intake
    /// pipeline, extracts structured data, posts the classified incident to the
    /// Band room and hands off to the DispatchCoordinatorAgent — all through Band.
    /// </summary>
    public class IncidentIntakeAgent : BandAgent
    {
        private readonly IIncidentIntakeService _intake;

        public IncidentIntakeAgent(
            IBandTransport transport,
            IIncidentIntakeService intake,
            ILogger<IncidentIntakeAgent> logger)
            : base(transport, BandIdentities.IntakeAgent, logger)
        {
            _intake = intake;
        }

        protected override bool ShouldHandle(BandMessage message) =>
            message.Kind == BandMessageKind.RawReport;

        protected override async Task HandleAsync(BandMessage message)
        {
            var roomId = message.RoomId;
            JoinAndAnnounce(roomId);

            var rawText = GetString(message, "rawText", message.Text);
            var area = GetString(message, "area");
            var channel = GetString(message, "channel", "Web");

            Band.Post(roomId, Identity, BandMessageKind.System,
                $"Reading raw report from {channel}. Classifying type, severity, area and required resource…");

            var request = new IncidentIntakeRequest
            {
                Description = rawText,
                Suburb = string.IsNullOrWhiteSpace(area) ? null : area,
                SourceChannel = ParseChannel(channel),
                CreatedBy = "IncidentIntakeAgent (Band)"
            };

            var result = await _intake.ProcessAsync(request);
            var incident = result.Incident;
            var unitType = DispatchMapping.ToUnitType(incident.AssignedDepartment);
            var resolvedArea = string.IsNullOrWhiteSpace(incident.NormalizedArea) ? area : incident.NormalizedArea;

            // Promote the room from a raw intake space to a classified incident.
            Band.UpdateRoom(roomId, r =>
            {
                r.IncidentReference = incident.ReferenceNumber;
                r.Title = $"{incident.Category} — {resolvedArea}";
                r.Area = resolvedArea;
                r.Severity = incident.Priority.ToString();
                r.Phase = "Classified";
            });

            var data = new Dictionary<string, object?>
            {
                ["incidentId"] = incident.Id,
                ["reference"] = incident.ReferenceNumber,
                ["category"] = incident.Category,
                ["department"] = incident.AssignedDepartment.ToString(),
                ["departmentName"] = incident.AssignedDepartment.GetDisplayName(),
                ["priority"] = incident.Priority.ToString(),
                ["severity"] = incident.Priority.ToString(),
                ["area"] = resolvedArea,
                ["ward"] = incident.Ward,
                ["requiredUnitType"] = unitType.ToString(),
                ["slaTargetMinutes"] = DispatchMapping.SlaTargetMinutes(incident.Priority),
                ["emergencyReferral"] = incident.EmergencyReferralRecommended,
                ["classificationMethod"] = incident.ClassificationMethod,
                ["summary"] = incident.AISummary
            };

            var headline =
                $"CLASSIFIED · {incident.Priority} · {incident.Category}\n" +
                $"Ref {incident.ReferenceNumber} in {resolvedArea} ({incident.Ward}).\n" +
                $"Routed to {incident.AssignedDepartment.GetDisplayName()} → required unit: {UnitTypeName(unitType)}.\n" +
                $"Summary: {incident.AISummary}\n" +
                $"Classifier: {incident.ClassificationMethod}. SLA target {DispatchMapping.SlaTargetMinutes(incident.Priority)} min." +
                (incident.EmergencyReferralRecommended ? "\n⚠️ Emergency language detected — advise caller to also contact emergency services." : string.Empty);

            Band.Post(roomId, Identity, BandMessageKind.Classified, headline, data);

            // Explicit hand-off to the next agent, through Band.
            Band.Post(roomId, Identity, BandMessageKind.Handoff,
                $"Handing off to DispatchCoordinatorAgent for unit matching. Required unit type: {UnitTypeName(unitType)}.",
                new Dictionary<string, object?>
                {
                    ["requiredUnitType"] = unitType.ToString(),
                    ["area"] = resolvedArea,
                    ["severity"] = incident.Priority.ToString()
                },
                handoffTo: BandIdentities.DispatchAgent.Id);
        }

        private static string UnitTypeName(UnitType t) => new ResponseUnit { Type = t }.TypeName;

        private static SourceChannel ParseChannel(string channel) => channel.ToLowerInvariant() switch
        {
            "whatsapp" => SourceChannel.WhatsApp,
            "android" => SourceChannel.Android,
            "voicenote" or "voice" => SourceChannel.VoiceNote,
            "demo" => SourceChannel.Demo,
            _ => SourceChannel.Web
        };
    }
}

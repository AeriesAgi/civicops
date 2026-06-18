using System.Linq;
using CivicOps.Band;
using Microsoft.AspNetCore.Mvc;

namespace CivicOps.Controllers
{
    /// <summary>
    /// UI + REST surface for the Band multi-agent dispatch layer: the console, the
    /// live per-incident Room Viewer, the human dispatcher confirmation, and the
    /// simulation entry point.
    /// </summary>
    public class BandController : Controller
    {
        private readonly BandAgentService _band;
        private readonly BandSimulationService _simulation;
        private readonly IFleetService _fleet;

        public BandController(BandAgentService band, BandSimulationService simulation, IFleetService fleet)
        {
            _band = band;
            _simulation = simulation;
            _fleet = fleet;
        }

        // ---- Views ------------------------------------------------------------

        [HttpGet("/Band")]
        [HttpGet("/demo/band")]
        public IActionResult Console()
        {
            ViewData["Title"] = "Band Dispatch Console";
            ViewBag.Mode = _band.Mode;
            ViewBag.IsLive = _band.IsLive;
            ViewBag.Scenarios = BandSimulationService.Scenarios;
            ViewBag.Rooms = _band.ListRooms();
            ViewBag.Units = _fleet.GetAllUnits();
            return View();
        }

        [HttpGet("/Band/Room/{id}")]
        public IActionResult Room(string id)
        {
            var view = _band.GetRoomView(id);
            if (view is null) return RedirectToAction(nameof(Console));
            ViewData["Title"] = $"Band Room {id}";
            ViewBag.RoomId = id;
            ViewBag.Mode = _band.Mode;
            ViewBag.Units = _fleet.GetAllUnits();
            return View(view);
        }

        // ---- REST API ---------------------------------------------------------

        [HttpGet("/api/band/scenarios")]
        public IActionResult Scenarios() => Json(BandSimulationService.Scenarios);

        [HttpGet("/api/band/units")]
        public IActionResult Units() => Json(_fleet.GetAllUnits().Select(BandDto.Unit));

        [HttpGet("/api/band/rooms")]
        public IActionResult Rooms() => Json(_band.ListRooms().Select(BandDto.Room));

        [HttpGet("/api/band/rooms/{id}")]
        public IActionResult RoomView(string id)
        {
            var view = _band.GetRoomView(id);
            return view is null ? NotFound() : Json(BandDto.RoomView(view));
        }

        [HttpGet("/api/band/rooms/{id}/since/{seq:int}")]
        public IActionResult Since(string id, int seq) =>
            Json(_band.GetMessagesSince(id, seq).Select(BandDto.Message));

        [HttpPost("/api/band/simulate")]
        public IActionResult Simulate([FromBody] SimulateRequest? req)
        {
            var roomId = _simulation.Run(req?.Scenario, req?.AutoConfirm ?? true);
            return Json(new { roomId, url = $"/Band/Room/{roomId}" });
        }

        [HttpPost("/api/band/report")]
        public IActionResult Report([FromBody] ReportRequest req)
        {
            if (req is null || string.IsNullOrWhiteSpace(req.RawText))
                return BadRequest(new { error = "rawText is required" });
            var roomId = _band.StartIncident(req.RawText, req.Area ?? string.Empty, req.Channel ?? "Web");
            return Json(new { roomId, url = $"/Band/Room/{roomId}" });
        }

        [HttpPost("/api/band/rooms/{id}/decision")]
        public IActionResult Decision(string id, [FromBody] DecisionRequest req)
        {
            if (req is null) return BadRequest();
            var ok = _band.SubmitHumanDecision(id, req.Decision ?? "confirm", req.UnitId, req.Note, req.DispatcherName);
            return ok ? Json(new { ok = true }) : NotFound();
        }

        public class SimulateRequest
        {
            public string? Scenario { get; set; }
            public bool AutoConfirm { get; set; } = true;
        }

        public class ReportRequest
        {
            public string RawText { get; set; } = string.Empty;
            public string? Area { get; set; }
            public string? Channel { get; set; }
        }

        public class DecisionRequest
        {
            public string? Decision { get; set; }
            public string? UnitId { get; set; }
            public string? Note { get; set; }
            public string? DispatcherName { get; set; }
        }
    }
}

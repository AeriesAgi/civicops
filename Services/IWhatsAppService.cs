using System.Threading.Tasks;

namespace CivicOps.Services
{
    public class WhatsAppStatus
    {
        public bool Enabled { get; set; }
        public bool DemoMode { get; set; }
        public bool CanSend { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Mode { get; set; } = string.Empty;
        public string GraphVersion { get; set; } = string.Empty;
        public string PublicBaseUrl { get; set; } = string.Empty;
    }

    public interface IWhatsAppService
    {
        WhatsAppStatus GetStatus();
        bool VerifyToken(string mode, string token);
        Task<bool> SendTextAsync(string to, string message);
        string MaskPhone(string? phoneNumber);
    }
}

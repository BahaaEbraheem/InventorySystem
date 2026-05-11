using System;

namespace InventorySystem.Shared.Responses
{
    public class ResponseMeta
    {
        public string TraceId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? Environment { get; set; }
        public string? Version { get; set; } = "1.0.0"; // Optional: API version

        public ResponseMeta(string traceId, string? environment = null)
        {
            TraceId = traceId;
            Timestamp = DateTime.UtcNow;
            Environment = environment;
        }

        public static ResponseMeta Create(string traceId, string environment) =>
            new(traceId, environment);
    }
}
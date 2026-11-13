using System.Text.Json.Serialization;

namespace CarCareTracker.Models
{
    /// <summary>
    /// Response object representing server health
    /// </summary>
    public class ServerHealth
    {
        public string Status { get { 
                if (Checks.All(x=>x.Success)) {
                    //all checks passed
                    return "pass"; 
                }
                else if (Checks.All(x => !x.Success))
                { //all checks failed
                    return "fail";
                } else
                {
                    //partial failure
                    return "degraded";
                }
            } }
        public string Version { get; set; } = string.Empty;
        public TimeSpan TotalDuration { get; set; }
        public List<ServerHealthCheck> Checks { get; set; } = new List<ServerHealthCheck>();
    }
    public class ServerHealthCheck
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = "fail";
        [JsonIgnore]
        public bool Success { get; set; } = false;
    }
}
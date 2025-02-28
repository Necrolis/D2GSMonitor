using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace D2GSMonitor
{
    internal class JSON
    {
        public class RegEntry
        {
            [JsonProperty("key")]
            public string? Key { get; set; }
            [JsonProperty("type")]
            public string? ValueType { get; set; }
            [JsonProperty("string_value")]
            public string? SValue { get; set; }
            [JsonProperty("dword_value")]
            public uint DValue { get; set; }
        }

        public class Event
        {
            [JsonProperty("type")]
            public string? Type { get; set; }
            [JsonProperty("data")]
            public Dictionary<string, object>? Data { get; set; }
        }

        public class EndpointInfo
        {
            [JsonProperty("data")]
            public string? Data { get; set; }
            [JsonProperty("events")]
            public string? Events { get; set; }
            [JsonProperty("manifest")]
            public string? Manifest { get; set; }
            [JsonProperty("registry")]
            public string? Registry { get; set; }
        }

        public class TelnetInfo
        {
            [JsonProperty("port")]
            public short Port { get; set; }
            [JsonProperty("password")]
            public string? Password { get; set; }
        }

        public class ReportingInfo
        {
            [JsonProperty("status")]
            public int StatusTime { get; set; }
            [JsonProperty("games")]
            public int GamesTime { get; set; }
        }

        public class AuthInfo
        {
            [JsonProperty("header")]
            public string? Header { get; set; }
            [JsonProperty("value")]
            public string? Value { get; set; }
        }

        public class UpdateInfo
        {
            [JsonProperty("files")]
            public bool Files { get; set; }
            [JsonProperty("registry")]
            public bool Registry { get; set; }
        }

        public class WatchDogInfo
        {
            [JsonProperty("offest")]
            public int TickOffset { get; set; }
            [JsonProperty("timeout")]
            public int Timeout { get; set; }
        }

        public class Config
        {
            [JsonProperty("gsname")]
            public string? GSName { get; set; }
            [JsonProperty("executable")]
            public string? GSExecutablePath { get; set; }
            [JsonProperty("endpoints")]
            public EndpointInfo? CommandEndpoints { get; set; }
            [JsonProperty("auth")]
            public AuthInfo? CommandAuth { get; set; }
            [JsonProperty("telnet")]
            public TelnetInfo? Telnet { get; set; }
            [JsonProperty("restart_duration")]
            public int RestartTime { get; set; }
            [JsonProperty("restart_timeout")]
            public int RestartWaitTime { get; set; }
            [JsonProperty("update")]
            public UpdateInfo? Update { get; set; }
            [JsonProperty("report")]
            public ReportingInfo? Reporting { get; set; }
            [JsonProperty("watchdog")]
            public WatchDogInfo? WatchDog { get; set; }
            [JsonProperty("autostart")]
            public bool AutoStart { get; set; }
        }

        public class FileDownload
        {
            [JsonProperty("file")]
            public string? File { get; set; }
            [JsonProperty("md5")]
            public string? MD5 { get; set; }
            [JsonProperty("url")]
            public string? URL { get; set; }
            [JsonProperty("skip_existing")]
            public bool SkipIfPresent { get; set; }
        }

        public class Game
        {
            [JsonProperty("id")]
            public int Id { get; set; }
            [JsonProperty("name")]
            public string? Name { get; set; }
            [JsonProperty("password")]
            public string? Password { get; set; }
            [JsonProperty("expansion")]
            public bool IsExpansion { get; set; }
            [JsonProperty("hardcore")]
            public bool IsHardcore { get; set; }
            [JsonProperty("ladder")]
            public bool IsLadder { get; set; }
            [JsonProperty("enabled")]
            public bool IsEnabled { get; set; }
            [JsonProperty("difficulty")]
            public string? Difficulty { get; set; }
            [JsonProperty("characters")]
            public int Characters { get; set; }
            [JsonProperty("created")]
            public string? Created { get; set; }
        }

        public class MemoryStatus
        {
            [JsonProperty("name")]
            public string? Name { get; set; }
            [JsonProperty("current")]
            public double Current { get; set; }
            [JsonProperty("maximum")]
            public double Maximum { get; set; }
        }

        public class NetworkMetric
        {
            [JsonProperty("packets")]
            public long Packets { get; set; }
            [JsonProperty("bytes")]
            public long Bytes { get; set; }
            [JsonProperty("rate")]
            public double Rate { get; set; }
            [JsonProperty("peak_rate")]
            public double PeakRate { get; set; }
        }

        public class NetworkStatus
        {
            [JsonProperty("name")]
            public string? Name { get; set; }
            [JsonProperty("connected")]
            public bool Connected { get; set; }
            [JsonProperty("recv")]
            public NetworkMetric? Received { get; set; }
            [JsonProperty("send")]
            public NetworkMetric? Sent { get; set; }
        }

        public class CPUStatus
        {
            [JsonProperty("kernel")]
            public float Kernel { get; set; }
            [JsonProperty("user")]
            public float User { get; set; }
        }

        public class GamesStatus
        {
            [JsonProperty("maximum")]
            public int Maximum { get; set; }
            [JsonProperty("current_maximum")]
            public int CurrentMaximum { get; set; }
            [JsonProperty("active")]
            public int Active { get; set; }
            [JsonProperty("total")]
            public int Total { get; set; }
            [JsonProperty("maximum_life")]
            public int MaximumLife { get; set; }
        }

        public class PlayersStatus
        {
            [JsonProperty("maximum")]
            public int Maximum { get; set; }
            [JsonProperty("current")]
            public int Current { get; set; }
        }

        public class ServerStatus
        {
            [JsonProperty("games")]
            public GamesStatus? Games { get; set; }
            [JsonProperty("players")]
            public PlayersStatus? Players { get; set; }
            [JsonProperty("network")]
            public List<NetworkStatus>? Connections { get; set; }
            [JsonProperty("memory")]
            public List<MemoryStatus>? Memory { get; set; }
            [JsonProperty("cpu")]
            public CPUStatus? CPU { get; set; }
            [JsonProperty("motd")]
            public string? MOTD { get; set; }
        }

        public class Data<T>
        {
            [JsonProperty("time")]
            public DateTime Time { get; set; }
            [JsonProperty("type")]
            public string? Type { get; set; }
            [JsonProperty("gsname")]
            public string? GSName { get; set; }
            [JsonProperty("results")]
            public T? Results { get; set; }
        }
    }
}

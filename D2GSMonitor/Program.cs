using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text;
using Microsoft.Win32;
using Newtonsoft.Json;
using PrimS.Telnet;
using static D2GSMonitor.JSON;
using System.Text.RegularExpressions;
using System.Net.NetworkInformation;
using static System.Net.Mime.MediaTypeNames;
using System.Reflection;

namespace D2GSMonitor
{
    internal class Program
    {
        private static TimeSpan _restartTime = TimeSpan.FromHours(24);
        private static TimeSpan _exitWaitTime = TimeSpan.FromMinutes(5);
        private static readonly HttpClient _httpClient = new HttpClient();

        static async Task Main(string[] args)
        {
            var config = JsonConvert.DeserializeObject<JSON.Config>(File.ReadAllText("config.json"));
            if (config == null)
            {
                Console.WriteLine("Unable to load config.json!");
                return;
            }

            RegisterService(config.AutoStart);

            _restartTime = TimeSpan.FromMinutes(config.RestartTime);
            _exitWaitTime = TimeSpan.FromSeconds(config.RestartWaitTime);
            if (config.CommandAuth != null && !string.IsNullOrEmpty(config.CommandAuth.Header) && !string.IsNullOrEmpty(config.CommandAuth.Value))
            {
                _httpClient.DefaultRequestHeaders.Add(config.CommandAuth.Header, config.CommandAuth.Value);
            }

            if (config.Update != null && config.Update.Files && !string.IsNullOrEmpty(config.CommandEndpoints?.Manifest))
            {
                var files = await HttpGetJSON<List<JSON.FileDownload>>(config.CommandEndpoints.Manifest);
                if (files != null)
                {
                    Console.WriteLine("Running File Update");
                    foreach (var f in files)
                    {
                        if (string.IsNullOrEmpty(f.File) || string.IsNullOrEmpty(f.URL))
                        {
                            continue;
                        }

                        Console.Write($"Processing File: {f.File} ({f.MD5})...");
                        if ((File.Exists(f.File) && f.SkipIfPresent) || string.IsNullOrEmpty(f.MD5))
                        {
                            Console.WriteLine("Skipping");
                            continue;
                        }

                        var hash = StringToByteArray(f.MD5);
                        if (hash != null)
                        {
                            if (ByteArrayCompare(hash, GetFileMD5(f.File)))
                            {
                                Console.WriteLine("Unchanged");
                                continue;
                            }
                        }

                        Console.Write("Downloading");
                        var now = DateTime.Now;
                        var size = await HttpDownloadFile(f.URL, f.File);
                        var end = DateTime.Now;

                        Console.Write($": {ConvertSize(size)} ({end - now})");
                    }
                }
            }

            if (config.Update != null && config.Update.Registry && !string.IsNullOrEmpty(config.CommandEndpoints?.Registry) && OperatingSystem.IsWindows())
            {
                Console.WriteLine("Running Registry Update");
                var values = await HttpGetJSON<List<JSON.RegEntry>>(config.CommandEndpoints.Registry);
                if (values != null)
                {
                    using (var key = GetOrCreateMachineKey(@"Software\Wow6432Node\D2Server\D2GS"))
                    {
                        if (key != null)
                        {
                            foreach (var v in values)
                            {
                                if (string.IsNullOrEmpty(v.Key) || string.IsNullOrEmpty(v.ValueType))
                                {
                                    continue;
                                }

                                if (v.ValueType.Equals("dword", StringComparison.OrdinalIgnoreCase))
                                {
                                    key.SetValue(v.Key, v.DValue, RegistryValueKind.DWord);
                                }
                                else if (v.ValueType.Equals("string", StringComparison.OrdinalIgnoreCase))
                                {
                                    key.SetValue(v.Key, v.SValue ?? string.Empty, RegistryValueKind.String);
                                }
                            }
                        }
                    }
                }
            }

            var gsInfo = new FileInfo("D2GS.exe");
            if (gsInfo == null || !gsInfo.Exists)
            {
                Console.WriteLine("Cannot find D2GS.exe!");
                return;
            }

            SetAppCompat(gsInfo.FullName);

            var procInfo = new ProcessStartInfo
            {
                FileName = "D2GS.exe",
                Verb = "runas",
                UseShellExecute = true
            };

            while (true)
            {
                var start = DateTime.Now;
                var proc = Process.Start(procInfo);
                if (proc == null)
                {
                    Console.WriteLine("Unable to start D2GS.exe!");
                    break;
                }

                if (!string.IsNullOrEmpty(config.CommandEndpoints?.Events))
                {
                    _ = HttpPost(config.CommandEndpoints.Events, JsonConvert.SerializeObject(new JSON.Event
                    {
                        Type = "start",
                        Data = new Dictionary<string, object> {
                            {"gsname", config.GSName ?? string.Empty},
                            {"time", DateTime.UtcNow},
                        }
                    }));
                }

                Console.WriteLine("Starting Server");
                bool restartPending = false;
                bool deadlockDetected = false;
                DateTime exitStart = new DateTime(0);
                DateTime reportGamesLast = DateTime.Now;
                DateTime reportStatusLast = DateTime.Now;
                IntPtr watchdogAddress = GetGSWatchDogTimerAddress(proc, config.WatchDog?.TickOffset);

                while (!proc.WaitForExit(2500))
                {
                    var now = DateTime.Now;

                    if (restartPending && now - exitStart > _exitWaitTime.Add(TimeSpan.FromSeconds(30)))
                    {
                        Console.WriteLine("Force Stopping Server");
                        proc.Refresh();
                        proc.CloseMainWindow();
                        proc.Close();
                    }

                    if (config.Telnet != null)
                    {
                        if (now - start > _restartTime)
                        {
                            Console.WriteLine("Restarting Server");
                            await IssueTelnetCommand(config.Telnet, $"restart {config.RestartWaitTime}");
                            restartPending = true;
                            exitStart = DateTime.Now;
                        }

                        if (config.Reporting != null && !string.IsNullOrEmpty(config.CommandEndpoints?.Data))
                        {
                            if (config.Reporting.GamesTime != 0 && (now - reportGamesLast).TotalSeconds >= config.Reporting.GamesTime)
                            {
                                var games = await IssueTelnetCommand(config.Telnet, "gl");
                                if (!string.IsNullOrEmpty(games))
                                {
                                    _ = HttpPost(config.CommandEndpoints.Data, JsonConvert.SerializeObject(new JSON.Data<List<JSON.Game>>
                                    {
                                        Time = DateTime.UtcNow,
                                        Type = "games",
                                        GSName = config.GSName,
                                        Results = ParseGames(games)
                                    }));
                                }

                                reportGamesLast = now;
                            }

                            if (config.Reporting.StatusTime != 0 && (now - reportStatusLast).TotalSeconds >= config.Reporting.StatusTime)
                            {
                                var status = await IssueTelnetCommand(config.Telnet, "status");
                                if (!string.IsNullOrEmpty(status))
                                {
                                    _ = HttpPost(config.CommandEndpoints.Data, JsonConvert.SerializeObject(new JSON.Data<JSON.ServerStatus>
                                    {
                                        Time = DateTime.UtcNow,
                                        Type = "status",
                                        GSName = config.GSName,
                                        Results = ParseStatus(status)
                                    }));
                                }

                                reportStatusLast = now;
                            }
                        }
                    }

                    uint watchdog = GetGSWatchDogTimer(proc.Handle, watchdogAddress);
                    if (watchdog > 0 && config.WatchDog != null && config.WatchDog.Timeout > 0)
                    {
                        if (GetTickCount() - watchdog > config.WatchDog.Timeout)
                        {
                            Console.WriteLine("D2GE has deadlocked, queing restart");
                            deadlockDetected = true;
                            restartPending = true;
                        }
                    }
                }

                var reason = restartPending ? "routine" : (deadlockDetected ? "deadlock" : "crash");
                if (!string.IsNullOrEmpty(config.CommandEndpoints?.Events))
                {
                    _ = HttpPost(config.CommandEndpoints.Events, JsonConvert.SerializeObject(new JSON.Event
                    {
                        Type = "restart",
                        Data = new Dictionary<string, object> {
                            {"gsname", config.GSName ?? string.Empty},
                            {"time", DateTime.UtcNow},
                            {"reason", reason}
                        }
                    }));
                }

                Console.WriteLine($"Server Stopped ({reason})");
                Thread.Sleep(1000);
            }
        }

        private static void RegisterService(bool Enabled)
        {
            if (OperatingSystem.IsWindows())
            {
                Console.WriteLine(Enabled ? "Registed For AutoStart" : "Unregisted For AutoStart");
                using (var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    if (key != null)
                    {
                        const string AppName = "D2GSMonitor";
                        if (Enabled)
                        {
                            key.SetValue(AppName, System.Reflection.Assembly.GetExecutingAssembly().Location);
                        }
                        else
                        {
                            key.DeleteValue(AppName, false);
                        }
                    }
                }
            }
        }

        static async Task<string> HttpGet(string URL)
        {
            using (var response = await _httpClient.GetAsync(URL))
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return await response.Content.ReadAsStringAsync();
                }

                return string.Empty;
            }
        }

        static async Task<T?> HttpGetJSON<T>(string URL) where T : class
        {
            using (var response = await _httpClient.GetAsync(URL))
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(json))
                    {
                        return JsonConvert.DeserializeObject<T>(json);
                    }
                }

                return null;
            }
        }

        static async Task<bool> HttpPost(string URL, string JSONBody)
        {
            using (var request = await _httpClient.PostAsync(URL, new StringContent(JSONBody, Encoding.UTF8, "application/json")))
            {
                return request.IsSuccessStatusCode;
            }
        }

        static async Task<long> HttpDownloadFile(string URL, string FilePath)
        {
            using (var response = await _httpClient.GetAsync(URL))
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    using (var fs = new FileStream(FilePath, FileMode.Create))
                    {
                        await response.Content.CopyToAsync(fs);
                        return fs.Length;
                    }
                }

                return 0;
            }
        }

        private static int GetInt(string s, int defaultValue)
        {
            if (int.TryParse(s, out int result))
            {
                return result;
            }

            return defaultValue;
        }

        private static double GetDouble(string s, double defaultValue)
        {
            if (double.TryParse(s, out double result))
            {
                return result;
            }

            return defaultValue;
        }

        private static float GetFloat(string s, float defaultValue)
        {
            if (float.TryParse(s, out float result))
            {
                return result;
            }

            return defaultValue;
        }

        private static string SkipAndTrim(string s)
        {
            var i = s.IndexOf(':');
            if (i == -1)
            {
                return string.Empty;
            }

            return s.Substring(i + 1, s.Length - 1).Trim();
        }

        private static ServerStatus ParseStatus(string status)
        {
            var entries = status.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var vmem = SkipAndTrim(entries[10]).Replace("MB", string.Empty).Split('/');
            var pmem = SkipAndTrim(entries[9]).Replace("MB", string.Empty).Split('/');
            return new ServerStatus
            {
                Games = new GamesStatus
                {
                    Maximum = GetInt(SkipAndTrim(entries[0]), -1),
                    CurrentMaximum = GetInt(SkipAndTrim(entries[1]), -1),
                    Active = GetInt(SkipAndTrim(entries[2]), -1),
                    Total = GetInt(SkipAndTrim(entries[6]), -1),
                    MaximumLife = GetInt(SkipAndTrim(entries[5]).Split(' ').First(), -1),
                },
                Players = new PlayersStatus
                {
                    Maximum = GetInt(SkipAndTrim(entries[4]), -1),
                    Current = GetInt(SkipAndTrim(entries[3]), -1),
                },
                Connections = new List<NetworkStatus>
                {
                    new NetworkStatus
                    {
                        Name = "d2cs",
                        Connected = SkipAndTrim(entries[7]).Equals("yes", StringComparison.OrdinalIgnoreCase),
                    },
                    new NetworkStatus
                    {
                        Name = "d2dbs",
                        Connected = SkipAndTrim(entries[8]).Equals("yes", StringComparison.OrdinalIgnoreCase)
                    },
                },
                Memory = new List<MemoryStatus>
                {
                    new MemoryStatus
                    {
                        Name = "virtual",
                        Maximum = GetDouble(vmem[1], 0.0),
                        Current = GetDouble(vmem[0], 0.0),
                    },
                    new MemoryStatus
                    {
                        Name = "physical",
                        Maximum = GetDouble(pmem[1], 0.0),
                        Current = GetDouble(pmem[0], 0.0),
                    }
                },
                CPU = new CPUStatus
                {
                    Kernel = GetFloat(SkipAndTrim(entries[11]).Replace("%", string.Empty), 0.0f),
                    User = GetFloat(SkipAndTrim(entries[12]).Replace("%", string.Empty), 0.0f),
                },
                MOTD = entries[21]
            };
        }

        private static readonly Regex _gameEntryRegex = new Regex(@"\| (\d{3})  ([\S ]{15})  ([\S ]{15})  ([\d ]{4})  ([\S ]{7})  ([\S ]{4})  ([\S ]{11}) ([\S ]{10}) ([\d ]{5}) ([\d\: ]{10}) ([\S ]{3}) \|", RegexOptions.Compiled);
        private static Game? ParseGame(string g)
        {
            var m = _gameEntryRegex.Match(g);
            if (m.Success)
            {
                return new Game
                {
                    Id = GetInt(m.Captures[3].Value.Trim(), -1),
                    Name = m.Captures[1].Value.Trim(),
                    Password = m.Captures[2].Value.Trim(),
                    IsExpansion = m.Captures[4].Value.Trim().Equals("exp", StringComparison.OrdinalIgnoreCase),
                    IsHardcore = m.Captures[5].Value.Trim().Equals("hc", StringComparison.OrdinalIgnoreCase),
                    IsLadder = m.Captures[7].Value.Trim().Equals("ladder", StringComparison.OrdinalIgnoreCase),
                    IsEnabled = m.Captures[10].Value.Trim().Equals("y", StringComparison.OrdinalIgnoreCase),
                    Difficulty = m.Captures[6].Value.Trim().ToLower(),
                    Characters = GetInt(m.Captures[8].Value.Trim(), -1),
                    Created = m.Captures[9].Value.Trim()
                };
            }
            else
            {
                return null;
            }
        }

        private static List<Game> ParseGames(string games)
        {
            var gameslist = new List<Game>();
            var entries = games.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (entries.Length > 1)
            {
                foreach (var e in entries.Skip(1).SkipLast(2))
                {
                    var game = ParseGame(e);
                    if (game != null)
                    {
                        gameslist.Add(game);
                    }
                }
            }

            return gameslist;
        }

        private static async Task<bool> IsTerminatedWithAsync(Client client, int loginTimeoutMs, string terminator)
        {
            return (await client.TerminatedReadAsync(terminator, TimeSpan.FromMilliseconds(loginTimeoutMs), 1).ConfigureAwait(false)).TrimEnd().EndsWith(terminator);
        }

        private static async Task<bool> TrySendPassword(Client client, int loginTimeoutMs, string password)
        {
            var isTerm = await IsTerminatedWithAsync(client, loginTimeoutMs, ":");
            if (isTerm)
            {
                await client.WriteAsync(password).ConfigureAwait(false);
                return await IsTerminatedWithAsync(client, loginTimeoutMs, ">");
            }

            return false;
        }

        static async Task<string?> IssueTelnetCommand(TelnetInfo Info, string Comamnd)
        {
            using (var client = new Client("localhost", Info.Port, new CancellationToken()))
            {
                if (await TrySendPassword(client, 500, Info.Password ?? string.Empty))
                {
                    try
                    {
                        await client.WriteLineAsync(Comamnd);
                        return await client.ReadAsync(TimeSpan.FromMilliseconds(500));
                    }
                    catch
                    {
                        return null;
                    }
                }

                return null;
            }
        }

        public static byte[] GetFileMD5(string FilePath)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(FilePath))
                {
                    return md5.ComputeHash(stream);
                }
            }
        }

        public static string ConvertSize(long size)
        {
            if (size > 1024 * 1024)
            {
                return $"{size / (1024.0 * 1024.0):0.##} MB";
            }

            if (size > 1024)
            {
                return $"{size / 1024.0:0.##} KB";
            }

            return $"{size} B";
        }

        public static void SetAppCompat(string GSPath)
        {
            if (OperatingSystem.IsWindows())
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", true))
                {
                    if (key != null)
                    {
                        key.SetValue(GSPath, "~ RUNASADMIN WINXPSP2", RegistryValueKind.String);
                    }
                }
            }
        }

        public static RegistryKey? GetOrCreateMachineKey(string RegPath)
        {
            if (OperatingSystem.IsWindows())
            {
                var key = Registry.LocalMachine.OpenSubKey(RegPath, true);
                if (key == null)
                {
                    return Registry.LocalMachine.CreateSubKey(RegPath, true);
                }

                return key;
            }

            return null;
        }

        //https://stackoverflow.com/questions/321370/how-can-i-convert-a-hex-string-to-a-byte-array
        public static byte[] StringToByteArray(string hex)
        {
            if (hex.Length % 2 == 1)
            {
                return new byte[0];
            }

            byte[] arr = new byte[hex.Length >> 1];
            for (int i = 0; i < hex.Length >> 1; ++i)
            {
                arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
            }

            return arr;
        }

        public static int GetHexVal(char hex)
        {
            int val = (int)hex;
            return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }

        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int memcmp(byte[] b1, byte[] b2, long count);

        public static bool ByteArrayCompare(byte[] b1, byte[] b2)
        {
            return b1.Length == b2.Length && memcmp(b1, b2, b1.Length) == 0;
        }

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        static extern uint GetTickCount();

        public static uint GetGSWatchDogTimer(IntPtr hProcess, IntPtr hAddress)
        {
            if (hAddress != IntPtr.Zero)
            {
                var valueBytes = new byte[4];
                int read = 0;

                if (ReadProcessMemory(hProcess, hAddress, valueBytes, 4, ref read) && read == 4)
                {
                    return BitConverter.ToUInt32(valueBytes, 0);
                }
            }

            return 0;
        }

        public static IntPtr GetGSWatchDogTimerAddress(Process proc, int? offset)
        {
            if (OperatingSystem.IsWindows() && offset != null && offset.Value != 0)
            {
                foreach (ProcessModule m in proc.Modules)
                {
                    if (!string.IsNullOrEmpty(m.ModuleName) && m.ModuleName.Equals("D2Server.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        return IntPtr.Add(m.BaseAddress, offset.Value);
                    }
                }
            }

            return IntPtr.Zero;
        }
    }
}
﻿using Libs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using yt_dlp_gui.Models;

namespace yt_dlp_gui.Wrappers {

    public class DLP {
        public enum DLPType {
            yd_dlp, youtube_dl
        }
        static public DLPType Type { get; set; } = DLPType.yd_dlp;
        static public string Path_DLP { get; set; } = string.Empty;
        static public string Path_Aria2 { get; set; } = string.Empty;
        public Dictionary<string, string> Options { get; set; } = new Dictionary<string, string>();
        public string Url { get; set; } = string.Empty;
        public bool IsLive { get; set; } = false;
        public HashSet<DLPError> StdErr { get; set; } = new();
        Process process = new();
        public enum DLPError { Sign, Unsupported }

        public DLP(string url = "") {
            Url = url;
            NoPlaylist().NoPart().Overwrite().IgnoreConfig();
            if (Type == DLPType.yd_dlp) {
                Options["--progress-template"] = "\""
                    + "[yt-dlp]," //0
                    + "%(progress._percent_str)s," //1
                    + "%(progress._eta_str)s," //2
                    + "%(progress.downloaded_bytes)s," //3
                    + "%(progress.total_bytes)s," //4
                    + "%(progress.speed)s," //5
                    + "%(progress.eta)s" //6
                    + "\"";
            }
        }
        public DLP NoPart() {
            Options["--no-part"] = "";
            return this;
        }
        public DLP IgnoreConfig() {
            Options["--ignore-config"] = "";
            return this;
        }
        public DLP Proxy(string proxy_url) {
            Options["--proxy"] = "\"" + proxy_url + "\"";
            return this;
        }
        public DLP LoadConfig(string path) {
            Options.Remove("--ignore-config");
            Options["--config-location"] = "\"" + path + "\"";
            return this;
        }
        public DLP Output(string targetpath) {
            Options["-o"] = targetpath;
            return this;
        }
        public DLP DownloadSections(string regex) {
            Options["--download-sections"] = "\"" + regex + "\"";
            return this;
        }
        public DLP Subtitle(string lang, string targetpath) {
            Options["--sub-format"] = "vtt";
            Options["--sub-langs"] = lang;
            Options["--write-subs"] = "";
            Options["--skip-download"] = "";
            Options["-o"] = targetpath;
            return this;
        }
        public DLP NoPlaylist() {
            Options["--no-playlist"] = "";
            return this;
        }
        public DLP GetInfo() {
            Options["-j"] = "";
            return this;
        }
        public DLP Overwrite() {
            if (Type == DLPType.yd_dlp) {
                //Options["--force-overwrites"] = "";
            }
            return this;
        }
        public DLP UseAria2() {
            if (File.Exists(Path_Aria2)) {
                Options["--external-downloader"] = "\"" + Path_Aria2 + "\"";
                //Options["--external-downloader"] = "aria2c";
                //Options["--downloader-args"] = "aria2c:\"-x 16 -k 10M --user-agent=''\"";
            }
            return this;
        }
        public DLP Cookie(CookieType type) {
            switch (type) {
                case CookieType.Chrome:
                    Options["--cookies-from-browser"] = $"chrome";
                    break;
                case CookieType.Edge:
                    Options["--cookies-from-browser"] = $"edge";
                    break;
                case CookieType.Firefox:
                    Options["--cookies-from-browser"] = $"firefox";
                    break;
                case CookieType.Opera:
                    Options["--cookies-from-browser"] = $"opera";
                    break;
                case CookieType.Chromium:
                    Options["--cookies-from-browser"] = $"chromium";
                    break;
                case CookieType.Chrome_Beta:
                    var AppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var CookiePath = Path.Combine(AppData, "Google", "Chrome Beta");
                    Options["--cookies-from-browser"] = $"chrome:{CookiePath}";
                    break;
            }
            return this;
        }
        private string Args {
            get {
                var args = Options.Select(x => {
                    if (x.Key == "-o") {
                        return $"{x.Key} \"{x.Value}\"";
                    } else if (string.IsNullOrWhiteSpace(x.Value)) {
                        return x.Key;
                    } else {
                        return $"{x.Key} {x.Value}";
                    }
                }).ToList();
                args.Add($"\"{Url}\"");
                return string.Join(" ", args);
            }
        }
        public DLP DownloadFormat(string format_id, string targetpath) {
            Options["-f"] = format_id;
            Options["-o"] = targetpath;
            return this;
        }
        private static Regex ErrSign = new Regex(@"^(?=.*?ERROR)(?=.*?sign)(?=.*?confirm)", RegexOptions.IgnoreCase);
        private static Regex ErrUnsupported = new Regex(@"^(?=.*?ERROR)(?=.*?Unsupported)", RegexOptions.IgnoreCase);

        public Process Exec(Action<string> stdall = null, Action<string> stdout = null, Action<string> stderr = null) {
            //var fn = App.Path(App.Folders.bin, "yt-dlp.exe");
            var fn = Path_DLP;
            var info = new ProcessStartInfo() {
                FileName = fn,
                Arguments = Args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            Debug.WriteLine(Args);
            process.StartInfo = info;
            process.EnableRaisingEvents = true;
            process.OutputDataReceived += (s, e) => {
                Debug.WriteLine(e.Data, "STD");
                if (!string.IsNullOrWhiteSpace(e.Data)) {
                    stdall?.Invoke(e.Data);
                    stdout?.Invoke(e.Data);
                }
            };
            process.ErrorDataReceived += (s, e) => {
                Debug.WriteLine(e.Data, "ERR");
                if (!string.IsNullOrWhiteSpace(e.Data)) {
                    stdall?.Invoke(e.Data);
                    stderr?.Invoke(e.Data);
                    if (ErrSign.IsMatch(e.Data)) StdErr.Add(DLPError.Sign);
                    if (ErrUnsupported.IsMatch(e.Data)) StdErr.Add(DLPError.Unsupported);
                }
            };

            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            process.WaitForExit();
            return process;
        }
        public DLP Close() {
            Debug.WriteLine("CLOSE");
            Util.SendCtrlC(process);
            if (Options.ContainsKey("-o")) {
                var tempfile = Options["-o"];
                if (!IsLive) {
                    if (File.Exists(tempfile)) File.Delete(tempfile);
                }
            }
            return this;
        }
        public DLP Err(DLPError err, Action callback) {
            if (StdErr.Contains(err)) callback.Invoke();
            return this;
        }
    }
}

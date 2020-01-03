using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using System.IO;

namespace BlazorDualMode.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseConfiguration(new ConfigurationBuilder()
                    .AddCommandLine(args)
                    .Build())
                .UseStartup<Startup>()
                .UseUrls($"http://*:{(args.Length > 0 ? args[0] : "5000")}")
                .Build();
    }

    public class Daemon
    {
        public static void Main(string[] args)
        {
            if (args.Length >= 1)
            {
                var netDllPath = typeof(Program).Assembly.Location;
                var dllFileName = Path.GetFileName(netDllPath);
                var serviceName = "dotnet-" + Path.GetFileNameWithoutExtension(dllFileName).ToLower();
                switch (args[0])
                {
                    case "install":
                        InstallService(netDllPath, args.Length > 1 ? args[1..^0] : null, true);
                        return;
                    case "uninstall":
                        InstallService(netDllPath, null, false);
                        return;
                    case "start":
                        ControlService(serviceName, "start");
                        return;
                    case "stop":
                        ControlService(serviceName, "stop");
                        return;
                }
            }

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
            };


            Program.Main(args);
        }

        static int InstallService(string netDllPath, string[] args, bool doInstall)
        {
            var dllFileName = Path.GetFileName(netDllPath);
            var osName = Environment.OSVersion.ToString();

            FileInfo fi = null;

            try
            {
                fi = new FileInfo(netDllPath);
            }
            catch { }

            if (doInstall == true && fi != null && fi.Exists == false)
            {
                WriteLog("NOT FOUND: " + fi.FullName);
                return 1;
            }

            var serviceName = "dotnet-" + Path.GetFileNameWithoutExtension(dllFileName).ToLower();

            var exeName = Process.GetCurrentProcess().MainModule.FileName;

            var workingDir = Path.GetDirectoryName(fi.FullName);

            string serviceFilePath = $"/etc/systemd/system/{serviceName}.service";

            if (doInstall == true)
            {
                var execStart = "";
                if (exeName.EndsWith("dotnet") == true)
                    execStart = $"{exeName} {fi.FullName}";
                else
                    execStart = exeName;
                var exeArgs = string.Concat(args ?? new[] { "" });

                var fullText = $@"
[Unit]
Description={dllFileName} running on {osName}
[Service]
WorkingDirectory={workingDir}
ExecStart={execStart} {exeArgs}
KillSignal=SIGINT
SyslogIdentifier={serviceName}
[Install]
WantedBy=multi-user.target
";
                Console.WriteLine(fullText);

                File.WriteAllText(serviceFilePath, fullText);
                WriteLog(serviceFilePath + " Created");
                ControlService(serviceName, "enable");
                ControlService(serviceName, "start");
            }
            else
            {
                if (File.Exists(serviceFilePath) == true)
                {
                    ControlService(serviceName, "stop");
                    File.Delete(serviceFilePath);
                    WriteLog(serviceFilePath + " Deleted");
                }
            }

            return 0;
        }

        static int ControlService(string serviceName, string mode)
        {
            string servicePath = $"/etc/systemd/system/{serviceName}.service";

            if (File.Exists(servicePath) == false)
            {
                WriteLog($"No service: {serviceName} to {mode}");
                return 1;
            }

            var psi = new ProcessStartInfo();
            psi.FileName = "systemctl";
            psi.Arguments = $"{mode} {serviceName}";
            var child = Process.Start(psi);
            child.WaitForExit();
            return child.ExitCode;
        }

        static void WriteLog(string text)
        {
            Console.WriteLine(text);
        }
    }
}

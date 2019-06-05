﻿using System;
using System.Net;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GridFSSyncService.Composition
{
    public static class Program
    {
#pragma warning disable SA1011 // Closing square brackets should be spaced correctly -- StyleCop fails to handle nullable arrays
        public static async Task Main(string?[]? args)
#pragma warning restore SA1011 // Closing square brackets should be spaced correctly
        {
            ConfigureNetworking();
            var commandLineOptions = GetCommandLineOptions(args);
            var appConfiguration = LoadAppConfiguration(commandLineOptions.Config);
            while (true)
            {
                var hostingOptions = GetHostingOptions(commandLineOptions.HostingConfig);
                using (var host = BuildWebHost(hostingOptions, appConfiguration))
                {
                    await host.RunAsync();
                }
            }
        }

        private static IConfiguration LoadAppConfiguration(string configPath)
            => new ConfigurationBuilder()
                .AddJsonFile(configPath, optional: false, reloadOnChange: true)
                .AddEnvironmentVariables("GSS_")
                .Build();

#pragma warning disable SA1011 // Closing square brackets should be spaced correctly -- StyleCop fails to handle nullable arrays
        private static CommandLineOptions GetCommandLineOptions(string?[]? args)
#pragma warning restore SA1011 // Closing square brackets should be spaced correctly
            => new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build()
                .Get<CommandLineOptions>() ?? new CommandLineOptions();

        private static HostingOptions GetHostingOptions(string configPath)
            => new ConfigurationBuilder()
                .AddJsonFile(configPath, optional: false)
                .Build()
                .Get<HostingOptions>() ?? new HostingOptions();

        private static IWebHost BuildWebHost(HostingOptions hostingOptions, IConfiguration appConfiguration)
            => new WebHostBuilder()
                .UseSetting(WebHostDefaults.ServerUrlsKey, hostingOptions.Listen)
                .ConfigureLogging(loggingBuilder => ConfigureLogging(loggingBuilder, hostingOptions))
                .UseKestrel(options => ConfigureKestrel(options))
                .ConfigureServices(services => services.AddSingleton(appConfiguration))
                .UseStartup<Startup>()
                .Build();

        private static void ConfigureLogging(ILoggingBuilder loggingBuilder, HostingOptions hostingOptions)
        {
            loggingBuilder.AddFilter(Filter);
            var hasJournalD = Tmds.Systemd.Journal.IsSupported;
            if (hasJournalD)
            {
                loggingBuilder.AddJournal(options => options.SyslogIdentifier = "gridfs-sync-service");
            }

            if (!hasJournalD || hostingOptions.ForceConsoleLogging)
            {
                loggingBuilder.AddConsole(options =>
                {
                    options.DisableColors = true;
                    options.IncludeScopes = true;
                });
            }

            static bool Filter(string category, LogLevel level)
                => level >= LogLevel.Warning
                    || level == LogLevel.Trace
                    || !category.StartsWith("Microsoft.AspNetCore.", StringComparison.OrdinalIgnoreCase);
        }

        private static void ConfigureKestrel(KestrelServerOptions options)
        {
            options.AddServerHeader = false;
            options.Limits.MaxRequestBodySize = 0;
            options.Limits.MaxRequestHeadersTotalSize = 4096;
            options.Limits.MaxConcurrentConnections = 10;
        }

        private static void ConfigureNetworking()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.CheckCertificateRevocationList = true;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.DnsRefreshTimeout = 3000;
            ServicePointManager.EnableDnsRoundRobin = true;
            ServicePointManager.ReusePort = true;
        }
    }
}

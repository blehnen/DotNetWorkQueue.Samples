using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using DotNetWorkQueue;
using DotNetWorkQueue.Interceptors;
using DotNetWorkQueue.Metrics.Net;
using DotNetWorkQueue.Queue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ConfigurationBuilder = Microsoft.Extensions.Configuration.ConfigurationBuilder;
using IMetrics = DotNetWorkQueue.IMetrics;
#if NET8_0_OR_GREATER
using DotNetWorkQueue.Dashboard.Client;
#endif

namespace SampleShared
{
    public static class Injectors
    {
        private static MetricsNet _metrics;
        private static MeterProvider _meterProvider;
        private static ActivitySource _tracer;

        public static void AddInjectors(ILoggerFactory logFactory,
            bool addTrace,
            bool addMetrics,
            bool enableGzip,
            bool enableEncryption,
            string appName,
            IContainer container)
        {
            container.Register<ILoggerFactory>(() => logFactory, LifeStyles.Singleton);
            if (addMetrics)
            {
                AddMetrics(container, appName);
            }

            if (addTrace)
            {
                AddTrace(container);
            }

            if (enableGzip || enableEncryption)
            {
                AddMessageInterceptors(container, enableEncryption, enableGzip);
            }

#if NET8_0_OR_GREATER
            if (_dashboardClient != null)
                AddDashboardMetrics(container);
#endif
        }

        public static void SetOptions(IContainer container, bool enableChaos)
        {
            var pol = container.GetInstance<IPolicies>();
            pol.EnableChaos = enableChaos;

            var history = container.GetInstance<IHistoryConfiguration>();
            history.Enabled = SharedConfiguration.EnableHistory;
        }

        private static void AddMessageInterceptors(IContainer container,
            bool des, bool gzip)
        {
            //encryption keys for sample only
            string key = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            string iv = "aaaaaaaaaaa=";

            if (des && gzip)
            {
                var desConfiguration = new TripleDesMessageInterceptorConfiguration(Convert.FromBase64String(key), Convert.FromBase64String(iv));
                container.RegisterCollection<IMessageInterceptor>(new[]
                {
                    typeof (GZipMessageInterceptor), //gzip compression
                    typeof (TripleDesMessageInterceptor) //encryption
                });
                container.Register(() => desConfiguration, LifeStyles.Singleton);
            }
            else if (gzip)
            {
                container.RegisterCollection<IMessageInterceptor>(new[]
                {
                    typeof (GZipMessageInterceptor) //gzip compression
                });
            }
            else if (des)
            {
                var desConfiguration = new TripleDesMessageInterceptorConfiguration(Convert.FromBase64String(key), Convert.FromBase64String(iv));
                container.RegisterCollection<IMessageInterceptor>(new[]
                {
                    typeof (TripleDesMessageInterceptor) //encryption
                });
                container.Register(() => desConfiguration,
                    LifeStyles.Singleton);
            }
        }
        private static void AddMetrics(IContainer container, string appName)
        {
            if (_metrics != null)
            {
                container.RegisterNonScopedSingleton<IMetrics>(_metrics);
                return;
            }

            // DotNetWorkQueue 0.9.1+ uses System.Diagnostics.Metrics built into the core library.
            // Export metrics via OTLP to Prometheus (or any OTLP-compatible backend).
            // Prometheus supports OTLP ingestion natively since v2.47.
            //
            // Configure the endpoint in metricsettings.json, or fall back to console output.
            var metricsConfig = LoadMetricsConfig();
            var otlpEndpoint = metricsConfig?["OtlpEndpoint"];

            var meterBuilder = Sdk.CreateMeterProviderBuilder()
                .AddMeter("DotNetWorkQueue");

            if (!string.IsNullOrEmpty(otlpEndpoint))
            {
                meterBuilder.AddOtlpExporter((exporterOptions, readerOptions) =>
                {
                    exporterOptions.Endpoint = new Uri(otlpEndpoint);
                    exporterOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                    readerOptions.PeriodicExportingMetricReaderOptions = new PeriodicExportingMetricReaderOptions
                    {
                        ExportIntervalMilliseconds = 5000
                    };
                });
                Console.WriteLine($"Metrics enabled — OTLP export to {otlpEndpoint} (every 5s)");
            }
            else
            {
                meterBuilder.AddConsoleExporter((exporterOptions, readerOptions) =>
                {
                    readerOptions.PeriodicExportingMetricReaderOptions = new PeriodicExportingMetricReaderOptions
                    {
                        ExportIntervalMilliseconds = 5000
                    };
                });
                Console.WriteLine("Metrics enabled — console output (set OtlpEndpoint in metricsettings.json for Prometheus)");
            }

            // Create MetricsNet (and its Meter) BEFORE building the provider,
            // so the provider can discover the meter instance.
            var metrics = new MetricsNet();

            _meterProvider = meterBuilder.Build();

            container.RegisterNonScopedSingleton<IMetrics>(metrics);
            _metrics = metrics;
        }

        private static IConfigurationSection LoadMetricsConfig()
        {
            try
            {
                if (!File.Exists("metricsettings.json"))
                    return null;

                return new ConfigurationBuilder()
                    .AddJsonFile("metricsettings.json", optional: true)
                    .Build()
                    .GetSection("Metrics");
            }
            catch
            {
                return null;
            }
        }

#if NET8_0_OR_GREATER
        private static DashboardConsumerClient _dashboardClient;

        public static DashboardConsumerClient StartDashboardRegistration(string queueName, string friendlyName)
        {
            if (!SharedConfiguration.EnableDashboard)
                return null;

            var options = new DashboardClientOptions
            {
                DashboardApiUrl = SharedConfiguration.DashboardApiUrl,
                QueueName = queueName,
                FriendlyName = friendlyName
            };

            var client = new DashboardConsumerClient(options);
            client.StartAsync().GetAwaiter().GetResult();
            _dashboardClient = client;
            return client;
        }

        public static void StopDashboardRegistration(DashboardConsumerClient client)
        {
            if (client == null) return;
            _dashboardClient = null;
            client.StopAsync().GetAwaiter().GetResult();
            client.Dispose();
        }

        public static void AddDashboardMetrics(IContainer container)
        {
            if (_dashboardClient != null)
            {
                var client = _dashboardClient;
                container.Register<IConsumerMetricsNotification>(
                    () => new ConsumerMetricsNotification(
                        client.IncrementProcessed,
                        client.IncrementErrored,
                        client.IncrementRolledBack,
                        client.IncrementPoisonMessage),
                    LifeStyles.Singleton);
            }
        }
#endif

        private static void AddTrace(IContainer container)
        {
            if (_tracer != null)
            {
                container.RegisterNonScopedSingleton(_tracer);
                return;
            }
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("tracesettings.json")
                .Build()
                .GetSection("Jaeger");

            var openTelemetry = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(configuration["JAEGER_SERVICE_NAME"]))
                .AddSource(configuration["JAEGER_SERVICE_NAME"], configuration["JAEGER_SERVICE_NAME"])
                .AddOtlpExporter(o =>
                {
                    var host = configuration["JAEGER_AGENT_HOST"];
                    var port = int.Parse(configuration["JAEGER_AGENT_PORT"]);
                    o.Endpoint = new Uri($"http://{host}:{port}");

                    // Using Batch Exporter (which is default)
                    // The other option is ExportProcessorType.Simple
                    o.ExportProcessorType = ExportProcessorType.Batch;
                    o.BatchExportProcessorOptions = new BatchExportProcessorOptions<Activity>()
                    {
                        MaxQueueSize = 2048,
                        ScheduledDelayMilliseconds = 5000,
                        ExporterTimeoutMilliseconds = 30000,
                        MaxExportBatchSize = 512,
                    };
                })
                .Build();

            _tracer = new ActivitySource(configuration["JAEGER_SERVICE_NAME"]);
            container.RegisterNonScopedSingleton(_tracer);
        }
    }
}

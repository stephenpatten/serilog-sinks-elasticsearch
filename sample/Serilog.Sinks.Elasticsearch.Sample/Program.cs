
namespace Serilog.Sinks.Elasticsearch.Sample
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using Serilog;
    using Serilog.Context;
    using Serilog.Debugging;
    using Serilog.Events;
    using Serilog.Formatting.Json;
    using Serilog.Sinks.RollingFile;
    using Serilog.Sinks.SystemConsole.Themes;

    class Program
    {
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .WriteTo.Console(theme: SystemConsoleTheme.Literate)
                .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://elastic:changeme@localhost:9200")) // for the docker-compose implementation
                {
                    AutoRegisterTemplate = true,
                    //BufferBaseFilename = "./buffer",
                    RegisterTemplateFailure = RegisterTemplateRecovery.IndexAnyway,
                    FailureCallback = e => Console.WriteLine("Unable to submit event " + e.MessageTemplate),
                    EmitEventFailure = EmitEventFailureHandling.WriteToSelfLog |
                                       EmitEventFailureHandling.WriteToFailureSink |
                                       EmitEventFailureHandling.RaiseCallback,
                    FailureSink = new RollingFileSink("./fail-{Date}.txt", new JsonFormatter(), null, null)
                })
                .CreateLogger();

            // Enable the selflog output
            SelfLog.Enable(Console.Error);

            var myLog = Log.ForContext<Program>();

            using (myLog.BeginTimedOperation("Time a thread sleep for 2 seconds."))
            {
                Thread.Sleep(2000);
            }

            myLog.Information("Hello, world!");

            int a = 10, b = 0;
            try
            {
                myLog.Debug("Dividing {A} by {B}", a, b);
                Console.WriteLine(a / b);
            }
            catch (Exception ex)
            {
                myLog.Error(ex, "Something went wrong");
            }

            // Introduce a failure by storing a field as a different type
            myLog.Debug("Reusing {A} by {B}", "string", true);

            myLog.Information("No contextual properties");

            using (LogContext.PushProperty("A", 1))
            {
                myLog.Information("Carries property A = 1");

                using (LogContext.PushProperty("A", 2))
                using (LogContext.PushProperty("B", 1))
                {
                    myLog.Information("Carries A = 2 and B = 1");
                }

                myLog.Information("Carries property A = 1, again");
            }

            // metrics - counter
            var counter = myLog.CountOperation("counter", "operation(s)", true, LogEventLevel.Debug);
            counter.Increment();
            counter.Increment();
            counter.Increment();
            counter.Decrement();

            // metrics - gauge
            var queue = new Queue<int>();
            var gauge = myLog.GaugeOperation("queue", "item(s)", () => queue.Count());

            gauge.Write();

            queue.Enqueue(20);

            gauge.Write();

            queue.Dequeue();

            gauge.Write();

            using (myLog.BeginTimedOperation("Using a passed in identifier", "test-loop"))
            {
                var at = string.Empty;
                for (int i = 0; i < 1000; i++)
                {
                    at += "b";
                }
            }

            using (myLog.BeginTimedOperation("This should execute within 1 second.", null, LogEventLevel.Debug, TimeSpan.FromSeconds(1)))
            {
                Thread.Sleep(1100);
            }

            Log.CloseAndFlush();
            Console.Read();
        }
    }
}

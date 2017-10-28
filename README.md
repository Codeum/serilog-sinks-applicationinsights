# Serilog.Sinks.ApplicationInsights

A sink for Serilog that writes events to Microsoft Application Insights.
 
[![Build status](https://build.codeum.com/app/rest/builds/buildType:%28id:SerilogSinksApplicationinsights_Build%29/statusIcon)](https://build.codeum.com/viewType.html?buildTypeId=SerilogSinksApplicationinsights_Build&guest=1)

This Sink comes with two main helper extensions that send Serilog `LogEvent` messages to Application Insights as either `EventTelemetry`:

```csharp
var log = new LoggerConfiguration()
    .WriteTo
	.ApplicationInsightsEvents("<MyApplicationInsightsInstrumentationKey>")
    .CreateLogger();
```


.. or as `TraceTelemetry`:


```csharp
var log = new LoggerConfiguration()
    .WriteTo
	.ApplicationInsightsTraces("<MyApplicationInsightsInstrumentationKey>")
    .CreateLogger();
```

For those two `LogEvent` instances that have Exceptions are always sent as Exceptions to AI though... well, by default.


Additionally, you can also customize *whether* to send the LogEvents at all, if so *which type(s)* of Telemetry to send and also *what to send* (all or no LogEvent properties at all), via a bit more bare-metal set of overloads that take a  `Func<LogEvent, IFormatProvider, ITelemetry> logEventToTelemetryConverter` parameter, i.e. like this to send over MetricTelemetries:

```csharp
var log = new LoggerConfiguration()
    .WriteTo
	.ApplicationInsights("<MyApplicationInsightsInstrumentationKey>", LogEventsToMetricTelemetryConverter)
    .CreateLogger();

// ....

private static ITelemetry LogEventsToMetricTelemetryConverter(LogEvent serilogLogEvent, IFormatProvider formatProvider)
{
    var metricTelemetry = new MetricTelemetry(/* ...*/);
    // forward properties from logEvent or ignore them altogether...
    return metricTelemetry;
}

```


.. or alternatively by using the built-in, default TraceTelemetry generation logic, but adapt the Telemetry's Context to include a UserId:


```csharp
public static void Main()
{
    var log = new LoggerConfiguration()
        .WriteTo
        .ApplicationInsights("<MyApplicationInsightsInstrumentationKey>", ConvertLogEventsToCustomTraceTelemetry)
        .CreateLogger();
}

private static ITelemetry ConvertLogEventsToCustomTraceTelemetry(LogEvent logEvent, IFormatProvider formatProvider)
{
    // first create a default TraceTelemetry using the sink's default logic
    // .. but without the log level, and (rendered) message (template) included in the Properties
    var telemetry = logEvent.ToDefaultTraceTelemetry(
        formatProvider,
        includeLogLevelAsProperty: false,
        includeRenderedMessageAsProperty: false,
        includeMessageTemplateAsProperty: false);

    // then go ahead and post-process the telemetry's context to contain the user id as desired
    if (logEvent.Properties.ContainsKey("UserId"))
    {
        telemetry.Context.User.Id = logEvent.Properties["UserId"].ToString();
    }

    // and remove the UserId from the Telemetry .Properties (we don't need redundancies)
    if (telemetry.Properties.ContainsKey("UserId"))
    {
        telemetry.Properties.Remove("UserId");
    }
	
    return telemetry;
}
```

If you want to skip sending a particular LogEvent, just return `null` from your own converter method.


## How, When and Why to Flush Messages Manually

### Or: Where did my Messages go?

As explained by the [Application Insights documentation](https://azure.microsoft.com/en-us/documentation/articles/app-insights-api-custom-events-metrics/#flushing-data), the default behaviour of the AI client is to buffer messages and send them to AI in batches whenever the client seems fit. However, this may lead to lost messages when your application terminates while there are still unsent messages in said buffer.

You can either use Persistent Channels (see below) or control when AI shall flush its messages, for example when your application closes:

1.) Create a custom `TelemetryClient` and hold on to it in a field or property:

```csharp
// private TelemetryClient _telemetryClient;

// ...
_telemetryClient = new TelemetryClient()
            {
                InstrumentationKey = "<My AI Instrumentation Key>"
            };
```

2.) Use that custom `TelemetryClient` to initialize the Sink:

```csharp
var log = new LoggerConfiguration()
    .WriteTo
	.ApplicationInsightsEvents(telemetryClient)
    .CreateLogger();
```

3.) Call .Flush() on the TelemetryClient whenever you deem necessary, i.e. Application Shutdown:

```csharp
_telemetryClient.Flush();

// The AI Documentation mentions that calling .Flush() *can* be asynchronous and non-blocking so
// depending on the underlying Channel to AI you might want to wait some time
// specific to your application and its connectivity constraints for the flush to finish.

await Task.Delay(1000);

// or 

System.Threading.Thread.Sleep(1000);

```

## Using AI Persistent Channels
By default the Application Insights client and therefore also this Sink use an in-memory buffer of messages which are sent out periodically whenever the AI client deems necessary. This may lead to unexpected behaviour upon process termination, particularly [not all of your logged messages may have been sent and therefore be lost](https://github.com/serilog/serilog-sinks-applicationinsights/pull/9).

Besides flushing the messages manually (see above), you can also use a custom `ITelemetryChannel` such as the [Persistent Channel(s)](https://azure.microsoft.com/en-us/documentation/articles/app-insights-windows-services/#persistence-channel) one with this Sink and thereby *not* lose messages, i.e. like this:

1.) Add the [Microsoft.ApplicationInsights.PersistenceChannel](https://www.nuget.org/packages/Microsoft.ApplicationInsights.PersistenceChannel) to your project

2.) Create a `TelemetryConfiguration` using the Persistence Channel:

```csharp
var configuration = new TelemetryConfiguration()
            {
                InstrumentationKey = "<My AI Instrumentation Key>",
                TelemetryChannel = new PersistenceChannel()
            };
```

3.) Use that custom `TelemetryConfiguration` to initialize the Sink:

```csharp
var log = new LoggerConfiguration()
    .WriteTo
	.ApplicationInsightsEvents(configuration)
    .CreateLogger();
```

Copyright &copy; 2016 Serilog Contributors - Provided under the [Apache License, Version 2.0](http://apache.org/licenses/LICENSE-2.0.html).

See also: [Serilog Documentation](https://github.com/serilog/serilog/wiki)

## Accessing structured data

Serilog data is formatted as JSON and made available in the `customDimensions` property. [Log Analytics](https://docs.microsoft.com/en-us/azure/application-insights/app-insights-analytics) can be used to drill into this data. Use the [`todynamic`](https://docs.loganalytics.io/docs/Language-Reference/Scalar-functions/todynamic()) scalar function to convert the JSON:

```
traces | extend properties = todynamic(tostring(customDimensions))
```

The `properties` calculated column has three property values:

- `LogLevel`: The Serilog log event level (`Verbose`, `Debug`, `Information`, `Warning`, `Error` or `Fatal`)
- `MessageTemplate`: The Serilog message template
- `RenderedMessage`: The Serilog log event rendered as text

The last property is only included when logging as custom events. However, when logging as traces then the rendered message is directly available in the `message` column.

Any Serilog named parameters are also included in `customDimensions`.

Assume the following data is logged as traces:

``` csharp
var position = new { Latitude = 25, Longitude = 134 };
var elapsedMs = 34;

log.Information("Processed {@Position} in {Elapsed} ms.", position, elapsedMs);
```

Then Log Analytics can be used to filter on `Elapsed`:

```
traces
    | extend properties = todynamic(tostring(customDimensions))
    | where properties.Elapsed == 34
```

While the values of structured objects is displayed when inspecting the `properties` column (and also the `customDimensions` column) it is necessary to use the `todynamic` function to filter on these values:

```
traces
    | extend properties = todynamic(tostring(customDimensions))
    | extend position = todynamic(tostring(properties.Position))
    | where position.Latitude == 25
```

This procedure has to be repeated when drilling deeper into subobjects.

The type of a structured value is available in the `_typeName` property (not included for simple or compiler generated types).
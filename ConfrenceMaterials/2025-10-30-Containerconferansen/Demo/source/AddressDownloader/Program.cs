global using static AddressDownloader.ConfigKeys;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Reflection;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Microsoft.Extensions.Logging;
using No.Nhn.Address.Cadastre.ImportFormat;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry;

#region Functions

async Task<string> DownloadZip(TelemetrySpan telemetrySpan, ILogger logger, string csvUrl, string workDirectory,
    string zipFileName, string correlationId)
{
    // https://stackoverflow.com/a/71949994
    telemetrySpan.AddEvent("Download starting");
    var downloadDestination = Path.Combine(workDirectory, Path.GetFileName(zipFileName));
    if (File.Exists(downloadDestination))
    {
        logger.LogInformation("{CorrelationId} Skipping download because file \"{DownloadDestination}\" already exists", correlationId, downloadDestination);
        return downloadDestination;
    }
    logger.LogInformation("{CorrelationId} Downloading zip of csv of cadastre (\"matrikkel\") addresses from \"{CsvUrl}\" to \"{DownloadDestination}\"", correlationId, csvUrl, downloadDestination);
    using var client = new HttpClient();
    await using var zipDownloadStream = await client.GetStreamAsync(csvUrl);
    await using var zipDownloadFileStream = new FileStream(downloadDestination, FileMode.OpenOrCreate);
    await zipDownloadStream.CopyToAsync(zipDownloadFileStream);
    telemetrySpan.AddEvent("Download finished");
    return downloadDestination;
}

string CsvExtracted(TelemetrySpan telemetrySpan, ILogger logger, string correlationId, string downloadDestination,
    string csvFileName, string workDirectory)
{
    // https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-compress-and-extract-files
    telemetrySpan.AddEvent("Extracting zip file started");
    logger.LogInformation("{CorrelationId} Extracting downloaded csv", correlationId);
    using ZipArchive archive = ZipFile.OpenRead(downloadDestination);
    var relevantEntries = archive.Entries.Where(e => !e.FullName.EndsWith('/')).ToList();
    if (relevantEntries.Count != 1 || !relevantEntries[0].Name.EndsWith(csvFileName))
    {
        logger.LogError("{CorrelationId} Downloaded zip file \"{DownloadDestination}\" contained \"{EntriesCount}\" entries, expected there to be only 1 called \"{CsvFileName}\". The entries in the zip file are: {Serialize}", correlationId, downloadDestination, archive.Entries.Count, csvFileName, System.Text.Json.JsonSerializer.Serialize(archive.Entries.Select(ae => ae.FullName).ToList()));
        throw new Exception($"{correlationId} Downloaded zip didn't contain expected entries.");
    }
    var csvExtracted = Path.Combine(workDirectory, Path.GetFileName(csvFileName));
    relevantEntries[0].ExtractToFile(destinationFileName: csvExtracted, overwrite: true);
    telemetrySpan.AddEvent("Extracting zip file done");
    return csvExtracted;
}

string GetCsvHeaderLine(TelemetrySpan telemetrySpan, ILogger logger, StreamReader streamReader, string correlationId, string expectedHeaders)
{
    telemetrySpan.AddEvent("Verifying zip file header row starting");
    var firstLine = streamReader.ReadLine();
    logger.LogInformation("{CorrelationId} Verifying csv file has expected headers (everything falls apart if this doesn't line up)", correlationId);
    if (firstLine != expectedHeaders)
    {
        logger.LogError("{CorrelationId} First line of CSV was not the headers expected, no reasonable way to proceed from here. Got first line \"{FirstLine}\", expected \"{ExpectedHeaders}\"", correlationId, firstLine, expectedHeaders);
        throw new Exception($"{correlationId} Got unexpected CSV header line");
    }
    telemetrySpan.AddEvent("Verifying zip file header row done");
    return firstLine;
}

IProducer<string, CadastreRoadAddressImport> GetProducer(TelemetrySpan telemetrySpan)
{
    telemetrySpan.AddEvent("Setting up Kafka producer");

    // var producer = new Iproducer
    var protobufSerializerConfig = new ProtobufSerializerConfig
    {
        AutoRegisterSchemas = false,
        UseLatestVersion = true
    };
    var schemaRegistry = new CachedSchemaRegistryClient(KafkaConfigBinder.GetSchemaRegistryConfig());
    var producer = new ProducerBuilder<string, CadastreRoadAddressImport>(KafkaConfigBinder.GetProducerConfig())
        .SetValueSerializer(new ProtobufSerializer<CadastreRoadAddressImport>(schemaRegistry, protobufSerializerConfig).AsSyncOverAsync())
        .Build();
    telemetrySpan.AddEvent("Setting up Kafka producer done");
    return producer;
}

bool TryParseCsvLine(TelemetrySpan telemetrySpan, ILogger logger, string nextLine, string csvDelimiter, int expectedNumberOfFields,
    int lineNumber, string correlationId, [MaybeNullWhen(returnValue: false)] out CadastreRoadAddressImport cadastreRoadAddressImport)
{
    var lineSplit = nextLine.Split(csvDelimiter);
    telemetrySpan.AddEvent("CSV line split");
    if (lineSplit.Length != expectedNumberOfFields)
    {
        logger.LogWarning("{CorrelationId} Line number {LineNumber} had {LineSplitLength} columns, expected {ExpectedNumberOfFields}. Line was \"{NextLine}\".", correlationId, lineNumber, lineSplit.Length, expectedNumberOfFields, nextLine);
        cadastreRoadAddressImport = null;
        return false;
    }

    cadastreRoadAddressImport = new CadastreRoadAddressImport
    {
        LocalId = lineSplit[0],
        MunicipalityNumber = lineSplit[1],
        MunicipalityName = lineSplit[2],
        AddressType = lineSplit[3],
        AddressAdditionalName = lineSplit[4],
        AddressAdditionalNameSource = lineSplit[5],
        AddressCode = lineSplit[6],
        AddressName = lineSplit[7],
        Number = lineSplit[8],
        Letter = lineSplit[9],
        CadastralUnitNumber = lineSplit[10],
        PropertyUnitNumber = lineSplit[11],
        LeaseNumber = lineSplit[12],
        SubNumber = lineSplit[13],
        AddressText = lineSplit[14],
        AddressTextWithoutAddressAdditionalName = lineSplit[15],
        EpsgCode = lineSplit[16],
        North = lineSplit[17],
        East = lineSplit[18],
        PostalCode = lineSplit[19],
        PostalCity = lineSplit[20],
        UpdateDate = lineSplit[21],
        DataTakeOutDate = lineSplit[22],
        AddressId = lineSplit[23],
        UuidAddress = lineSplit[24],
        AccessId = lineSplit[25],
        UuidAccess = lineSplit[26],
        AccessNorth = lineSplit[27],
        AccessSouth = lineSplit[28],
        SummerAccessId = lineSplit[29],
        UuidSummerAccess = lineSplit[30],
        SummerAccessNorth = lineSplit[31],
        SummerAccessEast = lineSplit[32],
        WinterAccessId = lineSplit[33],
        UuidWinterAccess = lineSplit[34],
        WinterAccessNorth = lineSplit[35],
        WinterAccessEast = lineSplit[36]
    };
    telemetrySpan.AddEvent("CSV line parsed");
    return true;
}

async Task SendAddressToKafka(TelemetrySpan telemetrySpan, ILogger logger, string correlationId, int lineNumber,
    CadastreRoadAddressImport nextAddress, IProducer<string, CadastreRoadAddressImport> producer, Headers headers, string destinationTopic,
    CancellationToken cancellationToken)
{
    var nextMessage = new Message<string, CadastreRoadAddressImport>
    {
        Headers = headers,
        Key = nextAddress.AddressId,
        Value = nextAddress
    };
    // var producerResult = await producer.ProduceAsync(destinationTopic, nextMessage);
    // Perform the more laborious producer queue full check here, because raw byte could go so fast we get fastness issues
    var notSent = true;
    while (notSent)
    {
        if (cancellationToken.IsCancellationRequested) return;
        try
        {
            telemetrySpan.AddEvent("Producing CSV line");
            producer.Produce(destinationTopic, nextMessage);
            notSent = false;
        }
        catch (ProduceException<string, CadastreRoadAddressImport> ex)
        {
            if (!ex.Message.Contains("Queue full"))
            {
                throw;
            }

            logger.LogWarning("{CorrelationId} We are producing too fast, producer queue is full, sleeping and retrying line {LineNumber}", correlationId, lineNumber);
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }
    }
}

#endregion

#region Observability setup

// var serviceName = "MyServiceName";
// var serviceVersion = "1.0.0";
var correlationId = Guid.NewGuid().ToString();
string serviceName = Environment.GetEnvironmentVariable("SERVICE_NAME") ??
                     Assembly.GetEntryAssembly()?.GetName().Name ?? "Nhn.Addresses.Cadastre.Downloader.ServiceName";
string environment = Environment.GetEnvironmentVariable("DEPLOYMENT_ENVIRONMENT") ??
                     Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
                     Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "UNKNOWN_ENVIRONMENT";
string tracingName = "Nhn.Addresses.Cadastre.Downloader";
var otelResourceBuilder = ResourceBuilder.CreateDefault().AddService(serviceName)
    .AddAttributes([new("deployment.environment.name", environment)]);
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(otelResourceBuilder)
    .AddSource("*") // see final notes at https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/trace/customizing-the-sdk/README.md#activity-source , nhn.* maybe better?
    // .AddConsoleExporter() // Too chatty for logs when running on full dataset, but leaving it toggleable in here for easing verification work.
    .AddOtlpExporter()
    .Build();

var meterProvider = Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(otelResourceBuilder)
    .AddRuntimeInstrumentation()
    .AddMeter("*") // same as above , nhn.* maybe better?
    // .AddConsoleExporter() // This is horribly noisy in console
    .AddOtlpExporter()
    .Build();

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(logging =>
    {
        logging.SetResourceBuilder(otelResourceBuilder);
        logging.IncludeScopes = true;
        // logging.ParseStateValues = true; // Figure out if this is actually needed
        // logging.IncludeFormattedMessage = true;
        logging.AddConsoleExporter();
        logging.AddOtlpExporter();
    });
});

ILogger logger = loggerFactory.CreateLogger("AddressDownloader");

using var overallTracer = tracerProvider.GetTracer(tracingName).StartRootSpan("OverallProgressTrace");
overallTracer.SetAttribute("jobId", correlationId);

#endregion

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) =>
{
    logger.LogInformation("Canceling...");
    cts.Cancel();
    e.Cancel = true;
};
var cancellationToken = cts.Token;

#region Configuration parsing and validation

overallTracer.AddEvent("Setting up variables");

logger.LogInformation("{CorrelationId} Starting addressDownloader at time {DateTime:u}", correlationId, DateTime.UtcNow);
logger.LogInformation("{CorrelationId} Retrieving config", correlationId);

var csvUrl = Environment.GetEnvironmentVariable(ADDRESS_DOWNLOADER_CSV_URL);
// https://nedlasting.geonorge.no/geonorge/Basisdata/MatrikkelenVegadresse/CSV/Basisdata_0000_Norge_4258_MatrikkelenVegadresse_CSV.zip
var workDirectory = Environment.GetEnvironmentVariable(ADDRESS_DOWNLOADER_WORK_DIR_RW);
// For instance /tmp/address_downloader, just important to remember it has to be rw. If cloud is unpremissive of rw containers just mount tmpfs
var zipFileName = Environment.GetEnvironmentVariable(ADDRESS_DOWNLOADER_EXPECTED_ZIP_FILE_NAME);
// Basisdata_0000_Norge_4258_MatrikkelenVegadresse_CSV.zip
var csvFileName = Environment.GetEnvironmentVariable(ADDRESS_DOWNLOADER_EXPECTED_CSV_FILE_NAME);
// matrikkelenVegadresse.csv
// ReSharper disable once StringLiteralTypo
const string expectedHeaders = "lokalId;kommunenummer;kommunenavn;adressetype;adressetilleggsnavn;adressetilleggsnavnKilde;adressekode;adressenavn;nummer;bokstav;gardsnummer;bruksnummer;festenummer;undernummer;adresseTekst;adresseTekstUtenAdressetilleggsnavn;EPSG-kode;Nord;Øst;postnummer;poststed;oppdateringsdato;datauttaksdato;adresseId;uuidAdresse;atkomstId;uuidAtkomst;atkomstNord;atkomstØst;sommeratkomstId;uuidSommeratkomst;sommeratkomstNord;sommeratkomstØst;vinteratkomstId;uuidVinteratkomst;vinteratkomstNord;vinteratkomstØst";
var csvDelimiter = Environment.GetEnvironmentVariable(ADDRESS_DOWNLOADER_CSV_FIELD_DELIMITER);
var destinationTopic = Environment.GetEnvironmentVariable(ADDRESS_DOWNLOADER_KAFKA_TOPIC);

logger.LogInformation("{CorrelationId} Doing basic config validation", correlationId);
if(string.IsNullOrEmpty(csvUrl)) throw new Exception($"URL to download Kartverket Matrikkel Vegadresse from is not specified in env var {nameof(ADDRESS_DOWNLOADER_CSV_URL)} (expected something like https://nedlasting.geonorge.no/geonorge/Basisdata/MatrikkelenVegadresse/CSV/Basisdata_0000_Norge_4258_MatrikkelenVegadresse_CSV.zip)");
if(string.IsNullOrEmpty(workDirectory)) throw new Exception($"Working directory not specified in env var {nameof(ADDRESS_DOWNLOADER_WORK_DIR_RW)}");
if(string.IsNullOrEmpty(zipFileName)) throw new Exception($"Expected zip file name not specified in env var {nameof(ADDRESS_DOWNLOADER_EXPECTED_ZIP_FILE_NAME)}");
if(string.IsNullOrEmpty(csvFileName)) throw new Exception($"Expected csv file name not specified in env var {nameof(ADDRESS_DOWNLOADER_EXPECTED_CSV_FILE_NAME)}");
// if(string.IsNullOrEmpty(expectedHeaders)) throw new Exception($"Expected headers not specified in env var {nameof()}");
if(string.IsNullOrEmpty(csvDelimiter))
    csvDelimiter = ";";
if(string.IsNullOrEmpty(destinationTopic)) throw new Exception($"Destination topic not specified in env var {nameof(ADDRESS_DOWNLOADER_KAFKA_TOPIC)}");

#endregion

if (cancellationToken.IsCancellationRequested) return;

string downloadDestination = await DownloadZip(overallTracer, logger, csvUrl, workDirectory, zipFileName, correlationId);

if (cancellationToken.IsCancellationRequested) return;

var csvExtracted = CsvExtracted(overallTracer, logger, correlationId, downloadDestination, csvFileName, workDirectory);

if (cancellationToken.IsCancellationRequested) return;

using StreamReader sr = new StreamReader(csvExtracted);

var firstLine = GetCsvHeaderLine(overallTracer, logger, sr, correlationId, expectedHeaders);
var expectedNumberOfFields = firstLine.Split(csvDelimiter).Length;

if (cancellationToken.IsCancellationRequested) return;

var producer = GetProducer(overallTracer);

if (cancellationToken.IsCancellationRequested) return;

overallTracer.AddEvent("Starting processing and sending of downloaded csv lines");
Headers correlationHeaders =
[
    new Header("OriginUrl", System.Text.Encoding.UTF8.GetBytes($"{csvUrl}")),
    new Header("OriginAccessedTime", System.Text.Encoding.UTF8.GetBytes($"{DateTime.UtcNow:u}")),
    new Header("JobId", System.Text.Encoding.UTF8.GetBytes($"{correlationId}")),
];

logger.LogInformation("{CorrelationId} Starting processing of CSV file at {DateTime:u}, sending entries to Kafka topic {DestinationTopic} using kafka boostrap server {BootstrapServers}", correlationId, DateTime.UtcNow, destinationTopic, KafkaConfigBinder.GetProducerConfig().BootstrapServers);
var progressLogInterval = TimeSpan.FromSeconds(10);
var progressLogLastLogged = DateTime.Now;
var lineNumber = 1; // 1 because first line with headers already consumed by the stream reader when verifying headings above.
// ReSharper disable once RedundantAssignment // because it's clearer
string? nextLine = string.Empty;
while ((nextLine = sr.ReadLine()) != null)
{
    if (cancellationToken.IsCancellationRequested) return;
    using (var lineTracer = tracerProvider.GetTracer(tracingName).StartRootSpan("CsvLineTrace"))
    {
        lineNumber++;
        lineTracer.SetAttribute("jobId", correlationId);
        lineTracer.SetAttribute("lineNumber", lineNumber);
        if (progressLogLastLogged + progressLogInterval < DateTime.Now)
        {
            progressLogLastLogged = DateTime.Now;
            logger.LogInformation("{CorrelationId} Heartbeat at {DateTime:u} Processing CSV line {LineNumber}", correlationId, DateTime.Now, lineNumber);
        }

        if (!TryParseCsvLine(lineTracer, logger, nextLine, csvDelimiter, expectedNumberOfFields, lineNumber, correlationId, out var nextAddress)) continue;

        await SendAddressToKafka(lineTracer, logger, correlationId, lineNumber, nextAddress, producer, correlationHeaders, destinationTopic, cancellationToken);

        lineTracer.AddEvent("Done with CSV line");
    }
}

producer.Flush();
logger.LogInformation("{CorrelationId} Done at {DateTime:u} after processing {LineNumber} addresses from CSV.", correlationId, DateTime.UtcNow, lineNumber);
overallTracer.AddEvent("Done processing and sending of all downloaded csv lines");

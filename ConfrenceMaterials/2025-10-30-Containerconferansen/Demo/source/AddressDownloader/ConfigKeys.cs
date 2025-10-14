// ReSharper disable InconsistentNaming
// #pragma warning disable CS0414 // Field is assigned but its value is never used
namespace AddressDownloader;

public static class ConfigKeys
{
    public const string ADDRESS_DOWNLOADER_CSV_URL = nameof(ADDRESS_DOWNLOADER_CSV_URL);
    public const string ADDRESS_DOWNLOADER_WORK_DIR_RW = nameof(ADDRESS_DOWNLOADER_WORK_DIR_RW);
    public const string ADDRESS_DOWNLOADER_EXPECTED_ZIP_FILE_NAME = nameof(ADDRESS_DOWNLOADER_EXPECTED_ZIP_FILE_NAME);
    public const string ADDRESS_DOWNLOADER_EXPECTED_CSV_FILE_NAME = nameof(ADDRESS_DOWNLOADER_EXPECTED_CSV_FILE_NAME);
    // public const string ADDRESS_DOWNLOADER_EXPECTED_HEADERS = nameof(ADDRESS_DOWNLOADER_EXPECTED_HEADERS); // We have to code what each field maps to anyways, so no real way around this being hardcoded.
    public const string ADDRESS_DOWNLOADER_CSV_FIELD_DELIMITER = nameof(ADDRESS_DOWNLOADER_CSV_FIELD_DELIMITER);

    public const string ADDRESS_DOWNLOADER_KAFKA_TOPIC = nameof(ADDRESS_DOWNLOADER_KAFKA_TOPIC);

    // Kafka client (producer/consumer/admin) configs
    public const string KAFKA_BOOTSTRAP_SERVERS = nameof(KAFKA_BOOTSTRAP_SERVERS);

    public const string KAFKA_SECURITY_PROTOCOL = nameof(KAFKA_SECURITY_PROTOCOL);
    public const string KAFKA_SSL_CA_PEM_LOCATION = nameof(KAFKA_SSL_CA_PEM_LOCATION);
    public const string KAFKA_SSL_CERTIFICATE_LOCATION = nameof(KAFKA_SSL_CERTIFICATE_LOCATION);
    public const string KAFKA_SSL_KEY_LOCATION = nameof(KAFKA_SSL_KEY_LOCATION);
    public const string KAFKA_SSL_KEY_PASSWORD_LOCATION = nameof(KAFKA_SSL_KEY_PASSWORD_LOCATION);

    public const string KAFKA_ACKS = nameof(KAFKA_ACKS);

    public const string KAFKA_CLIENT_ID = nameof(KAFKA_CLIENT_ID);
    public const string KAFKA_GROUP_ID = nameof(KAFKA_GROUP_ID);
    public const string KAFKA_AUTO_OFFSET_RESET = nameof(KAFKA_AUTO_OFFSET_RESET);
    public const string KAFKA_ENABLE_AUTO_OFFSET_STORE = nameof(KAFKA_ENABLE_AUTO_OFFSET_STORE);
    public const string KAFKA_ENABLE_PARTITION_EOF = nameof(KAFKA_ENABLE_PARTITION_EOF);

    /// <summary>
    /// The url(s) you can reach a schema registry at. You can specify more than one schema registry url by separating them with commas.
    /// </summary>
    public const string KAFKA_SCHEMA_REGISTRY_URL = nameof(KAFKA_SCHEMA_REGISTRY_URL);

}

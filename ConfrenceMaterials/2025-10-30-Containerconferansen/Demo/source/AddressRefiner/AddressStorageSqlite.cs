using Confluent.Kafka;
using Microsoft.Data.Sqlite;
using No.Nhn.Address.Cadastre.Road;

namespace AddressRefiner;

public class AddressStorageSqlite : IAddressStorage
{
    private readonly ILogger<AddressStorageSqlite> _logger;
    private readonly SqliteConnection _sqliteDb;

    private List<TopicPartitionOffset> _highestOffsetsAtStartupTime;
    private bool _ready;

    public AddressStorageSqlite(ILogger<AddressStorageSqlite> logger)
    {
        _logger = logger;
        _sqliteDb = new SqliteConnection(GetSqliteConnectionString());
        // Safe to log because now always in mem or local disk without passwords in connection string
        _logger.LogTrace($"Connection to db using connection string \"{GetSqliteConnectionString()}\" set up");
        _sqliteDb.Open();
        InitializeDb();
        _highestOffsetsAtStartupTime = [];
        _ready = false;

        _logger.LogDebug($"{nameof(AddressStorageSqlite)} initialized");
    }

    public bool Remove(string key, string correlationId)
    {
        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            DELETE FROM CadastreRoadAddresses
            WHERE AddressId = $k;
        ";
        command.Parameters.AddWithValue("$k", key);
        var rowsAffected = command.ExecuteNonQuery();
        return rowsAffected == 1;
    }

    public bool Store(CadastreRoadAddress cadastreRoadAddress)
    {
        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            INSERT INTO CadastreRoadAddresses(
                AddressId,
                AddressUuid,
                AddressCode,
                AddressType,
                UpdateDate,
                MunicipalityNumber,
                MunicipalityName,
                CadastralUnitNumber,
                PropertyUnitNumber,
                LeaseNumber,
                SubNumber,
                AddressAdditionalName,
                AddressName,
                Number,
                Letter,
                AddressText,
                AddressTextWithoutAddressAdditionalName,
                PostalCode,
                PostalCity,
                EpsgCode,
                North,
                East,
                AccessId,
                AccessUuid,
                AccessNorth,
                AccessSouth,
                AccessSummerId,
                AccessSummerUuid,
                AccessSummerNorth,
                AccessSummerEast,
                AccessWinterId,
                AccessWinterUuid,
                AccessWinterNorth,
                AccessWinterEast
            )
            VALUES (
                $addressId,
                $addressUuid,
                $addressCode,
                $addressType,
                $updateDate,
                $municipalityNumber,
                $municipalityName,
                $cadastralUnitNumber,
                $propertyUnitNumber,
                $leaseNumber,
                $subNumber,
                $addressAdditionalName,
                $addressName,
                $number,
                $letter,
                $addressText,
                $addressTextWithoutAddressAdditionalName,
                $postalCode,
                $postalCity,
                $epsgCode,
                $north,
                $east,
                $accessId,
                $accessUuid,
                $accessNorth,
                $accessSouth,
                $accessSummerId,
                $accessSummerUuid,
                $accessSummerNorth,
                $accessSummerEast,
                $accessWinterId,
                $accessWinterUuid,
                $accessWinterNorth,
                $accessWinterEast
            )
            ON CONFLICT (AddressId)
                DO UPDATE SET
                    AddressId=excluded.AddressId,
                    AddressUuid=excluded.AddressUuid,
                    AddressCode=excluded.AddressCode,
                    AddressType=excluded.AddressType,
                    UpdateDate=excluded.UpdateDate,
                    MunicipalityNumber=excluded.MunicipalityNumber,
                    MunicipalityName=excluded.MunicipalityName,
                    CadastralUnitNumber=excluded.CadastralUnitNumber,
                    PropertyUnitNumber=excluded.PropertyUnitNumber,
                    LeaseNumber=excluded.LeaseNumber,
                    SubNumber=excluded.SubNumber,
                    AddressAdditionalName=excluded.AddressAdditionalName,
                    AddressName=excluded.AddressName,
                    Number=excluded.Number,
                    Letter=excluded.Letter,
                    AddressText=excluded.AddressText,
                    AddressTextWithoutAddressAdditionalName=excluded.AddressTextWithoutAddressAdditionalName,
                    PostalCode=excluded.PostalCode,
                    PostalCity=excluded.PostalCity,
                    EpsgCode=excluded.EpsgCode,
                    North=excluded.North,
                    East=excluded.East,
                    AccessId=excluded.AccessId,
                    AccessUuid=excluded.AccessUuid,
                    AccessNorth=excluded.AccessNorth,
                    AccessSouth=excluded.AccessSouth,
                    AccessSummerId=excluded.AccessSummerId,
                    AccessSummerUuid=excluded.AccessSummerUuid,
                    AccessSummerNorth=excluded.AccessSummerNorth,
                    AccessSummerEast=excluded.AccessSummerEast,
                    AccessWinterId=excluded.AccessWinterId,
                    AccessWinterUuid=excluded.AccessWinterUuid,
                    AccessWinterNorth=excluded.AccessWinterNorth,
                    AccessWinterEast=excluded.AccessWinterEast;
        ";
        command.Parameters.AddWithValue("$addressId",cadastreRoadAddress.AddressId);
        command.Parameters.AddWithValue("$addressUuid",cadastreRoadAddress.AddressUuid);
        command.Parameters.AddWithValue("$addressCode",cadastreRoadAddress.AddressCode);
        command.Parameters.AddWithValue("$addressType",cadastreRoadAddress.AddressType);
        command.Parameters.AddWithValue("$updateDate",cadastreRoadAddress.UpdateDate);
        command.Parameters.AddWithValue("$municipalityNumber",cadastreRoadAddress.MunicipalityNumber);
        command.Parameters.AddWithValue("$municipalityName",cadastreRoadAddress.MunicipalityName);
        command.Parameters.AddWithValue("$cadastralUnitNumber",cadastreRoadAddress.CadastralUnitNumber);
        command.Parameters.AddWithValue("$propertyUnitNumber",cadastreRoadAddress.PropertyUnitNumber);
        command.Parameters.AddWithValue("$leaseNumber",cadastreRoadAddress.LeaseNumber);
        command.Parameters.AddWithValue("$subNumber",cadastreRoadAddress.SubNumber);
        command.Parameters.AddWithValue("$addressAdditionalName",cadastreRoadAddress.AddressAdditionalName);
        command.Parameters.AddWithValue("$addressName",cadastreRoadAddress.AddressName);
        command.Parameters.AddWithValue("$number",cadastreRoadAddress.Number);
        command.Parameters.AddWithValue("$letter",cadastreRoadAddress.Letter);
        command.Parameters.AddWithValue("$addressText",cadastreRoadAddress.AddressText);
        command.Parameters.AddWithValue("$addressTextWithoutAddressAdditionalName",cadastreRoadAddress.AddressTextWithoutAddressAdditionalName);
        command.Parameters.AddWithValue("$postalCode",cadastreRoadAddress.PostalCode);
        command.Parameters.AddWithValue("$postalCity",cadastreRoadAddress.PostalCity);
        command.Parameters.AddWithValue("$epsgCode",cadastreRoadAddress.EpsgCode);
        command.Parameters.AddWithValue("$north",cadastreRoadAddress.North);
        command.Parameters.AddWithValue("$east",cadastreRoadAddress.East);
        command.Parameters.AddWithValue("$accessId",cadastreRoadAddress.AccessId);
        command.Parameters.AddWithValue("$accessUuid",cadastreRoadAddress.AccessUuid);
        command.Parameters.AddWithValue("$accessNorth",cadastreRoadAddress.AccessNorth);
        command.Parameters.AddWithValue("$accessSouth",cadastreRoadAddress.AccessSouth);
        command.Parameters.AddWithValue("$accessSummerId",cadastreRoadAddress.AccessSummerId);
        command.Parameters.AddWithValue("$accessSummerUuid",cadastreRoadAddress.AccessSummerUuid);
        command.Parameters.AddWithValue("$accessSummerNorth",cadastreRoadAddress.AccessSummerNorth);
        command.Parameters.AddWithValue("$accessSummerEast",cadastreRoadAddress.AccessSummerEast);
        command.Parameters.AddWithValue("$accessWinterId",cadastreRoadAddress.AccessWinterId);
        command.Parameters.AddWithValue("$accessWinterUuid",cadastreRoadAddress.AccessWinterUuid);
        command.Parameters.AddWithValue("$accessWinterNorth",cadastreRoadAddress.AccessWinterNorth);
        command.Parameters.AddWithValue("$accessWinterEast",cadastreRoadAddress.AccessWinterEast);
        var rowsAffected = command.ExecuteNonQuery();

        return rowsAffected == 1;
    }

    public bool TryRetrieve(string addressId, out CadastreRoadAddress result)
    {
        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            SELECT
                AddressId,
                AddressUuid,
                AddressCode,
                AddressType,
                UpdateDate,
                MunicipalityNumber,
                MunicipalityName,
                CadastralUnitNumber,
                PropertyUnitNumber,
                LeaseNumber,
                SubNumber,
                AddressAdditionalName,
                AddressName,
                Number,
                Letter,
                AddressText,
                AddressTextWithoutAddressAdditionalName,
                PostalCode,
                PostalCity,
                EpsgCode,
                North,
                East,
                AccessId,
                AccessUuid,
                AccessNorth,
                AccessSouth,
                AccessSummerId,
                AccessSummerUuid,
                AccessSummerNorth,
                AccessSummerEast,
                AccessWinterId,
                AccessWinterUuid,
                AccessWinterNorth,
                AccessWinterEast
            FROM CadastreRoadAddresses
            WHERE AddressId = $addressId
        ";
        command.Parameters.AddWithValue("$addressId", addressId);
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                result = new CadastreRoadAddress
                {
                    AddressId = reader["AddressId"] as string,
                    AddressUuid = reader["AddressUuid"] as string,
                    AddressCode = reader["AddressCode"] as string,
                    AddressType = reader["AddressType"] as string,
                    UpdateDate = reader["UpdateDate"] as string,
                    MunicipalityNumber = reader["MunicipalityNumber"] as string,
                    MunicipalityName = reader["MunicipalityName"] as string,
                    CadastralUnitNumber = reader["CadastralUnitNumber"] as string,
                    PropertyUnitNumber = reader["PropertyUnitNumber"] as string,
                    LeaseNumber = reader["LeaseNumber"] as string,
                    SubNumber = reader["SubNumber"] as string,
                    AddressAdditionalName = reader["AddressAdditionalName"] as string,
                    AddressName = reader["AddressName"] as string,
                    Number = reader["Number"] as string,
                    Letter = reader["Letter"] as string,
                    AddressText = reader["AddressText"] as string,
                    AddressTextWithoutAddressAdditionalName = reader["AddressTextWithoutAddressAdditionalName"] as string,
                    PostalCode = reader["PostalCode"] as string,
                    PostalCity = reader["PostalCity"] as string,
                    EpsgCode = reader["EpsgCode"] as string,
                    North = reader["North"] as string,
                    East = reader["East"] as string,
                    AccessId = reader["AccessId"] as string,
                    AccessUuid = reader["AccessUuid"] as string,
                    AccessNorth = reader["AccessNorth"] as string,
                    AccessSouth = reader["AccessSouth"] as string,
                    AccessSummerId = reader["AccessSummerId"] as string,
                    AccessSummerUuid = reader["AccessSummerUuid"] as string,
                    AccessSummerNorth = reader["AccessSummerNorth"] as string,
                    AccessSummerEast = reader["AccessSummerEast"] as string,
                    AccessWinterId = reader["AccessWinterId"] as string,
                    AccessWinterUuid = reader["AccessWinterUuid"] as string,
                    AccessWinterNorth = reader["AccessWinterNorth"] as string,
                    AccessWinterEast = reader["AccessWinterEast"] as string,
                };
                return true;
            }
        }
        result = new CadastreRoadAddress();
        return false;
    }

    public List<TopicPartitionOffset> GetLastConsumedTopicPartitionOffsets()
    {
        List<TopicPartitionOffset> topicPartitionOffsets = [];

        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            SELECT Topic, Partition, Offset
            FROM TopicPartitionOffsets
        ";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var topic = reader.GetString(0);
                var partition = reader.GetInt32(1);
                var offset = reader.GetInt64(2);

                topicPartitionOffsets.Add(new TopicPartitionOffset(topic, partition, offset));
            }
        }
        return topicPartitionOffsets;
    }

    public bool UpdateLastConsumedTopicPartitionOffsets(TopicPartitionOffset topicPartitionOffset)
    {
        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            INSERT INTO TopicPartitionOffsets(Topic, Partition, Offset)
            VALUES ($t, $p, $o)
            ON CONFLICT (Topic, Partition) DO UPDATE SET Offset=excluded.Offset;
        ";
        command.Parameters.AddWithValue("$t", topicPartitionOffset.Topic);
        command.Parameters.AddWithValue("$p", topicPartitionOffset.Partition.Value);
        command.Parameters.AddWithValue("$o", topicPartitionOffset.Offset.Value);
        var rowsAffected = command.ExecuteNonQuery();

        return rowsAffected == 1;
    }

    public bool SetStartupTimeHightestTopicPartitionOffsets(List<TopicPartitionOffset> topicPartitionOffsets)
    {
        _highestOffsetsAtStartupTime = topicPartitionOffsets;
        return true;
    }

    public List<TopicPartitionOffset> GetStartupTimeHightestTopicPartitionOffsets()
    {
        return _highestOffsetsAtStartupTime;
    }

    public bool Ready()
    {
        _logger.LogTrace($"{nameof(AddressStorageSqlite)} received request to check readiness");
        if(_ready) return true;

        if(_highestOffsetsAtStartupTime.Count == 0) return false;

        if(_highestOffsetsAtStartupTime.All(tpo => tpo.Offset.Value == 0)) return true;

        var latestConsumedOffsets = GetLastConsumedTopicPartitionOffsets();

        if(latestConsumedOffsets.Count == 0) return false; // This case should not happen when earliest is set to low watermark before first consume, but leave it in as a safeguard

        foreach(var latestOffset in latestConsumedOffsets)
        {
            var partitionHighWatermarkAtStartupTime = _highestOffsetsAtStartupTime.FirstOrDefault(tpo => tpo.Topic == latestOffset.Topic && tpo.Partition == latestOffset.Partition);
            if(latestOffset.Offset.Value < (partitionHighWatermarkAtStartupTime?.Offset.Value ?? long.MaxValue))
            {
                return false;
            }
        }

        _ready = true;
        return _ready;
    }

    private string GetSqliteConnectionString()
    {
        var locationType = Environment.GetEnvironmentVariable(ADDRESS_REFINER_STATE_STORAGE_SQLITE_LOCATION_TYPE);
        switch (locationType)
        {
            case "file":
                return GetSqliteConnectionStringFileBacked();
                break;
            case "memory":
            default:
                return GetSqliteConnectionStringInMem();
                break;
        }
    }

    private string GetSqliteConnectionStringFileBacked()
    {
        return new SqliteConnectionStringBuilder()
        {
            DataSource = new FileInfo("/ContainerData/StateDistributor.sqlite").FullName,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    private string GetSqliteConnectionStringInMem()
    {
        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = "KeyValueStateInSQLiteMemDb",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared
        };
        var connectionString = connectionStringBuilder.ToString();
        return connectionString;
    }

    private void InitializeDb()
    {
        var command = _sqliteDb.CreateCommand();
        command.CommandText =
        @"
            CREATE TABLE IF NOT EXISTS CadastreRoadAddresses (
                AddressId TEXT PRIMARY KEY,
                AddressUuid TEXT,
                AddressCode TEXT,
                AddressType TEXT,
                UpdateDate TEXT,
                MunicipalityNumber TEXT,
                MunicipalityName TEXT,
                CadastralUnitNumber TEXT,
                PropertyUnitNumber TEXT,
                LeaseNumber TEXT,
                SubNumber TEXT,
                AddressAdditionalName TEXT,
                AddressName TEXT,
                Number TEXT,
                Letter TEXT,
                AddressText TEXT,
                AddressTextWithoutAddressAdditionalName TEXT,
                PostalCode TEXT,
                PostalCity TEXT,
                EpsgCode TEXT,
                North TEXT,
                East TEXT,
                AccessId TEXT,
                AccessUuid TEXT,
                AccessNorth TEXT,
                AccessSouth TEXT,
                AccessSummerId TEXT,
                AccessSummerUuid TEXT,
                AccessSummerNorth TEXT,
                AccessSummerEast TEXT,
                AccessWinterId TEXT,
                AccessWinterUuid TEXT,
                AccessWinterNorth TEXT,
                AccessWinterEast TEXT
            );

            CREATE TABLE IF NOT EXISTS TopicPartitionOffsets (
                Topic TEXT NOT NULL,
                Partition INTEGER NOT NULL,
                Offset INTEGER NOT NULL,
                PRIMARY KEY(Topic, Partition)
            );
        ";
        command.ExecuteNonQuery();
    }
}

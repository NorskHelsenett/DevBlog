using System.Diagnostics;
using System.Text;
using AddressWebApi.Dtos;
using CadastreRoadAddress = No.Nhn.Address.Cadastre.Road.CadastreRoadAddress;

namespace AddressWebApi;
using Microsoft.Data.Sqlite;

using Confluent.Kafka;
// using Microsoft.Data.Sqlite;
public class AddressStorage : IAddressStorage
{
    private readonly ILogger<AddressStorage> _logger;
    private readonly ActivitySource _activitySource;
    private readonly SqliteConnection _sqliteDb;

    private readonly uint _queryResultNumberLimit;
    private readonly TimeSpan _queryTimeout;

    private List<TopicPartitionOffset> _highestOffsetsAtStartupTime;
    private bool _ready;

    public AddressStorage(ILogger<AddressStorage> logger, ActivitySource activitySource)
    {
        _logger = logger;
        _activitySource = activitySource;
        _sqliteDb = new SqliteConnection(GetSqliteConnectionString());
        // Safe to log because now always in mem or local disk without passwords in connection string
        _logger.LogTrace($"Connection to db using connection string \"{GetSqliteConnectionString()}\" set up");
        _sqliteDb.Open();
        InitializeDb();
        _highestOffsetsAtStartupTime = [];
        _ready = false;

        _queryResultNumberLimit = 0;
        var queryResultLimitEnvVarValue = Environment.GetEnvironmentVariable(ADDRESS_WEB_API_QUERY_NUMBER_OF_RESULTS_LIMIT);
        if (!string.IsNullOrEmpty(queryResultLimitEnvVarValue))
        {
            if (uint.TryParse(queryResultLimitEnvVarValue, out var tempLimit))
            {
                _queryResultNumberLimit = tempLimit;
            }
        }
        _queryTimeout = TimeSpan.Zero;
        var queryTimeoutMsEnvVarValue = Environment.GetEnvironmentVariable(ADDRESS_WEB_API_QUERY_TIMEOUT_MS);
        if (!string.IsNullOrEmpty(queryTimeoutMsEnvVarValue))
        {
            if (uint.TryParse(queryTimeoutMsEnvVarValue, out var tempTimeoutMs))
            {
                _queryTimeout = TimeSpan.FromMilliseconds(tempTimeoutMs);
            }
        }


        _logger.LogDebug($"{nameof(AddressStorage)} initialized");
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

    public ResultStatus TryQuery(Query query, string correlationId, CancellationToken cancellationToken, out IEnumerable<AddressWebApi.Dtos.CadastreRoadAddress> result)
    {
        using var activity = _activitySource.StartActivity();
        activity?.SetTag("CorrelationId", correlationId);
        activity?.AddEvent(new ActivityEvent("Parsing query parameters"));
        var resultStatusType = ResultStatusTypes.Success;
        var resultStatusProps = new Dictionary<string, string>();
        var queryStartTime = DateTime.UtcNow;
        var queryCutoffTime = queryStartTime + _queryTimeout;
        var command = _sqliteDb.CreateCommand();
        var selectedFieldNames = RequestedFields(query);
        var requestedFilters = RequestedFilters(query);
        activity?.AddEvent(new ActivityEvent("Building query command text from parsed parameters"));
        var sb = new StringBuilder();
        sb.Append("SELECT").Append("\n");
        sb.Append("\t").Append(selectedFieldNames).Append("\n");
        sb.Append("FROM CadastreRoadAddresses").Append("\n");
        sb.Append("WHERE").Append("\n");
        sb.Append("\t").Append(requestedFilters).Append("\n");
        if (_queryResultNumberLimit > 0)
        {
            sb.Append("LIMIT ").Append(_queryResultNumberLimit); // Not really needed until maybe someday someone makes a series of unfortunate choice? And even then, it doesn't help all that much
        }

        command.CommandText = sb.ToString();

        activity?.AddEvent(new ActivityEvent("Adding parametrised values to query"));
        foreach (var f in query.Filters)
        {
            var fieldNameVariableName = GetCommandParameterName(f.Field.FieldName());
            var formattedValue = FormattedQueriedValue(f);
            command.Parameters.AddWithValue(fieldNameVariableName, formattedValue);
        }

        var foundAddresses = new List<AddressWebApi.Dtos.CadastreRoadAddress>();

        activity?.AddEvent(new ActivityEvent("Executing query"));
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    resultStatusType = ResultStatusTypes.Error;
                    resultStatusProps["message"] = "Query was canceled.";
                    resultStatusProps["reason"] = "canceled";
                    activity?.AddEvent(new ActivityEvent("Aborting query due to cancellation"));
                    _logger.LogWarning("Query {cancelledQuery} cancelled after {duration} ms, having already successfully read {readRows} rows", command.CommandText,
                        (DateTime.UtcNow - queryStartTime).TotalMilliseconds,foundAddresses.Count);
                    reader.Close();
                    break;
                }
                if (_queryTimeout != TimeSpan.Zero && queryCutoffTime < DateTime.UtcNow)
                {
                    resultStatusType = ResultStatusTypes.Warning;
                    resultStatusProps["message"] = "Query timed out.";
                    resultStatusProps["reason"] = "timeout";
                    activity?.AddEvent(new ActivityEvent("Aborting query due to timeout"));
                    _logger.LogWarning("Query {timeoutedQuery} timed out after {timeout} ms, having already successfully read {readRows} rows", command.CommandText, _queryTimeout.TotalMilliseconds, foundAddresses.Count);
                    reader.Close();
                    break;
                }
                var next = new AddressWebApi.Dtos.CadastreRoadAddress();
                if(query.RequestedFields.Contains(RequestableField.AddressId))
                    next.AddressId = reader["AddressId"] as string;
                if(query.RequestedFields.Contains(RequestableField.AddressUuid))
                    next.AddressUuid = reader["AddressUuid"] as string;
                if(query.RequestedFields.Contains(RequestableField.AddressCode))
                    next.AddressCode = reader["AddressCode"] as string;
                if(query.RequestedFields.Contains(RequestableField.AddressType))
                    next.AddressType = reader["AddressType"] as string;
                if(query.RequestedFields.Contains(RequestableField.UpdateDate))
                    next.UpdateDate = reader["UpdateDate"] as string;
                if(query.RequestedFields.Contains(RequestableField.MunicipalityNumber))
                    next.MunicipalityNumber = reader["MunicipalityNumber"] as string;
                if(query.RequestedFields.Contains(RequestableField.MunicipalityName))
                    next.MunicipalityName = reader["MunicipalityName"] as string;
                if(query.RequestedFields.Contains(RequestableField.CadastralUnitNumber))
                    next.CadastralUnitNumber = reader["CadastralUnitNumber"] as string;
                if(query.RequestedFields.Contains(RequestableField.PropertyUnitNumber))
                    next.PropertyUnitNumber = reader["PropertyUnitNumber"] as string;
                if(query.RequestedFields.Contains(RequestableField.LeaseNumber))
                    next.LeaseNumber = reader["LeaseNumber"] as string;
                if(query.RequestedFields.Contains(RequestableField.SubNumber))
                    next.SubNumber = reader["SubNumber"] as string;
                if(query.RequestedFields.Contains(RequestableField.AddressAdditionalName))
                    next.AddressAdditionalName = reader["AddressAdditionalName"] as string;
                if(query.RequestedFields.Contains(RequestableField.AddressName))
                    next.AddressName = reader["AddressName"] as string;
                if(query.RequestedFields.Contains(RequestableField.Number))
                    next.Number = reader["Number"] as string;
                if(query.RequestedFields.Contains(RequestableField.Letter))
                    next.Letter = reader["Letter"] as string;
                if(query.RequestedFields.Contains(RequestableField.AddressText))
                    next.AddressText = reader["AddressText"] as string;
                if(query.RequestedFields.Contains(RequestableField.AddressTextWithoutAddressAdditionalName))
                    next.AddressTextWithoutAddressAdditionalName = reader["AddressTextWithoutAddressAdditionalName"] as string;
                if(query.RequestedFields.Contains(RequestableField.PostalCode))
                    next.PostalCode = reader["PostalCode"] as string;
                if(query.RequestedFields.Contains(RequestableField.PostalCity))
                    next.PostalCity = reader["PostalCity"] as string;
                if(query.RequestedFields.Contains(RequestableField.EpsgCode))
                    next.EpsgCode = reader["EpsgCode"] as string;
                if(query.RequestedFields.Contains(RequestableField.North))
                    next.North = reader["North"] as string;
                if(query.RequestedFields.Contains(RequestableField.East))
                    next.East = reader["East"] as string;
                if(query.RequestedFields.Contains(RequestableField.AccessId))
                    next.AccessId = reader["AccessId"] as string;
                if(query.RequestedFields.Contains(RequestableField.AccessUuid))
                    next.AccessUuid = reader["AccessUuid"] as string;
                if(query.RequestedFields.Contains(RequestableField.AccessNorth))
                    next.AccessNorth = reader["AccessNorth"] as string;
                if(query.RequestedFields.Contains(RequestableField.AccessSouth))
                    next.AccessSouth = reader["AccessSouth"] as string;
                if(query.RequestedFields.Contains(RequestableField.AccessSummerId))
                    next.AccessSummerId = reader["AccessSummerId"] as string;
                if(query.RequestedFields.Contains(RequestableField.AccessSummerUuid))
                    next.AccessSummerUuid = reader["AccessSummerUuid"] as string;
                if(query.RequestedFields.Contains(RequestableField.AccessSummerNorth))
                    next.AccessSummerNorth = reader["AccessSummerNorth"] as string;
                if(query.RequestedFields.Contains(RequestableField.AccessSummerEast))
                    next.AccessSummerEast = reader["AccessSummerEast"] as string;
                if(query.RequestedFields.Contains(RequestableField.AccessWinterId))
                    next.AccessWinterId = reader["AccessWinterId"] as string;
                if(query.RequestedFields.Contains(RequestableField.AccessWinterUuid))
                    next.AccessWinterUuid = reader["AccessWinterUuid"] as string;
                if(query.RequestedFields.Contains(RequestableField.AccessWinterNorth))
                    next.AccessWinterNorth = reader["AccessWinterNorth"] as string;
                if(query.RequestedFields.Contains(RequestableField.AccessWinterEast))
                    next.AccessWinterEast = reader["AccessWinterEast"] as string;
                foundAddresses.Add(next);
            }
        }
        activity?.AddEvent(
            new ActivityEvent(
                name: "Returning query results",
                tags: new ActivityTagsCollection(new List<KeyValuePair<string, object?>> { new("NumberOfFoundAddresses", foundAddresses.Count)})
            )
        );
        resultStatusProps["resultCount"] = foundAddresses.Count.ToString();
        result = foundAddresses;
        return new ResultStatus { Type = resultStatusType, AdditionalInfo = resultStatusProps};
    }

    private string FormattedQueriedValue(FilterClause filterClause)
    {
        if (filterClause.Criteria is FilterCriteria.Contains or FilterCriteria.DoesntContain)
        {
            return $"%{filterClause.Value}%";
        }
        return filterClause.Value ?? string.Empty;
    }

    /// <summary>
    /// Note that this way of adding pre approved column names alleviates some concerns about trust, both in what the client is capable of submitting, but also how SQL could fail if given bad/weird input.
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    private string RequestedFields(Query query)
    {
        HashSet<string> fields = []; // HashSet effectively weeds out duplicates
        foreach (var rf in query.RequestedFields)
        {
            fields.Add(rf.FieldName());
        }
        return string.Join(", ", fields);
    }

    private string RequestedFilters(Query query)
    {
        HashSet<string> fields = []; // HashSet effectively weeds out duplicates
        foreach (var f in query.Filters)
        {
            var fieldName = f.Field.FieldName();
            var fieldNameVariableName = GetCommandParameterName(fieldName);
            switch (f.Criteria)
            {
                case FilterCriteria.Equals:
                    fields.Add($"{fieldName} = {fieldNameVariableName}");
                    break;
                case FilterCriteria.Contains:
                    fields.Add($"{fieldName} LIKE {fieldNameVariableName}");
                    break;
                case FilterCriteria.DoesntEqual:
                    fields.Add($"{fieldName} != {fieldNameVariableName}");
                    break;
                case FilterCriteria.DoesntContain:
                    fields.Add($"{fieldName} NOT LIKE {fieldNameVariableName}");
                    break;
                case FilterCriteria.IsNull:
                    fields.Add($"{fieldName} ISNULL");
                    break;
                case FilterCriteria.IsEmpty:
                    fields.Add($"{fieldName} = ''");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        return string.Join(" AND ", fields);
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
        _logger.LogTrace($"{nameof(AddressStorage)} received request to check readiness");
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

    private string GetCommandParameterName(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName)) throw new ArgumentException(message: "Must have value", paramName: nameof(fieldName));
        return "$" + char.ToLower(fieldName[0]) + fieldName[1..];

    }

    private string GetSqliteConnectionString()
    {
        return GetSqliteConnectionStringInMem();
        // return GetSqliteConnectionStringFileBacked();
    }

    // private string GetSqliteConnectionStringFileBacked()
    // {
    //     return new SqliteConnectionStringBuilder()
    //     {
    //         DataSource = new FileInfo("/ContainerData/StateDistributor.sqlite").FullName,
    //         Mode = SqliteOpenMode.ReadWriteCreate
    //     }.ToString();
    // }

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

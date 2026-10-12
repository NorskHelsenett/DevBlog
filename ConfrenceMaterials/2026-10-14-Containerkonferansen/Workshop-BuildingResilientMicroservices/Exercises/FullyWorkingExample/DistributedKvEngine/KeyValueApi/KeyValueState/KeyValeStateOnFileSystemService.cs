public class KeyValeStateOnFileSystemService : IKeyValueStateService
{
    private readonly ILogger<KeyValeStateOnFileSystemService> _logger;
    private readonly string _storageRootDirectoryPath;

    private List<KafkaTopicPartitionOffset> _highestOffsetsAtStartupTime;
    private bool _ready;

    public KeyValeStateOnFileSystemService(ILogger<KeyValeStateOnFileSystemService> logger)
    {
        _logger = logger;
        var storageRoot = Environment.GetEnvironmentVariable(KV_API_STATE_STORAGE_DISK_LOCATION);
        if(string.IsNullOrWhiteSpace(storageRoot))
        {
            if(string.IsNullOrEmpty(storageRoot))
            {
                _logger.LogWarning($"Failed to read content of environment variable \"{KV_API_STATE_STORAGE_DISK_LOCATION}\", got null/empty string");
            }
            else
            {
                _logger.LogWarning($"Failed to read proper content of environment variable \"{KV_API_STATE_STORAGE_DISK_LOCATION}\", contained only whitespaces");
            }
            storageRoot = "."; // Remove possibility of null, using . further reduces chances of doing weird stuff at root of file system
        }
        _storageRootDirectoryPath = storageRoot;
        _highestOffsetsAtStartupTime = [];
        _logger.LogDebug($"{nameof(KeyValeStateOnFileSystemService)} initialized");
    }

    public bool Store(byte[] key, byte[] value, string correlationId)
    {
        var directory = GetDirectoryForKey(key);
        // For future me: Directory.CreateDirectory() handles the case where it already exists.
        // So same as with mkdir -p, call it just in case instead of bloating path with if()'s.
        Directory.CreateDirectory(directory); // Create if not exists
        string[] preExistingFiles = Directory.GetFiles(directory);
        var keyPath = string.Empty;
        var valuePath = string.Empty;
        var correlationIdPath = string.Empty;
        if(preExistingFiles.Length == 0)
        {
            keyPath = $"{directory}/0.key";
            valuePath = $"{directory}/0.value";
            correlationIdPath = $"{directory}/0.correlationId";
            File.WriteAllBytes(keyPath, key);
            File.WriteAllBytes(valuePath, value);
            File.WriteAllText(correlationIdPath, correlationId);
            return true;
        }
        var keyFiles = preExistingFiles.Where(fileName => fileName.EndsWith(".key")).ToArray();
        foreach(var keyFile in keyFiles)
        {
            if(File.ReadAllBytes(keyFile).SequenceEqual(key))
            {
                var associatedValueFile = keyFile[0..^3] + "value";
                var associatedCorrelationIdFile = keyFile[0..^3] + "correlationId";
                File.WriteAllBytes(associatedValueFile, value);
                File.WriteAllText(associatedCorrelationIdFile, correlationId);
                return true;
            }
        }

        // Detect holes, and insert in hole or if none at end
        var keyFileNames = keyFiles
            .Select(fullPath => Path.GetFileNameWithoutExtension(fullPath))
            .ToArray();
        var fileNumbers = new List<int>();
        foreach(var keyFileName in keyFileNames)
        {
            if(int.TryParse(keyFileName, out var baseName) && 0 <= baseName)
            {
                fileNumbers.Add(baseName);
            }
        }
        fileNumbers.Sort();

        var nextAvailableBaseName = fileNumbers.Count;

        if(0 < fileNumbers.Count && fileNumbers[^1] != fileNumbers.Count - 1)
        {
            // Here there be gaps. Unless there are duplicates. But that is hard to imagine (I don't know any file system allowing for duplicate names). Or someone snuck in a weird value that is parsable as an int. At which point, shrek it, the retrieval will end up finding this anyways.
            for(int i = 0; i < fileNumbers.Count; i++)
            {
                if(fileNumbers[i] != i)
                {
                    nextAvailableBaseName = i;
                    break;
                }
            }
        }
        keyPath = $"{directory}/{nextAvailableBaseName}.key";
        valuePath = $"{directory}/{nextAvailableBaseName}.value";
        correlationIdPath = $"{directory}/{nextAvailableBaseName}.correlationId";
        File.WriteAllBytes(keyPath, key);
        File.WriteAllBytes(valuePath, value);
        File.WriteAllText(correlationIdPath, correlationId);
        return true;
    }

    public bool TryRetrieve(byte[] key, out (byte[] Value, string CorrelationId) result)
    {
        // var keyEncrypted = _encrypt(keyRaw);
        var directory = GetDirectoryForKey(key);
        if(!Directory.Exists(directory))
        {
            result = (Value: [], CorrelationId: string.Empty);
            return false;
        }
        string[] preExistingFiles = Directory.GetFiles(directory);
        var keyFiles = preExistingFiles.Where(fileName => fileName.EndsWith(".key")).ToArray();
        foreach(var keyFile in keyFiles)
        {
            if(File.ReadAllBytes(keyFile).SequenceEqual(key))
            {
                var associatedValueFile = keyFile[0..^3] + "value";
                if(File.Exists(associatedValueFile))
                {
                    var value = File.ReadAllBytes(associatedValueFile);
                    var correlationId = string.Empty;
                    var associatedCorrelationIdFile = keyFile[0..^3] + "correlationId";
                    if(File.Exists(associatedValueFile))
                    {
                        correlationId = File.ReadAllText(associatedCorrelationIdFile);
                    }
                    result = (Value: value, CorrelationId: correlationId);
                    return true;
                }
                else
                {
                    _logger.LogWarning($"On retrieve found matching key at path {keyFile}, but no corresponding file/path for the value as expected at {associatedValueFile}. Checking other keys jut to be sure, but something truly weird is going on.");
                    // But it matters not, if we for some inexplicable reason have a duplicate key, we will just find and use the first proper match.
                    // If we on the other hand don't have an actual match at all, it will fall through to the "no result" below.
                }
            }
        }
        result = (Value: [], CorrelationId: string.Empty);
        return false;
    }

    public bool Remove(byte[] key, string correlationId)
    {
        // var keyEncrypted = _encrypt(keyRaw);
        var directory = GetDirectoryForKey(key);
        if(!Directory.Exists(directory))
        {
            _logger.LogWarning($"Someone tried to delete {directory} which doesn't exist, weird");
            return true;
        }
        string[] preExistingFiles = Directory.GetFiles(directory);
        var keyFiles = preExistingFiles.Where(fileName => fileName.EndsWith(".key")).ToArray();
        foreach(var keyFile in keyFiles)
        {
            if(File.ReadAllBytes(keyFile).SequenceEqual(key))
            {
                var associatedValueFile = keyFile[0..^3] + "value";
                if(File.Exists(associatedValueFile))
                {
                    File.Delete(associatedValueFile);
                }
                else
                {
                    _logger.LogWarning($"On delete request found matching key at path {keyFile}, but no corresponding file/path for the value as expected at {associatedValueFile}.");
                }
                File.Delete(keyFile);

                try
                {
                    if(Directory.GetFiles(directory).Length == 0)
                    {
                        var parentDirectory = Directory.GetParent(directory)!.FullName;
                        Directory.Delete(directory, false);
                        if(Directory.GetDirectories(parentDirectory).Length == 0)
                        {
                            Directory.Delete(parentDirectory, false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Got exception when trying to clean up directories that at the time were thought to be empty. Perhaps something new has been inserted? The directory in question/it's parent is/was \"{directory}\"");
                }
                return true;
            }
        }
        return true;
    }

    public List<KafkaTopicPartitionOffset> GetLastConsumedTopicPartitionOffsets()
    {
        // The triple nested foreach (+ all the linq) has huge potential for performance gains by being minimally clever.
        // However, seeing as it's most likely there will only ever be 1 topic + a single digit number of partitions, it will normally not be a noticeable penalty.
        // Also, should these expectations not be met the outcome is crippling slowness, instead of weird reads of random data and odd behaviour.
        // Should you be lucky enough that your solution is used enough for high performance to matter, well, then you will need to invest time in optimizing.
        // At that point, consider the SQLite or in mem dict solution?
        List<KafkaTopicPartitionOffset> result = [];
        var tpoTopicGroupDirs = $"{_storageRootDirectoryPath}/topicPartitionOffsets/";
        Directory.CreateDirectory(tpoTopicGroupDirs); // Create if not exists
        string[] topicsGroup = Directory.GetDirectories(tpoTopicGroupDirs);
        foreach(var topicDirsInGroup in topicsGroup)
        {
            string[] topicsDirs = Directory.GetDirectories(topicDirsInGroup);
            foreach(var topicDir in topicsDirs)
            {
                string[] topicDirFiles = Directory.GetFiles(topicDir);
                var topicNameFile = topicDirFiles.FirstOrDefault(f => f.EndsWith("topicName.txt"));
                var topicName = !string.IsNullOrEmpty(topicNameFile) ? File.ReadAllText(topicNameFile) : string.Empty;
                var partitionFiles = topicDirFiles.Where(f => f.EndsWith(".partition"));
                foreach(var partitionFile in partitionFiles)
                {
                    var partitionFileName = new FileInfo(partitionFile).Name;
                    result.Add(new KafkaTopicPartitionOffset {
                        Topic     = new KafkaTopic { Value = topicName },
                        Partition = new KafkaPartition { Value = int.Parse(partitionFileName[0..^10])},
                        Offset    = new KafkaOffset { Value = long.Parse(File.ReadAllText(partitionFile))},
                    });
                }
            }
        }
        return result;
    }

    public bool UpdateLastConsumedTopicPartitionOffsets(KafkaTopicPartitionOffset topicPartitionOffset)
    {
        var topicDirectory = $"{_storageRootDirectoryPath}/topicPartitionOffsets/{topicPartitionOffset.Topic.Value.GetHashString()}"; // Guard agains weird topic names not playing along with file system
        // var topicPartitionOffsetsDirectory = $"{_storageRootDirectoryPath}/topicPartitionOffsets";
        // For future me: Directory.CreateDirectory() handles the case where it already exists.
        // So same as with mkdir -p, call it just in case instead of bloating path with if()'s.
        Directory.CreateDirectory(topicDirectory); // Create if not exists
        string[] preExistingTopics = Directory.GetDirectories(topicDirectory);
        if(preExistingTopics.Length == 0)
        {
            Directory.CreateDirectory($"{topicDirectory}/0");
            File.WriteAllText($"{topicDirectory}/0/topicName.txt", topicPartitionOffset.Topic.Value);
            File.WriteAllText($"{topicDirectory}/0/{topicPartitionOffset.Partition.Value}.partition", $"{topicPartitionOffset.Offset.Value}");
            return true;
        }
        foreach(var candidateTopicDirectory in preExistingTopics)
        {
            string[] preExistingFiles = Directory.GetFiles(candidateTopicDirectory);
            var topicNameFile = preExistingFiles.FirstOrDefault(f => f.EndsWith("topicName.txt"));
            if(!string.IsNullOrEmpty(topicNameFile))
            {
                if(File.ReadAllText(topicNameFile) == topicPartitionOffset.Topic.Value)
                {
                    File.WriteAllText($"{candidateTopicDirectory}/{topicPartitionOffset.Partition.Value}.partition", $"{topicPartitionOffset.Offset.Value}");
                    return true;
                }
            }
        }

        var nextDirNumber = preExistingTopics.Select(dirName => int.Parse(dirName.Remove(0,topicDirectory.Length))).Max() + 1;
        Directory.CreateDirectory($"{topicDirectory}/{nextDirNumber}");
        File.WriteAllText($"{topicDirectory}/{nextDirNumber}/topicName.txt", topicPartitionOffset.Topic.Value);
        File.WriteAllText($"{topicDirectory}/{nextDirNumber}/{topicPartitionOffset.Partition.Value}.partition", $"{topicPartitionOffset.Offset.Value}");
        return true;
    }

    public bool SetStartupTimeHightestTopicPartitionOffsets(List<KafkaTopicPartitionOffset> topicPartitionOffsets)
    {
        _highestOffsetsAtStartupTime = topicPartitionOffsets;
        return true;
    }

    public List<KafkaTopicPartitionOffset> GetStartupTimeHightestTopicPartitionOffsets()
    {
        return _highestOffsetsAtStartupTime;
    }

    public bool Ready()
    {
        _logger.LogTrace($"{nameof(KeyValueStateInSQLiteService)} received request to check readiness");
        if(_ready) return true;

        if(_highestOffsetsAtStartupTime.Count == 0) return false;

        if(_highestOffsetsAtStartupTime.All(tpo => tpo.Offset.Value == 0)) return true;

        var latestConsumedOffsets = GetLastConsumedTopicPartitionOffsets();

        if(latestConsumedOffsets.Count == 0) return false; // This case should not happen any more as earliest is set to low watermark before first consume, but leave it in as a safeguard

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

    private string GetDirectoryForKey(byte[] key)
    {
        // Make directory for first 3 (16 ^ 3 = up to 4096 directories per level), then next 3, then dump files
        // var keyHash = System.IO.Hashing.Crc32.Hash(key);
        // var keyHashHex = Convert.ToHexString(keyHash).ToLowerInvariant();
        var hash = key.GetHashString();
        var firstLevel = hash.Substring(0, 3);
        var secondLevel = hash.Substring(3, 3);
        return $"{_storageRootDirectoryPath}/{firstLevel}/{secondLevel}";
    }
}

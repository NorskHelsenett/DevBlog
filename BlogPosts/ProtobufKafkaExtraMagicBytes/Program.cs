using Google.Protobuf;
using Google.Protobuf.Reflection;

byte[] AppendBytes(byte[] first, byte[] second)
{
    byte[] ret = new byte[first.Length + second.Length];
    var byteIndex = first.Length * sizeof(byte);
    Buffer.BlockCopy(first, 0, ret, 0, byteIndex);
    Buffer.BlockCopy(second, 0, ret, byteIndex, second.Length * sizeof(byte));
    return ret;
}

IEnumerable<byte> VarIntEncode(int value)
{
    do
    {
        byte lower7Bits = (byte)(value & 0x7f);
        value >>= 7;
        if (value > 0)
            lower7Bits |= 128;
        yield return lower7Bits;
    } while (value > 0);
}

int VarIntDecode(Stream stream)
{
    bool more = true;
    int value = 0;
    int shift = 0;
    while(more)
    {
        int lower7Bits = stream.ReadByte();
        more = (lower7Bits & 128) != 0;
        value |= (lower7Bits & 0x7f) << shift;
        shift += 7;
    }

    return value;
}

IEnumerable<byte> ZigzagEncode(int n)
{
    var zzEncoded = (n << 1) ^ (n >> 31);
    var varIntEncoded = VarIntEncode(zzEncoded);
    return varIntEncoded;
}

int ZigzagDecode(Stream encoded)
{
    var n = VarIntDecode(encoded);
    var decoded = (n >> 1) ^ -(n & 1);
    return decoded;
}

List<int> GetProtoIndexes(IMessage kafkaProtobufPayload)
{
    MessageDescriptor currentDescription, previousDescription = currentDescription = kafkaProtobufPayload.Descriptor;
    List<int> indexes = [];
    while(currentDescription.ContainingType != null)
    {
        (previousDescription, currentDescription) = (currentDescription, currentDescription.ContainingType);
        var previousIndex = currentDescription.NestedTypes.IndexOf(previousDescription);
        indexes.Add(previousIndex);
    }
    var rootDescriptionIndex = currentDescription.File.MessageTypes.IndexOf(currentDescription);
    indexes.Add(rootDescriptionIndex);
    indexes.Reverse();
    return indexes;
}

byte[] GetProtoMagicBytesForSerialization(IMessage kafkaProtobufPayload)
{
    var protoIndexes = GetProtoIndexes(kafkaProtobufPayload);
    if (protoIndexes.Count == 1 && protoIndexes[0] == 0)
        return [0]; // Special case when only 1 and is first
    protoIndexes.Insert(0, protoIndexes.Count);
    List<byte> protoMagicBytes = [];
    foreach (var protoIndex in protoIndexes)
    {
        var zz = ZigzagEncode(protoIndex);
        protoMagicBytes.AddRange(zz);
    }
    return protoMagicBytes.ToArray();
}

(List<int> ProtoIndexes, byte[] PureProtoPayload) GetProtoMagicBytesAndPayloadForDeserialization(byte[] kafkaSerializedProtobufPayload)
{
    var consumableStream = new MemoryStream(kafkaSerializedProtobufPayload);
    var numberOfProtoIndexes = ZigzagDecode(consumableStream);
    if (numberOfProtoIndexes == 0)
    {
        return (ProtoIndexes: [0], PureProtoPayload: kafkaSerializedProtobufPayload[1..]);
    }

    var indexes = new List<int>();
    for (int i = 0; i < numberOfProtoIndexes; i++)
    {
        indexes.Add(ZigzagDecode(consumableStream));
    }
    var pureProtoPayload = new List<byte>();
    var nextOffset = (int)consumableStream.Position; // Safe cast from int64 to int32, because the original byte[] cannot have indexes that aren't int32.
    pureProtoPayload.AddRange(kafkaSerializedProtobufPayload[nextOffset..]);
    return (ProtoIndexes: indexes, PureProtoPayload: pureProtoPayload.ToArray());
}

var single = new ms { FirstField = "Hello single proto message!" };
byte[] singleSerialized = single.ToByteArray();
byte[] singleProtoMagicBytes = GetProtoMagicBytesForSerialization(single);
byte[] singlePayload =  AppendBytes(singleProtoMagicBytes, singleSerialized);
var singleDeserializationBytes = GetProtoMagicBytesAndPayloadForDeserialization(singlePayload);
var singleDeserialized = ms.Parser.ParseFrom(singleDeserializationBytes.PureProtoPayload);
var singleProtoMagicBytesUnpackedZigzagEncoded = singleDeserializationBytes.ProtoIndexes.Select(ZigzagEncode).SelectMany(x=> x).ToArray();
Console.WriteLine($"Single serdes:" +
                  $"\n\t Single serialized bytes: {BitConverter.ToString(singleSerialized)}" +
                  $"\n\t Single unpackaged bytes: {BitConverter.ToString(singleDeserializationBytes.PureProtoPayload)}" +
                  $"\n\t\t Single bytes equal: {singleSerialized.SequenceEqual(singleDeserializationBytes.PureProtoPayload)}" +
                  $"\n\t Single protobuf magic bytes (with leading count indicator if not 0): {BitConverter.ToString(singleProtoMagicBytes)}" +
                  $"\n\t Single protobuf magic bytes deserialized: {string.Join(", ", singleDeserializationBytes.ProtoIndexes)}" +
                  $"\n\t Single protobuf magic bytes deserialized re zigzag encoded: {BitConverter.ToString(singleProtoMagicBytesUnpackedZigzagEncoded)}" +
                  $"\n\t\t Single protobuf magic bytes equal: {singleProtoMagicBytes.SequenceEqual(singleProtoMagicBytesUnpackedZigzagEncoded)}" +
                  $"\n\t Single serialized content equals deserialized: {single.FirstField == singleDeserialized.FirstField}");

var few = new mf_2 { FirstField = new mf_0 {FirstField = "Hello few mf_0"}, SecondField = new mf_1 { FirstField = "Hello few mf_1"} };
byte[] fewSerialized = few.ToByteArray();
byte[] fewProtoMagicBytes = GetProtoMagicBytesForSerialization(few);
byte[] fewPayload =  AppendBytes(fewProtoMagicBytes, fewSerialized);
var fewDeserializationBytes = GetProtoMagicBytesAndPayloadForDeserialization(fewPayload);
var fewDeserialized = mf_2.Parser.ParseFrom(fewDeserializationBytes.PureProtoPayload);
var fewProtoMagicBytesUnpackedZigzagEncoded = fewDeserializationBytes.ProtoIndexes.Select(ZigzagEncode).SelectMany(x=> x).ToArray();
Console.WriteLine($"Few serdes:" +
                  $"\n\t Few serialized bytes: {BitConverter.ToString(fewSerialized)}" +
                  $"\n\t Few unpackaged bytes: {BitConverter.ToString(fewDeserializationBytes.PureProtoPayload)}" +
                  $"\n\t\t Few bytes equal: {fewSerialized.SequenceEqual(fewDeserializationBytes.PureProtoPayload)}" +
                  $"\n\t Few protobuf magic bytes (with leading count indicator if not 0): {BitConverter.ToString(fewProtoMagicBytes)}" +
                  $"\n\t Few protobuf magic bytes deserialized: {string.Join(", ", fewDeserializationBytes.ProtoIndexes)}" +
                  $"\n\t Few protobuf magic bytes deserialized re zigzag encoded: {BitConverter.ToString(fewProtoMagicBytesUnpackedZigzagEncoded)}" +
                  $"\n\t\t Few protobuf magic bytes equal: {fewProtoMagicBytes.Skip(1).SequenceEqual(fewProtoMagicBytesUnpackedZigzagEncoded)}" +
                  $"\n\t Few serialized content equals deserialized: {few.FirstField.FirstField == fewDeserialized.FirstField.FirstField && few.SecondField.FirstField == fewDeserialized.SecondField.FirstField}");

var complex = new mc_2.Types.mc_2_0.Types.mc_2_0_1 { FirstField = "Hello complex proto message!" };
byte[] complexSerialized = complex.ToByteArray();
byte[] complexProtoMagicBytes = GetProtoMagicBytesForSerialization(complex);
byte[] complexPayload =  AppendBytes(complexProtoMagicBytes, complexSerialized);
var complexDeserializationBytes = GetProtoMagicBytesAndPayloadForDeserialization(complexPayload);
var complexDeserialized = mc_2.Types.mc_2_0.Types.mc_2_0_1.Parser.ParseFrom(complexDeserializationBytes.PureProtoPayload);
var complexProtoMagicBytesUnpackedZigzagEncoded = complexDeserializationBytes.ProtoIndexes.Select(ZigzagEncode).SelectMany(x=> x).ToArray();
Console.WriteLine($"Complex serdes:" +
                  $"\n\t Complex serialized bytes: {BitConverter.ToString(complexSerialized)}" +
                  $"\n\t Complex unpackaged bytes: {BitConverter.ToString(complexDeserializationBytes.PureProtoPayload)}" +
                  $"\n\t\t Complex bytes equal: {complexSerialized.SequenceEqual(complexDeserializationBytes.PureProtoPayload)}" +
                  $"\n\t Complex protobuf magic bytes (with leading count indicator if not 0): {BitConverter.ToString(complexProtoMagicBytes)}" +
                  $"\n\t Complex protobuf magic bytes deserialized: {string.Join(", ", complexDeserializationBytes.ProtoIndexes)}" +
                  $"\n\t Complex protobuf magic bytes deserialized re zigzag encoded: {BitConverter.ToString(complexProtoMagicBytesUnpackedZigzagEncoded)}" +
                  $"\n\t\t Complex protobuf magic bytes equal: {complexProtoMagicBytes.Skip(1).SequenceEqual(complexProtoMagicBytesUnpackedZigzagEncoded)}" +
                  $"\n\t Complex serialized content equals deserialized: {complex.FirstField == complexDeserialized.FirstField}");

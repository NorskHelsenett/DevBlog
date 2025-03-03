Protobuf on Kafka: More magic bytes
===

This all started with a rendering bug. We tried to send a [Protobuf](https://protobuf.dev/) serialized message on Kafka, but on the other end we just got a message that our deserialization failed. The simple reason for this is that when using the Confluent serializer package in dotnet to serialize Protobuf messages, it introduces some additional [magic bytes](https://www.confluent.io/blog/how-to-fix-unknown-magic-byte-errors-in-apache-kafka/), which we didn't account for at the deserialization point.

Once we got to know this, we had several questions, like; Why these extra bytes? And can we simply insert them ourselves? This article aims to gently take us to the bottom of the rabbit hole, because I needed to sort out my thoughts, and be able to explain and talk to others about how surrounding issues arise and what can or should be done about them.

[Img: rabbit(hole); Confused rabbit? Anything to appease the editors need for imagery]

If your preferred language is code, you can check out a working example of how it all fits together here:
https://github.com/NorskHelsenett/DevBlog/tree/main/BlogPosts/ProtobufKafkaExtraMagicBytes
But then again, if you're here, you probably also want an explanation of what's going on and why. If you only need to get things done, using Confluents serdes libraries for protobuf ([java](https://github.com/confluentinc/schema-registry/tree/master/protobuf-serde) [dotnet](https://github.com/confluentinc/confluent-kafka-dotnet/tree/master/src/Confluent.SchemaRegistry.Serdes.Protobuf)), will probably save you a lot of time. And as they're Apache 2.0 licensed, so basically risk free to use. Again, the focus here is if you need to understand what's going on. So let's get started!

# Background

## Kafka magic bytes

Before we get started, we must be sure that we properly understand the context. Here it is that we are sending messages on Kafka, we use a schema for the messages, we use the Kafka way to indicate which schema is being used for the payload, and the schema/payload type is Protobuf. Also, because we have a large [.NET](https://dotnet.microsoft.com/en-us/) developer community that I spend a lot of time supporting, the code examples will use that.

The important thing to reiterate, is that when you use schemas with Kafka, the payload on the wire will contain 5 leading magic bytes, that indicate which schema is being used. Confluent has a nice description on it in their [article giving and overview of serializing and deserialzing here](https://docs.confluent.io/platform/current/schema-registry/fundamentals/serdes-develop/index.html#wire-format). The short of it is that the 5 bytes are 8 bit, the first one is always `0`, and the rest are the [big endian](https://en.wikipedia.org/wiki/Endianness) representation of the ID (effectively serial number) that the schema used has in the Schema Registry. So for instance the 5th registered schema/schema with ID 5, would result in the payload being prefixed with `00-00-00-00-05` if you represent the bytes as `-` separated hex.

## Protobuf schemas

But now comes the part where Protobuf has gotten special treatment. Because while you usually are confined to a schema being a singular type, prtobuf allows you to define several "message" definitions in protobuf schema/`.proto` file. Even cooler, it allows you to define nested message types!

So as a simple example, a protobuf schema .proto file with a single message could look like this:

```proto
package no.nhn.example;
message mx {
    string FirstField = 1;
}
```

A more involved proto could look like this:

```proto
package no.nhn.example;
message mx_0 {
    string FirstField = 1;
}
message mx_1 {
    string FirstField = 1;
}
message mx_2 {
    mx_0 FirstField = 1;
    mx_1 SecondField = 2;
}
```

While a completely unhinged nested mess could look like the below:

```proto
syntax = "proto3";
package no.nhn.example;
option csharp_namespace = "ExampleNamespace";

message mx_0 {
    string FirstField = 1;
}

message mx_1 {
    message mx_1_0 {
        string FirstField = 1;
    }
}

message mx_2 {
    message mx_2_0 {
        message mx_2_0_0 {
            string FirstField = 1;
        }
        message mx_2_0_1 {
            string FirstField = 1;
        }
    }
    message mx_2_1 {
        string FirstField = 1;
    }
}
```

The cool part is, that the way Confluent has specified usage of Protobuf schemaed messages on Kafka, the magic bytes refer to the schema/.proto file as a whole, but the message content is allowed be a single one of the message types defined in (referred to from) the .proto schema file, even one of the nested ones!

Which kind of makes sense, once you see how you're allowed to define a proto. But that has left us with a problem; How do we specify which Protobuf message type has been used for the message sent on Kafka? Fortunately, some smart people have pondered this, and come up with a neat answer. However, that answer is unfortunately a bit complicated. Or at least it took some of my brainpower to wrap my head around. Hence this article.

## Zigzag Encoding

Now, before we go on, we need to understand [Zigzag encoding](https://en.wikipedia.org/wiki/Variable-length_quantity#Zigzag_encoding) for things to make sense. Very basically, it is a compact way to represent small signed integers in binary, whit some really nice properties if bit shift math is your ting because you need the performance. Basically it maps 0 (decimal) to 0 (binary), -1 (decimal) to 1 binary, 1 (decimal) to 10 binary, -2 (decimal) to 11 (binary), etc. A short illustration:

```
binary -> decimal
     0 ->  0
     1 -> -1
    10 ->  1
    11 -> -2
   100 ->  2
   101 -> -3
   110 ->  3
   111 -> -4
  1000 ->  4
```

To get a Zigzag encoded value for a number wikipedia gives the pseudocode `(n << 1) ^ (n >> k - 1)` where `n` is the number to encode and `k` is the number of bits you want. Which for a 32 bit integer in dotnet becomes `(number << 1) ^ (number >> 31)` to encode. Inversely then, decoding would be done like this `(n >> 1) ^ -(n & 1)`, which if constrained to int in dotnet becomes `(encodedNumber >> 1) ^ -(encodedNumber & 1)` .

## Variable length number encoding

And, to support a large degree of nesting, and many Protobuf message types inside a proto, Protobufs usage of variable length encoding has been reused. Because we deal with indexes of the proto messages, it more specifically is variable length integers, or "varint"s we deal with. If you're familiar with [variable length values in Midi](https://stackoverflow.com/a/74215043), it's the same concept.

The way `varints` work, is that the leftmost bit is set to 1 if the next byte is part of the number. So basically, you consume bytes, and as long as it has a leading 1 you strip it and continue on to the next byte. A practical example is that 129 would be encoded as 10000001 00000001. Unfortunately, as of 2025-02-25, I haven't found any built in utilities in dotnet for variable length encoding. The [Convert.ToByte](https://learn.microsoft.com/en-us/dotnet/api/system.convert.tobyte?view=net-9.0#system-convert-tobyte(system-uint32)) only supports converting values that fit inside a single 8 bit byte. The hope in [BitConverter.GetBytes](https://learn.microsoft.com/en-us/dotnet/api/system.bitconverter.getbytes?view=net-9.0#system-bitconverter-getbytes(system-uint32)) is also misplaced, because it results in fixed length byte arrays, without the variable length encoding bits. Hence, our best option seems to be to implement the encoding and decoding ourselves, like the Kafka project has done in their [ByteUtils class](https://github.com/apache/kafka/blob/3.9/clients/src/main/java/org/apache/kafka/common/utils/ByteUtils.java#L378). Fortunately the technique is ancient, and there are many pre existing solutions to pick from.

A basic, probably not performance optimal solution (I haven't tested it though, so could be great for all I know) for encoding variable length ints in dotnet inspired by [this StackOverflow answer](https://stackoverflow.com/a/3564685) is shown below:

```cs
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
```

Similarly, for decoding:

```cs
int VarIntDecode(IEnumerable<byte> encoded)
{
    bool more = true;
    int value = 0;
    int shift = 0;
    using(var iter = encoded.GetEnumerator());
    while(more)
    {
        iter.MoveNext();
        byte lower7Bits = iter.Current;
        more = (lower7Bits & 128) != 0;
        value |= (lower7Bits & 0x7f) << shift;
        shift += 7;
    }
    return value;
}
```

A small thing to beware when decoding is that you might wan't to remove/consume the bytes as they are read, so that you don't have to keep track of where the current entry starts and ends. In dotnet you can easily achieve this by using byte arrays for you underlying data structure. Then you can get a `Stream` from your byte array by doing `Stream consumableStream = new System.IO.MemoryStream(yourByteArray);`. Which in turn allows you to call the built in `int nextByte = consumableStream.ReadByte()` to retrieve the content and move the referenced stream ahead.

Seeing as the solutions fit in between 10 and 20 lines of code, and are easily convertible to using other types for input and output like uints and streams, you probably want to just copy in the code you end up using to a utility class rather than adding to your supply chain, but figuring out what's best is up to you.

# How Prtobuf magic bytes are actually determined

So then, finally, we get to answering the interesting question; How do we indicate which proto message is used as the schema for the message we publish on Kafka?

The answer, if you read a bit further in the [documentation on the wire format](https://docs.confluent.io/platform/current/schema-registry/fundamentals/serdes-develop/index.html#wire-format), is that after the standard Kafka magic bytes, there is a new section of bytes indicating the proto message, and then after that the payload itself comes.

The Protobuf magic bytes start with a variable length zigzag encoded number telling us how many Protobuf magic bytes will follow. Then, for each nesting level of proto message definitions, we get the 0 indexed variable length zigzag encoded index of the proto message definition.

So if we revisit the example proto shown below:

```proto
package no.nhn.example;
message mx_0 {
    string FirstField = 1;
}
message mx_1 {
    string FirstField = 1;
}
message mx_2 {
    mx_0 FirstField = 1;
    mx_1 SecondField = 2;
}
```

Say we want to send a message of the `mx_2` type, and this proto is given the id 8 in the schema registry. Then the Kafka schema magic bytes would be `00 00 00 00 08` (hex). The protobuf magic bytes would in decimal be `1 2`, because there is 1 number, pointing to the index 2. Because the numbers are low, their byte representation won't be affected by the variable length encoding, but the zigzag portion of the encoding would make the `02 04` (when converted to hex). So the messages seen on Kafka would effectively start with `00 00 00 00 08 02 04`. Before the payload.

For a more complex example, we could take a new look at the complex schema again:

```proto
syntax = "proto3";
package no.nhn.example;
option csharp_namespace = "ExampleNamespace";

message mx_0 {
    string FirstField = 1;
}

message mx_1 {
    message mx_1_0 {
        string FirstField = 1;
    }
}

message mx_2 {
    message mx_2_0 {
        message mx_2_0_0 {
            string FirstField = 1;
        }
        message mx_2_0_1 {
            string FirstField = 1;
        }
    }
    message mx_2_1 {
        string FirstField = 1;
    }
}
```

Let's go with that it got ID 320 in the schema registry. That would make the schema registry magic bytes `00 00 00 0A 20`. If we then wanted to send a payload serializing the proto type `mx_2_0_1`, the decimal representation would be `3 2 0 1`. 3 because there are 3 elements following, 2 because that's the index of `mx_2` from the root of the proto, 0 because that's the index of `mx_2_0` in it's parent, and finally 1 because that is the index of `mx_2_0_1` within `mx_2_0`. Variable length zizag encoded the byes would look like this: `05 04 00 02`. So our Kafka payloads would be prefixed with the following bytes when combining the kafka schema magic bytes and protobuf magic bytes: `00 00 00 0A 20 05 04 00 02`.

But what about our first proto example?

```proto
package no.nhn.example;
message mx {
    string FirstField = 1;
}
```

Here, we hit a special case. Because most protos will contain 1 message definition, instead of specifying `1 0` (decimal, `02 00` as hex) as the protobuf magic bytes, the protobuf magic bytes are simplified to just be 0. So, if our simplest example schema above go the schema registry ID 5, the Kafka magic bytes would be `00 00 00 00 05`, the protobuf magic bytes `00`, and the prefix we would see on the wire at the start of all our payloads on Kafka would be `00 00 00 00 05 00`.

And that's pretty much it; Should someone now ask you to add the bytes indicating the protobuf message type to your Kafka payloads you'll know what to do!

# How to find the protobuf proto message index programmatically

I though of leaving this bit as an exercise for the reader, or another post, seeing as this has already become somewhat long. However, given that the note I want to end on is "now you know what's going on all the way down", it is important to actually say something about how to obtain the protobuf message type indexes without relying on counting them out by hand and introducing extra config code (though it probably would be fewer lines).

The way to go about this in dotnet, if you have a dotnet type generated using protoc, you can pass it to a function that takes a `Google.Protobuf.IMessage` or simply cast it to one. Once you have a protobuf `IMessage`, you should be able to access the `Descriptor` property of the interface. At this point you have obtained a `Google.Protobuf.Reflection.MessageDescriptor`, the parent message type in the `ContainingType` property if we're not at the root and it is `null`, and child message types (if any) in the `NestedTypes` IList.

All you need from there, is to know that the `Descriptor` also has a property called `File` of the `Google.Protobuf.Reflection.FileDescriptor` type, where you can access the list of proto message type types that live on the root level in the `MessageTypes` property. The lists are in the indexed order we need for the Kafka schema protobuf magic bytes, so we can use those.

The only thing to be careful about, is that you probably will want to start with the type you're sending, and traverse your way up the tree of nested messages from there. Which in practice will land you with a list of indexes in the reverse order of what we actually need. So remember to reverse them before you run off to start using them.

If you prefer the compactness of code to dense explanations like the one above, here is an example shown below:

```cs
List<int> ProtoIndexes(IMessage kafkaProtobufPayload)
{
    var currentDescription = kafkaProtobufPayload.Descriptor;
    List<int> indexes = [];
    while(currentDescription.ContainingType != null)
    {
        var previousDescription = currentDescription;
        currentDescription = previousDescription.ContainingType;
        var previousIndex = currentDescription.NestedTypes.IndexOf(previousDescription);
        indexes.Add(previousIndex);
    }
    var rootDescriptionIndex = currentDescription.File.MessageTypes.IndexOf(currentDescription);
    indexes.Add(rootDescriptionIndex);
    indexes.Reverse();
    return indexes;
}
// Usage example with the very nested example types from above:
var protoDeserialized = new mx_2.Types.mx_2_0.Types.mx_2_0_1 { FirstField = $"First value!" };
var protoIndexes = ProtoIndexes(protoDeserialized);
Console.WriteLine($"Proto indexes are {System.Text.Json.JsonSerializer.Serialize(protoIndexes)}");
// Prints Proto indexes are: [2,0,1]
```

And that's pretty much it! Hopefully, you now have a decent grasp of what's going on, and what these extra bytes that magically appear before protobuf messages on Kafka actually are.

You can check out a full example of how all of this fits together here:
https://github.com/NorskHelsenett/DevBlog/tree/main/BlogPosts/ProtobufKafkaExtraMagicBytes

[Share and enjoy!](https://en.wikipedia.org/wiki/Phrases_from_The_Hitchhiker%27s_Guide_to_the_Galaxy#Share_and_Enjoy)

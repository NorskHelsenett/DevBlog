This workshop focuses on working with state in a distributed way using Kafka.

It spans how to get a producer up and running to get data into Kafka, how to get a consumer running in the background to retrieve your state from Kafka for further distribution, and finally how to set up your stateful service so that it can efficiently resume from where it left off across startups.

# Before you get started

To start it all off, fire up the [docker compose 2 folders above this](../../docker-compose.yaml).
This runs all the infrastructure this exercise relies on, such as the Kafka Brokers.
If you have a leftover [ContainerData](../../ContainerData/) folder for previous runs/exercises, you might want to delete it first so that you start with a complete blank slate.

If at some point your efforts grind to a complete halt, you can reset everything by taking down the [exercise compose](./docker-compose.yaml), the [infrastructure compose](../../docker-compose.yaml), and deleting the [ContainerData folder](../../ContainerData/).

The examples/exercises are in dotnet.
You don't need anything besides docker installed to compile/run them.
But VsCode is recommended if you have not set up any tooling for working with dotnet.
Also the [REST Client extension](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) for running/executing the example/test requests.
You will also need the [protobuf compiler (protoc)](https://grpc.io/docs/protoc-installation/) (Linux: `apt install -y protobuf-compiler`, Apple: `brew install protobuf
`, other: https://github.com/protocolbuffers/protobuf/releases)


## Structure of the exercises

Each exercise is located in a folder below this one.
They are
- [./Exercise1-ReceiveAndProduce/](./Exercise1-ReceiveAndProduce/)
- [./Exercise2-ConsumeAndMakeAvailable/](./Exercise2-ConsumeAndMakeAvailable/)
- [./Exercise3-ResumeWhenRestarted/](./Exercise3-ResumeWhenRestarted/)

Finally, there is a fully working example in the [./FullyWorkingExample/](./FullyWorkingExample/) directory.

To run the exercises, simply change which exercise/folder the build context in the [docker-compose.yaml](./docker-compose.yaml#L11) is pointing to.
For instance, to run exercise 2, change the build context directory to `./Exercise2-ConsumeAndMakeAvailable`.

To test the code you can use [the http-file in this directory](./StateDistributor.http).

# First exercise

The goal of this exercise is get warm with producing data to Kafka.
To speed things up, a small web server/api is spun up in [Program.cs](./Exercise1-ReceiveAndProduce/StateDistributor/Program.cs), which takes json input that can be deserialized to [ApiParamPerson.cs](./Exercise1-ReceiveAndProduce/StateDistributor/ApiParamPerson.cs).
This, and the response should you succeed is already set up for you.
What remains for you to do is to implement producing to kafka in [KafkaProducerService.cs](./Exercise1-ReceiveAndProduce/StateDistributor/KafkaProducerService.cs), and wire it up so that the submit/write endpoint [at line 26 in Program.cs](./Exercise1-ReceiveAndProduce/StateDistributor/Program.cs#L26) actually produces to the topic.

For convenience a class binding to the Kafka connection config variables, including secrets/auth is provided in [KafkaConfigBinder.cs](./Exercise1-ReceiveAndProduce/StateDistributor/KafkaConfigBinder.cs).
An example of how to obtain a config is provided [at line 24 in KafkaProducerService.cs](./Exercise1-ReceiveAndProduce/StateDistributor/KafkaProducerService.cs#L24).

You can check your work in the Kafka UI instance running at http://localhost:8081 .
The data should end up in the topic specified in the [STATE_DISTRIBUTOR_KAFKA_STATE_TOPIC environment variable](./docker-compose.yaml#L47), `persons-protobuf`.

Note, while the input is a json formatted [ApiParamPerson.cs](./Exercise1-ReceiveAndProduce/StateDistributor/ApiParamPerson.cs), the suggested type to use for the Kafka topic in the [Produce() method in the KafkaProducerService.cs](./Exercise1-ReceiveAndProduce/StateDistributor/KafkaProducerService.cs#L33) is a [protobuf](https://protobuf.dev/) serialized payload.
You can find a suggested schema in [Protos directory](./Exercise1-ReceiveAndProduce/StateDistributor/Protos/), as well as instructions on how to generate the dotnet types.

If you struggle with this step/get stuck, you can copy the generated dotnet mapping class for the protobuf definition from [the same location in the second exercise](./Exercise2-ConsumeAndMakeAvailable/StateDistributor/Protos/GeneratedFiles/Person.cs).

# Second exercise

The second exercise focuses on consuming data form Kafka, and making it available for the basic API in [Program.cs](./Exercise2-ConsumeAndMakeAvailable/StateDistributor/Program.cs).
Again the focus is not on implementing an API or clever ways of holding and passing data in dotnet, so this is provided for you.
In the [/retrieve endpoint](./Exercise2-ConsumeAndMakeAvailable/StateDistributor/Program.cs#L52) you will find a reference to the [OutputStateService.cs](./Exercise2-ConsumeAndMakeAvailable/StateDistributor/OutputStateService.cs), which is the one we will be using in the [KafkaConsumerService.cs](./Exercise2-ConsumeAndMakeAvailable/StateDistributor/KafkaConsumerService.cs) to pass data to the frontend.

Here the focus will be on bootstrapping the consumer, much in the same way we did the producer in exercise 1, in the [GetConsumer() method](./Exercise2-ConsumeAndMakeAvailable/StateDistributor/KafkaConsumerService.cs#L61).

You can ignore the `SaveStartupTimeLastTopicPartitionOffsets()` and `GetTopicPartitions()` methods for now, they are relevant for the third exercise.

Once you have done that, you should have available an instance of a Kafka consumer in the [DoWork() method](./Exercise2-ConsumeAndMakeAvailable/StateDistributor/KafkaConsumerService.cs#L36).
Your next step is implementing the consuming [on line 47 in the DoWork() method](./Exercise2-ConsumeAndMakeAvailable/StateDistributor/KafkaConsumerService.cs#L47), and passing the consumed data to the [Store() method in OutputStateService.cs](./Exercise2-ConsumeAndMakeAvailable/StateDistributor/OutputStateService.cs) (which has already been implemented for you).
Once you've done this, you should be able to test retrieval from the API.

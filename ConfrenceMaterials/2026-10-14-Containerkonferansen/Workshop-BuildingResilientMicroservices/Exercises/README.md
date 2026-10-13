This workshop focuses on working with state in a distributed way using Kafka.

ToDo: Neat re-summary of the workshop description.

# Before you get started

To start it all off, fire up the [run the setup script in the Infrastructure directory above this](../Infrastructure/setup.sh).
Alternatively if you don't have acess to run anything besides/outside docker, you can run [the compose above](../docker-compose.yaml).
It has all (I think?) the tooling you need to spin up and manipulate the different components you need for the workshop.
So once the container it starts has set everything up it remains running idle, and you can exec into it to run for instance helm commands.
I still warmly recommend having most of the tools installed in the host shell you use, as it lowers the barrier for you to mess with the workshop components (which is good to expend energy on in a learning context like this).

If at some point your efforts grind to a complete halt, you can reset everything by deleting all the running docker containers, but it should most often be sufficient to re-run the deployment scripts.

The examples/exercises are in dotnet.
You don't need anything besides docker installed to compile/run them.
But VsCode is recommended if you have not set up any tooling for working with dotnet.
Also the [REST Client extension](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) for running/executing the example/test requests.
You will also need the [protobuf compiler (protoc)](https://grpc.io/docs/protoc-installation/) (Linux: `apt install -y protobuf-compiler`, Apple: `brew install protobuf
`, other: https://github.com/protocolbuffers/protobuf/releases)
K9s is additionally warmly recommended https://github.com/derailed/k9s#installation , as it makes jumping between logs and descriptions of various resources and components easier as you work.

## Structure of the exercises

Each exercise is located in a folder below this one.
They are
- [./Exercise1-OutboxPattern/](./Exercise1-OutboxPattern/)
- [./Exercise2-InnboxPattern/](./Exercise2-InnboxPattern/)

Finally, there is a fully working example in the [./FullyWorkingExample/](./FullyWorkingExample/) directory.

To run the exercises, simply run the `deploy.sh` script in each exercise.
For instance, to run exercise 2, run [./Exercise2-InnboxPattern/deploy.sh](./Exercise2-InnboxPattern/deploy.sh).

To test the code you can use [the http-file in this directory](./PokeWorkshopApi.http) or [the equivalent curl script](./PokeWorkshopApi.sh).

# Exercise Outbox Pattern

ToDo: Getting started guidance for those not there in person.

# Exercise Outbox Pattern

ToDo: Getting started guidance for those not there in person.

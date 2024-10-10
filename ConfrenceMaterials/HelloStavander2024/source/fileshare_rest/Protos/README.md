Generating native code from protobuf schemas!

- Create the protos folder
- Add/create the protobuf schema .proto files
- Create the output folder if it doesn't already exists
- Generate the dotnet resources by running the protobuf compiler (`protoc`)

# Example for this project

```sh
# Protoc can be obtained from https://protobuf.dev/downloads/
brew install protobuf

mkdir GeneratedFiles

protoc --proto_path=. --csharp_out=GeneratedFiles *.proto
```

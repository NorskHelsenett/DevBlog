protoc --proto_path=. --csharp_out=GeneratedFiles *.proto

Separate protos and generated file per app/project, so that they can be updated/maintained independently over time.

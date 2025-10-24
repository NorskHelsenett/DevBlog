started_time=$(date +"%FT%T")
echo "Started at $started_time"
for i in $(seq 1 300);
do
echo "Iteration $i starting"

echo "Storing value in first instance"
curl --header "Content-Type: application/json" \
  --request POST \
  --data '{
    "Key": "helloFirst '$i'",
    "Value": "dGhlc2UgYXJlIGJ5dGVz",
    "ValueComment": "these are bytes",
    "Headers": [
        {"Key": "a", "Value": "1"}
    ] }' \
  http://localhost:8001/store

echo "Retrieving value form first instance"
curl --header "Content-Type: application/json" \
  --request POST \
  --data '{ "Key": "helloFirst '$i'" }' \
  http://localhost:8001/retrieve

echo "Removing value from first instance"
curl --header "Content-Type: application/json" \
  --request POST \
  --data '{ "Key": "helloFirst '$i'" }' \
  http://localhost:8001/remove

echo "Re-Storing value in first instance"
curl --header "Content-Type: application/json" \
  --request POST \
  --data '{
    "Key": "helloFirst '$i'",
    "Value": "dGhlc2UgYXJlIGJ5dGVz",
    "ValueComment": "these are bytes",
    "Headers": [
        {"Key": "a", "Value": "2"}
    ] }' \
  http://localhost:8001/store

####

echo "Storing value in second instance"
curl --header "Content-Type: application/json" \
  --request POST \
  --data '{
    "Key": "helloSecond '$i'",
    "Value": "dGhlc2UgYXJlIGJ5dGVz",
    "ValueComment": "these are bytes",
    "Headers": [
        {"Key": "b", "Value": "1"}
    ] }' \
  http://localhost:8002/store

echo "Retrieving value form second instance"
curl --header "Content-Type: application/json" \
  --request POST \
  --data '{ "Key": "helloSecond '$i'" }' \
  http://localhost:8002/retrieve

echo "Removing value from second instance"
curl --header "Content-Type: application/json" \
  --request POST \
  --data '{ "Key": "helloSecond '$i'" }' \
  http://localhost:8002/remove

echo "Re-Storing value in second instance"
curl --header "Content-Type: application/json" \
  --request POST \
  --data '{
    "Key": "helloSecond '$i'",
    "Value": "dGhlc2UgYXJlIGJ5dGVz",
    "ValueComment": "these are bytes",
    "Headers": [
        {"Key": "b", "Value": "2"}
    ] }' \
  http://localhost:8002/store

####

echo "Storing value in third instance"
curl --header "Content-Type: application/json" \
  --request POST \
  --data '{
    "Key": "helloThird '$i'",
    "Value": "dGhlc2UgYXJlIGJ5dGVz",
    "ValueComment": "these are bytes",
    "Headers": [
        {"Key": "c", "Value": "1"}
    ] }' \
  http://localhost:8003/store

echo "Retrieving value form third instance"
curl --header "Content-Type: application/json" \
  --request POST \
  --data '{ "Key": "helloThird '$i'" }' \
  http://localhost:8003/retrieve

echo "Removing value from third instance"
curl --header "Content-Type: application/json" \
  --request POST \
  --data '{ "Key": "helloThird '$i'" }' \
  http://localhost:8003/remove

echo "Re-Storing value in third instance"
curl --header "Content-Type: application/json" \
  --request POST \
  --data '{
    "Key": "helloThird '$i'",
    "Value": "dGhlc2UgYXJlIGJ5dGVz",
    "ValueComment": "these are bytes",
    "Headers": [
        {"Key": "c", "Value": "2"}
    ] }' \
  http://localhost:8003/store

####

echo "Storing value in fourth instance"
curl --header "Content-Type: application/json" \
  --request POST \
  --data '{
    "Key": "helloFourth '$i'",
    "Value": "dGhlc2UgYXJlIGJ5dGVz",
    "ValueComment": "these are bytes",
    "Headers": [
        {"Key": "d", "Value": "1"}
    ] }' \
  http://localhost:8004/store

echo "Retrieving value form fourth instance"
curl --header "Content-Type: application/json" \
  --request POST \
  --data '{ "Key": "helloFourth '$i'" }' \
  http://localhost:8004/retrieve

echo "Removing value from fourth instance"
curl --header "Content-Type: application/json" \
  --request POST \
  --data '{ "Key": "helloFourth '$i'" }' \
  http://localhost:8004/remove

echo "Re-Storing value in fourth instance"
curl --header "Content-Type: application/json" \
  --request POST \
  --data '{
    "Key": "helloFourth '$i'",
    "Value": "dGhlc2UgYXJlIGJ5dGVz",
    "ValueComment": "these are bytes",
    "Headers": [
        {"Key": "d", "Value": "2"}
    ] }' \
  http://localhost:8004/store

echo "Iteration $i done"
# sleep 2s
done
completed_time=$(date +"%FT%T")
echo "Completed at $completed_time"

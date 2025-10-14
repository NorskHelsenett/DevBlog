curl  -w '\n' http://localhost:4081/version

curl -w '\n' http://localhost:4081/healthz

curl -w '\n' http://localhost:4081/healthz/live

curl -w '\n' http://localhost:4081/healthz/ready

# curl http://localhost:4081

echo "Requesting address by ID"

curl -w '\n' \
  --header "Content-Type: application/json" \
  --request POST \
  --data '{"filters":[{"field": "AddressId", "value": "11719375", "criteria": "Equals"}],"requestedFields":["AddressId","AddressText","North","East"]}' \
  http://localhost:4081/query

# 11719375;5059;ORKLAND;vegadresse;;;6045;Vatnveien;133;;705;51;;;Vatnveien 133;Vatnveien 133;4258;63.621611;9.686519;7318;AGDENES;01.03.2023 20:07:49.29;30.03.2025 01:32:39;11719375;7cf5af2b-c749-5dcf-8086-575bbbc1c315;;;;;;;;;;;;
# curl --header "Content-Type: application/json" --request POST --data '{"filters":[{"field": "AddressId", "value": "11719375", "criteria": "Equals"}],"requestedFields":["AddressId","AddressText","North","East"]}' http://localhost:4081/query

echo "Requesting all addresses with given street name"

curl -w '\n' --header "Content-Type: application/json" --request POST --data '{"filters":[{"field": "AddressName", "value": "Dakota", "criteria": "equals"}],"requestedFields":["AddressId","AddressText","North","East"]}' http://localhost:4081/query

echo "Requesting all addresses with given street name"

curl -w '\n' --header "Content-Type: application/json" --request POST --data '{"filters":[{"field": "AddressName", "value": "Trondheimsveien", "criteria": "equals"}],"requestedFields":["AddressId","AddressText","North","East","MunicipalityNumber","MunicipalityName"]}' http://localhost:4081/query

echo "Requesting addresses matching streetname in given municipality (note filteret field doesn't have to be requested)"

curl -w '\n' --header "Content-Type: application/json" --request POST --data '{"filters":[{"field": "AddressName", "value": "rondheimsveien", "criteria": "Contains"}, {"field": "MunicipalityNumber", "value": "3232", "criteria": "equals"}],"requestedFields":["AddressId","AddressText","North","East","PostalCode","PostalCity"]}' http://localhost:4081/query

# echo "Find all addresses matching 'rondheimsve'"
#curl -w '\n' --header "Content-Type: application/json" --request POST --data '{"filters":[{"field": "AddressName", "value": "rondheimsve", "criteria": "Contains"}],"requestedFields":["AddressId","AddressText","North","East","PostalCode","PostalCity","MunicipalityNumber","MunicipalityName"]}' http://localhost:4081/query | less

# echo "Trigger timeout by requesting everything"
# curl -w '\n' --header "Content-Type: application/json" --request POST --data '{"filters":[{"field": "AddressId", "value": "this is a not used value", "criteria": "DoesntEqual"}],"requestedFields":["AddressId","AddressText","North","East"]}' http://localhost:4081/query

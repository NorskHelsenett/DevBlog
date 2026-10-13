#!/usr/bin/env bash

BASE_URL="http://workshop-api-a1.localho.st:8080"
IDP_URL="http://keycloak.localho.st:8080/realms/workshop"
CLIENT_SECRET="todo-set-up-ws-client-secret-with-eso-and-retrieve-with-kubectl-here"

TOKEN_ENDPOINT="${IDP_URL}/protocol/openid-connect/token"

echo "🔐 Requesting token from ${TOKEN_ENDPOINT} …"
TOKEN_RESPONSE=$(curl -s -X POST "${TOKEN_ENDPOINT}" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "client_id=workshop-client" \
  --data-urlencode "client_secret=${CLIENT_SECRET}" \
  --data-urlencode "username=user" \
  --data-urlencode "password=password" \
  --data-urlencode "realm=workshop" \
  --data-urlencode "grant_type=password")

# Extract the token (jq is optional – a simple fallback is provided)
if command -v jq >/dev/null 2>&1; then
  ACCESS_TOKEN=$(echo "$TOKEN_RESPONSE" | jq -r '.access_token')
else
  ACCESS_TOKEN=$(echo "$TOKEN_RESPONSE" | sed -n 's/.*"access_token":"\([^"]*\)".*/\1/p')
fi

if [[ -z "$ACCESS_TOKEN" || "$ACCESS_TOKEN" == "null" ]]; then
  echo "❌ Failed to obtain a token. Response:"
  echo "$TOKEN_RESPONSE"
  exit 1
fi
echo "✅ Token acquired"

curl -s -X GET "${BASE_URL}/healthz/ready" | jq .

curl -s -X GET "${BASE_URL}/healthz/live" | jq .

curl -s -X GET "${BASE_URL}/healthz" | jq .

curl -s -X POST "${BASE_URL}/store" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-Id: submitting correlation id should be optional" \
  -d '{
        "Key": "my first key!",
        "Value": "the very bestest first value",
        "Headers": {
          "these": "first value",
          "values": "2nd value",
          "should": "moaar",
          "be": "I can has value?",
          "optional": "truely"
        }
      }' | jq .

curl -s -X POST "${BASE_URL}/store" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
        "Key": "meny",
        "Value": "Using crc32 '\''meny'\'' should collide with '\''menthols'\''"
      }' | jq .

curl -s -X POST "${BASE_URL}/store" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
        "Key": "menthols",
        "Value": "Using crc32 '\''menthols'\'' should collide with '\''meny'\''"
      }' | jq .

curl -s -X POST "${BASE_URL}/retrieve" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-Id: nah" \
  -d '{
        "Key": "my first key!"
      }' | jq .

curl -s -X POST "${BASE_URL}/retrieve" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
        "Key": "meny"
      }' | jq .

curl -s -X POST "${BASE_URL}/retrieve" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
        "Key": "menthols"
      }' | jq .

# exit 0

curl -s -X POST "${BASE_URL}/remove" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
        "Key": "my first key!"
      }' | jq .

curl -s -X POST "${BASE_URL}/remove" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
        "Key": "meny"
      }' | jq .

curl -s -X POST "${BASE_URL}/remove" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
        "Key": "menthols"
      }' | jq .

echo "✅ All requests completed."

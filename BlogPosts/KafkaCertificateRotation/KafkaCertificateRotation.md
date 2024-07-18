Kafka Certificate Authority Rotation
===

# Motivation

So, you've set up Kafka to run in SSL mode, or perhaps it's just time to renew the certificates on the brokers. However, you'd like to do so without downtime. Here is a quick run through of how you can do it if you're running your own infrastructure with your own Certificate Authority (CA).

# Context

This was written in july 2024, by Simon Randby while working for [Norsk Helsenett](https://www.nhn.no/). The used versions of [Kafka](https://kafka.apache.org/) was 3.5. For verification the docker images confluentinc/cp-kafka:7.5.0
and provectuslabs/kafka-ui:v0.7.2 were used, with [Docker](https://docs.docker.com/) version was 27.0.3. All Docker images run were for the arm architecture.

# Overview

The process is rather simple. Here's the complete list of steps, assuming your not only rotating the certificates, but the CA as well.

1. Obtain new CA
2. Add new CA to trust store on brokers (add, not replace)
3. Add new CA to trust store on connecting applications (again, don't replace the old CA)
4. Obtain new certificates for connecting applications and start using them
5. Obtain new certificates for brokers and start using them

Should you run with zookeepers the process is much the same for them, except you can update their certificates immediately after the new CA is trusted by both the brokers and the other zookeepers.

The important bit is that neither the central infrastructure components or connecting applications can start using the new certificates/credentials before the other side has established trust in the new issuer. And, more often than not, you will have a greater number and diversity of connecting applications, which is harder to manage, than the small handful of central components, which now mostly consists of relatively uniform K-Raft nodes. This will result in that you need a significantly greater amount of time to heard all the ~~kittens~~ connecting applications so they start using the new credentials and updating their trust stores. The effect is that it is really important that the first change you make is updating the brokers trust stores, and that the last thing you change is the certificates the brokers present themselves with.

# The interesting details

Most of this is very straight forward, at least if you've already set up the SSL credentials previously. For an example of a full cluster set up with SSL auth, you can check out our example at https://github.com/NorskHelsenett/Kafka/blob/main/GetStarted/MultiBrokerClusterWithAuthAndMtls/docker-compose.yaml

The trick is really in the "Add new CA to trust store". This might sound like it involves a lot of magic and incantations of the openssl CLI, but can in fact be achieved by simply smashing the old and new CA certificate together in a single file.

For instance, if your old/original CAs certificate (PEM formatted) is

```
-----BEGIN CERTIFICATE-----
MIICJjCCAY+gAwIBAgIUEgW7PzDKhznm9O5Xs062566vu3UwDQYJKoZIhvcNAQEL
BQAwJTEjMCEGA1UEAwwaY2EtbG9rYWxtYXNraW4uZXhhbXBsZS5jb20wHhcNMjQw
NzE2MDc0NDI5WhcNMjUwNzE2MDc0NDI5WjAlMSMwIQYDVQQDDBpjYS1sb2thbG1h
c2tpbi5leGFtcGxlLmNvbTCBnzANBgkqhkiG9w0BAQEFAAOBjQAwgYkCgYEAxfwS
PoQctCokNhDozBb+13lariFB/pFmYm+JOEKKnI1KKED8D72DG37sIV4RwMkUGYuI
qDXakr0qI796Ge+HKmeNW/7iChgce5x2MKuvbWEfBlHEoL2ZXhx2Ozz02z+4BVrF
tT1M6/VznaJ96/1Wy3daLMs/xgnMNF22+uANB78CAwEAAaNTMFEwHQYDVR0OBBYE
FMQ03XiiGG+YhzCuqetvETENEaNSMB8GA1UdIwQYMBaAFMQ03XiiGG+YhzCuqetv
ETENEaNSMA8GA1UdEwEB/wQFMAMBAf8wDQYJKoZIhvcNAQELBQADgYEAAXlkvN82
R4AHjbp0MNMKu//CkLn0bLbyZLkuxGIz2+l33kYevNlBX/G6eLHj5wtzOVhf9NnQ
gGOdS2sgGns9uIWERMLEfikYu6dRFhbgOoFMaq34BE0Xih0cwwuE5G4k0GgYMnCy
mEFIUijv07sixdHaKjtXl6TFFxCWi4f69ps=
-----END CERTIFICATE-----
```

and you have a new anchor of trust, a brand new CA whose certificate (PEM formatted) looks like this

```
-----BEGIN CERTIFICATE-----
MIICJjCCAY+gAwIBAgIUbOwkvBGDOslhgBKMHsJzLBuQdOAwDQYJKoZIhvcNAQEL
BQAwJTEjMCEGA1UEAwwaY2EtbG9rYWxtYXNraW4uZXhhbXBsZS5jb20wHhcNMjQw
NzE4MTIwODQ2WhcNMjUwNzE4MTIwODQ2WjAlMSMwIQYDVQQDDBpjYS1sb2thbG1h
c2tpbi5leGFtcGxlLmNvbTCBnzANBgkqhkiG9w0BAQEFAAOBjQAwgYkCgYEA05Ut
VCm9wnu3NCvmNr0g6J8THj3DMxVPmMfvsMCtNQsozhe2z1dkt1cpW6a153p/9Ns5
R6JPtoK25vGtch2+AIgKMcBBDI6JWaccS32wFJlXL5mG5KvZfHRIgfad++F80H38
2Cd5QQ8fkVpsDBR3laLcS7UCuP6pkdZmU0jvTbcCAwEAAaNTMFEwHQYDVR0OBBYE
FOXDo6MBzzj7M7NW1pUkfMJpD49vMB8GA1UdIwQYMBaAFOXDo6MBzzj7M7NW1pUk
fMJpD49vMA8GA1UdEwEB/wQFMAMBAf8wDQYJKoZIhvcNAQELBQADgYEAnfTTw6Sk
eSMypCESwKjgFEgZLT7P+0b7dF3N2xaS6KeofAPIBwNuWOLyNeWPNJGItmjZz51G
EQP0p3rhsR3NFT2uQx+XMNiJ20VbHcvshXfY/GVcsdecTCtiaDw6696yfr59d3cm
toA0CuS0Dzkdt/d9ihol0H8BhUAUnr3iZ34=
-----END CERTIFICATE-----
```

then the trust store (PEM formatted) for the transition period would end up looking like this

```
-----BEGIN CERTIFICATE-----
MIICJjCCAY+gAwIBAgIUEgW7PzDKhznm9O5Xs062566vu3UwDQYJKoZIhvcNAQEL
BQAwJTEjMCEGA1UEAwwaY2EtbG9rYWxtYXNraW4uZXhhbXBsZS5jb20wHhcNMjQw
NzE2MDc0NDI5WhcNMjUwNzE2MDc0NDI5WjAlMSMwIQYDVQQDDBpjYS1sb2thbG1h
c2tpbi5leGFtcGxlLmNvbTCBnzANBgkqhkiG9w0BAQEFAAOBjQAwgYkCgYEAxfwS
PoQctCokNhDozBb+13lariFB/pFmYm+JOEKKnI1KKED8D72DG37sIV4RwMkUGYuI
qDXakr0qI796Ge+HKmeNW/7iChgce5x2MKuvbWEfBlHEoL2ZXhx2Ozz02z+4BVrF
tT1M6/VznaJ96/1Wy3daLMs/xgnMNF22+uANB78CAwEAAaNTMFEwHQYDVR0OBBYE
FMQ03XiiGG+YhzCuqetvETENEaNSMB8GA1UdIwQYMBaAFMQ03XiiGG+YhzCuqetv
ETENEaNSMA8GA1UdEwEB/wQFMAMBAf8wDQYJKoZIhvcNAQELBQADgYEAAXlkvN82
R4AHjbp0MNMKu//CkLn0bLbyZLkuxGIz2+l33kYevNlBX/G6eLHj5wtzOVhf9NnQ
gGOdS2sgGns9uIWERMLEfikYu6dRFhbgOoFMaq34BE0Xih0cwwuE5G4k0GgYMnCy
mEFIUijv07sixdHaKjtXl6TFFxCWi4f69ps=
-----END CERTIFICATE-----
-----BEGIN CERTIFICATE-----
MIICJjCCAY+gAwIBAgIUbOwkvBGDOslhgBKMHsJzLBuQdOAwDQYJKoZIhvcNAQEL
BQAwJTEjMCEGA1UEAwwaY2EtbG9rYWxtYXNraW4uZXhhbXBsZS5jb20wHhcNMjQw
NzE4MTIwODQ2WhcNMjUwNzE4MTIwODQ2WjAlMSMwIQYDVQQDDBpjYS1sb2thbG1h
c2tpbi5leGFtcGxlLmNvbTCBnzANBgkqhkiG9w0BAQEFAAOBjQAwgYkCgYEA05Ut
VCm9wnu3NCvmNr0g6J8THj3DMxVPmMfvsMCtNQsozhe2z1dkt1cpW6a153p/9Ns5
R6JPtoK25vGtch2+AIgKMcBBDI6JWaccS32wFJlXL5mG5KvZfHRIgfad++F80H38
2Cd5QQ8fkVpsDBR3laLcS7UCuP6pkdZmU0jvTbcCAwEAAaNTMFEwHQYDVR0OBBYE
FOXDo6MBzzj7M7NW1pUkfMJpD49vMB8GA1UdIwQYMBaAFOXDo6MBzzj7M7NW1pUk
fMJpD49vMA8GA1UdEwEB/wQFMAMBAf8wDQYJKoZIhvcNAQELBQADgYEAnfTTw6Sk
eSMypCESwKjgFEgZLT7P+0b7dF3N2xaS6KeofAPIBwNuWOLyNeWPNJGItmjZz51G
EQP0p3rhsR3NFT2uQx+XMNiJ20VbHcvshXfY/GVcsdecTCtiaDw6696yfr59d3cm
toA0CuS0Dzkdt/d9ihol0H8BhUAUnr3iZ34=
-----END CERTIFICATE-----
```

The order does not matter.

A minor source of confusion could be that this is also how embedded certificate trust chains look like when PEM formatted. For instance, if you get a certificate form "Middle Man CA" that is trusted by the root CA "Super Legit Trust Me CA", it's not unusual that you would use a PEM file structured like this

```
-----BEGIN CERTIFICATE-----
Super Legit Trust Me CA
-----END CERTIFICATE-----
-----BEGIN CERTIFICATE-----
Middle Man CA
-----END CERTIFICATE-----
-----BEGIN CERTIFICATE-----
Your cert
-----END CERTIFICATE-----
```

This is however not the case when rotating TLS credentials when connecting to Kafka. Here the trust stores are simply

```
-----BEGIN CERTIFICATE-----
One of the CAs, old or new
-----END CERTIFICATE-----
-----BEGIN CERTIFICATE-----
The other CA
-----END CERTIFICATE-----
```

Share and enjoy!

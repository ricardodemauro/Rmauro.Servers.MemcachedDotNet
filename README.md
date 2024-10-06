# Challenge 17 - Write Your Own Memcached Server

This challenge corresponds to the seventeenth part of the Coding Challenges series by John Crickett https://codingchallenges.fyi/challenges/challenge-memcached.

## Description

This is a my implementation of a Memcached server written in C# using Native Sockets.

The server supports the following commands:

- `get`: Get the value corresponding to the provided key
- `set`: Set a key value pair
- `add`: Add a key value pair if not already present
- `flush_all`: Replace a key value pair if present

The `command.ts` file exports a function that parses the information about the command sent by the client and returns an instance of `MemCommand` class which is used by the server to handle the execution of the command.

## Running using Docker Container

First build the Docker Image with the following command.

```bash
docker build -t rmauro/memcached:latest .
```

Runnning the Container.

```bash
docker run -p 8888:22122 rmauro/memcached:latest
```

Or the Full docker run command (with all options)

```bash
docker run -e LOG_LEVEL=Warning -e SOCKET_MAX_CONNECTIONS=4096 -e USE_OBJECT_POOL=false -e SOCKET_LISTENER=IOCPSocketListener -e SOCKET_PORT=8888 -p 8888:22122 rmauro/memcached:latest
```

## Available Options

- LOG_LEVEL: Debug | Information | Warning | Error | Fatal
- SOCKET_MAX_CONNECTIONS: Any number starting from 1
- USE_OBJECT_POOL: true or false
- SOCKET_LISTENER: `IOCPSocketListener` or `IOCP2SocketListener` or `TcpClientListener`
- SOCKET_PORT: The port number. Defaults to `8888`

## Build From Source

```bash
#navigate to the csproj folder (not the solution)
cd Rmauro.Servers.Memcached

dotnet build -c Release

#run using dotnet cli
dotnet run -c Release

#create an executable file
dotnet publish -o .\publish -c Release
```

## Benchmarks

```
Using IOCPSocketListener in my machine
ALL STATS
============================================================================================================================
Type         Ops/sec     Hits/sec   Misses/sec    Avg. Latency     p50 Latency     p99 Latency   p99.9 Latency       KB/sec
----------------------------------------------------------------------------------------------------------------------------
Sets         7127.38          ---          ---         1.03856         0.82300         5.75900        13.31100       486.42
Gets        71264.40         0.00     71264.40         1.00999         0.80700         5.53500        12.67100      1801.66
Waits           0.00          ---          ---             ---             ---             ---             ---          ---
Totals      78391.78         0.00     71264.40         1.01259         0.80700         5.56700        12.67100      2288.08
```

Running the benchmark using `redislabs/memtier_benchmark`

```bash
docker run --network host redislabs/memtier_benchmark -s 192.168.1.210 -p 8888 --protocol=memcache_text --clients=20 --requests=50000 --hide-histogram
```

## TODO List

- [] Add support `expTime`
- [] Add support to `noreply`
- [] Add `replace`: Replace a key value pair if present
- [] Add other commands
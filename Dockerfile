FROM mono:latest

WORKDIR /app

# Install build dependencies
RUN apt-get update && apt-get install -y \
    nuget \
    && rm -rf /var/lib/apt/lists/*

# Copy solution files
COPY *.sln ./
COPY EmuLibrary/ ./EmuLibrary/

# Restore NuGet packages and build
RUN nuget restore && \
    msbuild /p:Configuration=Debug /p:Platform="Any CPU" /p:PostBuildEvent= EmuLibrary.sln

# Output will be in /app/EmuLibrary/bin/Debug/net462/
#!/bin/bash
cd "$(dirname "$0")"

# Cleaning prior builds
sudo rm -rf bin/Release/

# Windows build
sudo dotnet build --configuration Release --runtime win-x64
sudo chmod 777 -R bin/

#dotnet build --configuration Release --runtime linux-x64
#dotnet build --configuration Release --runtime osx-x64
#dotnet build --configuration Release --runtime osx-arm64
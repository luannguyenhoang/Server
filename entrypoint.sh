#!/bin/sh
# Read PORT from environment variable (Railway provides this)
PORT=${PORT:-8080}
export ASPNETCORE_URLS=http://+:$PORT

# Run the application
exec dotnet HoodLab.Api.dll


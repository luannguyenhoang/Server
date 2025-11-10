#!/bin/sh
# Read PORT from environment variable (Railway provides this)
PORT=${PORT:-8080}
export ASPNETCORE_URLS=http://+:$PORT

# Log the port being used
echo "Starting application on port: $PORT"
echo "ASPNETCORE_URLS=$ASPNETCORE_URLS"

# Run the application
exec dotnet HoodLab.Api.dll


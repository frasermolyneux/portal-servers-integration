#!/bin/bash
set -e

# Script to generate OpenAPI specification files
# This script builds the API project and generates the OpenAPI spec by running the app and fetching the swagger JSON

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR/src/XtremeIdiots.Portal.Integrations.Servers.Api.V1"
OUTPUT_DIR="$SCRIPT_DIR/openapi"

echo "Building the API project..."
cd "$SCRIPT_DIR/src"
dotnet build XtremeIdiots.Portal.Integrations.Servers.Api.V1/XtremeIdiots.Portal.Integrations.Servers.Api.V1.csproj -c Release

mkdir -p "$OUTPUT_DIR"

# Copy the OpenApiGeneration appsettings to the bin folder
cp "$PROJECT_DIR/appsettings.OpenApiGeneration.json" "$PROJECT_DIR/bin/Release/net9.0/"

echo "Starting the API application..."
# Set environment variables for configuration
export ASPNETCORE_ENVIRONMENT=OpenApiGeneration
export ASPNETCORE_URLS="http://localhost:5000"

# Start the application in the background
cd "$PROJECT_DIR/bin/Release/net9.0"
dotnet XtremeIdiots.Portal.Integrations.Servers.Api.V1.dll &
APP_PID=$!

echo "Waiting for the application to start (15 seconds)..."
sleep 15

# Fetch the OpenAPI specification
if curl -s -f "http://localhost:5000/swagger/v1/swagger.json" -o "$OUTPUT_DIR/openapi-v1.json"; then
    echo "OpenAPI specification downloaded successfully"
    
    # Replace /api/v1/ paths with / to match the actual API routing
    sed -i 's|/api/v1/|/|g' "$OUTPUT_DIR/openapi-v1.json"
    sed -i 's|"/api/v1"|"/"|g' "$OUTPUT_DIR/openapi-v1.json"
    
    echo "OpenAPI specification processed and saved to $OUTPUT_DIR/openapi-v1.json"
    
    # Stop the application
    echo "Stopping the application..."
    kill $APP_PID 2>/dev/null || true
    sleep 2
    
    echo "Done!"
    exit 0
else
    echo "Failed to download OpenAPI specification"
    echo "Checking if application is running..."
    curl -v "http://localhost:5000" || true
    kill $APP_PID 2>/dev/null || true
    exit 1
fi

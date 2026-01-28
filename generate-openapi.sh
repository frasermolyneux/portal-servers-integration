#!/bin/bash
set -e

# Script to generate OpenAPI specification files
# This script builds the API project and generates the OpenAPI spec by running the app and fetching the swagger JSON

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR/src/XtremeIdiots.Portal.Integrations.Servers.Api.V1"
OUTPUT_DIR="$SCRIPT_DIR/openapi"

# Trap to ensure cleanup on any exit
cleanup() {
    if [ -n "$APP_PID" ] && kill -0 "$APP_PID" 2>/dev/null; then
        echo "Stopping the application (PID: $APP_PID)..."
        kill "$APP_PID" 2>/dev/null || true
        wait "$APP_PID" 2>/dev/null || true
    fi
}
trap cleanup EXIT INT TERM

echo "Building the API project..."
dotnet build "$PROJECT_DIR/XtremeIdiots.Portal.Integrations.Servers.Api.V1.csproj" -c Release

mkdir -p "$OUTPUT_DIR"

# Copy the OpenApiGeneration appsettings to the bin folder
cp "$PROJECT_DIR/appsettings.OpenApiGeneration.json" "$PROJECT_DIR/bin/Release/net9.0/"

echo "Starting the API application..."
# Set environment variables for configuration
export ASPNETCORE_ENVIRONMENT=OpenApiGeneration
export ASPNETCORE_URLS="http://localhost:5000"

# Start the application in the background
dotnet "$PROJECT_DIR/bin/Release/net9.0/XtremeIdiots.Portal.Integrations.Servers.Api.V1.dll" &
APP_PID=$!

echo "Waiting for the application to start..."
# Wait for app to be ready with retries (max 30 seconds)
MAX_RETRIES=30
RETRY_COUNT=0
until curl -s -f "http://localhost:5000/" > /dev/null 2>&1; do
    RETRY_COUNT=$((RETRY_COUNT + 1))
    if [ $RETRY_COUNT -ge $MAX_RETRIES ]; then
        echo "ERROR: Application failed to start within 30 seconds"
        exit 1
    fi
    sleep 1
done

echo "Application is ready. Fetching OpenAPI specification..."

# Fetch the OpenAPI specification
if curl -s -f "http://localhost:5000/swagger/v1/swagger.json" -o "$OUTPUT_DIR/openapi-v1.json.tmp"; then
    echo "OpenAPI specification downloaded successfully"
    
    # Replace /api/v1/ paths with / to match the actual API routing
    # Use a temp file approach for portability across Linux and macOS
    sed 's|/api/v1/|/|g' "$OUTPUT_DIR/openapi-v1.json.tmp" | sed 's|"/api/v1"|"/"|g' > "$OUTPUT_DIR/openapi-v1.json"
    rm "$OUTPUT_DIR/openapi-v1.json.tmp"
    
    echo "OpenAPI specification processed and saved to $OUTPUT_DIR/openapi-v1.json"
    echo "Done!"
    exit 0
else
    echo "ERROR: Failed to download OpenAPI specification"
    exit 1
fi

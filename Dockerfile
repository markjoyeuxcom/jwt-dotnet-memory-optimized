# Use the official .NET 8.0 runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Memory optimization environment variables
ENV DOTNET_TieredPGO=1
ENV DOTNET_ReadyToRun=1
ENV DOTNET_TC_QuickJitForLoops=1
ENV DOTNET_TieredCompilation=1
ENV ASPNETCORE_ENVIRONMENT=Production

# Garbage Collection optimizations
ENV DOTNET_gcServer=1
ENV DOTNET_gcConcurrent=1
ENV DOTNET_GCHeapCount=0x0
ENV DOTNET_GCNoAffinitize=1

# Use the official .NET 8.0 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["JwtApi.csproj", "."]
RUN dotnet restore "JwtApi.csproj" \
    --runtime linux-x64 \
    --no-cache \
    --verbosity minimal

# Copy source code
COPY . .

# Build the application with optimizations
RUN dotnet build "JwtApi.csproj" \
    -c Release \
    -o /app/build \
    --no-restore \
    --verbosity minimal \
    -p:TreatWarningsAsErrors=false

# Publish the application
FROM build AS publish
RUN dotnet publish "JwtApi.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    --self-contained false \
    --verbosity minimal \
    -p:PublishTrimmed=false \
    -p:PublishSingleFile=false \
    -p:TreatWarningsAsErrors=false

# Final stage: Create the runtime image
FROM base AS final
WORKDIR /app

# Create non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Copy published application
COPY --from=publish /app/publish .

# Create logs directory with proper permissions
RUN mkdir -p /app/logs && chown -R appuser:appuser /app/logs

# Set proper ownership
RUN chown -R appuser:appuser /app

# Add health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost/health || exit 1

# Add labels for metadata
LABEL maintainer="JWT API Team" \
      version="1.0.0" \
      description=".NET 8.0 JWT API with Memory Optimizations" \
      framework=".NET 8.0" \
      memory-optimized="true"

# Switch to non-root user
USER appuser

# Set the entry point
ENTRYPOINT ["dotnet", "JwtApi.dll"]

# Default command arguments
CMD ["--urls", "http://0.0.0.0:80"]
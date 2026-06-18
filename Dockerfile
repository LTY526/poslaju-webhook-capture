# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["PosLajuWebhookCapture/PosLajuWebhookCapture.csproj", "PosLajuWebhookCapture/"]
RUN dotnet restore "PosLajuWebhookCapture/PosLajuWebhookCapture.csproj"

# Copy the rest of the source and build
COPY PosLajuWebhookCapture/ PosLajuWebhookCapture/
WORKDIR "/src/PosLajuWebhookCapture"
RUN dotnet build "PosLajuWebhookCapture.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "PosLajuWebhookCapture.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PosLajuWebhookCapture.dll"]

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Gateway.Poller/Gateway.Poller.csproj", "Gateway.Poller/"]
COPY ["Shared/Shared.csproj", "Shared/"]

RUN dotnet restore "Gateway.Poller/Gateway.Poller.csproj"

COPY . .
RUN dotnet build "Gateway.Poller/Gateway.Poller.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Gateway.Poller/Gateway.Poller.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Gateway.Poller.dll"]

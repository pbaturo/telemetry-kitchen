FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Ingest.Consumer/Ingest.Consumer.csproj", "Ingest.Consumer/"]
COPY ["Shared/Shared.csproj", "Shared/"]

RUN dotnet restore "Ingest.Consumer/Ingest.Consumer.csproj"

COPY . .
RUN dotnet build "Ingest.Consumer/Ingest.Consumer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Ingest.Consumer/Ingest.Consumer.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Ingest.Consumer.dll"]

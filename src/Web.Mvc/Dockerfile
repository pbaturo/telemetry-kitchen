FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Web.Mvc/Web.Mvc.csproj", "Web.Mvc/"]
COPY ["Shared/Shared.csproj", "Shared/"]

RUN dotnet restore "Web.Mvc/Web.Mvc.csproj"

COPY . .
RUN dotnet build "Web.Mvc/Web.Mvc.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Web.Mvc/Web.Mvc.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .

EXPOSE 5000

ENTRYPOINT ["dotnet", "Web.Mvc.dll"]

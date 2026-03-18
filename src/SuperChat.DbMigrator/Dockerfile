FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY SuperChat.sln ./
COPY src/SuperChat.Contracts/SuperChat.Contracts.csproj src/SuperChat.Contracts/
COPY src/SuperChat.Domain/SuperChat.Domain.csproj src/SuperChat.Domain/
COPY src/SuperChat.Infrastructure/SuperChat.Infrastructure.csproj src/SuperChat.Infrastructure/
COPY src/SuperChat.DbMigrator/SuperChat.DbMigrator.csproj src/SuperChat.DbMigrator/

RUN dotnet restore src/SuperChat.DbMigrator/SuperChat.DbMigrator.csproj

COPY . .
RUN dotnet publish src/SuperChat.DbMigrator/SuperChat.DbMigrator.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SuperChat.DbMigrator.dll"]

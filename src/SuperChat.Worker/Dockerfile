FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY SuperChat.sln ./
COPY src/SuperChat.Contracts/SuperChat.Contracts.csproj src/SuperChat.Contracts/
COPY src/SuperChat.Domain/SuperChat.Domain.csproj src/SuperChat.Domain/
COPY src/SuperChat.Application/SuperChat.Application.csproj src/SuperChat.Application/
COPY src/SuperChat.Infrastructure/SuperChat.Infrastructure.csproj src/SuperChat.Infrastructure/
COPY src/SuperChat.Worker/SuperChat.Worker.csproj src/SuperChat.Worker/

RUN dotnet restore src/SuperChat.Worker/SuperChat.Worker.csproj

COPY . .
RUN dotnet publish src/SuperChat.Worker/SuperChat.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SuperChat.Worker.dll"]

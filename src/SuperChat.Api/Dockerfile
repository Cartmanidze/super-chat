FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY SuperChat.sln ./
COPY src/SuperChat.Contracts/SuperChat.Contracts.csproj src/SuperChat.Contracts/
COPY src/SuperChat.Domain/SuperChat.Domain.csproj src/SuperChat.Domain/
COPY src/SuperChat.Application/SuperChat.Application.csproj src/SuperChat.Application/
COPY src/SuperChat.Infrastructure/SuperChat.Infrastructure.csproj src/SuperChat.Infrastructure/
COPY src/SuperChat.Api/SuperChat.Api.csproj src/SuperChat.Api/

RUN dotnet restore src/SuperChat.Api/SuperChat.Api.csproj

COPY . .
RUN dotnet publish src/SuperChat.Api/SuperChat.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SuperChat.Api.dll"]

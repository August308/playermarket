# E10 — ảnh Docker cho giao diện MVC.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY PlayerTransfer.sln ./
COPY src/PlayerTransfer.Domain/PlayerTransfer.Domain.csproj src/PlayerTransfer.Domain/
COPY src/PlayerTransfer.Infrastructure/PlayerTransfer.Infrastructure.csproj src/PlayerTransfer.Infrastructure/
COPY src/PlayerTransfer.Application/PlayerTransfer.Application.csproj src/PlayerTransfer.Application/
COPY src/PlayerTransfer.Api/PlayerTransfer.Api.csproj src/PlayerTransfer.Api/
COPY src/PlayerTransfer.Web/PlayerTransfer.Web.csproj src/PlayerTransfer.Web/
COPY tests/PlayerTransfer.Tests/PlayerTransfer.Tests.csproj tests/PlayerTransfer.Tests/
RUN dotnet restore src/PlayerTransfer.Web/PlayerTransfer.Web.csproj

COPY . .
RUN dotnet publish src/PlayerTransfer.Web/PlayerTransfer.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN mkdir -p /app/logs /app/keys && \
    useradd -m -u 1001 playertransfer && \
    chown -R playertransfer:playertransfer /app
USER playertransfer

COPY --from=build --chown=playertransfer:playertransfer /app ./

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080
ENTRYPOINT ["dotnet", "PlayerTransfer.Web.dll"]

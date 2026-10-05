# E10 — ảnh Docker cho máy chủ API. Build nhiều tầng để ảnh cuối chỉ chứa runtime.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Chép riêng tệp dự án trước để tận dụng bộ đệm tầng khi chỉ đổi mã nguồn.
COPY PlayerTransfer.sln ./
COPY src/PlayerTransfer.Domain/PlayerTransfer.Domain.csproj src/PlayerTransfer.Domain/
COPY src/PlayerTransfer.Infrastructure/PlayerTransfer.Infrastructure.csproj src/PlayerTransfer.Infrastructure/
COPY src/PlayerTransfer.Application/PlayerTransfer.Application.csproj src/PlayerTransfer.Application/
COPY src/PlayerTransfer.Api/PlayerTransfer.Api.csproj src/PlayerTransfer.Api/
COPY src/PlayerTransfer.Web/PlayerTransfer.Web.csproj src/PlayerTransfer.Web/
COPY tests/PlayerTransfer.Tests/PlayerTransfer.Tests.csproj tests/PlayerTransfer.Tests/
RUN dotnet restore src/PlayerTransfer.Api/PlayerTransfer.Api.csproj

COPY . .
RUN dotnet publish src/PlayerTransfer.Api/PlayerTransfer.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Thư mục dữ liệu SQLite và nhật ký, gắn volume ở docker-compose.
RUN mkdir -p /app/dulieu /app/logs && \
    useradd -m -u 1001 playertransfer && \
    chown -R playertransfer:playertransfer /app
USER playertransfer

COPY --from=build --chown=playertransfer:playertransfer /app ./

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Database__Provider=Sqlite \
    ConnectionStrings__Default="Data Source=/app/dulieu/playertransfer.db"

EXPOSE 8080
ENTRYPOINT ["dotnet", "PlayerTransfer.Api.dll"]

# syntax=docker/dockerfile:1.7

# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# csproj だけ先にコピーして restore を分離 (レイヤーキャッシュ最大化)
COPY src/DiscordBot-Noteworthy/DiscordBot-Noteworthy.csproj src/DiscordBot-Noteworthy/
RUN dotnet restore src/DiscordBot-Noteworthy/DiscordBot-Noteworthy.csproj

# 残りのソースをコピーして publish
COPY src/DiscordBot-Noteworthy/ src/DiscordBot-Noteworthy/
RUN dotnet publish src/DiscordBot-Noteworthy/DiscordBot-Noteworthy.csproj \
        -c Release \
        -o /app/publish \
        --no-restore \
        /p:UseAppHost=false

# ---------- Runtime stage ----------
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

# タイムゾーン (compose 側で TZ 環境変数を渡す前提だが、tzdata は同梱)
RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# posted_articles.json は data/ 配下に保存する想定 (compose 側で volume マウント)

ENTRYPOINT ["dotnet", "DiscordBot-Noteworthy.dll"]

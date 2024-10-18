FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /app

COPY ./OrgBot/OrgBot.csproj ./OrgBot/OrgBot.csproj
COPY ./ThrottledTelegramBot/ThrottledTelegramBotClient/ThrottledTelegramBotClient.csproj ./ThrottledTelegramBot/ThrottledTelegramBotClient/ThrottledTelegramBotClient.csproj

RUN dotnet restore ./OrgBot/

COPY . ./

RUN dotnet publish ./OrgBot -c Linux -o ./out

FROM mcr.microsoft.com/dotnet/runtime:8.0

WORKDIR /app

COPY --from=build /app/out ./

VOLUME /app/data

ENV BOT_TOKEN=YOUR_BOT_TOKEN
ENV OWNER=YOUR_TELEGRAM_ID
ENV SETTINGS_PATH=/app/data/botsettings.json

ENTRYPOINT ["dotnet", "OrgBot.dll"]
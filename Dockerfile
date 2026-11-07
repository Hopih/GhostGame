# Сервер «Призрак и охотники» в контейнере. Собирается прямо из исходников, .NET на машине не нужен — только Docker.
# Два этапа: тяжёлый образ с SDK собирает сервер, лёгкий образ с одним рантаймом его запускает.

# --- 1. Сборка ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# сначала только .csproj: пока зависимости не поменялись, Docker берёт restore из кэша и пересборка быстрая
COPY GhostGame.Shared/GhostGame.Shared.csproj GhostGame.Shared/
COPY GhostGame.Server/GhostGame.Server.csproj GhostGame.Server/
RUN dotnet restore GhostGame.Server/GhostGame.Server.csproj
# потом весь код сервера и Shared (клиент серверу не нужен)
COPY GhostGame.Shared/ GhostGame.Shared/
COPY GhostGame.Server/ GhostGame.Server/
RUN dotnet publish GhostGame.Server/GhostGame.Server.csproj -c Release --no-restore -o /app -p:InvariantGlobalization=true

# --- 2. Запуск ---
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
# из первого этапа забираем только готовый сервер: GhostGame.Server.dll, GhostGame.Shared.dll и map.txt
COPY --from=build /app .
# не от root: если в разборе пакетов найдут дыру, атакующий окажется непривилегированным пользователем в контейнере
USER $APP_UID
EXPOSE 5000/udp
ENTRYPOINT ["dotnet", "GhostGame.Server.dll"]
# порт внутри контейнера; снаружи он пробрасывается через -p (см. compose.yaml)
CMD ["5000"]

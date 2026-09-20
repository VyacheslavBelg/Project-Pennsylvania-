# Контекст сборки — корень репозитория: нужны оба проекта и сертификаты из docker/certs.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Сначала только файлы проектов: слой восстановления зависимостей переиспользуется,
# пока не менялись ссылки на пакеты. Это держит пересборку в бюджете 5 минут.
COPY src/Domovoy.Core/Domovoy.Core.csproj src/Domovoy.Core/
COPY src/Domovoy.Api/Domovoy.Api.csproj src/Domovoy.Api/
RUN dotnet restore src/Domovoy.Api/Domovoy.Api.csproj

COPY src/Domovoy.Core/ src/Domovoy.Core/
COPY src/Domovoy.Api/ src/Domovoy.Api/
RUN dotnet publish src/Domovoy.Api/Domovoy.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Без корневого сертификата НУЦ Минцифры контейнер не установит TLS-соединение
# с platform-api2.max.ru. Сертификаты лежат в репозитории, а не скачиваются при сборке:
# внешняя загрузка делает сборку невоспроизводимой.
COPY docker/certs/*.crt /usr/local/share/ca-certificates/

# libgssapi-krb5-2 нужен Npgsql: без неё драйвер пишет в лог ошибку загрузки библиотеки
# при каждом подключении, хотя Kerberos мы не используем.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && update-ca-certificates \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Domovoy.Api.dll"]

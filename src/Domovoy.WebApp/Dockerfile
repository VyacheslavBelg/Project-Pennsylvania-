# Контекст сборки — корень репозитория.

FROM node:22-alpine AS build
WORKDIR /src

# Слой зависимостей отдельно от исходников, чтобы пересборка не тянула npm ci заново.
COPY src/Domovoy.WebApp/package.json src/Domovoy.WebApp/package-lock.json ./
RUN npm ci

COPY src/Domovoy.WebApp/ ./

# Адрес бэкенда подставляется на сборке: у Vite нет рантайм-конфигурации.
ARG VITE_API_BASE_URL=""
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN npm run build

FROM nginx:alpine AS runtime
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist /usr/share/nginx/html/Project-Pennsylvania-
EXPOSE 80

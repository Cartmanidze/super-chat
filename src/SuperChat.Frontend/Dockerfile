FROM node:22-alpine AS build
WORKDIR /app

COPY src/SuperChat.Frontend/package.json ./
COPY src/SuperChat.Frontend/package-lock.json ./
RUN npm ci

COPY src/SuperChat.Frontend/ ./
RUN npm run build

FROM nginx:1.29-alpine AS runtime
WORKDIR /usr/share/nginx/html

COPY src/SuperChat.Frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist ./

EXPOSE 80

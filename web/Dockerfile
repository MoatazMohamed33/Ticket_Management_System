# syntax=docker/dockerfile:1
# Multi-stage build for the Angular web app.

FROM node:20-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build -- --configuration production

FROM nginx:alpine AS runtime

# SPA-friendly nginx config (try_files fallback for Angular Router).
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Angular 17 outputs to dist/<project-name>/browser
COPY --from=build /app/dist/ticket-system-web/browser /usr/share/nginx/html

EXPOSE 80

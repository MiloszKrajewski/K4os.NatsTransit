@echo off
pushd %~dp0\..\docker
docker compose -f docker-compose.yaml -f docker-services.yaml stop
docker compose -f docker-compose.yaml -f docker-services.yaml up -d
popd

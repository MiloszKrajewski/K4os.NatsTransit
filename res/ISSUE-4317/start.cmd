@echo off
pushd %~dp0
docker compose stop
docker compose up -d --remove-orphans
popd

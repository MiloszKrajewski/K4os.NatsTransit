$cs = "Host=localhost;Username=test;Password=Test!123"

docker compose stop
docker compose rm --force
docker compose up -d --remove-orphans
start-sleep 3
dotnet ef database --project .\..\src\FlowDemo.Entities update -- "$cs"
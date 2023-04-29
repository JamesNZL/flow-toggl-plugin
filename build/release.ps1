dotnet publish ./src -o ./bin -c Release -r win-x64 --no-self-contained
Compress-Archive -LiteralPath bin -DestinationPath bin/TogglTrack.zip -Force
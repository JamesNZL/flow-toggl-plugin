$FlowProcess = Get-Process -Name "Flow.Launcher" -ErrorAction SilentlyContinue

if ($FlowProcess) {
    $FlowPath = $FlowProcess.Path
    Stop-Process $FlowProcess

    dotnet publish ./src -o ./bin -c Debug -r win-x64 --no-self-contained

    Start-Process -FilePath $FlowPath
}
else
{
	echo "> ERROR: Flow.Launcher executable not currently running."
	echo "> "
	echo "> Please start Flow Launcher and try again."
}

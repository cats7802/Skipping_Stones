$unityPath = "C:\Program Files\Unity\Hub\Editor\6000.5.8f1\Editor\Unity.exe"
$projectPath = "d:\Git_Hub\Test_AI\Test_AI"
$logPath = "d:\Git_Hub\Test_AI\Test_AI\Builds\build.log"

Write-Host "Starting Unity Build..."
$proc = Start-Process -FilePath $unityPath -ArgumentList "-quit", "-batchmode", "-projectPath", "`"$projectPath`"", "-executeMethod", "BuildPlayerHelper.BuildWindowsEXE", "-logFile", "`"$logPath`"" -PassThru
$proc.WaitForExit()
Write-Host "Build Finished with Exit Code: " $proc.ExitCode

@echo off
set TargetFramework1=net46
set TargetFramework2=netstandard1.6

:: Initialise
cd /d "%~dp0"
cd NetRevisionTask

:: Clean
if exist bin\Release\%TargetFramework1% rd /s /q bin\Release\%TargetFramework1% || goto error
if exist bin\Release\%TargetFramework2% rd /s /q bin\Release\%TargetFramework2% || goto error
dotnet clean -v m -c Release -nologo || goto error

:: Build
dotnet restore || goto error
dotnet build -c Release -nologo || goto error

:: Exit
powershell write-host -fore Green Build finished.
cd /d "%~dp0"
timeout /t 2 /nobreak >nul
exit /b

:error
pause

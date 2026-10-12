@echo off
setlocal
set "component_dir=%~dp0"
if exist "%component_dir%.venv\Scripts\python.exe" (
  "%component_dir%.venv\Scripts\python.exe" "%component_dir%echos\launcher.py" %*
) else (
  python "%component_dir%echos\launcher.py" %*
)
exit /b %errorlevel%

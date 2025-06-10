@echo off
setlocal enabledelayedexpansion
set output=cs_files.txt
if exist %output% del %output%
set ignore=Plugins,Input,VeryAnimation
for /r "%cd%" %%f in (*.cs) do (
  set skip=
  for %%i in (%ignore%) do (
    if "!skip!"=="" echo %%f | findstr /i "\\%%i\\" >nul && set skip=1
  )
  if "!skip!"=="" type "%%f" >> %output%
)
echo Done!

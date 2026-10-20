@echo off
chcp 65001 >nul
cd /d "%~dp0"
where git >nul 2>nul || (echo [!] Git is not installed. & pause & exit /b 1)

set /p MSG=커밋 메시지 입력 (엔터 = 날짜로 자동): 
if "%MSG%"=="" set MSG=Update %date% %time:~0,5%

git add -A
git commit -m "%MSG%"
if errorlevel 1 echo [i] 커밋할 변경사항이 없거나 커밋 실패
git push origin main
if errorlevel 1 (echo [!] Push 실패 - 위 메시지를 확인하세요 & pause & exit /b 1)

echo.
echo Done: https://github.com/atticsoft369-cmyk/DU_Duckov-Ver2
pause

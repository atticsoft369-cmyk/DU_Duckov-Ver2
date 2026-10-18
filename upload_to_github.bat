@echo off
chcp 65001 >nul
cd /d "%~dp0"
where git >nul 2>nul || (echo [!] Git is not installed. Install from https://git-scm.com/download/win and run again. & pause & exit /b 1)
if not exist .git git init -b main
git config core.longpaths true
git add -A
git commit -m "Initial commit: DU_Duckov Ver2 Unity project"
git remote remove origin 2>nul
git remote add origin https://github.com/atticsoft369-cmyk/DU_Duckov-Ver2.git
git config http.postBuffer 524288000
git push -u origin main
echo.
echo Done: https://github.com/atticsoft369-cmyk/DU_Duckov-Ver2
pause

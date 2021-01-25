@echo off 

if exist "Essential Mods.7z" del "Essential Mods.7z"
"c:\Program Files\7-Zip\7z.exe" a "Essential Mods.7z" "Essential Mods"

if exist Tailwind.7z del Tailwind.7z
"c:\Program Files\7-Zip\7z.exe" a Tailwind.7z Tailwind

timeout /t 5
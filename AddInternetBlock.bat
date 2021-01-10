@echo off
echo Marking ModnixInstaller as downloaded from Internet...

echo [ZoneTransfer] > ModnixInstaller.exe:Zone.Identifier
echo ZoneId=3 >> ModnixInstaller.exe:Zone.Identifier

echo Done.
pause
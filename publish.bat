del /q published\*
dotnet publish src\SqlBackupTools\SqlBackupTools.csproj ^
 -c Release -o published -r win10-x64 ^
 /p:PublishSingleFile=true ^
 /p:IncludeNativeLibrariesForSelfExtract=true
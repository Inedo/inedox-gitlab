@echo off

dotnet new tool-manifest --force
dotnet tool install inedo.extensionpackager

cd GitLab\InedoExtension
dotnet inedoxpack pack . C:\LocalDev\BuildMaster\Extensions\GitLab.upack --build=Debug -o
cd ..\..
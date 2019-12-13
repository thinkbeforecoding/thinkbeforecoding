@echo off
dotnet tool restore
dotnet paket restore
dotnet fsi blog.fsx
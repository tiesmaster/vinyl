#!/bin/bash

dotnet build Vinyl.slnf
dotnet test

MSYS_NO_PATHCONV=1 'C:/Program Files (x86)/Microsoft Visual Studio/2019/Professional/MSBuild/Current/Bin/amd64/MSBuild.exe' src/Vinyl.Vsix/Vinyl.Vsix.csproj /v:m
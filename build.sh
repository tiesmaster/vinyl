#!/bin/bash

dotnet build Vinyl.slnf
dotnet test

MSYS_NO_PATHCONV=1 "`vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe'`" src/Vinyl.Vsix/Vinyl.Vsix.csproj /v:m
#!/bin/bash
export ASPNETCORE_ENVIRONMENT=Development
cd /home/jamailo/workspace/sapi-sk-proxy
exec dotnet run --urls="http://0.0.0.0:13333;https://0.0.0.0:13334"

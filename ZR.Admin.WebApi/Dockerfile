#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 8888

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ZR.Admin.WebApi/ZR.Admin.WebApi.csproj", "ZR.Admin.WebApi/"]
COPY ["ZR.CodeGenerator/ZR.CodeGenerator.csproj", "ZR.CodeGenerator/"]
COPY ["Infrastructure/ZR.Infrastructure.csproj", "Infrastructure/"]
COPY ["ZR.Common/ZR.Common.csproj", "ZR.Common/"]
COPY ["ZR.ServiceCore/ZR.ServiceCore.csproj", "ZR.ServiceCore/"]
COPY ["ZR.Repository/ZR.Repository.csproj", "ZR.Repository/"]
COPY ["ZR.Model/ZR.Model.csproj", "ZR.Model/"]
COPY ["ZR.Service/ZR.Service.csproj", "ZR.Service/"]
COPY ["ZR.Tasks/ZR.Tasks.csproj", "ZR.Tasks/"]
RUN dotnet restore "./ZR.Admin.WebApi/ZR.Admin.WebApi.csproj"
COPY . .
WORKDIR "/src/ZR.Admin.WebApi"
RUN dotnet build "./ZR.Admin.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ZR.Admin.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ZR.Admin.WebApi.dll"]
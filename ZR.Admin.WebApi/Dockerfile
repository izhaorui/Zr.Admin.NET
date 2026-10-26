#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8888
ENV TZ=Asia/Shanghai
# 可根据需要设置环境
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8888

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# 先拷贝 csproj 以便利用层缓存
COPY ["ZR.Admin.WebApi/ZR.Admin.WebApi.csproj", "ZR.Admin.WebApi/"]
COPY ["ZR.CodeGenerator/ZR.CodeGenerator.csproj", "ZR.CodeGenerator/"]
COPY ["Infrastructure/ZR.Infrastructure.csproj", "Infrastructure/"]
COPY ["ZR.Common/ZR.Common.csproj", "ZR.Common/"]
COPY ["ZR.ServiceCore/ZR.ServiceCore.csproj", "ZR.ServiceCore/"]
COPY ["ZR.Repository/ZR.Repository.csproj", "ZR.Repository/"]
COPY ["ZR.Model/ZR.Model.csproj", "ZR.Model/"]
COPY ["ZR.Service/ZR.Service.csproj", "ZR.Service/"]
COPY ["ZR.Tasks/ZR.Tasks.csproj", "ZR.Tasks/"]
COPY ["ZR.Mall/ZR.Mall.csproj", "ZR.Mall/"]
COPY ["ZR.Workflow/ZR.Workflow.csproj", "ZR.Workflow/"]

# 恢复依赖（此层可被缓存）
RUN dotnet restore "./ZR.Admin.WebApi/ZR.Admin.WebApi.csproj"

# 复制其余文件并构建（构建时跳过 restore，使用之前缓存）
COPY . .
WORKDIR "/src/ZR.Admin.WebApi"
RUN dotnet build "./ZR.Admin.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/ZR.Admin.WebApi"
RUN dotnet publish "./ZR.Admin.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# 显式拷贝 IP 库文件，避免未被 publish 带入导致运行时报文件不存在
COPY --from=build /src/ZR.Admin.WebApi/ip2region_v4.xdb ./ip2region_v4.xdb

# 可选：非 root 运行
#RUN addgroup --system appgroup && adduser --system appuser --ingroup appgroup
#USER appuser

RUN mkdir -p /app/DataProtection \
    && chmod -R 777 /app/DataProtection

ENTRYPOINT ["dotnet", "ZR.Admin.WebApi.dll"]
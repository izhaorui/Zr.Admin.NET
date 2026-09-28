# 公网 C 端宿主（ZR.Front.WebApi）镜像：只暴露 [PublicApi] 标记的 C 端接口
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8888
ENV TZ=Asia/Shanghai
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8888

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# 先拷贝 csproj 以便利用层缓存
# 依赖闭包：ZR.Front.WebApi → ZR.Mall → ZR.ServiceCore → ZR.Repository → ZR.Model → ZR.Common → Infrastructure
COPY ["ZR.Front.WebApi/ZR.Front.WebApi.csproj", "ZR.Front.WebApi/"]
COPY ["ZR.Mall/ZR.Mall.csproj", "ZR.Mall/"]
COPY ["ZR.ServiceCore/ZR.ServiceCore.csproj", "ZR.ServiceCore/"]
COPY ["ZR.Repository/ZR.Repository.csproj", "ZR.Repository/"]
COPY ["ZR.Model/ZR.Model.csproj", "ZR.Model/"]
COPY ["ZR.Common/ZR.Common.csproj", "ZR.Common/"]
COPY ["Infrastructure/ZR.Infrastructure.csproj", "Infrastructure/"]

# 恢复依赖（此层可被缓存）
RUN dotnet restore "./ZR.Front.WebApi/ZR.Front.WebApi.csproj"

# 复制其余文件并构建（构建时跳过 restore，使用之前缓存）
COPY . .
WORKDIR "/src/ZR.Front.WebApi"
RUN dotnet build "./ZR.Front.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/ZR.Front.WebApi"
RUN dotnet publish "./ZR.Front.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# 显式拷贝 IP 库文件，避免未被 publish 带入导致运行时报文件不存在
COPY --from=build /src/ZR.Admin.WebApi/ip2region_v4.xdb ./ip2region_v4.xdb

RUN mkdir -p /app/DataProtection \
    && chmod -R 777 /app/DataProtection

ENTRYPOINT ["dotnet", "ZR.Front.WebApi.dll"]

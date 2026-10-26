# 公网 C 端宿主（ZR.Front.WebApi）镜像：只暴露 [PublicApi] 标记的 C 端接口
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8888
ENV TZ=Asia/Shanghai
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8888

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
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
# IP 库为本站自有文件（csproj 已配置 CopyToPublishDirectory），此处再显式兜底一次
COPY --from=build /src/ZR.Front.WebApi/ip2region_v4.xdb ./ip2region_v4.xdb

RUN mkdir -p /app/DataProtection \
    && chmod -R 777 /app/DataProtection

ENTRYPOINT ["dotnet", "ZR.Front.WebApi.dll"]

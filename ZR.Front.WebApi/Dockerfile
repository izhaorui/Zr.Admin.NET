# 公网 C 端宿主（ZR.Front.WebApi）镜像：只暴露 [PublicApi] 标记的 C 端接口
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8888
ENV TZ=Asia/Shanghai
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8888

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
# 目标运行时。必须显式指定：不指定时 NuGet 会把 SkiaSharp.NativeAssets.Linux 里
# 全部 13 个 Linux RID 的 libSkiaSharp.so 都复制进产物（约 142MB，另有 Windows/macOS 约 48MB），
# 而容器实际只用得上与之匹配的那一个（linux-x64 约 11.6MB）。
# 换 arm64 构建时用 --build-arg TARGET_RID=linux-arm64。
ARG TARGET_RID=linux-x64
WORKDIR /src

# 先拷贝 csproj 以便利用层缓存
# 依赖闭包：ZR.Front.WebApi → ZR.Mall → ZR.Payment / ZR.ServiceCore → ZR.Repository → ZR.Model → ZR.Common → Infrastructure
COPY ["ZR.Front.WebApi/ZR.Front.WebApi.csproj", "ZR.Front.WebApi/"]
COPY ["ZR.Mall/ZR.Mall.csproj", "ZR.Mall/"]
COPY ["ZR.Payment/ZR.Payment.csproj", "ZR.Payment/"]
COPY ["ZR.Oss/ZR.Oss.csproj", "ZR.Oss/"]
COPY ["ZR.ServiceCore/ZR.ServiceCore.csproj", "ZR.ServiceCore/"]
COPY ["ZR.Repository/ZR.Repository.csproj", "ZR.Repository/"]
COPY ["ZR.Model/ZR.Model.csproj", "ZR.Model/"]
COPY ["ZR.Common/ZR.Common.csproj", "ZR.Common/"]
COPY ["Infrastructure/ZR.Infrastructure.csproj", "Infrastructure/"]

# 恢复依赖（此层可被缓存）。带 RID 还原，后续 build/publish 的 --no-restore 才能命中对应资产。
RUN dotnet restore "./ZR.Front.WebApi/ZR.Front.WebApi.csproj" -r $TARGET_RID

# 复制其余文件并构建（构建时跳过 restore，使用之前缓存）
COPY . .
WORKDIR "/src/ZR.Front.WebApi"
RUN dotnet build "./ZR.Front.WebApi.csproj" -c $BUILD_CONFIGURATION -r $TARGET_RID -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/ZR.Front.WebApi"
# --self-contained false：框架依赖部署，基础镜像已含运行时，不需再带一份运行时文件
RUN dotnet publish "./ZR.Front.WebApi.csproj" -c $BUILD_CONFIGURATION -r $TARGET_RID --self-contained false -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# IP 库为本站自有文件（csproj 已配置 CopyToPublishDirectory），此处再显式兜底一次
COPY --from=build /src/ZR.Front.WebApi/ip2region_v4.xdb ./ip2region_v4.xdb

RUN mkdir -p /app/DataProtection \
    && chmod -R 777 /app/DataProtection

ENTRYPOINT ["dotnet", "ZR.Front.WebApi.dll"]

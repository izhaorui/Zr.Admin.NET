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
# 目标运行时。必须显式指定：不指定时 NuGet 会把 SkiaSharp.NativeAssets.Linux 里
# 全部 13 个 Linux RID 的 libSkiaSharp.so 都复制进产物（约 142MB，另有 Windows/macOS 约 48MB），
# 而容器实际只用得上与之匹配的那一个（linux-x64 约 11.6MB）。
# 换 arm64 构建时用 --build-arg TARGET_RID=linux-arm64。
ARG TARGET_RID=linux-x64
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
COPY ["ZR.Oss/ZR.Oss.csproj", "ZR.Oss/"]
COPY ["ZR.Payment/ZR.Payment.csproj", "ZR.Payment/"]

# 恢复依赖（此层可被缓存）。带 RID 还原，后续 build/publish 的 --no-restore 才能命中对应资产。
RUN dotnet restore "./ZR.Admin.WebApi/ZR.Admin.WebApi.csproj" -r $TARGET_RID

# 复制其余文件并构建（构建时跳过 restore，使用之前缓存）
COPY . .
WORKDIR "/src/ZR.Admin.WebApi"
RUN dotnet build "./ZR.Admin.WebApi.csproj" -c $BUILD_CONFIGURATION -r $TARGET_RID -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/ZR.Admin.WebApi"
# --self-contained false：框架依赖部署，基础镜像已含运行时，不需再带一份运行时文件
RUN dotnet publish "./ZR.Admin.WebApi.csproj" -c $BUILD_CONFIGURATION -r $TARGET_RID --self-contained false -o /app/publish /p:UseAppHost=false --no-restore

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

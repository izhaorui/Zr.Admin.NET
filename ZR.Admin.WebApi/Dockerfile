FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
#创建 /app文件夹
WORKDIR /app
#创建挂载目录,用于将程序部署在服务器本地
#VOLUME /app
#设置docker容器对外暴露端口
EXPOSE 5000
VOLUME /app/logs
#COPY bin/Release/net5.0/publish/ app/
COPY . app/

#设置容器内的时区，如果不设置，默认时区是标准时间比北京时间晚8个小时
RUN echo "Asia/shanghai" > /etc/timezone
RUN cp /usr/share/zoneinfo/Asia/Shanghai /etc/localtime

# 复制发布文件到工作目录
#COPY . app/

ENTRYPOINT ["dotnet", "ZR.Admin.WebApi.dll"]
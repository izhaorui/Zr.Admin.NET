FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
#���� /app�ļ���
WORKDIR /app
#��������Ŀ¼,���ڽ��������ڷ���������
#VOLUME /app
#����docker�������Ⱪ¶�˿�
EXPOSE 5000
VOLUME /app/logs
#COPY bin/Release/net5.0/publish/ app/
COPY . app/

#���������ڵ�ʱ������������ã�Ĭ��ʱ���Ǳ�׼ʱ��ȱ���ʱ����8��Сʱ
RUN echo "Asia/shanghai" > /etc/timezone
RUN cp /usr/share/zoneinfo/Asia/Shanghai /etc/localtime

# ���Ʒ����ļ�������Ŀ¼
#COPY . app/

ENTRYPOINT ["dotnet", "ZR.Admin.WebApi.dll"]
# ---------- 运行时阶段 ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# 运行期需要的目录：动态插件目录、日志目录
RUN mkdir -p /app/UserDll /app/Logs

# 拷贝本地发布产物（发布到 publish/ 目录）
COPY publish/ ./

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8000 \
    TZ=Asia/Shanghai

EXPOSE 8000

ENTRYPOINT ["dotnet", "DTSoftServerApp.dll"]

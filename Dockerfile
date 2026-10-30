# Imagem da Portfolio API. Multi-stage: o SDK so existe no estagio de build;
# a imagem final leva apenas o runtime do ASP.NET Core e a pasta publicada.
#
#   docker build -t portfolio-api .
#   docker run -p 5080:8080 -e ConnectionStrings__DefaultConnection=... -e Jwt__Secret=... portfolio-api

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG APP_VERSION=1.0.0
ARG GIT_SHA=local
WORKDIR /src

# Primeiro so os arquivos de projeto: o restore fica em cache enquanto eles nao mudam.
COPY Directory.Build.props Portfolio.sln ./
COPY src/Portfolio.Core/Portfolio.Core.csproj          src/Portfolio.Core/
COPY src/Portfolio.UseCases/Portfolio.UseCases.csproj  src/Portfolio.UseCases/
COPY src/Portfolio.Infra/Portfolio.Infra.csproj        src/Portfolio.Infra/
COPY src/Portfolio.WebApi/Portfolio.WebApi.csproj      src/Portfolio.WebApi/
RUN dotnet restore src/Portfolio.WebApi/Portfolio.WebApi.csproj

COPY src/ src/
RUN dotnet publish src/Portfolio.WebApi/Portfolio.WebApi.csproj \
    --configuration Release --no-restore --output /app \
    -p:Version=${APP_VERSION} \
    -p:InformationalVersion=${APP_VERSION}+${GIT_SHA}

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# A imagem base ja escuta em 8080 (ASPNETCORE_HTTP_PORTS) e roda como usuario sem privilegio.
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
USER $APP_UID

COPY --from=build /app ./

# Sem HEALTHCHECK aqui: a imagem base nao traz curl/wget. A saude e conferida de
# fora, pelo deploy/Smoke-Test.ps1, em GET /health.
ENTRYPOINT ["dotnet", "Portfolio.WebApi.dll"]

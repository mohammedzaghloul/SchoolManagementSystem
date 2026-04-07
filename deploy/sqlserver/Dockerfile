FROM mcr.microsoft.com/mssql/server:2022-latest

USER root

# Railway's root filesystem restrictions can prevent the default non-root
# SQL Server image from creating its internal /.system directory.
RUN mkdir -p /.system /var/opt/mssql /var/opt/mssql/data /var/opt/mssql/log /var/opt/mssql/secrets

ENV ACCEPT_EULA=Y

EXPOSE 1433

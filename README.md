# EV Charging Dashboard

A dockerized Azure Webapp for displaying Electric Vehicle charing station status, following the OCPP standard data model. It can be deployed in as a Docker container into an Azure Web App instance. Containers are automaticLly built on checkin to the main branch and can be run using the Docker run command. They are located at ghcr.io/barnstee/evchargingdashboard:main.

This is a companion dashboard for the [OCPP Central System project](https://github.com/barnstee/iot-edge-ocpp-central-system).


## Configuration Settings

The following environment variables need to be defined:
* "MyDBConnection": Azure SQL Server connection string
* "StorageAccountConnectionString": Azure Storage connection string
* "IotHubEventHubName": Azure IoT Hub name
* "EventHubEndpointIotHubOwnerConnectionString": Azure Event Hub owner connection string
* "APIKey": SendGrid SaaS API key
* "SignalRConnectionString": Azure SignalR connection string
* "ASPNETCORE_ENVIRONMENT": Set to "Development" for enabling the exception web pages


## CI/CD Status

[![Docker](https://github.com/barnstee/EVChargingDashboard/actions/workflows/docker-publish.yml/badge.svg)](https://github.com/barnstee/EVChargingDashboard/actions/workflows/docker-publish.yml)

[![publish](https://github.com/barnstee/EVChargingDashboard/actions/workflows/publish-app.yml/badge.svg)](https://github.com/barnstee/EVChargingDashboard/actions/workflows/publish-app.yml)

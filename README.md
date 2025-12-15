# FD Wotlk Web API

This project provides a web-based interface for creating user accounts for a World of Warcraft: Wrath of the Lich King private server running on MaNGOS. It consists of an ASP.NET Core Web API backend.

## Overview

The solution is divided into two main projects:

1.  **FDWotlkWebApi**: An ASP.NET Core project that exposes a RESTful API for account management. It communicates with the MaNGOS server via SOAP to create accounts and interacts with a MySQL database to update account details.

## Features

*   **Account Creation**: Users can create new game accounts by providing a username and password.
*   **Server Info**: The API can fetch and display information about the game server.
*   **Expansion Update**: Automatically sets the game expansion to WotLK (expansion 2) for newly created accounts.

## Architecture

*   **Backend**: ASP.NET Core 9.0 Web API
*   **Database**: MySQL
*   **Communication Protocol**: SOAP (for MaNGOS server), REST (for client-server)

## Prerequisites

*   [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
*   A running MaNGOS WotLK server with SOAP enabled.
*   A MySQL database accessible to the API.

## Configuration

The application's settings are managed in `FDWotlkWebApi/appsettings.json`. You will need to configure the following sections:

### Database Connection

Update the `ConnectionStrings` section with the details for your `wotlkrealmd` database.

```json
"ConnectionStrings": {
  "Mangos": "Server=your-mysql-host;Port=3306;Database=wotlkrealmd;User Id=your-user;Password=your-password;"
}
```

### SOAP Server

Configure the `SoapServer` section with the connection details for your MaNGOS server's SOAP interface.

```json
"SoapServer": {
  "Host": "your-mangos-host",
  "Port": 7878,
  "Username": "your-soap-username",
  "Password": "your-soap-password"
}
```

## Running the Application

1.  **Run the API**:
    *   Navigate to the `FDWotlkWebApi` directory.
    *   Run the command: `dotnet run`
    *   The API will be available at `https://localhost:5001`.


## API Endpoints

*   `POST /api/account/create`: Creates a new game account.
    *   **Body**: `{ "username": "youruser", "password": "yourpassword" }`
*   `GET /api/account/server-info`: Retrieves information from the MaNGOS server.
*   `GET /api/wotlk/players`: Retrieves a list of all players on the server.


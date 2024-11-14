# MaksIT.Dapr

This repository hosts the `maksit-dapr` project, which utilizes [Dapr](https://dapr.io/) (Distributed Application Runtime) to facilitate building and managing microservices with ease. The project focuses on implementing a robust, scalable solution leveraging Dapr's building blocks and abstractions.

## Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Installation](#installation)
- [Configuration](#configuration)
- [Usage](#usage)
  - [Running the Project](#running-the-project)
  - [Environment Variables](#environment-variables)
- [Testing](#testing)
- [Deployment](#deployment)
- [Contributing](#contributing)
- [License](#license)

## Overview

`maksit-dapr` serves as a foundational project to explore and implement Dapr-based microservices, demonstrating the integration of Dapr’s pub-sub, bindings, state management, and other building blocks in a distributed system environment.

## Features

- **Pub-Sub Integration**: Uses Dapr's pub-sub component for seamless event-driven communication.
- **State Management**: Efficient, distributed state handling across microservices.

## Getting Started

Ensure that you have the following installed:

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/get-started)

## Installation

To install MaksIT.Core, add the package to your project via NuGet:

```powershell
dotnet add package MaksIT.Dapr
```

Or manually add it to your .csproj file:

```powershell
<PackageReference Include="MaksIT.Dapr" Version="1.0.0" />
```

## Usage

### Registering Dapr Services

To use `maksit-dapr` in your application, you must register the provided services for dependency injection. Follow these steps to integrate Dapr's pub-sub and state management capabilities in your ASP.NET Core application:

1. **Register the Publisher and State Store Services**: Add these services in `Program.cs` or `Startup.cs`.

```csharp
using MaksIT.Dapr.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register Dapr services
builder.Services.RegisterPublisher();
builder.Services.RegisterStateStore();

var app = builder.Build();

// Set up Dapr subscriber middleware (optional)
// Only after Webapi Authorization services
app.RegisterSubscriber();
```

2. **Use Controller as a Dapr Subscriber**:

To designate a controller as a Dapr subscriber, annotate it with the `[Topic("pubsubName", "name")]` attribute:

```csharp
using Dapr;

[Topic("my-pubsub", "my-topic")]
public class MyController : ControllerBase
{
    [HttpPost("/my-endpoint")]
    public IActionResult ReceiveMessage([FromBody] MyCommand payload)
    {


        // Handle message
        return Ok();
    }
}
```

### Injecting and Using Dapr Services

With the services registered, you can inject `IDaprPublisherService` and `IDaprStateStoreService` into controllers or other services as needed:

```csharp
using MaksIT.Dapr;

public class MyService
{
    private readonly IDaprPublisherService _publisher;
    private readonly IDaprStateStoreService _stateStore;

    public MyService(IDaprPublisherService publisher, IDaprStateStoreService stateStore)
    {
        _publisher = publisher;
        _stateStore = stateStore;
    }

    public async Task PublishEventAsync()
    {
        var command = new MyCommand
        {
            CommandId = Guid.NewGuid(),
            CommandName = "SampleCommand",
            Timestamp = DateTime.UtcNow
        };

        var options = new JsonSerializerOptions
        {
          PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
          DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        var payload = JsonSerializer.Serialize(command, options);

        var result = await _publisher.PublishEventAsync("my-pubsub", "my-topic", payload);
        if (!result.IsSuccess)
        {
            // Handle error
        }
    }

    public async Task SetStateAsync()
    {
        var saveResult = await _stateStore.SetStateAsync("my-store", "my-key", "my-value");
        if (!saveResult.IsSuccess)
        {
            // Handle error
        }
    }

    public async Task<string?> GetStateAsync()
    {
        var stateResult = await _stateStore.GetStateAsync<string>("my-store", "my-key");
        return stateResult.IsSuccess ? stateResult.Value : null;
    }

    public async Task DeleteStateAsync()
    {
        var deleteResult = await _stateStore.DeleteStateAsync("my-store", "my-key");
        if (!deleteResult.IsSuccess)
        {
            // Handle error
        }
    }
}
```

This setup enables your ASP.NET Core application to utilize Dapr's pub-sub, state management, and other building blocks with minimal boilerplate.


## Contributing

Contributions to this project are welcome! Please fork the repository and submit a pull request with your changes. If you encounter any issues or have feature requests, feel free to open an issue on GitHub.

## Contact

If you have any questions or need further assistance, feel free to reach out:

- **Email**: [maksym.sadovnychyy@gmail.com](mailto:maksym.sadovnychyy@gmail.com)

## License

This project is licensed under the MIT License. See the full license text below.

---

### MIT License

```
MIT License

Copyright (c) 2024 Maksym Sadovnychyy (MAKS-IT)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

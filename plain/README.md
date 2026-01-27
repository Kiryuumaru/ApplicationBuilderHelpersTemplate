# Plain CLI Template

A simple CLI application template demonstrating DDD domain events with clean architecture.

## Quick Start

```bash
dotnet build
dotnet run --project src/Presentation.Cli
```

## Project Structure

```
plain/
├── src/
│   ├── Domain/                    # Domain layer - entities, events, interfaces
│   ├── Domain.SourceGenerators/   # Build constants generator
│   ├── Application/               # Application layer - event handlers, services
│   ├── Presentation/              # Shared presentation (base commands)
│   └── Presentation.Cli/          # CLI entry point
└── tests/
    ├── Domain.UnitTests/
    └── Application.UnitTests/
```

## Features

- **Domain Events**: Demonstrates `IDomainEvent`, `DomainEvent`, `Entity`, and `IAggregateRoot`
- **Event Handlers**: `IDomainEventHandler` and `DomainEventDispatcher` for decoupled side effects
- **Clean Architecture**: Proper layer separation with dependency inversion
- **Native AOT Ready**: Pattern matching for event dispatch (no reflection)

## HelloWorld Example

The template includes a HelloWorld feature that demonstrates:

1. `HelloWorldEntity` - An entity that raises a domain event on creation
2. `HelloWorldCreatedEvent` - A domain event record
3. `HelloWorldCreatedEventHandler` - Handles the event and logs a message

Run the CLI to see domain events in action:

```bash
dotnet run --project src/Presentation.Cli
```

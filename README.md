# Themisquo

![Themisquo](Themisquo.png)

A lightweight core library for Command and Query Responsibility Segregation (CQRS) and Event Sourcing (ES) on .NET. Based on the teachings in [CqrsInPractice](https://github.com/vkhorikov/CqrsInPractice).

With Themisquo you separate commands and queries into plain conceptual definitions (`ICommand`, `IQuery<T>`) and their handlers (`ICommandHandler<T>`, `IQueryHandler<T, TResult>`). A dispatcher resolves the right handler for whatever command or query you give it, so callers never depend on a handler directly. To keep the CQRS boundary honest, only commands are handed a scoped event dispatcher when they're invoked — queries never get one, so they can't sneak in state changes. Handlers use that event dispatcher to raise events describing what changed, which are routed to event observers for side effects (persistence, notifications, integration events, and so on).

## Why Themisquo?

* **Small and dependency-light** — the core package only depends on `Microsoft.Extensions.DependencyInjection.Abstractions`. No MediatR-style megapackage.
* **CQRS enforced by design, not convention** — commands get an `IEventDispatcher`; queries don't. It's structurally impossible for a query handler to raise events.
* **Handlers resolved through your DI container** — no reflection-based assembly scanning magic at runtime; you register handlers explicitly (or scan once at startup).
* **Fail fast on missing handlers** — `ValidateHandlersRegistered()` scans your commands, queries, and events at startup and throws before your first request does.
* **First-class ASP.NET Core mapping** — turn a command or query type into a minimal API endpoint in one line, including automatic `201 Created` + `Location` responses driven by the events a command raises.
* **Pluggable validation** — an optional FluentValidation integration validates commands/queries before they ever reach a handler.
* **Bring your own event sourcing/integration story** — `IEventObserver<TEvent>` and `IEventDispatcher` are just interfaces, so you can back them with EF Core, an event store, MassTransit, or anything else.

## Packages

| Package | Purpose |
|---|---|
| `Themisquo` | Core abstractions: `ICommand`, `IQuery<T>`, handlers, dispatcher, event dispatcher/observer, DI registration helpers. |
| `Themisquo.AspNetCore` | Maps commands/queries to minimal API endpoints, plus a `ThemisquoExceptionHandler` that turns exceptions into `ProblemDetails`. |
| `Themisquo.FluentValidation` | Wires up FluentValidation validators for commands/queries and a validating dispatcher decorator. |

## Getting started

Install the core package:

```
dotnet add package Themisquo
```

Define a command and its handler:

```csharp
public record CreateCardCommand(Guid ProjectId, string Title) : ICommand
{
    public Guid Instance { get; } = Guid.NewGuid();
}

public class CreateCardCommandHandler : ICommandHandler<CreateCardCommand>
{
    public async Task Handle(CreateCardCommand command, IEventDispatcher eventDispatcher)
    {
        // ... do the work ...
        await eventDispatcher.Dispatch(new CardCreatedEvent(command.ProjectId, Guid.NewGuid()));
    }
}
```

Define a query and its handler:

```csharp
public record GetCardQuery(Guid CardId) : IQuery<CardDto>;

public class GetCardQueryHandler : IQueryHandler<GetCardQuery, CardDto>
{
    public async Task<CardDto> Handle(GetCardQuery query)
    {
        // ... load and map ...
        return await Task.FromResult(new CardDto());
    }
}
```

Register everything and dispatch:

```csharp
services.AddThemisquo();
services.AddCommandHandler<CreateCardCommandHandler, CreateCardCommand>();
services.AddQueryHandler<GetCardQueryHandler, GetCardQuery, CardDto>();

// wherever you resolve IDispatcher / IQueryDispatcher from DI:
await dispatcher.Dispatch(new CreateCardCommand(projectId, "New card"));
var card = await dispatcher.Dispatch(new GetCardQuery(cardId));
```

## Connecting commands and queries to API endpoints

Add `Themisquo.AspNetCore` to map a command or query directly onto a minimal API route. Route values are bound onto matching properties before the request body, so a route id always wins over a conflicting body value.

```csharp
app.MapCommand<CreateCardCommand>("/projects/{projectId}/cards");         // POST by default
app.MapCommand<DeleteCardCommand>("/cards/{id}", "DELETE");
app.MapQuery<GetCardQuery, CardDto>("/cards/{cardId}");                   // GET by default
```

If a command raises an event decorated with `[Location]`, the endpoint responds `201 Created` with a `Location` header resolved from that event instead of `200 OK`:

```csharp
[Location("/projects/{ProjectId}/cards/{CardId}")]
public record CardCreatedEvent(Guid ProjectId, Guid CardId) : IEvent
{
    public int Version => 1;
    public DateTime EventTime => DateTime.UtcNow;
    public Guid ProcessId => Guid.NewGuid();
}
```

```csharp
builder.Services.AddThemisquo();
builder.Services.AddThemisquoCommandLocations(); // enables Location header support, call after AddThemisquo()
```

For larger APIs, decorate command/query types with `[Endpoint]` and map them all at once by scanning an assembly:

```csharp
[Endpoint("/projects/{projectId}/cards", "POST")]
public record CreateCardCommand(Guid ProjectId, string Title) : ICommand
{
    public Guid Instance { get; } = Guid.NewGuid();
}

app.MapCQEndpoints(typeof(CreateCardCommand).Assembly, baseUrl: "/api");
```

## Setting up DI and registering handlers

`AddThemisquo()` registers the dispatcher, the default event dispatcher, and the handler method resolver:

```csharp
services.AddThemisquo();
```

Register handlers explicitly, either with the typed helpers or directly against the interfaces:

```csharp
services.AddCommandHandler<CreateCardCommandHandler, CreateCardCommand>();
services.AddQueryHandler<GetCardQueryHandler, GetCardQuery, CardDto>();

// Event observers are registered like any other scoped service:
services.AddScoped<IEventObserver<CardCreatedEvent>, CardCreatedObserver>();
```

Enable caching of the reflection lookups the dispatcher does to find handler methods (useful once you have many handlers):

```csharp
services.AddCachingDispatch();
```

Catch missing registrations at startup instead of at first dispatch, by scanning the assemblies that reference Themisquo for every `ICommand`, `IQuery<T>`, and `IEvent` and verifying a handler/observer is registered for each:

```csharp
var app = builder.Build();
app.Services.ValidateHandlersRegistered(); // throws MissingHandlersException if anything is missing
```

## Using validators

Add `Themisquo.FluentValidation` to validate commands and queries with [FluentValidation](https://docs.fluentvalidation.net/) before they reach a handler.

```csharp
public class CreateCardCommandValidator : AbstractValidator<CreateCardCommand>
{
    public CreateCardCommandValidator()
    {
        RuleFor(c => c.Title).NotEmpty();
    }
}
```

Register the validators found in an assembly and swap in the validating dispatcher, which runs a matching `IValidator<T>` (if one is registered) before delegating to the normal dispatcher:

```csharp
services.AddThemisquo();
services.AddValidatorsFromAssembly(typeof(CreateCardCommandValidator).Assembly);
services.AddValidatingDispatcher(); // must be called after AddThemisquo()
```

A failed validation throws FluentValidation's `ValidationException`. In an ASP.NET Core app, `AddThemisquoExceptionHandling()` maps it (and any other exception you register) to a `ProblemDetails` response automatically:

```csharp
builder.Services.AddThemisquoExceptionHandling(options =>
    options.Map<NotFoundException>(StatusCodes.Status404NotFound));
// ValidationException -> 400 with per-field errors is mapped by default.

app.UseExceptionHandler();
```

## Complying with CQRS/ES using Themisquo

Themisquo enforces the CQRS split structurally rather than by convention:

* `ICommandHandler<TCommand>.Handle(command, eventDispatcher)` is the *only* place in a command's lifecycle that receives an `IEventDispatcher`. It's scoped to that single dispatch and is disposed as soon as the command finishes, so events can't be raised outside of handling a command.
* `IQueryHandler<TQuery, TResult>.Handle(query)` never receives an event dispatcher at all, so a query handler is structurally unable to raise events or mutate state through Themisquo.
* Commands describe intent (`CreateCard`); events describe facts that already happened (`CardCreated`). A command handler raises one or more events via `IEventDispatcher.Dispatch(IEvent)`, and each event is routed to its `IEventObserver<TEvent>` to apply side effects — persisting to an event store, updating a read model, publishing an integration event, etc.
* Because handler resolution goes through your DI container, you decide the actual persistence/event-sourcing strategy; Themisquo only guarantees *when* and *how* handlers are invoked, not *what* they do.
* `ValidateHandlersRegistered()` (see above) lets you assert at startup that every command, query, and event in your assemblies has a corresponding handler/observer registered, so a missing registration is a deployment-time failure instead of a runtime surprise.

## Integrating with MassTransit and RabbitMQ

Themisquo doesn't ship a MassTransit or RabbitMQ package — `IEventDispatcher` and `IEventObserver<TEvent>` are just interfaces, so integrating with a message bus is a matter of implementing them against MassTransit's `IPublishEndpoint`/`IBus`. Two common shapes:

**Publish every event to RabbitMQ via MassTransit**, so other services (or a background consumer) can react to it:

```csharp
public class MassTransitEventDispatcher : IEventDispatcher
{
    private readonly IPublishEndpoint publishEndpoint;

    public MassTransitEventDispatcher(IPublishEndpoint publishEndpoint) =>
        this.publishEndpoint = publishEndpoint;

    public Task Dispatch(IEvent @event) => publishEndpoint.Publish(@event, @event.GetType());
}
```

```csharp
services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) => cfg.ConfigureEndpoints(context));
});

services.AddThemisquo();
services.AddScoped<IEventDispatcher, MassTransitEventDispatcher>(); // replaces the default in-process dispatcher
```

**Or handle an event in-process and publish only the side effect that needs to leave the process**, from an `IEventObserver<TEvent>`:

```csharp
public class CardCreatedObserver : IEventObserver<CardCreatedEvent>
{
    private readonly IPublishEndpoint publishEndpoint;
    private readonly ICardStore store;

    public CardCreatedObserver(IPublishEndpoint publishEndpoint, ICardStore store)
    {
        this.publishEndpoint = publishEndpoint;
        this.store = store;
    }

    public async Task Invoke(CardCreatedEvent @event)
    {
        await store.AppendAsync(@event);                 // persist the event
        await publishEndpoint.Publish(new CardCreatedIntegrationEvent(@event.CardId)); // notify other services
    }
}
```

```csharp
services.AddScoped<IEventObserver<CardCreatedEvent>, CardCreatedObserver>();
```

The first approach fits systems where every domain event is also an integration event; the second keeps the two concerns separate, publishing to RabbitMQ only when a specific event actually needs to leave the process.

## License

[MIT](LICENSE)

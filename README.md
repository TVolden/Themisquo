# Themisquo

![Themisquo](Themisquo.png)

A lightweight core library for Command and Query Responsibility Segregation (CQRS) and Event Sourcing (ES) on .NET. Based on the teachings in [CqrsInPractice](https://github.com/vkhorikov/CqrsInPractice).

With Themisquo you separate commands and queries into plain conceptual definitions (`ICommand`, `IQuery<T>`) and their handlers (`ICommandHandler<T>`, `IQueryHandler<T, TResult>`). A dispatcher resolves the right handler for whatever command or query you give it, so callers never depend on a handler directly. To keep the CQRS boundary honest, only commands are handed a scoped event dispatcher when they're invoked — queries never get one, so they can't sneak in state changes. Handlers use that event dispatcher to raise events describing what changed, which are routed to event observers for side effects (persistence, notifications, integration events, and so on).

## Why Themisquo?

* **Small and dependency-light** — the core package only depends on `Microsoft.Extensions.DependencyInjection.Abstractions`. No MediatR-style megapackage.
* **CQRS enforced by design, not convention** — commands get an `IEventDispatcher`; queries don't. A bundled analyzer (`THQ001`) and a registration-time check also stop query handlers from injecting `IDispatcher`, `IEventDispatcher` or `ICommandHandler<T>`, so a query can't dispatch commands or raise events.
* **Handlers resolved through your DI container** — no reflection-based assembly scanning magic at runtime; you register handlers explicitly (or scan once at startup).
* **Fail fast on missing handlers** — `ValidateHandlersRegistered()` scans your commands, queries, and events at startup and throws before your first request does.
* **First-class ASP.NET Core mapping** — turn a command or query type into a minimal API endpoint in one line, including automatic `201 Created` + `Location` responses driven by the events a command raises.
* **Pluggable validation** — an optional FluentValidation integration validates commands/queries before they ever reach a handler.
* **Bring your own event sourcing/integration story** — `IEventObserver<TEvent>` and `IEventDispatcher` are just interfaces, so you can back them with EF Core, an event store, MassTransit, or anything else.

## Packages

| Package | Purpose |
|---|---|
| `Themisquo` | Core abstractions: `ICommand`, `IQuery<T>`, handlers, dispatcher, event dispatcher/observer, DI registration helpers, and the CQRS analyzers. |
| `Themisquo.AspNetCore` | Maps commands/queries to minimal API endpoints, plus a `ThemisquoExceptionHandler` that turns exceptions into `ProblemDetails`. |
| `Themisquo.AspNetCore.Siren` | Opt-in [Siren](https://github.com/kevinswiber/siren) hypermedia responses for query endpoints. |
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
    public async Task Handle(CreateCardCommand command, IEventDispatcher eventDispatcher, CancellationToken cancellationToken)
    {
        // ... do the work ...
        await eventDispatcher.Dispatch(new CardCreatedEvent(command.ProjectId, Guid.NewGuid()), cancellationToken);
    }
}
```

Define a query and its handler:

```csharp
public record GetCardQuery(Guid CardId) : IQuery<CardDto>;

public class GetCardQueryHandler : IQueryHandler<GetCardQuery, CardDto>
{
    public async Task<CardDto> Handle(GetCardQuery query, CancellationToken cancellationToken)
    {
        // ... load and map ...
        return new CardDto();
    }
}
```

Register everything and dispatch:

```csharp
services.AddThemisquo();
services.AddCommandHandler<CreateCardCommandHandler, CreateCardCommand>();
services.AddQueryHandler<GetCardQueryHandler, GetCardQuery, CardDto>();

// wherever you resolve IDispatcher / IQueryDispatcher from DI:
await dispatcher.Dispatch(new CreateCardCommand(projectId, "New card"), cancellationToken);
var card = await dispatcher.Dispatch(new GetCardQuery(cardId), cancellationToken);
```

`IDispatcher` dispatches both commands and queries; `IQueryDispatcher` dispatches queries only. A query handler that needs to run another query should inject `IQueryDispatcher` — see [Keeping query handlers free of side effects](#keeping-query-handlers-free-of-side-effects).

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

### Hypermedia responses with Siren

Add `Themisquo.AspNetCore.Siren` and opt in with one call. Query endpoints can then respond with a [Siren](https://github.com/kevinswiber/siren) document (`application/vnd.siren+json`) instead of plain JSON. The endpoints themselves don't change.

```csharp
builder.Services.AddThemisquoSiren();                  // Siren when the client sends Accept: application/vnd.siren+json
builder.Services.AddThemisquoSiren(asDefault: true);   // ...and also when Accept is missing, */*, or unsupported
```

The response format follows the request's `Accept` header, in order of its `q` preferences. `application/json` always gets plain JSON. A missing `Accept`, a wildcard, or an unsupported type gets the default: plain JSON, or Siren with `asDefault: true`. Responses carry `Vary: Accept`, so caches keep the formats apart.

A single result becomes the entity's `properties`, serialized with the same JSON options as a plain response. Every entity gets a `self` link to the request:

```json
{
  "class": ["cardDto"],
  "properties": { "cardId": "…", "title": "New card" },
  "links": [{ "rel": ["self"], "href": "/cards/…" }]
}
```

A list result becomes a `collection` with one `item` sub-entity per element. If a GET query endpoint returns a single result of the list's element type, each item gets a `self` link to it. For example, a list of `ICard` mapped next to `app.MapQuery<GetCardQuery, ICard>("/projects/{projectId}/cards/{cardId}")`, where `GetCardQuery` is marked `[Resource(Type = "card")]` (see below):

```json
{
  "class": ["card", "collection"],
  "entities": [
    {
      "rel": ["item"],
      "class": ["card"],
      "properties": { "id": 7, "title": "New card" },
      "links": [{ "rel": ["self"], "href": "/projects/…/cards/7" }]
    }
  ],
  "links": [{ "rel": ["self"], "href": "/projects/…/cards" }]
}
```

Each placeholder in the item route is filled from, in order:
1. the item's property of the same name, so `{projectId}` uses `ProjectId`;
2. the item's primary id, for the route's last placeholder only, so `{cardId}` uses `Id`;
3. the current request's route values, so a list at `/projects/{projectId}/cards` passes its `projectId` on.

An item whose route can't be fully resolved gets no link.

A single result, or an item in a list, also links to the resources it refers to. A property matches another resource when it has the same name as the last placeholder of that resource's single-item GET query route. For example, a card's `ProjectId` matches `/projects/{projectId}`. The link's `rel` is the related resource's type name:

```json
"links": [
  { "rel": ["self"], "href": "/cards/7" },
  { "rel": ["project"], "href": "/projects/…" }
]
```

The related route's other placeholders are filled from the item's properties of the same name, then from the request's route values. Related routes that can't be fully resolved are skipped. Routes that return the item's own type, a list or a scalar aren't linked.

#### Links to nested resources

An entity also links to the GET queries nested directly under its route: those whose route is the entity's route plus one literal segment. The link's `rel` is that segment, and its `class` is the nested resource's. For example, with these endpoints:

```csharp
app.MapQuery<GetProjectQuery, IProject>("/projects/{projectId}");
app.MapQuery<GetProjectCardsQuery, IEnumerable<ICard>>("/projects/{projectId}/cards");
app.MapQuery<GetProjectRulesQuery, IEnumerable<IRule>>("/projects/{projectId}/rules");
app.MapQuery<GetRuleDependenciesQuery, IEnumerable<IRule>>("/projects/{projectId}/rules/dependencies");
```

a project links to its cards and rules:

```json
"links": [
  { "rel": ["self"], "href": "/projects/…" },
  { "rel": ["cards"], "href": "/projects/…/cards", "class": ["card", "collection"] },
  { "rel": ["rules"], "href": "/projects/…/rules", "class": ["rule", "collection"] }
]
```

This applies to:
* a single result;
* each item in a list that has its own route, so every project in `GET /projects` links to its cards;
* a list, so the rules list links to `dependencies`.

Only direct children are linked. A project doesn't link to `rules/dependencies`; the rules list does. A route with another placeholder, such as `/projects/{projectId}/cards/{cardId}`, is an item rather than a nested resource, so it isn't linked this way. Routes match by shape, so placeholder names and constraints don't matter.

To use a different `rel`, put `[Resource(Rel = "…")]` on the nested query.

Commands mapped on the same route as an entity become its Siren `actions`, unless switched off (see [Declaring actions](#declaring-actions)). Routes match by shape, so placeholder names don't matter: `DELETE /cards/{id}` acts on `GET /cards/{cardId}`. This applies to:
* a single result;
* a list, so a create command on the list route appears on the collection;
* each item in a list, at the item's own route.

```csharp
app.MapQuery<GetCardQuery, ICard>("/cards/{cardId}");
app.MapCommand<RenameCardCommand>("/cards/{cardId}", "PUT"); // RenameCardCommand(Guid CardId, string Title)
```

```json
"actions": [
  {
    "name": "renameCardCommand",
    "method": "PUT",
    "href": "/cards/7",
    "type": "application/json",
    "fields": [{ "name": "title", "type": "text" }]
  }
]
```

The action's name is the command type's name in camelCase. To set a different name, or to add a title, put `[Action]` from `Themisquo.AspNetCore` on the command:

```csharp
[Action(Name = "rename", Title = "Rename card")]
public record RenameCardCommand(Guid CardId, string Title) : ICommand { /* ... */ }
```

Its fields are the command properties the request body can set, which leaves out properties bound from the route. Each field's type is an HTML input type:
* `number` for numeric properties;
* `checkbox` for `bool`;
* `date`, `time` or `datetime-local` for dates and times;
* `text` for everything else.

A command without body fields, such as a delete, has no `type` or `fields`.

When a command responds `201 Created` with a `Location` (see `AddThemisquoCommandLocations()` above), Siren dispatches the query mapped on the location's route. The response body is the created resource as a Siren document, the same one a `GET` of the location returns. So the client doesn't need a second request to get the new resource and its actions. The query is bound from the location's route values, the same way commands are bound.

The response falls back to `201 Created` with only the `Location` header when:
* no query route matches the location;
* the query fails, for example because an asynchronously updated read model hasn't caught up yet. A warning is logged;
* the query returns `null`.

A scalar result such as a `string` or `bool` is wrapped as `properties.value`.

By default, the class name is the result type's name in camelCase, so `ICard` becomes `iCard` and `CardDto` becomes `cardDto`. The primary id is the result's `Id` property.

Both can be set with `[Resource]` from `Themisquo.AspNetCore`. It goes on the query, so your domain types stay free of web dependencies. It isn't Siren-specific either: other hypermedia formats can use the same names and ids.

```csharp
[Endpoint("/projects/{projectId}/cards/{cardId}")]
[Resource(Type = "card", Id = nameof(ICard.CardId))]
public record GetCardQuery(Guid ProjectId, Guid CardId) : IQuery<ICard>;
```

* **`Type`** names the result, or the elements of a list query. A list query without it takes the name from a single-item query for the same type, so annotating `GetCardQuery` is enough.
* **`Id`** names the result property that fills the last placeholder of the query's route when an item links to it. Here `{cardId}` comes from `ICard.CardId`.

#### Declaring actions

Automatic actions only come from commands on the entity's own route. For example, the cards in a collection get the card's own `update` and `delete`, but not "remove from this collection", which lives on another route. You can declare actions on the query instead. Each form comes in two versions:
* **`Resource…`** targets the entity the query returns. For a list query, that's the collection.
* **`Item…`** targets each item of a list query.

| | Entity (`Resource…`) | Items of a list (`Item…`) |
|---|---|---|
| A mapped command | `[ResourceAction<TCommand>]` | `[ItemAction<TCommand>]` |
| An external URL | `[ResourceAction("name", "METHOD", "url template")]` | `[ItemAction("name", "METHOD", "url template")]` |
| Commands by context | `[Resource(Context = "…")]` | `[Resource(ItemContext = "…")]` |
| Switch off automatic actions | `[Resource(AutoActions = false)]` | `[Resource(AutoItemActions = false)]` |

```csharp
[Endpoint("/projects/{projectId}/collections/{collectionId}/cards")]
[Resource(AutoItemActions = false)]                       // no update/delete of the card itself here
[ItemAction<RemoveCardFromCollectionCommand>]
[ItemAction("print", "POST", "https://print.example/cards/{cardId}", Title = "Print card")]
public record GetCollectionCards(Guid ProjectId, Guid CollectionId) : IQuery<IEnumerable<ICard>>;
```

* **A mapped command** uses its endpoint's route and method. Its name, title and fields come from the command, as for automatic actions. A `Title` on the declaration replaces the command's title.
* **An external action** can point to another service. A relative URL gets the request's path base in front; an absolute `http(s)` URL is used as is. Its optional `Fields` are sent as `text`.
* **A context** adds every mapped command marked with a matching `[Action(Context = "…")]`.

The automatic actions come first, then the declared commands, the commands by context, and the external actions. A command appears once. With the automatic actions switched off and nothing declared, the entity or items get no actions.

Placeholders are filled the same way as for links: from the entity's or item's property of the same name, then its primary id for the last placeholder only, then the request's route values. An action that can't be fully resolved is left out.

Check the declarations at startup, after mapping the endpoints:

```csharp
app.MapCQEndpoints(typeof(GetCollectionCards).Assembly);
app.ValidateResourceActions(); // throws InvalidResourceActionException
```

It throws when:
* a declared command isn't mapped as an endpoint, or sets `Fields`;
* a `Context` or `ItemContext` matches no mapped command;
* an `Item…` attribute, `ItemContext` or `AutoItemActions` is on a query that doesn't return a list;
* an external action's method isn't an HTTP method.

#### Declaring links

Links work the same way as actions: automatic by default, declarable, and switchable off.

| | Entity (`Resource…`) | Items of a list (`Item…`) |
|---|---|---|
| A mapped GET query | `[ResourceLink<TQuery>(Rel = "…")]` | `[ItemLink<TQuery>(Rel = "…")]` |
| An external URL | `[ResourceLink("rel", "url template")]` | `[ItemLink("rel", "url template")]` |
| Switch off automatic links | `[Resource(AutoLinks = false)]` | `[Resource(AutoItemLinks = false)]` |

```csharp
[Endpoint("/projects/{projectId}/cards/{cardId}")]
[ResourceLink<GetProjectTemplatesQuery>(Rel = "templates")]
[ResourceLink("preview", "https://preview.example/cards/{cardId}")]
public record GetCardQuery(Guid ProjectId, Guid CardId) : IQuery<ICard>;
```

* **A mapped query** is linked at its route, filled like a declared command's. Its `rel` defaults to the query's `[Resource(Rel)]`, else its resource type name.
* **An external link** is filled like an external action: a relative URL gets the request's path base in front, and an absolute `http(s)` URL is used as is.
* **Switching off** removes all automatic links except `self`: the links to referenced resources and to nested resources.

The declared links come after the automatic ones. A link with the same `rel` and `href` appears once, and a link that can't be fully resolved is left out.

Check the declarations at startup, after mapping the endpoints:

```csharp
app.ValidateResourceLinks(); // throws InvalidResourceLinkException
```

It throws when:
* a linked query isn't mapped as a GET query endpoint;
* an `ItemLink…` attribute or `AutoItemLinks` is on a query that doesn't return a list.

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

The same call also checks every resolved query handler for forbidden dependencies and throws `QueryHandlerDependencyException` if it finds any. This covers query handlers registered directly with `services.AddScoped<IQueryHandler<…>, …>()`, which bypass the check in `AddQueryHandler`.

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

* `ICommandHandler<TCommand>.Handle(command, eventDispatcher, cancellationToken)` is the *only* place in a command's lifecycle that receives an `IEventDispatcher`. It's scoped to that single dispatch and is disposed as soon as the command finishes, so events can't be raised outside of handling a command.
* `IQueryHandler<TQuery, TResult>.Handle(query, cancellationToken)` never receives an event dispatcher at all, and query handlers aren't allowed to inject one (or a command dispatcher) either. See below.
* Commands describe intent (`CreateCard`); events describe facts that already happened (`CardCreated`). A command handler raises one or more events via `IEventDispatcher.Dispatch(IEvent, CancellationToken)`, and each event is routed to its `IEventObserver<TEvent>` to apply side effects — persisting to an event store, updating a read model, publishing an integration event, etc.
* Because handler resolution goes through your DI container, you decide the actual persistence/event-sourcing strategy; Themisquo only guarantees *when* and *how* handlers are invoked, not *what* they do.
* `ValidateHandlersRegistered()` (see above) lets you assert at startup that every command, query, and event in your assemblies has a corresponding handler/observer registered, so a missing registration is a deployment-time failure instead of a runtime surprise.

### Keeping query handlers free of side effects

A query handler that could inject `IDispatcher` could dispatch commands, which breaches CQRS. Themisquo blocks constructor dependencies on any of the following in a query handler:

* `IDispatcher`, or anything that implements it (such as `Dispatcher`)
* `IEventDispatcher`
* `ICommandHandler<T>`

These are also blocked when wrapped in another generic type, like `Lazy<IDispatcher>`, `Func<IDispatcher>` or `IEnumerable<ICommandHandler<T>>`. Inject `IQueryDispatcher` instead if a query handler needs to run other queries:

```csharp
public class GetProjectSummaryQueryHandler : IQueryHandler<GetProjectSummaryQuery, ProjectSummaryDto>
{
    private readonly IQueryDispatcher dispatcher; // OK: queries only
    // private readonly IDispatcher dispatcher;    // THQ001: could dispatch commands

    public GetProjectSummaryQueryHandler(IQueryDispatcher dispatcher) =>
        this.dispatcher = dispatcher;

    // ...
}
```

The rule is enforced in three places:

| When | How | Result |
|---|---|---|
| Compile time | The `THQ001` analyzer, shipped inside the `Themisquo` package | Build error on the offending constructor parameter |
| Registration | `AddQueryHandler<THandler, TQuery, TResult>()` | Throws `QueryHandlerDependencyException` |
| Startup | `ValidateHandlersRegistered()` | Throws `QueryHandlerDependencyException` |

The `IQueryDispatcher` resolved from DI is a query-only wrapper (`QueryOnlyDispatcher`), so it can't be cast back to `IDispatcher` to get around the rule.

## Cancellation

Every `Handle`/`Invoke`/`Dispatch` method on the abstractions above takes a required `CancellationToken` — there's no overload without one, so a handler can't quietly ignore cancellation by omission.

Themisquo itself never cancels anything on your behalf: it just plumbs the token through so *you* can decide where honoring it is safe. That distinction matters most for `IEventObserver<TEvent>.Invoke`, since observers are typically the place where an event actually gets persisted (an event store, a read model, an outbox row). Aborting that write partway through can leave your data in a state Themisquo can't help you recover from. As a rule of thumb:

* In a command handler, it's fine to check the token (or let an early `await` observe it) *before* you start committing anything — a request that was already cancelled shouldn't do work at all.
* Once you're inside an observer's actual commit, don't let cancellation cut it short. Either don't check the token there, or explicitly pass `CancellationToken.None` to the persistence call itself, even though the method received a live token — you can still forward the live token to unrelated, safely-abortable work (e.g. an outbound HTTP call) in the same method.

## Integrating with MassTransit and RabbitMQ

Themisquo doesn't ship a MassTransit or RabbitMQ package — `IEventDispatcher` and `IEventObserver<TEvent>` are just interfaces, so integrating with a message bus is a matter of implementing them against MassTransit's `IPublishEndpoint`/`IBus`. Two common shapes:

**Publish every event to RabbitMQ via MassTransit**, so other services (or a background consumer) can react to it:

```csharp
public class MassTransitEventDispatcher : IEventDispatcher
{
    private readonly IPublishEndpoint publishEndpoint;

    public MassTransitEventDispatcher(IPublishEndpoint publishEndpoint) =>
        this.publishEndpoint = publishEndpoint;

    public Task Dispatch(IEvent @event, CancellationToken cancellationToken) =>
        publishEndpoint.Publish(@event, @event.GetType(), cancellationToken);
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

    public async Task Invoke(CardCreatedEvent @event, CancellationToken cancellationToken)
    {
        // The append must not be left half-done, so it always runs to completion, even if the
        // caller's token is already cancelled by the time we get here.
        await store.AppendAsync(@event, CancellationToken.None);
        // Publishing the integration event is safe to abort, so the live token is fine here.
        await publishEndpoint.Publish(new CardCreatedIntegrationEvent(@event.CardId), cancellationToken);
    }
}
```

```csharp
services.AddScoped<IEventObserver<CardCreatedEvent>, CardCreatedObserver>();
```

The first approach fits systems where every domain event is also an integration event; the second keeps the two concerns separate, publishing to RabbitMQ only when a specific event actually needs to leave the process.

## License

[MIT](LICENSE)

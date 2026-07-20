# Themisquo
A core library for Command and Query Responsibility Segregation (CQRS) and Event Sourcing (ES). Based on the teachings in https://github.com/vkhorikov/CqrsInPractice

This project includes (is limited to):
* Structure interfaces for commands and queries.
* Message dispatcher resolving handlers for commands and queries.
* Structure interface for events and event observers.
* Scope limited event dispatcher for commands.
* Helper methods to register handlers.
* Helper methods to wire web endpoints with command/queries.
* Helper method to check if all commands/queries/events has handlers (to avoid runtime problems).
* Supports abstract validation of queries/commands before parsing it to a handler.

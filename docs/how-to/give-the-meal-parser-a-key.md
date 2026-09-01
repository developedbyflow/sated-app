# Give the meal parser a key

> Email has the same shape and the same store: `Email:ApiKey`, plus `App:BaseUrl` for the links.
> No provider is written yet, so setting `Email:ApiKey` today stops the API from starting, on
> purpose.

`POST /api/meals/parse` answers `503` until an OpenAI key is configured. Nothing else in Sated
needs one, and nothing else stops working without it.

## Where the key goes

**Not in `appsettings.json` or `appsettings.Development.json`.** Both are committed to this
repository, which is public. The database password is in there because it is a local development
value that everybody running this repository uses; an API key is the opposite of that.

It goes in the **user secrets** store instead — a JSON file that .NET keeps outside the repository,
per machine and per user, and reads automatically when the environment is Development. There is
nothing to add to `.gitignore`, because there is nothing inside the repository to ignore.

```bash
dotnet user-secrets set "OpenAi:ApiKey" "sk-…" --project server/Sated.Api
```

The project already carries a `UserSecretsId`, which is the folder name the store uses. On macOS
the file lands in `~/.microsoft/usersecrets/<id>/secrets.json`.

To see what is stored, without printing it into a shared terminal:

```bash
dotnet user-secrets list --project server/Sated.Api
```

## What else can be set

| Key | Default | What it does |
|---|---|---|
| `OpenAi:ApiKey` | none | Without it the parser is the one that answers nothing, and the endpoint is a `503` |
| `OpenAi:Model` | `gpt-5.6-luna` | The model the request names |
| `OpenAi:TimeoutSeconds` | `20` | How long the SDK waits before giving up, per call |

Only the key is a secret. The other two can live in `appsettings.json` if you ever want them
committed.

## In production

User secrets are a development convenience and are **not read outside Development**. A deployed
Sated reads the same configuration key from an environment variable instead:

```
OpenAi__ApiKey=sk-…
```

The double underscore is how .NET writes a nested configuration key in an environment variable —
`OpenAi__ApiKey` is `OpenAi:ApiKey`.

## Checking that it works

Restart the API and post a sentence:

```bash
curl -k -X POST https://localhost:7245/api/meals/parse -H "Content-Type: application/json" -d '{"text":"two eggs and a glass of milk"}'
```

A `503` after setting the key means the key was not read: check the project the secret was set
against, and that the environment is Development.

**Then read the log line.** Every call writes one:

```
Meal parsed by gpt-5.6-luna: 21132 in, 0 of them cached, 96 out
```

`0 of them cached` on the **first** call is expected. On the second call with the same catalogue it
should be most of the input — the catalogue is around 20 000 tokens and caching needs 1 024. If it
stays 0, the prompt prefix is not identical between calls, and the first place to look is the order
the catalogue is written in ([0023](../decisions/0023-a-parsed-meal-is-a-proposal-nobody-saved.md)).

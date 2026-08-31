---
title: 0008 — Keep the session in an HttpOnly cookie
---

# 0008 — Keep the session in an HttpOnly cookie, not a bearer token

**Status:** accepted
**Date:** 2026-08-31

## Context

Epic 2 needs accounts, and every story after it needs to know who is asking. HTTP carries nothing
between requests, so the server has to hand the browser something at sign-in that comes back on
every later call. There are two shapes for that thing, and they are not interchangeable once a
frontend is written against one of them.

Four facts about this application decide it, and none of them is about which shape is better in
general:

- **The only client is a React app on the same site**, served from `client/`. There is no mobile
  app and no third-party consumer of this API, and none is in the MVP scope.
- **That React app will carry dozens of npm dependencies.** Any one of them, or any unescaped
  field rendered from the catalogue, is a way for someone else's JavaScript to run on the page.
- **Story 2.3 requires that deleting an account signs the user out immediately.** Something has to
  be able to invalidate credentials that are already issued.
- **ASP.NET Core Identity ships both.** `MapIdentityApi` issues a bearer token by default and a
  cookie when `/login` is called with `useCookies=true`; a hand-written controller picks the scheme
  through `SignInManager`. Neither option is extra work on the server.

## Decision

Sign-in issues the **ASP.NET Core Identity application cookie**, marked `HttpOnly`, `Secure` and
`SameSite=Lax`. The React client sends nothing by hand; the browser attaches the cookie. No bearer
token is issued and no token endpoint is exposed.

## Alternatives considered

**A JWT in `localStorage`.** The common shape, and the one asked about in interviews, which is a
real consideration given that `CLAUDE.md §1` names career preparation as a goal of this codebase.

It lost on the second fact above. A token in `localStorage` is readable by any script running on
the page, so a single cross-site scripting bug hands over a credential that works from anywhere
until it expires. An `HttpOnly` cookie cannot be read by script at all; the same bug can act while
the page is open but cannot carry anything away. The two advantages a bearer token has — non-browser
clients and cross-domain use — are not advantages here, because neither exists.

Revocation compounds it. A JWT is valid until it expires, and the standard fix is a server-side
list of tokens that are no longer accepted — which is the server-side state the token was chosen to
avoid, arrived at by a longer road. Story 2.3 makes this a requirement, not a preference.

**A JWT delivered in an `HttpOnly` cookie.** This gets the storage property right and keeps the
token format. Rejected as pure cost: the format buys nothing once the client is a browser on the
same site, and it means writing signing, expiry and refresh by hand instead of using what Identity
already configures.

## Consequences

- **CSRF is now a live concern**, because the browser attaches the cookie without being asked.
  `SameSite=Lax` is the default in ASP.NET Core and covers the ordinary case, but it does not cover
  a state-changing endpoint reached by a top-level navigation. No endpoint that changes data may be
  a `GET`.
- **The dev setup needs CORS.** The React dev server runs on a different port, so the browser treats
  it as a different origin: the API must name that origin and allow credentials, and the client must
  send `credentials: 'include'`. Browsers reject `AllowAnyOrigin` together with credentials, so the
  origin has to be listed. Serving the built client from the API's own origin removes this entirely.
- **The cookie is self-contained, not a session row.** It carries the user's claims, encrypted.
  Signing out clears it in that browser, but a copy taken elsewhere stays valid until it expires
  unless the security stamp is used. `SecurityStampValidationInterval` decides how fast a stamp
  change takes effect; it defaults to 30 minutes, and Story 2.3 requires setting it lower.
- **Adding a mobile client later means running two schemes**, cookie and bearer, in one application.
  That is the point at which this decision would be revisited, and it is a real cost, not a
  theoretical one.
- Identity's cookie handler points an unauthenticated request at a login page that does not exist
  in an API. **Measured on .NET 10**, including with `Accept: text/html`: the status is already
  `401`, but the response carries a `Location` header naming `/Account/Login`. The redirect event
  is overridden so that header is not sent, because a client that follows it reaches a 404 instead
  of reading the auth error. Older ASP.NET Core versions answered `302` here, which is what most
  guidance still describes.

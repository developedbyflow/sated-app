---
title: 0010 — Ask for the password before export and deletion
---

# 0010 — Ask for the password before export and deletion

**Status:** accepted
**Date:** 2026-08-31

## Context

FR-29 gives the account holder two rights: take all their data, and destroy it. Story 2.3 turns
those into two endpoints. Both are reached with nothing but the session cookie, which is what every
other endpoint needs, and both are worse to lose than anything else the API does.

The two are not equally dangerous in the same way. Deletion **destroys** — the harm is loud, and the
victim finds out immediately. Export **exfiltrates** — one request returns the weight, the food log,
and every consent text in a single document, and the account holder never learns it happened. On the
theft path, export is the larger loss.

[0008](0008-keep-the-session-in-an-httponly-cookie.md) already removed the cheap ways to steal a
session: the cookie is `HttpOnly`, so an XSS cannot read it, and `SameSite=Lax`, so a cross-site
`DELETE` never carries it. What remains is a cookie taken off the machine itself — a borrowed
laptop, an unlocked screen, a stolen backup.

Identity offers two ways to check a password. `UserManager.CheckPasswordAsync` compares the hash and
nothing else: no counter, no lockout. `SignInManager.CheckPasswordSignInAsync` runs the same
comparison through the lockout the login path already uses. Neither issues a cookie.

## Decision

Both endpoints require the account password in the request body, checked with
`CheckPasswordSignInAsync(..., lockoutOnFailure: true)`. A failure answers `403`.

Export is therefore `POST /api/account/export`, not a `GET` — a `GET` has nowhere to put a password.

## Alternatives considered

**Nothing beyond the session.** What the acceptance criteria literally ask for. Rejected: it makes a
stolen cookie equal to a stolen identity for the two actions where that costs most. Google Takeout
and Facebook's archive download both re-ask for the password at exactly this point.

**Type your email address to confirm**, the way GitHub guards repository deletion. Rejected as
security, kept as a UI idea: anyone holding the session can read the address from `/api/auth/me`. It
prevents the accident, not the attack, and it must not be mistaken for the guard.

**A confirmation link sent by email.** Strictly stronger — it defeats a stolen password as well as a
stolen cookie, because it moves the proof into a second mailbox. Not available: there is no sending
service, domain, or SPF/DKIM/DMARC yet. That is Story 2.4, and it is the upgrade path.

**A grace period with a soft delete**, the way Google and Apple hold an account for thirty days.
Rejected on two counts. FR-29 says complete and irreversible. And it inverts the privacy trade: to
protect against a malicious deletion, it keeps health data that the owner has explicitly asked to be
rid of, for a month.

**`CheckPasswordAsync` instead of the sign-in variant.** Rejected once the hole was named: it has no
counter, so a stolen session could guess the password against these endpoints indefinitely, while
the login endpoint next door locks after five.

## Consequences

**A wrong password locks the account after five tries**, for both endpoints, sharing the counter with
login. That is deliberate, and it carries the same cost the login path already accepted: someone
holding a session can lock the owner out for five minutes on purpose. The alternative — an
uncounted guessing endpoint — is worse.

**`POST /api/account/export` is not a resource fetch and does not read like one.** The guard bought
the verb. `DELETE /api/account` keeps its verb and carries a body, which HTTP allows and `fetch`,
`curl` and `.http` files all send; the cost is that .NET's `HttpClient.DeleteAsync` has no body
overload, so the tests build an `HttpRequestMessage` by hand.

**Both answer `403` whether the password was wrong or the account was locked.** Consistent with the
addendum §3.3 decision on login, and for the same reason: a distinct answer would tell an attacker
which accounts they have already locked.

**A password prompt sits in front of a GDPR right.** Article 15 asks for access without undue
obstacle. Re-typing a password one already knows is not an obstacle in the sense the article means,
but if that ever gets challenged, the fix is to drop the guard from export and keep it on deletion —
not to weaken both.

**Deletion cannot be undone, and support cannot undo it either.** There is no copy anywhere. That is
what FR-29 asks for, and it should be said plainly on the screen before the button.

**The export is a snapshot with no history**, because nothing in Sated keeps history yet. Weight is a
value, not a series ([0009](0009-consent-is-a-document-and-a-signature.md)). When meals and
user-owned foods arrive, they are added to `AccountExportDto` and removed inside `Accounts.Delete`,
which is why deletion goes through `SatedDbContext` rather than `UserManager.DeleteAsync` — one
context, one `SaveChangesAsync`, one transaction, no window where half an account is gone.

**Open, not blocking:** the export cannot say when the account was created, because `IdentityUser`
has no such column and Sated never added one. Adding it is a column and a migration; it has not been
decided.

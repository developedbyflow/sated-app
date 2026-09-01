---
title: 0025 — A reset link is single use, and says nothing about who has an account
---

# 0025 — A reset link is single use, and says nothing about who has an account

**Status:** accepted
**Date:** 2026-09-01

## Context

FR-33 is a requirement the PRD never had; it was added on 2026-08-31 in the account-security
addendum, along with the reason the two halves ship together: **password reset without a verified
address is a way to take over an account.** Whoever typed somebody else's address at registration
receives the reset link.

The addendum is also blunt about the cheap version: *a sender that writes the link to the console
verifies nothing. It adds a step that can be walked past, so it gives the appearance of a
protection without the protection.*

This record covers the code. The provider account and the domain records (SPF, DKIM, DMARC) are
the other half and are not code.

## Decision

```
POST /api/auth/forgot-password   →  202, always
POST /api/auth/reset-password    →  204, or 400 for a link that is spent, stale or altered
POST /api/auth/confirm-email     →  204, or 400
```

**`forgot-password` answers `202` whether or not the address has an account.** Any other answer
turns the form into a directory: type an address, read the status, learn who is a member. Nothing
is sent to an address without an account, and the caller cannot tell the difference.

**The link carries `userId` and `token`, never the email address.** A query string travels into
server logs, browser history and referrer headers. The user id is opaque and already public to the
person holding it.

**A reset link works once**, and that is Identity's doing rather than ours: changing the password
moves the account's security stamp, and the token was signed against the old one. The second use
fails validation. Written down because the mechanism is invisible in the code — there is no line
that marks a token as used, and a reader looking for one will not find it.

**A confirmation link stays usable until it expires.** Confirming an address that is already
confirmed changes nothing, so single use would be ceremony. Tested as it is rather than as it
might be — the first version of that test asserted a second confirmation fails, and it does not.

**Both links live two hours.** One lifespan for both, because Identity's default provider carries
one. Two hours is short for a confirmation email somebody opens in the evening — the reason that
is tolerable is that **an unconfirmed account works completely**, and asking for a password reset
confirms the address on its way through. If it becomes a nuisance, a second token provider with
its own lifespan is the fix.

**Resetting a password confirms the address.** It is proof of delivery: the link arrived, and only
the mailbox owner could have used it. This is what the acceptance criterion asks for accounts that
existed before FR-33 — they get confirmed the first time they are recovered, with no migration.

**Five wrong passwords warn the owner once, and the login screen still says nothing.** The
lockout was already configured; what it lacked was the half that tells the truth to somebody
entitled to it. The warning is sent on the attempt that causes the lockout and not on the ones
after it, so a script cannot turn the account into a mail cannon.

**The warning says nobody got in.** An attack that failed, described as one that succeeded, sends
somebody to change a password for nothing and teaches them to ignore the next message.

**`forgot-password`, `reset-password` and `confirm-email` are limited to three a minute per
address block**, the same shape as the login limiter. They are the only endpoints in Sated that
cause mail to leave.

**Outside Development, the API refuses to start without a provider.** Not a warning at startup and
not a `503` on the endpoint: those leave a deployed Sated where accounts can be created and never
recovered, which is the state FR-33 exists to end. In Development the message goes to the log,
which is how the link gets read while the screens are being built — a way to see the message, not
a way to deliver it.

## Alternatives considered

**Answer `404` on `forgot-password` for an unknown address.** Friendlier, and it publishes the
membership list.

**Boot in Production without a provider, and answer `503`.** It is what the meal parser does
([0024](0024-the-provider-is-chosen-by-a-key.md)), and the difference is what the failure costs.
A sentence that cannot be parsed falls back to search. An account that cannot be recovered is
lost. **This is one line to relax** if a deployment ever has to go out before the domain records
do.

**Block sign-in until the address is confirmed.** The addendum rules it out: confirmation is for
recovery, not for access. A person who cannot receive mail can still use everything.

**Put the email address in the link instead of the user id.** One fewer lookup, and the address
ends up in every log the link passes through.

## Consequences

**Nothing has been sent yet.** `LoggingEmailSender` writes the whole message to the API log, and
that is all any of this has done. The provider, the domain and the records that keep the mail out
of spam are the second half.

**The links point at screens that do not exist.** `App:BaseUrl` defaults to `http://localhost:3000`
and the two routes — `/reset-password` and `/confirm-email` — are for `client/`, which is empty.
The API takes `userId`, `token` and the new password in a request body, so the screens only have to
read two query parameters and post them.

**Nothing sends a second confirmation link.** Somebody who lets the first one expire confirms their
address by resetting their password instead. A dedicated "send it again" endpoint is another way in
for mail abuse, and there is no screen asking for it yet.

# `.template.config/template.json` — rename design

This documents the rename mechanism encoded in `.template.config/template.json`:
the routing rule, the closed per-follower enumeration for each token family, and
the per-bucket rationale. It lives here rather than as comments inside
`template.json` itself because **JSON has no comment syntax** — there is nowhere
in that file to put this. `templates/smoke-test.sh` and `CLAUDE.md`'s template
subsection both point here instead of repeating it; read this file before adding
or changing a symbol in `template.json`.

Background: `appointme` (the brand token) is a strict prefix of `appointment`
(the domain word — the aggregate, the `/appointments` route, the
`appointments.statistics:view` permission). Any rename mechanism has to touch
every brand occurrence without touching any domain occurrence. That constraint
is what both sections below are working around.

## PascalCase brand token: where each generated form comes from

`template.json` drives the PascalCase rename with three symbols, all matching
the same literal search text `"AppointMe"` (case-sensitive), disambiguated
purely by the single character that immediately follows the match (`onlyIf:
[{"before": X}]`) — never by a bare, ungated `"AppointMe"` catch-all, since two
symbols both matching that shorter text would leave replacement order
unspecified (the same trap `sourceName`'s own derived lowercase form sits in
for `"appointments"`).

| Followers | Symbol | Produces |
|---|---|---|
| `.` `" "` `"` `'` `\n` `\r` `;` `-` `<` | `identityBucket` | dotted, as typed: namespaces, paths, prose, connection strings. Also the only symbol with `fileRename` — every path in this repo is `AppointMe.Xyz` (dot-followed), and `fileRename` does not honor `onlyIf` the way `replaces` does, so a second symbol declaring `fileRename` would silently race it. |
| `_` | `safeBucketUnderscore` | `dot_to_underscore`. Exactly one case: `Program.cs`'s `AddProject<AppointMe_Api>` must match the type name .NET Aspire's own SDK source-generates from the renamed `.csproj` file name (dots → underscores). |
| `S` `M` `A` `H` `O` `D` `E` `J` `1` | `safeBucketCompact` | invalid characters deleted, not substituted. `AppointMeSql` is simultaneously a real C# identifier (`ConnectionStrings.cs`, needs underscore-or-delete) and an Aspire resource-name string validated by ASPIRE006, which explicitly rejects underscores — delete is the only form valid in both roles for the identical search text. |

This is a **closed enumeration** over these 19 followers (9 + 1 + 9 above),
independently verified against every file that reaches `$GEN_DIR` (git-tracked,
minus withheld paths, minus `template.json`'s own excludes) — not a sample,
except `\r`: on the LF checkout this repo and CI both use, `\r` has zero actual
occurrences. It is a defensive hedge for a `core.autocrlf=true` checkout, where
the one occurrence that currently sits right before a bare `\n`
(`templates/overlay/README.md`'s `"# AppointMe"` heading — the generated
project's own README, not this repo's withheld one) would have its follower
become `\r` instead of `\n`, falling out of every bucket. Every other follower
listed above was independently measured, not guessed.

Adding a new `AppointMe`-prefixed C# identifier with a follower character
outside this list (e.g. `AddAppointMeBilling`, follower `"B"`) falls out of
every bucket: the "no residual AppointMe" check in `templates/smoke-test.sh`
still catches it (loudly, in CI), but whoever adds it then has to add a
`{ "before": "B" }` entry to `safeBucketCompact` (or `identityBucket`, if it's a
non-identifier position) in `template.json` themselves. There is no way to make
this self-updating; this file is the only record of the rule.

## Lowercase brand token: the second, independent token family

`"appointme"` (lowercase) is a **prefix** of `"appointment"` — a bare
`"appointme"` match would also rewrite the `Appointment` aggregate, the
`/appointments` route, and `appointments.statistics:view`. That collision is
not, by itself, why this family avoids a bare-token-plus-`onlyIf` approach the
way the PascalCase family above does: `onlyIf` is exactly what guards
`identityBucket` and friends against the equivalent risk one section up, and
the same follower-gating would work just as well here for the 13 lowercase
symbols below that carry no `fileRename`.

The actual reason all 15 lowercase symbols bake their guard character directly
into the search text instead (`"appointme-"`, `"appointme."`, ...): two of
them — `tokDot` and `tokDash` — carry `fileRename`, and `fileRename` ignores
`onlyIf` entirely, the same fileRename-ignores-`onlyIf` fact that limits
`identityBucket` to being the *only* PascalCase symbol allowed to declare it
(see the table above). Those two symbols have no `onlyIf` to lean on and must
guard via search text or not at all. Once the family needs that baked-in form
for two of its members, the other 13 use it too, so the whole family shares one
guarding mechanism rather than mixing onlyIf-gated bare tokens with baked-in-text
tokens within the same family. There is no bare `"appointme"` catch-all
anywhere in this family, gated or not.

Three base value symbols, then one `generated`/`join` symbol per follower
character that joins the base value with that literal follower (consuming and
immediately re-emitting it, so the character itself is unchanged):

**`lowerDotted`** (`lowerCaseInvariant`) → case-folded, separators preserved
exactly as typed. Followers: `.` `'` `"` `/` `` ` `` `;` (`tokDot`, `tokApos`,
`tokQuot`, `tokSlash`, `tokTick`, `tokSemi`). `tokDot` is also the only symbol
in this bucket with `fileRename` (the same fileRename-ignores-`onlyIf` reason as
`identityBucket` above). This bucket is **not** a free stylistic choice:
`SuperAdminRegistryTests.should_match_email_case_insensitively` compares this
rename's allowlist literal (`"demo@appointme.dev"`) against the PascalCase
family's identity-preserving rename of `"Demo@AppointMe.DEV"` — both must fold
to the same string, which only holds if this bucket preserves the user's own
separators (e.g. the dot in `"Contoso.Booking"`) exactly as `identityBucket`
does, rather than normalizing them to hyphens. The same value is also what
keeps `'@/api/appointme'` import specifiers (`tokApos`) resolving to the file
`tokDot` renames, and what keeps `main.bicep`'s apostrophe-quoted values in
sync with `main.json`'s double-quoted compiled equivalents (`tokQuot`) and with
`appointme-realm.json`'s own realm name and URL-path segments (`tokQuot`,
`tokSlash`).

**`lowerKebab`** (`kebabCase`) → hyphen-normalized (dots/underscores become
hyphens). Followers: `-` `\n` `\r` (`tokDash`, `tokNewline`, `tokCR`). Required,
not stylistic: `tokDash` covers the three Aspire/Keycloak resource-name strings
(`"appointme-sql"`, `"appointme-api"`, `"appointme-frontend"`) that ASPIRE006
restricts to ASCII letters, digits and hyphens (no dots) — the same constraint
that drove `safeBucketCompact`'s "delete" transform above, just hyphen-safe
instead of hyphen-free since these are strings, not C# identifiers. `tokNewline`
covers `compose.yaml`'s `name: appointme` Docker Compose project name, which
Compose validates as lowercase alphanumeric plus hyphen/underscore — no dots
allowed. (The root `README.md` also has two shell-example occurrences with the
same "followed by end-of-line" shape, but `README.md` is pack-excluded — see
`templates/AppointMe.Templates.csproj` — so they never reach the reachable set
this enumeration is measured over.) `tokCR` is the same `core.autocrlf=true`
defensive hedge as `identityBucket`'s own `\r` follower above, extended to this
family for symmetry: on such a checkout, `tokNewline`'s one reachable
occurrence — `compose.yaml`'s Compose project name — would have its follower
become `\r` and fall out of every lowercase bucket without `tokCR`. Like
`identityBucket`'s `\r`, it has zero actual occurrences on the LF checkout this
repo and CI both use; it exists for the checkout that isn't this one. `tokDash`
is also the only lowercase symbol with `fileRename` (renames
`appointme-realm.json`).

**`lowerCompact`** (`compactSafeNameLower`: `lowerCaseInvariant` then delete
every non-alphanumeric character) → Followers: `A` `:` `$` `{` `d` `_` (`tokA`,
`tokColon`, `tokDollar`, `tokBrace`, `tokD`, `tokUnder`). Each follower here
sits in a context stricter than either bucket above allows: `tokA` is
`Program.cs`'s `var appointmeApi = ...` C# local (a literal hyphen or dot
mid-identifier would not compile); `tokColon` is `orval.config.ts`'s unquoted
object key `appointme: { ... }` (a hyphen there is a JS syntax error — caught
for real by `npm run lint`); `tokDollar` and `tokBrace` are `main.bicep`'s /
`main.json`'s ACR and storage-account name construction
(`"acrappointme${...}"` / `"acrappointme{0}{1}"`), which Azure restricts to
lowercase alphanumeric only (no hyphens, no dots); `tokD` is the illustrative
`"acrappointmedevtest.azurecr.io"` description string for that same ACR name,
kept consistent with it rather than given its own style; `tokUnder` is
`main.bicepparam`'s `appointme_admin` SQL admin login example.

This is a **closed enumeration** over these 15 followers (6 + 3 + 6 above),
independently measured with a byte-level scan (not `grep`, which — see the
residual-check comments in `templates/smoke-test.sh` — silently strips
line-terminating newlines before matching and would have missed the `\n`
follower entirely) over the reachable file set (every git-tracked file minus
`is_withheld()`'s paths minus `template.json`'s own `modifiers.exclude` globs
minus `.template.config/`), not a sample: **203 non-domain occurrences total**
on the LF checkout this repo and CI both use (`\r` contributes zero of them
here — see `tokCR` above), zero left over once all 15 followers are routed.

Adding a new lowercase `"appointme"`-prefixed string with a follower character
outside this list falls out of every bucket: the residual check in
`templates/smoke-test.sh` still catches it (loudly, in CI), but whoever adds it
then has to add a new `tok*` symbol to `template.json` themselves, choosing
whichever of the three base values above (or a new one) is valid for that
occurrence's own context. There is no way to make this self-updating; this file
is the only record of the rule.

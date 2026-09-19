# Code Signing Policy

This document describes how Vehistra releases are built, reviewed and signed.
It is written in English because it primarily addresses the reviewers of the
[SignPath Foundation](https://signpath.org/), whose free code signing programme
for open-source projects Vehistra participates in. The project itself and its
end-user documentation are in German.

## Project

| | |
| --- | --- |
| Product | Vehistra – Open Fleet Management |
| Repository | https://github.com/officialrabe/VEHISTRA |
| License | MIT (see [`LICENSE`](LICENSE)) |
| Maintainer | LSP Virtual Services |
| Contact | support@vehistra.dev |
| Website | https://vehistra.dev |

Vehistra is a Windows desktop application for managing a company vehicle fleet
(vehicles, drivers, inspections, mileage, maintenance, damages, accidents,
workshop orders, documents). It stores its data in a database the operator runs
themselves.

## Third-party components

Every component Vehistra ships or builds with is listed with its licence in
[`THIRD-PARTY-LICENSES.md`](THIRD-PARTY-LICENSES.md). There is no proprietary,
closed-source component in the product.

One entry needs stating here rather than being left to be found: **QuestPDF**,
used to generate the PDF reports, is not under a single OSI-approved licence but
under a Community/Commercial dual licence. Its Community licence explicitly
covers open-source projects under OSI-approved licences, which is what Vehistra
is. Everything else is MIT, Apache-2.0, BSD-3-Clause or MS-PL/Apache-2.0.

## Roles

| Role | Held by | Rights |
| --- | --- | --- |
| Maintainer | LSP Virtual Services | merges pull requests, triggers releases, holds the SignPath account |
| Contributor | anyone | opens pull requests; cannot merge or release |

There is currently a single maintainer. Should that change, this document is
updated in the same pull request that grants the new rights.

## Source and review process

1. All changes arrive as pull requests against `main`.
2. Continuous integration runs on every pull request and on every push to
   `main`. It builds the whole solution in `Release`, runs the full test suite
   and builds the installers. The workflow is
   [`.github/workflows/ci.yml`](.github/workflows/ci.yml).
3. A dedicated CI job fails the build if a certificate, key file or plaintext
   credential is ever committed.
4. A release is cut only from a commit whose CI run is green.

## Build provenance

Releases are built exclusively by GitHub Actions on Microsoft-hosted
`windows-latest` runners. No artefact is ever built on a developer machine and
uploaded by hand.

- Release workflow: [`.github/workflows/release.yml`](.github/workflows/release.yml)
- It runs [`tools/ReleaseBuilder/CreateRelease.ps1`](tools/ReleaseBuilder/CreateRelease.ps1),
  the same script a contributor would run locally, so the published path is the
  documented one.
- Installers are compiled with Inno Setup 6 from
  [`tools/Installer/`](tools/Installer/).
- Every released file is listed with its SHA-256 checksum in
  `checksums.sha256`, published as a release asset.
- Release notes are generated from [`CHANGELOG.md`](CHANGELOG.md).

A release is triggered only by the maintainer, either by pushing a version tag
or by manually dispatching the release workflow with an explicit version
number.

## Release status

Version 1.3.0 is released and publicly available at
https://github.com/officialrabe/VEHISTRA/releases – in exactly the form that
should be signed. Releases from 1.3.0 onwards are regular releases, no longer
marked as pre-release.

What the application does is described in
[`README.md`](README.md), on the release page and in the six end-user guides
shipped with it (German). The guides are generated from
[`docs/`](docs/).

## Artefacts to be signed

| File | Description |
| --- | --- |
| `Vehistra-Setup.exe` | workstation installer |
| `Vehistra-Update.exe` | update package for existing installations |
| `Vehistra.exe` | main application |
| `Vehistra.Updater.exe` | applies program and database updates |
| `VehistraServerSetup.exe` | server / single-workstation setup wizard |
| `VehistraServerCheck.exe` | diagnostics tool |

## Handling of signing credentials

- No certificate, private key or signing credential is stored in the
  repository. `.gitignore` excludes `*.pfx`, `*.p12`, `*.snk`, `signing/` and
  `codesign/`, and the CI job described above enforces this.
- Signing credentials are held only as GitHub Actions secrets, accessible to
  the release workflow.
- The private key itself never leaves the SignPath Foundation HSM.

## Privacy statement

Vehistra is operated entirely within the user's own network.

- No telemetry, no tracking, no advertising components, no analytics.
- No data is transmitted to LSP Virtual Services, to SignPath or to any third
  party, neither automatically nor in the background.
- A support package, which the user creates deliberately and sends by
  themselves, contains log files, version information and a system diagnosis.
  It contains no passwords, no database credentials and no vehicle or personal
  data.
- Passwords are stored only as PBKDF2-HMAC-SHA256 hashes (210,000 iterations).
  A locally stored database password is encrypted with the Windows Data
  Protection API and never written in plaintext.

## Reporting a vulnerability

Please report security issues to support@vehistra.dev rather than opening a
public issue. We will confirm receipt and keep the reporter informed.
